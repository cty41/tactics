import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MANIFEST = ROOT / "Tools/migration/manifest/retirement/unity-governance-retirement-v1.json"


class UnityGovernanceRetirementTests(unittest.TestCase):
    def test_frozen_manifest_covers_required_legacy_surfaces(self) -> None:
        document = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(1, document["schemaVersion"])
        self.assertEqual("unity-governance-retirement-v1", document["manifestId"])
        self.assertEqual(document["entryCount"], len(document["entries"]))
        paths = {entry["path"] for entry in document["entries"]}
        self.assertEqual(len(paths), len(document["entries"]))
        self.assertIn(".agents/rules/unity-core.md", paths)
        self.assertIn(".agents/skills/unity-git-commit/SKILL.md", paths)
        self.assertIn("Tools/unity-mcp/Sync-ProjectMcpConfig.ps1", paths)
        self.assertIn("Tests/gameplay-specs/barbarian-uppercut.gameplay-test.md", paths)
        self.assertIn("Tests/gameplay-specs/hunter-mark.gameplay-test.md", paths)

        for entry in document["entries"]:
            self.assertRegex(entry["gitBlobSha1"], r"^[0-9a-f]{40}$")
            self.assertRegex(entry["sha256"], r"^[0-9a-f]{64}$")
            self.assertGreaterEqual(entry["byteCount"], 0)


if __name__ == "__main__":
    unittest.main()
