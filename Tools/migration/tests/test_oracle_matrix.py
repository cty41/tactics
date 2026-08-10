import json
import subprocess
import unittest
from pathlib import Path


class OracleMatrixTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = Path(__file__).resolve().parents[3]
        cls.matrix = json.loads(
            (cls.root / "Tests" / "golden" / "oracle-matrix.json").read_text(encoding="utf-8")
        )

    def test_matrix_is_bound_to_frozen_unity_snapshot(self) -> None:
        snapshot = json.loads(
            (self.root / "Tools" / "migration" / "manifest" / "source-snapshot.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(self.matrix["unityOracle"]["tag"], snapshot["unityTag"])
        self.assertEqual(self.matrix["unityOracle"]["commit"], snapshot["unityCommit"])
        self.assertEqual(self.matrix["unityOracle"]["policy"], "read_only")

    def test_bound_csharp_oracles_exist_and_name_real_tests(self) -> None:
        for contract in self.matrix["contracts"]:
            source_file = contract["sourceFile"]
            if source_file is not None and source_file.endswith(".cs"):
                source_path = self.root / source_file
                with self.subTest(contract=contract["id"]):
                    self.assertTrue(source_path.is_file(), source_file)
                    source = source_path.read_text(encoding="utf-8-sig")
                    for test_name in contract["sourceTests"]:
                        self.assertIn(test_name, source)

            for runtime_file in contract.get("sourceRuntimeFiles", []):
                with self.subTest(contract=contract["id"], runtime=runtime_file):
                    self.assertTrue((self.root / runtime_file).is_file(), runtime_file)

            for asset_test_file in contract.get("sourceAssetTests", []):
                with self.subTest(contract=contract["id"], asset_test=asset_test_file):
                    self.assertTrue((self.root / asset_test_file).is_file(), asset_test_file)

            for data_file in contract.get("sourceDataFiles", []):
                with self.subTest(contract=contract["id"], data=data_file):
                    self.assertTrue((self.root / data_file).is_file(), data_file)

            harness_file = contract.get("oracleHarnessFile")
            if harness_file is None:
                continue
            with self.subTest(contract=contract["id"], harness=harness_file):
                harness_path = self.root / harness_file
                self.assertTrue(harness_path.is_file(), harness_file)
                harness = harness_path.read_text(encoding="utf-8-sig")
                for test_name in contract["oracleTests"]:
                    self.assertIn(test_name, harness)

    def test_linked_source_blob_ids_match_the_frozen_commit_and_harness(self) -> None:
        commit = self.matrix["unityOracle"]["commit"]
        harness = (self.root / self.matrix["linkedSourceOracle"]["testFile"]).read_text(
            encoding="utf-8-sig"
        )
        for path, expected_blob_id in self.matrix["frozenSourceBlobs"].items():
            with self.subTest(path=path):
                result = subprocess.run(
                    ["git", "rev-parse", f"{commit}:{path}"],
                    cwd=self.root,
                    check=True,
                    capture_output=True,
                    text=True,
                )
                self.assertEqual(result.stdout.strip(), expected_blob_id)
                self.assertIn(expected_blob_id, harness)

    def test_frozen_unit_asset_blob_ids_match_without_parsing_unity_yaml(self) -> None:
        commit = self.matrix["unityOracle"]["commit"]
        frozen_assets = self.matrix["frozenAssetBlobs"]
        self.assertEqual(len(frozen_assets), 38)
        for path, expected_blob_id in frozen_assets.items():
            with self.subTest(path=path):
                self.assertTrue((self.root / path).is_file(), path)
                result = subprocess.run(
                    ["git", "rev-parse", f"{commit}:{path}"],
                    cwd=self.root,
                    check=True,
                    capture_output=True,
                    text=True,
                )
                self.assertEqual(result.stdout.strip(), expected_blob_id)

    def test_frozen_json_blob_ids_match_final_commit_without_line_ending_assumptions(self) -> None:
        commit = self.matrix["unityOracle"]["commit"]
        frozen_data = self.matrix["frozenDataBlobs"]
        self.assertEqual(2, len(frozen_data))
        for path, expected_blob_id in frozen_data.items():
            with self.subTest(path=path):
                result = subprocess.run(
                    ["git", "rev-parse", f"{commit}:{path}"],
                    cwd=self.root,
                    check=True,
                    capture_output=True,
                    text=True,
                )
                self.assertEqual(result.stdout.strip(), expected_blob_id)

    def test_oracle_and_contract_statuses_are_explicit(self) -> None:
        statuses = {contract["status"] for contract in self.matrix["contracts"]}
        self.assertIn("unity_final_linked_source_oracle", statuses)
        self.assertNotIn("missing_dedicated_unity_oracle", statuses)
        self.assertIn("unity_final_asset_export_and_linked_source_oracle", statuses)
        self.assertNotIn("real_asset_export_and_oracle_pending", statuses)
        self.assertNotIn("approved_core_contract_unity_oracle_pending", statuses)
        self.assertNotIn("approved_core_replay_contract_unity_rng_parity_pending", statuses)
        self.assertIn("versioned_migration_contract", statuses)
        self.assertIn("deterministic_replacement_contract", statuses)
        contract_statuses = {
            contract["id"]: contract["status"] for contract in self.matrix["contracts"]
        }
        self.assertEqual(
            contract_statuses["runtime-scope.lifecycle"],
            "unity_final_linked_source_oracle",
        )
        self.assertEqual(
            contract_statuses["presentation.fork-join"],
            "unity_final_linked_source_oracle",
        )
        self.assertEqual(
            contract_statuses["turn.current-round-dynamic-reorder"],
            "unity_final_linked_source_oracle",
        )
        self.assertEqual(
            contract_statuses["battle.command-event-transition"],
            "versioned_migration_contract",
        )
        self.assertEqual(
            contract_statuses["random.splitmix64-v1"],
            "deterministic_replacement_contract",
        )
        self.assertEqual(
            contract_statuses["skill.poison-spear-lv1"],
            "unity_final_asset_export_and_linked_source_oracle",
        )
        self.assertEqual(
            contract_statuses["unit.pure-run-v1"],
            "unity_final_asset_export_and_linked_source_oracle",
        )
        self.assertEqual(
            contract_statuses["buff-item.pure-run-v1"],
            "unity_final_asset_export_and_linked_source_oracle",
        )

    def test_golden_schema_replays_commands_events_rng_and_final_state(self) -> None:
        golden = json.loads(
            (self.root / "Tests" / "golden" / "10x10-core-vectors.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(golden["schemaVersion"], 7)
        self.assertTrue(golden["randomCases"])
        self.assertTrue(golden["battleScenarios"])
        self.assertTrue(golden["statusCases"])
        self.assertTrue(golden["consumableCases"])
        self.assertTrue(golden["equipmentCases"])
        self.assertEqual(
            golden["pathQueries"][0]["oracleStatus"],
            "unity_final_linked_source_oracle",
        )
        self.assertEqual(
            golden["initiativeCases"][0]["oracleStatus"],
            "unity_final_linked_source_oracle",
        )
        self.assertNotEqual(
            golden["initiativeCases"][0]["entries"][0]["instanceId"],
            golden["initiativeCases"][0]["entries"][0]["definitionId"],
        )
        self.assertEqual(
            golden["initiativeRoundCases"][0]["oracleStatus"],
            "unity_final_linked_source_oracle",
        )
        self.assertEqual(
            golden["runtimeScopeCases"][0]["oracleStatus"],
            "unity_final_linked_source_oracle",
        )
        self.assertEqual(
            golden["presentationCases"][0]["oracleStatus"],
            "unity_final_linked_source_oracle",
        )
        scenario = golden["battleScenarios"][0]
        self.assertTrue(scenario["commands"])
        self.assertTrue(all(command["expectedEvents"] for command in scenario["commands"]))
        self.assertIn("units", scenario["expectedFinalState"])
        self.assertEqual(scenario["oracleStatus"], "versioned_migration_contract")
        self.assertEqual(
            golden["randomCases"][0]["oracleStatus"],
            "deterministic_replacement_contract",
        )

    def test_linked_source_oracle_is_test_only_and_in_the_unified_gate(self) -> None:
        oracle = self.matrix["linkedSourceOracle"]
        self.assertEqual(oracle["policy"], "test_only_no_runtime_dependency")
        solution = (self.root / "Tactics.Migration.slnx").read_text(encoding="utf-8-sig")
        verifier = (self.root / "Tools" / "migration" / "Verify-GodotMigration.ps1").read_text(
            encoding="utf-8-sig"
        )
        self.assertIn(oracle["project"], solution)
        self.assertIn("Tactics.UnityOracle.Tests.csproj", verifier)


if __name__ == "__main__":
    unittest.main()
