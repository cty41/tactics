# Tactics 项目 - Agent 指南

Agent 优先的 Unity 项目，由 Agent 在人工监督下维护代码库。

## Quick Reference

| 规则 | 说明 |
|------|------|
| 禁止 `Resources.Load` | 必须用 `GameAssetManager`（详见 `rules/unity-asset-loading.md`） |
| 禁止 `Debug.Log` | 用 `TLog`/`TBattleLog`（详见 `rules/unity-logging.md`） |
| 禁止直接读写 Unity YAML | 必须通过 MCP 工具（详见 `unity-mcp-core` skill） |
| 禁止抢占前台焦点 | 默认不使用 Computer Use、窗口激活或真实输入（详见 `.agents/rules/foreground-interaction.md`） |
| Worktree 默认复用 | 除非计划或用户明确要求，禁止自动创建/删除/切换 worktree（详见 `.agents/rules/agent-worktree.md`） |
| `.cs` 修改后必须编译 | 调用 `refresh_unity` |
| 写 C# 代码前必须验证 | 遵循 `rules/unity-code-generation.md` 工作流 |
| git commit 前必须检查 | 加载 `unity-git-commit` skill |
| 跨系统知识查询/沉淀 | 先读 `.agents/knowledge/index.md`，并遵循 `knowledge-maintenance` skill |
| 代码/文档变更后同步 OKF | 运行 `python Tools/okf/catalog_impact.py report --worktree`，更新并同步受影响 scope |

## 规则文件索引

详细规则按领域分类，按需读取：

| 规则 | 适用场景 |
|------|----------|
| `.agents/rules/unity-core.md` | C# 命名规范、MonoBehaviour 生命周期、序列化 |
| `.agents/rules/unity-asset-loading.md` | GameAssetManager 强制约束、Load/Release 配对 |
| `.agents/rules/unity-input.md` | Unity Input System |
| `.agents/rules/unity-logging.md` | 日志规范（禁止 Debug.Log，使用 TLog/TBattleLog） |
| `.agents/rules/unity-code-generation.md` | C# 代码生成强制工作流、防编译错误 |
| `.agents/rules/code-documentation.md` | 代码注释规范（XML doc + // 块注释，英文，系统规则必须注释） |
| `.agents/rules/foreground-interaction.md` | Computer Use、窗口激活、真实输入与人工验证边界 |
| `.agents/rules/agent-worktree.md` | worktree 创建、复用、切换与 Unity 项目启动约束 |
| `.agents/rules/knowledge-maintenance.md` | OKF 知识查询、写回、替代和校验规范 |

## 核心原则

### 绝对禁止（红线）

1. **严禁** `Resources.Load` — 必须用 `GameAssetManager`，所有文件类型都是 Unity 资产
2. **严禁** `Debug.Log` — 通用日志用 `TLog`，战斗日志用 `TBattleLog`
3. **严禁** 直接读写 Unity YAML 文件 — 必须通过 MCP 工具
4. **严禁** 未经当前任务明确请求和动作时确认的前台 UI 自动化 — 不得调用 Computer Use、`activate_window`、真实鼠标键盘或快捷键抢占用户焦点；普通实现、QA、截图和测试授权不构成例外。后台无法完成时按 `.agents/rules/foreground-interaction.md` 标记 `manual_visual_qa_pending`

### 必须执行

5. **必须** `refresh_unity` — 修改 `.cs` 后显式编译
6. **必须** 加载 `unity-git-commit` — git commit 前执行提交前检查（`.meta` 配对、GUID 校验）
7. **必须** 用户确认 — Unity Editor 内手动验证的功能，禁止自动提交
8. **必须** OKF 影响检测 — 修改 `catalog-scopes.yaml` 监控范围内的代码、文档、规则或工具后，先运行 `report --worktree`；核对并更新本任务影响的概念正文，再运行 `sync --worktree --scope <scope> --write`

### 最佳实践

9. Inspector 适当时优先使用 Odin API
10. 标识符遵循 .NET 命名规范（PascalCase、camelCase 等）
11. `execute_code` 仅当用户明确要求时使用

## Agent 约束

- **语言**：Plan、code 和 debug 输出必须使用中文
- **标识符**：遵循 .NET 命名规范（PascalCase、camelCase 等）

## Agent 限制

**如果它不在代码库中，对 Agent 来说就不存在。**
将权威文档保存在 `.agents/` 下。

跨系统架构、当前设计和历史决策先从 `.agents/knowledge/index.md` 渐进读取。OKF 页面是导航与当前状态综合层；涉及实际实现时，必须继续核对其 `repo_paths` 指向的代码、Unity 资产和测试。
