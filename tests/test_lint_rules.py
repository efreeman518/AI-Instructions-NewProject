import json
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS_DIR = REPO_ROOT / "scripts"

if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import lint_rules


def write_file(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


class LintRuleTests(unittest.TestCase):
    def test_check_broken_links_reports_missing_relative_target(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            guide = root / "docs" / "guide.md"
            write_file(guide, "See [missing](./missing.md).\n")

            issues = lint_rules.check_broken_links(root, [guide])

        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0]["Category"], "Links")
        self.assertEqual(issues[0]["File"], "docs/guide.md")
        self.assertIn("Broken local link target: ./missing.md", issues[0]["Message"])

    def test_check_shell_fences_reports_powershell_syntax_inside_bash_fence(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            page = root / "docs" / "shell.md"
            write_file(page, "```bash\n$env:ASPNETCORE_ENVIRONMENT = \"Development\"\n```\n")

            issues = lint_rules.check_shell_fences(root, [page])

        self.assertEqual(len(issues), 1)
        self.assertEqual(issues[0]["Category"], "ShellFence")
        self.assertEqual(issues[0]["File"], "docs/shell.md")

    def test_check_manifest_invariants_reports_missing_mode_exclusions(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest = {
                "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
                "files": [
                    {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                    {"path": "skills/identity-management.md", "phase": "phase-5a", "estimatedTokens": 1},
                ],
            }
            write_file(root / "_manifest.json", json.dumps(manifest, indent=4))
            write_file(root / "skills" / "identity-management.md", "# Identity\n")

            issues = lint_rules.check_manifest_invariants(root)

        messages = {issue["Message"] for issue in issues}
        self.assertIn("Manifest is missing top-level modeExclusions.", messages)
        self.assertIn("Missing generated phase-load-packs.json.", messages)

    def test_check_manifest_invariants_reports_entries_outside_declared_scope(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest = {
                "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
                "modeExclusions": {"lite": [], "api-only": []},
                "files": [
                    {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                    {"path": "skills/identity-management.md", "phase": "phase-5f", "estimatedTokens": 1},
                    {"path": "scripts/update-manifest.py", "phase": "metadata", "estimatedTokens": 0},
                ],
            }
            phase_load_packs = {
                "phaseOrder": ["metadata", "phase-5f"],
                "packs": {
                    "full": {"metadata": ["_manifest.json"], "phase-5f": ["skills/identity-management.md"]},
                    "lite": {"metadata": ["_manifest.json"], "phase-5f": ["skills/identity-management.md"]},
                    "api-only": {"metadata": ["_manifest.json"], "phase-5f": ["skills/identity-management.md"]},
                },
            }

            write_file(root / "_manifest.json", json.dumps(manifest, indent=4))
            write_file(root / "phase-load-packs.json", json.dumps(phase_load_packs, indent=4))
            write_file(root / "skills" / "identity-management.md", "# Identity\n")

            issues = lint_rules.check_manifest_invariants(root)

        messages = {issue["Message"] for issue in issues}
        self.assertIn(
            "Manifest entry is outside declared scope: scripts/update-manifest.py.",
            messages,
        )


if __name__ == "__main__":
    unittest.main()
