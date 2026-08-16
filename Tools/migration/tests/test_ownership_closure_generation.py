import tempfile
import unittest
from pathlib import Path

from Tools.migration.ownership_closure_generation import compile_evidence


class OwnershipClosureGenerationTests(unittest.TestCase):
    def test_rejects_missing_generated_resource(self):
        draft = {
            "batchId": "pure-run-ownership-closure-v1",
            "playerSkillDefinitions": [{"contentId": f"skill.test.branch-{index}.lv3"} for index in range(9)],
            "internalSkillDefinitions": [{"contentId": "skill.test.internal.lv3"}],
            "source": {},
            "payloadBoundary": {},
        }
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(FileNotFoundError):
                compile_evidence(draft, Path(directory))


if __name__ == "__main__":
    unittest.main()
