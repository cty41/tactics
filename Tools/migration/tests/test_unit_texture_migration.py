import copy
import hashlib
import unittest
from pathlib import Path

from Tools.migration.export_document import load_json
from Tools.migration.unit_texture_migration import compile_unit_texture_artifacts


ROOT = Path(__file__).resolve().parents[3]
DRAFT_PATH = ROOT / "Tools/migration/out/pure-run-units-v1.draft.json"


@unittest.skipUnless(DRAFT_PATH.is_file(), "real Unit typed draft is a disposable local artifact")
class RealUnitTextureMigrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.draft = load_json(DRAFT_PATH)

    def test_compiles_exact_project_owned_png_allowlist(self):
        source, artifacts = compile_unit_texture_artifacts(ROOT, self.draft)
        self.assertEqual("unity-final-2026-08-08", source.source_tag)
        self.assertEqual(19, len(artifacts))
        self.assertEqual(19, len({artifact.relative_path for artifact in artifacts}))
        self.assertTrue(
            all(artifact.relative_path.startswith("godot/assets/units/") for artifact in artifacts)
        )
        self.assertTrue(all(artifact.relative_path.endswith(".png") for artifact in artifacts))
        self.assertTrue(
            all(
                hashlib.sha256(artifact.payload).hexdigest()
                == artifact.semantic_model["sha256"]
                for artifact in artifacts
            )
        )

    def test_source_hash_drift_is_rejected(self):
        draft = copy.deepcopy(self.draft)
        draft["textureAssets"][0]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "frozen SHA-256"):
            compile_unit_texture_artifacts(ROOT, draft)

    def test_third_party_source_and_noncanonical_target_are_rejected(self):
        draft = copy.deepcopy(self.draft)
        draft["textureAssets"][0]["sourcePath"] = "Assets/ThirdParty/forbidden.png"
        with self.assertRaisesRegex(ValueError, "project-owned allowlist"):
            compile_unit_texture_artifacts(ROOT, draft)

        draft = copy.deepcopy(self.draft)
        draft["textureAssets"][0]["targetPath"] = "res://content/forbidden.png"
        with self.assertRaisesRegex(ValueError, "canonical Godot folder"):
            compile_unit_texture_artifacts(ROOT, draft)


if __name__ == "__main__":
    unittest.main()
