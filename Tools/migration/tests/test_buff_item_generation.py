import copy
import hashlib
import json
import unittest
from pathlib import Path

from Tools.migration.buff_item_generation_receipt import (
    compile_buff_item_generation_receipt,
)


ROOT = Path(__file__).resolve().parents[3]
BATCH_PATH = ROOT / "Tools/migration/manifest/batches/pure-run-buffs-items-v1.json"
EXPORT_RECEIPT_PATH = (
    ROOT / "Tools/migration/manifest/receipts/pure-run-buffs-items-v1-export.json"
)
GENERATION_RECEIPT_PATH = (
    ROOT / "Tools/migration/manifest/receipts/pure-run-buffs-items-v1-generation.json"
)
LEDGER_PATH = ROOT / "Tools/migration/manifest/state/pure-run-buffs-items-v1.json"
DRAFT_PATH = ROOT / "Tools/migration/out/pure-run-buffs-items-v1.draft.json"


def sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


class BuffItemGenerationTests(unittest.TestCase):
    def setUp(self):
        self.batch = json.loads(BATCH_PATH.read_text(encoding="utf-8"))
        self.export_receipt = json.loads(EXPORT_RECEIPT_PATH.read_text(encoding="utf-8"))
        self.receipt = json.loads(GENERATION_RECEIPT_PATH.read_text(encoding="utf-8"))
        self.ledger = json.loads(LEDGER_PATH.read_text(encoding="utf-8"))

    def test_batch_is_validated_without_a_visual_payload_gate(self):
        self.assertEqual("Validated", self.batch["status"])
        self.assertEqual("UnityOwned", self.batch["owner"])
        self.assertEqual(
            "not_applicable_no_visual_payload",
            self.batch["validation"]["visualAcceptance"],
        )
        self.assertEqual("Validated", self.receipt["state"])
        self.assertEqual("UnityOwned", self.receipt["ownership"])
        self.assertEqual("not_applicable_no_visual_payload", self.receipt["visualAcceptance"])

    def test_ledger_targets_match_generated_bytes_and_unique_uids(self):
        artifacts = self.ledger["artifacts"]
        self.assertEqual(30, len(artifacts))
        uids = [artifact["resourceUid"] for artifact in artifacts]
        paths = [artifact["resourcePath"] for artifact in artifacts]
        self.assertEqual(len(uids), len(set(uids)))
        self.assertEqual(len(paths), len(set(paths)))
        for artifact in artifacts:
            self.assertRegex(artifact["resourceUid"], r"^uid://[a-z0-9]+$")
            target = ROOT / "godot" / artifact["resourcePath"].removeprefix("res://")
            self.assertTrue(target.is_file(), artifact["resourcePath"])
            self.assertEqual(artifact["targetHash"], sha256(target))

    def test_generation_is_exact_and_keeps_poison_external(self):
        paths = {artifact["resourcePath"] for artifact in self.ledger["artifacts"]}
        self.assertIn("res://content/buffs_items/ContentCatalog.tres", paths)
        self.assertIn("res://content/ContentCatalog.tres", paths)
        self.assertNotIn("res://content/buffs_items/BuffPoison.tres", paths)
        self.assertEqual(13, sum("/Buff" in path for path in paths))
        self.assertEqual(3, sum("/Consumable" in path for path in paths))
        self.assertEqual(12, sum("/Equipment" in path for path in paths))
        self.assertEqual(29, self.receipt["batchCatalogEntryCount"])
        self.assertEqual(47, self.receipt["canonicalCatalogEntryCount"])
        self.assertEqual("buff.poison", self.receipt["externalContentDependencies"][0]["contentId"])

    def test_receipt_is_bound_to_current_ledger_and_no_visual_payload(self):
        self.assertEqual(sha256(LEDGER_PATH), self.receipt["generationLedgerHash"])
        self.assertEqual(30, self.receipt["artifactCount"])
        self.assertEqual(3, self.receipt["dependencyBoundary"]["buffIconReferenceCount"])
        self.assertFalse(self.receipt["dependencyBoundary"]["buffIconPayloadCopied"])
        self.assertFalse(self.receipt["dependencyBoundary"]["unityMaterialOrShaderPayloadCopied"])
        self.assertFalse(self.receipt["dependencyBoundary"]["thirdPartyPayloadCopied"])
        for artifact in self.ledger["artifacts"]:
            self.assertFalse(artifact["resourcePath"].endswith((".png", ".mat", ".shader")))
        if DRAFT_PATH.is_file():
            self.assertEqual(sha256(DRAFT_PATH), self.receipt["typedDraftHash"])

    def test_receipt_compiler_rejects_source_drift(self):
        if not DRAFT_PATH.is_file():
            self.skipTest("real typed draft is a disposable local artifact")
        draft = json.loads(DRAFT_PATH.read_text(encoding="utf-8"))
        ledger = copy.deepcopy(self.ledger)
        ledger["source"]["exportHash"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(ValueError, "differs"):
            compile_buff_item_generation_receipt(
                self.export_receipt,
                draft,
                sha256(DRAFT_PATH),
                ledger,
                sha256(LEDGER_PATH),
            )


if __name__ == "__main__":
    unittest.main()
