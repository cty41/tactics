import copy,json,unittest
from pathlib import Path
from Tools.migration.pure_run_ui_input_converter import compile_draft
from Tools.migration.pure_run_ui_input_receipt import compile_receipt
ROOT=Path(__file__).resolve().parents[3]
class UiInputConverterTests(unittest.TestCase):
 def setUp(self):
  self.export=json.loads((ROOT/'Tools/migration/out/pure-run-ui-input-v1.unity.json').read_text(encoding='utf-8'));self.spec=json.loads((ROOT/'Tools/migration/manifest/export-batches/pure-run-ui-input-v1.json').read_text(encoding='utf-8'))
 def test_contract(self):
  d=compile_draft(self.export,self.spec);self.assertEqual(['home','battle','settlement','summary'],d['pages']);self.assertFalse(d['payloadBoundary']['unityUiToolkitCopied']);a=next(x for x in self.export['assets'] if x['sourceKey']=='input.actions');self.assertEqual('audit-only-file',a['exportMode']);self.assertEqual([],a['objects']);self.assertTrue(compile_receipt(self.export,self.spec,d)['idempotency']['byteIdentical'])
 def test_root_drift_rejected(self):
  e=copy.deepcopy(self.export);e['assets'].pop();self.assertRaises(ValueError,compile_draft,e,self.spec)
 def test_input_serialized_traversal_rejected(self):
  e=copy.deepcopy(self.export);a=next(x for x in e['assets'] if x['sourceKey']=='input.actions');a['exportMode']='serialized-object';self.assertRaises(ValueError,compile_draft,e,self.spec)
if __name__=='__main__':unittest.main()
