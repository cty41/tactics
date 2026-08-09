import copy
import json
import unittest
from pathlib import Path

from Tools.migration.export_document import load_json
from Tools.migration.poison_spear_converter import canonical_json, compile_poison_spear_draft


ROOT = Path(__file__).resolve().parents[3]
EXPORT_PATH = ROOT / "Tools/migration/out/poison-spear-lv1.unity.json"
SPECIFICATION_PATH = ROOT / "Tools/migration/manifest/export-batches/poison-spear-lv1.json"


@unittest.skipUnless(EXPORT_PATH.is_file(), "real Unity DTO is a disposable local artifact")
class RealPoisonSpearConverterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.specification = load_json(SPECIFICATION_PATH)
        cls.export = load_json(EXPORT_PATH)

    def test_real_export_compiles_exact_lv1_semantics(self):
        draft = compile_poison_spear_draft(self.export, self.specification)
        contents = {item["contentId"]: item for item in draft["contents"]}

        skill = contents["skill.poison-spear.lv1"]["properties"]
        self.assertEqual(5, skill["range"])
        self.assertEqual(6, skill["manaCost"])
        self.assertEqual(8, skill["damage"])
        self.assertEqual(3, skill["poisonDuration"])
        self.assertEqual(2.0, skill["poisonTickDamage"])
        self.assertTrue(skill["requiresLineOfSight"])
        self.assertEqual(7.0, skill["projectileSpeed"])
        self.assertEqual(1, skill["authoredDropSearchRadius"])
        self.assertEqual(3, skill["runtimeDropSearchRadius"])
        self.assertTrue(skill["dropsSpearOnCompletion"])

        poison = contents["buff.poison"]["properties"]
        self.assertEqual("AddDuration", poison["refreshStrategy"])
        self.assertEqual("TurnStart", poison["triggerTiming"])
        self.assertEqual("Poison", poison["effectType"])

        graph = contents["presentation.poison-spear.lv1"]["properties"]["graph"]
        self.assertEqual(6, len(graph["nodes"]))
        self.assertEqual(4, len(graph["edges"]))
        self.assertEqual(2, len(graph["previewPhases"]))
        self.assertEqual(
            [
                {"x": 0.0, "y": 20.0},
                {"x": 270.0, "y": 20.0},
                {"x": 560.0, "y": 20.0},
                {"x": 0.0, "y": 220.0},
                {"x": 280.0, "y": 220.0},
                {"x": 570.0, "y": 220.0},
            ],
            [node["position"] for node in graph["nodes"]],
        )

    def test_output_is_byte_deterministic(self):
        first = canonical_json(compile_poison_spear_draft(self.export, self.specification))
        second = canonical_json(compile_poison_spear_draft(self.export, self.specification))
        self.assertEqual(first.encode("utf-8"), second.encode("utf-8"))

    def test_range_drift_is_rejected(self):
        export = copy.deepcopy(self.export)
        ability = next(
            asset for asset in export["assets"]
            if asset["sourceKey"] == "skill.poison-spear.lv1/ability"
        )
        target_range = next(
            prop for prop in ability["objects"][0]["properties"]
            if prop["propertyPath"] == "_targetRange"
        )
        target_range["value"] = "6"
        with self.assertRaisesRegex(ValueError, "range disagree"):
            compile_poison_spear_draft(export, self.specification)

    def test_typed_draft_is_json_round_trip_safe(self):
        draft = compile_poison_spear_draft(self.export, self.specification)
        self.assertEqual(draft, json.loads(canonical_json(draft)))


if __name__ == "__main__":
    unittest.main()
