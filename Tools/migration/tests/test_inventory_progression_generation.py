import hashlib, json, unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
class InventoryProgressionGenerationTests(unittest.TestCase):
    def test_generation_evidence(self):
        receipt=json.loads((ROOT/'Tools/migration/manifest/receipts/pure-run-inventory-progression-v1-generation.json').read_text(encoding='utf-8'))
        ledger=json.loads((ROOT/'Tools/migration/manifest/state/pure-run-inventory-progression-v1.json').read_text(encoding='utf-8'))
        self.assertEqual(28,receipt['generatedSkillDefinitionCount']);self.assertEqual(125,receipt['canonicalCatalogEntryCount'])
        self.assertTrue(receipt['idempotency']['byteIdentical'])
        self.assertEqual(29,len(ledger['artifacts']));self.assertEqual('pending',receipt['manualInventoryProgressionAcceptance'])
        for artifact in ledger['artifacts']:
            actual='sha256:'+hashlib.sha256((ROOT/artifact['resourcePath']).read_bytes()).hexdigest()
            self.assertEqual(actual,artifact['targetHash'])
