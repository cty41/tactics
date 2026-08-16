import json
import subprocess
import unittest
from pathlib import Path


class ContractDecisionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = Path(__file__).resolve().parents[3]
        cls.decisions = json.loads(
            (cls.root / "Tests" / "golden" / "contract-decisions.json").read_text(
                encoding="utf-8"
            )
        )
        cls.contracts = {
            contract["id"]: contract for contract in cls.decisions["contracts"]
        }

    def test_decisions_are_bound_to_the_frozen_snapshot(self) -> None:
        snapshot = json.loads(
            (self.root / "Tools" / "migration" / "manifest" / "source-snapshot.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(self.decisions["unityOracle"]["tag"], snapshot["unityTag"])
        self.assertEqual(self.decisions["unityOracle"]["commit"], snapshot["unityCommit"])
        self.assertEqual(self.decisions["unityOracle"]["policy"], "read_only")

    def test_all_frozen_evidence_blobs_match_final_commit(self) -> None:
        commit = self.decisions["unityOracle"]["commit"]
        for contract in self.decisions["contracts"]:
            for evidence in contract["frozenEvidence"]:
                with self.subTest(contract=contract["id"], path=evidence["path"]):
                    result = subprocess.run(
                        ["git", "rev-parse", f"{commit}:{evidence['path']}"],
                        cwd=self.root,
                        check=True,
                        capture_output=True,
                        text=True,
                    )
                    self.assertEqual(result.stdout.strip(), evidence["blob"])
                    self.assertTrue(evidence["observation"])

    def test_transition_decision_matches_runtime_contract(self) -> None:
        contract = self.contracts["battle.command-event-transition"]
        source = (self.root / "src" / "Tactics.Core" / "Battle" / "BattleTransitionService.cs").read_text(
            encoding="utf-8-sig"
        )
        self.assertEqual(contract["resolution"], "versioned_migration_contract")
        self.assertIn(f'ContractId = "{contract["runtimeContractId"]}"', source)
        self.assertGreaterEqual(len(contract["invariants"]), 4)

    def test_rng_decision_matches_versioned_algorithm(self) -> None:
        contract = self.contracts["random.splitmix64-v1"]
        source = (self.root / "src" / "Tactics.Core" / "Randomness" / "DeterministicRandom.cs").read_text(
            encoding="utf-8-sig"
        )
        self.assertEqual(contract["resolution"], "deterministic_replacement_contract")
        self.assertIn(f'AlgorithmId = "{contract["runtimeContractId"]}"', source)
        self.assertGreaterEqual(len(contract["invariants"]), 4)

    def test_godot_line_of_sight_replacement_matches_runtime_contract(self) -> None:
        contract = self.contracts["battle.line-of-sight-shadow-cone-v1"]
        source = (
            self.root / "src" / "Tactics.Core" / "Pathfinding" / "LineOfSight.cs"
        ).read_text(encoding="utf-8-sig")
        self.assertEqual(
            contract["resolution"], "godot_mainline_replacement_contract"
        )
        self.assertIn(f'ContractId = "{contract["runtimeContractId"]}"', source)
        self.assertEqual(
            contract["supersedes"],
            "10x10-core-vectors.json#/lineOfSightQueries/0",
        )
        self.assertGreaterEqual(len(contract["invariants"]), 5)


if __name__ == "__main__":
    unittest.main()
