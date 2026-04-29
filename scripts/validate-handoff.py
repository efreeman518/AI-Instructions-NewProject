#!/usr/bin/env python3
"""Validate HANDOFF.md YAML front-matter against schemas/handoff.schema.json.

Extracts the first ```yaml fenced block from HANDOFF.md and validates it.
Prose sections (Notes, Blockers, etc.) are not validated.

Requires: pyyaml, jsonschema
  pip install pyyaml jsonschema
"""

from __future__ import annotations

import argparse
import json
import re
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


YAML_BLOCK = re.compile(r"```yaml\s*\n(.*?)\n```", re.DOTALL)


def extract_front_matter(handoff_path: Path) -> dict | None:
    text = handoff_path.read_text(encoding="utf-8")
    match = YAML_BLOCK.search(text)
    if not match:
        return None
    return yaml.safe_load(match.group(1))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("handoff", type=Path, help="Path to HANDOFF.md")
    parser.add_argument(
        "--schema",
        type=Path,
        default=Path(__file__).resolve().parent.parent / "schemas" / "handoff.schema.json",
    )
    args = parser.parse_args()

    if not args.handoff.exists():
        print(f"HANDOFF file not found: {args.handoff}", file=sys.stderr)
        return 1

    if not args.schema.exists():
        print(f"Schema not found: {args.schema}", file=sys.stderr)
        return 1

    front_matter = extract_front_matter(args.handoff)
    if front_matter is None:
        print(
            f"{args.handoff}: no ```yaml front-matter block found.",
            file=sys.stderr,
        )
        return 1

    schema = json.loads(args.schema.read_text(encoding="utf-8"))
    validator = Draft202012Validator(schema)
    errors = sorted(validator.iter_errors(front_matter), key=lambda e: e.path)

    if not errors:
        print(f"OK: {args.handoff} front-matter is valid.")
        return 0

    print(f"{args.handoff}: validation failed with {len(errors)} error(s):", file=sys.stderr)
    for err in errors:
        path = ".".join(str(p) for p in err.absolute_path) or "<root>"
        print(f"  {path}: {err.message}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
