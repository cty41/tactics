import tempfile
import unittest
from pathlib import Path

from Tools.migration.map_treasure_generation import compile_evidence


class MapTreasureGenerationTests(unittest.TestCase):
    def test_rejects_missing_generated_resources(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(FileNotFoundError):
                compile_evidence(Path(directory))


if __name__ == "__main__":
    unittest.main()
