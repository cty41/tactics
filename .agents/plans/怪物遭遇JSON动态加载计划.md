# 怪物遭遇 JSON 动态加载计划

## Summary

- 目标：将敌方怪物的 Unit 与 AI 配置从 `Test1` 场景实例迁移到地图节点引用的遭遇 JSON，在进入战斗时动态生成敌方单位。
- 当前复查结论（2026-06-02）：当前工作区未发现 `EncounterConfig`、`encounterConfigPath`、`SpawnEncounterEnemies`、`aiBrainAssetPath` 等相关实现；关键文件 `BattleController.cs`、`Unit.cs`、`NodeInteractionManager.cs`、`SerializableMapData.cs` 当前无相关 diff。
- 成功标准：同一个战斗场景可根据当前 Roguelike 节点加载不同怪物阵容、出生格、Unit prefab、AiBrainAsset；玩家队伍仍沿用现有存档 / `TestParty.json` 动态生成。
- 范围：敌方遭遇 MVP；不重做战斗框架，不把玩家队伍迁入遭遇 JSON，不改 AssetBundle 基础管线。

## Current State

- `AIPlayer` 当前通过 `Unit.AiBrainAsset` 判断是否进入新 AI：`AIPlayer -> AiBrainRunner.Execute(unit, gridController, concreteUnit.AiBrainAsset)`。
- `Unit` 当前只有 `_aiBrainAsset` 序列化字段与只读 getter，动态生成后若要绑定 brain，需要新增受控 API，例如 `ApplyAiBrain(AiBrainAsset brain)`。
- `BattleController.Start()` 仍是 `SpawnPartyUnits()` 后直接 `InitializeGame()`；`IUnitManager.Initialize()` 会收集 `_unitContainer` 或 `UnitManager` 下已有的 `IUnit`。
- 玩家队伍已经动态化：`PlayerAdventureStateStore` 从 `Assets/Tactics/GameData/TestParty.json` / PlayerPrefs 读取队伍、prefab 映射和角色状态。
- Roguelike 战斗入口仍在 `NodeInteractionManager.HandleBattleNode()`，当前保存 pending node 后加载固定战斗场景 `Test1`。
- 地图 JSON 已有 `SerializableMapData` / `SerializableNodeData`；当前节点 payload 模式已有 `eventId`、`shopId`、`treasureId`，适合追加 encounter 引用字段。

## Implementation Changes

### Task 0: 确认当前代码基线

- 目标：防止计划基于缺失分支或未同步代码。
- 输入：当前 `D:\codes\tactics` 工作区。
- 输出：记录 encounter 相关实现是否已存在。
- 验收标准：
  - `rg "Encounter|encounter|SpawnEncounter|aiBrainAssetPath|encounterConfigPath" Assets/Tactics` 结果被记录。
  - 若后续出现相关实现，先复查再执行 Task 1-5，不重复造一套并行系统。

### Task 1: 定义敌方遭遇 JSON 契约

- 目标：新增最小可用的 `EncounterConfig` 数据结构与样例 JSON。
- 输入：`encounterId` 或 `encounterConfigPath`、怪物条目列表。
- 输出：每个怪物条目包含 `unitPrefabPath`、`aiBrainAssetPath`、`playerNumber`、`spawnCell`；可选 `displayName`、基础属性覆盖后续再扩展。
- 验收标准：
  - 使用 Newtonsoft.Json 反序列化。
  - Asset 路径统一使用 `Assets/...` 项目路径，并通过 `GameAssetManager.Load<T>()` 加载。
  - 样例 JSON 放在 `Assets/Tactics/GameData/Encounters/`。

### Task 2: 将地图节点连接到遭遇配置

- 目标：地图节点只保存遭遇引用，不内联完整怪物列表。
- 输入：`SerializableNodeData` / `RoguelikeMapNode`。
- 输出：敌方节点可携带 `encounterConfigPath`，进入战斗前保存为当前 pending encounter。
- 验收标准：
  - `MinorEnemy`、`EliteEnemy`、`Boss` 能指向不同 encounter。
  - 旧地图没有 encounter 字段时走默认 encounter，不阻断现有流程。
  - `NodeInteractionManager.HandleBattleNode()` 在加载战斗场景前保存当前 encounter 引用。

### Task 3: 战斗初始化前动态生成敌方单位

- 目标：在 `BattleController.InitializeGame()` 前，根据 pending encounter 生成敌方单位。
- 输入：pending encounter、`UnitManager` 容器、格子管理器、遭遇 JSON。
- 输出：实例化敌方 Unit prefab，设置 `PlayerNumber`、位置 / `CurrentCell`、AI brain，并让现有 `IUnitManager.Initialize()` 收集。
- 验收标准：
  - 生成顺序为 `SpawnPartyUnits()` -> `SpawnEncounterEnemies()` -> `InitializeGame()`。
  - 清理或禁用场景内旧敌方占位单位，避免重复敌人。
  - 动态敌人仍由现有 `AIPlayer` 和 `AiBrainRunner` 执行。

### Task 4: 最小编辑器校验与迁移辅助

- 目标：降低 JSON 配错导致运行时空引用的概率。
- 输入：遭遇 JSON 路径、Unit prefab 路径、AiBrainAsset 路径、出生格。
- 输出：轻量校验入口或 Editor 测试。
- 验收标准：
  - 校验路径存在、prefab 有 `Unit` / `TilemapUnit`、brain 资产有效、出生格格式合法。
  - 校验失败明确指出配置文件和字段。
  - 不直接读写 Unity YAML；场景迁移必须走 Unity MCP 或 Editor API。

### Task 5: 回归地图到战斗全流程

- 目标：确认动态遭遇不破坏现有队伍生成、结算、战后返回。
- 输入：`Test1`、Roguelike 地图节点、基础小怪 encounter。
- 输出：从地图节点进入战斗时，玩家队伍来自存档，敌方来自 encounter JSON，胜利后仍返回地图。
- 验收标准：
  - Unity 编译通过。
  - Editor Play Mode 下从 `Home -> RoguelikeMap -> MinorEnemy` 进入 `Test1` 可看到 JSON 配置的敌人。
  - 敌方回合日志显示动态生成单位使用对应 `AiBrainAsset`。
  - `RoguelikeBattleReturnHandler` 现有返回流程不回退。

## Interfaces / Data Flow

```text
RoguelikeMapNode
  -> encounterConfigPath
  -> NodeInteractionManager.HandleBattleNode()
  -> PlayerPrefs 或 RoguelikeMapRuntimeState 保存 pending encounter
  -> BattleFlowCoordinator.LoadSceneAsync(Test1)
  -> BattleController.Start()
  -> SpawnPartyUnits()
  -> SpawnEncounterEnemies()
  -> InitializeGame()
  -> AIPlayer 使用 Unit.AiBrainAsset
```

- 新增 DTO：`EncounterConfig`、`EncounterUnitEntry`。
- `Unit` 新增受控 AI brain 绑定 API，不开放任意公共字段。
- 出生格建议先使用 `Vector2Int` 或 `"x,y"`，由现有 `CellManager` 找到 Cell 并同步 `CurrentCell`、`CurrentUnits`、`IsTaken`。

## Test Plan

- 自动检查：
  - JSON 反序列化与必填字段校验。
  - encounter 加载器缺文件、缺 prefab、缺 brain、重复出生格测试。
  - 修改 `.cs` 后执行 `refresh_unity(compile="request")` 并检查 Unity Console。
- 手工验证：
  - `MinorEnemy` 使用基础小怪 encounter。
  - `EliteEnemy` 临时指向不同 encounter，同一 `Test1` 场景出现不同敌人。
  - 场景旧敌方单位不重复出现。
  - 敌人 AI 正常行动，战斗结束后返回地图。

## Assumptions

- 当前工作区就是本次计划依据；如之后切换分支或拉取远端代码，需要先重新执行 Task 0。
- 只动态化敌方怪物遭遇；玩家队伍继续使用 `PlayerAdventureStateStore`。
- 地图节点保存 encounter 引用，遭遇详情放独立 JSON。
- 不把 AI graph/profile/brain 改成 JSON，只通过 JSON 引用现有 `AiBrainAsset`。

## Risks / Open Questions

- 用户提到“代码已更新”，但当前工作区未发现 encounter 相关实现；若更新在其他分支/路径，执行前必须同步并重审。
- 出生格与 Tilemap 坐标需要实测确认，避免世界坐标和格子坐标错位。
- `IntentScorer` 的 score 节点仍需注意全局评分语义，遭遇 JSON 不应暗示每个怪物能通过 JSON 改写 AI graph 内部评分作用域。

## Handoff Notes

- 新 session 先读：`BattleController.cs`、`NodeInteractionManager.cs`、`SerializableMapData.cs`、`Unit.cs`、`AIPlayer.cs`、`PlayerAdventureStateStore.cs`。
- 先运行 `rg "Encounter|encounter|SpawnEncounter|aiBrainAssetPath|encounterConfigPath" Assets/Tactics`，确认是否已有实现。
- 不要直接编辑 `.unity` / `.prefab` YAML；需要改场景或 prefab 时使用 Unity MCP / Editor API。
- 不要新增第二套 AI 执行链；动态生成出的单位必须继续走 `AIPlayer` 和 `AiBrainRunner`。
