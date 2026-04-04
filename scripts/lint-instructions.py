#!/usr/bin/env python3
"""Lint instruction files for common issues."""

import argparse
import json
import re
import sys
from pathlib import Path


def add_issue(category, file, message, line=0):
    return {"Category": category, "File": file, "Line": line, "Message": message}


def main():
    parser = argparse.ArgumentParser(description="Lint instruction files")
    parser.add_argument("--root", "-Root", default=None,
                        help="Root directory (defaults to parent of scripts/)")
    args = parser.parse_args()

    root = Path(args.root) if args.root else Path(__file__).resolve().parent.parent

    issues = []

    md_files = list(root.rglob("*.md"))

    # -------------------------------------------------------------------------
    # 1) Template symbol contract checks
    # -------------------------------------------------------------------------
    agent_template_path = root / "templates" / "agent-template.md"
    service_template_path = root / "templates" / "service-template.md"

    if not agent_template_path.exists():
        issues.append(add_issue("TemplateContract", "templates/agent-template.md",
                                "Missing required template file."))
    if not service_template_path.exists():
        issues.append(add_issue("TemplateContract", "templates/service-template.md",
                                "Missing required template file."))

    if agent_template_path.exists() and service_template_path.exists():
        agent_content = agent_template_path.read_text(encoding="utf-8")
        service_content = service_template_path.read_text(encoding="utf-8")

        if re.search(r"GetByIdAsync\(", agent_content):
            issues.append(add_issue(
                "TemplateContract", "templates/agent-template.md",
                "Found GetByIdAsync(...) reference. Use GetAsync(...) to match service template contract."
            ))

        if not re.search(r"GetAsync\(Guid\.Parse\(id\),\s*ct\)", agent_content):
            issues.append(add_issue(
                "TemplateContract", "templates/agent-template.md",
                "Agent template does not show GetAsync(Guid.Parse(id), ct) usage."
            ))

        if not re.search(r"Task<Result<DefaultResponse<\{Entity\}Dto>>>\s+GetAsync\(", service_content):
            issues.append(add_issue(
                "TemplateContract", "templates/service-template.md",
                "Service template is missing expected GetAsync(...) contract signature."
            ))

    # -------------------------------------------------------------------------
    # 2) Markdown shell fence checks
    # -------------------------------------------------------------------------
    for file in md_files:
        relative_file = file.relative_to(root).as_posix()
        try:
            lines = file.read_text(encoding="utf-8").splitlines()
        except Exception:
            continue

        in_fence = False
        fence_lang = ""
        fence_start_line = 0
        fence_lines = []

        for index, line in enumerate(lines):
            line_number = index + 1

            if not in_fence:
                fence_start = re.match(r"^```(\w+)?\s*$", line)
                if fence_start:
                    in_fence = True
                    fence_lang = fence_start.group(1) or ""
                    fence_start_line = line_number
                    fence_lines = []
                continue

            if re.match(r"^```\s*$", line):
                if fence_lang == "bash":
                    has_ps_syntax = False
                    for fl in fence_lines:
                        if re.search(r"\$env:", fl) or re.search(r"\bSet-Item\b", fl) or re.search(r"\bGet-ChildItem\b", fl):
                            has_ps_syntax = True
                            break

                    if has_ps_syntax:
                        issues.append(add_issue(
                            "ShellFence", relative_file,
                            "bash code fence contains PowerShell-specific syntax.",
                            fence_start_line
                        ))

                in_fence = False
                fence_lang = ""
                fence_start_line = 0
                fence_lines = []
                continue

            fence_lines.append(line)

    # -------------------------------------------------------------------------
    # 3) Broken local markdown links
    # -------------------------------------------------------------------------
    for file in md_files:
        relative_file = file.relative_to(root).as_posix()
        try:
            content = file.read_text(encoding="utf-8")
        except Exception:
            continue

        link_targets = re.findall(r"\[[^\]]+\]\(([^)]+)\)", content)
        for target in link_targets:
            if re.match(r"^(http|https|mailto):", target):
                continue
            if target.startswith("#"):
                continue

            target_no_anchor = target.split("#")[0]
            if not target_no_anchor.strip():
                continue

            resolved = (file.parent / target_no_anchor).resolve()
            if not resolved.exists():
                issues.append(add_issue(
                    "Links", relative_file,
                    f"Broken local link target: {target_no_anchor}"
                ))

    # -------------------------------------------------------------------------
    # 4) Terminology drift checks
    # -------------------------------------------------------------------------
    drift_patterns = [
        {
            "pattern": r"UPDATE_INSTRUCTIONS\.md",
            "message": "Use UPDATE-INSTRUCTIONS.md (hyphen), not UPDATE_INSTRUCTIONS.md.",
            "category": "Terminology",
        },
        {
            "pattern": r"codeunless",
            "message": 'Typo detected: use "code unless".',
            "category": "Terminology",
        },
    ]

    for file in md_files:
        relative_file = file.relative_to(root).as_posix()
        try:
            lines = file.read_text(encoding="utf-8").splitlines()
        except Exception:
            continue

        for index, line in enumerate(lines):
            line_number = index + 1
            for rule in drift_patterns:
                if re.search(rule["pattern"], line):
                    issues.append(add_issue(
                        rule["category"], relative_file,
                        rule["message"], line_number
                    ))

    # -------------------------------------------------------------------------
    # 5) Placeholder token coverage
    # -------------------------------------------------------------------------
    placeholder_doc_path = root / "ai" / "placeholder-tokens.md"
    known_tokens = []

    if not placeholder_doc_path.exists():
        issues.append(add_issue(
            "PlaceholderCoverage", "ai/placeholder-tokens.md",
            "Missing placeholder token glossary."
        ))
    else:
        placeholder_content = placeholder_doc_path.read_text(encoding="utf-8")
        known_tokens = sorted(set(re.findall(r"\{[A-Za-z][A-Za-z0-9-]*\}", placeholder_content)))

        required_canonical_tokens = [
            "{Project}", "{Org}", "{App}", "{Host}", "{Entity}", "{entity}",
            "{Entities}", "{entities}", "{entity-route}", "{ChildEntity}",
            "{Children}", "{Feature}", "{Gateway}",
            "{SolutionName}", "{entra-tenant-id}", "{api-client-id}",
            "{Agent}", "{agent-route}", "{Tool}", "{SearchIndex}",
        ]

        for token in required_canonical_tokens:
            if token not in known_tokens:
                issues.append(add_issue(
                    "PlaceholderCoverage", "placeholder-tokens.md",
                    f"Missing canonical token in glossary: {token}"
                ))

        # Check coverage in templates/skills/root .md files
        coverage_files = []
        templates_dir = root / "templates"
        skills_dir = root / "skills"
        if templates_dir.exists():
            coverage_files.extend(templates_dir.rglob("*.md"))
        if skills_dir.exists():
            coverage_files.extend(skills_dir.rglob("*.md"))
        coverage_files.extend(
            f for f in root.glob("*.md")
            if f.name != "placeholder-tokens.md"
        )

        coverage_text = ""
        for cf in coverage_files:
            try:
                coverage_text += cf.read_text(encoding="utf-8") + "\n"
            except Exception:
                continue

        for token in required_canonical_tokens:
            if re.escape(token) and token not in coverage_text:
                issues.append(add_issue(
                    "PlaceholderCoverage", "templates/|skills/",
                    f"Canonical token is not referenced in docs/templates: {token}"
                ))

    # -------------------------------------------------------------------------
    # 6) Duplicate critical gotcha entries outside canonical file
    # -------------------------------------------------------------------------
    canonical_troubleshooting_path = root / "support" / "troubleshooting.md"
    if not canonical_troubleshooting_path.exists():
        issues.append(add_issue(
            "GotchaDuplication", "support/troubleshooting.md",
            "Missing canonical troubleshooting file."
        ))
    else:
        critical_phrases = [
            "Search returns empty/0 results",
            "Search returns 500 / negative OFFSET",
            "All writes return `NotImplementedException`",
            "Rate-limited 429 in integration tests",
            "StructureValidator not found",
        ]

        for file in md_files:
            relative_file = file.relative_to(root).as_posix()
            if relative_file == "support/troubleshooting.md":
                continue
            try:
                content = file.read_text(encoding="utf-8")
            except Exception:
                continue
            for phrase in critical_phrases:
                if phrase in content:
                    issues.append(add_issue(
                        "GotchaDuplication", relative_file,
                        f"Critical gotcha phrase duplicated outside canonical file: {phrase}"
                    ))

    # -------------------------------------------------------------------------
    # 7) Skill file existence
    # -------------------------------------------------------------------------
    skill_md_path = root / "ai" / "SKILL.md"
    if skill_md_path.exists():
        skill_content = skill_md_path.read_text(encoding="utf-8")
        skill_refs = sorted(set(re.findall(r"skills/[a-z0-9-]+\.md", skill_content)))

        for ref in skill_refs:
            full_path = root / ref
            if not full_path.exists():
                issues.append(add_issue(
                    "MissingSkill", "ai/SKILL.md",
                    f"Referenced skill file does not exist: {ref}"
                ))

    # -------------------------------------------------------------------------
    # 8) Manifest <-> SKILL.md phase alignment
    # -------------------------------------------------------------------------
    manifest_path = root / "_manifest.json"
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

        for entry in manifest["files"]:
            entry_path = root / entry["path"]
            if not entry_path.exists():
                issues.append(add_issue(
                    "ManifestSync", "_manifest.json",
                    f"Manifest entry references missing file: {entry['path']}"
                ))

        # Check that all .md files (except sample-app/, bin/, obj/) are in the manifest
        manifest_path_set = set()
        for entry in manifest["files"]:
            normalized = entry["path"].replace("/", "\\").lower()
            manifest_path_set.add(normalized)

        for file in md_files:
            relative_file = file.relative_to(root).as_posix()
            if relative_file.startswith("sample-app/"):
                continue
            if re.search(r"(^|/)bin/", relative_file) or re.search(r"(^|/)obj/", relative_file):
                continue
            if relative_file == "CHANGELOG.md":
                continue
            lookup_key = relative_file.replace("/", "\\").lower()
            if lookup_key not in manifest_path_set:
                issues.append(add_issue(
                    "ManifestSync", relative_file,
                    "Markdown file exists but is not listed in _manifest.json."
                ))

    # -------------------------------------------------------------------------
    # 9) Undefined placeholder tokens in templates
    # -------------------------------------------------------------------------
    if placeholder_doc_path.exists() and known_tokens:
        template_dir = root / "templates"
        if template_dir.exists():
            template_files = list(template_dir.rglob("*.md"))
            skip_patterns = {
                "get", "set", "init", "value", "index", "item", "key",
                "options", "ctx", "context", "provider", "sp", "builder",
                "db", "app", "config", "services", "host",
                "T", "TEntity", "TDto", "TContext", "TAuditId", "TTenantId",
                "TProperty", "TResult", "TResponse", "TRequest", "TFilter",
                "TProgram",
                "Namespace", "ParentEntity", "PropertyName", "PropertyType",
                "RuleName", "Event", "EventPayload", "Target",
                "NameMaxLength", "DescriptionMaxLength", "MaxChildren",
                "DtoProperty", "ErrorMessage", "Condition",
                "FieldName", "FieldType", "TableName", "ColumnName",
                "HandlerName", "JobName", "ScheduleExpression",
                "ValidatorName", "ServiceName", "MethodName",
                "IndexField", "SearchField", "ModelName", "DeploymentName",
                "Parent", "Related", "Enum", "Name", "Id", "Message",
                "Action", "EventName", "Handler", "User", "Query", "Mode",
                "Count", "DocumentId", "EntityId", "ExceptionType",
                "AgentName", "FunctionName", "MessageCount", "ResponseCount",
                "Property1", "Property2", "SomeOtherProp",
            }

            for t_file in template_files:
                try:
                    t_content = t_file.read_text(encoding="utf-8")
                except Exception:
                    continue
                relative_file = t_file.relative_to(root).as_posix()

                token_results = re.findall(r"\{([A-Z][A-Za-z0-9-]*)\}", t_content)
                for inner_token in token_results:
                    if inner_token in skip_patterns:
                        continue
                    token = "{" + inner_token + "}"
                    if token not in known_tokens:
                        issues.append(add_issue(
                            "UndefinedToken", relative_file,
                            f"Placeholder token not defined in glossary: {token}"
                        ))

    # -------------------------------------------------------------------------
    # 10) Manifest invariants for load orchestration
    # -------------------------------------------------------------------------
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

        if "modeExclusions" not in manifest:
            issues.append(add_issue(
                "ManifestInvariants", "_manifest.json",
                "Manifest is missing top-level modeExclusions."
            ))
        else:
            for mode in ["lite", "api-only"]:
                if mode not in manifest["modeExclusions"]:
                    issues.append(add_issue(
                        "ManifestInvariants", "_manifest.json",
                        f"modeExclusions is missing '{mode}'."
                    ))
                    continue

                for path in manifest["modeExclusions"][mode]:
                    if not re.match(r"^skills/.+\.md$", path):
                        issues.append(add_issue(
                            "ManifestInvariants", "_manifest.json",
                            f"Mode exclusion '{path}' for '{mode}' must reference a skill markdown file."
                        ))
                        continue
                    full_path = root / path
                    if not full_path.exists():
                        issues.append(add_issue(
                            "ManifestInvariants", "_manifest.json",
                            f"Mode exclusion '{path}' for '{mode}' does not exist on disk."
                        ))

            # identity-management.md must be in phase-4f
            identity_entries = [
                e for e in manifest["files"]
                if e["path"] == "skills/identity-management.md"
            ]
            if not identity_entries:
                issues.append(add_issue(
                    "ManifestInvariants", "_manifest.json",
                    "skills/identity-management.md is missing from manifest.files."
                ))
            elif str(identity_entries[0]["phase"]) != "phase-4f":
                issues.append(add_issue(
                    "ManifestInvariants", "_manifest.json",
                    "skills/identity-management.md must be assigned to phase-4f."
                ))

            phase_load_packs_path = root / "phase-load-packs.json"
            if not phase_load_packs_path.exists():
                issues.append(add_issue(
                    "ManifestInvariants", "phase-load-packs.json",
                    "Missing generated phase-load-packs.json."
                ))
            else:
                plp = json.loads(phase_load_packs_path.read_text(encoding="utf-8"))
                generated_phase_order = plp["phaseOrder"]
                manifest_phases = sorted(set(
                    str(e["phase"]) for e in manifest["files"]
                ))

                for phase in manifest_phases:
                    if phase not in generated_phase_order:
                        issues.append(add_issue(
                            "ManifestInvariants", "phase-load-packs.json",
                            f"Generated phaseOrder is missing manifest phase '{phase}'."
                        ))

                for mode in ["full", "lite", "api-only"]:
                    if mode not in plp["packs"]:
                        issues.append(add_issue(
                            "ManifestInvariants", "phase-load-packs.json",
                            f"Generated packs is missing mode '{mode}'."
                        ))
                        continue

                    for phase in manifest_phases:
                        if phase not in plp["packs"][mode]:
                            issues.append(add_issue(
                                "ManifestInvariants", "phase-load-packs.json",
                                f"Generated mode '{mode}' is missing phase '{phase}'."
                            ))

    # -------------------------------------------------------------------------
    # Output
    # -------------------------------------------------------------------------
    if not issues:
        print("PASS: instruction lint checks passed.")
        sys.exit(0)

    print(f"FAIL: instruction lint checks found {len(issues)} issue(s).")

    # Sort and print table
    issues.sort(key=lambda x: (x["Category"], x["File"], x["Line"]))

    col_cat = max((len(i["Category"]) for i in issues), default=8)
    col_file = max((len(i["File"]) for i in issues), default=4)
    col_line = max((len(str(i["Line"])) for i in issues), default=4)
    col_msg = max((len(i["Message"]) for i in issues), default=7)

    col_cat = max(col_cat, len("Category"))
    col_file = max(col_file, len("File"))
    col_line = max(col_line, len("Line"))
    col_msg = max(col_msg, len("Message"))

    header = (f"{'Category':<{col_cat}}  {'File':<{col_file}}  "
              f"{'Line':>{col_line}}  {'Message':<{col_msg}}")
    print()
    print(header)
    print("-" * len(header))
    for issue in issues:
        print(f"{issue['Category']:<{col_cat}}  {issue['File']:<{col_file}}  "
              f"{issue['Line']:>{col_line}}  {issue['Message']:<{col_msg}}")
    print()

    sys.exit(1)


if __name__ == "__main__":
    main()
