import json, unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
class InventoryProgressionGenerationTests(unittest.TestCase):
    def test_generation_evidence(self):
        receipt=json.loads((ROOT/'Tools/migration/manifest/receipts/pure-run-inventory-progression-v1-generation.json').read_text(encoding='utf-8'))
        ledger=json.loads((ROOT/'Tools/migration/manifest/state/pure-run-inventory-progression-v1.json').read_text(encoding='utf-8'))
        self.assertEqual(27,receipt['generatedSkillDefinitionCount']);self.assertEqual(101,receipt['canonicalCatalogEntryCount'])
        self.assertEqual(28,len(ledger['artifacts']));self.assertEqual('pending',receipt['manualInventoryProgressionAcceptance'])
