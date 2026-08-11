import tempfile,unittest
from pathlib import Path
from Tools.migration.pure_run_ui_input_generation import compile_evidence
class UiInputGenerationTests(unittest.TestCase):
 def test_scene_contract_and_manual_gate(self):
  with tempfile.TemporaryDirectory() as d:
   scene=Path(d)/'Main.tscn';scene.write_text('[ext_resource path="res://TacticsMigrationRoot.cs"]\n[node type="Node"]\n',encoding='utf-8');state,receipt=compile_evidence({'batchId':'pure-run-ui-input-v1','source':{}},scene);self.assertEqual('Generated',receipt['state']);self.assertEqual('pending',receipt['manualUiInputAcceptance']);self.assertEqual(1,len(state['artifacts']))
 def test_wrong_root_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   scene=Path(d)/'Main.tscn';scene.write_text('[node type="Control"]',encoding='utf-8');self.assertRaises(ValueError,compile_evidence,{'batchId':'pure-run-ui-input-v1','source':{}},scene)
