import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
BATCH_PATH = ROOT / "Tools/migration/manifest/batches/poison-spear-lv1-real.json"
LEDGER_PATH = ROOT / "Tools/migration/manifest/state/poison-spear-lv1-real.json"
RECEIPT_PATH = ROOT / "Tools/migration/manifest/receipts/poison-spear-lv1-generation.json"
LICENSE_RECEIPT_PATH = ROOT / "Tools/migration/manifest/receipts/poison-spear-lv1-license.json"
DRAFT_PATH = ROOT / "Tools/migration/out/poison-spear-lv1.draft.json"


def sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


class PoisonSpearGenerationTests(unittest.TestCase):
    def setUp(self):
        self.batch = json.loads(BATCH_PATH.read_text(encoding="utf-8"))
        self.ledger = json.loads(LEDGER_PATH.read_text(encoding="utf-8"))
        self.receipt = json.loads(RECEIPT_PATH.read_text(encoding="utf-8"))
        self.license_receipt = json.loads(LICENSE_RECEIPT_PATH.read_text(encoding="utf-8"))

    def test_real_batch_is_validated_for_the_placeholder_but_remains_unity_owned(self):
        self.assertEqual("Validated", self.batch["status"])
        self.assertEqual("UnityOwned", self.batch["owner"])
        self.assertEqual(
            "passed_for_programmatic_placeholder_only",
            self.batch["validation"]["visualAcceptance"],
        )
        self.assertEqual("Validated", self.receipt["state"])
        self.assertEqual("UnityOwned", self.receipt["ownership"])
        self.assertEqual(
            "passed_for_programmatic_placeholder_only",
            self.receipt["visualAcceptance"],
        )
        self.assertEqual(
            "passed_for_programmatic_placeholder_only",
            self.license_receipt["visualAcceptance"],
        )

    def test_generation_source_binding_matches_export_receipt(self):
        export_receipt = json.loads(
            (ROOT / self.batch["exportReceipt"]).read_text(encoding="utf-8")
        )
        source = self.ledger["source"]
        self.assertEqual(export_receipt["sourceTag"], source["sourceTag"])
        self.assertEqual(export_receipt["sourceCommit"], source["sourceCommit"])
        self.assertEqual(export_receipt["exporterVersion"], source["exporterVersion"])
        self.assertEqual(export_receipt["exportHash"], source["exportHash"])

    def test_ledger_targets_match_tracked_resource_bytes_and_uids_are_unique(self):
        artifacts = self.ledger["artifacts"]
        self.assertEqual(7, len(artifacts))
        uids = [artifact["resourceUid"] for artifact in artifacts]
        self.assertEqual(len(uids), len(set(uids)))
        for artifact in artifacts:
            self.assertRegex(artifact["resourceUid"], r"^uid://[a-z0-9]+$")
            relative = artifact["resourcePath"].removeprefix("res://")
            target = ROOT / "godot" / relative
            self.assertTrue(target.is_file(), artifact["resourcePath"])
            self.assertEqual(artifact["targetHash"], sha256(target))

    def test_generation_receipt_matches_ledger_and_real_source_values(self):
        self.assertEqual(self.ledger["batchId"], self.receipt["batchId"])
        self.assertEqual(sha256(LEDGER_PATH), self.receipt["generationLedgerHash"])
        if DRAFT_PATH.is_file():
            self.assertEqual(sha256(DRAFT_PATH), self.receipt["typedDraftHash"])
        values = self.receipt["sourceValues"]
        self.assertEqual(5, values["range"])
        self.assertEqual(6, values["manaCost"])
        self.assertEqual(8, values["damage"])
        self.assertEqual("AddDuration", values["poisonRefreshStrategy"])
        self.assertEqual("TurnStart", values["poisonTriggerTiming"])
        self.assertEqual(6, values["presentationAuthoringNodeCount"])
        self.assertEqual(4, values["presentationAuthoringEdgeCount"])
        self.assertEqual(1, values["authoredDropSearchRadius"])
        self.assertEqual(3, values["runtimeDropSearchRadius"])
        self.assertTrue(values["dropsSpearOnCompletion"])

    def test_migrated_semantics_are_serialized_in_assets_not_csharp_defaults(self):
        expected_fields = {
            "godot/content/poison_spear/PoisonBuff.tres": (
                "ContentIdValue",
                "DefaultDuration",
                "DamagePerTurn",
                "RefreshStrategy",
                "TriggerTiming",
            ),
            "godot/content/poison_spear/PoisonSpearSkillLv1.tres": (
                "ContentIdValue",
                "Range",
                "ManaCost",
                "Damage",
                "PoisonTurns",
                "DropOnHit = false",
                "DropSearchRadius",
                "DropsSpearOnCompletion",
            ),
            "godot/content/poison_spear/PoisonSpear10x10Fixture.tres": (
                "ContentIdValue",
                "BoardWidth",
                "BoardHeight",
                "CasterCell",
                "TargetCell",
            ),
            "godot/content/poison_spear/PoisonSpearPresentationLv1.tres": (
                "Revision = \"sha256:",
                "AuthoringNodeIds",
                "AuthoringNodePositions",
                "AuthoringNodeEnabled",
                "EdgeIds",
                "PlanRootNodeId",
            ),
            "godot/content/poison_spear/PoisonSpearProjectile.tscn": (
                "FlightSeconds",
                "SourceScale",
                "ArcHeight",
                "Tint",
                "RotateAlongTangent",
            ),
            "godot/content/poison_spear/PoisonSpearImpact.tscn": (
                "TailSeconds",
                "SourceScale",
                "Tint",
            ),
        }
        for relative_path, fields in expected_fields.items():
            payload = (ROOT / relative_path).read_text(encoding="utf-8")
            for field in fields:
                with self.subTest(asset=relative_path, field=field):
                    self.assertIn(field, payload)

    def test_current_visual_payload_is_procedural_and_does_not_copy_unverified_piloto_assets(self):
        self.assertEqual(
            "pass_for_procedural_placeholder_only",
            self.license_receipt["decision"],
        )
        payload = self.license_receipt["generatedGodotPayload"]
        self.assertFalse(payload["thirdPartyBinaryOrTextureCopied"])
        self.assertFalse(payload["unityPrefabMaterialOrTextureReferenced"])
        self.assertEqual(
            "passed_for_procedural_placeholder_only",
            self.batch["validation"]["licenseProvenance"],
        )
        for artifact in self.ledger["artifacts"]:
            relative = artifact["resourcePath"].removeprefix("res://")
            text = (ROOT / "godot" / relative).read_text(encoding="utf-8")
            self.assertNotIn("PilotoAdapted", text)
            self.assertNotIn("Assets/Tactics", text)
            self.assertNotIn(".prefab", text)
            self.assertNotIn(".mat", text)


if __name__ == "__main__":
    unittest.main()
