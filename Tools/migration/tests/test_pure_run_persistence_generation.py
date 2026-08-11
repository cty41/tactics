import hashlib
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
LEDGER = ROOT / "Tools/migration/manifest/state/pure-run-persistence-v1.json"
RECEIPT = ROOT / "Tools/migration/manifest/receipts/pure-run-persistence-v1-generation.json"
BATCH = ROOT / "Tools/migration/manifest/batches/pure-run-persistence-v1.json"


class PureRunPersistenceGenerationTests(unittest.TestCase):
    def test_batch_and_receipt_are_validated_without_visual_gate(self):
        batch = json.loads(BATCH.read_text(encoding="utf-8"))
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
        self.assertEqual("Validated", batch["status"])
        self.assertEqual("UnityOwned", batch["ownership"])
        self.assertEqual("not_required_automated_observability", receipt["manualGameplayAcceptance"])
        self.assertEqual("not_applicable_no_visual_payload", receipt["visualAcceptance"])
        self.assertEqual(74, receipt["canonicalCatalogEntryCount"])

    def test_ledger_owns_exactly_three_generated_artifacts(self):
        ledger = json.loads(LEDGER.read_text(encoding="utf-8"))
        self.assertEqual(3, len(ledger["artifacts"]))
        paths = {item["resourcePath"] for item in ledger["artifacts"]}
        self.assertEqual({
            "res://content/runs/PureRunThreeEncounterV1.tres",
            "res://content/runs/ContentCatalog.tres",
            "res://content/runs/RunPersistenceFixture.tscn",
        }, paths)
        for item in ledger["artifacts"]:
            target = ROOT / "godot" / item["resourcePath"].removeprefix("res://")
            self.assertEqual(item["targetHash"], "sha256:" + hashlib.sha256(target.read_bytes()).hexdigest())


if __name__ == "__main__":
    unittest.main()
