import json
import subprocess
import tempfile
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "Tools/unity-mcp/Manage-UnityTestGate.ps1"


class ManageUnityTestGateTests(unittest.TestCase):
    def setUp(self):
        self._temporary_directory = tempfile.TemporaryDirectory()
        self.state_root = Path(self._temporary_directory.name)

    def tearDown(self):
        self._temporary_directory.cleanup()

    def run_gate(self, *arguments):
        return subprocess.run(
            [
                "powershell.exe",
                "-NoProfile",
                "-File",
                str(SCRIPT_PATH),
                *arguments,
                "-StateRoot",
                str(self.state_root),
            ],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )

    def run_gate_command(self, arguments):
        command = (
            f"& '{SCRIPT_PATH}' {arguments} "
            f"-StateRoot '{self.state_root}'"
        )
        return subprocess.run(
            ["powershell.exe", "-NoProfile", "-Command", command],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )

    def create_targeted_gate(self):
        result = self.run_gate_command(
            "-Create -Scope Targeted "
            "-EditModeTestName @('Z.Test','A.Test','A.Test') "
            "-PlayModeTestName @('P.Test')"
        )
        self.assertEqual(0, result.returncode, result.stderr)
        return json.loads(result.stdout)

    def test_targeted_gate_deduplicates_and_groups_by_mode(self):
        gate = self.create_targeted_gate()

        self.assertEqual(2, len(gate["jobs"]))
        self.assertEqual(["A.Test", "Z.Test"], gate["jobs"][0]["payload"]["test_names"])
        self.assertEqual(["P.Test"], gate["jobs"][1]["payload"]["test_names"])
        self.assertEqual(120000, gate["jobs"][1]["payload"]["init_timeout"])

    def test_test_names_are_deduplicated_with_ordinal_case_sensitive_rules(self):
        result = self.run_gate_command(
            "-Create -Scope Targeted "
            "-EditModeTestName @('Namespace.Foo','Namespace.foo','Namespace.Foo')"
        )
        self.assertEqual(0, result.returncode, result.stderr)
        gate = json.loads(result.stdout)
        self.assertEqual(
            ["Namespace.Foo", "Namespace.foo"],
            gate["jobs"][0]["payload"]["test_names"],
        )

    def test_full_gate_has_exactly_two_unfiltered_jobs(self):
        result = self.run_gate("-Create", "-Scope", "Full")

        self.assertEqual(0, result.returncode, result.stderr)
        gate = json.loads(result.stdout)
        self.assertEqual(["EditMode", "PlayMode"], [job["mode"] for job in gate["jobs"]])
        self.assertTrue(all("test_names" not in job["payload"] for job in gate["jobs"]))

    def test_running_job_blocks_next_and_duplicate_start(self):
        gate = self.create_targeted_gate()
        gate_id = gate["gateId"]
        first_job = gate["jobs"][0]
        first_job_id = "1" * 32

        reservation_result = self.run_gate("-Next", "-GateId", gate_id)
        self.assertEqual(0, reservation_result.returncode, reservation_result.stderr)
        reservation = json.loads(reservation_result.stdout)

        started = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            first_job["key"],
            "-JobId",
            first_job_id,
            "-ReservationId",
            reservation["reservationId"],
        )
        self.assertEqual(0, started.returncode, started.stderr)

        next_result = self.run_gate("-Next", "-GateId", gate_id)
        self.assertEqual("waiting", json.loads(next_result.stdout)["state"])

        duplicate = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            first_job["key"],
            "-JobId",
            "2" * 32,
            "-ReservationId",
            reservation["reservationId"],
        )
        self.assertNotEqual(0, duplicate.returncode)

        wrong_reservation_replay = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            first_job["key"],
            "-JobId",
            first_job_id,
            "-ReservationId",
            "f" * 32,
        )
        self.assertNotEqual(0, wrong_reservation_replay.returncode)

    def test_completed_jobs_validate_successfully(self):
        gate = self.create_targeted_gate()
        gate_id = gate["gateId"]

        for index, job in enumerate(gate["jobs"], start=1):
            job_id = str(index) * 32
            reservation_result = self.run_gate("-Next", "-GateId", gate_id)
            self.assertEqual(0, reservation_result.returncode, reservation_result.stderr)
            reservation = json.loads(reservation_result.stdout)
            self.assertEqual(job["key"], reservation["job"]["key"])
            started = self.run_gate(
                "-RecordStart",
                "-GateId",
                gate_id,
                "-JobKey",
                job["key"],
                "-JobId",
                job_id,
                "-ReservationId",
                reservation["reservationId"],
            )
            self.assertEqual(0, started.returncode, started.stderr)
            completed = self.run_gate(
                "-RecordResult",
                "-GateId",
                gate_id,
                "-JobKey",
                job["key"],
                "-JobId",
                job_id,
                "-Status",
                "succeeded",
                "-Total",
                "2",
                "-Passed",
                "2",
                "-Failed",
                "0",
                "-Skipped",
                "0",
            )
            self.assertEqual(0, completed.returncode, completed.stderr)

        validation = self.run_gate("-Validate", "-GateId", gate_id)
        self.assertEqual(0, validation.returncode, validation.stderr)
        self.assertEqual("succeeded", json.loads(validation.stdout)["state"])

    def test_incomplete_gate_fails_validation(self):
        gate = self.create_targeted_gate()

        validation = self.run_gate("-Validate", "-GateId", gate["gateId"])

        self.assertNotEqual(0, validation.returncode)

    def test_explicit_gate_isolates_each_exact_test_name(self):
        result = self.run_gate_command(
            "-Create -Scope Explicit "
            "-PlayModeTestName @('Benchmark.B','Benchmark.A')"
        )

        self.assertEqual(0, result.returncode, result.stderr)
        gate = json.loads(result.stdout)
        self.assertEqual(2, len(gate["jobs"]))
        self.assertEqual(
            [["Benchmark.A"], ["Benchmark.B"]],
            [job["payload"]["test_names"] for job in gate["jobs"]],
        )

    def test_failed_job_requires_supersede_evidence_before_restart(self):
        gate = self.create_targeted_gate()
        gate_id = gate["gateId"]
        job_key = gate["jobs"][0]["key"]
        original_job_id = "3" * 32
        replacement_job_id = "4" * 32

        original_reservation_result = self.run_gate("-Next", "-GateId", gate_id)
        self.assertEqual(
            0, original_reservation_result.returncode, original_reservation_result.stderr
        )
        original_reservation = json.loads(original_reservation_result.stdout)

        started = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            original_job_id,
            "-ReservationId",
            original_reservation["reservationId"],
        )
        self.assertEqual(0, started.returncode, started.stderr)
        failed = self.run_gate(
            "-RecordResult",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            original_job_id,
            "-Status",
            "failed",
            "-Total",
            "1",
            "-Passed",
            "0",
            "-Failed",
            "1",
            "-Skipped",
            "0",
        )
        self.assertEqual(0, failed.returncode, failed.stderr)

        unsupported_restart = self.run_gate(
            "-Next",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
        )
        self.assertNotEqual(0, unsupported_restart.returncode)

        retry_reservation_result = self.run_gate(
            "-Next",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-SupersedesJobId",
            original_job_id,
            "-SupersedeReason",
            "Original job reached a terminal transport failure.",
        )
        self.assertEqual(
            0, retry_reservation_result.returncode, retry_reservation_result.stderr
        )
        retry_reservation = json.loads(retry_reservation_result.stdout)

        supported_restart = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            replacement_job_id,
            "-ReservationId",
            retry_reservation["reservationId"],
        )
        self.assertEqual(0, supported_restart.returncode, supported_restart.stderr)

    def test_reservation_blocks_a_different_gate(self):
        first_gate = self.create_targeted_gate()
        second_gate = self.create_targeted_gate()

        first_next = self.run_gate("-Next", "-GateId", first_gate["gateId"])
        self.assertEqual(0, first_next.returncode, first_next.stderr)
        second_next = self.run_gate("-Next", "-GateId", second_gate["gateId"])

        self.assertEqual(0, second_next.returncode, second_next.stderr)
        blocked = json.loads(second_next.stdout)
        self.assertEqual("blocked", blocked["state"])
        self.assertEqual(first_gate["gateId"], blocked["activeGateId"])

    def test_cancel_reservation_releases_global_slot(self):
        first_gate = self.create_targeted_gate()
        second_gate = self.create_targeted_gate()
        first_reservation = json.loads(
            self.run_gate("-Next", "-GateId", first_gate["gateId"]).stdout
        )

        cancelled = self.run_gate(
            "-CancelReservation",
            "-GateId",
            first_gate["gateId"],
            "-JobKey",
            first_reservation["job"]["key"],
            "-ReservationId",
            first_reservation["reservationId"],
            "-CancellationReason",
            "Independent evidence proved that no MCP job was created.",
        )
        self.assertEqual(0, cancelled.returncode, cancelled.stderr)
        cancelled_job = json.loads(cancelled.stdout)["job"]
        self.assertEqual(1, len(cancelled_job["cancellations"]))
        self.assertEqual(
            first_reservation["reservationId"],
            cancelled_job["cancellations"][0]["reservationId"],
        )
        self.assertEqual(
            "Independent evidence proved that no MCP job was created.",
            cancelled_job["cancellations"][0]["reason"],
        )

        second_next = self.run_gate("-Next", "-GateId", second_gate["gateId"])
        self.assertEqual(0, second_next.returncode, second_next.stderr)
        self.assertEqual("ready", json.loads(second_next.stdout)["state"])

    def test_cancel_reservation_rejects_whitespace_reason_without_releasing_slot(self):
        gate = self.create_targeted_gate()
        reservation = json.loads(
            self.run_gate("-Next", "-GateId", gate["gateId"]).stdout
        )
        rejected = self.run_gate(
            "-CancelReservation",
            "-GateId",
            gate["gateId"],
            "-JobKey",
            reservation["job"]["key"],
            "-ReservationId",
            reservation["reservationId"],
            "-CancellationReason",
            "   ",
        )
        self.assertNotEqual(0, rejected.returncode)

        waiting = self.run_gate("-Next", "-GateId", gate["gateId"])
        self.assertEqual("waiting", json.loads(waiting.stdout)["state"])

    def test_retry_evidence_without_job_key_is_rejected(self):
        gate = self.create_targeted_gate()
        rejected = self.run_gate(
            "-Next",
            "-GateId",
            gate["gateId"],
            "-SupersedesJobId",
            "a" * 32,
            "-SupersedeReason",
            "Retry evidence must be bound.",
        )
        self.assertNotEqual(0, rejected.returncode)

        reservation = self.run_gate("-Next", "-GateId", gate["gateId"])
        self.assertEqual("ready", json.loads(reservation.stdout)["state"])

    def test_concurrent_next_reserves_exactly_one_global_slot(self):
        first_gate = self.create_targeted_gate()
        second_gate = self.create_targeted_gate()
        barrier = threading.Barrier(2)

        def reserve(gate_id):
            barrier.wait()
            return self.run_gate("-Next", "-GateId", gate_id)

        with ThreadPoolExecutor(max_workers=2) as executor:
            results = list(
                executor.map(reserve, (first_gate["gateId"], second_gate["gateId"]))
            )

        self.assertTrue(all(result.returncode == 0 for result in results))
        states = sorted(json.loads(result.stdout)["state"] for result in results)
        self.assertEqual(["blocked", "ready"], states)

    def test_result_requires_complete_counts_and_is_idempotent(self):
        gate = self.create_targeted_gate()
        gate_id = gate["gateId"]
        job_key = gate["jobs"][0]["key"]
        job_id = "5" * 32
        reservation = json.loads(
            self.run_gate("-Next", "-GateId", gate_id).stdout
        )
        started = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            job_id,
            "-ReservationId",
            reservation["reservationId"],
        )
        self.assertEqual(0, started.returncode, started.stderr)

        incomplete = self.run_gate(
            "-RecordResult",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            job_id,
            "-Status",
            "succeeded",
            "-Total",
            "10",
            "-Passed",
            "1",
            "-Failed",
            "0",
            "-Skipped",
            "0",
        )
        self.assertNotEqual(0, incomplete.returncode)

        complete_arguments = (
            "-RecordResult",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            job_id,
            "-Status",
            "succeeded",
            "-Total",
            "2",
            "-Passed",
            "2",
            "-Failed",
            "0",
            "-Skipped",
            "0",
        )
        first_result = self.run_gate(*complete_arguments)
        second_result = self.run_gate(*complete_arguments)
        self.assertEqual(0, first_result.returncode, first_result.stderr)
        self.assertEqual(0, second_result.returncode, second_result.stderr)
        self.assertEqual(
            job_id,
            json.loads(first_result.stdout)["jobs"][0]["result"]["jobId"],
        )

    def test_duplicate_mcp_job_id_cannot_be_bound_to_another_job(self):
        gate = self.create_targeted_gate()
        gate_id = gate["gateId"]
        first_job_id = "6" * 32

        first_reservation = json.loads(
            self.run_gate("-Next", "-GateId", gate_id).stdout
        )
        first_job_key = first_reservation["job"]["key"]
        started = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            first_job_key,
            "-JobId",
            first_job_id,
            "-ReservationId",
            first_reservation["reservationId"],
        )
        self.assertEqual(0, started.returncode, started.stderr)
        completed = self.run_gate(
            "-RecordResult",
            "-GateId",
            gate_id,
            "-JobKey",
            first_job_key,
            "-JobId",
            first_job_id,
            "-Status",
            "succeeded",
            "-Total",
            "1",
            "-Passed",
            "1",
            "-Failed",
            "0",
            "-Skipped",
            "0",
        )
        self.assertEqual(0, completed.returncode, completed.stderr)

        second_reservation = json.loads(
            self.run_gate("-Next", "-GateId", gate_id).stdout
        )
        duplicate = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            second_reservation["job"]["key"],
            "-JobId",
            first_job_id,
            "-ReservationId",
            second_reservation["reservationId"],
        )
        self.assertNotEqual(0, duplicate.returncode)

    def test_validate_rejects_inconsistent_succeeded_state(self):
        result = self.run_gate("-Create", "-Scope", "Full")
        self.assertEqual(0, result.returncode, result.stderr)
        gate = json.loads(result.stdout)
        state_path = self.state_root / f"{gate['gateId']}.json"
        state = json.loads(state_path.read_text(encoding="utf-8"))
        for job in state["jobs"]:
            job["status"] = "succeeded"
            job["attempts"] = []
            job["result"] = None
        state_path.write_text(json.dumps(state), encoding="utf-8")

        validation = self.run_gate("-Validate", "-GateId", gate["gateId"])
        self.assertNotEqual(0, validation.returncode)

    def test_validate_rejects_result_bound_to_different_job_id(self):
        gate = self.create_targeted_gate()
        gate_id = gate["gateId"]
        reservation = json.loads(
            self.run_gate("-Next", "-GateId", gate_id).stdout
        )
        job_key = reservation["job"]["key"]
        job_id = "e" * 32
        started = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            job_id,
            "-ReservationId",
            reservation["reservationId"],
        )
        self.assertEqual(0, started.returncode, started.stderr)
        completed = self.run_gate(
            "-RecordResult",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            job_id,
            "-Status",
            "succeeded",
            "-Total",
            "1",
            "-Passed",
            "1",
            "-Failed",
            "0",
            "-Skipped",
            "0",
        )
        self.assertEqual(0, completed.returncode, completed.stderr)

        state_path = self.state_root / f"{gate_id}.json"
        state = json.loads(state_path.read_text(encoding="utf-8"))
        state["jobs"][0]["result"]["jobId"] = "f" * 32
        state_path.write_text(json.dumps(state), encoding="utf-8")

        validation = self.run_gate("-Validate", "-GateId", gate_id)
        self.assertNotEqual(0, validation.returncode)
        self.assertIn("invalid result counts", validation.stderr)

    def test_schema_v1_terminal_state_is_migrated_without_losing_job_evidence(self):
        created = self.run_gate_command(
            "-Create -Scope Targeted -EditModeTestName @('Legacy.Test')"
        )
        self.assertEqual(0, created.returncode, created.stderr)
        gate = json.loads(created.stdout)
        gate_id = gate["gateId"]
        reservation = json.loads(
            self.run_gate("-Next", "-GateId", gate_id).stdout
        )
        job_key = reservation["job"]["key"]
        job_id = "d" * 32
        started = self.run_gate(
            "-RecordStart",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            job_id,
            "-ReservationId",
            reservation["reservationId"],
        )
        self.assertEqual(0, started.returncode, started.stderr)
        completed = self.run_gate(
            "-RecordResult",
            "-GateId",
            gate_id,
            "-JobKey",
            job_key,
            "-JobId",
            job_id,
            "-Status",
            "succeeded",
            "-Total",
            "1",
            "-Passed",
            "1",
            "-Failed",
            "0",
            "-Skipped",
            "0",
        )
        self.assertEqual(0, completed.returncode, completed.stderr)

        state_path = self.state_root / f"{gate_id}.json"
        state = json.loads(state_path.read_text(encoding="utf-8"))
        state["schemaVersion"] = 1
        for job in state["jobs"]:
            job.pop("cancellations")
            for attempt in job["attempts"]:
                attempt.pop("reservationId")
            if job["result"] is not None:
                job["result"].pop("jobId")
        state_path.write_text(json.dumps(state), encoding="utf-8")

        validation = self.run_gate("-Validate", "-GateId", gate_id)
        self.assertEqual(0, validation.returncode, validation.stderr)

    def test_gate_lock_is_cross_session_file_lock(self):
        source = SCRIPT_PATH.read_text(encoding="utf-8")
        self.assertNotIn('"Local\\Tactics.UnityTestGate.', source)
        self.assertIn("[System.IO.FileShare]::None", source)

    def test_external_file_lock_times_out_then_recovers_after_owner_exit(self):
        lock_path = self.state_root / ".unity-test-gate.lock"
        escaped_path = str(lock_path).replace("'", "''")
        holder_command = (
            f"$stream=[System.IO.File]::Open('{escaped_path}',"
            "[System.IO.FileMode]::OpenOrCreate,"
            "[System.IO.FileAccess]::ReadWrite,"
            "[System.IO.FileShare]::None);"
            "[Console]::Out.WriteLine('READY');[Console]::Out.Flush();"
            "Start-Sleep -Seconds 30"
        )
        holder = subprocess.Popen(
            ["powershell.exe", "-NoProfile", "-Command", holder_command],
            cwd=REPOSITORY_ROOT,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        try:
            self.assertEqual("READY", holder.stdout.readline().strip())
            blocked = self.run_gate("-Create", "-Scope", "Full")
            self.assertNotEqual(0, blocked.returncode)
            self.assertIn("Timed out waiting", blocked.stderr)
        finally:
            holder.terminate()
            holder.wait(timeout=10)
            holder.stdout.close()
            holder.stderr.close()

        recovered = self.run_gate("-Create", "-Scope", "Full")
        self.assertEqual(0, recovered.returncode, recovered.stderr)

    def test_concurrent_create_allows_only_one_writer(self):
        gate_id = "a" * 32

        def create():
            return self.run_gate("-Create", "-Scope", "Full", "-GateId", gate_id)

        with ThreadPoolExecutor(max_workers=8) as executor:
            results = list(executor.map(lambda _: create(), range(8)))

        self.assertEqual(1, sum(result.returncode == 0 for result in results))
        state = json.loads((self.state_root / f"{gate_id}.json").read_text(encoding="utf-8"))
        self.assertEqual(gate_id, state["gateId"])

    def test_script_has_no_mcp_transport_implementation(self):
        source = SCRIPT_PATH.read_text(encoding="utf-8").lower()

        for forbidden in ("invoke-webrequest", "invoke-restmethod", "httpclient", "websocket"):
            self.assertNotIn(forbidden, source)


if __name__ == "__main__":
    unittest.main()
