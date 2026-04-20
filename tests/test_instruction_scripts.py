import json
import math
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

    def test_generate_phase_load_packs_includes_slice_profiles(self):
        manifest = {
            "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
            "modeExclusions": {"lite": [], "api-only": []},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 1},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 1},
                {"path": "skills/domain-model.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "skills/data-persistence.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "templates/entity-template.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "templates/domain-rules-template.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "templates/ef-configuration-template.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "templates/repository-template.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "templates/test-templates-domain.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "templates/test-templates-repository.md", "phase": "phase-5a", "estimatedTokens": 1},
                {"path": "skills/application-layer.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "skills/bootstrapper.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "skills/api.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "skills/testing.md", "phase": "phase-5e", "estimatedTokens": 1},
                {"path": "templates/data-mapping-template.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "templates/service-template.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "templates/structure-validator-template.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "templates/endpoint-template.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "templates/exception-handler-template.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "templates/test-templates-service.md", "phase": "phase-5b", "estimatedTokens": 1},
                {"path": "templates/test-templates-endpoint.md", "phase": "phase-5b", "estimatedTokens": 1},
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_json(root / "_manifest.json", manifest)

            run_script("generate-phase-load-packs.py", "--root", str(root))

            packs = json.loads((root / "phase-load-packs.json").read_text(encoding="utf-8"))

        self.assertEqual(
            packs["slices"]["full"]["phase-5a"]["domain"],
            [
                "skills/domain-model.md",
                "templates/entity-template.md",
                "templates/domain-rules-template.md",
                "templates/test-templates-domain.md",
            ],
        )
        self.assertEqual(
            packs["slices"]["full"]["phase-5b"]["endpoint"],
            [
                "skills/application-layer.md",
                "skills/bootstrapper.md",
                "skills/api.md",
                "skills/testing.md",
                "templates/endpoint-template.md",
                "templates/exception-handler-template.md",
                "templates/test-templates-endpoint.md",
            ],
        )

    def test_report_context_budgets_reports_core_phases_and_slices(self):
        manifest = {
            "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
            "files": [
                {"path": "ai/contract-scaffolding.md", "phase": "phase-4", "estimatedTokens": 1000},
                {"path": "skills/solution-structure.md", "phase": "phase-4", "estimatedTokens": 2000},
                {"path": "skills/package-dependencies.md", "phase": "phase-4", "estimatedTokens": 500},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 1000},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 1500},
                {"path": "ai/SKILL.md", "phase": "phase-5-base", "estimatedTokens": 2000},
                {"path": "ai/tdd-protocol.md", "phase": "phase-5-base", "estimatedTokens": 1000},
                {"path": "skills/domain-model.md", "phase": "phase-5a", "estimatedTokens": 2000},
                {"path": "skills/data-persistence.md", "phase": "phase-5a", "estimatedTokens": 3000},
                {"path": "templates/entity-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                {"path": "templates/domain-rules-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                {"path": "templates/ef-configuration-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                {"path": "templates/repository-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                {"path": "templates/test-templates-domain.md", "phase": "phase-5a", "estimatedTokens": 1000},
                {"path": "templates/test-templates-repository.md", "phase": "phase-5a", "estimatedTokens": 1000},
                {"path": "skills/application-layer.md", "phase": "phase-5b", "estimatedTokens": 1500},
                {"path": "skills/bootstrapper.md", "phase": "phase-5b", "estimatedTokens": 1500},
                {"path": "skills/api.md", "phase": "phase-5b", "estimatedTokens": 1500},
                {"path": "skills/testing.md", "phase": "phase-5e", "estimatedTokens": 2500},
                {"path": "templates/data-mapping-template.md", "phase": "phase-5b", "estimatedTokens": 1500},
                {"path": "templates/service-template.md", "phase": "phase-5b", "estimatedTokens": 1500},
                {"path": "templates/structure-validator-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                {"path": "templates/endpoint-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                {"path": "templates/exception-handler-template.md", "phase": "phase-5b", "estimatedTokens": 500},
                {"path": "templates/test-templates-service.md", "phase": "phase-5b", "estimatedTokens": 1000},
                {"path": "templates/test-templates-endpoint.md", "phase": "phase-5b", "estimatedTokens": 1000},
            ],
        }
        phase_load_packs = {
            "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
            "packs": {
                "full": {
                    "phase-4": [
                        "ai/contract-scaffolding.md",
                        "skills/solution-structure.md",
                        "skills/package-dependencies.md",
                    ],
                    "phase-5-base": [
                        "ai/SKILL.md",
                        "ai/placeholder-tokens.md",
                        "ai/tdd-protocol.md",
                        "support/ef-packages-reference.md",
                    ],
                }
            },
            "slices": {
                "full": {
                    "phase-5a": {
                        "domain": [
                            "skills/domain-model.md",
                            "templates/entity-template.md",
                            "templates/domain-rules-template.md",
                            "templates/test-templates-domain.md",
                        ],
                        "repository": [
                            "skills/domain-model.md",
                            "skills/data-persistence.md",
                            "templates/entity-template.md",
                            "templates/ef-configuration-template.md",
                            "templates/repository-template.md",
                            "templates/test-templates-repository.md",
                        ],
                    },
                    "phase-5b": {
                        "service": [
                            "skills/application-layer.md",
                            "skills/bootstrapper.md",
                            "templates/data-mapping-template.md",
                            "templates/service-template.md",
                            "templates/structure-validator-template.md",
                            "templates/test-templates-service.md",
                        ],
                        "endpoint": [
                            "skills/application-layer.md",
                            "skills/bootstrapper.md",
                            "skills/api.md",
                            "skills/testing.md",
                            "templates/endpoint-template.md",
                            "templates/exception-handler-template.md",
                            "templates/test-templates-endpoint.md",
                        ],
                    },
                }
            },
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            write_json(root / "_manifest.json", manifest)
            write_json(root / "phase-load-packs.json", phase_load_packs)

            result = run_script(
                "report-context-budgets.py",
                "--root",
                str(root),
                "--mode",
                "full",
                "--as-json",
            )

        payload = json.loads(result.stdout)

        self.assertEqual(payload["corePhases"]["phase-4"]["totalEstimatedTokens"], 3500)
        self.assertEqual(payload["corePhases"]["phase-5-base"]["totalEstimatedTokens"], 5500)
        self.assertEqual(payload["sliceTotals"]["phase-5a"]["domain"]["totalEstimatedTokens"], 5000)
        self.assertEqual(payload["sliceTotals"]["phase-5a"]["repository"]["totalEstimatedTokens"], 9000)
        self.assertEqual(payload["sliceTotals"]["phase-5b"]["service"]["totalEstimatedTokens"], 8000)
        self.assertEqual(payload["sliceTotals"]["phase-5b"]["endpoint"]["totalEstimatedTokens"], 9500)
        self.assertTrue(payload["sliceTotals"]["phase-5b"]["endpoint"]["withinCompact"])

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

    def test_get_phase_load_set_resolves_slice_profiles_and_dependencies(self):
        manifest = {
            "contextBudget": {"default": 200, "extended": 400, "compact": 100},
            "modeExclusions": {"lite": [], "api-only": []},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 7},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 9},
                {"path": "skills/application-layer.md", "phase": "phase-5b", "estimatedTokens": 11},
                {"path": "skills/bootstrapper.md", "phase": "phase-5b", "estimatedTokens": 13},
                {
                    "path": "skills/api.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 17,
                    "dependencies": ["skills/application-layer.md", "skills/bootstrapper.md"],
                },
                {"path": "templates/entity-template.md", "phase": "phase-5a", "estimatedTokens": 19},
                {
                    "path": "templates/data-mapping-template.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 23,
                    "requires": ["templates/entity-template.md"],
                },
                {
                    "path": "templates/service-template.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 29,
                    "requires": ["templates/data-mapping-template.md"],
                },
                {
                    "path": "templates/structure-validator-template.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 31,
                    "requires": ["templates/data-mapping-template.md", "skills/application-layer.md"],
                },
                {
                    "path": "templates/endpoint-template.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 37,
                    "requires": ["templates/service-template.md", "skills/api.md"],
                },
                {
                    "path": "templates/exception-handler-template.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 41,
                    "requires": ["skills/api.md"],
                },
                {"path": "skills/testing.md", "phase": "phase-5e", "estimatedTokens": 43},
                {
                    "path": "templates/test-templates-endpoint.md",
                    "phase": "phase-5b",
                    "estimatedTokens": 47,
                    "requires": ["templates/endpoint-template.md", "skills/testing.md"],
                },
                {"path": "templates/test-templates-service.md", "phase": "phase-5b", "estimatedTokens": 53},
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
                "5b",
                "--slice",
                "endpoint",
                "--mode",
                "full",
                "--budget-profile",
                "extended",
                "--as-json",
            )

        payload = json.loads(result.stdout)

        self.assertEqual(payload["slice"], "endpoint")
        self.assertEqual(payload["availableSlices"], ["service", "endpoint"])
        self.assertEqual(
            {item["path"] for item in payload["requestedFiles"]},
            {
                "skills/application-layer.md",
                "skills/bootstrapper.md",
                "skills/api.md",
                "skills/testing.md",
                "templates/endpoint-template.md",
                "templates/exception-handler-template.md",
                "templates/test-templates-endpoint.md",
            },
        )
        self.assertEqual(payload["dependencyFiles"], [])

    def test_get_phase_load_set_fails_on_unknown_slice(self):
        manifest = {
            "contextBudget": {"default": 200, "extended": 400, "compact": 100},
            "modeExclusions": {"lite": [], "api-only": []},
            "files": [
                {"path": "_manifest.json", "phase": "metadata", "estimatedTokens": 1},
                {"path": "ai/placeholder-tokens.md", "phase": "phase-5-base", "estimatedTokens": 7},
                {"path": "support/ef-packages-reference.md", "phase": "phase-5-base", "estimatedTokens": 9},
                {"path": "skills/domain-model.md", "phase": "phase-5a", "estimatedTokens": 11},
                {"path": "skills/data-persistence.md", "phase": "phase-5a", "estimatedTokens": 13},
                {"path": "templates/entity-template.md", "phase": "phase-5a", "estimatedTokens": 17},
                {"path": "templates/domain-rules-template.md", "phase": "phase-5a", "estimatedTokens": 19},
                {"path": "templates/ef-configuration-template.md", "phase": "phase-5a", "estimatedTokens": 23},
                {"path": "templates/repository-template.md", "phase": "phase-5a", "estimatedTokens": 29},
                {"path": "templates/test-templates-domain.md", "phase": "phase-5a", "estimatedTokens": 31},
                {"path": "templates/test-templates-repository.md", "phase": "phase-5a", "estimatedTokens": 37},
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
                "5a",
                "--slice",
                "missing",
                "--mode",
                "full",
            )

        self.assertIn("Unknown slice 'missing'", result.stderr)
        self.assertIn("Available slices: domain, repository.", result.stderr)

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


if __name__ == "__main__":
    unittest.main()
