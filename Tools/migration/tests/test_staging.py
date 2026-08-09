import json
import tempfile
import unittest
from pathlib import Path

from Tools.migration.staging import (
    MigrationConflictError,
    MigrationSourceBinding,
    StagedArtifact,
    apply_staged_batch,
)


class StagingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.source = MigrationSourceBinding(
            source_tag="unity-final-2026-08-08",
            source_commit="168d19345d7e0f7f22ce2516351eda9cef2e1cb1",
            exporter_version="unity-assetdatabase-v1",
            export_hash="sha256:export",
        )

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def artifact(
        self,
        content_id: str = "skill.poison-spear.lv1",
        relative_path: str = "generated/PoisonSpearSkill.tres",
        payload: bytes = b"generated-v1",
        semantic_model: object | None = None,
        resource_uid: str | None = "uid://poisonspear",
    ) -> StagedArtifact:
        return StagedArtifact(
            content_id=content_id,
            relative_path=relative_path,
            payload=payload,
            semantic_model=semantic_model or {"damage": 8, "range": 6},
            resource_uid=resource_uid,
        )

    def test_dry_run_reports_changes_without_touching_files(self) -> None:
        result = apply_staged_batch(
            self.root,
            "poison-spear-lv1",
            self.source,
            [self.artifact()],
            dry_run=True,
        )

        self.assertTrue(result.changed)
        self.assertFalse((self.root / "generated/PoisonSpearSkill.tres").exists())
        self.assertFalse((self.root / "Tools/migration/manifest/state/poison-spear-lv1.json").exists())

    def test_repeated_apply_is_idempotent_and_semantic_noop_preserves_bytes(self) -> None:
        first = apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])
        second = apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])
        third = apply_staged_batch(
            self.root,
            "poison-spear-lv1",
            self.source,
            [self.artifact(payload=b"formatting-only-change")],
        )

        target = self.root / "generated/PoisonSpearSkill.tres"
        self.assertTrue(first.changed)
        self.assertFalse(second.changed)
        self.assertFalse(third.changed)
        self.assertEqual(target.read_bytes(), b"generated-v1")

    def test_manual_target_edit_is_rejected(self) -> None:
        apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])
        target = self.root / "generated/PoisonSpearSkill.tres"
        target.write_bytes(b"manual edit")

        with self.assertRaisesRegex(MigrationConflictError, "modified after"):
            apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])
        self.assertEqual(target.read_bytes(), b"manual edit")

    def test_unmanaged_different_target_is_rejected_but_exact_target_can_be_adopted(self) -> None:
        target = self.root / "generated/PoisonSpearSkill.tres"
        target.parent.mkdir(parents=True)
        target.write_bytes(b"different")
        with self.assertRaisesRegex(MigrationConflictError, "without a matching"):
            apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])

        target.write_bytes(b"generated-v1")
        result = apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])
        self.assertEqual(result.changed_paths, ())
        self.assertTrue(result.ledger_changed)

    def test_resource_uid_change_is_rejected(self) -> None:
        apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])
        with self.assertRaisesRegex(MigrationConflictError, "resource UID changed"):
            apply_staged_batch(
                self.root,
                "poison-spear-lv1",
                self.source,
                [self.artifact(resource_uid="uid://changed")],
            )

    def test_failure_rolls_back_all_targets_and_ledger(self) -> None:
        initial = [
            self.artifact(),
            self.artifact(
                content_id="buff.poison",
                relative_path="generated/PoisonBuff.tres",
                payload=b"buff-v1",
                semantic_model={"damagePerTurn": 2},
                resource_uid="uid://poisonbuff",
            ),
        ]
        apply_staged_batch(self.root, "poison-spear-lv1", self.source, initial)
        ledger_path = self.root / "Tools/migration/manifest/state/poison-spear-lv1.json"
        original_ledger = ledger_path.read_bytes()

        changed = [
            self.artifact(payload=b"generated-v2", semantic_model={"damage": 9, "range": 6}),
            initial[1],
        ]

        def fail(point: str) -> None:
            if point == "after_artifact:1":
                raise RuntimeError("injected failure")

        with self.assertRaisesRegex(RuntimeError, "injected failure"):
            apply_staged_batch(
                self.root,
                "poison-spear-lv1",
                self.source,
                changed,
                failure_injector=fail,
            )

        self.assertEqual((self.root / "generated/PoisonSpearSkill.tres").read_bytes(), b"generated-v1")
        self.assertEqual(ledger_path.read_bytes(), original_ledger)
        self.assertFalse((self.root / ".migration-staging").exists())

    def test_ledger_is_deterministic_and_has_no_temporary_file(self) -> None:
        apply_staged_batch(self.root, "poison-spear-lv1", self.source, [self.artifact()])
        ledger_dir = self.root / "Tools/migration/manifest/state"
        ledger = json.loads((ledger_dir / "poison-spear-lv1.json").read_text(encoding="utf-8"))
        self.assertEqual(ledger["source"]["sourceTag"], "unity-final-2026-08-08")
        self.assertEqual(ledger["artifacts"][0]["contentId"], "skill.poison-spear.lv1")
        self.assertEqual(list(ledger_dir.glob("*.tmp")), [])

    def test_invalid_or_duplicate_targets_are_rejected_before_mutation(self) -> None:
        with self.assertRaises(ValueError):
            apply_staged_batch(
                self.root,
                "poison-spear-lv1",
                self.source,
                [self.artifact(relative_path="../escape.tres")],
            )
        with self.assertRaisesRegex(ValueError, "duplicate ContentId"):
            apply_staged_batch(
                self.root,
                "poison-spear-lv1",
                self.source,
                [self.artifact(), self.artifact(relative_path="generated/duplicate.tres")],
            )
        self.assertFalse((self.root / "generated").exists())


if __name__ == "__main__":
    unittest.main()
