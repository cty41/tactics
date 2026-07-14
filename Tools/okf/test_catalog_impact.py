from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from catalog_impact import (
    CatalogConfig,
    ScopeConfig,
    impacts_for_paths,
    path_matches,
    source_fingerprint,
    update_concept_frontmatter,
    update_log,
)


class CatalogImpactTests(unittest.TestCase):
    def setUp(self) -> None:
        self.config = CatalogConfig(
            tracked_roots=("Assets/Tactics", ".agents/docs"),
            ignored_paths=("Assets/Tactics/Generated",),
            scopes={
                "battle-system": ScopeConfig(
                    concept="systems/battle.md",
                    paths=("Assets/Tactics/Scripts/Battle",),
                ),
                "skill-graph": ScopeConfig(
                    concept="systems/skill-graph.md",
                    paths=("Assets/Tactics/Battle/Abilities/SkillGraphs",),
                ),
                "first-slice": ScopeConfig(
                    concept="plans/first-slice.md",
                    paths=("Assets/Tactics/Battle/Abilities/SkillGraphs",),
                ),
            },
        )

    def test_directory_pattern_matches_descendants(self) -> None:
        self.assertTrue(path_matches("Assets/Tactics/Scripts/Battle/Log.cs", "Assets/Tactics/Scripts/Battle"))
        self.assertFalse(path_matches("Assets/Tactics/Scripts/Common/Log.cs", "Assets/Tactics/Scripts/Battle"))

    def test_unity_meta_file_inherits_asset_mapping(self) -> None:
        self.assertTrue(
            path_matches(
                "Assets/Tactics/Scripts/Common/UIManager.cs.meta",
                "Assets/Tactics/Scripts/Common/UIManager.cs",
            )
        )

    def test_shared_path_maps_to_multiple_scopes(self) -> None:
        impacts, unmapped = impacts_for_paths(
            self.config,
            ["Assets/Tactics/Battle/Abilities/SkillGraphs/Spear.asset"],
        )
        self.assertEqual({"skill-graph", "first-slice"}, set(impacts))
        self.assertEqual([], unmapped)

    def test_unmapped_tracked_path_is_reported(self) -> None:
        impacts, unmapped = impacts_for_paths(
            self.config,
            ["Assets/Tactics/Scripts/NewSystem/NewSystem.cs"],
        )
        self.assertEqual({}, impacts)
        self.assertEqual(["Assets/Tactics/Scripts/NewSystem/NewSystem.cs"], unmapped)

    def test_ignored_path_is_not_reported(self) -> None:
        impacts, unmapped = impacts_for_paths(
            self.config,
            ["Assets/Tactics/Generated/Cache.asset"],
        )
        self.assertEqual({}, impacts)
        self.assertEqual([], unmapped)

    def test_frontmatter_sync_preserves_body_and_is_idempotent(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            concept = Path(temporary) / "concept.md"
            concept.write_text(
                "---\ntype: Game System\ntimestamp: old\n---\n\n# Body\n\nKeep me.\n",
                encoding="utf-8",
            )
            fingerprint = "sha256:" + "a" * 64
            self.assertTrue(update_concept_frontmatter(concept, "2026-07-14T12:00:00+08:00", fingerprint))
            updated = concept.read_text(encoding="utf-8")
            self.assertIn(f"source_fingerprint: {fingerprint}", updated)
            self.assertIn("# Body\n\nKeep me.", updated)
            self.assertFalse(update_concept_frontmatter(concept, "2026-07-15T12:00:00+08:00", fingerprint))

    def test_log_sync_is_idempotent(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            log = Path(temporary) / "log.md"
            log.write_text("# Log\n\n## 2026-07-14\n* **Creation**: Initial.\n", encoding="utf-8")
            fingerprints = {"battle-system": "sha256:" + "b" * 64}
            self.assertTrue(update_log(log, "2026-07-14T12:00:00+08:00", fingerprints))
            self.assertFalse(update_log(log, "2026-07-14T12:00:00+08:00", fingerprints))
            fingerprints = {"battle-system": "sha256:" + "c" * 64}
            self.assertTrue(update_log(log, "2026-07-14T13:00:00+08:00", fingerprints))
            self.assertEqual(1, log.read_text(encoding="utf-8").count(fingerprints["battle-system"]))
            self.assertEqual(1, log.read_text(encoding="utf-8").count("* **Sync**: `battle-system`"))

    def test_source_fingerprint_changes_with_source_content(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repo_root = Path(temporary)
            source = repo_root / "Assets" / "Tactics" / "Scripts" / "Battle" / "Battle.cs"
            source.parent.mkdir(parents=True)
            source.write_text("first", encoding="utf-8")
            import subprocess

            subprocess.run(["git", "init", "--quiet"], cwd=repo_root, check=True)
            subprocess.run(["git", "add", "."], cwd=repo_root, check=True)
            first = source_fingerprint(repo_root, "battle-system", self.config)
            source.write_text("second", encoding="utf-8")
            second = source_fingerprint(repo_root, "battle-system", self.config)
            self.assertNotEqual(first, second)


if __name__ == "__main__":
    unittest.main()
