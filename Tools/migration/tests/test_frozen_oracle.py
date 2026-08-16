import hashlib
import json
import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
PROJECT = ROOT / "src/Tactics.FrozenOracle.Tests/Tactics.FrozenOracle.Tests.csproj"
MANIFEST = ROOT / "src/Tactics.FrozenOracle.Tests/frozen-source-manifest.json"


class FrozenOracleTests(unittest.TestCase):
    def test_manifest_matches_repository_owned_snapshots(self) -> None:
        document = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(47, document["entryCount"])
        self.assertEqual(document["entryCount"], len(document["entries"]))
        for entry in document["entries"]:
            path = MANIFEST.parent / entry["frozenPath"]
            payload = path.read_bytes()
            self.assertEqual(entry["byteCount"], len(payload), entry["sourcePath"])
            self.assertEqual(
                entry["sha256"], hashlib.sha256(payload).hexdigest(), entry["sourcePath"]
            )

    def test_generation_is_deterministic_and_project_has_no_live_assets_link(self) -> None:
        before = hashlib.sha256(MANIFEST.read_bytes()).hexdigest()
        subprocess.run(
            ["python", "Tools/migration/freeze_unity_oracle.py"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        after = hashlib.sha256(MANIFEST.read_bytes()).hexdigest()
        self.assertEqual(before, after)
        project = PROJECT.read_text(encoding="utf-8")
        self.assertNotIn("../../Assets/", project)
        self.assertNotIn("Tactics.UnityOracle.Tests", project)


if __name__ == "__main__":
    unittest.main()
