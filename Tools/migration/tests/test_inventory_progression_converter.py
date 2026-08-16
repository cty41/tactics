import copy
import json
import unittest
from pathlib import Path

from Tools.migration.inventory_progression_converter import compile_inventory_progression_draft

ROOT = Path(__file__).parents[3]
EXPORT = ROOT / "Tools/migration/out/pure-run-inventory-progression-v1.unity.json"
SPEC = ROOT / "Tools/migration/manifest/export-batches/pure-run-inventory-progression-v1.json"

class InventoryProgressionConverterTests(unittest.TestCase):
    def setUp(self):
        self.export = json.loads(EXPORT.read_text(encoding="utf-8"))
        self.spec = json.loads(SPEC.read_text(encoding="utf-8"))

    def test_compiles_eighteen_branches_and_thirty_six_levels(self):
        draft = compile_inventory_progression_draft(self.export, self.spec)
        self.assertEqual(18, len(draft["branches"]))
        self.assertEqual(36, len(draft["definitions"]))
        self.assertEqual(34, sum(value.get("graphObjectCount", 0) > 0 for value in draft["definitions"]))
        self.assertEqual(2, max(value["level"] for value in draft["definitions"]))
        self.assertFalse(draft["payloadBoundary"]["unityUiPayloadCopied"])
        definitions = {value["contentId"]: value for value in draft["definitions"]}
        self.assertEqual(1, definitions["skill.mage.fireball.lv2"]["areaRadius"])
        self.assertEqual(4, definitions["skill.amazon.multi-stab.lv2"]["orderedTargetCount"])
        self.assertTrue(definitions["skill.mage.teleport.lv2"]["ignoreLineOfSight"])
        self.assertEqual(definitions["skill.necromancer.bone-spear.lv2"]["requiredAttribute"], "Charisma")
        self.assertEqual("SkeletonMage", definitions["skill.necromancer.skeleton-mage.lv2"]["summonCategory"])
        self.assertEqual(6, definitions["skill.amazon.recover-spear.lv2"]["secondaryDamage"])
        dependencies = {value["contentId"]: value for value in draft["internalSkillDependencies"]}
        fire_demon = dependencies["skill.summon.fire-demon-attack"]
        self.assertEqual("FireDemonAttack", fire_demon["executionKind"])
        self.assertEqual((0, 1, 3, 4, 1), (fire_demon["manaCost"], fire_demon["minRange"],
            fire_demon["maxRange"], fire_demon["damage"], fire_demon["statusDuration"]))
        self.assertEqual("buff.ignite", fire_demon["statusContentId"])
        self.assertFalse(fire_demon["canCrit"])
        self.assertEqual(
            {
                "skill.summon.fire-demon-attack",
                "skill.summon.skeleton-attack.lv1",
                "skill.summon.skeleton-attack.lv2",
                "skill.summon.skeleton-mage-fireball.lv1",
                "skill.summon.skeleton-mage-fireball.lv2",
            },
            set(dependencies),
        )
        skeleton1 = dependencies["skill.summon.skeleton-attack.lv1"]
        skeleton2 = dependencies["skill.summon.skeleton-attack.lv2"]
        self.assertEqual(2, skeleton2["level"])
        self.assertEqual(("MeleeAttack", 0, 1, 2),
            (skeleton1["executionKind"], skeleton1["manaCost"], skeleton1["maxRange"], skeleton1["damage"]))
        self.assertEqual(3, skeleton2["damage"])
        mage1 = dependencies["skill.summon.skeleton-mage-fireball.lv1"]
        mage2 = dependencies["skill.summon.skeleton-mage-fireball.lv2"]
        self.assertEqual(2, mage2["level"])
        self.assertEqual(("Fireball", 0, 4, 2, "buff.ignite"),
            (mage1["executionKind"], mage1["manaCost"], mage1["maxRange"], mage1["damage"], mage1["statusContentId"]))
        self.assertEqual(4, mage2["damage"])
        self.assertTrue(all(not value["growthVisible"] for value in dependencies.values()))

    def test_combat_techniques_preserves_formal_ui_metadata(self):
        definitions = {
            value["contentId"]: value
            for value in compile_inventory_progression_draft(self.export, self.spec)["definitions"]
        }
        for level in (1, 2):
            definition = definitions[f"skill.amazon.combat-techniques.lv{level}"]
            self.assertEqual("amazon.combat-techniques", definition["branchId"])
            self.assertEqual("战斗技巧", definition["displayName"])
            self.assertTrue(definition["description"].strip())

    def test_rejects_missing_level(self):
        changed = copy.deepcopy(self.export)
        changed["assets"] = [value for value in changed["assets"] if value["sourceKey"] != "skill.mage.teleport.lv2"]
        with self.assertRaises(ValueError):
            compile_inventory_progression_draft(changed, self.spec)

    def test_rejects_source_drift(self):
        changed = copy.deepcopy(self.export)
        changed["assets"][0]["gitBlobSha1"] = "0" * 40
        with self.assertRaises(ValueError):
            compile_inventory_progression_draft(changed, self.spec)

    def test_rejects_missing_graph_root(self):
        changed = copy.deepcopy(self.export)
        changed["assets"] = [value for value in changed["assets"] if value["sourceKey"] != "graph.mage.teleport.lv2"]
        with self.assertRaises(ValueError):
            compile_inventory_progression_draft(changed, self.spec)

if __name__ == "__main__":
    unittest.main()
