#!/usr/bin/env python3
"""Lint instruction files for common issues."""

import argparse
import sys
from pathlib import Path

from lint_rules import collect_issues

def main():
    parser = argparse.ArgumentParser(description="Lint instruction files")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    args = parser.parse_args()

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parent.parent

    issues = collect_issues(root)

    # -------------------------------------------------------------------------
    # Output
    # -------------------------------------------------------------------------
    if not issues:
        print("PASS: instruction lint checks passed.")
        sys.exit(0)

    print(f"FAIL: instruction lint checks found {len(issues)} issue(s).")

    # Sort and print table
    issues.sort(key=lambda x: (x["Category"], x["File"], x["Line"]))

    col_cat = max((len(i["Category"]) for i in issues), default=8)
    col_file = max((len(i["File"]) for i in issues), default=4)
    col_line = max((len(str(i["Line"])) for i in issues), default=4)
    col_msg = max((len(i["Message"]) for i in issues), default=7)

    col_cat = max(col_cat, len("Category"))
    col_file = max(col_file, len("File"))
    col_line = max(col_line, len("Line"))
    col_msg = max(col_msg, len("Message"))

    header = (f"{'Category':<{col_cat}}  {'File':<{col_file}}  "
              f"{'Line':>{col_line}}  {'Message':<{col_msg}}")
    print()
    print(header)
    print("-" * len(header))
    for issue in issues:
        print(f"{issue['Category']:<{col_cat}}  {issue['File']:<{col_file}}  "
              f"{issue['Line']:>{col_line}}  {issue['Message']:<{col_msg}}")
    print()

    sys.exit(1)


if __name__ == "__main__":
    main()
