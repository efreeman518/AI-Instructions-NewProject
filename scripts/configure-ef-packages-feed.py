#!/usr/bin/env python3
"""Configure nuget.config for the private EF.Packages feed without writing secrets."""

import argparse
import copy
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


NUGET_ORG_URL = "https://api.nuget.org/v3/index.json"
DEFAULT_NUGET_PATTERNS = [
    "dotnet-ef",
    "Microsoft.*",
    "Microsoft.Extensions.*",
    "System.*",
    "Azure.*",
    "Aspire.*",
    "OpenTelemetry.*",
]
SECRET_PATTERNS = [
    re.compile(r"ghp_[A-Za-z0-9_]+"),
    re.compile(r"github_pat_[A-Za-z0-9_]+"),
]


def local_name(tag):
    return tag.split("}", 1)[-1]


def find_child(parent, name):
    for child in parent:
        if local_name(child.tag) == name:
            return child
    return None


def ensure_child(parent, name):
    child = find_child(parent, name)
    if child is not None:
        return child, False
    return ET.SubElement(parent, name), True


def find_add(parent, key):
    for child in parent:
        if local_name(child.tag) == "add" and child.attrib.get("key") == key:
            return child
    return None


def ensure_add(parent, key, value):
    item = find_add(parent, key)
    if item is None:
        ET.SubElement(parent, "add", {"key": key, "value": value})
        return True
    if item.attrib.get("value") != value:
        item.set("value", value)
        return True
    return False


def source_key_for_nuget_org(package_sources):
    for item in package_sources:
        if local_name(item.tag) != "add":
            continue
        key = item.attrib.get("key", "")
        value = item.attrib.get("value", "")
        if key.lower() == "nuget.org" or "api.nuget.org" in value.lower():
            return key
    return "nuget.org"


def find_package_source(mapping, key):
    for child in mapping:
        if local_name(child.tag) == "packageSource" and child.attrib.get("key") == key:
            return child
    return None


def ensure_package_source(mapping, key):
    source = find_package_source(mapping, key)
    if source is not None:
        return source, False
    return ET.SubElement(mapping, "packageSource", {"key": key}), True


def package_patterns(package_source):
    return [
        child.attrib.get("pattern", "")
        for child in package_source
        if local_name(child.tag) == "package"
    ]


def ensure_package_pattern(package_source, pattern):
    patterns = package_patterns(package_source)
    if "*" in patterns or pattern in patterns:
        return False
    ET.SubElement(package_source, "package", {"pattern": pattern})
    return True


def ensure_credentials(credentials, source_name, username, token_env):
    source = find_child(credentials, source_name)
    changed = False
    if source is None:
        source = ET.SubElement(credentials, source_name)
        changed = True
    changed = ensure_add(source, "Username", username) or changed
    changed = ensure_add(source, "ClearTextPassword", f"%{token_env}%") or changed
    return changed


def has_hardcoded_pat(text):
    return any(pattern.search(text) for pattern in SECRET_PATTERNS)


def read_or_create_config(config_path):
    if not config_path.exists():
        return ET.Element("configuration"), True
    try:
        return ET.parse(config_path).getroot(), False
    except Exception as exc:
        print(f"error: unable to parse {config_path}: {exc}", file=sys.stderr)
        raise SystemExit(1)


def configure(root, feed_url, source_name, username, token_env):
    config_path = root / "nuget.config"
    config, created = read_or_create_config(config_path)
    changed = created

    package_sources, child_changed = ensure_child(config, "packageSources")
    changed = child_changed or changed
    nuget_key = source_key_for_nuget_org(package_sources)
    changed = ensure_add(package_sources, nuget_key, NUGET_ORG_URL) or changed
    changed = ensure_add(package_sources, source_name, feed_url) or changed

    mapping, child_changed = ensure_child(config, "packageSourceMapping")
    changed = child_changed or changed

    nuget_mapping, child_changed = ensure_package_source(mapping, nuget_key)
    changed = child_changed or changed
    for pattern in DEFAULT_NUGET_PATTERNS:
        changed = ensure_package_pattern(nuget_mapping, pattern) or changed

    ef_mapping, child_changed = ensure_package_source(mapping, source_name)
    changed = child_changed or changed
    changed = ensure_package_pattern(ef_mapping, "EF.*") or changed

    credentials, child_changed = ensure_child(config, "packageSourceCredentials")
    changed = child_changed or changed
    changed = ensure_credentials(credentials, source_name, username, token_env) or changed

    return config, changed


def serialize_xml(config):
    ET.indent(config, space="  ")
    return ET.tostring(config, encoding="unicode")


def default_username():
    return (
        os.environ.get("GITHUB_ACTOR")
        or os.environ.get("GITHUB_USER")
        or os.environ.get("USERNAME")
        or os.environ.get("USER")
        or "github"
    )


def main():
    parser = argparse.ArgumentParser(
        description="Create or update nuget.config for EF.Packages private feed auth.",
    )
    parser.add_argument("--root", "-Root", default=".", help="Target app repo root.")
    parser.add_argument(
        "--feed-url",
        default=os.environ.get("EF_PACKAGES_FEED_URL"),
        help="Private feed URL, e.g. https://nuget.pkg.github.com/{owner}/index.json. "
             "Can also be set with EF_PACKAGES_FEED_URL.",
    )
    parser.add_argument("--source-name", default="efpackages", help="NuGet package source key.")
    parser.add_argument("--username", default=default_username(), help="GitHub Packages username.")
    parser.add_argument("--token-env", default="NUGET_AUTH_TOKEN", help="Environment variable used for PAT.")
    parser.add_argument("--check-only", action="store_true", help="Fail if nuget.config would change.")
    parser.add_argument("--dry-run", action="store_true", help="Print generated nuget.config without writing.")
    parser.add_argument(
        "--require-auth-env",
        action="store_true",
        help="Fail if the token environment variable is not set.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.exists():
        print(f"error: root does not exist: {root}", file=sys.stderr)
        return 1
    if not args.feed_url:
        print("error: --feed-url is required unless EF_PACKAGES_FEED_URL is set.", file=sys.stderr)
        return 1
    if args.require_auth_env and not os.environ.get(args.token_env):
        print(f"error: environment variable '{args.token_env}' is not set.", file=sys.stderr)
        return 1

    original_path = root / "nuget.config"
    original_text = original_path.read_text(encoding="utf-8") if original_path.exists() else ""
    config, changed = configure(root, args.feed_url, args.source_name, args.username, args.token_env)
    output = '<?xml version="1.0" encoding="utf-8"?>\n' + serialize_xml(copy.deepcopy(config)) + "\n"

    if has_hardcoded_pat(output):
        print("error: generated nuget.config contains a hardcoded GitHub PAT.", file=sys.stderr)
        return 1

    if args.check_only:
        if changed:
            print("FAIL: nuget.config is missing required EF.Packages feed settings.")
            return 1
        if has_hardcoded_pat(original_text):
            print("FAIL: nuget.config contains a hardcoded GitHub PAT.")
            return 1
        print("PASS: nuget.config already contains required EF.Packages feed settings.")
        return 0

    if args.dry_run:
        print(output, end="")
        return 0

    original_path.write_text(output, encoding="utf-8", newline="\n")
    action = "Updated" if original_text else "Created"
    print(f"{action} {original_path}")
    print(f"Credential uses %{args.token_env}% only. No PAT was written.")
    print("Next: run `dotnet restore` to confirm the feed authenticates correctly.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
