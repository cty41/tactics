# Tactics 项目 - Agent 指南

Agent 优先的 Unity 项目，由 Agent 在人工监督下维护代码库。

## 核心原则

1. **资源加载**：
   - 必须使用 `GameAssetManager`，**严禁** `Resources.Load`
   - 所有文件类型（.json/.txt/.xml/.asset/.prefab）都是 Unity 资产
   - Load/Release 必须配对，优先使用异步 `LoadAsync`
   - 路径使用完整项目路径（`Assets/...`）
2. **Inspector**：适当时优先使用 Odin API
3. **编译**：修改 C# 脚本后，**必须**显式调用 `refresh_unity` 触发 Unity 编译。Agent 一次 build mode 执行完成前，若修改过任何 `.cs` 文件，必须在最后调用一次 `refresh_unity`
4. **日志**：通用日志用 `TLog.Info/Warning/Error`，战斗日志用 `TBattleLog.Log`，禁止 `Debug.Log`
5. **工具安全**：禁止使用 `unity-MCP_execute_code` 执行自行编写的测试代码或验证脚本；仅当用户明确要求时才可使用该工具
6. **Git 提交**：执行任何 git commit 前，**必须**先加载 `unity-git-commit` skill 并逐项完成其定义的提交前检查（`.meta` 配对校验、GUID 有效性确认）
7. **审查与验证**：涉及需要在 Unity Editor 内手动操作验证的功能或修复，完成 `review-work` 审查后**禁止自动提交**。必须等待用户在 Editor 内测试验证并明确确认通过后，方可执行 `git commit`

## 规则与指南

详细规则按领域分类，按需读取。相关领域工作时，主动读取对应文件：

| 规则 | 适用场景 |
|------|----------|
| `rules/unity-core.md` | C# 命名规范、MonoBehaviour 生命周期、序列化 |
| `rules/unity-asset-loading.md` | GameAssetManager API、Load/Release 配对 |
| `rules/unity-input.md` | Unity Input System |
| `rules/unity-logging.md` | 日志规范（禁止 Debug.Log，使用 TLog/TBattleLog） |
| `rules/game-asset-pipeline.md` | AssetBundle 构建与加载完整指南 |

## Agent 约束

- **语言**：Plan、code 和 debug 输出必须使用中文
- **标识符**：遵循 .NET 命名规范（PascalCase、camelCase 等）

## Agent 限制

**如果它不在代码库中，对 Agent 来说就不存在。**
将权威文档保存在 `.agents/` 下。
