import hashlib
import json
import math
import subprocess
import unittest
from pathlib import Path


class UnitBatchGoldenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = Path(__file__).resolve().parents[3]
        cls.golden = json.loads(
            (cls.root / "Tests" / "golden" / "unit-batch-v1.json").read_text(
                encoding="utf-8"
            )
        )
        cls.matrix = json.loads(
            (cls.root / "Tests" / "golden" / "oracle-matrix.json").read_text(
                encoding="utf-8"
            )
        )

    def test_identity_and_category_set_is_exact(self) -> None:
        expected_ids = {
            "unit.pure-run.mage",
            "unit.pure-run.necromancer",
            "unit.pure-run.amazon",
            "unit.pure-run.skeleton-warrior",
            "unit.pure-run.skeleton-mage",
            "unit.pure-run.fire-demon",
            "unit.pure-run.goat-charger",
            "unit.pure-run.goat-ranged",
            "unit.pure-run.goat-aoe",
            "unit.pure-run.goat-support",
            "unit.pure-run.goat-elite-charger",
            "unit.pure-run.goat-elite-poison-caster",
        }
        units = self.golden["units"]
        self.assertEqual(self.golden["schemaVersion"], 1)
        self.assertEqual(self.golden["batchId"], "pure-run-units-v1")
        self.assertEqual({unit["contentId"] for unit in units}, expected_ids)
        self.assertEqual(
            {category: sum(unit["category"] == category for unit in units) for category in ("player", "summon", "goat")},
            {"player": 3, "summon": 3, "goat": 6},
        )

    def test_all_explicit_derived_values_match_frozen_formula(self) -> None:
        for unit in self.golden["units"]:
            attributes = unit["attributes"]
            derived = unit["derived"]
            speed = unit["speed"]
            with self.subTest(content_id=unit["contentId"]):
                self.assertEqual(derived["maxHealth"], max(1, attributes["constitution"] * 4))
                self.assertEqual(derived["maxMana"], max(0, attributes["charisma"] * 3))
                self.assertEqual(derived["startingMana"], attributes["charisma"])
                self.assertEqual(derived["moveRange"], min(4, max(1, math.ceil(speed * 0.5))))
                self.assertEqual(derived["initiative"], speed * 2)

        for case in self.golden["formulaCases"]:
            self.assertEqual(case["moveRange"], min(4, max(1, math.ceil(case["speed"] * 0.5))))
            self.assertEqual(case["initiative"], case["speed"] * 2)

    def test_visual_scope_is_project_owned_and_exactly_nineteen_pngs(self) -> None:
        textures = self.golden["textureAssets"]
        self.assertEqual(len(textures), 19)
        self.assertEqual(len({asset["sourcePath"] for asset in textures}), 19)
        self.assertEqual(len({asset["targetPath"] for asset in textures}), 19)
        self.assertTrue(all("/PureRun/Textures/" in asset["sourcePath"] for asset in textures))
        self.assertTrue(all(asset["targetPath"].startswith("res://assets/units/") for asset in textures))
        self.assertTrue(all("ThirdParty" not in asset["sourcePath"] for asset in textures))

        units = self.golden["units"]
        self.assertTrue(all(unit["visual"]["shadowTexture"].endswith("pure_run_unit_shadow_1x1_v01.png") for unit in units))
        self.assertTrue(all(unit["visual"]["shadowOffset"] == [0, -0.03] for unit in units))
        self.assertTrue(all(unit["visual"]["deathTexture"] is None for unit in units if unit["category"] == "summon"))
        self.assertTrue(all(unit["visual"]["deathTexture"] is not None for unit in units if unit["category"] != "summon"))
        self.assertTrue(
            all(
                unit["visual"]["tintMode"] == "goat-body-mask-v1"
                for unit in units
                if unit["category"] == "goat"
            )
        )
        self.assertTrue(
            all(
                unit["visual"]["tintMode"] == "multiply"
                for unit in units
                if unit["category"] != "goat"
            )
        )
        self.assertTrue(all(len(unit["visual"]["baseBodyColor"]) == 4 for unit in units))

    def test_goat_tint_contract_is_bound_to_the_frozen_project_owned_shader(self) -> None:
        contract = self.golden["tintContract"]
        shader_path = contract["unityShaderPath"]
        self.assertEqual(contract["id"], "unity-goat-body-tint-v1")
        self.assertEqual(contract["maskSmoothstep"], [0.1, 0.28])
        self.assertEqual(contract["luminanceWeights"], [0.299, 0.587, 0.114])
        self.assertEqual(contract["minimumBaseLuminance"], 0.01)
        self.assertEqual(contract["materialThresholdAudit"], 0.08)
        self.assertEqual(
            self.matrix["frozenAssetBlobs"][shader_path],
            contract["unityShaderGitBlobSha1"],
        )
        self.assertEqual(
            self._git_blob(self.golden["source"]["unityCommit"], shader_path),
            contract["unityShaderGitBlobSha1"],
        )

    def test_sprite_geometry_contract_preserves_unity_pivots_and_shadow_transform(self) -> None:
        contract = self.golden["spriteContract"]
        self.assertEqual("unity-unit-sprite-geometry-v1", contract["id"])
        self.assertEqual([0.5, 0.078125], contract["living"]["pivot"])
        self.assertEqual(128, contract["living"]["pixelsPerUnit"])
        self.assertEqual([0.5, 0.5], contract["death"]["pivot"])
        self.assertEqual([0, -0.03, 0], contract["shadow"]["localPosition"])
        self.assertEqual([0.8, 0.8, 0.8], contract["shadow"]["localScale"])
        self.assertEqual([1, 1, 1, 0.9], contract["shadow"]["color"])

    def test_texture_hashes_dimensions_and_frozen_blobs_match(self) -> None:
        commit = self.golden["source"]["unityCommit"]
        frozen_assets = self.matrix["frozenAssetBlobs"]
        for asset in self.golden["textureAssets"]:
            source_path = self.root / asset["sourcePath"]
            with self.subTest(path=asset["sourcePath"]):
                self.assertTrue(source_path.is_file())
                self.assertEqual(hashlib.sha256(source_path.read_bytes()).hexdigest(), asset["sha256"])
                self.assertEqual(frozen_assets[asset["sourcePath"]], asset["gitBlobSha1"])
                self.assertEqual(self._git_blob(commit, asset["sourcePath"]), asset["gitBlobSha1"])
                expected_size = (64, 32) if asset["kind"] == "shadow" else (256, 256)
                self.assertEqual((asset["width"], asset["height"]), expected_size)

    def test_prefab_bindings_match_frozen_commit_without_parsing_yaml(self) -> None:
        commit = self.golden["source"]["unityCommit"]
        frozen_assets = self.matrix["frozenAssetBlobs"]
        for unit in self.golden["units"]:
            path = unit["sourcePrefabPath"]
            expected_blob = unit["sourcePrefabGitBlobSha1"]
            with self.subTest(content_id=unit["contentId"]):
                self.assertEqual(frozen_assets[path], expected_blob)
                self.assertEqual(self._git_blob(commit, path), expected_blob)

    def test_oracle_contract_records_the_real_assetdatabase_export(self) -> None:
        contract = next(
            contract for contract in self.matrix["contracts"] if contract["id"] == "unit.pure-run-v1"
        )
        self.assertEqual(contract["goldenVector"], "unit-batch-v1.json#/units")
        self.assertEqual(contract["evidence"]["derivedMovement"], "unity_final_linked_source_oracle")
        self.assertEqual(
            contract["evidence"]["prefabAndVisuals"],
            "unity_assetdatabase_export_and_frozen_asset_snapshot",
        )
        self.assertEqual(
            contract["exportReceipt"],
            "Tools/migration/manifest/receipts/pure-run-units-v1-export.json",
        )
        self.assertEqual(contract["status"], "unity_final_asset_export_and_linked_source_oracle")

    def _git_blob(self, commit: str, path: str) -> str:
        result = subprocess.run(
            ["git", "rev-parse", f"{commit}:{path}"],
            cwd=self.root,
            check=True,
            capture_output=True,
            text=True,
        )
        return result.stdout.strip()


if __name__ == "__main__":
    unittest.main()
