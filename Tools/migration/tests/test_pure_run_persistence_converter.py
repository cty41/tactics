import copy
import json
import unittest
from pathlib import Path

from Tools.migration.pure_run_persistence_converter import compile_persistence_draft


ROOT = Path(__file__).resolve().parents[3]
SPEC = json.loads((ROOT / "Tools/migration/manifest/export-batches/pure-run-persistence-v1.json").read_text(encoding="utf-8"))


class PureRunPersistenceConverterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = ROOT / "Tools/migration/out/pure-run-persistence-v1.unity.json"
        if not path.exists():
            raise unittest.SkipTest("Unity persistence export has not been produced yet")
        cls.export = json.loads(path.read_text(encoding="utf-8"))

    def test_compiles_frozen_three_encounter_contract(self):
        draft = compile_persistence_draft(self.export, SPEC)
        self.assertEqual(draft["definition"]["encounters"], [
            "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3"])
        self.assertEqual(draft["settlement"]["baseGold"], 3)
        self.assertFalse(draft["compatibility"]["unityPlayerPrefsImport"])

    def test_rejects_unauthorized_root(self):
        changed = copy.deepcopy(self.export)
        changed["assets"][0]["sourceKey"] = "pure-run.node.boss"
        with self.assertRaises(ValueError):
            compile_persistence_draft(changed, SPEC)

    def test_rejects_source_hash_drift(self):
        changed = copy.deepcopy(self.export)
        changed["assets"][0]["gitBlobSha1"] = "0" * 40
        with self.assertRaises(ValueError):
            compile_persistence_draft(changed, SPEC)


if __name__ == "__main__":
    unittest.main()
