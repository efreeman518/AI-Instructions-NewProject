#!/usr/bin/env python3
"""Validate HANDOFF.md so AI sessions can resume without rereading the full repo."""

import argparse
import re
import sys
from pathlib import Path


VALID_PHASES = {"1", "2", "3", "4", "5"}
VALID_SUBPHASES = {"5a", "5b", "5c", "5d", "5e", "5f", "5g"}
VALID_MODES = {"full", "lite", "api-only"}
VALID_TESTING_PROFILES = {"minimal", "balanced", "comprehensive"}
VALID_TEST_STATUS = {"not-started", "red", "green"}
VALID_HOST_STATUS = {"not-started", "scaffolded", "validated"}
REQUIRED_TOP_LEVEL = [
    "instructionVersion",
    "currentPhase",
    "scaffoldMode",
    "testingProfile",
    "contractsScaffolded",
    "enabledFeatures",
    "testStatus",
    "resumeCommand",
    "toolingNotes",
    "instructionGapsPath",
]
REQUIRED_HEADINGS = [
    "Next Step",
    "Next Load Set",
    "Environment Setup",
    "Current Objective",
    "Deferred",
    "Blockers",
    "Completed",
    "Validation",
]


def add_issue(issues, category, path, message):
    issues.append({
        "category": category,
        "path": str(path),
        "message": message,
    })


def strip_comment(line):
    in_single = False
    in_double = False
    for index, char in enumerate(line):
        if char == "'" and not in_double:
            in_single = not in_single
        elif char == '"' and not in_single:
            in_double = not in_double
        elif char == "#" and not in_single and not in_double:
            return line[:index]
    return line


def parse_scalar(value):
    value = value.strip()
    if value in {'""', "''"}:
        return ""
    if (value.startswith('"') and value.endswith('"')) or (
        value.startswith("'") and value.endswith("'")
    ):
        return value[1:-1]
    if value.lower() == "true":
        return True
    if value.lower() == "false":
        return False
    return value


def extract_yaml_block(content):
    match = re.search(r"```ya?ml\s*\n(.*?)\n```", content, re.IGNORECASE | re.DOTALL)
    if match:
        return match.group(1)
    return content


def parse_simple_yaml(content):
    result = {}
    stack = [(-1, result)]
    for raw_line in content.splitlines():
        line = strip_comment(raw_line).rstrip()
        if not line.strip():
            continue
        match = re.match(r"^(\s*)([A-Za-z][A-Za-z0-9]*):(?:\s*(.*))?$", line)
        if not match:
            continue
        indent = len(match.group(1).replace("\t", "    "))
        key = match.group(2)
        value = match.group(3) or ""
        while stack and indent <= stack[-1][0]:
            stack.pop()
        parent = stack[-1][1]
        if value.strip() == "":
            node = {}
            parent[key] = node
            stack.append((indent, node))
        else:
            parent[key] = parse_scalar(value)
    return result


def require_value(data, key, issues, path):
    value = data.get(key)
    if value is None or value == "":
        add_issue(issues, "Yaml", path, f"Required key '{key}' is empty or missing.")
        return None
    return value


def check_headings(content, path, issues):
    for heading in REQUIRED_HEADINGS:
        if not re.search(rf"(?m)^##+\s+{re.escape(heading)}\s*$", content):
            add_issue(issues, "Heading", path, f"Missing section heading: {heading}.")


def check_yaml(data, path, issues):
    for key in REQUIRED_TOP_LEVEL:
        if key not in data:
            add_issue(issues, "Yaml", path, f"Required key '{key}' is missing.")

    phase = str(require_value(data, "currentPhase", issues, path) or "")
    if phase and phase not in VALID_PHASES:
        add_issue(issues, "Phase", path, f"currentPhase must be one of {sorted(VALID_PHASES)}.")

    subphase = str(data.get("currentSubPhase") or "")
    if phase == "5":
        if subphase not in VALID_SUBPHASES:
            add_issue(issues, "Phase", path, "Phase 5 requires currentSubPhase 5a-5g.")
    elif subphase:
        add_issue(issues, "Phase", path, "currentSubPhase must be empty unless currentPhase is 5.")

    mode = str(require_value(data, "scaffoldMode", issues, path) or "")
    if mode and mode not in VALID_MODES:
        add_issue(issues, "Mode", path, f"scaffoldMode must be one of {sorted(VALID_MODES)}.")

    profile = str(require_value(data, "testingProfile", issues, path) or "")
    if profile and profile not in VALID_TESTING_PROFILES:
        add_issue(
            issues,
            "Mode",
            path,
            f"testingProfile must be one of {sorted(VALID_TESTING_PROFILES)}.",
        )

    contracts = data.get("contractsScaffolded")
    if not isinstance(contracts, bool):
        add_issue(issues, "Yaml", path, "contractsScaffolded must be true or false.")
    elif phase == "5" and not contracts:
        add_issue(issues, "Phase", path, "Phase 5 requires contractsScaffolded: true.")

    enabled_features = data.get("enabledFeatures")
    if not isinstance(enabled_features, dict) or not enabled_features:
        add_issue(issues, "Yaml", path, "enabledFeatures must be a populated mapping.")
    elif any(not isinstance(value, bool) for value in enabled_features.values()):
        add_issue(issues, "Yaml", path, "enabledFeatures values must be true or false.")

    test_status = data.get("testStatus")
    if not isinstance(test_status, dict) or not test_status:
        add_issue(issues, "Yaml", path, "testStatus must be a populated mapping.")
    else:
        for key, value in test_status.items():
            if str(value) not in VALID_TEST_STATUS:
                add_issue(issues, "TestStatus", path, f"{key} status '{value}' is invalid.")

    host_gates = data.get("hostGates")
    if isinstance(host_gates, dict):
        for key, value in host_gates.items():
            if str(value) not in VALID_HOST_STATUS:
                add_issue(issues, "HostGate", path, f"{key} status '{value}' is invalid.")

    if not str(data.get("resumeCommand") or "").strip():
        add_issue(issues, "Resume", path, "resumeCommand must contain the next-session prompt.")


def check_resume_content(content, path, issues):
    if "START-AI.md" not in content:
        add_issue(issues, "Resume", path, "Next load set must include START-AI.md.")
    if "HANDOFF.md" not in content:
        add_issue(issues, "Resume", path, "Next load set must include HANDOFF.md.")

    if re.search(r"https?://(localhost|127\.0\.0\.1)(:\d+)?", content, re.IGNORECASE):
        add_issue(
            issues,
            "EphemeralUrl",
            path,
            "Do not record runtime localhost URLs in HANDOFF.md; record discovery method instead.",
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
    parser = argparse.ArgumentParser(description="Validate target-project HANDOFF.md.")
    parser.add_argument("--root", "-Root", default=".", help="Target app repo root.")
    parser.add_argument("--file", default="HANDOFF.md", help="Handoff path relative to root.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    path = (root / args.file).resolve()
    issues = []

    if not path.is_file():
        add_issue(issues, "File", path, "HANDOFF.md is missing.")
    else:
        content = path.read_text(encoding="utf-8")
        data = parse_simple_yaml(extract_yaml_block(content))
        check_yaml(data, path, issues)
        check_headings(content, path, issues)
        check_resume_content(content, path, issues)

    if issues:
        print(f"FAIL: HANDOFF validation found {len(issues)} issue(s).")
        print()
        print_issues(issues)
        return 1

    print(f"PASS: HANDOFF validation passed for {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
