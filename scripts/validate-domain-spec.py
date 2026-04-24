#!/usr/bin/env python3
"""Validate a domain specification YAML file."""

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path


def add_issue(category, file_path, message, line=0):
    return {"Category": category, "File": file_path, "Line": line, "Message": message}


def load_schema(schema_path):
    return json.loads(Path(schema_path).read_text(encoding="utf-8"))


def get_entities_section(content):
    # Support both markdown-embedded specs (## Entities header) and raw YAML.
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
    parser = argparse.ArgumentParser(description="Validate a domain specification file")
    parser.add_argument("--file-path", "-FilePath", required=True,
                        help="Path to the domain specification file")
    parser.add_argument("--schema-path", "-SchemaPath", default=None,
                        help="Path to domain-specification.schema.json")
    args = parser.parse_args()

    file_path = args.file_path
    schema_path = (
        Path(args.schema_path)
        if args.schema_path
        else Path(__file__).resolve().parent.parent / "schemas" / "domain-specification.schema.json"
    )

    if not Path(file_path).exists():
        print(f"File not found: {file_path}", file=sys.stderr)
        sys.exit(1)

    issues = []
    content = Path(file_path).read_text(encoding="utf-8")
    schema = load_schema(schema_path)

    # -------------------------------------------------------------------------
    # 0) Extract the Entities section
    # -------------------------------------------------------------------------
    entities_section = get_entities_section(content)

    # -------------------------------------------------------------------------
    # 1) Required top-level keys
    # -------------------------------------------------------------------------
    required_keys = schema.get("required", [])
    for key in required_keys:
        if not re.search(rf"(?m)^{re.escape(key)}:", content):
            issues.append(add_issue("MissingKey", file_path,
                                    f"Required top-level key missing: {key}"))

    project_name_match = re.search(r"(?m)^ProjectName:\s*([A-Za-z][A-Za-z0-9]*)\s*$", content)
    project_name_pattern = schema["properties"]["ProjectName"].get("pattern")
    if project_name_match and project_name_pattern:
        if not re.match(project_name_pattern, project_name_match.group(1)):
            issues.append(add_issue(
                "InvalidValue", file_path,
                "ProjectName must be PascalCase alphanumeric with no spaces."
            ))

    # -------------------------------------------------------------------------
    # 2) Entity name conflict check (C# reserved type names)
    # -------------------------------------------------------------------------
    reserved_names = schema["$defs"]["entity"]["properties"]["name"]["not"]["enum"]
    entity_matches = get_entity_names(entities_section)

    for entity_name in entity_matches:
        if entity_name in reserved_names:
            issues.append(add_issue(
                "NamingConflict", file_path,
                f"Entity name '{entity_name}' conflicts with C# framework type. "
                "Use a domain-specific alternative."
            ))

    # -------------------------------------------------------------------------
    # 3) Entity must have at least one property defined
    # -------------------------------------------------------------------------
    entity_block_pattern = r"(?m)^  -\s*name:\s*[A-Za-z][A-Za-z0-9]*\b"
    entity_blocks = list(re.finditer(entity_block_pattern, entities_section))

    for i, match in enumerate(entity_blocks):
        start_index = match.end()
        if i + 1 < len(entity_blocks):
            block_end = entity_blocks[i + 1].start()
        else:
            block_end = len(entities_section)
        block = entities_section[start_index:block_end]
        if not re.search(r"(?m)^    properties:\s*$", block):
            entity_name_match = re.search(r"name:\s*(\w+)", match.group())
            if entity_name_match:
                entity_name = entity_name_match.group(1)
                issues.append(add_issue(
                    "IncompleteEntity", file_path,
                    f"Entity '{entity_name}' has no properties defined."
                ))

    # -------------------------------------------------------------------------
    # 4) Relationship target references exist
    # -------------------------------------------------------------------------
    all_entity_names = list(entity_matches)

    relationship_targets = re.findall(r"(?m)(?:target|entity):\s*(\w+)", entities_section)
    for target in relationship_targets:
        if target not in all_entity_names and target != "self":
            issues.append(add_issue(
                "BrokenReference", file_path,
                f"Relationship target '{target}' not found in entity definitions."
            ))

    # -------------------------------------------------------------------------
    # 5) Duplicate entity names
    # -------------------------------------------------------------------------
    name_counts = Counter(all_entity_names)
    for name, count in name_counts.items():
        if count > 1:
            issues.append(add_issue(
                "DuplicateEntity", file_path,
                f"Duplicate entity name: {name} (appears {count} times)."
            ))

    # -------------------------------------------------------------------------
    # Output
    # -------------------------------------------------------------------------
    if not issues:
        print(f"PASS: domain specification validation passed for {file_path}")
        sys.exit(0)

    print(f"FAIL: domain specification validation found {len(issues)} issue(s).")

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
