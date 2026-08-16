#!/usr/bin/env python3
"""Validate the repository foreground-interaction policy wiring."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import yaml


POLICY_REFERENCE = "foreground-interaction.md"
POLICY_PATH = ".agents/rules/foreground-interaction.md"
AGENTS_PATH = "AGENTS.md"
UNITY_CORE_SKILL_PATH = ".agents/skills/unity-mcp-core/SKILL.md"
MCP_TROUBLESHOOTING_SKILL_PATH = ".agents/skills/mcp-connection-troubleshooting/SKILL.md"
PURE_RUN_ARTWORK_SKILL_PATH = ".agents/skills/pure-run-artwork-pipeline/SKILL.md"
GODOT_LIFECYCLE_SKILL_PATH = ".agents/skills/godot-editor-lifecycle/SKILL.md"
CATALOG_PATH = ".agents/knowledge/catalog-scopes.yaml"

REQUIRED_POLICY_TOKENS = (
    "policy-id: foreground-interaction",
    "default-deny",
    "current-turn-explicit-request",
    "action-time-confirmation",
    "manual_visual_qa_pending",
    "godot-editor-lifecycle",
)

REQUIRED_CONSUMER_REFERENCES = (
    AGENTS_PATH,
    UNITY_CORE_SKILL_PATH,
    MCP_TROUBLESHOOTING_SKILL_PATH,
    PURE_RUN_ARTWORK_SKILL_PATH,
    GODOT_LIFECYCLE_SKILL_PATH,
)

REQUIRED_SCOPE_PATHS = {
    "unity-agent-workflow": (
        AGENTS_PATH,
        POLICY_PATH,
        UNITY_CORE_SKILL_PATH,
        MCP_TROUBLESHOOTING_SKILL_PATH,
        "Tools/agent-policy/validate_foreground_interaction_policy.py",
        "Tools/agent-policy/test_validate_foreground_interaction_policy.py",
    ),
    "pure-run-artwork": (PURE_RUN_ARTWORK_SKILL_PATH,),
    "godot-agent-workflow": (GODOT_LIFECYCLE_SKILL_PATH,),
}


def normalize_path(value: str) -> str:
    return value.replace("\\", "/").strip().strip("/")


def path_is_covered(path: str, patterns: list[str]) -> bool:
    normalized_path = normalize_path(path)
    for pattern in patterns:
        normalized_pattern = normalize_path(pattern)
        if normalized_path == normalized_pattern or normalized_path.startswith(f"{normalized_pattern}/"):
            return True
    return False


def read_required_file(repo_root: Path, relative_path: str, errors: list[str]) -> str:
    path = repo_root / relative_path
    if not path.is_file():
        errors.append(f"缺少必需文件：{relative_path}")
        return ""
    return path.read_text(encoding="utf-8")


def validate_policy(repo_root: Path) -> list[str]:
    errors: list[str] = []

    policy_text = read_required_file(repo_root, POLICY_PATH, errors)
    for token in REQUIRED_POLICY_TOKENS:
        if token not in policy_text:
            errors.append(f"{POLICY_PATH} 缺少策略锚点：{token}")

    for consumer_path in REQUIRED_CONSUMER_REFERENCES:
        consumer_text = read_required_file(repo_root, consumer_path, errors)
        if consumer_text and POLICY_REFERENCE not in consumer_text:
            errors.append(f"{consumer_path} 未引用 {POLICY_REFERENCE}")

    agents_text = read_required_file(repo_root, AGENTS_PATH, errors)
    if agents_text and "manual_visual_qa_pending" not in agents_text:
        errors.append(f"{AGENTS_PATH} 缺少 manual_visual_qa_pending 停止条件")

    pure_run_text = read_required_file(repo_root, PURE_RUN_ARTWORK_SKILL_PATH, errors)
    if pure_run_text and "manual_visual_qa_pending" not in pure_run_text:
        errors.append(f"{PURE_RUN_ARTWORK_SKILL_PATH} 缺少 manual_visual_qa_pending 停止条件")

    catalog_text = read_required_file(repo_root, CATALOG_PATH, errors)
    if catalog_text:
        try:
            catalog = yaml.safe_load(catalog_text) or {}
        except yaml.YAMLError as exception:
            errors.append(f"{CATALOG_PATH} 不是有效 YAML：{exception}")
        else:
            scopes = catalog.get("scopes") if isinstance(catalog, dict) else None
            if not isinstance(scopes, dict):
                errors.append(f"{CATALOG_PATH} 缺少 scopes mapping")
            else:
                for scope_name, required_paths in REQUIRED_SCOPE_PATHS.items():
                    scope = scopes.get(scope_name)
                    patterns = scope.get("paths") if isinstance(scope, dict) else None
                    if not isinstance(patterns, list) or any(not isinstance(item, str) for item in patterns):
                        errors.append(f"{CATALOG_PATH} 缺少有效 scope：{scope_name}")
                        continue
                    for required_path in required_paths:
                        if not path_is_covered(required_path, patterns):
                            errors.append(f"{scope_name} 未映射策略路径：{required_path}")

    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="校验前台交互与焦点保护规则的仓库接线。")
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="仓库根目录，默认从脚本位置推导。",
    )
    arguments = parser.parse_args(argv)
    repo_root = arguments.repo_root.resolve()
    errors = validate_policy(repo_root)

    if errors:
        print(f"FOREGROUND_INTERACTION_POLICY_FAILED errors={len(errors)}")
        for error in errors:
            print(f"- {error}")
        return 1

    print(
        "FOREGROUND_INTERACTION_POLICY_OK "
        f"consumers={len(REQUIRED_CONSUMER_REFERENCES)} scopes={len(REQUIRED_SCOPE_PATHS)}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
