# [历史方案] Unity Auto Compile Guard Hook

> **状态：已停用**
>
> Codex 不再推荐使用 hook 路径实现自动编译约束。当前方案改为纯 skill 约束，详见：
> - `.agents/skills/unity-auto-compile-guard/SKILL.md`
> - `.agents/shared-rules/unity-auto-compile.md`

## 停用原因

Codex TUI 对项目级 hook trust 的持久化不稳定，会反复报：

- `Failed to trust hook: config/batchWrite failed while updating hook trust in TUI`
- 重启后持续提示 `review hooks`

## 当前推荐方案

Codex 仅通过 `unity-auto-compile-guard` skill 和共享规则约束 `.cs` 修改后的编译动作，不再依赖 hook。

## 历史实现（仅供参考，不推荐启用）

以下文件是历史实现，仅供开发者参考：

- Python 逻辑：`.codex/hooks/unity_auto_compile_guard.py`
- PowerShell 启动器：`.codex/hooks/unity_auto_compile_guard_launcher.ps1`
- 同步脚本：`.codex/hooks/sync_user_codex_hooks.ps1`

如需手动启用（不推荐），可参考原配置示例。但请注意：

1. hook trust 持久化问题仍可能存在
2. 每次 Codex 重启可能需要重新 review hooks
3. 当前 skill 约束方案已能满足自动编译需求
