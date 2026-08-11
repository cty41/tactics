import hashlib
import json
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
LEDGER=ROOT/"Tools/migration/manifest/state/pure-run-ai-encounter-v1.json"
RECEIPT=ROOT/"Tools/migration/manifest/receipts/pure-run-ai-encounter-v1-generation.json"
BATCH=ROOT/"Tools/migration/manifest/batches/pure-run-ai-encounter-v1.json"

class AiEncounterGenerationTests(unittest.TestCase):
    def test_generated_batch_preserves_manual_gate(self):
        batch=json.loads(BATCH.read_text(encoding="utf-8")); receipt=json.loads(RECEIPT.read_text(encoding="utf-8"))
        self.assertEqual("Generated",batch["status"]); self.assertEqual("UnityOwned",batch["ownership"]); self.assertEqual("pending",receipt["manualGameplayAcceptance"]); self.assertEqual(73,receipt["canonicalCatalogEntryCount"])
    def test_ledger_owns_exactly_seventeen_artifacts(self):
        ledger=json.loads(LEDGER.read_text(encoding="utf-8")); self.assertEqual(17,len(ledger["artifacts"])); paths={item["resourcePath"] for item in ledger["artifacts"]}; self.assertNotIn("res://content/ContentCatalog.tres",paths); self.assertIn("res://content/ai_encounters/AiEncounterFixture.tscn",paths)
        for item in ledger["artifacts"]:
            target=ROOT/"godot"/item["resourcePath"].removeprefix("res://"); self.assertEqual(item["targetHash"],"sha256:"+hashlib.sha256(target.read_bytes()).hexdigest())
    def test_fixture_and_project_use_native_canvas(self):
        project=(ROOT/"godot/project.godot").read_text(encoding="utf-8"); self.assertIn("size/viewport_width=1600",project); self.assertIn("size/viewport_height=900",project)
        fixture=(ROOT/"godot/src/Tactics.Godot.Adapter/Runtime/GodotAiEncounterFixture.cs").read_text(encoding="utf-8"); self.assertRegex(fixture,r"CanvasWidth\s*=\s*1600"); self.assertRegex(fixture,r"CanvasHeight\s*=\s*900")

if __name__=="__main__":unittest.main()
