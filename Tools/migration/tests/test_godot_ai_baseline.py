import unittest
from pathlib import Path
from unittest.mock import patch

from Tools.migration.godot_ai_baseline import validate_checkout


class GodotAiBaselineTests(unittest.TestCase):
    @patch("Tools.migration.godot_ai_baseline.subprocess.check_output")
    @patch("Tools.migration.godot_ai_baseline.Path.read_bytes", return_value=b"fixture")
    def test_baseline_contract_is_read_only(self, read_bytes, check_output):
        check_output.side_effect = [
            "678b16a6a0a335cf80cbb7d3f85c183cd3e616de\n",
            "v3.1.2\n",
        ]
        # The digest assertion is intentionally exercised with the pinned digest
        # to keep this test independent from the user's external checkout.
        with patch("Tools.migration.godot_ai_baseline.hashlib.sha256") as sha256:
            sha256.return_value.hexdigest.return_value = "6290daf20479a1ad215abce7b489f78bd6b43154909fd7317ee3f4551c299514"
            result = validate_checkout(Path("D:/codes/godot-ai"))
        self.assertEqual(result["tag"], "v3.1.2")
        read_bytes.assert_called_once()
