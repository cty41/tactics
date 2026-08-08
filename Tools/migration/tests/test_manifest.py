import json
import unittest
from pathlib import Path

from Tools.migration.manifest import (
    normalize_content_id,
    semantic_manifest_hash,
    validate_unique_content_ids,
)


class ManifestTests(unittest.TestCase):
    def test_content_id_is_normalized_and_stable(self) -> None:
        self.assertEqual(normalize_content_id("  Amazon.Poison_Spear "), "amazon.poison_spear")

    def test_duplicate_content_ids_are_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "duplicate ContentId"):
            validate_unique_content_ids(
                [{"contentId": "unit.amazon"}, {"contentId": "UNIT.AMAZON"}]
            )

    def test_manifest_hash_is_order_independent(self) -> None:
        left = [{"contentId": "b", "hash": "2"}, {"contentId": "a", "hash": "1"}]
        right = list(reversed(left))
        self.assertEqual(semantic_manifest_hash(left), semantic_manifest_hash(right))

    def test_poison_spear_batch_is_bound_to_generated_godot_assets(self) -> None:
        root = Path(__file__).resolve().parents[3]
        batch = json.loads(
            (root / "Tools" / "migration" / "manifest" / "batches" / "poison-spear.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(batch["sourceCommit"], "168d1934")
        self.assertEqual(
            set(batch["contentIds"]),
            {
                "skill.poison-spear.lv1",
                "presentation.poison-spear.lv1",
                "projectile.poison-spear",
                "impact.poison-spear",
            },
        )
        for relative_path in batch["targetAssets"]:
            self.assertTrue((root / relative_path).is_file(), relative_path)


if __name__ == "__main__":
    unittest.main()
