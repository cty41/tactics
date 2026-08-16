import json
import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
RECEIPT = ROOT / "Tools/migration/manifest/ownership/godot-content-ownership-v1.json"


class GodotContentOwnershipTests(unittest.TestCase):
    def test_receipt_is_current_and_separates_manual_acceptance(self) -> None:
        subprocess.run(
            ["python", "Tools/migration/godot_content_ownership.py", "--check"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
        self.assertEqual("GodotOwned", receipt["ownership"])
        self.assertEqual(142, receipt["catalogCount"])
        self.assertEqual("pending_separate_quality_gate", receipt["manualAcceptance"])
        self.assertTrue(receipt["historicalExportReceiptsPreserved"])
        self.assertTrue(all(item["owner"] == "GodotOwned" for item in receipt["categories"]))

    def test_historical_batches_remain_immutable_unity_owned_evidence(self) -> None:
        batches = ROOT / "Tools/migration/manifest/batches"
        documents = [json.loads(path.read_text(encoding="utf-8")) for path in batches.glob("*.json")]
        owners = {document.get("ownership", document.get("owner")) for document in documents}
        self.assertIn("UnityOwned", owners)
        self.assertNotIn("GodotOwned", owners)


if __name__ == "__main__":
    unittest.main()
