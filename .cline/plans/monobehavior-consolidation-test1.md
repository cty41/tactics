# Test1.unity MonoBehavior 收束计划

## 场景结构概览

Test1.unity 中有 9 个根 GameObject：

| GameObject | MonoBehavior 脚本 |
|---|---|
| Grid | `TilemapCellManager` |
| GridController | `UnityGridController` + `BattlePartyBootstrap` + `BattleController` |
| ↳ TurnResolver (`GridController` 子节点) | `UnitSpeedTurnResolver` |
| PlayerManager | `UnityPlayerManager` |
| ↳ HumanPlayer | `HumanPlayer` |
| ↳ AIPlayer | `AIPlayer` + `SubsequentUnitSelector` |
| UnitManager | `UnityUnitManager` |
| Camera | Unity Camera |
| EventSystem | Unity EventSystem |
| Directional Light | Unity Light |
| Canvas | UI 相关 |

## 可收束的 MonoBehaviour（共 4 个）

以下脚本不依赖任何 MonoBehaviour 生命周期方法（无 `Awake/Start/Update`），不使用 `transform`，唯一 Unity 引用是 `Debug.LogWarning`，完全可以收束为普通类。

### 1. `UnitSpeedTurnResolver` → 普通类

**文件**: `Assets/Tactics/Scripts/Common/UnitSpeedTurnResolver.cs`  
**当前**: `class UnitSpeedTurnResolver : UnityTurnResolver : MonoBehaviour, ITurnResolver`  
**问题**: 纯逻辑类，仅实现 `ResolveStart` 和 `ResolveTurn`，无任何 MonoBehavior 生命周期  
**方案**: 
- 改为 `class UnitSpeedTurnResolver : ITurnResolver`（直接实现接口，不再继承 `UnityTurnResolver`）
- 删除 `UnityTurnResolver` 抽象类（`Assets/Tactics/Scripts/Common/Controllers/turnResolvers/UnityTurnResolver.cs`）
- `UnityGridController` 中将 `_turnResolver` 字段改为 `[SerializeReference] private ITurnResolver _turnResolver`
- 场景中的 TurnResolver GameObject 可删除

### 2. `SubsequentUnitSelector` → 普通类

**文件**: `Assets/Tactics/Scripts/Common/ai/SubsequentUnitSelector.cs`  
**当前**: `class SubsequentUnitSelector : UnityUnitSelector : MonoBehaviour, IUnitSelector`  
**问题**: 仅封装 `SubsequentUnitSelectorImpl` 的 `SelectNext` 调用，无任何 MonoBehavior 功能  
**方案**:
- 改为 `class SubsequentUnitSelector : IUnitSelector`
- 删除 `UnityUnitSelector` 抽象类（`Assets/Tactics/Scripts/Common/ai/UnityUnitSelector.cs`）
- `AIPlayer` 中将 `_unitSelector` 字段改为 `[SerializeReference] private IUnitSelector _unitSelector`
- 场景中 AIPlayer 子节点上的 `SubsequentUnitSelector` 组件可移除

### 3. `HumanPlayer` → 可考虑简化

**文件**: `Assets/Tactics/Scripts/Common/players/HumanPlayer.cs`  
**当前**: `class HumanPlayer : Player : MonoBehaviour, IPlayer`  
**现状**: 极其简单，`Play()` 只设置 `GridState = new GridStateAwaitInput()`  
**方案**: 保留 MonoBehaviour（因为 PlayerManager 通过 `GetComponentsInChildren<IPlayer>()` 加载），但可考虑用 `[SerializeReference]` 替代

## 保留 MonoBehaviour 的脚本（有正当理由）

| 脚本 | 保留理由 |
|---|---|
| `TilemapCellManager` | 引用 `Tilemap` 组件、`Camera`；使用 `Update()` 处理鼠标输入 |
| `UnityUnitManager` | 使用 `GetComponentsInChildren<IUnit>()` 加载子节点上的单位 |
| `UnityPlayerManager` | 使用 `GetComponentsInChildren<IPlayer>()` 加载子节点上的玩家 |
| `AIPlayer` | 使用 `[SerializeField]` 配置延迟参数；使用 `WaitForKeypress` + new input system；`Reset()` 自动添加组件 |
| `Player` (基类) | 提供 `_playerNumber` 序列化字段 |
| `BattlePartyBootstrap` | 使用 `FindFirstObjectByType` 和 `transform` 访问子节点 |
| `BattleController` | `MonoBehaviourSingleton` 基类，需要 MonoBehaviour 生命周期 |
| `UnityGridController` | `Start()` 中使用 `_startImmediatelly`；序列化引用其他管理器 |

## 实施步骤

### Phase 1: TurnResolver 收束

1. **修改 `UnitSpeedTurnResolver`** — 改为直接实现 `ITurnResolver` 接口，移除继承链
2. **删除 `UnityTurnResolver`** — 接口已有 `ITurnResolver`，无需额外抽象
3. **修改 `UnityGridController`** — `[SerializeReference]` 替代 MonoBehaviour 引用
4. **场景清理** — 删除 GridController 下的 TurnResolver GameObject
5. **编译验证** — 确认无编译错误

### Phase 2: UnitSelector 收束

1. **修改 `SubsequentUnitSelector`** — 改为直接实现 `IUnitSelector`
2. **删除 `UnityUnitSelector`** — 同上
3. **修改 `AIPlayer`** — `[SerializeReference] private IUnitSelector _unitSelector` 替代 MonoBehaviour 引用
4. **修改 `AIPlayer.Reset()`** — 改为 `gameObject.AddComponent` → 构造实例赋值
5. **场景清理** — 移除 AIPlayer 上的 `SubsequentUnitSelector` 组件
6. **编译验证**

### Phase 3: 回归测试

1. 运行 Test1.unity 场景确认战斗流程正常
2. 确认 AI 回合正常执行（包括 UnitSelector 工作）
3. 确认 Turn 切换正常（包括 UnitSpeedTurnResolver 工作）

## 风险与注意事项

1. **SerializeReference 序列化**: Unity 的 `[SerializeReference]` 需要正确的 `[SerializeReference]` 标记，且在 Inspector 中显示为下拉选择器。需要在场景重新保存后确保序列化数据正确迁移。
2. **场景数据丢失**: 修改后，旧场景文件中的 MonoBehaviour 引用会失效。需要重新在 Inspector 中勾选对应的实现类型。
3. **其他场景**: 需要确认是否有其他场景也引用了 `UnityTurnResolver` 或 `UnityUnitSelector`。
