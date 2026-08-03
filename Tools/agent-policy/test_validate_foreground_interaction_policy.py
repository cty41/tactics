from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from validate_foreground_interaction_policy import (  # noqa: E402
    AGENTS_PATH,
    CATALOG_PATH,
    MCP_TROUBLESHOOTING_SKILL_PATH,
    POLICY_PATH,
    PURE_RUN_ARTWORK_SKILL_PATH,
    UNITY_CORE_SKILL_PATH,
    validate_policy,
)


VALID_POLICY = """<!-- policy-id: foreground-interaction -->
default-deny
current-turn-explicit-request
action-time-confirmation
manual_visual_qa_pending
"""

VALID_CONSUMER = "See ../../rules/foreground-interaction.md.\n"

VALID_CATALOG = """version: 1
scopes:
  unity-agent-workflow:
    concept: operations/unity-agent-workflow.md
    paths:
      - AGENTS.md
      - .agents/rules
      - .agents/skills/unity-mcp-core/SKILL.md
      - .agents/skills/mcp-connection-troubleshooting/SKILL.md
      - Tools/agent-policy
  pure-run-artwork:
    concept: operations/pure-run-artwork.md
    paths:
      - .agents/skills/pure-run-artwork-pipeline
"""


class ForegroundInteractionPolicyTests(unittest.TestCase):
    def create_repository(self) -> tuple[tempfile.TemporaryDirectory[str], Path]:
        temporary = tempfile.TemporaryDirectory()
        root = Path(temporary.name)

        self.write(root, POLICY_PATH, VALID_POLICY)
        self.write(
            root,
            AGENTS_PATH,
            ".agents/rules/foreground-interaction.md\nmanual_visual_qa_pending\n",
        )
        self.write(root, UNITY_CORE_SKILL_PATH, VALID_CONSUMER)
        self.write(root, MCP_TROUBLESHOOTING_SKILL_PATH, VALID_CONSUMER)
        self.write(
            root,
            PURE_RUN_ARTWORK_SKILL_PATH,
            VALID_CONSUMER + "manual_visual_qa_pending\n",
        )
        self.write(root, CATALOG_PATH, VALID_CATALOG)
        return temporary, root

    @staticmethod
    def write(root: Path, relative_path: str, content: str) -> None:
        path = root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def test_complete_policy_wiring_passes(self) -> None:
        temporary, root = self.create_repository()
        with temporary:
            self.assertEqual([], validate_policy(root))

    def test_missing_policy_anchor_fails(self) -> None:
        temporary, root = self.create_repository()
        with temporary:
            policy = root / POLICY_PATH
            policy.write_text(VALID_POLICY.replace("action-time-confirmation\n", ""), encoding="utf-8")
            errors = validate_policy(root)
            self.assertTrue(any("action-time-confirmation" in error for error in errors))

    def test_agents_without_authority_reference_fails(self) -> None:
        temporary, root = self.create_repository()
        with temporary:
            (root / AGENTS_PATH).write_text("manual_visual_qa_pending\n", encoding="utf-8")
            errors = validate_policy(root)
            self.assertTrue(any(AGENTS_PATH in error and "未引用" in error for error in errors))

    def test_unity_skill_without_authority_reference_fails(self) -> None:
        temporary, root = self.create_repository()
        with temporary:
            (root / UNITY_CORE_SKILL_PATH).write_text("missing link\n", encoding="utf-8")
            errors = validate_policy(root)
            self.assertTrue(any(UNITY_CORE_SKILL_PATH in error and "未引用" in error for error in errors))

    def test_pure_run_skill_without_manual_gate_fails(self) -> None:
        temporary, root = self.create_repository()
        with temporary:
            (root / PURE_RUN_ARTWORK_SKILL_PATH).write_text(VALID_CONSUMER, encoding="utf-8")
            errors = validate_policy(root)
            self.assertTrue(any(PURE_RUN_ARTWORK_SKILL_PATH in error and "manual_visual_qa_pending" in error for error in errors))

    def test_catalog_without_validator_mapping_fails(self) -> None:
        temporary, root = self.create_repository()
        with temporary:
            catalog = root / CATALOG_PATH
            catalog.write_text(VALID_CATALOG.replace("      - Tools/agent-policy\n", ""), encoding="utf-8")
            errors = validate_policy(root)
            self.assertTrue(any("Tools/agent-policy" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
