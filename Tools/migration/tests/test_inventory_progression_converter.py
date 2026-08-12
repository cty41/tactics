import copy
import json
import unittest
from pathlib import Path

from Tools.migration.inventory_progression_converter import compile_inventory_progression_draft

ROOT = Path(__file__).parents[3]
EXPORT = ROOT / "Tools/migration/out/pure-run-inventory-progression-v1.unity.json"
SPEC = ROOT / "Tools/migration/manifest/export-batches/pure-run-inventory-progression-v1.json"

class InventoryProgressionConverterTests(unittest.TestCase):
    def setUp(self):
        self.export = json.loads(EXPORT.read_text(encoding="utf-8"))
        self.spec = json.loads(SPEC.read_text(encoding="utf-8"))

    def test_compiles_eighteen_branches_and_thirty_six_levels(self):
        draft = compile_inventory_progression_draft(self.export, self.spec)
        self.assertEqual(18, len(draft["branches"]))
        self.assertEqual(36, len(draft["definitions"]))
        self.assertEqual(2, max(value["level"] for value in draft["definitions"]))
        self.assertFalse(draft["payloadBoundary"]["unityUiPayloadCopied"])

    def test_rejects_missing_level(self):
        changed = copy.deepcopy(self.export)
        changed["assets"] = [value for value in changed["assets"] if value["sourceKey"] != "skill.mage.teleport.lv2"]
        with self.assertRaises(ValueError):
            compile_inventory_progression_draft(changed, self.spec)

    def test_rejects_source_drift(self):
        changed = copy.deepcopy(self.export)
        changed["assets"][0]["gitBlobSha1"] = "0" * 40
        with self.assertRaises(ValueError):
            compile_inventory_progression_draft(changed, self.spec)

if __name__ == "__main__":
    unittest.main()
