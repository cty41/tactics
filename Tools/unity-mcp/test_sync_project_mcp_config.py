import json
import os
import re
import shutil
import subprocess
import tempfile
import time
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SYNC_SCRIPT = Path("Tools/unity-mcp/Sync-ProjectMcpConfig.ps1")
INITIALIZE_SCRIPT = Path("Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1")
LOCK_ANCHOR = Path("Tools/unity-mcp/ProjectMcpConfig.lock-anchor")
TEMPLATE_PATHS = (
    Path(".agents/mcp.template.json"),
    Path(".codex/config.template.toml"),
    Path(".opencode/opencode.template.json"),
    Path(".mimocode/mimocode.template.json"),
)
MANAGED_PATHS = (
    Path(".agents/mcp.json"),
    Path(".agents/mcp.local.json"),
    Path(".codex/config.toml"),
    Path(".opencode/opencode.json"),
    Path(".mimocode/mimocode.json"),
)


class SyncProjectMcpConfigTests(unittest.TestCase):
    def setUp(self):
        self._temporary_directory = tempfile.TemporaryDirectory()
        self.project_root = Path(self._temporary_directory.name)

        for relative_path in (
            SYNC_SCRIPT,
            INITIALIZE_SCRIPT,
            LOCK_ANCHOR,
            *TEMPLATE_PATHS,
        ):
            source = REPOSITORY_ROOT / relative_path
            destination = self.project_root / relative_path
            destination.parent.mkdir(parents=True, exist_ok=True)
            if relative_path == LOCK_ANCHOR:
                staged_anchor = subprocess.run(
                    ["git", "show", f":{LOCK_ANCHOR.as_posix()}"],
                    cwd=REPOSITORY_ROOT,
                    capture_output=True,
                    check=False,
                )
                self.assertEqual(0, staged_anchor.returncode, staged_anchor.stderr)
                destination.write_bytes(staged_anchor.stdout)
            else:
                shutil.copy2(source, destination)

        agents_directory = self.project_root / ".agents"
        agents_directory.mkdir(parents=True, exist_ok=True)
        self.write_source("http://127.0.0.1:8081/mcp")

    def tearDown(self):
        self._temporary_directory.cleanup()

    def write_source(self, endpoint):
        (self.project_root / ".agents/mcp.json").write_text(
            json.dumps(
                {"mcpServers": {"unityMCP": {"url": endpoint}}},
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )

    def run_powershell(self, script, *arguments, environment=None, timeout=40):
        return subprocess.run(
            [
                "powershell.exe",
                "-NoProfile",
                "-File",
                str(self.project_root / script),
                *arguments,
            ],
            cwd=self.project_root,
            capture_output=True,
            text=True,
            check=False,
            env=environment,
            timeout=timeout,
        )

    def run_sync(self, *arguments, environment=None, timeout=40):
        return self.run_powershell(
            SYNC_SCRIPT,
            *arguments,
            environment=environment,
            timeout=timeout,
        )

    def run_initialize(self, *arguments, environment=None, timeout=40):
        return self.run_powershell(
            INITIALIZE_SCRIPT,
            *arguments,
            environment=environment,
            timeout=timeout,
        )

    def snapshot_tree(self):
        snapshot = {}
        for path in sorted(self.project_root.rglob("*")):
            relative = path.relative_to(self.project_root).as_posix()
            if path.is_dir():
                snapshot[relative] = ("directory", path.stat().st_mtime_ns)
            else:
                stat = path.stat()
                snapshot[relative] = ("file", path.read_bytes(), stat.st_mtime_ns)
        return snapshot

    def snapshot_bytes_tree(self):
        snapshot = {}
        for path in sorted(self.project_root.rglob("*")):
            relative = path.relative_to(self.project_root).as_posix()
            if path.is_dir():
                snapshot[relative] = ("directory",)
            else:
                snapshot[relative] = ("file", path.read_bytes())
        return snapshot

    def managed_snapshot(self):
        return {
            path: (
                (self.project_root / path).exists(),
                (self.project_root / path).read_bytes()
                if (self.project_root / path).exists()
                else None,
            )
            for path in MANAGED_PATHS
        }

    def assert_managed_snapshot(self, expected):
        self.assertEqual(expected, self.managed_snapshot())

    def read_all_endpoints(self):
        source_url = json.loads(
            (self.project_root / ".agents/mcp.json").read_text(encoding="utf-8")
        )["mcpServers"]["unityMCP"]["url"]
        codex_text = (self.project_root / ".codex/config.toml").read_text(
            encoding="utf-8"
        )
        codex_url = None
        current_section = None
        for line in codex_text.splitlines():
            section_match = re.fullmatch(r"\[([A-Za-z0-9_.-]+)\]", line)
            if section_match:
                current_section = section_match.group(1)
                continue
            url_match = re.fullmatch(r'url = "([^"]+)"', line)
            if url_match:
                self.assertEqual("mcp_servers.unityMCP", current_section)
                self.assertIsNone(codex_url)
                codex_url = url_match.group(1)
        self.assertIsNotNone(codex_url)
        opencode_url = json.loads(
            (self.project_root / ".opencode/opencode.json").read_text(
                encoding="utf-8"
            )
        )["mcp"]["unity-MCP"]["url"]
        mimocode_url = json.loads(
            (self.project_root / ".mimocode/mimocode.json").read_text(
                encoding="utf-8"
            )
        )["mcp"]["unity-MCP"]["url"]
        return source_url, codex_url, opencode_url, mimocode_url

    def assert_injected_failure(self, result, position):
        output = result.stdout + result.stderr
        self.assertNotEqual(0, result.returncode)
        self.assertIn(
            f"Injected MCP configuration transaction failure after {position} operations.",
            output,
        )
        self.assertNotIn("Invalid test-only MCP configuration fault injection", output)

    def test_sync_creates_safe_utf8_configs_with_timeout(self):
        result = self.run_sync()

        self.assertEqual(0, result.returncode, result.stderr)
        for path in MANAGED_PATHS:
            if path == Path(".agents/mcp.local.json"):
                continue
            content = (self.project_root / path).read_bytes()
            self.assertFalse(content.startswith(b"\xef\xbb\xbf"), path)
            content.decode("utf-8", errors="strict")
        self.assertEqual(
            {"http://127.0.0.1:8081/mcp"}, set(self.read_all_endpoints())
        )
        document = json.loads(
            (self.project_root / ".mimocode/mimocode.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(300000, document["mcp"]["unity-MCP"]["timeout"])

    def test_sync_preserves_non_ascii_personal_mimocode_settings(self):
        output_path = self.project_root / ".mimocode/mimocode.json"
        output_path.parent.mkdir(parents=True, exist_ok=True)
        personal_content = json.dumps(
                {
                    "plugin": ["custom-plugin"],
                    "mcp": {
                        "unity-MCP": {
                            "type": "remote",
                            "url": "http://127.0.0.1:9999/mcp",
                        },
                        "personal": {
                            "type": "remote",
                            "url": "https://example.invalid/mcp",
                            "headers": {"X-CUSTOM": "保留我"},
                        },
                    },
                    "unknown": {"说明": [None, 9007199254740991, "保持原样"]},
                },
                ensure_ascii=False,
            )
        personal_content = personal_content[:-1] + (
            ', "precise": 0.12345678901234567890123456789, "huge": 1e400}'
        )
        output_path.write_text(personal_content, encoding="utf-8")

        result = self.run_sync()

        self.assertEqual(0, result.returncode, result.stderr)
        text = output_path.read_text(encoding="utf-8")
        self.assertIn("保留我", text)
        self.assertIn("0.12345678901234567890123456789", text)
        self.assertRegex(text, r'"huge"\s*:\s*1e400')
        document = json.loads(text)
        self.assertEqual("保留我", document["mcp"]["personal"]["headers"]["X-CUSTOM"])
        self.assertEqual(
            {"说明": [None, 9007199254740991, "保持原样"]},
            document["unknown"],
        )

    def test_sync_preserves_personal_opencode_settings(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        output_path = self.project_root / ".opencode/opencode.json"
        document = json.loads(output_path.read_text(encoding="utf-8"))
        document["provider"] = {"local": {"options": {"label": "保留"}}}
        document["mcp"]["personal-server"] = {
            "type": "remote",
            "url": "http://127.0.0.1:9999/mcp",
        }
        output_path.write_text(
            json.dumps(document, ensure_ascii=False, indent=2), encoding="utf-8"
        )
        self.write_source("http://127.0.0.1:8082/mcp")

        result = self.run_sync()

        self.assertEqual(0, result.returncode, result.stderr)
        updated = json.loads(output_path.read_text(encoding="utf-8"))
        self.assertEqual({"local": {"options": {"label": "保留"}}}, updated["provider"])
        self.assertEqual(
            "http://127.0.0.1:9999/mcp",
            updated["mcp"]["personal-server"]["url"],
        )
        self.assertEqual(
            "http://127.0.0.1:8082/mcp", updated["mcp"]["unity-MCP"]["url"]
        )

    def test_existing_mimocode_config_rejects_wrong_case_managed_members(self):
        output_path = self.project_root / ".mimocode/mimocode.json"
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(
            '{"MCP":{"unity-MCP":{"type":"remote","url":"http://127.0.0.1:8081/mcp"}}}',
            encoding="utf-8",
        )
        before = self.snapshot_tree()

        result = self.run_sync()

        self.assertNotEqual(0, result.returncode)
        self.assertEqual(before, self.snapshot_tree())

    def test_source_json_is_exact_case_type_safe_and_duplicate_safe(self):
        source_path = self.project_root / ".agents/mcp.json"
        invalid_documents = {
            "wrong-case": '{"McpServers":{"unityMCP":{"url":"http://127.0.0.1:8081/mcp"}}}',
            "wrong-url-case": '{"mcpServers":{"unityMCP":{"URL":"http://127.0.0.1:8081/mcp"}}}',
            "wrong-root-type": "[]",
            "unicode-whitespace": '{\u00a0"mcpServers":{}}',
            "wrong-url-type": '{"mcpServers":{"unityMCP":{"url":8081}}}',
            "duplicate": '{"mcpServers":{"unityMCP":{"url":"http://127.0.0.1:8081/mcp","url":"http://127.0.0.1:8082/mcp"}}}',
            "decoded-duplicate": '{"mcpServers":{"unityMCP":{"url":"http://127.0.0.1:8081/mcp","u\\u0072l":"http://127.0.0.1:8082/mcp"}}}',
            "case-collision": '{"mcpServers":{"unityMCP":{"url":"http://127.0.0.1:8081/mcp","Url":"http://127.0.0.1:8082/mcp"}}}',
        }
        for name, content in invalid_documents.items():
            with self.subTest(name=name):
                source_path.write_text(content, encoding="utf-8")
                before = self.snapshot_tree()
                result = self.run_sync()
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(before, self.snapshot_tree())

    def test_invalid_utf8_is_rejected_without_side_effects(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        mimocode_path = self.project_root / ".mimocode/mimocode.json"
        mimocode_path.write_bytes(mimocode_path.read_bytes().replace(b"{", b"{\xff", 1))
        before = self.snapshot_tree()

        result = self.run_sync()

        self.assertNotEqual(0, result.returncode)
        self.assertIn("Invalid UTF-8", result.stdout + result.stderr)
        self.assertEqual(before, self.snapshot_tree())

    def test_mimocode_template_rejects_wrong_case_type_duplicate_and_placeholder(self):
        path = self.project_root / ".mimocode/mimocode.template.json"
        original = path.read_text(encoding="utf-8")
        invalid_templates = {
            "wrong-case": original.replace('"mcp"', '"MCP"', 1),
            "timeout-string": original.replace("300000", '"300000"', 1),
            "wrapped-placeholder": original.replace(
                '"__UNITY_MCP_URL__"',
                '"https://example.invalid/?endpoint=__UNITY_MCP_URL__"',
                1,
            ),
            "missing-placeholder": original.replace(
                "__UNITY_MCP_URL__", "http://127.0.0.1:8080/mcp", 1
            ),
            "duplicate-key": original.replace(
                '"type": "remote",', '"type": "remote",\n      "type": "local",', 1
            ),
            "case-collision": original.replace(
                '"type": "remote",', '"type": "remote",\n      "Type": "local",', 1
            ),
            "personal-field": original.replace(
                '"unity-MCP": {',
                '"personal": {"type":"remote","url":"https://example.invalid"},\n    "unity-MCP": {',
                1,
            ),
        }
        for name, content in invalid_templates.items():
            with self.subTest(name=name):
                path.write_text(content, encoding="utf-8")
                before = self.snapshot_tree()
                result = self.run_sync()
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(before, self.snapshot_tree())
                path.write_text(original, encoding="utf-8")

    def test_opencode_template_requires_exact_path_and_strict_json(self):
        path = self.project_root / ".opencode/opencode.template.json"
        original = path.read_text(encoding="utf-8")
        invalid_templates = {
            "wrong-key-case": original.replace('"mcp"', '"MCP"', 1),
            "wrapped-placeholder": original.replace(
                '"__UNITY_MCP_URL__"', '"prefix-__UNITY_MCP_URL__"', 1
            ),
            "wrong-path": original.replace(
                '"url": "__UNITY_MCP_URL__"',
                '"url": "http://127.0.0.1:8080/mcp", "note": "__UNITY_MCP_URL__"',
                1,
            ),
            "duplicate-key": original.replace(
                '"type": "remote",', '"type": "remote", "type": "local",', 1
            ),
            "malformed": original[:-2],
        }
        for name, content in invalid_templates.items():
            with self.subTest(name=name):
                path.write_text(content, encoding="utf-8")
                before = self.snapshot_tree()
                result = self.run_sync()
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(before, self.snapshot_tree())
                path.write_text(original, encoding="utf-8")

    def test_codex_template_requires_exact_allowlisted_toml(self):
        path = self.project_root / ".codex/config.template.toml"
        original = path.read_text(encoding="utf-8")
        invalid_templates = {
            "placeholder-in-comment": original.replace(
                'url = "__UNITY_MCP_URL__"',
                'url = "http://127.0.0.1:8080/mcp"\n# __UNITY_MCP_URL__',
                1,
            ),
            "wrapped-placeholder": original.replace(
                '"__UNITY_MCP_URL__"', '"prefix-__UNITY_MCP_URL__"', 1
            ),
            "duplicate-key": original.replace(
                'url = "__UNITY_MCP_URL__"',
                'url = "__UNITY_MCP_URL__"\nurl = "http://127.0.0.1:8080/mcp"',
                1,
            ),
            "wrong-key-case": original.replace(
                'url = "__UNITY_MCP_URL__"', 'URL = "__UNITY_MCP_URL__"', 1
            ),
            "unicode-whitespace": original.replace(
                "rmcp_client = true", "rmcp_client\u00a0=\u00a0true", 1
            ),
            "duplicate-table": original + "\n[mcp_servers.unityMCP]\n",
            "unknown-section": original + "\n[unknown]\nvalue = true\n",
            "malformed-string": original.replace(
                'url = "__UNITY_MCP_URL__"', 'url = "__UNITY_MCP_URL__', 1
            ),
        }
        for name, content in invalid_templates.items():
            with self.subTest(name=name):
                path.write_text(content, encoding="utf-8")
                before = self.snapshot_tree()
                result = self.run_sync()
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(before, self.snapshot_tree())
                path.write_text(original, encoding="utf-8")

    def test_check_is_zero_side_effect_when_current_stale_or_invalid(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        owned_residual = self.project_root / (
            ".mimocode/mimocode.json.123.0123456789abcdef0123456789abcdef.bak"
        )
        owned_residual.write_text("recovery-bytes", encoding="utf-8")

        cases = ("current", "stale", "invalid-source")
        for case in cases:
            with self.subTest(case=case):
                if case == "stale":
                    (self.project_root / ".mimocode/mimocode.json").write_text(
                        "{}", encoding="utf-8"
                    )
                elif case == "invalid-source":
                    (self.project_root / ".agents/mcp.json").write_text(
                        "{invalid", encoding="utf-8"
                    )
                before = self.snapshot_tree()
                result = self.run_sync("--check")
                if case == "current":
                    self.assertEqual(0, result.returncode, result.stderr)
                else:
                    self.assertNotEqual(0, result.returncode)
                self.assertEqual(before, self.snapshot_tree())
                self.write_source("http://127.0.0.1:8081/mcp")
                if case == "stale":
                    repaired = self.run_sync()
                    self.assertEqual(0, repaired.returncode, repaired.stderr)

    def test_check_does_not_create_lock_directory_when_missing(self):
        library_path = self.project_root / "Library"
        self.assertFalse(library_path.exists())
        before = self.snapshot_tree()

        result = self.run_sync("--check")

        self.assertNotEqual(0, result.returncode)
        self.assertEqual(before, self.snapshot_tree())
        self.assertFalse(library_path.exists())

    def test_case_only_output_drift_is_stale_and_repaired(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        output_path = self.project_root / ".opencode/opencode.json"
        output_path.write_text(
            output_path.read_text(encoding="utf-8").replace('"remote"', '"REMOTE"'),
            encoding="utf-8",
        )

        stale = self.run_sync("--check")
        self.assertNotEqual(0, stale.returncode)
        repaired = self.run_sync()

        self.assertEqual(0, repaired.returncode, repaired.stderr)
        self.assertIn('"remote"', output_path.read_text(encoding="utf-8"))

    def test_mutating_sync_removes_only_strict_tool_owned_sidecars(self):
        owned = (
            Path(".agents/mcp.json.123.0123456789abcdef0123456789abcdef.tmp"),
            Path(".agents/mcp.local.json.4.abcdefabcdefabcdefabcdefabcdefab.bak"),
            Path(".codex/config.toml.9.0123456789abcdef0123456789abcdef.tmp"),
            Path(".opencode/opencode.json.88.abcdefabcdefabcdefabcdefabcdefab.bak"),
            Path(".mimocode/mimocode.json.777.0123456789abcdef0123456789abcdef.tmp"),
        )
        preserved = (
            Path(".agents/mcp.json.manual.bak"),
            Path(".agents/mcp.JSON.1.0123456789abcdef0123456789abcdef.tmp"),
            Path(".agents/mcp.json.x.0123456789abcdef0123456789abcdef.tmp"),
            Path(".codex/config.toml.1.aaaaaaaa.tmp"),
            Path(".opencode/opencode.json.1.0123456789abcdef0123456789abcdef.tmp.extra"),
            Path(".mimocode/mimocode.json.1.0123456789abcdef0123456789abcdeg.bak"),
        )
        for path in (*owned, *preserved):
            target = self.project_root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text("sidecar", encoding="utf-8")

        result = self.run_sync()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(all(not (self.project_root / path).exists() for path in owned))
        self.assertTrue(all((self.project_root / path).exists() for path in preserved))

    def test_transaction_rolls_back_every_operation_position(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        baseline_tree = self.snapshot_bytes_tree()

        for fail_after in range(1, 5):
            with self.subTest(fail_after=fail_after):
                environment = os.environ.copy()
                environment["TACTICS_TEST_MCP_CONFIG"] = "1"
                environment["TACTICS_TEST_MCP_CONFIG_FAIL_AFTER_OPERATIONS"] = str(
                    fail_after
                )
                result = self.run_initialize(
                    "-Url",
                    "http://127.0.0.1:8082/mcp",
                    environment=environment,
                )
                self.assert_injected_failure(result, fail_after)
                self.assertEqual(baseline_tree, self.snapshot_bytes_tree())

    def test_transaction_restores_absent_and_present_paths(self):
        baseline = self.snapshot_bytes_tree()
        environment = os.environ.copy()
        environment["TACTICS_TEST_MCP_CONFIG"] = "1"
        environment["TACTICS_TEST_MCP_CONFIG_FAIL_AFTER_OPERATIONS"] = "2"

        result = self.run_initialize(
            "-Url", "http://127.0.0.1:8082/mcp", environment=environment
        )

        self.assert_injected_failure(result, 2)
        self.assertEqual(baseline, self.snapshot_bytes_tree())

    def test_transaction_rejects_non_file_targets_without_deleting_them(self):
        target = self.project_root / ".codex/config.toml"
        owned_sidecar = self.project_root / (
            ".codex/config.toml.1.0123456789abcdef0123456789abcdef.tmp"
        )
        for with_child in (False, True):
            with self.subTest(with_child=with_child):
                target.mkdir(parents=True)
                if with_child:
                    (target / "keep.txt").write_text("keep", encoding="utf-8")
                owned_sidecar.write_text("must-not-clean", encoding="utf-8")
                baseline = self.snapshot_bytes_tree()

                result = self.run_initialize(
                    "-Url", "http://127.0.0.1:8082/mcp"
                )

                self.assertNotEqual(0, result.returncode)
                self.assertIn(
                    "Managed MCP configuration target exists but is not a file",
                    result.stdout + result.stderr,
                )
                self.assertEqual(baseline, self.snapshot_bytes_tree())
                if with_child:
                    (target / "keep.txt").unlink()
                target.rmdir()
                owned_sidecar.unlink()

    def test_prepare_preflights_every_cleanup_target(self):
        target = self.project_root / ".codex/config.toml"
        target.mkdir(parents=True)
        owned_sidecar = self.project_root / (
            ".codex/config.toml.1.0123456789abcdef0123456789abcdef.tmp"
        )
        owned_sidecar.write_text("must-not-clean", encoding="utf-8")
        baseline = self.snapshot_bytes_tree()

        result = self.run_initialize(
            "-PrepareMigration", "-Url", "http://127.0.0.1:8082/mcp"
        )

        self.assertNotEqual(0, result.returncode)
        self.assertIn(
            "Managed MCP configuration target exists but is not a file",
            result.stdout + result.stderr,
        )
        self.assertEqual(baseline, self.snapshot_bytes_tree())

    def test_prepare_transaction_rolls_back_its_only_operation(self):
        baseline = self.snapshot_bytes_tree()
        environment = os.environ.copy()
        environment["TACTICS_TEST_MCP_CONFIG"] = "1"
        environment["TACTICS_TEST_MCP_CONFIG_FAIL_AFTER_OPERATIONS"] = "1"

        result = self.run_initialize(
            "-PrepareMigration",
            "-Url",
            "http://127.0.0.1:8082/mcp",
            environment=environment,
        )

        self.assert_injected_failure(result, 1)
        self.assertEqual(baseline, self.snapshot_bytes_tree())

    def test_invalid_or_out_of_range_fault_injection_is_zero_side_effect(self):
        for value in ("invalid", "0", "999"):
            with self.subTest(value=value):
                environment = os.environ.copy()
                environment["TACTICS_TEST_MCP_CONFIG"] = "1"
                environment["TACTICS_TEST_MCP_CONFIG_FAIL_AFTER_OPERATIONS"] = value
                before = self.snapshot_tree()
                result = self.run_initialize(
                    "-Url",
                    "http://127.0.0.1:8082/mcp",
                    environment=environment,
                )
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(before, self.snapshot_tree())

    def test_prepare_and_restore_migration_are_one_shot_and_consistent(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        mimocode_path = self.project_root / ".mimocode/mimocode.json"
        mimocode = json.loads(mimocode_path.read_text(encoding="utf-8"))
        mimocode["personal"] = {"name": "本地保留"}
        migration_mimocode = json.dumps(mimocode, ensure_ascii=False, indent=2)
        migration_mimocode = migration_mimocode[:-1] + ', "huge": 1e400}'
        mimocode_path.write_text(migration_mimocode, encoding="utf-8")
        opencode_path = self.project_root / ".opencode/opencode.json"
        opencode = json.loads(opencode_path.read_text(encoding="utf-8"))
        opencode["provider"] = {"personal": {"enabled": True}}
        opencode_path.write_text(
            json.dumps(opencode, ensure_ascii=False, indent=2), encoding="utf-8"
        )
        prepared = self.run_initialize(
            "-PrepareMigration", "-Url", "http://127.0.0.1:8082/mcp"
        )
        self.assertEqual(0, prepared.returncode, prepared.stderr)
        backup_path = self.project_root / ".agents/mcp.local.json"
        self.assertTrue(backup_path.exists())
        backup_bytes = backup_path.read_bytes()
        self.assertFalse(backup_bytes.startswith(b"\xef\xbb\xbf"))
        backup_bytes.decode("utf-8", errors="strict")
        self.assertEqual(
            "http://127.0.0.1:8081/mcp",
            json.loads(
                (self.project_root / ".agents/mcp.json").read_text(encoding="utf-8")
            )["mcpServers"]["unityMCP"]["url"],
        )
        mimocode_path.unlink()
        opencode_path.unlink()

        restored = self.run_initialize("-RestoreMigration")

        self.assertEqual(0, restored.returncode, restored.stderr)
        self.assertFalse(backup_path.exists())
        self.assertEqual(
            {"http://127.0.0.1:8082/mcp"}, set(self.read_all_endpoints())
        )
        self.assertEqual(
            {"name": "本地保留"},
            json.loads(mimocode_path.read_text(encoding="utf-8"))["personal"],
        )
        self.assertRegex(
            mimocode_path.read_text(encoding="utf-8"), r'"huge"\s*:\s*1e400'
        )
        self.assertEqual(
            {"personal": {"enabled": True}},
            json.loads(opencode_path.read_text(encoding="utf-8"))["provider"],
        )

    def test_internal_operation_failure_rolls_back_current_path(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        baseline = self.snapshot_bytes_tree()
        write_environment = os.environ.copy()
        write_environment["TACTICS_TEST_MCP_CONFIG"] = "1"
        write_environment["TACTICS_TEST_MCP_CONFIG_FAIL_DURING_OPERATION"] = (
            "WriteText"
        )
        write_result = self.run_initialize(
            "-Url",
            "http://127.0.0.1:8082/mcp",
            environment=write_environment,
        )
        self.assertNotEqual(0, write_result.returncode)
        self.assertIn(
            "Injected MCP configuration failure during WriteText operation.",
            write_result.stdout + write_result.stderr,
        )
        self.assertEqual(baseline, self.snapshot_bytes_tree())

        prepared = self.run_initialize(
            "-PrepareMigration", "-Url", "http://127.0.0.1:8082/mcp"
        )
        self.assertEqual(0, prepared.returncode, prepared.stderr)
        baseline = self.snapshot_bytes_tree()
        delete_environment = os.environ.copy()
        delete_environment["TACTICS_TEST_MCP_CONFIG"] = "1"
        delete_environment["TACTICS_TEST_MCP_CONFIG_FAIL_DURING_OPERATION"] = "Delete"
        delete_result = self.run_initialize(
            "-RestoreMigration", environment=delete_environment
        )
        self.assertNotEqual(0, delete_result.returncode)
        self.assertIn(
            "Injected MCP configuration failure during Delete operation.",
            delete_result.stdout + delete_result.stderr,
        )
        self.assertEqual(baseline, self.snapshot_bytes_tree())

    def test_restore_transaction_rolls_back_backup_delete(self):
        prepared = self.run_initialize(
            "-PrepareMigration", "-Url", "http://127.0.0.1:8082/mcp"
        )
        self.assertEqual(0, prepared.returncode, prepared.stderr)
        baseline = self.snapshot_bytes_tree()
        for fail_after in range(1, 6):
            with self.subTest(fail_after=fail_after):
                environment = os.environ.copy()
                environment["TACTICS_TEST_MCP_CONFIG"] = "1"
                environment["TACTICS_TEST_MCP_CONFIG_FAIL_AFTER_OPERATIONS"] = str(
                    fail_after
                )
                result = self.run_initialize(
                    "-RestoreMigration", environment=environment
                )
                self.assert_injected_failure(result, fail_after)
                self.assertEqual(baseline, self.snapshot_bytes_tree())

    def test_prepare_and_restore_share_the_cross_process_lock(self):
        anchor = self.project_root / LOCK_ANCHOR
        escaped_anchor = str(anchor).replace("'", "''")
        holder_command = (
            f"$stream=[System.IO.File]::Open('{escaped_anchor}',"
            "[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,"
            "[System.IO.FileShare]::None);"
            "[Console]::Out.WriteLine('READY');[Console]::Out.Flush();"
            "Start-Sleep -Seconds 30"
        )

        def hold_lock():
            process = subprocess.Popen(
                ["powershell.exe", "-NoProfile", "-Command", holder_command],
                cwd=self.project_root,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            self.assertEqual("READY", process.stdout.readline().strip())
            return process

        holder = hold_lock()
        try:
            blocked_prepare = self.run_initialize(
                "-PrepareMigration", "-Url", "http://127.0.0.1:8082/mcp"
            )
            self.assertNotEqual(0, blocked_prepare.returncode)
            self.assertFalse((self.project_root / ".agents/mcp.local.json").exists())
        finally:
            holder.terminate()
            holder.wait(timeout=10)
            holder.stdout.close()
            holder.stderr.close()

        prepared = self.run_initialize(
            "-PrepareMigration", "-Url", "http://127.0.0.1:8082/mcp"
        )
        self.assertEqual(0, prepared.returncode, prepared.stderr)
        before_restore = self.managed_snapshot()

        holder = hold_lock()
        try:
            blocked_restore = self.run_initialize("-RestoreMigration")
            self.assertNotEqual(0, blocked_restore.returncode)
            self.assert_managed_snapshot(before_restore)
        finally:
            holder.terminate()
            holder.wait(timeout=10)
            holder.stdout.close()
            holder.stderr.close()

        restored = self.run_initialize("-RestoreMigration")
        self.assertEqual(0, restored.returncode, restored.stderr)

    def test_lock_is_held_through_the_entire_transaction(self):
        initial = self.run_sync()
        self.assertEqual(0, initial.returncode, initial.stderr)
        marker = self.project_root / "transaction-barrier.ready"
        release = self.project_root / "transaction-barrier.release"
        first_environment = os.environ.copy()
        first_environment["TACTICS_TEST_MCP_CONFIG"] = "1"
        first_environment["TACTICS_TEST_MCP_CONFIG_BARRIER_AFTER_OPERATION"] = "1"
        first_environment["TACTICS_TEST_MCP_CONFIG_BARRIER_MARKER"] = str(marker)
        first_environment["TACTICS_TEST_MCP_CONFIG_BARRIER_RELEASE"] = str(release)
        first = subprocess.Popen(
            [
                "powershell.exe",
                "-NoProfile",
                "-File",
                str(self.project_root / INITIALIZE_SCRIPT),
                "-Url",
                "http://127.0.0.1:8082/mcp",
            ],
            cwd=self.project_root,
            env=first_environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        second = None
        try:
            deadline = time.monotonic() + 10
            while not marker.exists() and time.monotonic() < deadline:
                self.assertIsNone(first.poll())
                time.sleep(0.05)
            self.assertTrue(marker.exists())
            lock_attempt = self.project_root / "second-process-lock-attempt.ready"
            second_environment = os.environ.copy()
            second_environment["TACTICS_TEST_MCP_CONFIG"] = "1"
            second_environment["TACTICS_TEST_MCP_CONFIG_LOCK_ATTEMPT_MARKER"] = str(
                lock_attempt
            )
            second = subprocess.Popen(
                [
                    "powershell.exe",
                    "-NoProfile",
                    "-File",
                    str(self.project_root / INITIALIZE_SCRIPT),
                    "-Url",
                    "http://127.0.0.1:8083/mcp",
                ],
                cwd=self.project_root,
                env=second_environment,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            deadline = time.monotonic() + 10
            while not lock_attempt.exists() and time.monotonic() < deadline:
                self.assertIsNone(second.poll())
                time.sleep(0.05)
            self.assertTrue(lock_attempt.exists())
            with self.assertRaises(subprocess.TimeoutExpired):
                second.communicate(timeout=1)
            release.write_text("release", encoding="ascii")
            first_stdout, first_stderr = first.communicate(timeout=20)
            second_stdout, second_stderr = second.communicate(timeout=20)
            self.assertEqual(0, first.returncode, first_stdout + first_stderr)
            self.assertEqual(0, second.returncode, second_stdout + second_stderr)
            self.assertEqual(
                {"http://127.0.0.1:8083/mcp"}, set(self.read_all_endpoints())
            )
        finally:
            release.write_text("release", encoding="ascii")
            for process in (first, second):
                if process is not None and process.poll() is None:
                    process.kill()
                    process.communicate(timeout=10)

    def test_invalid_migration_backup_changes_nothing(self):
        backup_path = self.project_root / ".agents/mcp.local.json"
        backup_path.write_text('{"McpServers":{}}', encoding="utf-8")
        before = self.snapshot_tree()

        result = self.run_initialize("-RestoreMigration")

        self.assertNotEqual(0, result.returncode)
        self.assertEqual(before, self.snapshot_tree())

    def test_concurrent_initializers_leave_all_endpoints_consistent(self):
        processes = []
        try:
            for port in (8082, 8083):
                processes.append(
                    subprocess.Popen(
                        [
                            "powershell.exe",
                            "-NoProfile",
                            "-File",
                            str(self.project_root / INITIALIZE_SCRIPT),
                            "-Url",
                            f"http://127.0.0.1:{port}/mcp",
                        ],
                        cwd=self.project_root,
                        stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE,
                        text=True,
                    )
                )
            results = [process.communicate(timeout=30) for process in processes]
        finally:
            for process in processes:
                if process.poll() is None:
                    process.kill()
                    process.communicate()
        for process, (_, stderr) in zip(processes, results):
            self.assertEqual(0, process.returncode, stderr)
        endpoints = self.read_all_endpoints()
        self.assertEqual({endpoints[0]}, set(endpoints))

    def test_generated_paths_are_ignored_with_sidecars(self):
        candidates = (
            ".agents/mcp.json",
            ".agents/mcp.local.json",
            ".codex/config.toml",
            ".opencode/opencode.json",
            ".mimocode/mimocode.json",
            ".agents/mcp.json.1.0123456789abcdef0123456789abcdef.tmp",
            ".mimocode/mimocode.json.1.0123456789abcdef0123456789abcdef.bak",
        )
        ignored = subprocess.run(
            ["git", "check-ignore", "--no-index", *candidates],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, ignored.returncode, ignored.stderr)
        self.assertEqual(
            set(candidates),
            {line.replace("\\", "/") for line in ignored.stdout.splitlines()},
        )

    def test_real_index_tracks_phase_a_sources_not_generated_configs(self):
        tracked = subprocess.run(
            ["git", "ls-files"],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=True,
        ).stdout.splitlines()
        expected_tracked = (
            ".gitignore",
            ".mimocode/mimocode.template.json",
            "Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1",
            "Tools/unity-mcp/ProjectMcpConfig.lock-anchor",
            "Tools/unity-mcp/README.md",
            "Tools/unity-mcp/Sync-ProjectMcpConfig.ps1",
            "Tools/unity-mcp/test_sync_project_mcp_config.py",
        )
        for path in expected_tracked:
            self.assertIn(path, tracked)
        for path in (
            ".agents/mcp.json",
            ".agents/mcp.local.json",
            ".codex/config.toml",
            ".opencode/opencode.json",
            ".mimocode/mimocode.json",
        ):
            self.assertNotIn(path, tracked)

    def test_windows_powershell_compatibility_suite_runs_on_version_5(self):
        version = subprocess.run(
            [
                "powershell.exe",
                "-NoProfile",
                "-Command",
                "$PSVersionTable.PSVersion.Major",
            ],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, version.returncode, version.stderr)
        self.assertEqual("5", version.stdout.strip())

    def test_initializer_rejects_user_info_without_echoing_it(self):
        before = self.snapshot_tree()
        test_user = "local-" + "user"
        test_password = "local-" + "pass"
        endpoint = (
            "http://" + test_user + ":" + test_password + "@127.0.0.1:8082/mcp"
        )
        result = self.run_initialize("-Url", endpoint)
        self.assertNotEqual(0, result.returncode)
        output = result.stdout + result.stderr
        self.assertNotIn(test_user, output)
        self.assertNotIn(test_password, output)
        self.assertEqual(before, self.snapshot_tree())

    def test_tracked_templates_contain_no_credential_fields(self):
        for relative_path in TEMPLATE_PATHS:
            with self.subTest(path=relative_path.as_posix()):
                template = (self.project_root / relative_path).read_text(
                    encoding="utf-8"
                ).lower()
                for forbidden in (
                    "api_key",
                    "apikey",
                    "token",
                    "secret",
                    "password",
                    "headers",
                    "authorization",
                ):
                    self.assertNotIn(forbidden, template)


if __name__ == "__main__":
    unittest.main()
