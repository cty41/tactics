# Agent Worktree Policy

## 默认行为

Agent 默认复用用户当前指定的 worktree，不自动创建、删除或切换到新的 worktree。

只有以下情况允许创建新的 worktree：

1. 当前执行计划明确把新 worktree 作为必要的隔离步骤；
2. 用户明确要求创建新的 worktree；
3. 当前 worktree 存在无法安全保留的冲突状态，且新 worktree 是继续工作的唯一安全路径。

“代码改动较大”“方便管理”或“可能需要并行”本身不构成创建 worktree 的理由。

## Godot 项目约束

- Canonical 项目只使用当前 `D:\codes\tactics-worktrees\godot` worktree 下的 `godot/project.godot`；不得为编辑器能力、测试或资产 Spike 创建第二个 Godot 项目。
- 最终 Tag、归档源或其他冻结检出默认只读，只允许用于取证；不得把它们恢复为当前实现或编辑入口。
- 已存在的 worktree 必须优先复用，不能重复创建等价路径或等价分支的副本。
- 不自动删除已有 worktree。删除、移动或覆盖前必须核对分支、未提交修改和用户是否仍在使用。
- 验证脚本和测试统一放在该 worktree 的 `Tools/godot`、`godot/tests` 或正式项目结构内。

## 创建前检查

确需创建时，Agent 必须先记录：

- 创建理由及对应计划/用户指令；
- 源 commit、源分支和目标分支；
- 目标绝对路径；
- 预计生命周期和回收条件；
- 当前 worktree 中必须保留的 dirty 文件。

创建后要向用户说明哪个 worktree 是主工作路径，避免 Godot Editor 打开错误项目。
