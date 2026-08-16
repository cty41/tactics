import json
import tempfile
import unittest
from pathlib import Path

from Tools.migration.unity_retirement_inventory import compile_inventory


ROOT = Path(__file__).resolve().parents[3]
RULES = ROOT / "Tools/migration/manifest/retirement/unity-retirement-rules-v1.json"


class UnityRetirementInventoryTests(unittest.TestCase):
    def test_inventory_is_complete_and_deterministic(self):
        first = compile_inventory(ROOT, RULES)
        second = compile_inventory(ROOT, RULES)
        self.assertEqual(first, second)
        self.assertEqual("b881177a7a34eff2d4ef8bc3ca6e47c12f5a468d", first["sourceTagObject"])
        self.assertEqual("168d19345d7e0f7f22ce2516351eda9cef2e1cb1", first["sourceCommit"])
        self.assertGreater(first["trackedFileCount"], 9000)
        self.assertEqual(0, first["counts"]["unresolved"])

    def test_legacy_and_third_party_boundaries_are_explicit(self):
        inventory = compile_inventory(ROOT, RULES)
        by_path = {entry["path"]: entry for entry in inventory["entries"]}
        self.assertEqual(
            "retired_legacy_prototype",
            by_path["Assets/Tactics/Battle/Classes/Barbarian.asset"]["classification"],
        )
        self.assertEqual(
            "excluded_third_party",
            by_path["Assets/ThirdParty/com.unity.uiextensions/LICENSE.md"]["classification"],
        )


if __name__ == "__main__":
    unittest.main()
