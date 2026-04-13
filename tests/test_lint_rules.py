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

    def test_check_compact_slice_budgets_reports_missing_required_slice(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest = {
                "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
                "files": [
                    {"path": "skills/domain-model.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/entity-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/domain-rules-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-domain.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "skills/data-persistence.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/ef-configuration-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/repository-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-repository.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "skills/application-layer.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "skills/bootstrapper.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/data-mapping-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/service-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/structure-validator-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-service.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "skills/api.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "skills/testing.md", "phase": "phase-5e", "estimatedTokens": 1000},
                    {"path": "templates/endpoint-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/exception-handler-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-endpoint.md", "phase": "phase-5b", "estimatedTokens": 1000},
                ],
            }
            phase_load_packs = {
                "contextBudget": {"compact": 15000},
                "slices": {
                    "full": {
                        "phase-5a": {
                            "domain": [
                                "skills/domain-model.md",
                                "templates/entity-template.md",
                                "templates/domain-rules-template.md",
                                "templates/test-templates-domain.md",
                            ]
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
                    },
                    "lite": {
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
                    },
                    "api-only": {
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
                    },
                },
            }

            write_file(root / "_manifest.json", json.dumps(manifest, indent=4))
            write_file(root / "phase-load-packs.json", json.dumps(phase_load_packs, indent=4))

            issues = lint_rules.check_compact_slice_budgets(root)

        messages = {issue["Message"] for issue in issues}
        self.assertIn("Generated slices is missing 'full:phase-5a:repository'.", messages)

    def test_check_compact_slice_budgets_reports_over_budget_slice(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            manifest = {
                "contextBudget": {"default": 30000, "extended": 60000, "compact": 15000},
                "files": [
                    {"path": "skills/domain-model.md", "phase": "phase-5a", "estimatedTokens": 9000},
                    {"path": "templates/entity-template.md", "phase": "phase-5a", "estimatedTokens": 8000},
                    {"path": "templates/domain-rules-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-domain.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "skills/data-persistence.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/ef-configuration-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/repository-template.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-repository.md", "phase": "phase-5a", "estimatedTokens": 1000},
                    {"path": "skills/application-layer.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "skills/bootstrapper.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/data-mapping-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/service-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/structure-validator-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-service.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "skills/api.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "skills/testing.md", "phase": "phase-5e", "estimatedTokens": 1000},
                    {"path": "templates/endpoint-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/exception-handler-template.md", "phase": "phase-5b", "estimatedTokens": 1000},
                    {"path": "templates/test-templates-endpoint.md", "phase": "phase-5b", "estimatedTokens": 1000},
                ],
            }
            slices = {
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
            phase_load_packs = {
                "contextBudget": {"compact": 15000},
                "slices": {
                    "full": slices,
                    "lite": slices,
                    "api-only": slices,
                },
            }

            write_file(root / "_manifest.json", json.dumps(manifest, indent=4))
            write_file(root / "phase-load-packs.json", json.dumps(phase_load_packs, indent=4))

            issues = lint_rules.check_compact_slice_budgets(root)

        self.assertTrue(
            any(
                issue["Category"] == "SliceBudget"
                and "Compact slice 'full:phase-5a:domain' exceeds compact budget (19000 > 15000)." in issue["Message"]
                for issue in issues
            )
        )


if __name__ == "__main__":
    unittest.main()
