import json
import math
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPTS_DIR = REPO_ROOT / "scripts"


def run_script(script_name, *args, cwd=None):
    result = subprocess.run(
        [sys.executable, str(SCRIPTS_DIR / script_name), *args],
        cwd=str(cwd or REPO_ROOT),
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        raise AssertionError(
            f"{script_name} failed with exit code {result.returncode}.\n"
            f"STDOUT:\n{result.stdout}\nSTDERR:\n{result.stderr}"
        )
    return result


def run_script_expect_failure(script_name, *args, cwd=None):
    result = subprocess.run(
        [sys.executable, str(SCRIPTS_DIR / script_name), *args],
        cwd=str(cwd or REPO_ROOT),
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode == 0:
        raise AssertionError(
            f"{script_name} succeeded unexpectedly.\n"
            f"STDOUT:\n{result.stdout}\nSTDERR:\n{result.stderr}"
        )
    return result


def run_powershell_script(script_name, *args, cwd=None):
    powershell = shutil.which("pwsh")
    if not powershell:
        raise unittest.SkipTest("pwsh is required")

    result = subprocess.run(
        [powershell, "-NoProfile", "-File", str(SCRIPTS_DIR / script_name), *args],
        cwd=str(cwd or REPO_ROOT),
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        raise AssertionError(
            f"{script_name} failed with exit code {result.returncode}.\n"
            f"STDOUT:\n{result.stdout}\nSTDERR:\n{result.stderr}"
        )
    return result


def run_powershell_script_expect_failure(script_name, *args, cwd=None):
    powershell = shutil.which("pwsh")
    if not powershell:
        raise unittest.SkipTest("pwsh is required")

    result = subprocess.run(
        [powershell, "-NoProfile", "-File", str(SCRIPTS_DIR / script_name), *args],
        cwd=str(cwd or REPO_ROOT),
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode == 0:
        raise AssertionError(
            f"{script_name} succeeded unexpectedly.\n"
            f"STDOUT:\n{result.stdout}\nSTDERR:\n{result.stderr}"
        )
    return result


def write_json(path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=4), encoding="utf-8", newline="\n")


class InstructionScriptTests(unittest.TestCase):
    def test_generate_phase_load_packs_applies_overlay_and_mode_exclusions(self):
        manifest = {
            "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
            "modeExclusions": {
                "lite": ["skills/aspire.md"],
                "api-only": ["skills/aspire.md", "skills/gateway.md"],
            },
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                {"path": "ai/contract-scaffolding.md", "phase": "phase-4", "estimatedTokens": 10},
                {"path": "skills/solution-structure.md", "phase": "phase-4", "estimatedTokens": 10},
                {"path": "skills/package-dependencies.md", "phase": "phase-4", "estimatedTokens": 10},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 10},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 10},
                {"path": "skills/gateway.md", "phase": "phase-5c", "estimatedTokens": 10},
                {"path": "skills/aspire.md", "phase": "phase-5c", "estimatedTokens": 10},
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_json(root / "_manifest.json", manifest)

            run_script("generate-phase-load-packs.py", "--root", str(root))

            packs = json.loads((root / "phase-load-packs.json").read_text(encoding="utf-8"))

        phase_four = packs["packs"]["full"]["phase-4"]
        self.assertIn("ai/placeholder-tokens.md", phase_four)
        self.assertIn("support/ef-packages-reference.md", phase_four)
        self.assertNotIn("skills/aspire.md", packs["packs"]["lite"]["phase-5c"])
        self.assertNotIn("skills/aspire.md", packs["packs"]["api-only"]["phase-5c"])
        self.assertNotIn("skills/gateway.md", packs["packs"]["api-only"]["phase-5c"])

    def test_get_phase_load_set_resolves_dependencies_and_budget(self):
        manifest = {
            "contextBudget": {"default": 200, "extended": 400, "compact": 100},
            "modeExclusions": {"lite": [], "api-only": []},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 7},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 9},
                {"path": "skills/solution-structure.md", "phase": "phase-4", "estimatedTokens": 11},
                {"path": "skills/package-dependencies.md", "phase": "phase-4", "estimatedTokens": 13},
                {"path": "skills/bootstrapper.md", "phase": "phase-5b", "estimatedTokens": 17},
                {"path": "skills/configuration-secrets.md", "phase": "phase-5c", "estimatedTokens": 19},
                {"path": "skills/identity-management.md", "phase": "phase-5f", "estimatedTokens": 23},
                {
                    "path": "skills/ai-integration.md",
                    "phase": "phase-5g",
                    "estimatedTokens": 29,
                    "dependencies": [
                        "skills/bootstrapper.md",
                        "skills/configuration-secrets.md",
                        "skills/identity-management.md",
                    ],
                },
                {
                    "path": "templates/ai-search-template.md",
                    "phase": "phase-5g",
                    "estimatedTokens": 31,
                    "requires": ["skills/package-dependencies.md"],
                },
                {
                    "path": "templates/agent-template.md",
                    "phase": "phase-5g",
                    "estimatedTokens": 37,
                    "requires": ["skills/solution-structure.md"],
                },
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_json(root / "_manifest.json", manifest)

            run_script("generate-phase-load-packs.py", "--root", str(root))

            result = run_script(
                "get-phase-load-set.py",
                "--root",
                str(root),
                "--phase",
                "5g",
                "--mode",
                "full",
                "--include-ai-services",
                "--as-json",
            )

        payload = json.loads(result.stdout)
        files = payload["files"]
        path_to_index = {item["path"]: index for index, item in enumerate(files)}

        self.assertTrue(payload["withinBudget"])
        self.assertEqual(
            {item["path"] for item in payload["requestedFiles"]},
            {
                "skills/ai-integration.md",
                "templates/ai-search-template.md",
                "templates/agent-template.md",
            },
        )
        self.assertEqual(
            {item["path"] for item in payload["dependencyFiles"]},
            {
                "skills/bootstrapper.md",
                "skills/configuration-secrets.md",
                "skills/identity-management.md",
                "skills/package-dependencies.md",
                "skills/solution-structure.md",
            },
        )
        self.assertLess(path_to_index["skills/bootstrapper.md"], path_to_index["skills/ai-integration.md"])
        self.assertLess(path_to_index["skills/configuration-secrets.md"], path_to_index["skills/ai-integration.md"])
        self.assertLess(path_to_index["skills/identity-management.md"], path_to_index["skills/ai-integration.md"])
        self.assertLess(path_to_index["skills/package-dependencies.md"], path_to_index["templates/ai-search-template.md"])
        self.assertLess(path_to_index["skills/solution-structure.md"], path_to_index["templates/agent-template.md"])
        self.assertEqual(
            payload["totalEstimatedTokens"],
            sum(item["estimatedTokens"] for item in files),
        )

    def test_get_phase_load_set_fails_on_dependency_cycle(self):
        manifest = {
            "contextBudget": {"default": 200, "extended": 400, "compact": 100},
            "modeExclusions": {"lite": [], "api-only": []},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 7},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 9},
                {
                    "path": "skills/bootstrapper.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 17,
                    "dependencies": ["skills/ai-integration.md"],
                },
                {
                    "path": "skills/ai-integration.md",
                    "phase": "phase-5g",
                    "estimatedTokens": 29,
                    "dependencies": ["skills/bootstrapper.md"],
                },
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_json(root / "_manifest.json", manifest)

            run_script("generate-phase-load-packs.py", "--root", str(root))

            result = run_script_expect_failure(
                "get-phase-load-set.py",
                "--root",
                str(root),
                "--phase",
                "5g",
                "--mode",
                "full",
                "--include-ai-services",
            )

        self.assertIn("Dependency cycle detected", result.stderr)

    def test_get_phase_load_set_fails_when_mode_excludes_required_dependency(self):
        manifest = {
            "contextBudget": {"default": 200, "extended": 400, "compact": 100},
            "modeExclusions": {"lite": [], "api-only": ["skills/bootstrapper.md"]},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 7},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 9},
                {"path": "skills/bootstrapper.md", "phase": "phase-5b", "estimatedTokens": 17},
                {
                    "path": "skills/ai-integration.md",
                    "phase": "phase-5g",
                    "estimatedTokens": 29,
                    "dependencies": ["skills/bootstrapper.md"],
                },
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_json(root / "_manifest.json", manifest)

            run_script("generate-phase-load-packs.py", "--root", str(root))

            result = run_script_expect_failure(
                "get-phase-load-set.py",
                "--root",
                str(root),
                "--phase",
                "5g",
                "--mode",
                "api-only",
                "--include-ai-services",
            )

        self.assertIn("excluded by mode 'api-only'", result.stderr)

    def test_update_manifest_recalculates_written_manifest_tokens_and_removes_stale_entries(self):
        readme_content = "# Test Repo\n\nHello from the instruction set.\n"
        notes_content = "# Notes\n\nThis file should be added to the manifest.\n"

        manifest = {
            "version": "1.1",
            "generatedAt": "2026-04-10",
            "tokenEstimationMethod": "ceil(characters/4)",
            "totalEstimatedTokens": 0,
            "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
            "modeExclusions": {"lite": [], "api-only": []},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 0},
                {"path": "scripts/update-manifest.py", "phase": "metadata", "estimatedTokens": 0},
                {"path": "README.md", "phase": "support-only", "estimatedTokens": 0},
                {"path": "docs/stale.md", "phase": "support-only", "estimatedTokens": 999},
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            (root / "README.md").write_text(readme_content, encoding="utf-8", newline="\n")
            (root / "notes.md").write_text(notes_content, encoding="utf-8", newline="\n")
            write_json(root / "_manifest.json", manifest)

            run_script("update-manifest.py", "--root", str(root))

            updated_manifest_text = (root / "_manifest.json").read_text(encoding="utf-8")
            updated_manifest = json.loads(updated_manifest_text)

        files = {entry["path"]: entry for entry in updated_manifest["files"]}
        self.assertNotIn("docs/stale.md", files)
        self.assertNotIn("scripts/update-manifest.py", files)
        self.assertIn("notes.md", files)
        self.assertEqual(files["README.md"]["estimatedTokens"], math.ceil(len(readme_content) / 4))
        self.assertEqual(files["notes.md"]["estimatedTokens"], math.ceil(len(notes_content) / 4))
        self.assertEqual(files["_manifest.json"]["estimatedTokens"], math.ceil(len(updated_manifest_text) / 4))
        self.assertEqual(
            updated_manifest["totalEstimatedTokens"],
            sum(entry["estimatedTokens"] for entry in updated_manifest["files"]),
        )

    def test_update_manifest_powershell_recalculates_written_manifest_tokens_and_removes_stale_entries(self):
        readme_content = "# Test Repo\n\nHello from the instruction set.\n"
        notes_content = "# Notes\n\nThis file should be added to the manifest.\n"

        manifest = {
            "version": "1.1",
            "generatedAt": "2026-04-10",
            "tokenEstimationMethod": "ceil(characters/4)",
            "totalEstimatedTokens": 0,
            "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
            "modeExclusions": {"lite": [], "api-only": []},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 0},
                {"path": "scripts/update-manifest.py", "phase": "metadata", "estimatedTokens": 0},
                {"path": "README.md", "phase": "support-only", "estimatedTokens": 0},
                {"path": "docs/stale.md", "phase": "support-only", "estimatedTokens": 999},
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            (root / "README.md").write_text(readme_content, encoding="utf-8", newline="\n")
            (root / "notes.md").write_text(notes_content, encoding="utf-8", newline="\n")
            write_json(root / "_manifest.json", manifest)

            run_powershell_script("update-manifest.ps1", "-Root", str(root))

            updated_manifest_text = (root / "_manifest.json").read_text(encoding="utf-8")
            updated_manifest = json.loads(updated_manifest_text)

        files = {entry["path"]: entry for entry in updated_manifest["files"]}
        self.assertNotIn("docs/stale.md", files)
        self.assertNotIn("scripts/update-manifest.py", files)
        self.assertIn("notes.md", files)
        self.assertEqual(files["README.md"]["estimatedTokens"], math.ceil(len(readme_content) / 4))
        self.assertEqual(files["notes.md"]["estimatedTokens"], math.ceil(len(notes_content) / 4))
        self.assertEqual(files["_manifest.json"]["estimatedTokens"], math.ceil(len(updated_manifest_text) / 4))
        self.assertEqual(
            updated_manifest["totalEstimatedTokens"],
            sum(entry["estimatedTokens"] for entry in updated_manifest["files"]),
        )

    def test_lint_instructions_powershell_reports_broken_links(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            docs_dir = root / "docs"
            docs_dir.mkdir(parents=True, exist_ok=True)
            (docs_dir / "guide.md").write_text("See [missing](./missing.md).\n", encoding="utf-8", newline="\n")

            result = run_powershell_script_expect_failure("lint-instructions.ps1", "-Root", str(root))

        combined_output = result.stdout + result.stderr
        self.assertIn("FAIL: instruction lint checks found", combined_output)
        self.assertIn("Broken local link target: ./missing.md", combined_output)


if __name__ == "__main__":
    unittest.main()
