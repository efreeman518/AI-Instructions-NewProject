#!/usr/bin/env python3
"""Run preflight checks: refresh manifest, generate phase load packs, and lint."""

import argparse
import subprocess
import sys
from pathlib import Path


def invoke_step(name, command):
    print(f"==> {name}")
    result = subprocess.run(command, cwd=str(command[1]).rsplit("scripts", 1)[0] if len(command) > 1 else None)
    if result.returncode != 0:
        sys.exit(result.returncode)


def main():
    parser = argparse.ArgumentParser(description="Run preflight instruction checks")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    args = parser.parse_args()

    root = Path(args.root) if args.root else Path(__file__).resolve().parent.parent
    scripts_dir = root / "scripts"
    python = sys.executable

    invoke_step("Refresh manifest tokens", [
        python, str(scripts_dir / "update-manifest.py"), "--root", str(root)
    ])

    invoke_step("Generate phase load packs", [
        python, str(scripts_dir / "generate-phase-load-packs.py"), "--root", str(root)
    ])

    invoke_step("Run instruction lint", [
        python, str(scripts_dir / "lint-instructions.py"), "--root", str(root)
    ])

    print("Preflight complete.")


if __name__ == "__main__":
    main()
