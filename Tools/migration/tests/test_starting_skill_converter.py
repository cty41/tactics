import copy
import unittest
from pathlib import Path

from Tools.migration.export_document import load_json
from Tools.migration.starting_skill_converter import compile_starting_skill_draft


ROOT = Path(__file__).resolve().parents[3]
EXPORT = ROOT / "Tools/migration/out/pure-run-starting-skills-v1.unity.json"
SPEC = ROOT / "Tools/migration/manifest/export-batches/pure-run-starting-skills-v1.json"


@unittest.skipUnless(EXPORT.exists(), "frozen Unity export is not present")
class StartingSkillConverterTests(unittest.TestCase):
    def setUp(self):
        self.export = load_json(EXPORT)
        self.spec = load_json(SPEC)

    def test_compiles_twelve_definitions_with_external_poison(self):
        draft = compile_starting_skill_draft(self.export, self.spec)
        self.assertEqual(12, len(draft["definitions"]))
        poison = next(item for item in draft["definitions"] if item["contentId"] == "skill.poison-spear.lv1")
        self.assertTrue(poison["externalDependency"])
        self.assertFalse(draft["payloadBoundary"]["thirdPartyPayloadCopied"])

    def test_growth_visible_starting_skills_have_canonical_ui_metadata(self):
        definitions = {
            item["contentId"]: item
            for item in compile_starting_skill_draft(self.export, self.spec)["definitions"]
        }
        expected_branches = {
            "skill.mage.fireball.lv1": "mage.fireball",
            "skill.mage.ice-bolt.lv1": "mage.ice-bolt",
            "skill.mage.lightning.lv1": "mage.lightning",
            "skill.necromancer.summon-skeleton.lv1": "necromancer.summon-skeleton",
            "skill.necromancer.amplify-damage.lv1": "necromancer.amplify-damage",
            "skill.necromancer.bone-spear.lv1": "necromancer.bone-spear",
            "skill.amazon.thrust.lv1": "amazon.thrust",
            "skill.poison-spear.lv1": "amazon.poison-spear",
            "skill.amazon.combat-techniques.lv1": "amazon.combat-techniques",
        }
        for content_id, branch_id in expected_branches.items():
            with self.subTest(content_id=content_id):
                definition = definitions[content_id]
                self.assertEqual(branch_id, definition["branchId"])
                self.assertTrue(definition["displayName"].strip())
                self.assertTrue(definition["description"].strip())
        self.assertEqual("战斗技巧", definitions["skill.amazon.combat-techniques.lv1"]["displayName"])

    def test_rejects_mana_drift(self):
        changed = copy.deepcopy(self.export)
        asset = next(item for item in changed["assets"] if item["sourceKey"] == "skill.mage.fireball.lv1")
        prop = next(item for item in asset["objects"][0]["properties"] if item["propertyPath"] == "_manaCost")
        prop["value"] = "999"
        with self.assertRaisesRegex(ValueError, "mana cost differs"):
            compile_starting_skill_draft(changed, self.spec)

    def test_rejects_graph_reference_drift(self):
        changed = copy.deepcopy(self.export)
        graph = next(item for item in changed["assets"] if item["sourceKey"] == "graph.mage.ice-bolt.lv1")
        graph["dependencyHash"] = "0" * 32
        with self.assertRaisesRegex(ValueError, "graph reference differs"):
            compile_starting_skill_draft(changed, self.spec)
