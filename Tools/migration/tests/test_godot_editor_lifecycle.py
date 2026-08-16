from __future__ import annotations

import json
import subprocess
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
SCRIPT = (
    REPO_ROOT
    / ".agents"
    / "skills"
    / "godot-editor-lifecycle"
    / "scripts"
    / "Invoke-GodotEditorLifecycle.ps1"
)
PROJECT = REPO_ROOT / "godot"


class GodotEditorLifecycleTests(unittest.TestCase):
    def run_script(
        self,
        action: str,
        executable: Path,
        *extra: str,
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(SCRIPT),
                "-Action",
                action,
                "-ProjectPath",
                str(PROJECT),
                "-GodotExecutable",
                str(executable),
                "-DryRun",
                *extra,
            ],
            cwd=REPO_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

    def test_script_uses_normal_close_and_never_force_terminates(self) -> None:
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("CloseMainWindow", source)
        self.assertIn("WaitForExit", source)
        self.assertIn("Start-Process", source)
        self.assertIn("Test-CommandLineTargetsProject", source)
        for forbidden in ("Stop-Process", "taskkill", ".Kill("):
            self.assertNotIn(forbidden, source)

    def test_open_dry_run_targets_only_the_canonical_project(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            executable = Path(temporary) / "Godot_v4.7.1-stable_mono_win64.exe"
            executable.write_bytes(b"")
            result = self.run_script("Open", executable)
            self.assertEqual(0, result.returncode, result.stderr)
            payload = json.loads(result.stdout)
            self.assertEqual("open", payload["action"])
            self.assertEqual("planned", payload["status"])
            self.assertEqual(PROJECT.resolve(), Path(payload["projectPath"]).resolve())

    def test_close_dry_run_requires_and_preserves_the_exact_pid(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            executable = Path(temporary) / "Godot_v4.7.1-stable_mono_win64.exe"
            executable.write_bytes(b"")
            result = self.run_script(
                "Close",
                executable,
                "-EditorProcessId",
                "4242",
            )
            self.assertEqual(0, result.returncode, result.stderr)
            payload = json.loads(result.stdout)
            self.assertEqual("close", payload["action"])
            self.assertEqual(4242, payload["editorProcessId"])

    def test_noncanonical_project_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            executable = temporary_path / "Godot_v4.7.1-stable_mono_win64.exe"
            executable.write_bytes(b"")
            other_project = temporary_path / "other"
            other_project.mkdir()
            result = subprocess.run(
                [
                    "powershell",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    str(SCRIPT),
                    "-Action",
                    "Open",
                    "-ProjectPath",
                    str(other_project),
                    "-GodotExecutable",
                    str(executable),
                    "-DryRun",
                ],
                cwd=REPO_ROOT,
                text=True,
                capture_output=True,
                check=False,
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("Refusing non-canonical Godot project", result.stderr)


if __name__ == "__main__":
    unittest.main()
