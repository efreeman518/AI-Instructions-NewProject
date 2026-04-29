#!/usr/bin/env python3
"""Validate a domain-specification.yaml file against schemas/domain-specification.schema.json.

Requires: pyyaml, jsonschema
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    import yaml  # type: ignore
    from jsonschema import Draft202012Validator  # type: ignore
except ImportError as e:
    print(
        f"Missing dependency: {e}. Install with: pip install pyyaml jsonschema",
        file=sys.stderr,
    )
    sys.exit(2)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("yaml_path", type=Path, help="Path to domain-specification.yaml")
    parser.add_argument(
        "--schema",
        type=Path,
        default=Path(__file__).resolve().parent.parent
        / "schemas"
        / "domain-specification.schema.json",
    )
    args = parser.parse_args()

    if not args.yaml_path.exists():
        print(f"YAML not found: {args.yaml_path}", file=sys.stderr)
        return 1
    if not args.schema.exists():
        print(f"Schema not found: {args.schema}", file=sys.stderr)
        return 1

    data = yaml.safe_load(args.yaml_path.read_text(encoding="utf-8"))
    schema = json.loads(args.schema.read_text(encoding="utf-8"))
    validator = Draft202012Validator(schema)
    errors = sorted(validator.iter_errors(data), key=lambda e: list(e.path))

    if not errors:
        print(f"OK: {args.yaml_path} is valid against domain-specification schema.")
        return 0

    print(f"{args.yaml_path}: {len(errors)} validation error(s):", file=sys.stderr)
    for err in errors:
        path = ".".join(str(p) for p in err.absolute_path) or "<root>"
        print(f"  {path}: {err.message}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
