import copy
import hashlib
import json
import unittest
from pathlib import Path

from Tools.migration.ownership_closure_converter import compile_ownership_closure_draft

ROOT = Path(__file__).parents[3]
EXPORT = ROOT / "Tools/migration/out/pure-run-ownership-closure-v1.unity.json"
SPEC = ROOT / "Tools/migration/manifest/export-batches/pure-run-ownership-closure-v1.json"
RECEIPT = ROOT / "Tools/migration/manifest/receipts/pure-run-ownership-closure-v1-export.json"


class OwnershipClosureConverterTests(unittest.TestCase):
    def setUp(self):
        self.export = json.loads(EXPORT.read_text(encoding="utf-8"))
        self.spec = json.loads(SPEC.read_text(encoding="utf-8"))

    def test_compiles_nine_player_levels_and_internal_attack(self):
        draft = compile_ownership_closure_draft(self.export, self.spec)
        self.assertEqual(9, len(draft["playerSkillDefinitions"]))
        self.assertEqual(1, len(draft["internalSkillDefinitions"]))
        definitions = {item["contentId"]: item for item in draft["playerSkillDefinitions"]}
        self.assertEqual((11, 50, True), (definitions["skill.mage.lightning.lv3"]["damage"],
            definitions["skill.mage.lightning.lv3"]["statusChancePercent"],
            definitions["skill.mage.lightning.lv3"]["ignoreLineOfSight"]))
        self.assertEqual((3, 1), (definitions["skill.mage.ice-bolt.lv3"]["bounceRange"],
            definitions["skill.mage.ice-bolt.lv3"]["bounceCount"]))
        self.assertTrue(definitions["skill.necromancer.bone-spear.lv3"]["pierceAll"])
        self.assertEqual("square", definitions["skill.amazon.poison-spear.lv3"]["areaShape"])
        self.assertEqual("Passive", definitions["skill.amazon.combat-techniques.lv3"]["kind"])
        self.assertEqual(4, draft["internalSkillDefinitions"][0]["damage"])
        self.assertFalse(draft["internalSkillDefinitions"][0]["canCrit"])

    def test_freezes_treasure_and_tooling_without_copying_payload(self):
        draft = compile_ownership_closure_draft(self.export, self.spec)
        self.assertEqual((2, 5, 1), (draft["treasureContract"]["goldMinimum"],
            draft["treasureContract"]["goldMaximum"], draft["treasureContract"]["eventsCompleted"]))
        self.assertEqual(7, len(draft["toolingContracts"]))
        self.assertFalse(any(draft["payloadBoundary"].values()))

    def test_rejects_source_and_graph_drift(self):
        changed = copy.deepcopy(self.export)
        changed["assets"][0]["gitBlobSha1"] = "0" * 40
        with self.assertRaises(ValueError):
            compile_ownership_closure_draft(changed, self.spec)
        changed = copy.deepcopy(self.export)
        skill = next(item for item in changed["assets"] if item["sourceKey"] == "skill.mage.fireball.lv3")
        prop = next(prop for prop in skill["objects"][0]["properties"] if prop["propertyPath"] == "_skillGraph")
        prop["reference"] = None
        with self.assertRaises(ValueError):
            compile_ownership_closure_draft(changed, self.spec)

    def test_recorded_export_receipt_is_bound_to_two_identical_runs(self):
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
        self.assertEqual(BATCH_ID := "pure-run-ownership-closure-v1", receipt["batchId"])
        self.assertEqual(BATCH_ID, self.export["batchId"])
        self.assertEqual(17, receipt["assetRoots"])
        self.assertEqual({"measuredIndependentRuns": 2, "byteIdentical": True}, receipt["idempotency"])
        self.assertEqual(hashlib.sha256(EXPORT.read_bytes()).hexdigest(), receipt["outputSha256"])
        self.assertFalse(any(receipt["payloadBoundary"].values()))


if __name__ == "__main__":
    unittest.main()
