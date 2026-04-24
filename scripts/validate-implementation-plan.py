#!/usr/bin/env python3
"""Validate Phase 3 implementation-plan.md readiness before code generation."""

import argparse
import re
import sys
from pathlib import Path


REQUIRED_HEADINGS = [
    "Inputs Summary",
    "Implementation Steps",
    "Phase 4",
    "Phase 5a",
    "Phase 5b",
    "Phase 5c",
    "Open Questions",
    "Decisions Log",
    "Tooling & Environment Readiness",
    "Required CLIs",
    "EF.Packages Feed Readiness",
    "Risk / Blockers",
]
REQUIRED_PHRASES = [
    "scaffoldMode",
    "testingProfile",
    "dotnet-ef",
    "nuget.config",
    "NUGET_AUTH_TOKEN",
    "packageSourceMapping",
    "Directory.Packages.props",
    "validate-ef-packages-feed.py",
    "--config-only --require-auth-env",
    "CLI",
    "MCP",
]
PLACEHOLDER_PATTERNS = [
    re.compile(r"\{\{[^}]+\}\}"),
    re.compile(r"_\["),
    re.compile(r"\be\.g\.,"),
]


def add_issue(issues, category, path, message):
    issues.append({
        "category": category,
        "path": str(path),
        "message": message,
    })


def read_text(path):
    return path.read_text(encoding="utf-8") if path.is_file() else ""


def get_scalar(content, key):
    match = re.search(rf"(?m)^{re.escape(key)}:\s*([A-Za-z0-9_.-]+)", content)
    return match.group(1) if match else ""


def check_headings(content, path, issues):
    for heading in REQUIRED_HEADINGS:
        if not re.search(rf"(?mi)^#+\s+.*{re.escape(heading)}\b", content):
            add_issue(issues, "Heading", path, f"Missing required section: {heading}.")


def check_required_phrases(content, path, issues):
    for phrase in REQUIRED_PHRASES:
        if phrase not in content:
            add_issue(issues, "Content", path, f"Missing required implementation-plan content: {phrase}.")


def check_placeholders(content, path, allow_template_placeholders, issues):
    if allow_template_placeholders:
        return
    for pattern in PLACEHOLDER_PATTERNS:
        match = pattern.search(content)
        if match:
            add_issue(issues, "Placeholder", path, f"Unresolved template placeholder remains: {match.group(0)}.")
            return


def check_resource_alignment(root, content, path, issues):
    resource_path = root / "resource-implementation.yaml"
    if not resource_path.is_file():
        return

    resource = read_text(resource_path)
    for key in ["scaffoldMode", "testingProfile"]:
        value = get_scalar(resource, key)
        if value and value not in content:
            add_issue(
                issues,
                "ResourceAlignment",
                path,
                f"Plan does not mention {key} value from resource-implementation.yaml: {value}.",
            )

    enabled_flags = re.findall(r"(?m)^(include[A-Za-z0-9]+):\s*true\b", resource)
    for flag in enabled_flags:
        if flag not in content:
            add_issue(
                issues,
                "ResourceAlignment",
                path,
                f"Plan does not mention enabled resource flag: {flag}.",
            )

    if "customNugetFeeds" in resource and "EF.Packages Feed Readiness" not in content:
        add_issue(
            issues,
            "ResourceAlignment",
            path,
            "Resource mapping declares customNugetFeeds but plan lacks EF.Packages Feed Readiness.",
        )


def check_tooling_tables(content, path, issues):
    required_cli_section = re.search(
        r"(?mis)^#+\s+Required CLIs\b(?P<body>.*?)(^#+\s+|\Z)",
        content,
    )
    if not required_cli_section:
        return
    body = required_cli_section.group("body")
    if "[ ]" not in body and "[x]" not in body.lower():
        add_issue(
            issues,
            "Tooling",
            path,
            "Required CLIs section must record verification checkboxes.",
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
    parser = argparse.ArgumentParser(description="Validate implementation-plan.md readiness.")
    parser.add_argument("--root", "-Root", default=".", help="Target app repo root.")
    parser.add_argument(
        "--file",
        default="implementation-plan.md",
        help="Implementation plan path relative to root.",
    )
    parser.add_argument(
        "--allow-template-placeholders",
        action="store_true",
        help="Allow {{ProjectName}} and example placeholders when checking the instruction template.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    path = root / args.file
    issues = []

    if not path.is_file():
        add_issue(issues, "File", path, "implementation-plan.md is missing.")
    else:
        content = read_text(path)
        check_headings(content, path, issues)
        check_required_phrases(content, path, issues)
        check_placeholders(content, path, args.allow_template_placeholders, issues)
        check_resource_alignment(root, content, path, issues)
        check_tooling_tables(content, path, issues)

    if issues:
        print(f"FAIL: implementation plan validation found {len(issues)} issue(s).")
        print()
        print_issues(issues)
        return 1

    print(f"PASS: implementation plan validation passed for {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
