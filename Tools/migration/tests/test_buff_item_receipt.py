import copy
import unittest
from pathlib import Path

from Tools.migration.buff_item_converter import compile_buff_item_draft
from Tools.migration.buff_item_receipt import compile_buff_item_export_receipt
from Tools.migration.export_document import load_json
import hashlib
import json


ROOT = Path(__file__).resolve().parents[3]
EXPORT_PATH = ROOT / "Tools/migration/out/pure-run-buffs-items-v1.unity.json"


@unittest.skipUnless(EXPORT_PATH.is_file(), "real Unity DTO is a disposable local artifact")
class BuffItemReceiptTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.export = load_json(EXPORT_PATH)
        cls.specification = load_json(
            ROOT / "Tools/migration/manifest/export-batches/pure-run-buffs-items-v1.json"
        )
        consumables_path = ROOT / "Assets/Tactics/GameData/Consumables.json"
        equipment_path = ROOT / "Assets/Tactics/GameData/Equipment.json"
        cls.draft = compile_buff_item_draft(
            cls.export,
            cls.specification,
            load_json(ROOT / "Tests/golden/buff-item-batch-v1.json"),
            load_json(consumables_path),
            json.loads(equipment_path.read_text(encoding="utf-8")),
            hashlib.sha256(consumables_path.read_bytes()).hexdigest(),
            hashlib.sha256(equipment_path.read_bytes()).hexdigest(),
        )

    def test_receipt_preserves_unity_ownership_and_exported_state(self):
        receipt = compile_buff_item_export_receipt(
            self.export, self.specification, self.draft, "9" * 64
        )
        self.assertEqual("UnityOwned", receipt["ownership"])
        self.assertEqual("Exported", receipt["nextState"])
        self.assertEqual(29, receipt["batchShape"]["uniqueContentIds"])
        self.assertEqual(3, receipt["dependencyAudit"]["iconReferences"])
        self.assertFalse(receipt["dependencyAudit"]["iconPayloadCopied"])
        self.assertEqual("not_applicable_no_visual_payload", receipt["visualAcceptance"])

    def test_receipt_rejects_unbound_or_visual_draft(self):
        draft = copy.deepcopy(self.draft)
        draft["source"]["exportHash"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "not bound"):
            compile_buff_item_export_receipt(
                self.export, self.specification, draft, "9" * 64
            )
        draft = copy.deepcopy(self.draft)
        draft["payloadBoundary"]["iconPayloadCopied"] = True
        with self.assertRaisesRegex(ValueError, "no-visual"):
            compile_buff_item_export_receipt(
                self.export, self.specification, draft, "9" * 64
            )


if __name__ == "__main__":
    unittest.main()
