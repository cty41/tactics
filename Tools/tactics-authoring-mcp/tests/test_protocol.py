import json
import os
import pathlib
import subprocess
import tempfile
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
PROJECT = REPO_ROOT / "Tools" / "tactics-authoring-mcp" / "Tactics.Authoring.Mcp.csproj"
CONFIGURATION = os.environ.get("TACTICS_AUTHORING_MCP_CONFIGURATION", "Release")


class TacticsAuthoringMcpProtocolTests(unittest.TestCase):
    def run_server(self, requests, descriptors=()):
        with tempfile.TemporaryDirectory() as root_text:
            root = pathlib.Path(root_text)
            godot = root / "godot"
            session_dir = godot / ".godot"
            session_dir.mkdir(parents=True)
            (godot / "project.godot").write_text("; protocol fixture\n", encoding="utf-8")
            for index, descriptor in enumerate(descriptors):
                (session_dir / f"tactics-authoring-session-{index}.json").write_text(
                    json.dumps(descriptor), encoding="utf-8")
            process = subprocess.run(
                ["dotnet", "run", "--project", str(PROJECT), "-c", CONFIGURATION, "--no-build", "--", str(root)],
                input="".join(json.dumps(value) + "\n" for value in requests),
                text=True,
                capture_output=True,
                check=True,
                cwd=REPO_ROOT,
            )
            return [json.loads(line) for line in process.stdout.splitlines() if line.strip()]

    def test_initialize_notification_schema_and_tool_error(self):
        responses = self.run_server([
            {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-11-25"}},
            {"jsonrpc": "2.0", "method": "notifications/initialized"},
            {"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}},
            {"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": "tactics_authoring_list", "arguments": {}}},
        ])
        self.assertEqual([value["id"] for value in responses], [1, 2, 3])
        self.assertEqual(responses[0]["result"]["protocolVersion"], "2025-11-25")
        tools = {value["name"]: value for value in responses[1]["result"]["tools"]}
        self.assertEqual(len(tools), 6)
        self.assertIn("changes", tools["tactics_authoring_apply"]["inputSchema"]["properties"])
        lifecycle = tools["tactics_authoring_apply"]["inputSchema"]["properties"]["lifecycle"]
        self.assertEqual(lifecycle["items"]["properties"]["operation"]["enum"],
                         ["create", "duplicate", "delete"])
        self.assertIn("expectedReferenceRevision", lifecycle["items"]["properties"])
        preview_context = tools["tactics_authoring_preview"]["inputSchema"]["properties"]["context"]
        self.assertEqual(preview_context["properties"]["targetX"]["maximum"], 9)
        self.assertIn("encounterContentId", preview_context["required"])
        self.assertTrue(responses[2]["result"]["isError"])

    def test_unsupported_protocol_is_json_rpc_error(self):
        response = self.run_server([
            {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "1999-01-01"}}
        ])[0]
        self.assertEqual(response["error"]["code"], -32022)

    def test_reload_and_multiple_sessions_fail_closed(self):
        base = {"projectRoot": "wrong", "pipeName": "unused", "sessionToken": "unused", "state": "reloading"}
        request = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": "tactics_authoring_list", "arguments": {}}}
        reload_response = self.run_server([request], [base])[0]
        multiple_response = self.run_server([request], [base, base])[0]
        self.assertTrue(reload_response["result"]["isError"])
        self.assertTrue(multiple_response["result"]["isError"])


if __name__ == "__main__":
    unittest.main()
