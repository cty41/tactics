---
name: unity-mcp-advanced
description: "Use when running Unity tests, profiling, debugging, managing builds, editing scripts, or executing C# code via MCP tools"
---

# unity-mcp-advanced

Advanced Unity MCP operations: scripts, code execution, editor control, builds, packages, profiling, testing, and utilities.

> **Prerequisite:** Read `unity-mcp-core` first for foundational rules and common operations.

## Quick Reference

| 操作 | 工具 | 关键参数 |
|------|------|---------|
| 创建脚本 | `create_script` | `path`, `contents` |
| 修改脚本 | `apply_text_edits` / `script_apply_edits` | `uri`, `edits` |
| Play Mode | `manage_editor` | `action="play"` / `"stop"` / `"pause"` |
| 运行测试 | `run_tests` | `mode="EditMode"` 或 `"PlayMode"` |
| 构建 | `manage_build` | `action="build"`, `target` |
| 执行代码 | `execute_code` | `action="execute"`, `code` |
| 刷新编译 | `refresh_unity` | `compile="request"` |

## When to use

- 需要创建、修改、验证或编译 C# 脚本时
- 需要运行 Unity EditMode/PlayMode 测试或读取测试结果时
- 需要管理构建、包、Profiler、Frame Debugger 或 Console 时
- 只有在用户明确要求或确有必要时才使用 `execute_code`

## Tool Tables

### Script Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `create_script` | — | Create new C# script |
| `delete_script` | — | Delete C# script |
| `validate_script` | — | Validate C# script |
| `apply_text_edits` | — | Apply raw text edits (range-based) |
| `script_apply_edits` | — | Apply structured edits (method/class) |
| `get_sha` | — | Get script SHA256 |
| `manage_scriptable_object` | create | Create ScriptableObject asset |
| `manage_scriptable_object` | modify | Modify ScriptableObject asset |

### Code Execution

| Tool | Action | Purpose |
|------|--------|---------|
| `execute_code` | execute | Execute arbitrary C# in Unity Editor |
| `batch_execute` | — | Execute multiple MCP commands in batch |

### Editor Control

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_editor` | play | Enter Play Mode |
| `manage_editor` | stop | Exit Play Mode |
| `manage_editor` | pause | Pause Play Mode |
| `manage_editor` | undo | Perform Undo |
| `manage_editor` | redo | Perform Redo |
| `manage_editor` | set_active_tool | Set active editor tool |

### Build Operations

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_build` | build | Trigger player build |
| `manage_build` | settings | Configure build settings |
| `manage_build` | scenes | Manage build scenes |
| `manage_build` | profiles | Manage build profiles |

### Package Management

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_packages` | add_package | Install package |
| `manage_packages` | remove_package | Remove package |
| `manage_packages` | list_packages | List installed packages |
| `manage_packages` | search_packages | Search registry |

### Profiler & Debugging

| Tool | Action | Purpose |
|------|--------|---------|
| `manage_profiler` | profiler_start | Start profiler |
| `manage_profiler` | get_counters | Read profiler counters |
| `manage_profiler` | memory_take_snapshot | Capture memory snapshot |
| `manage_profiler` | frame_debugger_enable | Enable Frame Debugger |

### Console & Testing

| Tool | Action | Purpose |
|------|--------|---------|
| `read_console` | get | Retrieve Unity Editor console logs |
| `read_console` | clear | Clear console logs |
| `run_tests` | — | Run Unity tests (EditMode/PlayMode) |
| `get_test_job` | — | Poll test run status |

### Documentation & Reflection

| Tool | Action | Purpose |
|------|--------|---------|
| `unity_docs` | get_doc | Fetch Unity ScriptReference docs |
| `unity_docs` | lookup | Search Unity docs |
| `unity_reflect` | get_type | Reflect C# type members |
| `unity_reflect` | search | Search types across assemblies |

### Utilities

| Tool | Action | Purpose |
|------|--------|---------|
| `refresh_unity` | — | Refresh AssetDatabase and optionally compile |
| `find_in_file` | — | Search file with regex |
| `manage_tools` | list_groups | List available tool groups |
| `execute_menu_item` | — | Execute Unity menu item |
| `debug_request_context` | — | Get MCP request context |

## Workflow

### 1. Script Iteration
```
create_script → validate_script → refresh_unity (compile=request)
```
1. Create or edit script.
2. Validate with `validate_script`.
3. Compile with `refresh_unity` (compile="request").

### 2. VFX Control
```
find_gameobjects → manage_vfx (particle_play / particle_stop / line_set_positions)
```
1. Locate VFX GameObject.
2. Control playback, modify parameters via `manage_vfx`.

## Best Practices

- **execute_code safety**: The `safety_checks` flag blocks known dangerous patterns (File.Delete, Process.Start, infinite loops) but is not a full sandbox. Only execute trusted code.
- **Script iteration**: Always call `refresh_unity` with `compile="request"` after C# file changes.
- **Asset refresh**: Use `refresh_unity` after external asset changes to sync the AssetDatabase.
- **Console monitoring**: Use `read_console` to check for errors after operations. Filter by `types=["error","warning"]`.
- **Test workflow**: `run_tests` (async) → `get_test_job` (poll) — tests run asynchronously.
- **Development-only batched test helper**: `Tools/unity-mcp/Manage-UnityTestGate.ps1` is a local draft helper, not a mandatory `run_tests` prerequisite or a CI/release authority. When used, group all related names into at most one targeted job per mode; `-Next` reserves the single worktree test slot, and its reservation must be passed to `-RecordStart` immediately after receiving the MCP job ID. Whether tests are started directly or through the helper, one MCP timeout does not authorize a duplicate job.
- **Final coverage**: Run one unfiltered EditMode job and one unfiltered PlayMode job. Keep `[Explicit]` profiler and benchmark tests in separate exact-name gates. Never start both modes concurrently or use `batch_execute` as a test batching mechanism.
- **Timeout units**: `run_tests.init_timeout=120000` is milliseconds; `get_test_job.wait_timeout=30` is seconds. The project MiMoCode MCP timeout is a separate 300000-millisecond client budget.

## Anti-patterns

| Wrong | Correct | Why |
|-------|---------|-----|
| Editing C# and skipping compile | `validate_script` then `refresh_unity(compile="request")` | Catches compile errors immediately |
| Using `execute_code` for routine edits | Use script edit tools | Keeps changes reviewable and persistent |
| Polling tests without wait timeout | Use `get_test_job(wait_timeout=30)` | Avoids tight polling loops |
| Running one `run_tests` per test name | Pass the gate's grouped `test_names` array | Avoids repeated dirty preflight, reload, and duplicate jobs |
| Retrying after a client timeout without the old job state | Query the gate's recorded job ID first | A timeout does not prove that Unity failed to create or finish the job |
| Retrying when `run_tests` timed out before returning an ID | Keep the reservation and stop until independent evidence proves no job exists | Without an ID, a retry can create an orphaned duplicate job |
| Ignoring console errors after operations | Read console errors/warnings | Unity failures often appear only in Console |

## Checklist

- [ ] `project-coding-reference` was used before writing C# code
- [ ] Script changes were validated with `validate_script`
- [ ] `.cs` changes triggered `refresh_unity(compile="request")`
- [ ] Tests or Console checks were run when behavior changed
- [ ] `execute_code` was only used with explicit need
