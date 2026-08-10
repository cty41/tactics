import copy
import hashlib
import json
import unittest
from pathlib import Path

from Tools.migration.unit_generation_receipt import compile_unit_generation_receipt


ROOT = Path(__file__).resolve().parents[3]
BATCH_PATH = ROOT / "Tools/migration/manifest/batches/pure-run-units-v1.json"
EXPORT_RECEIPT_PATH = ROOT / "Tools/migration/manifest/receipts/pure-run-units-v1-export.json"
GENERATION_RECEIPT_PATH = ROOT / "Tools/migration/manifest/receipts/pure-run-units-v1-generation.json"
GENERATION_LEDGER_PATH = ROOT / "Tools/migration/manifest/state/pure-run-units-v1.json"
TEXTURE_LEDGER_PATH = ROOT / "Tools/migration/manifest/state/pure-run-unit-textures-v1.json"
DRAFT_PATH = ROOT / "Tools/migration/out/pure-run-units-v1.draft.json"
SHADER_PATH = (
    ROOT
    / "godot"
    / "src"
    / "Tactics.Godot.Adapter"
    / "Runtime"
    / "Shaders"
    / "GoatBodyTint.gdshader"
)


def sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


class UnitGenerationTests(unittest.TestCase):
    def setUp(self):
        self.batch = json.loads(BATCH_PATH.read_text(encoding="utf-8"))
        self.export_receipt = json.loads(EXPORT_RECEIPT_PATH.read_text(encoding="utf-8"))
        self.receipt = json.loads(GENERATION_RECEIPT_PATH.read_text(encoding="utf-8"))
        self.generation_ledger = json.loads(GENERATION_LEDGER_PATH.read_text(encoding="utf-8"))
        self.texture_ledger = json.loads(TEXTURE_LEDGER_PATH.read_text(encoding="utf-8"))

    def test_batch_is_validated_but_visual_acceptance_remains_manual(self):
        self.assertEqual("UnityOwned", self.batch["owner"])
        self.assertEqual("Generated", self.batch["status"])
        self.assertEqual("manual_visual_qa_pending", self.batch["validation"]["visualAcceptance"])
        self.assertEqual("UnityOwned", self.receipt["ownership"])
        self.assertEqual("Generated", self.receipt["state"])
        self.assertEqual("manual_visual_qa_pending", self.receipt["visualAcceptance"])

    def test_resource_ledger_targets_match_generated_bytes_and_unique_uids(self):
        artifacts = self.generation_ledger["artifacts"]
        self.assertEqual(16, len(artifacts))
        uids = [artifact["resourceUid"] for artifact in artifacts]
        self.assertEqual(len(uids), len(set(uids)))
        for artifact in artifacts:
            self.assertRegex(artifact["resourceUid"], r"^uid://[a-z0-9]+$")
            target = ROOT / "godot" / artifact["resourcePath"].removeprefix("res://")
            self.assertTrue(target.is_file(), artifact["resourcePath"])
            self.assertEqual(artifact["targetHash"], sha256(target))

    def test_texture_ledger_is_exactly_the_approved_project_owned_payload(self):
        artifacts = self.texture_ledger["artifacts"]
        self.assertEqual(19, len(artifacts))
        for artifact in artifacts:
            target = ROOT / artifact["relativePath"]
            self.assertTrue(target.is_file(), artifact["relativePath"])
            self.assertEqual(artifact["targetHash"], sha256(target))
            self.assertNotIn("ThirdParty", artifact["relativePath"])
            self.assertFalse(artifact["relativePath"].endswith((".mat", ".shader")))

    def test_receipt_is_bound_to_current_ledgers_and_optional_disposable_draft(self):
        self.assertEqual(sha256(GENERATION_LEDGER_PATH), self.receipt["generationLedgerHash"])
        self.assertEqual(sha256(TEXTURE_LEDGER_PATH), self.receipt["textureLedgerHash"])
        self.assertEqual(13, self.receipt["contentEntryCount"])
        self.assertEqual(12, self.receipt["unitDefinitionCount"])
        self.assertEqual(19, self.receipt["texturePayloadCount"])
        self.assertFalse(self.receipt["dependencyBoundary"]["materialAndShaderPayloadCopied"])
        self.assertTrue(self.receipt["dependencyBoundary"]["projectOwnedShaderAlgorithmPorted"])
        self.assertFalse(self.receipt["dependencyBoundary"]["thirdPartyPayloadCopied"])
        self.assertEqual(
            "godot-image-software-reference-with-goat-body-mask-"
            "sprite-pivot-and-ground-baseline-v1",
            self.receipt["captureMode"],
        )
        self.assertEqual("unity-goat-body-tint-v1", self.receipt["tintContract"]["id"])
        self.assertEqual(sha256(SHADER_PATH), self.receipt["tintContract"]["godotShaderSha256"])
        self.assertFalse(self.receipt["tintContract"]["unityPayloadCopied"])
        self.assertEqual(
            "unity-unit-sprite-geometry-v1",
            self.receipt["spriteContract"]["id"],
        )
        self.assertEqual([0.5, 0.078125], self.receipt["spriteContract"]["livingPivot"])
        self.assertEqual([0.5, 0.5], self.receipt["spriteContract"]["deathPivot"])
        self.assertEqual([0, -0.03, 0], self.receipt["spriteContract"]["shadowLocalPosition"])
        self.assertRegex(self.receipt["galleryCaptureHash"], r"^sha256:[0-9a-f]{64}$")
        self.assertRegex(self.receipt["spawnCaptureHash"], r"^sha256:[0-9a-f]{64}$")
        if DRAFT_PATH.is_file():
            self.assertEqual(sha256(DRAFT_PATH), self.receipt["typedDraftHash"])

    def test_receipt_compiler_rejects_source_drift(self):
        if not DRAFT_PATH.is_file():
            self.skipTest("real typed draft is a disposable local artifact")
        draft = json.loads(DRAFT_PATH.read_text(encoding="utf-8"))
        generation_ledger = copy.deepcopy(self.generation_ledger)
        generation_ledger["source"]["exportHash"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "differs"):
            compile_unit_generation_receipt(
                self.export_receipt,
                draft,
                sha256(DRAFT_PATH),
                generation_ledger,
                sha256(GENERATION_LEDGER_PATH),
                self.texture_ledger,
                sha256(TEXTURE_LEDGER_PATH),
                "sha256:" + "1" * 64,
                "sha256:" + "2" * 64,
                "sha256:" + "3" * 64,
            )


if __name__ == "__main__":
    unittest.main()
