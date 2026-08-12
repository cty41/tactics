import json, unittest
import subprocess, sys, tempfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
class FullRunContractTests(unittest.TestCase):
 def test_spec_freezes_expected_source_set(self):
  spec=json.loads((ROOT/'Tools/migration/manifest/export-batches/pure-run-full-seven-layer-v1.json').read_text(encoding='utf-8'))
  self.assertEqual(spec['batchId'],'pure-run-full-seven-layer-v1')
  self.assertEqual(len(spec['assets']),7)
  self.assertEqual({v['sourceKey'] for v in spec['assets']},{'map.generator','encounter.catalog','battle.rewards','battle.settlement','run.session','run.summary','node.transaction'})
 def test_real_export_compiles_expected_contract(self):
  export=ROOT/'Tools/migration/out/pure-run-full-seven-layer-v1.unity.json'
  if not export.exists():self.skipTest('disposable Unity export unavailable')
  with tempfile.TemporaryDirectory() as folder:
   output=Path(folder)/'draft.json'
   subprocess.run([sys.executable,'-m','Tools.migration.full_run_converter','--export',str(export),'--specification',str(ROOT/'Tools/migration/manifest/export-batches/pure-run-full-seven-layer-v1.json'),'--output',str(output)],cwd=ROOT,check=True)
   draft=json.loads(output.read_text(encoding='utf-8'))
   self.assertEqual(len(draft['encounters']),5)
   self.assertEqual(draft['multipliers']['special'],{'health':1.8,'output':1.25})
   self.assertIn('Lv3',draft['excluded'])
