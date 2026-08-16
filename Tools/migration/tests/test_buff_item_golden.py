import json
import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


class BuffItemGoldenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.golden = json.loads(
            (ROOT / "Tests/golden/buff-item-batch-v1.json").read_text(encoding="utf-8")
        )
        cls.batch = json.loads(
            (ROOT / "Tools/migration/manifest/batches/pure-run-buffs-items-v1.json").read_text(
                encoding="utf-8"
            )
        )
        cls.specification = json.loads(
            (ROOT / "Tools/migration/manifest/export-batches/pure-run-buffs-items-v1.json").read_text(
                encoding="utf-8"
            )
        )

    def test_identity_counts_and_poison_ownership_are_explicit(self):
        ids = [item["contentId"] for item in self.golden["buffs"]]
        ids += [item["contentId"] for item in self.golden["consumables"]]
        ids += [item["contentId"] for item in self.golden["equipment"]]
        self.assertEqual(29, len(ids))
        self.assertEqual(29, len(set(ids)))
        self.assertEqual(["buff.poison"], self.golden["externalContentDependencies"])
        self.assertEqual("Validated", self.batch["status"])
        self.assertEqual("UnityOwned", self.batch["owner"])
        self.assertEqual("godot-resource-saver-buff-item-v1", self.batch["generation"])
        self.assertEqual(
            "Tools/migration/manifest/state/pure-run-buffs-items-v1.json",
            self.batch["generationLedger"],
        )
        self.assertEqual(
            "Tools/migration/manifest/receipts/pure-run-buffs-items-v1-generation.json",
            self.batch["generationReceipt"],
        )
        self.assertEqual("not_applicable_no_visual_payload", self.batch["validation"]["visualAcceptance"])

    def test_specification_asset_blobs_match_final_tag_without_yaml_parsing(self):
        self.assertEqual(14, len(self.specification["assets"]))
        commit = self.specification["sourceCommit"]
        for asset in self.specification["assets"]:
            with self.subTest(path=asset["sourcePath"]):
                result = subprocess.run(
                    ["git", "rev-parse", f"{commit}:{asset['sourcePath']}"],
                    cwd=ROOT,
                    check=True,
                    capture_output=True,
                    text=True,
                )
                self.assertEqual(asset["gitBlobSha1"], result.stdout.strip())

    def test_json_sources_match_final_tag_and_preserve_source_ids(self):
        commit = self.specification["sourceCommit"]
        for key in ("consumablesJson", "equipmentJson"):
            source = self.golden["source"][key]
            result = subprocess.run(
                ["git", "rev-parse", f"{commit}:{source['sourcePath']}"],
                cwd=ROOT,
                check=True,
                capture_output=True,
                text=True,
            )
            self.assertEqual(source["gitBlobSha1"], result.stdout.strip())
        self.assertEqual("life_potion", next(
            item for item in self.golden["consumables"]
            if item["contentId"] == "item.consumable.life-potion"
        )["sourceId"])
        self.assertEqual("steel_sword_01", next(
            item for item in self.golden["equipment"]
            if item["contentId"] == "item.equipment.steel-sword-01"
        )["sourceId"])


if __name__ == "__main__":
    unittest.main()
