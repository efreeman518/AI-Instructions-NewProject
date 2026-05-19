#!/usr/bin/env python3
"""
Installs the runtime payload of this instruction repo into a consumer app.

Copies (unless --instructions-only):
    <repo>/<runtime files>                   -> <target>/.instructions/
    <repo>/AGENTS.md                         -> <target>/AGENTS.md            (merge)
    <repo>/CLAUDE.md                         -> <target>/CLAUDE.md            (merge)
    <repo>/.github/copilot-instructions.md   -> <target>/.github/copilot-instructions.md (merge)
    <repo>/.claude/commands/                 -> <target>/.claude/commands/    (dir)
    <repo>/.github/agents/                   -> <target>/.github/agents/      (dir)

"merge" appends source content inside sentinel markers when the target file already exists,
so existing user content is preserved.

Excludes author-side files: scripts/__pycache__, tests/, .git/, .github/workflows/,
.githooks/, .vscode/, .venv/, .tmp/, .gitignore.

Usage:
    python scripts/install-to-project.py --target <app-repo-root>
    python scripts/install-to-project.py --target <app-repo-root> --update
    python scripts/install-to-project.py --target <app-repo-root> --dry-run
    python scripts/install-to-project.py --target <app-repo-root> --instructions-only
    python scripts/install-to-project.py --target <app-repo-root> --verify
    python scripts/install-to-project.py --target <app-repo-root> --verify-only
"""

import argparse
import shutil
import sys
from pathlib import Path


# Runtime payload copied into <target>/.instructions/
INSTRUCTIONS_FILES = [
    "README.md",
    "CLAUDE.md",
    "START-AI.md",
]

INSTRUCTIONS_DIRS = [
    "ai",
    "patterns",
    "schemas",
    "skills",
    "support",
    "templates",
    "scripts",
]

# Paths excluded from any directory copy (matched against path parts).
EXCLUDE_PARTS = {
    "__pycache__",
    ".git",
    ".venv",
    ".tmp",
    ".vscode",
    ".githooks",
    "tests",
    "bin",
    "obj",
}

# Sentinel markers used when merging root-level markdown files.
MERGE_SENTINEL_START = "<!-- ai-scaffold: start -->"
MERGE_SENTINEL_END = "<!-- ai-scaffold: end -->"

# Agent/command placements that land at the app repo root, not under .instructions/.
# kind="merge" appends content inside sentinel markers (idempotent) when file exists.
AGENT_COPIES = [
    ("AGENTS.md", "AGENTS.md", "merge"),
    ("CLAUDE.md", "CLAUDE.md", "merge"),
    (".github/copilot-instructions.md", ".github/copilot-instructions.md", "merge"),
    (".claude/commands", ".claude/commands", "dir"),
    (".github/agents", ".github/agents", "dir"),
]


class Planner:
    def __init__(self, dry_run: bool, update: bool):
        self.dry_run = dry_run
        self.update = update
        self.copied = 0
        self.skipped = 0
        self.preserved = 0
        self.merged = 0

    def _should_skip(self, rel_path: Path) -> bool:
        return any(part in EXCLUDE_PARTS for part in rel_path.parts)

    def copy_file(self, src: Path, dst: Path, label: str) -> None:
        if dst.exists() and self.update:
            src_mtime = src.stat().st_mtime
            dst_mtime = dst.stat().st_mtime
            if dst_mtime > src_mtime:
                print(f"  [preserve] {label} (target is newer)")
                self.preserved += 1
                return
        action = "[dry-run]" if self.dry_run else "[copy]"
        print(f"  {action} {label}")
        if not self.dry_run:
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
        self.copied += 1

    def copy_tree(self, src: Path, dst: Path, label_prefix: str) -> None:
        if not src.exists():
            print(f"  [skip]  {label_prefix} (missing in source)")
            self.skipped += 1
            return
        for path in src.rglob("*"):
            if path.is_dir():
                continue
            rel = path.relative_to(src)
            if self._should_skip(rel):
                continue
            self.copy_file(path, dst / rel, f"{label_prefix}/{rel.as_posix()}")

    def merge_file(self, src: Path, dst: Path, label: str) -> None:
        """Copy src to dst; if dst exists, append src inside sentinel markers (idempotent)."""
        src_content = src.read_text(encoding="utf-8")
        if dst.exists():
            dst_content = dst.read_text(encoding="utf-8")
            if MERGE_SENTINEL_START in dst_content:
                print(f"  [skip]  {label} (already merged)")
                self.skipped += 1
                return
            merged = (
                dst_content.rstrip("\n")
                + "\n\n"
                + MERGE_SENTINEL_START
                + "\n"
                + src_content.strip()
                + "\n"
                + MERGE_SENTINEL_END
                + "\n"
            )
            action = "[dry-run]" if self.dry_run else "[merge]"
            print(f"  {action} {label}")
            if not self.dry_run:
                dst.write_text(merged, encoding="utf-8")
            self.merged += 1
        else:
            action = "[dry-run]" if self.dry_run else "[copy]"
            print(f"  {action} {label}")
            if not self.dry_run:
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src, dst)
            self.copied += 1

    def summary(self) -> None:
        print()
        print(f"copied:    {self.copied}")
        print(f"merged:    {self.merged} (appended scaffold block to existing file)")
        print(f"preserved: {self.preserved} (target newer, --update)")
        print(f"skipped:   {self.skipped}")
        if self.dry_run:
            print("(dry-run — no files written)")


def preserve_handoff(target_instructions: Path, dry_run: bool) -> Path | None:
    handoff = target_instructions.parent / "HANDOFF.md"
    if handoff.exists():
        print(f"[note] HANDOFF.md exists at {handoff} — left untouched")
        return handoff
    return None


# Required files/dirs after a full install (relative to <target>).
# Skipped expectations are pruned in verify_install when --instructions-only was used.
SMOKE_CHECK_PAYLOAD = [
    ".instructions/START-AI.md",
    ".instructions/README.md",
    ".instructions/CLAUDE.md",
    ".instructions/ai/SKILL.md",
    ".instructions/support/execution-gates.md",
    ".instructions/support/HANDOFF.md",
]

SMOKE_CHECK_HARNESS_ENTRYPOINTS = [
    "AGENTS.md",
    "CLAUDE.md",
    ".github/copilot-instructions.md",
    ".claude/commands/scaffold.md",
    ".claude/commands/vertical-slice.md",
    ".claude/commands/scaffold-adopt.md",
    ".github/agents/dotnet-scaffold.agent.md",
    ".github/agents/vertical-slice.agent.md",
    ".github/agents/scaffold-adopt.agent.md",
]


def verify_install(target_root: Path, instructions_only: bool) -> int:
    """Verify the expected files exist after install. Returns process exit code."""
    expected = list(SMOKE_CHECK_PAYLOAD)
    if not instructions_only:
        expected += SMOKE_CHECK_HARNESS_ENTRYPOINTS

    missing = [rel for rel in expected if not (target_root / rel).exists()]

    print()
    print("== install smoke check ==")
    if missing:
        print(f"  [fail] {len(missing)} expected file(s) missing under {target_root}:")
        for rel in missing:
            print(f"         - {rel}")
        return 1
    print(f"  [ok]   all {len(expected)} expected files present under {target_root}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Install AI-Instructions runtime payload into a consumer app.",
    )
    parser.add_argument(
        "--target", required=True,
        help="Path to the consumer app repo root (parent of .instructions/).",
    )
    parser.add_argument(
        "--update", action="store_true",
        help="Skip files where the target copy is newer than the source.",
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Print planned copies without writing files.",
    )
    parser.add_argument(
        "--instructions-only", action="store_true",
        help="Install only <target>/.instructions/, skip agent/command placement.",
    )
    parser.add_argument(
        "--verify", action="store_true",
        help="After install, verify expected entrypoints and payload files exist.",
    )
    parser.add_argument(
        "--verify-only", action="store_true",
        help="Skip install; just verify an existing target. Implies --verify.",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parent.parent
    target_root = Path(args.target).resolve()

    if not target_root.exists():
        print(f"error: target does not exist: {target_root}", file=sys.stderr)
        return 1
    if not target_root.is_dir():
        print(f"error: target is not a directory: {target_root}", file=sys.stderr)
        return 1
    if target_root == repo_root and not args.verify_only:
        print("error: refusing to install into the instruction repo itself", file=sys.stderr)
        return 1

    target_instructions = target_root / ".instructions"
    print(f"source: {repo_root}")
    print(f"target: {target_root}")
    print()

    if args.verify_only:
        return verify_install(target_root, args.instructions_only)

    preserve_handoff(target_instructions, args.dry_run)

    planner = Planner(dry_run=args.dry_run, update=args.update)

    print("== .instructions/ payload ==")
    for rel in INSTRUCTIONS_FILES:
        src = repo_root / rel
        if not src.exists():
            print(f"  [skip]  {rel} (missing in source)")
            planner.skipped += 1
            continue
        planner.copy_file(src, target_instructions / rel, f".instructions/{rel}")

    for rel in INSTRUCTIONS_DIRS:
        planner.copy_tree(
            repo_root / rel,
            target_instructions / rel,
            f".instructions/{rel}",
        )

    if not args.instructions_only:
        print()
        print("== agent/command placement (app repo root) ==")
        for src_rel, dst_rel, kind in AGENT_COPIES:
            src = repo_root / src_rel
            dst = target_root / dst_rel
            if not src.exists():
                print(f"  [skip]  {dst_rel} (missing in source)")
                planner.skipped += 1
                continue
            if kind == "merge":
                planner.merge_file(src, dst, dst_rel)
            elif kind == "file":
                planner.copy_file(src, dst, dst_rel)
            else:
                planner.copy_tree(src, dst, dst_rel)

    planner.summary()

    if args.verify and not args.dry_run:
        return verify_install(target_root, args.instructions_only)

    return 0


if __name__ == "__main__":
    sys.exit(main())
