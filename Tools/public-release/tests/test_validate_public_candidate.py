import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "validate_public_candidate.py"
SPEC = importlib.util.spec_from_file_location("validate_public_candidate", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
audit = MODULE.audit


class PublicCandidateAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        (self.root / "Tools/public-release").mkdir(parents=True)
        for required in (
            "LICENSE", "NOTICE", "ASSET_LICENSE.md", "THIRD_PARTY_NOTICES.md",
            "TRADEMARKS.md", "CONTRIBUTING.md", "SECURITY.md",
        ):
            (self.root / required).write_text("test\n", encoding="utf-8")
        (self.root / "asset.png").write_bytes(b"asset")
        self.hash = hashlib.sha256(b"asset").hexdigest()
        self._write_policy()
        self._write_manifest(self.hash)
        subprocess.run(["git", "add", "."], cwd=self.root, check=True)

    def tearDown(self):
        self.temp.cleanup()

    def _write_policy(self):
        policy = {
            "schemaVersion": 1,
            "defaultTextLicense": "Apache-2.0",
            "assetManifest": "Tools/public-release/asset-provenance.json",
            "requiredFiles": [
                "LICENSE", "NOTICE", "ASSET_LICENSE.md", "THIRD_PARTY_NOTICES.md",
                "TRADEMARKS.md", "CONTRIBUTING.md", "SECURITY.md",
            ],
            "mediaExtensions": [".png"],
            "excludedFromPublicRoot": ["private/**"],
            "forbiddenPublicPrefixes": ["Assets/"],
            "allowedLicenses": ["CC-BY-4.0"],
        }
        (self.root / "Tools/public-release/public-source-policy.json").write_text(
            json.dumps(policy), encoding="utf-8")

    def _write_manifest(self, digest):
        manifest = {
            "schemaVersion": 1,
            "defaultAttribution": "cty41",
            "entries": [{
                "path": "asset.png",
                "sha256": digest,
                "status": "approved",
                "rightsHolder": "cty41",
                "license": "CC-BY-4.0",
                "provenance": "test",
            }],
        }
        (self.root / "Tools/public-release/asset-provenance.json").write_text(
            json.dumps(manifest), encoding="utf-8")

    def test_candidate_accepts_registered_asset(self):
        result = audit(self.root, candidate=True)
        self.assertTrue(result.ok, result.errors)
        self.assertEqual(1, result.approved_assets)

    def test_candidate_rejects_hash_drift(self):
        self._write_manifest("0" * 64)
        result = audit(self.root, candidate=True)
        self.assertIn("asset_hash_mismatch:asset.png", result.errors)

    def test_report_warns_but_candidate_rejects_excluded_file(self):
        (self.root / "private").mkdir()
        (self.root / "private/internal.txt").write_text("internal", encoding="utf-8")
        subprocess.run(["git", "add", "private/internal.txt"], cwd=self.root, check=True)
        report = audit(self.root, candidate=False)
        candidate = audit(self.root, candidate=True)
        self.assertIn("excluded_files_tracked:1", report.warnings)
        self.assertIn("excluded_files_tracked:1", candidate.errors)


if __name__ == "__main__":
    unittest.main()
