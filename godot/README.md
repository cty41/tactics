# Tactics Godot 工程

这是迁移分支唯一的 Godot 工程。Godot Project Manager 必须打开本目录，而不是迁移 worktree 的上一级目录：

```text
D:\codes\tactics-worktrees\godot\godot
```

工程入口是 `project.godot`，编辑器插件位于 `addons/tactics_tooling`，迁移期 Godot 资产位于 `content`。

统一验证入口：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File ..\Tools\migration\Verify-GodotMigration.ps1
```

除非迁移计划或用户明确要求，不创建第二个 Godot worktree、第二个 `project.godot` 或独立能力 Spike；新的验证应加入本工程的 `tests` 或 `Tools/migration`。
