import json
import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MANIFEST = ROOT / "Tools/migration/manifest/retirement/unity-governance-retirement-v1.json"


class UnityGovernanceRetirementTests(unittest.TestCase):
    def test_manifest_is_deterministic_and_covers_required_legacy_surfaces(self) -> None:
        subprocess.run(
            ["python", "Tools/migration/unity_governance_retirement.py"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        first = MANIFEST.read_bytes()
        subprocess.run(
            ["python", "Tools/migration/unity_governance_retirement.py"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        self.assertEqual(first, MANIFEST.read_bytes())
        document = json.loads(first)
        paths = {entry["path"] for entry in document["entries"]}
        self.assertIn(".agents/rules/unity-core.md", paths)
        self.assertIn(".agents/skills/unity-git-commit/SKILL.md", paths)
        self.assertIn("Tools/unity-mcp/Sync-ProjectMcpConfig.ps1", paths)
        self.assertIn("Tests/gameplay-specs/barbarian-uppercut.gameplay-test.md", paths)
        self.assertIn("Tests/gameplay-specs/hunter-mark.gameplay-test.md", paths)


if __name__ == "__main__":
    unittest.main()
