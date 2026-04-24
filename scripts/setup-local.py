#!/usr/bin/env python3
"""Create local Python tooling environment and run author preflight."""

import argparse
import subprocess
import sys
import venv
from pathlib import Path


def venv_python_path(venv_dir):
    if sys.platform.startswith("win"):
        return venv_dir / "Scripts" / "python.exe"
    return venv_dir / "bin" / "python"


def run(command, cwd):
    print("+ " + " ".join(str(part) for part in command))
    result = subprocess.run(command, cwd=str(cwd))
    if result.returncode != 0:
        raise SystemExit(result.returncode)


def main():
    parser = argparse.ArgumentParser(
        description="Set up local Python tooling for the instruction repo.",
    )
    parser.add_argument(
        "--root",
        "-Root",
        default=None,
        help="Instruction repo root (defaults to parent of scripts/).",
    )
    parser.add_argument(
        "--venv",
        default=".venv",
        help="Virtual environment directory relative to root.",
    )
    parser.add_argument(
        "--skip-preflight",
        action="store_true",
        help="Create/reuse .venv but do not run author preflight.",
    )
    parser.add_argument(
        "--instructions-only",
        action="store_true",
        help="When running from an installed .instructions payload, skip root agent placement checks.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parent.parent
    if not root.exists():
        print(f"error: root does not exist: {root}", file=sys.stderr)
        return 1

    venv_dir = (root / args.venv).resolve()
    if not str(venv_dir).startswith(str(root)):
        print(f"error: refusing to create venv outside root: {venv_dir}", file=sys.stderr)
        return 1

    python_path = venv_python_path(venv_dir)
    if not python_path.exists():
        print(f"Creating virtual environment: {venv_dir}")
        venv.EnvBuilder(with_pip=False, clear=False).create(venv_dir)
    else:
        print(f"Using existing virtual environment: {venv_dir}")

    if not python_path.exists():
        print(f"error: venv python not found after creation: {python_path}", file=sys.stderr)
        return 1

    print(f"Python: {python_path}")

    if not args.skip_preflight:
        preflight = (
            root / "scripts" / "preflight-instructions.py"
            if (root / "tests").is_dir()
            else root / "scripts" / "preflight-installed.py"
        )
        command = [
            str(python_path),
            str(preflight),
            "--root",
            str(root),
        ]
        if preflight.name == "preflight-installed.py" and args.instructions_only:
            command.append("--instructions-only")
        run(command, root)

    print("Local setup complete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
