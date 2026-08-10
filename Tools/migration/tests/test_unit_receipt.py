import copy
import unittest
from pathlib import Path

from Tools.migration.export_document import load_json
from Tools.migration.unit_converter import compile_unit_draft
from Tools.migration.unit_receipt import compile_unit_export_receipt


ROOT = Path(__file__).resolve().parents[3]
EXPORT_PATH = ROOT / "Tools/migration/out/pure-run-units-v1.unity.json"
SPECIFICATION_PATH = ROOT / "Tools/migration/manifest/export-batches/pure-run-units-v1.json"
GOLDEN_PATH = ROOT / "Tests/golden/unit-batch-v1.json"


@unittest.skipUnless(EXPORT_PATH.is_file(), "real Unity DTO is a disposable local artifact")
class RealUnitReceiptTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.export = load_json(EXPORT_PATH)
        cls.specification = load_json(SPECIFICATION_PATH)
        cls.golden = load_json(GOLDEN_PATH)
        cls.draft = compile_unit_draft(cls.export, cls.specification, cls.golden)

    def test_receipt_preserves_unity_ownership_and_exported_state(self):
        receipt = compile_unit_export_receipt(
            self.export,
            self.specification,
            self.draft,
            "9" * 64,
        )
        self.assertEqual("UnityOwned", receipt["ownership"])
        self.assertEqual("Exported", receipt["nextState"])
        self.assertEqual(37, receipt["batchShape"]["selectedRoots"])
        self.assertTrue(receipt["idempotency"]["byteIdentical"])
        self.assertFalse(receipt["dependencyAudit"]["materialAndShaderPayloadCopied"])

    def test_receipt_rejects_draft_from_another_export(self):
        draft = copy.deepcopy(self.draft)
        draft["source"]["exportHash"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "not bound"):
            compile_unit_export_receipt(
                self.export,
                self.specification,
                draft,
                "9" * 64,
            )


if __name__ == "__main__":
    unittest.main()
