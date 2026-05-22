# Tactics 项目 - Agent 指南

Agent 优先的 Unity 项目，由 Agent 在人工监督下维护代码库。

## Quick Reference

| 规则 | 说明 |
|------|------|
| 禁止 `Resources.Load` | 必须用 `GameAssetManager`（详见 `rules/unity-asset-loading.md`） |
| 禁止 `Debug.Log` | 用 `TLog`/`TBattleLog`（详见 `rules/unity-logging.md`） |
| 禁止直接读写 Unity YAML | 必须通过 MCP 工具（详见 `unity-mcp-core` skill） |
| `.cs` 修改后必须编译 | 调用 `refresh_unity` |
| 写 C# 代码前必须验证 | 遵循 `rules/unity-code-generation.md` 工作流 |
| git commit 前必须检查 | 加载 `unity-git-commit` skill |

## 规则文件索引

详细规则按领域分类，按需读取：

| 规则 | 适用场景 |
|------|----------|
| `rules/unity-core.md` | C# 命名规范、MonoBehaviour 生命周期、序列化 |
| `rules/unity-asset-loading.md` | GameAssetManager 强制约束、Load/Release 配对 |
| `rules/unity-input.md` | Unity Input System |
| `rules/unity-logging.md` | 日志规范（禁止 Debug.Log，使用 TLog/TBattleLog） |
| `rules/unity-code-generation.md` | C# 代码生成强制工作流、防编译错误 |

## 核心原则

### 绝对禁止（红线）

1. **严禁** `Resources.Load` — 必须用 `GameAssetManager`，所有文件类型都是 Unity 资产
2. **严禁** `Debug.Log` — 通用日志用 `TLog`，战斗日志用 `TBattleLog`
3. **严禁** 直接读写 Unity YAML 文件 — 必须通过 MCP 工具

### 必须执行

4. **必须** `refresh_unity` — 修改 `.cs` 后显式编译
5. **必须** 加载 `unity-git-commit` — git commit 前执行提交前检查（`.meta` 配对、GUID 校验）
6. **必须** 用户确认 — Unity Editor 内手动验证的功能，禁止自动提交

### 最佳实践

7. Inspector 适当时优先使用 Odin API
8. 标识符遵循 .NET 命名规范（PascalCase、camelCase 等）
9. `execute_code` 仅当用户明确要求时使用

## Agent 约束

- **语言**：Plan、code 和 debug 输出必须使用中文
- **标识符**：遵循 .NET 命名规范（PascalCase、camelCase 等）

## Agent 限制

**如果它不在代码库中，对 Agent 来说就不存在。**
将权威文档保存在 `.agents/` 下。
