# Tactics 项目 - Agent 指南

Agent 优先的 Unity 项目，由 Agent 在人工监督下维护代码库。

## 核心原则

1. **资源**：使用 `GameAssetManager`，不用 `Resources.Load`；每个 Load 都要配对 Release
2. **路径**：使用项目路径（`Assets/...`）
3. **Inspector**：适当时优先使用 Odin API
4. **编译**：修改 C# 脚本后，**必须**显式调用 `refresh_unity` 触发 Unity 编译。Agent 一次 build mode 执行完成前，若修改过任何 `.cs` 文件，必须在最后调用一次 `refresh_unity`
5. **日志**：通用日志用 `TLog.Info/Warning/Error`，战斗日志用 `TBattleLog.Log`，禁止 `Debug.Log`
6. **工具安全**：禁止使用 `unity-MCP_execute_code` 执行自行编写的测试代码或验证脚本；仅当用户明确要求时才可使用该工具

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
