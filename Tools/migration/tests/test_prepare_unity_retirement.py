import json
import pathlib
import subprocess
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[3]
MANIFEST = ROOT / "Tools/migration/manifest/retirement/unity-deletion-manifest-v1.json"


class UnityRetirementManifestTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        subprocess.run(
            ["python", "Tools/migration/prepare_unity_retirement.py"],
            cwd=ROOT,
            check=True,
        )
        cls.document = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cls.paths = {entry["path"] for entry in cls.document["entries"]}

    def test_covers_all_tracked_unity_roots(self):
        tracked = subprocess.check_output(["git", "ls-files"], cwd=ROOT, text=True).splitlines()
        expected = {
            path
            for path in tracked
            if path.startswith(("Assets/", "Packages/", "ProjectSettings/", "UIElementsSchema/"))
        }
        self.assertEqual(expected, self.paths & expected)

    def test_preserves_godot_core_frozen_oracle_and_evidence(self):
        forbidden = (
            "godot/",
            "src/Tactics.Core/",
            "src/Tactics.Application/",
            "src/Tactics.FrozenOracle.Tests/",
            "Tools/godot/",
            "Tools/migration/manifest/retirement/",
            "Tools/migration/manifest/receipts/",
        )
        self.assertFalse(any(path.startswith(forbidden) for path in self.paths))
        self.assertNotIn("Tactics.Godot.slnx", self.paths)
        self.assertNotIn("Tactics.Godot.runsettings", self.paths)
        self.assertNotIn("Tests/gameplay-specs/barbarian-counter.gameplay-test.md", self.paths)

    def test_removes_superseded_transition_entrypoints(self):
        self.assertIn("Tactics.Migration.slnx", self.paths)
        self.assertIn("Tools/migration/Verify-GodotMigration.ps1", self.paths)
        self.assertIn("Tools/migration/Test-GodotOwnedWithoutUnity.ps1", self.paths)
        self.assertIn("Tools/migration/tests/test_godot_mainline_verifier.py", self.paths)

    def test_entries_are_unique_and_byte_totals_match(self):
        entries = self.document["entries"]
        self.assertEqual(len(entries), len(self.paths))
        self.assertEqual(sum(entry["byteCount"] for entry in entries), self.document["totalBytes"])


if __name__ == "__main__":
    unittest.main()
