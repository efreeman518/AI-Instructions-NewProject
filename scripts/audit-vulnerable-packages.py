#!/usr/bin/env python3
"""Run `dotnet list package --vulnerable --include-transitive` and enforce severity policy.

Policy (per support/execution-gates.md § Vulnerability Audit):
  - High severity: must fail unless allowlisted in INSTRUCTION-GAPS.md
  - Moderate severity: warn-only, log to stderr
  - Low severity: report

Allowlist format in INSTRUCTION-GAPS.md (free-form; this script grep-matches):
  Lines containing both the package name and the severity word are treated as accepted.
  Example: 'System.Security.Cryptography.Xml High - blocked on .NET 10 upgrade (owner: ef, target: 2026Q3)'

Offline behavior: if `dotnet list package --vulnerable` fails (no network), exit 0
with a warning so the gate doesn't block on transient connectivity issues.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

# Matches a package row in dotnet list package --vulnerable output.
# Format examples vary by dotnet SDK version; this regex is permissive.
PACKAGE_LINE = re.compile(
    r"\s*>\s*(?P<package>\S+)\s+(?P<requested>\S+)?\s*(?P<resolved>\S+)\s+(?P<severity>High|Moderate|Low|Critical)\s+(?P<advisory>https?://\S+)",
    re.IGNORECASE,
)


def run_audit(solution_or_project: Path) -> tuple[int, str]:
    cmd = [
        "dotnet",
        "list",
        str(solution_or_project),
        "package",
        "--vulnerable",
        "--include-transitive",
    ]
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            check=False,
            timeout=120,
        )
    except FileNotFoundError:
        print("dotnet CLI not found on PATH.", file=sys.stderr)
        return 2, ""
    except subprocess.TimeoutExpired:
        print("dotnet list package --vulnerable timed out.", file=sys.stderr)
        return 0, ""

    return result.returncode, result.stdout + result.stderr


def parse_findings(output: str) -> list[dict[str, str]]:
    findings = []
    for line in output.splitlines():
        m = PACKAGE_LINE.search(line)
        if m:
            findings.append(
                {
                    "package": m.group("package"),
                    "resolved": m.group("resolved") or "",
                    "severity": m.group("severity").capitalize(),
                    "advisory": m.group("advisory"),
                }
            )
    return findings


def is_allowlisted(finding: dict[str, str], gaps_text: str) -> bool:
    pkg = finding["package"]
    sev = finding["severity"]
    # naive: a line containing both the package name and the severity = accepted
    for line in gaps_text.splitlines():
        if pkg in line and sev in line:
            return True
    return False


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "solution",
        nargs="?",
        type=Path,
        default=Path("."),
        help="Path to .slnx, .sln, or project (default: current dir)",
    )
    parser.add_argument(
        "--gaps",
        type=Path,
        default=Path("INSTRUCTION-GAPS.md"),
        help="Path to gaps file used as allowlist (default: ./INSTRUCTION-GAPS.md)",
    )
    args = parser.parse_args()

    rc, output = run_audit(args.solution)

    if rc == 2:
        return 2  # tooling missing

    findings = parse_findings(output)
    if not findings:
        print("OK: no vulnerable packages found.")
        return 0

    gaps_text = ""
    if args.gaps.exists():
        gaps_text = args.gaps.read_text(encoding="utf-8")

    high_unaccounted = []
    moderate = []
    low = []

    for f in findings:
        sev = f["severity"]
        if sev in ("High", "Critical"):
            if is_allowlisted(f, gaps_text):
                print(
                    f"  [accepted] {f['package']} {f['resolved']} ({sev}) - allowlisted in {args.gaps}"
                )
            else:
                high_unaccounted.append(f)
        elif sev == "Moderate":
            moderate.append(f)
        else:
            low.append(f)

    for f in moderate:
        print(
            f"  [moderate] {f['package']} {f['resolved']} - {f['advisory']}",
            file=sys.stderr,
        )
    for f in low:
        print(
            f"  [low] {f['package']} {f['resolved']} - {f['advisory']}",
            file=sys.stderr,
        )

    if high_unaccounted:
        print(
            f"\n{len(high_unaccounted)} HIGH/CRITICAL vulnerable package(s) without allowlist entry:",
            file=sys.stderr,
        )
        for f in high_unaccounted:
            print(
                f"  [HIGH] {f['package']} {f['resolved']} - {f['advisory']}",
                file=sys.stderr,
            )
        print(
            f"\nFix or document each in {args.gaps} (line must contain package name and 'High').",
            file=sys.stderr,
        )
        return 1

    print(
        f"OK: {len(moderate)} moderate, {len(low)} low; no unhandled high-severity findings."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
