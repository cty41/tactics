# Worktree-local Unity MCP configuration

Unity MCP endpoints are local to each Git worktree. Git tracks the templates and scripts in this directory, but ignores `.agents/mcp.json`, `.codex/config.toml`, and `.opencode/opencode.json`.

## Existing worktree migration

Before pulling the commit that removes the legacy tracked files, save the endpoint without changing the tracked configuration:

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -PrepareMigration -Url http://127.0.0.1:8080/mcp
```

After pulling that commit, restore the ignored worktree-local files:

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -RestoreMigration
```

Use `8080` for the main worktree and `8081` for W1. Other concurrent worktrees must use an unused loopback port.

## New worktree

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -Url http://127.0.0.1:8082/mcp
powershell.exe -File Tools/unity-mcp/Sync-ProjectMcpConfig.ps1 --check
```

Run the initializer again to change a worktree port. Run the sync command without `--check` after a template update to regenerate Codex and OpenCode configuration.
