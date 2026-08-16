import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


class GodotMainlineVerifierTests(unittest.TestCase):
    def test_solution_contains_only_godot_owned_projects(self) -> None:
        solution = (ROOT / "Tactics.Godot.slnx").read_text(encoding="utf-8")
        self.assertIn("Tactics.FrozenOracle.Tests", solution)
        self.assertIn("Tactics.Godot.Adapter", solution)
        self.assertNotIn("Tactics.UnityOracle", solution)
        self.assertNotIn("Assets/", solution)

    def test_verifier_has_no_public_ownership_skip_mode(self) -> None:
        verifier = (ROOT / "Tools/godot/Verify-GodotProject.ps1").read_text(encoding="utf-8")
        parameter_block = verifier.split(")", 1)[0]
        self.assertNotIn("[switch]$GodotOwned", parameter_block)
        self.assertIn("Godot mainline verification requires retired Unity root", verifier)
        self.assertIn("Tactics.Godot.slnx", verifier)
        self.assertIn("Tactics.FrozenOracle.Tests", verifier)

    def test_physical_copy_invokes_mainline_verifier(self) -> None:
        script = (ROOT / "Tools/migration/Test-GodotOwnedWithoutUnity.ps1").read_text(
            encoding="utf-8"
        )
        self.assertIn("Tools\\godot\\Verify-GodotProject.ps1", script)
        self.assertNotIn("-GodotOwned -GodotExecutable", script)
        self.assertIn("UIElementsSchema", script)


if __name__ == "__main__":
    unittest.main()
