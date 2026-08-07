# Worktree-local Unity MCP configuration

Unity MCP endpoints are local to each Git worktree. Git tracks templates, scripts, tests, and the configuration lock anchor, but ignores `.agents/mcp.json`, `.agents/mcp.local.json`, `.codex/config.toml`, `.opencode/opencode.json`, and `.mimocode/mimocode.json`.

The tracked MiMoCode template contains only project-safe plugin, LSP, and Unity MCP settings. Personal MCP credentials belong in user-global or otherwise ignored configuration. If a credential was previously committed, removing it from the current file does not revoke it; rotate the credential separately.

## Existing worktree migration

Before pulling the commit that removes the legacy tracked files, save the endpoint plus the complete current OpenCode and MiMoCode project JSON without changing those files:

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -PrepareMigration -Url http://127.0.0.1:8080/mcp
```

After pulling that commit, restore the ignored worktree-local files:

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -RestoreMigration
```

The migration backup is one-shot: restore merges backed-up personal OpenCode/MiMoCode fields with current project-owned settings, then deletes `.agents/mcp.local.json` in the same transaction. A failed restore preserves the backup and all managed files byte-for-byte.

Use `8080` for the main worktree and `8081` for W1. Other concurrent worktrees must use an unused loopback port.

## New worktree

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -Url http://127.0.0.1:8082/mcp
powershell.exe -File Tools/unity-mcp/Sync-ProjectMcpConfig.ps1 --check
```

Run the initializer again to change a worktree port. Run the sync command without `--check` after a template update to regenerate Codex, OpenCode, and MiMoCode configuration. MiMoCode receives a project-local request timeout of `300000` milliseconds; Codex and OpenCode timeout policy is unchanged.

Initialize, prepare, restore, and sync serialize on the tracked `ProjectMcpConfig.lock-anchor` with a cross-session `FileShare.None` lock. Before the first mutation, each operation strictly validates every input it consumes: sync validates source plus all templates and existing generated JSON; initialize validates its URL plus all templates and existing generated JSON; prepare validates its URL and local JSON being backed up; restore validates the backup, embedded local JSON, and all templates.

- JSON is strict UTF-8; project-owned keys are exact-case and type-checked, and all objects reject decoded duplicate or case-colliding keys.
- Codex templates use a project-owned TOML allowlist. OpenCode/MiMoCode templates and project-owned fields use strict JSON allowlists; unrelated valid local fields are preserved, including their original JSON number lexemes.
- `__UNITY_MCP_URL__` must appear exactly once as the complete managed URL value, and rendered output is validated again.

`--check` is read-only: it opens the existing lock anchor but does not create directories/files, clean residuals, or modify bytes/timestamps. Mutating operations clean only tool-owned `<target>.<positive-pid>.<32-hex-guid>.tmp|bak` residuals after preflight. Git ignore patterns are intentionally broader than this deletion grammar so crash artifacts or hand-created backups cannot be staged accidentally. Ordinary caught write/delete failures restore the original file bytes and existence; the source is committed after derived outputs, and restore deletes its migration backup last. A process hard-kill or power loss is not claimed to be cross-file atomic; ignored sidecars are recovered by the next successful mutating operation.

## Domain reload ownership

`UnityMcpProjectBootstrap` is intentionally a guarded no-op. It returns immediately in batch mode and Asset Import Worker processes; in an interactive main Editor it still does not read endpoint configuration, write `EditorPrefs` or `SessionState`, register callbacks, or start, stop, connect, verify, poll, or retry the MCP server or bridge. MCPForUnity and explicit user actions retain lifecycle ownership.

This safety boundary prevents project code from overriding a manual Disconnect or competing with package reload handling. It does not disable package-owned behavior, make machine-global package preferences worktree-safe, or guarantee reconnection after compilation, Play Mode transitions, or domain reload. If the bridge does not recover in the background, preserve any test job ID and ask the user to restore it manually; do not use foreground-window automation.

Current status (2026-08-07): automatic recovery remains `0/5`, `blocked_upstream`, and unverified. MCPForUnity 10.1.0 can still create concurrent reconnect continuations and evict sessions after a reload; 10.1.2 has no relevant source fix. A future automatic-recovery design requires a user-cancellable per-Editor owner and a new stable package/server that passes the source gate before the five-reload matrix is restarted.

The current stable 10.1.2 release does not modify the HTTP reload handler, WebSocket registered-message path, or tool discovery implementation relative to 10.1.0. It therefore does not satisfy this project's source gate and is not adopted as a reconnect fix. Future upgrades must use an exact stable Unity package and matching `mcpforunityserver` version, pass the relevant source diff, then pass the five-reload matrix. Forks and edits under `Library/PackageCache` are prohibited.

## Timeout layers

| Layer | Setting | Unit | Project guidance |
|---|---|---:|---:|
| MiMoCode MCP request | `mcp.unity-MCP.timeout` | milliseconds | `300000` |
| Test initialization | `run_tests.init_timeout` | milliseconds | `120000` |
| Test job long poll | `get_test_job.wait_timeout` | seconds | `30` |

The MiMoCode timeout prevents the client from abandoning a legitimate long request after its default five seconds. It does not change server command/readiness budgets, WebSocket heartbeat behavior, tool discovery, or bridge reconnection.

## Batched Unity test gates

`Manage-UnityTestGate.ps1` creates canonical `run_tests` payloads and records MCP job IDs/results under the ignored `Library/MCPForUnity/TestGates` directory. It does not implement MCP HTTP or call Unity itself.

Create one targeted gate with all related names grouped by mode:

```powershell
$gate = & Tools/unity-mcp/Manage-UnityTestGate.ps1 `
    -Create -Scope Targeted `
    -EditModeTestName @('Namespace.EditorTestA', 'Namespace.EditorTestB') `
    -PlayModeTestName @('Namespace.PlayTestA') | ConvertFrom-Json
```

Use `-Next -GateId $gate.gateId` to atomically reserve the one project-wide test slot and retrieve the next canonical payload plus `reservationId`. After `run_tests` returns, bind its ID with `-RecordStart -ReservationId <id>`; after `get_test_job` reaches a terminal state, use `-RecordResult`. Finish with `-Validate`. A reserved/running job blocks every gate in the worktree, and a replacement reservation requires the failed job ID plus a supersede reason.

Rules:

- Compile once after the final C# edit.
- Use at most one targeted job per mode.
- Final regular coverage is one full EditMode job plus one full PlayMode job.
- `[Explicit]` profiler/benchmark tests use an `Explicit` gate and exact test names.
- If `run_tests` returns a job ID, record it immediately. A later client timeout does not prove the job failed; reconnect and query that ID.
- If `run_tests` itself times out before returning an ID, keep the reservation and stop. Only after independent evidence proves no job was created may `-CancelReservation` release it; do not retry blindly.
- Cancellation requires a non-whitespace evidence reason and persists the reservation ID, reason, and cancellation time in gate state.
- Every attempt persists the consumed reservation ID; idempotent `RecordStart` must replay both that reservation ID and the MCP job ID.
- Do not put two `run_tests` calls in `batch_execute`; Unity MCP permits only one active test job.
