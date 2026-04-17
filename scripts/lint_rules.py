#!/usr/bin/env python3
"""Shared lint rules for instruction files."""

import json
import re
from pathlib import Path


def add_issue(category, file, message, line=0):
    return {"Category": category, "File": file, "Line": line, "Message": message}


def should_ignore_markdown_path(relative_path):
    return (
        re.search(r"(^|/)(\.git|\.github|\.claude|bin|obj|\.venv|venv|env)/", relative_path) is not None
    )


def is_manifest_scoped_path(relative_path):
    return relative_path == "_manifest.json" or (
        relative_path.endswith(".md")
        and not should_ignore_markdown_path(relative_path)
    )


def get_markdown_files(root):
    return [
        file for file in root.rglob("*.md")
        if not should_ignore_markdown_path(file.relative_to(root).as_posix())
    ]


def read_text(path):
    try:
        return path.read_text(encoding="utf-8")
    except Exception:
        return None


def read_lines(path):
    content = read_text(path)
    if content is None:
        return None
    return content.splitlines()


def load_manifest(root):
    manifest_path = root / "_manifest.json"
    content = read_text(manifest_path)
    if content is None:
        return None
    return json.loads(content)


def check_template_contracts(root):
    issues = []
    agent_template_path = root / "templates" / "agent-template.md"
    service_template_path = root / "templates" / "service-template.md"

    if not agent_template_path.exists():
        issues.append(add_issue(
            "TemplateContract",
            "templates/agent-template.md",
            "Missing required template file."
        ))
    if not service_template_path.exists():
        issues.append(add_issue(
            "TemplateContract",
            "templates/service-template.md",
            "Missing required template file."
        ))

    if not (agent_template_path.exists() and service_template_path.exists()):
        return issues

    agent_content = read_text(agent_template_path)
    service_content = read_text(service_template_path)
    if agent_content is None or service_content is None:
        return issues

    if re.search(r"GetByIdAsync\(", agent_content):
        issues.append(add_issue(
            "TemplateContract",
            "templates/agent-template.md",
            "Found GetByIdAsync(...) reference. Use GetAsync(...) to match service template contract."
        ))

    if not re.search(r"GetAsync\(Guid\.Parse\(id\),\s*ct\)", agent_content):
        issues.append(add_issue(
            "TemplateContract",
            "templates/agent-template.md",
            "Agent template does not show GetAsync(Guid.Parse(id), ct) usage."
        ))

    if not re.search(r"Task<Result<DefaultResponse<\{Entity\}Dto>>>\s+GetAsync\(", service_content):
        issues.append(add_issue(
            "TemplateContract",
            "templates/service-template.md",
            "Service template is missing expected GetAsync(...) contract signature."
        ))

    return issues


def check_shell_fences(root, md_files):
    issues = []

    for file in md_files:
        relative_file = file.relative_to(root).as_posix()
        lines = read_lines(file)
        if lines is None:
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
                    has_ps_syntax = any(
                        re.search(r"\$env:", fence_line)
                        or re.search(r"\bSet-Item\b", fence_line)
                        or re.search(r"\bGet-ChildItem\b", fence_line)
                        for fence_line in fence_lines
                    )
                    if has_ps_syntax:
                        issues.append(add_issue(
                            "ShellFence",
                            relative_file,
                            "bash code fence contains PowerShell-specific syntax.",
                            fence_start_line,
                        ))

                in_fence = False
                fence_lang = ""
                fence_start_line = 0
                fence_lines = []
                continue

            fence_lines.append(line)

    return issues


def check_broken_links(root, md_files):
    issues = []

    for file in md_files:
        relative_file = file.relative_to(root).as_posix()
        content = read_text(file)
        if content is None:
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
                    "Links",
                    relative_file,
                    f"Broken local link target: {target_no_anchor}",
                ))

    return issues


def check_terminology_drift(root, md_files):
    issues = []
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
        lines = read_lines(file)
        if lines is None:
            continue

        for index, line in enumerate(lines):
            line_number = index + 1
            for rule in drift_patterns:
                if re.search(rule["pattern"], line):
                    issues.append(add_issue(
                        rule["category"],
                        relative_file,
                        rule["message"],
                        line_number,
                    ))

    return issues


def check_placeholder_coverage(root):
    issues = []
    placeholder_doc_path = root / "ai" / "placeholder-tokens.md"
    known_tokens = []

    if not placeholder_doc_path.exists():
        issues.append(add_issue(
            "PlaceholderCoverage",
            "ai/placeholder-tokens.md",
            "Missing placeholder token glossary."
        ))
        return issues, placeholder_doc_path, known_tokens

    placeholder_content = read_text(placeholder_doc_path)
    if placeholder_content is None:
        return issues, placeholder_doc_path, known_tokens

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
                "PlaceholderCoverage",
                "placeholder-tokens.md",
                f"Missing canonical token in glossary: {token}",
            ))

    coverage_files = []
    templates_dir = root / "templates"
    skills_dir = root / "skills"
    if templates_dir.exists():
        coverage_files.extend(templates_dir.rglob("*.md"))
    if skills_dir.exists():
        coverage_files.extend(skills_dir.rglob("*.md"))
    coverage_files.extend(
        file for file in root.glob("*.md") if file.name != "placeholder-tokens.md"
    )

    coverage_text = ""
    for coverage_file in coverage_files:
        content = read_text(coverage_file)
        if content is not None:
            coverage_text += content + "\n"

    for token in required_canonical_tokens:
        if token not in coverage_text:
            issues.append(add_issue(
                "PlaceholderCoverage",
                "templates/|skills/",
                f"Canonical token is not referenced in docs/templates: {token}",
            ))

    return issues, placeholder_doc_path, known_tokens


def check_gotcha_duplication(root, md_files):
    issues = []
    canonical_troubleshooting_path = root / "support" / "troubleshooting.md"
    if not canonical_troubleshooting_path.exists():
        issues.append(add_issue(
            "GotchaDuplication",
            "support/troubleshooting.md",
            "Missing canonical troubleshooting file.",
        ))
        return issues

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

        content = read_text(file)
        if content is None:
            continue

        for phrase in critical_phrases:
            if phrase in content:
                issues.append(add_issue(
                    "GotchaDuplication",
                    relative_file,
                    f"Critical gotcha phrase duplicated outside canonical file: {phrase}",
                ))

    return issues


def check_skill_references(root):
    issues = []
    skill_md_path = root / "ai" / "SKILL.md"
    if not skill_md_path.exists():
        return issues

    skill_content = read_text(skill_md_path)
    if skill_content is None:
        return issues

    skill_refs = sorted(set(re.findall(r"skills/[a-z0-9-]+\.md", skill_content)))
    for ref in skill_refs:
        full_path = root / ref
        if not full_path.exists():
            issues.append(add_issue(
                "MissingSkill",
                "ai/SKILL.md",
                f"Referenced skill file does not exist: {ref}",
            ))

    return issues


def check_manifest_sync(root, md_files):
    issues = []
    manifest = load_manifest(root)
    if manifest is None:
        return issues

    for entry in manifest["files"]:
        entry_path = root / entry["path"]
        if not entry_path.exists():
            issues.append(add_issue(
                "ManifestSync",
                "_manifest.json",
                f"Manifest entry references missing file: {entry['path']}",
            ))

    manifest_path_set = {
        entry["path"].replace("/", "\\").lower()
        for entry in manifest["files"]
    }

    for file in md_files:
        relative_file = file.relative_to(root).as_posix()
        if relative_file == "CHANGELOG.md":
            continue
        lookup_key = relative_file.replace("/", "\\").lower()
        if lookup_key not in manifest_path_set:
            issues.append(add_issue(
                "ManifestSync",
                relative_file,
                "Markdown file exists but is not listed in _manifest.json.",
            ))

    return issues


def check_undefined_template_tokens(root, placeholder_doc_path, known_tokens):
    issues = []
    if not placeholder_doc_path.exists() or not known_tokens:
        return issues

    template_dir = root / "templates"
    if not template_dir.exists():
        return issues

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
        "Binding",
    }

    for template_file in template_dir.rglob("*.md"):
        content = read_text(template_file)
        if content is None:
            continue
        relative_file = template_file.relative_to(root).as_posix()
        token_results = re.findall(r"\{([A-Z][A-Za-z0-9-]*)\}", content)
        for inner_token in token_results:
            if inner_token in skip_patterns:
                continue
            token = "{" + inner_token + "}"
            if token not in known_tokens:
                issues.append(add_issue(
                    "UndefinedToken",
                    relative_file,
                    f"Placeholder token not defined in glossary: {token}",
                ))

    return issues


def check_manifest_invariants(root):
    issues = []
    manifest = load_manifest(root)
    if manifest is None:
        return issues

    for entry in manifest["files"]:
        relative_path = str(entry["path"])
        if not is_manifest_scoped_path(relative_path):
            issues.append(add_issue(
                "ManifestInvariants",
                "_manifest.json",
                f"Manifest entry is outside declared scope: {relative_path}.",
            ))

    if "modeExclusions" not in manifest:
        issues.append(add_issue(
            "ManifestInvariants",
            "_manifest.json",
            "Manifest is missing top-level modeExclusions.",
        ))
    else:
        for mode in ["lite", "api-only"]:
            if mode not in manifest["modeExclusions"]:
                issues.append(add_issue(
                    "ManifestInvariants",
                    "_manifest.json",
                    f"modeExclusions is missing '{mode}'.",
                ))
                continue

            for path in manifest["modeExclusions"][mode]:
                if not re.match(r"^skills/.+\.md$", path):
                    issues.append(add_issue(
                        "ManifestInvariants",
                        "_manifest.json",
                        f"Mode exclusion '{path}' for '{mode}' must reference a skill markdown file.",
                    ))
                    continue

                full_path = root / path
                if not full_path.exists():
                    issues.append(add_issue(
                        "ManifestInvariants",
                        "_manifest.json",
                        f"Mode exclusion '{path}' for '{mode}' does not exist on disk.",
                    ))

    identity_entries = [
        entry for entry in manifest["files"] if entry["path"] == "skills/identity-management.md"
    ]
    if not identity_entries:
        issues.append(add_issue(
            "ManifestInvariants",
            "_manifest.json",
            "skills/identity-management.md is missing from manifest.files.",
        ))
    elif str(identity_entries[0]["phase"]) != "phase-5f":
        issues.append(add_issue(
            "ManifestInvariants",
            "_manifest.json",
            "skills/identity-management.md must be assigned to phase-5f.",
        ))

    phase_load_packs_path = root / "phase-load-packs.json"
    if not phase_load_packs_path.exists():
        issues.append(add_issue(
            "ManifestInvariants",
            "phase-load-packs.json",
            "Missing generated phase-load-packs.json.",
        ))
    else:
        phase_load_packs = json.loads(phase_load_packs_path.read_text(encoding="utf-8"))
        generated_phase_order = phase_load_packs["phaseOrder"]
        manifest_phases = sorted({str(entry["phase"]) for entry in manifest["files"]})

        for phase in manifest_phases:
            if phase not in generated_phase_order:
                issues.append(add_issue(
                    "ManifestInvariants",
                    "phase-load-packs.json",
                    f"Generated phaseOrder is missing manifest phase '{phase}'.",
                ))

        for mode in ["full", "lite", "api-only"]:
            if mode not in phase_load_packs["packs"]:
                issues.append(add_issue(
                    "ManifestInvariants",
                    "phase-load-packs.json",
                    f"Generated packs is missing mode '{mode}'.",
                ))
                continue

            for phase in manifest_phases:
                if phase not in phase_load_packs["packs"][mode]:
                    issues.append(add_issue(
                        "ManifestInvariants",
                        "phase-load-packs.json",
                        f"Generated mode '{mode}' is missing phase '{phase}'.",
                    ))

    return issues


def check_compact_slice_budgets(root):
    issues = []
    manifest = load_manifest(root)
    if manifest is None:
        return issues

    phase_load_packs_path = root / "phase-load-packs.json"
    content = read_text(phase_load_packs_path)
    if content is None:
        return issues

    phase_load_packs = json.loads(content)
    compact_budget = phase_load_packs.get("contextBudget", {}).get("compact")
    if compact_budget is None:
        issues.append(add_issue(
            "SliceBudget",
            "phase-load-packs.json",
            "Generated phase-load-packs.json is missing contextBudget.compact.",
        ))
        return issues

    expected_slices = {
        "phase-5a": ["domain", "repository"],
        "phase-5b": ["service", "endpoint"],
    }
    token_by_path = {
        str(entry["path"]): int(entry.get("estimatedTokens", 0))
        for entry in manifest["files"]
    }
    slices_by_mode = phase_load_packs.get("slices", {})

    for mode in ["full", "lite", "api-only"]:
        mode_slices = slices_by_mode.get(mode)
        if mode_slices is None:
            issues.append(add_issue(
                "SliceBudget",
                "phase-load-packs.json",
                f"Generated slices is missing mode '{mode}'.",
            ))
            continue

        for phase, slice_names in expected_slices.items():
            phase_slices = mode_slices.get(phase)
            if phase_slices is None:
                issues.append(add_issue(
                    "SliceBudget",
                    "phase-load-packs.json",
                    f"Generated slices is missing phase '{phase}' for mode '{mode}'.",
                ))
                continue

            for slice_name in slice_names:
                slice_paths = phase_slices.get(slice_name)
                if slice_paths is None:
                    issues.append(add_issue(
                        "SliceBudget",
                        "phase-load-packs.json",
                        f"Generated slices is missing '{mode}:{phase}:{slice_name}'.",
                    ))
                    continue

                missing_paths = [path for path in slice_paths if path not in token_by_path]
                for path in missing_paths:
                    issues.append(add_issue(
                        "SliceBudget",
                        "phase-load-packs.json",
                        f"Compact slice '{mode}:{phase}:{slice_name}' references '{path}' which is missing from _manifest.json.",
                    ))

                if missing_paths:
                    continue

                total_tokens = sum(token_by_path[path] for path in slice_paths)
                if total_tokens > compact_budget:
                    issues.append(add_issue(
                        "SliceBudget",
                        "phase-load-packs.json",
                        f"Compact slice '{mode}:{phase}:{slice_name}' exceeds compact budget ({total_tokens} > {compact_budget}).",
                    ))

    return issues


def collect_issues(root):
    md_files = get_markdown_files(root)
    issues = []

    issues.extend(check_template_contracts(root))
    issues.extend(check_shell_fences(root, md_files))
    issues.extend(check_broken_links(root, md_files))
    issues.extend(check_terminology_drift(root, md_files))

    placeholder_issues, placeholder_doc_path, known_tokens = check_placeholder_coverage(root)
    issues.extend(placeholder_issues)

    issues.extend(check_gotcha_duplication(root, md_files))
    issues.extend(check_skill_references(root))
    issues.extend(check_manifest_sync(root, md_files))
    issues.extend(check_undefined_template_tokens(root, placeholder_doc_path, known_tokens))
    issues.extend(check_manifest_invariants(root))
    issues.extend(check_compact_slice_budgets(root))

    return issues
