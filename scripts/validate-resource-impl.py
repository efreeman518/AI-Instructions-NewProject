#!/usr/bin/env python3
"""Validate a resource implementation YAML file."""

import argparse
import re
import sys
from pathlib import Path


def add_issue(category, file_path, message, line=0):
    return {"Category": category, "File": file_path, "Line": line, "Message": message}


def main():
    parser = argparse.ArgumentParser(description="Validate a resource implementation file")
    parser.add_argument("--file-path", "-FilePath", required=True,
                        help="Path to the resource implementation file")
    parser.add_argument("--domain-spec-path", "-DomainSpecPath", default=None,
                        help="Path to the domain specification file for cross-reference")
    args = parser.parse_args()

    file_path = args.file_path
    domain_spec_path = args.domain_spec_path

    if not Path(file_path).exists():
        print(f"File not found: {file_path}", file=sys.stderr)
        sys.exit(1)

    issues = []
    content = Path(file_path).read_text(encoding="utf-8")

    # -------------------------------------------------------------------------
    # 1) Required top-level keys
    # -------------------------------------------------------------------------
    required_keys = ["scaffoldMode", "testingProfile", "entities"]
    for key in required_keys:
        if not re.search(rf"(?m)^{key}:", content):
            issues.append(add_issue("MissingKey", file_path,
                                    f"Required top-level key missing: {key}"))

    # -------------------------------------------------------------------------
    # 2) scaffoldMode must be valid
    # -------------------------------------------------------------------------
    mode_match = re.search(r"(?m)^scaffoldMode:\s*([\w-]+)", content)
    if mode_match:
        mode = mode_match.group(1)
        if mode not in ("full", "lite", "api-only"):
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"scaffoldMode '{mode}' is not valid. Use: full, lite, api-only."
            ))

    # -------------------------------------------------------------------------
    # 3) testingProfile must be valid
    # -------------------------------------------------------------------------
    profile_match = re.search(r"(?m)^testingProfile:\s*(\w+)", content)
    if profile_match:
        profile = profile_match.group(1)
        if profile not in ("minimal", "balanced", "comprehensive"):
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"testingProfile '{profile}' is not valid. Use: minimal, balanced, comprehensive."
            ))

    # -------------------------------------------------------------------------
    # 4) Entity dataStore must be a known type
    # -------------------------------------------------------------------------
    valid_stores = ["sql", "sqlServer", "cosmosDb", "tableStorage",
                    "blobStorage", "redis", "inMemory"]
    store_matches = re.findall(r"(?m)dataStore:\s*(\w+)", content)
    for store in store_matches:
        if store not in valid_stores:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"dataStore '{store}' is not a recognized store type. "
                f"Valid: {', '.join(valid_stores)}."
            ))

    # -------------------------------------------------------------------------
    # 5) Cross-reference: entities match domain spec (if provided)
    # -------------------------------------------------------------------------
    if domain_spec_path and Path(domain_spec_path).exists():
        domain_content = Path(domain_spec_path).read_text(encoding="utf-8")

        # Scope to ## Entities section only
        domain_entities_section = ""
        domain_section_match = re.search(
            r"(?ms)^## Entities\s*\r?\n(.*?)(?=\r?\n## |\Z)", domain_content
        )
        if domain_section_match:
            domain_entities_section = domain_section_match.group(1)

        domain_entities = sorted(set(
            re.findall(r"(?m)^\s+-?\s*name:\s*(\w+)", domain_entities_section)
        ))

        # Scope to ## Entity-to-Store Mapping section only
        store_map_section = ""
        store_map_match = re.search(
            r"(?ms)^## Entity-to-Store Mapping\s*\r?\n(.*?)(?=\r?\n## |\Z)", content
        )
        if store_map_match:
            store_map_section = store_map_match.group(1)

        resource_entities = sorted(set(
            re.findall(r"(?m)^  -\s*name:\s*(\w+)", store_map_section)
        ))

        for domain_entity in domain_entities:
            if domain_entity not in resource_entities:
                issues.append(add_issue(
                    "CrossReference", file_path,
                    f"Domain entity '{domain_entity}' not found in resource implementation."
                ))

        for res_entity in resource_entities:
            if res_entity not in domain_entities:
                issues.append(add_issue(
                    "CrossReference", file_path,
                    f"Resource entity '{res_entity}' not found in domain specification."
                ))

    # -------------------------------------------------------------------------
    # 6) maxLength values must be positive integers
    # -------------------------------------------------------------------------
    max_length_matches = re.findall(r"(?m)maxLength:\s*(-?\d+)", content)
    for val_str in max_length_matches:
        val = int(val_str)
        if val <= 0:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"maxLength value {val} must be a positive integer."
            ))

    # -------------------------------------------------------------------------
    # 7) precision format check (e.g., "decimal(10,4)")
    # -------------------------------------------------------------------------
    precision_matches = re.findall(
        r'(?m)precision:\s*"?decimal\((\d+),(\d+)\)"?', content
    )
    for p_str, s_str in precision_matches:
        p = int(p_str)
        s = int(s_str)
        if s >= p:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"Decimal precision scale ({s}) must be less than precision ({p})."
            ))

    # -------------------------------------------------------------------------
    # Output
    # -------------------------------------------------------------------------
    if not issues:
        print(f"PASS: resource implementation validation passed for {file_path}")
        sys.exit(0)

    print(f"FAIL: resource implementation validation found {len(issues)} issue(s).")

    # Sort and print table
    issues.sort(key=lambda x: x["Category"])

    col_cat = max((len(i["Category"]) for i in issues), default=8)
    col_msg = max((len(i["Message"]) for i in issues), default=7)

    col_cat = max(col_cat, len("Category"))
    col_msg = max(col_msg, len("Message"))

    header = f"{'Category':<{col_cat}}  {'Message':<{col_msg}}"
    print()
    print(header)
    print("-" * len(header))
    for issue in issues:
        print(f"{issue['Category']:<{col_cat}}  {issue['Message']:<{col_msg}}")
    print()

    sys.exit(1)


if __name__ == "__main__":
    main()
