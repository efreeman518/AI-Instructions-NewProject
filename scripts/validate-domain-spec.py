#!/usr/bin/env python3
"""Validate a domain specification YAML file."""

import argparse
import re
import sys
from collections import Counter
from pathlib import Path


def add_issue(category, file_path, message, line=0):
    return {"Category": category, "File": file_path, "Line": line, "Message": message}


def main():
    parser = argparse.ArgumentParser(description="Validate a domain specification file")
    parser.add_argument("--file-path", "-FilePath", required=True,
                        help="Path to the domain specification file")
    args = parser.parse_args()

    file_path = args.file_path

    if not Path(file_path).exists():
        print(f"File not found: {file_path}", file=sys.stderr)
        sys.exit(1)

    issues = []
    content = Path(file_path).read_text(encoding="utf-8")
    lines = content.splitlines()

    # -------------------------------------------------------------------------
    # 0) Extract the Entities section
    # -------------------------------------------------------------------------
    # Support both markdown-embedded specs (## Entities header) and raw YAML
    entities_section = ""
    section_match = re.search(r"(?ms)^## Entities\s*\r?\n(.*?)(?=\r?\n## |\Z)", content)
    if section_match:
        entities_section = section_match.group(1)
    elif re.search(r"(?m)^entities:", content):
        # Raw YAML — use everything from `entities:` onward
        ent_start = re.search(r"(?m)^entities:", content).start()
        entities_section = content[ent_start:]

    # -------------------------------------------------------------------------
    # 1) Required top-level keys
    # -------------------------------------------------------------------------
    required_keys = ["projectName", "entities"]
    for key in required_keys:
        if not re.search(rf"(?mi)^{key}:", content):
            issues.append(add_issue("MissingKey", file_path,
                                    f"Required top-level key missing: {key}"))

    # -------------------------------------------------------------------------
    # 2) Entity name conflict check (C# reserved type names)
    # -------------------------------------------------------------------------
    reserved_names = [
        "Task", "Thread", "Timer", "Type", "String", "Object",
        "Action", "Attribute", "File", "Path", "Event", "Delegate",
    ]
    entity_matches = re.findall(r"(?m)^\s+-?\s*name:\s*(\w+)", entities_section)

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
    entity_block_pattern = r"(?m)^\s+-?\s*name:\s*\w+"
    entity_blocks = list(re.finditer(entity_block_pattern, entities_section))

    for i, match in enumerate(entity_blocks):
        start_index = match.end()
        if i + 1 < len(entity_blocks):
            block_end = entity_blocks[i + 1].start()
        else:
            block_end = len(entities_section)
        block = entities_section[start_index:block_end]
        if "properties:" not in block:
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
