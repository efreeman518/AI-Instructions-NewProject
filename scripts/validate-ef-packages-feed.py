#!/usr/bin/env python3
"""Validate EF.Packages NuGet feed and central package configuration."""

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


EF_PACKAGE_PREFIX = "EF."
SECRET_PATTERNS = [
    re.compile(r"ghp_[A-Za-z0-9_]+"),
    re.compile(r"github_pat_[A-Za-z0-9_]+"),
]

SHARED_TYPE_PATTERNS = {
    "EntityBase": r"\b(class|record)\s+EntityBase\b",
    "AuditableBase": r"\b(class|record)\s+AuditableBase\b",
    "RepositoryBase": r"\b(class|record)\s+RepositoryBase\b",
    "DbContextBase": r"\b(class|record)\s+DbContextBase\b",
    "DomainResult": r"\b(class|record|struct)\s+DomainResult\b",
    "DomainError": r"\b(class|record|struct)\s+DomainError\b",
    "Result": r"\b(class|record|struct)\s+Result\b",
    "PagedResponse": r"\b(class|record|struct)\s+PagedResponse\b",
    "SearchRequest": r"\b(class|record|struct)\s+SearchRequest\b",
    "IRepositoryBase": r"\binterface\s+IRepositoryBase\b",
    "IRequestContext": r"\binterface\s+IRequestContext\b",
    "IInternalMessageBus": r"\binterface\s+IInternalMessageBus\b",
    "IMessageHandler": r"\binterface\s+IMessageHandler\b",
}


def add_issue(issues, category, path, message):
    issues.append({
        "category": category,
        "path": str(path),
        "message": message,
    })


def local_name(tag):
    return tag.split("}", 1)[-1]


def iter_named(root, name):
    for element in root.iter():
        if local_name(element.tag) == name:
            yield element


def parse_xml(path, issues, category):
    try:
        return ET.parse(path).getroot()
    except Exception as exc:
        add_issue(issues, category, path, f"Unable to parse XML: {exc}")
        return None


def get_add_items(parent):
    return [
        element
        for element in parent.iter()
        if local_name(element.tag) == "add"
    ]


def get_sources(config_root):
    sources = {}
    for package_sources in iter_named(config_root, "packageSources"):
        for item in get_add_items(package_sources):
            key = item.attrib.get("key")
            value = item.attrib.get("value")
            if key:
                sources[key] = value or ""
    return sources


def get_source_mappings(config_root):
    mappings = {}
    for package_source_mapping in iter_named(config_root, "packageSourceMapping"):
        for package_source in package_source_mapping:
            if local_name(package_source.tag) != "packageSource":
                continue
            key = package_source.attrib.get("key")
            if not key:
                continue
            patterns = [
                package.attrib.get("pattern", "")
                for package in package_source
                if local_name(package.tag) == "package"
            ]
            mappings[key] = [pattern for pattern in patterns if pattern]
    return mappings


def get_credentials(config_root):
    credentials = {}
    for source_credentials in iter_named(config_root, "packageSourceCredentials"):
        for source in source_credentials:
            source_name = local_name(source.tag)
            values = {}
            for item in get_add_items(source):
                key = item.attrib.get("key")
                value = item.attrib.get("value")
                if key:
                    values[key] = value or ""
            credentials[source_name] = values
    return credentials


def is_nuget_org_source(key, value):
    return key.lower() == "nuget.org" or "api.nuget.org" in value.lower()


def check_nuget_config(root, config_only, require_auth_env, issues):
    config_path = root / "nuget.config"
    if not config_path.is_file():
        add_issue(issues, "NuGetConfig", config_path, "nuget.config is required before Phase 4.")
        return

    config_text = config_path.read_text(encoding="utf-8")
    if any(pattern.search(config_text) for pattern in SECRET_PATTERNS):
        add_issue(issues, "FeedAuth", config_path, "nuget.config contains a hardcoded GitHub PAT.")

    config = parse_xml(config_path, issues, "NuGetConfig")
    if config is None:
        return

    sources = get_sources(config)
    if not sources:
        add_issue(issues, "NuGetConfig", config_path, "No packageSources entries found.")
        return

    nuget_sources = {
        key: value
        for key, value in sources.items()
        if is_nuget_org_source(key, value)
    }
    if not nuget_sources:
        add_issue(issues, "NuGetConfig", config_path, "nuget.org package source is required.")

    mappings = get_source_mappings(config)
    if mappings:
        mapped_ef_sources = [
            key
            for key, patterns in mappings.items()
            if any(pattern in {EF_PACKAGE_PREFIX + "*", "EF.Packages*"} for pattern in patterns)
        ]
        if not mapped_ef_sources:
            add_issue(
                issues,
                "PackageSourceMapping",
                config_path,
                "packageSourceMapping must map EF.* packages to the private feed.",
            )

        nuget_org_patterns = []
        for key in nuget_sources:
            nuget_org_patterns.extend(mappings.get(key, []))
        if "*" not in nuget_org_patterns and "dotnet-ef" not in nuget_org_patterns:
            add_issue(
                issues,
                "PackageSourceMapping",
                config_path,
                "packageSourceMapping must allow dotnet-ef from nuget.org.",
            )
    elif not config_only:
        add_issue(
            issues,
            "PackageSourceMapping",
            config_path,
            "packageSourceMapping is required for EF.* private-feed isolation.",
        )

    if require_auth_env:
        credentials = get_credentials(config)
        private_sources = [
            key
            for key, value in sources.items()
            if key not in nuget_sources and "api.nuget.org" not in value.lower()
        ]
        for source in private_sources:
            values = credentials.get(source, {})
            password = values.get("ClearTextPassword") or values.get("Password") or ""
            env_match = re.fullmatch(r"%([A-Za-z_][A-Za-z0-9_]*)%|\$([A-Za-z_][A-Za-z0-9_]*)", password)
            if not values:
                add_issue(issues, "FeedAuth", config_path, f"Private source '{source}' has no credentials.")
            elif env_match:
                env_name = env_match.group(1) or env_match.group(2)
                if not os.environ.get(env_name):
                    add_issue(issues, "FeedAuth", config_path, f"Environment variable '{env_name}' is not set.")


def check_central_package_management(root, issues):
    directory_packages = root / "Directory.Packages.props"
    if not directory_packages.is_file():
        add_issue(issues, "CentralPackages", directory_packages, "Directory.Packages.props is required.")
        return set()

    props = parse_xml(directory_packages, issues, "CentralPackages")
    if props is None:
        return set()

    ef_package_versions = set()
    for package_version in iter_named(props, "PackageVersion"):
        package_name = package_version.attrib.get("Include") or package_version.attrib.get("Update")
        version = package_version.attrib.get("Version")
        if package_name and package_name.startswith(EF_PACKAGE_PREFIX):
            ef_package_versions.add(package_name)
            if not version:
                add_issue(
                    issues,
                    "CentralPackages",
                    directory_packages,
                    f"{package_name} PackageVersion must declare Version centrally.",
                )
            elif "*" in version:
                add_issue(
                    issues,
                    "CentralPackages",
                    directory_packages,
                    f"{package_name} uses floating Version '{version}'. Use exact versions with CPM.",
                )

    if not ef_package_versions:
        add_issue(
            issues,
            "CentralPackages",
            directory_packages,
            "No EF.* PackageVersion entries found.",
        )

    cpm_text = directory_packages.read_text(encoding="utf-8")
    directory_build = root / "Directory.Build.props"
    if directory_build.is_file():
        cpm_text += "\n" + directory_build.read_text(encoding="utf-8")
    if "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>" not in cpm_text:
        add_issue(
            issues,
            "CentralPackages",
            directory_packages,
            "ManagePackageVersionsCentrally must be true in Directory.Packages.props or Directory.Build.props.",
        )

    return ef_package_versions


def check_project_references(root, central_ef_packages, issues):
    for project_file in root.rglob("*.csproj"):
        if any(part in {".instructions", ".git", "bin", "obj", ".tmp"} for part in project_file.parts):
            continue
        project = parse_xml(project_file, issues, "ProjectPackages")
        if project is None:
            continue
        for package_reference in iter_named(project, "PackageReference"):
            package_name = package_reference.attrib.get("Include") or package_reference.attrib.get("Update")
            if not package_name or not package_name.startswith(EF_PACKAGE_PREFIX):
                continue
            if package_reference.attrib.get("Version"):
                add_issue(
                    issues,
                    "ProjectPackages",
                    project_file,
                    f"{package_name} has a project-level Version. Use Directory.Packages.props.",
                )
            if central_ef_packages and package_name not in central_ef_packages:
                add_issue(
                    issues,
                    "ProjectPackages",
                    project_file,
                    f"{package_name} is referenced but missing from Directory.Packages.props.",
                )


def check_shared_type_reimplementation(root, issues):
    source_roots = [root / "src", root / "tests"]
    for source_root in source_roots:
        if not source_root.exists():
            continue
        for code_file in source_root.rglob("*.cs"):
            if any(part in {"bin", "obj"} for part in code_file.parts):
                continue
            content = code_file.read_text(encoding="utf-8")
            for type_name, pattern in SHARED_TYPE_PATTERNS.items():
                if re.search(pattern, content):
                    add_issue(
                        issues,
                        "SharedType",
                        code_file,
                        f"{type_name} is provided by EF.Packages. Do not reimplement it.",
                    )


def print_issues(issues):
    if not issues:
        return
    issues.sort(key=lambda issue: (issue["category"], issue["path"], issue["message"]))
    width_category = max(len("Category"), *(len(issue["category"]) for issue in issues))
    width_path = max(len("Path"), *(len(issue["path"]) for issue in issues))
    print(f"{'Category':<{width_category}}  {'Path':<{width_path}}  Message")
    print("-" * (width_category + width_path + 11))
    for issue in issues:
        print(f"{issue['category']:<{width_category}}  {issue['path']:<{width_path}}  {issue['message']}")


def main():
    parser = argparse.ArgumentParser(description="Validate EF.Packages feed and package configuration.")
    parser.add_argument("--root", "-Root", default=".", help="Target app repo root.")
    parser.add_argument(
        "--config-only",
        action="store_true",
        help="Validate nuget.config only. Use during Phase 3 before projects exist.",
    )
    parser.add_argument(
        "--require-auth-env",
        action="store_true",
        help="Require credential environment variables referenced by nuget.config to be set.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    issues = []

    if not root.exists():
        add_issue(issues, "Root", root, "Target root does not exist.")
    else:
        check_nuget_config(root, args.config_only, args.require_auth_env, issues)
        if not args.config_only:
            central_ef_packages = check_central_package_management(root, issues)
            check_project_references(root, central_ef_packages, issues)
            check_shared_type_reimplementation(root, issues)

    if issues:
        print(f"FAIL: EF.Packages feed validation found {len(issues)} issue(s).")
        print()
        print_issues(issues)
        return 1

    mode = "config" if args.config_only else "project"
    print(f"PASS: EF.Packages {mode} validation passed for {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
