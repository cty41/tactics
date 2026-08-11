import hashlib
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
LEDGER = ROOT / "Tools/migration/manifest/state/pure-run-starting-skills-v1.json"
RECEIPT = ROOT / "Tools/migration/manifest/receipts/pure-run-starting-skills-v1-generation.json"
BATCH = ROOT / "Tools/migration/manifest/batches/pure-run-starting-skills-v1.json"


class StartingSkillGenerationTests(unittest.TestCase):
    def test_generated_batch_remains_manual_gameplay_pending(self):
        batch = json.loads(BATCH.read_text(encoding="utf-8"))
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
        self.assertEqual("Generated", batch["status"])
        self.assertEqual("Generated", receipt["state"])
        self.assertEqual("pending", receipt["manualGameplayAcceptance"])
        self.assertEqual(58, receipt["canonicalCatalogEntryCount"])

    def test_ledger_bytes_and_external_poison_ownership(self):
        ledger = json.loads(LEDGER.read_text(encoding="utf-8"))
        self.assertEqual(13, len(ledger["artifacts"]))
        paths = {item["resourcePath"] for item in ledger["artifacts"]}
        self.assertNotIn("res://content/poison_spear/PoisonSpearSkillLv1.tres", paths)
        for item in ledger["artifacts"]:
            target = ROOT / "godot" / item["resourcePath"].removeprefix("res://")
            actual = "sha256:" + hashlib.sha256(target.read_bytes()).hexdigest()
            self.assertEqual(item["targetHash"], actual)


if __name__ == "__main__":
    unittest.main()
