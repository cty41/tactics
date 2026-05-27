import hashlib
import json
import os
import re
import sys
import tempfile
from pathlib import Path


CS_PATCH_PATTERN = re.compile(r"^\*\*\* (?:Add|Update|Delete) File: (.+?\.cs)\s*$", re.MULTILINE)
WRITE_HINT_PATTERN = re.compile(
    r"(\.cs\b).*(Set-Content|Add-Content|Out-File|New-Item|Move-Item|Copy-Item|Remove-Item|rename|move|copy|del|rm|echo\s+.*>|>>)",
    re.IGNORECASE | re.DOTALL,
)


def load_payload() -> dict:
    raw = sys.stdin.read()
    if not raw.strip():
        return {}
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return {}


def get_repo_root(payload: dict) -> Path:
    current = Path(payload.get("cwd") or os.getcwd()).resolve()
    for candidate in [current, *current.parents]:
        if (candidate / ".git").exists():
            return candidate
    return current


def get_rule_text(repo_root: Path) -> str:
    rule_path = repo_root / ".agents" / "shared-rules" / "unity-auto-compile.md"
    try:
        return rule_path.read_text(encoding="utf-8").strip()
    except OSError:
        return (
            "If you changed any .cs file, call refresh_unity(compile=\"request\") "
            "before concluding the task."
        )


def get_state_path(repo_root: Path, session_id: str) -> Path:
    key = hashlib.sha1(f"{repo_root}|{session_id}".encode("utf-8")).hexdigest()
    state_dir = Path(tempfile.gettempdir()) / "codex-auto-compile"
    state_dir.mkdir(parents=True, exist_ok=True)
    return state_dir / f"{key}.json"


def load_state(path: Path) -> dict:
    if not path.is_file():
        return {
            "cs_dirty": False,
            "compiled_after_dirty": True,
            "changed_files": [],
        }
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {
            "cs_dirty": False,
            "compiled_after_dirty": True,
            "changed_files": [],
        }


def save_state(path: Path, state: dict) -> None:
    path.write_text(json.dumps(state, ensure_ascii=False), encoding="utf-8")


def detect_cs_edit(tool_name: str, tool_input: dict) -> list[str]:
    command = ""
    if isinstance(tool_input, dict):
        command = tool_input.get("command", "") or ""

    if tool_name == "apply_patch":
        return CS_PATCH_PATTERN.findall(command)

    if tool_name == "Bash":
        if WRITE_HINT_PATTERN.search(command):
            return sorted(set(re.findall(r"([^\s\"']+\.cs)\b", command, re.IGNORECASE)))

    return []


def is_compile_request(tool_name: str, tool_input: dict) -> bool:
    if not tool_name.endswith("refresh_unity"):
        return False
    if not isinstance(tool_input, dict):
        return False
    return str(tool_input.get("compile", "")).lower() == "request"


def emit_json(obj: dict) -> None:
    sys.stdout.write(json.dumps(obj, ensure_ascii=False))


def build_additional_context(rule_text: str, changed_files: list[str]) -> str:
    lines = [rule_text]
    if changed_files:
        lines.append("")
        lines.append("Changed C# files in this session state:")
        for item in changed_files:
            lines.append(f"- {item}")
    return "\n".join(lines)


def run_pre(payload: dict, state_path: Path, rule_text: str) -> int:
    tool_name = payload.get("tool_name", "")
    tool_input = payload.get("tool_input", {})
    changed_files = detect_cs_edit(tool_name, tool_input)
    if not changed_files:
        return 0

    state = load_state(state_path)
    context = build_additional_context(rule_text, state.get("changed_files", []))
    emit_json(
        {
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "additionalContext": context,
            }
        }
    )
    return 0


def run_post(payload: dict, state_path: Path, rule_text: str) -> int:
    tool_name = payload.get("tool_name", "")
    tool_input = payload.get("tool_input", {})
    state = load_state(state_path)

    changed_files = detect_cs_edit(tool_name, tool_input)
    if changed_files:
        merged = sorted(set(state.get("changed_files", []) + changed_files))
        state["cs_dirty"] = True
        state["compiled_after_dirty"] = False
        state["changed_files"] = merged
        save_state(state_path, state)
        emit_json(
            {
                "hookSpecificOutput": {
                    "hookEventName": "PostToolUse",
                    "additionalContext": build_additional_context(rule_text, merged),
                }
            }
        )
        return 0

    if is_compile_request(tool_name, tool_input) and state.get("cs_dirty"):
        state["compiled_after_dirty"] = True
        save_state(state_path, state)
        return 0

    return 0


def run_stop(payload: dict, state_path: Path) -> int:
    state = load_state(state_path)
    if not state.get("cs_dirty") or state.get("compiled_after_dirty"):
        return 0
    if payload.get("stop_hook_active"):
        return 0

    changed_files = state.get("changed_files", [])
    reason = "你本轮已经修改了 C# 脚本，但还没有调用 refresh_unity(compile=\"request\")。先触发 Unity 编译，再结束本轮任务。"
    if changed_files:
        reason += " 涉及文件: " + ", ".join(changed_files)

    emit_json(
        {
            "decision": "block",
            "reason": reason,
        }
    )
    return 0


def main() -> int:
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    payload = load_payload()
    repo_root = get_repo_root(payload)
    session_id = payload.get("session_id", "unknown-session")
    state_path = get_state_path(repo_root, session_id)
    rule_text = get_rule_text(repo_root)

    if mode == "pre":
        return run_pre(payload, state_path, rule_text)
    if mode == "post":
        return run_post(payload, state_path, rule_text)
    if mode == "stop":
        return run_stop(payload, state_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
