import json
import tempfile
import unittest
from pathlib import Path

from Tools.migration.converter import (
    content_id_from_unity,
    convert_manifest,
    reference_diagnostics,
    semantic_diff,
)


class ConverterTests(unittest.TestCase):
    def test_unity_identity_is_stable(self):
        self.assertEqual(
            content_id_from_unity("01234567-89ab-cdef-0123-456789abcdef", 11500000),
            "unity.0123456789abcdef0123456789abcdef.11500000",
        )

    def test_duplicate_and_missing_reference_diagnostics(self):
        entries = [
            {"contentId": "skill.a", "references": ["skill.missing"]},
            {"contentId": "skill.b", "references": []},
        ]
        self.assertEqual(reference_diagnostics(entries), [{"source": "skill.a", "missing": "skill.missing"}])

    def test_dry_run_is_non_mutating_and_repeatable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.json"
            target = root / "target.json"
            ledger = root / "ledger.json"
            source.write_text(json.dumps([{"guid": "01234567-89ab-cdef-0123-456789abcdef", "localFileId": 1}]), encoding="utf-8")

            first = convert_manifest(source, target, ledger, dry_run=True)
            self.assertFalse(target.exists())
            self.assertFalse(ledger.exists())
            convert_manifest(source, target, ledger)
            second = convert_manifest(source, target, ledger, dry_run=True)
            self.assertEqual(first["semanticHash"], second["semanticHash"])

    def test_manual_target_change_requires_force(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.json"
            target = root / "target.json"
            ledger = root / "ledger.json"
            source.write_text(json.dumps([{"guid": "01234567-89ab-cdef-0123-456789abcdef", "localFileId": 1}]), encoding="utf-8")
            convert_manifest(source, target, ledger)
            target.write_text(json.dumps([{"contentId": "manual.change"}]), encoding="utf-8")
            with self.assertRaises(RuntimeError):
                convert_manifest(source, target, ledger)
            convert_manifest(source, target, ledger, force=True)

    def test_semantic_diff_ignores_runtime_metadata(self):
        self.assertEqual(semantic_diff({"a": 1, "targetHash": "x"}, {"a": 2, "targetHash": "y"}), {"a": {"before": 1, "after": 2}})
