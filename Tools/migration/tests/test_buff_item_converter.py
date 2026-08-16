import copy
import hashlib
import json
import unittest
from pathlib import Path

from Tools.migration.buff_item_converter import canonical_json, compile_buff_item_draft
from Tools.migration.export_document import load_json


ROOT = Path(__file__).resolve().parents[3]
EXPORT_PATH = ROOT / "Tools/migration/out/pure-run-buffs-items-v1.unity.json"
SPEC_PATH = ROOT / "Tools/migration/manifest/export-batches/pure-run-buffs-items-v1.json"
GOLDEN_PATH = ROOT / "Tests/golden/buff-item-batch-v1.json"
CONSUMABLES_PATH = ROOT / "Assets/Tactics/GameData/Consumables.json"
EQUIPMENT_PATH = ROOT / "Assets/Tactics/GameData/Equipment.json"


@unittest.skipUnless(EXPORT_PATH.is_file(), "real Unity DTO is a disposable local artifact")
class BuffItemConverterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.export = load_json(EXPORT_PATH)
        cls.specification = load_json(SPEC_PATH)
        cls.golden = load_json(GOLDEN_PATH)
        cls.consumables = load_json(CONSUMABLES_PATH)
        cls.equipment = json.loads(EQUIPMENT_PATH.read_text(encoding="utf-8"))
        cls.consumables_hash = hashlib.sha256(CONSUMABLES_PATH.read_bytes()).hexdigest()
        cls.equipment_hash = hashlib.sha256(EQUIPMENT_PATH.read_bytes()).hexdigest()

    def compile(self, export=None, golden=None, consumables=None, equipment=None):
        return compile_buff_item_draft(
            export or self.export,
            self.specification,
            golden or self.golden,
            consumables or self.consumables,
            equipment or self.equipment,
            self.consumables_hash,
            self.equipment_hash,
        )

    def test_complete_batch_is_deterministic_and_has_no_visual_payload(self):
        first = self.compile()
        second = self.compile()
        self.assertEqual(canonical_json(first), canonical_json(second))
        self.assertEqual((14, 3, 12), (
            len(first["buffs"]), len(first["consumables"]), len(first["equipment"])
        ))
        self.assertEqual(["buff.poison"], first["externalContentDependencies"])
        self.assertFalse(first["payloadBoundary"]["iconPayloadCopied"])

    def test_rejects_enum_and_guid_drift(self):
        export = copy.deepcopy(self.export)
        export["assets"][0]["sourceGuid"] = "0" * 32
        with self.assertRaisesRegex(ValueError, "GUID differs"):
            self.compile(export=export)
        export = copy.deepcopy(self.export)
        effect = next(
            prop for prop in export["assets"][0]["objects"][0]["properties"]
            if prop["propertyPath"] == "_effectType"
        )
        effect["value"] = "Unsupported"
        with self.assertRaisesRegex(ValueError, "unsupported"):
            self.compile(export=export)

    def test_rejects_icon_dependency_or_payload_boundary_drift(self):
        export = copy.deepcopy(self.export)
        frozen = next(item for item in export["assets"] if item["sourceKey"] == "buff.frozen")
        icon = next(
            prop for prop in frozen["objects"][0]["properties"]
            if prop["propertyPath"] == "_icon"
        )
        icon["reference"]["sourcePath"] = "Assets/ThirdParty/freezed.png"
        with self.assertRaisesRegex(ValueError, "audit-only boundary"):
            self.compile(export=export)

    def test_rejects_json_field_content_id_and_external_dependency_drift(self):
        consumables = copy.deepcopy(self.consumables)
        consumables["Definitions"][0]["Unexpected"] = 1
        with self.assertRaisesRegex(ValueError, "unknown or missing fields"):
            self.compile(consumables=consumables)
        equipment = copy.deepcopy(self.equipment)
        equipment[0]["Id"] = "BOW_01"
        with self.assertRaisesRegex(ValueError, "Golden"):
            self.compile(equipment=equipment)
        golden = copy.deepcopy(self.golden)
        golden["externalContentDependencies"] = []
        with self.assertRaisesRegex(ValueError, "external content dependency"):
            self.compile(golden=golden)


if __name__ == "__main__":
    unittest.main()
