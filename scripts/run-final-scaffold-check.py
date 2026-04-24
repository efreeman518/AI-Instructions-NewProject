#!/usr/bin/env python3
"""Run cross-platform final scaffold validation commands."""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


def run_step(name, command, cwd):
    print()
    print(f"==> {name}")
    print("+ " + " ".join(str(part) for part in command))
    result = subprocess.run(command, cwd=str(cwd))
    return result.returncode


def add_result(results, name, code):
    results.append((name, code))
    if code == 0:
        print(f"PASS: {name}")
    else:
        print(f"FAIL: {name} exited {code}")


def maybe_run(results, name, command, root, fail_fast):
    code = run_step(name, command, root)
    add_result(results, name, code)
    if fail_fast and code != 0:
        raise SystemExit(code)


def main():
    parser = argparse.ArgumentParser(
        description="Run final generated-app scaffold checks from the app root.",
    )
    parser.add_argument("--root", "-Root", default=".", help="Target app repo root.")
    parser.add_argument("--phase", choices=["4", "final"], default="final", help="Scaffold validator phase.")
    parser.add_argument("--static-only", action="store_true", help="Skip dotnet restore/build/test.")
    parser.add_argument("--skip-restore", action="store_true", help="Skip dotnet restore.")
    parser.add_argument("--skip-build", action="store_true", help="Skip dotnet build.")
    parser.add_argument("--skip-test", action="store_true", help="Skip dotnet test.")
    parser.add_argument("--skip-handoff", action="store_true", help="Skip HANDOFF.md validation.")
    parser.add_argument(
        "--skip-implementation-plan",
        action="store_true",
        help="Skip implementation-plan.md validation.",
    )
    parser.add_argument(
        "--require-auth-env",
        action="store_true",
        help="Pass --require-auth-env to validate-ef-packages-feed.py.",
    )
    parser.add_argument("--fail-fast", action="store_true", help="Stop at first failing command.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.exists():
        print(f"error: root does not exist: {root}", file=sys.stderr)
        return 1

    scripts_dir = Path(__file__).resolve().parent
    python = sys.executable
    results = []

    if not args.static_only:
        dotnet = shutil.which("dotnet")
        if dotnet is None:
            print("error: dotnet was not found on PATH.", file=sys.stderr)
            return 1
        if not args.skip_restore:
            maybe_run(results, "dotnet restore", [dotnet, "restore"], root, args.fail_fast)
        if not args.skip_build:
            maybe_run(results, "dotnet build", [dotnet, "build"], root, args.fail_fast)
        if not args.skip_test:
            maybe_run(results, "dotnet test", [dotnet, "test"], root, args.fail_fast)

    feed_command = [
        python,
        str(scripts_dir / "validate-ef-packages-feed.py"),
        "--root",
        str(root),
    ]
    if args.require_auth_env:
        feed_command.append("--require-auth-env")
    maybe_run(results, "EF.Packages feed validation", feed_command, root, args.fail_fast)

    maybe_run(
        results,
        "scaffold output validation",
        [
            python,
            str(scripts_dir / "validate-scaffold-output.py"),
            "--root",
            str(root),
            "--phase",
            args.phase,
        ],
        root,
        args.fail_fast,
    )

    if not args.skip_handoff:
        maybe_run(
            results,
            "HANDOFF validation",
            [python, str(scripts_dir / "validate-handoff.py"), "--root", str(root)],
            root,
            args.fail_fast,
        )

    if not args.skip_implementation_plan:
        maybe_run(
            results,
            "implementation plan validation",
            [python, str(scripts_dir / "validate-implementation-plan.py"), "--root", str(root)],
            root,
            args.fail_fast,
        )

    failures = [(name, code) for name, code in results if code != 0]
    print()
    if failures:
        print(f"FAIL: final scaffold check found {len(failures)} failing step(s).")
        for name, code in failures:
            print(f"- {name}: exit {code}")
        return 1

    print("PASS: final scaffold check passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
