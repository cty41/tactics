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
