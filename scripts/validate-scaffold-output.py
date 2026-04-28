#!/usr/bin/env python3
"""Validate generated scaffold structure without building or mutating the app."""

import argparse
import re
import sys
from pathlib import Path


IGNORED_PARTS = {".git", ".instructions", "bin", "obj", ".tmp"}
PLACEHOLDER_TOKENS = [
    "{Project}", "{Org}", "{App}", "{Host}", "{Entity}", "{entity}",
    "{Entities}", "{entities}", "{Gateway}", "{SolutionName}",
]


def add_issue(issues, category, path, message):
    issues.append({
        "category": category,
        "path": str(path),
        "message": message,
    })


def read_text(path):
    return path.read_text(encoding="utf-8") if path.is_file() else ""


def get_entities_section(content):
    start_match = re.search(r"(?m)^entities:\s*$", content)
    if not start_match:
        return ""
    start = start_match.start()
    next_top_level = re.search(r"(?m)^[A-Za-z][A-Za-z0-9]*:", content[start_match.end():])
    if next_top_level:
        return content[start:start_match.end() + next_top_level.start()]
    return content[start:]


def get_entity_names(resource_content):
    return re.findall(r"(?m)^  -\s*name:\s*([A-Z][A-Za-z0-9]*)\b", get_entities_section(resource_content))


def get_scalar_bool(content, key, default=False):
    match = re.search(rf"(?m)^{re.escape(key)}:\s*(true|false)\b", content, re.IGNORECASE)
    if not match:
        return default
    return match.group(1).lower() == "true"


def get_scalar_value(content, key, default=""):
    match = re.search(rf"(?m)^{re.escape(key)}:\s*([A-Za-z0-9_.-]+)", content)
    return match.group(1) if match else default


def iter_files(root, patterns):
    if isinstance(patterns, str):
        patterns = [patterns]
    for pattern in patterns:
        for path in root.rglob(pattern):
            if any(part in IGNORED_PARTS for part in path.parts):
                continue
            if path.is_file():
                yield path


def find_file(root, expected_name):
    expected = expected_name.lower()
    for path in iter_files(root, expected_name):
        if path.name.lower() == expected:
            return path
    for path in iter_files(root, "*"):
        if path.name.lower() == expected:
            return path
    return None


def find_file_containing(root, name_part, suffix=".cs"):
    needle = name_part.lower()
    for path in iter_files(root, "*" + suffix):
        if needle in path.name.lower():
            return path
    return None


def has_path_segment(root, segment):
    needle = segment.lower()
    for path in root.rglob("*"):
        if any(part in IGNORED_PARTS for part in path.parts):
            continue
        if needle in path.as_posix().lower():
            return True
    return False


def require_file(issues, root, relative_path, message):
    path = root / relative_path
    if not path.is_file():
        add_issue(issues, "RequiredFile", path, message)


def require_any_solution(issues, root):
    if not any(root.glob("*.slnx")):
        add_issue(issues, "Solution", root, "A .slnx solution file is required.")


def require_named_file(issues, root, name, category, message):
    path = find_file(root, name)
    if path is None:
        add_issue(issues, category, name, message)
    return path


def check_base_structure(root, phase, issues):
    require_any_solution(issues, root)
    require_file(issues, root, "Directory.Packages.props", "Central package management is required.")
    require_file(issues, root, "global.json", "SDK pinning is required.")
    require_file(issues, root, "nuget.config", "NuGet feed config is required.")
    require_file(issues, root, "domain-specification.yaml", "Phase 1 output is required.")
    require_file(issues, root, "UBIQUITOUS-LANGUAGE.md", "Phase 1 ubiquitous language artifact is required.")
    require_file(issues, root, "DESIGN-DECISIONS.md", "Phase 1 design decision artifact is required.")
    require_file(issues, root, "resource-implementation.yaml", "Phase 2 output is required.")
    require_file(issues, root, "implementation-plan.md", "Phase 3 output is required.")
    if phase in {"4", "final"}:
        require_file(issues, root, ".instruction-version", "Instruction version marker is required after Phase 4.")
    if phase == "final":
        require_file(issues, root, "HANDOFF.md", "Final handoff/resume record is required.")
    if not (root / "src").is_dir():
        add_issue(issues, "Layout", root / "src", "src/ directory is required.")


def check_entity_structure(root, entities, phase, issues):
    for entity in entities:
        expected = [
            (f"{entity}.cs", "Entity", f"{entity} entity class is missing."),
            (f"I{entity}Service.cs", "Contract", f"I{entity}Service contract is missing."),
            (f"I{entity}RepositoryTrxn.cs", "Contract", f"I{entity}RepositoryTrxn contract is missing."),
            (f"I{entity}RepositoryQuery.cs", "Contract", f"I{entity}RepositoryQuery contract is missing."),
            (f"{entity}Dto.cs", "Dto", f"{entity}Dto is missing."),
            (f"{entity}SearchFilter.cs", "Dto", f"{entity}SearchFilter is missing."),
            (f"{entity}Builder.cs", "TestSupport", f"{entity}Builder is missing."),
            (f"{entity}DtoBuilder.cs", "TestSupport", f"{entity}DtoBuilder is missing."),
        ]
        if phase == "final":
            expected.extend([
                (f"{entity}Service.cs", "Service", f"{entity}Service implementation is missing."),
                (f"{entity}Configuration.cs", "Data", f"{entity} EF configuration is missing."),
            ])

        for file_name, category, message in expected:
            require_named_file(issues, root, file_name, category, message)

        if phase == "final":
            if find_file_containing(root, entity + "Endpoint") is None and find_file_containing(root, entity + "Endpoints") is None:
                add_issue(issues, "Endpoint", entity, f"{entity} endpoint mapping file is missing.")
            if find_file_containing(root, entity + "ServiceTests") is None:
                add_issue(issues, "Tests", entity, f"{entity} service tests are missing.")
            if find_file_containing(root, entity + "EndpointTests") is None:
                add_issue(issues, "Tests", entity, f"{entity} endpoint tests are missing.")


def check_feature_paths(root, resource_content, issues):
    mode = get_scalar_value(resource_content, "scaffoldMode", "full")
    use_aspire = get_scalar_bool(resource_content, "useAspire", default=(mode == "full"))
    features = [
        ("includeGateway", "Gateway", "Gateway is enabled but no Gateway path was found."),
        ("includeScheduler", "Scheduler", "Scheduler is enabled but no Scheduler path was found."),
        ("includeFunctionApp", "Functions", "Function App is enabled but no Functions path was found."),
        ("includeUnoUI", "Uno", "Uno UI is enabled but no Uno path was found."),
        ("includeBlazorUI", "Blazor", "Blazor UI is enabled but no Blazor path was found."),
    ]
    if use_aspire and not has_path_segment(root, "AppHost"):
        add_issue(issues, "FeaturePath", "Aspire/AppHost", "Aspire is enabled but no AppHost path was found.")
    for flag, segment, message in features:
        if get_scalar_bool(resource_content, flag, default=False) and not has_path_segment(root, segment):
            add_issue(issues, "FeaturePath", segment, message)
    if get_scalar_bool(resource_content, "includeIaC", default=(mode == "full")) and not (root / "infra" / "main.bicep").is_file():
        add_issue(issues, "FeaturePath", root / "infra" / "main.bicep", "IaC is enabled but infra/main.bicep is missing.")
    if get_scalar_bool(resource_content, "includeGitHubActions", default=False) and not has_path_segment(root, ".github/workflows"):
        add_issue(issues, "FeaturePath", ".github/workflows", "GitHub Actions enabled but no workflow was found.")


def check_final_cleanliness(root, issues):
    for path in iter_files(root, ["*.cs", "*.csproj", "*.json", "*.props", "*.bicep", "*.yml", "*.yaml"]):
        content = read_text(path)
        if path.suffix == ".cs" and "throw new NotImplementedException" in content:
            add_issue(issues, "Completeness", path, "NotImplementedException remains in generated source.")
        for token in PLACEHOLDER_TOKENS:
            if token in content:
                add_issue(issues, "Placeholder", path, f"Unresolved scaffold placeholder remains: {token}")


def print_issues(issues):
    if not issues:
        return
    issues.sort(key=lambda issue: (issue["category"], issue["path"], issue["message"]))
    width_category = max(len("Category"), *(len(issue["category"]) for issue in issues))
    width_path = max(len("Path"), *(len(issue["path"]) for issue in issues))
    print(f"{'Category':<{width_category}}  {'Path':<{width_path}}  Message")
    print("-" * (width_category + width_path + 11))
    for issue in issues:
        print(f"{issue['category']:<{width_category}}  {issue['path']:<{width_path}}  {issue['message']}")


def main():
    parser = argparse.ArgumentParser(description="Validate generated scaffold output structure.")
    parser.add_argument("--root", "-Root", default=".", help="Target app repo root.")
    parser.add_argument("--phase", choices=["4", "final"], default="final", help="Validation depth.")
    parser.add_argument("--resource-impl", default="resource-implementation.yaml", help="Resource implementation YAML path.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    resource_path = root / args.resource_impl
    issues = []

    if not root.exists():
        add_issue(issues, "Root", root, "Target root does not exist.")
    elif not resource_path.is_file():
        add_issue(issues, "RequiredFile", resource_path, "resource-implementation.yaml is required.")
    else:
        resource_content = read_text(resource_path)
        entities = get_entity_names(resource_content)
        if not entities:
            add_issue(issues, "Resource", resource_path, "No entities found in resource-implementation.yaml.")
        check_base_structure(root, args.phase, issues)
        check_entity_structure(root, entities, args.phase, issues)
        if args.phase == "final":
            check_feature_paths(root, resource_content, issues)
            check_final_cleanliness(root, issues)

    if issues:
        print(f"FAIL: scaffold output validation found {len(issues)} issue(s).")
        print()
        print_issues(issues)
        return 1

    print(f"PASS: scaffold output validation ({args.phase}) passed for {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
