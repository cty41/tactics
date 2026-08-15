import json
import pathlib
import struct
import subprocess
import tempfile
import unittest


REPO = pathlib.Path(__file__).resolve().parents[3]
TOOLS = REPO / "Tools" / "migration"


def run_pwsh(script: pathlib.Path, *args: str, cwd: pathlib.Path | None = None):
    return subprocess.run(
        ["pwsh", "-NoProfile", "-File", str(script), *args],
        cwd=cwd or REPO,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )


class WindowsRcPipelineTests(unittest.TestCase):
    def test_powershell_scripts_parse(self):
        scripts = [
            TOOLS / "New-GodotOwnedRcSource.ps1",
            TOOLS / "Test-GodotWindowsPackage.ps1",
            TOOLS / "Test-GodotWindowsLaunch.ps1",
            TOOLS / "Build-GodotWindows.ps1",
        ]
        for script in scripts:
            command = (
                "$errors=$null; [void][System.Management.Automation.Language.Parser]::ParseFile("
                f"'{script}', [ref]$null, [ref]$errors); "
                "if($errors.Count){$errors|%{$_.Message};exit 1}"
            )
            result = subprocess.run(
                ["pwsh", "-NoProfile", "-Command", command],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
            )
            self.assertEqual(0, result.returncode, f"{script}: {result.stdout}")

    def test_owned_source_staging_excludes_unity_and_records_hashes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            source = root / "source"
            destination = root / "stage"
            (source / "godot").mkdir(parents=True)
            (source / "Assets").mkdir()
            (source / "src" / "Tactics.UnityOracle.Tests").mkdir(parents=True)
            (source / "godot" / "project.godot").write_text(
                '_mcp_game_helper="*res://addons/godot_ai/runtime/game_helper.gd"\n'
                'enabled=PackedStringArray("res://addons/godot_ai/plugin.cfg", '
                '"res://addons/tactics_tooling/plugin.cfg")\n',
                encoding="utf-8",
            )
            (source / "godot" / "Tactics.Godot.Adapter.sln").write_text(
                "Microsoft Visual Studio Solution File, Format Version 12.00\n",
                encoding="utf-8",
            )
            (source / "Assets" / "Unity.asset").write_text("unity", encoding="utf-8")
            (source / "src" / "Tactics.UnityOracle.Tests" / "Oracle.cs").write_text(
                "oracle", encoding="utf-8"
            )
            subprocess.run(["git", "init", "-q"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.name", "test"], cwd=source, check=True)
            subprocess.run(["git", "config", "user.email", "test@invalid"], cwd=source, check=True)
            subprocess.run(["git", "add", "."], cwd=source, check=True)
            subprocess.run(["git", "commit", "-qm", "fixture"], cwd=source, check=True)

            result = run_pwsh(
                TOOLS / "New-GodotOwnedRcSource.ps1",
                "-SourceRoot", str(source),
                "-DestinationRoot", str(destination),
                "-InitializeGit",
            )
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertFalse((destination / "Assets").exists())
            self.assertFalse((destination / "src" / "Tactics.UnityOracle.Tests").exists())
            project = (destination / "godot" / "project.godot").read_text(encoding="utf-8")
            self.assertNotIn("godot_ai", project)
            manifest = json.loads((destination / "rc-source-manifest.json").read_text(encoding="utf-8-sig"))
            self.assertEqual("godot-owned-without-unity-v1", manifest["boundary"])
            self.assertEqual(2, manifest["fileCount"])
            self.assertEqual(
                ["godot/Tactics.Godot.Adapter.sln", "godot/project.godot"],
                [entry["path"] for entry in manifest["files"]],
            )
            status = subprocess.run(
                ["git", "status", "--porcelain"], cwd=destination, check=True,
                text=True, stdout=subprocess.PIPE
            ).stdout
            self.assertEqual("", status)

    def test_package_audit_writes_manifests_and_rejects_unity_payload(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            package = root / "package"
            package.mkdir()
            pe = bytearray(256)
            struct.pack_into("<H", pe, 0, 0x5A4D)
            struct.pack_into("<I", pe, 0x3C, 0x80)
            struct.pack_into("<I", pe, 0x80, 0x00004550)
            struct.pack_into("<H", pe, 0x84, 0x8664)
            (package / "Tactics.exe").write_bytes(pe)
            (package / "Tactics.pck").write_bytes(b"PCK")
            (package / "Tactics.dll").write_bytes(b"managed")
            source_manifest = root / "source.json"
            source_manifest.write_text('{"schemaVersion":1}', encoding="utf-8")

            result = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
                "-SourceCommit", "a" * 40,
                "-GodotVersion", "4.7.1.stable.mono",
                "-DotnetSdk", "9.0.312",
            )
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertTrue((package / "rc-semantic-manifest.json").is_file())
            self.assertTrue((package / "rc-manifest.json").is_file())
            self.assertTrue((package / "SHA256SUMS.txt").is_file())

            (package / "Tactics.dll").unlink()
            embedded = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
                "-ManagedPayloadMode", "PckEmbedded",
            )
            self.assertEqual(0, embedded.returncode, embedded.stdout)
            semantic = json.loads((package / "rc-semantic-manifest.json").read_text(encoding="utf-8-sig"))
            self.assertEqual("PckEmbedded", semantic["managedPayloadMode"])

            (package / "UnityEngine.CoreModule.dll").write_bytes(b"forbidden")
            rejected = run_pwsh(
                TOOLS / "Test-GodotWindowsPackage.ps1",
                "-PackageRoot", str(package),
                "-SourceManifestPath", str(source_manifest),
            )
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn("Forbidden RC payload", rejected.stdout)

    def test_workflow_is_internal_read_only_and_uploads_bounded_artifacts(self):
        workflow = (REPO / ".github" / "workflows" / "godot-windows-build.yml").read_text(
            encoding="utf-8"
        )
        self.assertIn("workflow_dispatch:", workflow)
        self.assertIn("contents: read", workflow)
        self.assertNotIn("contents: write", workflow)
        self.assertNotIn("releases: write", workflow)
        self.assertIn("New-GodotOwnedRcSource.ps1", workflow)
        self.assertIn("Test-GodotWindowsLaunch.ps1", workflow)
        self.assertIn("Tools/okf/requirements.txt", workflow)
        self.assertIn("git config --global core.autocrlf false", workflow)
        self.assertIn("-GodotOwned", workflow)
        self.assertIn("if: always()", workflow)
        self.assertIn("retention-days: 14", workflow)
        self.assertNotIn("create-release", workflow.lower())
        self.assertNotIn("${{ runner.temp }}", workflow)
        self.assertIn("steps.paths.outputs.diagnostics != ''", workflow)

    def test_export_requires_the_godot_solution_and_rejects_logged_errors(self):
        build_script = (TOOLS / "Build-GodotWindows.ps1").read_text(encoding="utf-8-sig")
        staging_script = (TOOLS / "New-GodotOwnedRcSource.ps1").read_text(encoding="utf-8-sig")

        self.assertIn("Tactics.Godot.Adapter.sln", build_script)
        self.assertIn("Tactics.Godot.Adapter.sln", staging_script)
        self.assertIn("-match '^ERROR:'", build_script)
        self.assertIn("Godot reported export errors despite exit code 0", build_script)


if __name__ == "__main__":
    unittest.main()
