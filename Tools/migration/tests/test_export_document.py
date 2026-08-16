import json
import hashlib
import subprocess
import unittest
from pathlib import Path

from Tools.migration.export_document import (
    build_export_receipt,
    export_semantic_hash,
    validate_export_document,
)


class ExportDocumentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = Path(__file__).resolve().parents[3]
        cls.specification_path = (
            cls.root
            / "Tools"
            / "migration"
            / "manifest"
            / "export-batches"
            / "poison-spear-lv1.json"
        )
        cls.specification = json.loads(cls.specification_path.read_text(encoding="utf-8"))

    def test_poison_spear_export_spec_is_bound_to_frozen_git_blobs(self) -> None:
        self.assertEqual(self.specification["sourceTag"], "unity-final-2026-08-08")
        self.assertEqual(
            self.specification["sourceCommit"],
            "168d19345d7e0f7f22ce2516351eda9cef2e1cb1",
        )
        self.assertEqual(self.specification["exporterVersion"], "unity-assetdatabase-v1")
        self.assertEqual(len(self.specification["assets"]), 7)

        for asset in self.specification["assets"]:
            frozen_blob = subprocess.check_output(
                [
                    "git",
                    "rev-parse",
                    f"{self.specification['sourceTag']}:{asset['sourcePath']}",
                ],
                cwd=self.root,
                text=True,
            ).strip()
            current_blob = subprocess.check_output(
                ["git", "hash-object", asset["sourcePath"]],
                cwd=self.root,
                text=True,
            ).strip()
            self.assertEqual(asset["gitBlobSha1"], frozen_blob, asset["sourcePath"])
            self.assertEqual(asset["gitBlobSha1"], current_blob, asset["sourcePath"])

    def test_spec_includes_poison_buff_as_a_real_skill_dependency(self) -> None:
        poison = next(
            asset for asset in self.specification["assets"]
            if asset["sourceKey"] == "buff.poison/definition"
        )
        self.assertEqual(poison["targetContentIds"], ["buff.poison"])
        self.assertEqual(poison["sourcePath"], "Assets/Tactics/ScriptableObjects/Buffs/Poison.asset")

    def test_validator_rejects_identity_drift(self) -> None:
        document = self._minimal_document()
        document["assets"][0]["sourceGuid"] = "invalid"
        with self.assertRaisesRegex(ValueError, "invalid Unity GUID"):
            validate_export_document(document, self._minimal_specification())

    def test_receipt_is_deterministic_and_keeps_ownership_in_unity(self) -> None:
        document = self._minimal_document()
        specification = self._minimal_specification()
        first = build_export_receipt(document, specification)
        second = build_export_receipt(document, specification)
        self.assertEqual(first, second)
        self.assertEqual(first["exportHash"], export_semantic_hash(document))
        self.assertEqual(first["ownership"], "UnityOwned")
        self.assertEqual(first["nextState"], "Exported")

    def test_recorded_real_export_receipt_matches_specification(self) -> None:
        receipt_path = (
            self.root
            / "Tools"
            / "migration"
            / "manifest"
            / "receipts"
            / "poison-spear-lv1-export.json"
        )
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        expected_assets = {
            asset["sourceKey"]: asset for asset in self.specification["assets"]
        }

        self.assertEqual(receipt["classification"], "real_unity_assetdatabase_export")
        self.assertEqual(receipt["sourceTag"], self.specification["sourceTag"])
        self.assertEqual(receipt["sourceCommit"], self.specification["sourceCommit"])
        self.assertEqual(receipt["warnings"], [])
        self.assertEqual(receipt["idempotency"], {"runs": 2, "byteIdentical": True})
        self.assertEqual(receipt["ownership"], "UnityOwned")
        self.assertEqual(receipt["nextState"], "Exported")
        self.assertEqual({asset["sourceKey"] for asset in receipt["assets"]}, set(expected_assets))
        for asset in receipt["assets"]:
            expected = expected_assets[asset["sourceKey"]]
            self.assertEqual(asset["sourcePath"], expected["sourcePath"])
            self.assertEqual(asset["gitBlobSha1"], expected["gitBlobSha1"])
            self.assertEqual(asset["targetContentIds"], sorted(expected["targetContentIds"]))
            self.assertGreater(asset["serializedObjectCount"], 0)
            self.assertGreater(asset["serializedPropertyCount"], 0)

        local_output = self.root / self.specification["outputPath"]
        if local_output.is_file():
            document = json.loads(local_output.read_text(encoding="utf-8"))
            self.assertEqual(export_semantic_hash(document), receipt["exportHash"])
            self.assertEqual(
                hashlib.sha256(local_output.read_bytes()).hexdigest(),
                receipt["outputSha256"],
            )

    def test_real_batch_is_validated_but_remains_unity_owned(self) -> None:
        batch = json.loads(
            (
                self.root
                / "Tools"
                / "migration"
                / "manifest"
                / "batches"
                / "poison-spear-lv1-real.json"
            ).read_text(encoding="utf-8")
        )
        self.assertEqual(batch["classification"], "real_content_migration")
        self.assertEqual(batch["status"], "Validated")
        self.assertEqual(batch["owner"], "UnityOwned")
        self.assertEqual(batch["generation"], "godot-resource-saver-real-v1")
        self.assertEqual(
            batch["validation"]["visualAcceptance"],
            "passed_for_programmatic_placeholder_only",
        )
        self.assertIn("buff.poison", batch["contentIds"])

    @staticmethod
    def _minimal_specification() -> dict:
        return {
            "schemaVersion": 1,
            "batchId": "test",
            "exporterVersion": "unity-assetdatabase-v1",
            "sourceTag": "unity-final-2026-08-08",
            "sourceCommit": "168d19345d7e0f7f22ce2516351eda9cef2e1cb1",
            "assets": [
                {
                    "sourceKey": "skill.test/definition",
                    "sourcePath": "Assets/Test.asset",
                    "gitBlobSha1": "a" * 40,
                    "targetContentIds": ["skill.test"],
                }
            ],
        }

    @staticmethod
    def _minimal_document() -> dict:
        return {
            "schemaVersion": 1,
            "batchId": "test",
            "exporterVersion": "unity-assetdatabase-v1",
            "sourceTag": "unity-final-2026-08-08",
            "sourceCommit": "168d19345d7e0f7f22ce2516351eda9cef2e1cb1",
            "unityVersion": "6000.3.11f1",
            "assets": [
                {
                    "sourceKey": "skill.test/definition",
                    "sourcePath": "Assets/Test.asset",
                    "gitBlobSha1": "a" * 40,
                    "targetContentIds": ["skill.test"],
                    "sourceGuid": "b" * 32,
                    "sourceLocalFileId": 11400000,
                    "dependencyHash": "c" * 32,
                    "mainAssetType": "Test",
                    "objects": [
                        {
                            "objectPath": "main",
                            "objectType": "Test",
                            "properties": [
                                {
                                    "propertyPath": "value",
                                    "propertyType": "Integer",
                                    "supported": True,
                                    "value": "1",
                                }
                            ],
                        }
                    ],
                    "dependencies": [],
                    "unsupportedPropertyKinds": [],
                }
            ],
        }


if __name__ == "__main__":
    unittest.main()
