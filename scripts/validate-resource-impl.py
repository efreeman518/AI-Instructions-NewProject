#!/usr/bin/env python3
"""Validate a resource implementation YAML file."""

import argparse
import json
import re
import sys
from pathlib import Path


def add_issue(category, file_path, message, line=0):
    return {"Category": category, "File": file_path, "Line": line, "Message": message}


def load_schema(schema_path):
    return json.loads(Path(schema_path).read_text(encoding="utf-8"))


def get_entities_section(content):
    section_match = re.search(r"(?ms)^## Entities\s*\r?\n(.*?)(?=\r?\n## |\Z)", content)
    if section_match:
        return section_match.group(1)

    start_match = re.search(r"(?m)^entities:\s*$", content)
    if not start_match:
        return ""

    start = start_match.start()
    next_top_level = re.search(r"(?m)^[A-Za-z][A-Za-z0-9]*:", content[start_match.end():])
    if next_top_level:
        return content[start:start_match.end() + next_top_level.start()]
    return content[start:]


def get_entity_names(entities_section):
    return re.findall(r"(?m)^  -\s*name:\s*([A-Za-z][A-Za-z0-9]*)\b", entities_section)


def main():
    parser = argparse.ArgumentParser(description="Validate a resource implementation file")
    parser.add_argument("--file-path", "-FilePath", required=True,
                        help="Path to the resource implementation file")
    parser.add_argument("--domain-spec-path", "-DomainSpecPath", default=None,
                        help="Path to the domain specification file for cross-reference")
    parser.add_argument("--schema-path", "-SchemaPath", default=None,
                        help="Path to resource-implementation.schema.json")
    args = parser.parse_args()

    file_path = args.file_path
    domain_spec_path = args.domain_spec_path
    schema_path = (
        Path(args.schema_path)
        if args.schema_path
        else Path(__file__).resolve().parent.parent / "schemas" / "resource-implementation.schema.json"
    )

    if not Path(file_path).exists():
        print(f"File not found: {file_path}", file=sys.stderr)
        sys.exit(1)

    issues = []
    content = Path(file_path).read_text(encoding="utf-8")
    schema = load_schema(schema_path)

    # -------------------------------------------------------------------------
    # 1) Required top-level keys
    # -------------------------------------------------------------------------
    required_keys = schema.get("required", [])
    for key in required_keys:
        if not re.search(rf"(?m)^{key}:", content):
            issues.append(add_issue("MissingKey", file_path,
                                    f"Required top-level key missing: {key}"))

    # -------------------------------------------------------------------------
    # 2) scaffoldMode must be valid
    # -------------------------------------------------------------------------
    scaffold_mode_values = schema["properties"]["scaffoldMode"]["enum"]
    mode_match = re.search(r"(?m)^scaffoldMode:\s*([\w-]+)", content)
    if mode_match:
        mode = mode_match.group(1)
        if mode not in scaffold_mode_values:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"scaffoldMode '{mode}' is not valid. Use: {', '.join(scaffold_mode_values)}."
            ))

    # -------------------------------------------------------------------------
    # 3) testingProfile must be valid
    # -------------------------------------------------------------------------
    testing_profile_values = schema["properties"]["testingProfile"]["enum"]
    profile_match = re.search(r"(?m)^testingProfile:\s*(\w+)", content)
    if profile_match:
        profile = profile_match.group(1)
        if profile not in testing_profile_values:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"testingProfile '{profile}' is not valid. Use: {', '.join(testing_profile_values)}."
            ))

    # -------------------------------------------------------------------------
    # 4) Entity dataStore must be a known type
    # -------------------------------------------------------------------------
    valid_stores = schema["$defs"]["entityMapping"]["properties"]["dataStore"]["enum"]
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
        domain_entities = sorted(set(get_entity_names(get_entities_section(domain_content))))
        resource_entities = sorted(set(get_entity_names(get_entities_section(content))))

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

    inline_precision_matches = re.findall(
        r"(?m)precision:\s*(\d+).*scale:\s*(\d+)", content
    )
    for p_str, s_str in inline_precision_matches:
        p = int(p_str)
        s = int(s_str)
        if p <= 0:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"Decimal precision {p} must be a positive integer."
            ))
        if s < 0:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"Decimal scale {s} must be zero or greater."
            ))
        if s >= p:
            issues.append(add_issue(
                "InvalidValue", file_path,
                f"Decimal precision scale ({s}) must be less than precision ({p})."
            ))

    dependency_modes = schema["$defs"]["dependencyMode"]["enum"]
    external_modes_section_match = re.search(
        r"(?ms)^externalDependencyModes:\s*\r?\n(.*?)(?=\r?\n[A-Za-z][A-Za-z0-9]*:|\Z)",
        content,
    )
    if external_modes_section_match:
        mode_values = re.findall(r"(?m)^\s+[A-Za-z][A-Za-z0-9]*:\s*([A-Za-z0-9 -]+)\s*$",
                                 external_modes_section_match.group(1))
        for mode in mode_values:
            cleaned = mode.strip()
            if cleaned and cleaned not in dependency_modes:
                issues.append(add_issue(
                    "InvalidValue", file_path,
                    f"externalDependencyModes value '{cleaned}' is not valid. "
                    f"Use: {', '.join(dependency_modes)}."
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
