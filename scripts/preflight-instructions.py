#!/usr/bin/env python3
"""Run preflight checks: refresh manifest, generate phase load packs, lint, and run script tests."""

import argparse
import subprocess
import sys
from pathlib import Path


def invoke_step(name, command, cwd):
    print(f"==> {name}")
    result = subprocess.run(command, cwd=str(cwd))
    if result.returncode != 0:
        sys.exit(result.returncode)


def main():
    parser = argparse.ArgumentParser(description="Run preflight instruction checks")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    args = parser.parse_args()

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parent.parent
    if not root.exists():
        print(f"Root directory not found: {root}", file=sys.stderr)
        sys.exit(1)

    scripts_dir = root / "scripts"
    python = sys.executable

    invoke_step("Refresh manifest tokens", [
        python, str(scripts_dir / "update-manifest.py"), "--root", str(root)
    ], root)

    invoke_step("Generate phase load packs", [
        python, str(scripts_dir / "generate-phase-load-packs.py"), "--root", str(root)
    ], root)

    invoke_step("Run instruction lint", [
        python, str(scripts_dir / "lint-instructions.py"), "--root", str(root)
    ], root)

    invoke_step("Run script unit tests", [
        python, "-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py"
    ], root)

    print("Preflight complete.")


if __name__ == "__main__":
    main()
