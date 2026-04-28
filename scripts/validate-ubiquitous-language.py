#!/usr/bin/env python3
"""Validate Phase 1 language and design-decision artifacts."""

import argparse
import re
import sys
from pathlib import Path


STATUS_VALUES = {"proposed", "confirmed", "defaulted", "deferred", "superseded"}
IGNORED_TERMS = {"None"}


def add_issue(issues, category, path, message):
    issues.append({
        "category": category,
        "path": str(path),
        "message": message,
    })


def read_text(path):
    return path.read_text(encoding="utf-8") if path.is_file() else ""


def get_section(content, heading):
    start_match = re.search(rf"(?m)^{re.escape(heading)}:\s*$", content)
    if not start_match:
        return ""

    start = start_match.start()
    next_top_level = re.search(r"(?m)^[A-Za-z][A-Za-z0-9]*:", content[start_match.end():])
    if next_top_level:
        return content[start:start_match.end() + next_top_level.start()]
    return content[start:]


def get_entity_blocks(content):
    section = get_section(content, "entities")
    matches = list(re.finditer(r"(?m)^  -\s*name:\s*([A-Z][A-Za-z0-9]*)\b", section))
    blocks = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(section)
        blocks.append((match.group(1), section[match.start():end]))
    return blocks


def split_inline_list(value):
    return [
        item.strip().strip("\"'")
        for item in value.split(",")
        if item.strip()
    ]


def collect_terms(domain_content):
    terms = set()

    entity_blocks = get_entity_blocks(domain_content)
    for entity_name, block in entity_blocks:
        terms.add(entity_name)

        for match in re.finditer(r"(?m)^\s*-?\s*\{\s*name:\s*([A-Z][A-Za-z0-9]*)\b[^\n]*(?:Value object|value object)", block):
            terms.add(match.group(1))

        for states_match in re.finditer(r"(?m)^\s*states:\s*\[([^\]]+)\]", block):
            terms.update(split_inline_list(states_match.group(1)))

        for action_match in re.finditer(r"\baction:\s*([A-Z][A-Za-z0-9]*)\b", block):
            terms.add(action_match.group(1))

        for custom_action_match in re.finditer(r"(?m)^\s*-\s*\{\s*name:\s*([A-Z][A-Za-z0-9]*)\b", get_section(block, "customActions")):
            terms.add(custom_action_match.group(1))

    for event_match in re.finditer(r"(?m)^  -\s*name:\s*([A-Z][A-Za-z0-9]*)\b", get_section(domain_content, "events")):
        terms.add(event_match.group(1))

    for rule_match in re.finditer(r"(?m)^  -\s*name:\s*([A-Z][A-Za-z0-9]*)\b", get_section(domain_content, "domainRules")):
        terms.add(rule_match.group(1))

    for policy_match in re.finditer(r"(?m)^  -\s*name:\s*([A-Z][A-Za-z0-9]*)\b", get_section(domain_content, "policyMatrices")):
        terms.add(policy_match.group(1))

    for key in ["globalAdminRole", "authProvider", "authScenario"]:
        match = re.search(rf"(?m)^{key}:\s*([A-Za-z][A-Za-z0-9]*)\b", domain_content)
        if match:
            terms.add(match.group(1))

    return sorted(term for term in terms if term and term not in IGNORED_TERMS)


def check_language_terms(terms, language_content, language_path, issues):
    for term in terms:
        if not re.search(rf"\b{re.escape(term)}\b", language_content):
            add_issue(
                issues,
                "LanguageTerm",
                language_path,
                f"Domain term is missing from UBIQUITOUS-LANGUAGE.md: {term}",
            )


def check_decisions(decision_content, decision_path, issues):
    decision_ids = re.findall(r"\bD-\d{3,}\b", decision_content)
    if not decision_ids:
        add_issue(
            issues,
            "Decision",
            decision_path,
            "DESIGN-DECISIONS.md must contain at least one D-### decision record.",
        )

    if "Decision Dependency Graph" not in decision_content:
        add_issue(
            issues,
            "Decision",
            decision_path,
            "DESIGN-DECISIONS.md must include a Decision Dependency Graph section.",
        )

    lower_content = decision_content.lower()
    if not any(status in lower_content for status in STATUS_VALUES):
        add_issue(
            issues,
            "DecisionStatus",
            decision_path,
            "DESIGN-DECISIONS.md must use decision statuses: proposed, confirmed, defaulted, deferred, or superseded.",
        )

    for placeholder in ["{{ProjectName}}", "_Decision being made._", "_Chosen option._"]:
        if placeholder in decision_content:
            add_issue(
                issues,
                "Placeholder",
                decision_path,
                f"Unresolved decision placeholder remains: {placeholder}",
            )
            break


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
    parser = argparse.ArgumentParser(description="Validate Phase 1 language and decision artifacts.")
    parser.add_argument("--root", "-Root", default=".", help="Target app repo root.")
    parser.add_argument("--domain-spec", default="domain-specification.yaml", help="Domain spec path relative to root.")
    parser.add_argument("--language", default="UBIQUITOUS-LANGUAGE.md", help="Language artifact path relative to root.")
    parser.add_argument("--decisions", default="DESIGN-DECISIONS.md", help="Design decisions path relative to root.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    domain_path = root / args.domain_spec
    language_path = root / args.language
    decision_path = root / args.decisions
    issues = []

    if not domain_path.is_file():
        add_issue(issues, "File", domain_path, "domain-specification.yaml is missing.")
    if not language_path.is_file():
        add_issue(issues, "File", language_path, "UBIQUITOUS-LANGUAGE.md is missing.")
    if not decision_path.is_file():
        add_issue(issues, "File", decision_path, "DESIGN-DECISIONS.md is missing.")

    if not issues:
        domain_content = read_text(domain_path)
        language_content = read_text(language_path)
        decision_content = read_text(decision_path)

        terms = collect_terms(domain_content)
        if not terms:
            add_issue(issues, "Domain", domain_path, "No domain terms found to validate.")
        else:
            check_language_terms(terms, language_content, language_path, issues)
        check_decisions(decision_content, decision_path, issues)

    if issues:
        print(f"FAIL: ubiquitous language validation found {len(issues)} issue(s).")
        print()
        print_issues(issues)
        return 1

    print(f"PASS: ubiquitous language validation passed for {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
