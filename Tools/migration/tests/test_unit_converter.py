import copy
import json
import unittest
from pathlib import Path

from Tools.migration.export_document import load_json
from Tools.migration.unit_converter import canonical_json, compile_unit_draft


ROOT = Path(__file__).resolve().parents[3]
EXPORT_PATH = ROOT / "Tools/migration/out/pure-run-units-v1.unity.json"
SPECIFICATION_PATH = ROOT / "Tools/migration/manifest/export-batches/pure-run-units-v1.json"
GOLDEN_PATH = ROOT / "Tests/golden/unit-batch-v1.json"


@unittest.skipUnless(EXPORT_PATH.is_file(), "real Unity DTO is a disposable local artifact")
class RealUnitConverterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.export = load_json(EXPORT_PATH)
        cls.specification = load_json(SPECIFICATION_PATH)
        cls.golden = load_json(GOLDEN_PATH)

    def test_real_export_compiles_complete_unit_contract(self):
        draft = compile_unit_draft(self.export, self.specification, self.golden)
        self.assertEqual(12, len(draft["units"]))
        self.assertEqual(21, len(draft["textureAssets"]))
        amazon = next(item for item in draft["units"] if item["contentId"] == "unit.pure-run.amazon")
        self.assertTrue(amazon["visual"]["unarmedDownRightTexture"].endswith("unarmed_dr.png"))
        self.assertEqual("packed-scene.unit-actor", draft["actorContentId"])
        self.assertEqual(39, draft["dependencyAudit"]["selectedPayloadCount"])
        self.assertIn(
            "Assets/ThirdParty/TBSFramework/Examples/TilemapExample/Materials/HeliSprite.mat",
            draft["dependencyAudit"]["forbiddenPayloadDependencies"],
        )

    def test_output_is_byte_deterministic_and_json_round_trip_safe(self):
        first = canonical_json(compile_unit_draft(self.export, self.specification, self.golden))
        second = canonical_json(compile_unit_draft(self.export, self.specification, self.golden))
        self.assertEqual(first.encode("utf-8"), second.encode("utf-8"))
        self.assertEqual(json.loads(first), json.loads(second))

    def test_non_player_prefab_stat_drift_is_rejected(self):
        export = copy.deepcopy(self.export)
        prefab = next(
            asset for asset in export["assets"]
            if asset["sourceKey"] == "unit.pure-run.fire-demon/prefab"
        )
        unit = next(obj for obj in prefab["objects"] if obj["objectType"] == "Tactics.Units.TilemapUnit")
        strength = next(prop for prop in unit["properties"] if prop["propertyPath"] == "_strength")
        strength["value"] = "6"
        with self.assertRaisesRegex(ValueError, "_strength"):
            compile_unit_draft(export, self.specification, self.golden)

    def test_visual_reference_drift_is_rejected(self):
        export = copy.deepcopy(self.export)
        prefab = next(
            asset for asset in export["assets"]
            if asset["sourceKey"] == "unit.pure-run.mage/prefab"
        )
        visual = next(
            obj for obj in prefab["objects"]
            if obj["objectType"] == "Tactics.Common.Units.FourDirectionSpriteVisual"
        )
        down_right = next(
            prop for prop in visual["properties"] if prop["propertyPath"] == "_downRightSprite"
        )
        down_right["reference"]["sourcePath"] = "Assets/ThirdParty/forbidden.png"
        with self.assertRaisesRegex(ValueError, "visual reference drifted"):
            compile_unit_draft(export, self.specification, self.golden)

    def test_material_tint_drift_is_rejected(self):
        export = copy.deepcopy(self.export)
        material = next(
            asset for asset in export["assets"]
            if asset["sourceKey"] == "unit.pure-run.goat-charger/material-audit"
        )
        value = next(
            prop for prop in material["objects"][0]["properties"]
            if prop["propertyPath"] == "m_SavedProperties.m_Colors.Array.data[1].second"
        )
        value["value"] = "1,0,1,1"
        with self.assertRaisesRegex(ValueError, "body tint"):
            compile_unit_draft(export, self.specification, self.golden)

    def test_material_shader_base_color_and_threshold_drift_are_rejected(self):
        cases = (
            ("m_Shader", "shader path"),
            ("_BaseBodyColor", "base body color"),
            ("_Color", "material color"),
            ("_BodyThreshold", "body threshold"),
        )
        for field, diagnostic in cases:
            with self.subTest(field=field):
                export = copy.deepcopy(self.export)
                material = next(
                    asset for asset in export["assets"]
                    if asset["sourceKey"] == "unit.pure-run.goat-charger/material-audit"
                )
                properties = material["objects"][0]["properties"]
                if field == "m_Shader":
                    prop = next(item for item in properties if item["propertyPath"] == field)
                    prop["reference"]["sourcePath"] = "Assets/ThirdParty/forbidden.shader"
                elif field == "_BodyThreshold":
                    prop = self._saved_property(properties, "m_Floats", field)
                    prop["value"] = "0.5"
                else:
                    prop = self._saved_property(properties, "m_Colors", field)
                    prop["value"] = "1,0,1,1"
                with self.assertRaisesRegex(ValueError, diagnostic):
                    compile_unit_draft(export, self.specification, self.golden)

    def test_texture_import_contract_drift_is_rejected(self):
        export = copy.deepcopy(self.export)
        texture = next(
            asset for asset in export["assets"]
            if asset["sourceKey"] == "unit.pure-run.mage/texture-dr"
        )
        importer = next(obj for obj in texture["objects"] if obj["objectPath"] == "importer")
        srgb = next(prop for prop in importer["properties"] if prop["propertyPath"] == "m_sRGBTexture")
        srgb["value"] = "0"
        with self.assertRaisesRegex(ValueError, "m_sRGBTexture"):
            compile_unit_draft(export, self.specification, self.golden)

    def test_sprite_pivot_and_shadow_geometry_drift_are_rejected(self):
        pivot_export = copy.deepcopy(self.export)
        texture = next(
            asset for asset in pivot_export["assets"]
            if asset["sourceKey"] == "unit.pure-run.mage/texture-dr"
        )
        importer = next(obj for obj in texture["objects"] if obj["objectPath"] == "importer")
        pivot = next(
            prop for prop in importer["properties"] if prop["propertyPath"] == "m_SpritePivot"
        )
        pivot["value"] = "0.5,0.5"
        with self.assertRaisesRegex(ValueError, "m_SpritePivot"):
            compile_unit_draft(pivot_export, self.specification, self.golden)

        shadow_export = copy.deepcopy(self.export)
        prefab = next(
            asset for asset in shadow_export["assets"]
            if asset["sourceKey"] == "unit.pure-run.mage/prefab"
        )
        transform = next(
            obj for obj in prefab["objects"]
            if "/Shadow[" in obj["objectPath"] and obj["objectType"] == "UnityEngine.Transform"
        )
        position = next(
            prop for prop in transform["properties"] if prop["propertyPath"] == "m_LocalPosition"
        )
        position["value"] = "0,-0.5,0"
        with self.assertRaisesRegex(ValueError, "Shadow local position"):
            compile_unit_draft(shadow_export, self.specification, self.golden)

    def test_forbidden_selected_payload_is_rejected(self):
        specification = copy.deepcopy(self.specification)
        specification["assets"][0]["sourcePath"] = (
            "Assets/ThirdParty/TBSFramework/Examples/TilemapExample/Materials/HeliSprite.mat"
        )
        export = copy.deepcopy(self.export)
        export_asset = next(
            item for item in export["assets"]
            if item["sourceKey"] == specification["assets"][0]["sourceKey"]
        )
        export_asset["sourcePath"] = specification["assets"][0]["sourcePath"]
        with self.assertRaisesRegex(ValueError, "forbidden Unit payload"):
            compile_unit_draft(export, specification, self.golden)

    @staticmethod
    def _saved_property(properties, collection, property_name):
        prefix = f"m_SavedProperties.{collection}.Array.data["
        name_property = next(
            item for item in properties
            if item["propertyPath"].startswith(prefix)
            and item["propertyPath"].endswith(".first")
            and item.get("value") == property_name
        )
        value_path = name_property["propertyPath"].removesuffix(".first") + ".second"
        return next(item for item in properties if item["propertyPath"] == value_path)


if __name__ == "__main__":
    unittest.main()
