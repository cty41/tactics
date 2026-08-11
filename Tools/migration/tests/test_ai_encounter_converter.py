import copy
import unittest
from pathlib import Path

from Tools.migration.ai_encounter_converter import compile_ai_encounter_draft
from Tools.migration.export_document import load_json

ROOT=Path(__file__).parents[3]
EXPORT=ROOT/"Tools/migration/out/pure-run-ai-encounter-v1.unity.json"
SPEC=ROOT/"Tools/migration/manifest/export-batches/pure-run-ai-encounter-v1.json"

class AiEncounterConverterTests(unittest.TestCase):
    def setUp(self): self.export=load_json(EXPORT); self.spec=load_json(SPEC)
    def test_compiles_six_ai_four_skills_two_layouts_three_encounters(self):
        draft=compile_ai_encounter_draft(self.export,self.spec)
        self.assertEqual(10,len(draft["definitions"])); self.assertEqual(2,len(draft["layouts"])); self.assertEqual(3,len(draft["encounters"]))
        self.assertEqual(["N4","N5","N6","E1","E2","Special"],draft["excludedEncounterIds"])
    def test_rejects_unknown_skill_node(self):
        changed=copy.deepcopy(self.export); graph=next(a for a in changed["assets"] if a["sourceKey"]=="graph.enemy.ranged-attack.lv1")
        graph["objects"][0]["properties"].append({"propertyPath":"_nodes.Array.data[99]","propertyType":"ManagedReference","supported":True,"value":"com.tactics Tactics.UnknownNode"})
        graph["objects"][0]["properties"].sort(key=lambda item: item["propertyPath"])
        with self.assertRaisesRegex(ValueError,"unknown nodes"): compile_ai_encounter_draft(changed,self.spec)
    def test_rejects_unauthorized_root(self):
        changed=copy.deepcopy(self.export); changed["assets"].append(copy.deepcopy(changed["assets"][0]))
        with self.assertRaisesRegex(ValueError,"unique"): compile_ai_encounter_draft(changed,self.spec)

if __name__ == "__main__": unittest.main()
