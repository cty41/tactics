import tempfile,unittest
from pathlib import Path
from Tools.migration.pure_run_ui_input_generation import compile_evidence
class UiInputGenerationTests(unittest.TestCase):
 def test_scene_contract_and_manual_gate(self):
  with tempfile.TemporaryDirectory() as d:
   scene=Path(d)/'Main.tscn';scene.write_text('[ext_resource path="res://TacticsMigrationRoot.cs"]\n[node type="Node"]\n',encoding='utf-8');balance=Path(d)/'balance.tres';balance.write_text('PlayableLv1BalanceProfileResource skill.mage.fireball.lv1',encoding='utf-8');speed=Path(d)/'speed.tres';speed.write_text('[gd_resource script_class="PlayableEnemySpeedProfileResource"]\nUnitContentIds = PackedStringArray("unit.pure-run.goat-ranged")\nSpeeds = PackedFloat32Array(6)',encoding='utf-8');state,receipt=compile_evidence({'batchId':'pure-run-ui-input-v1','source':{}},scene,balance,speed);self.assertEqual('Validated',receipt['state']);self.assertEqual('passed',receipt['manualUiInputAcceptance']);self.assertEqual(3,len(state['artifacts']));self.assertEqual('godot-playable-enemy-speed-v1',receipt['enemySpeedContract'])
 def test_wrong_root_rejected(self):
  with tempfile.TemporaryDirectory() as d:
   scene=Path(d)/'Main.tscn';scene.write_text('[node type="Control"]',encoding='utf-8');self.assertRaises(ValueError,compile_evidence,{'batchId':'pure-run-ui-input-v1','source':{}},scene)
