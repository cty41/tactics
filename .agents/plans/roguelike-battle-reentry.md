# Roguelike 战斗与事件节点重入流程计划

## TL;DR

> **目标**：玩家在战斗结算界面或事件节点中退出游戏后，不保留任何进度（奖励/路径不前进），下次重进时回到地图该节点前，需重新点击进入。
>
> **核心方案**：B 方案（延迟路径提交）—— 进入节点时设置 `Tactics_EventInProgress` 标记，事件完成后才提交路径并清除标记。不保存预战斗快照，依赖"不保存即回滚"的自然语义。
>
> **涉及文件**：`RoguelikeBattleReturnHandler.cs`、`RoguelikeMapUIController.cs`、`PlayerAdventureStateStore.cs`（新增快照方法）
>
> **预计工作量**：5 个 Task，3 个并行 Wave

---

## Background

### 当前问题

当前战斗流程存在状态不一致风险：

1. 玩家点击战斗节点 → `EnterBattleNode()` 设置 `RoguelikePendingNode` → 启动战斗
2. 战斗结束（人类获胜）→ `OnBattleEnded()` **立即**调用 `ApplyRoguelikePathAfterBattle()`
3. `ApplyRoguelikePathAfterBattle()` 将节点加入地图路径、删除 PendingNode
4. 然后才进入结算流程（奖励展示 → 升级 → 属性加点 → 技能选择）
5. 结算完成后 `onComplete` 回调才保存 `PlayerAdventureState`

**崩溃场景**：
```
战斗胜利 → ApplyRoguelikePathAfterBattle（路径已前进）→ 结算UI → 玩家退出
                                                    ↑
                                              状态未保存（ProcessRewards 中的 Save 已删除）
                                              但地图路径已更新
```

重进游戏后：地图显示该节点已完成，但角色没有获得任何奖励（经验、金币、升级）。

### 目标

- 玩家在结算/事件中退出 → 地图路径不前进、奖励不保留
- 重进游戏 → 回到地图，该节点仍可选，需重新点击进入
- 所有事件节点（Battle/Rest/Store/Treasure/Mystery）使用统一的重入标记机制

### 预期收益

- 消除"路径前进但奖励丢失"的状态不一致
- 为所有事件节点提供统一的重入基础设施
- 支持未来扩展（如事件中途保存/恢复）

---

## Scope

### In Scope

- [x] 统一事件标记机制（`Tactics_EventInProgress` + `Tactics_EventNode`）
- [x] 延迟地图路径提交（从 `OnBattleEnded` 开头移到 `onComplete` 回调）
- [x] 游戏启动/地图加载时的事件中断检测与清理
- [x] 为 Battle 以外的节点（Rest/Store/Treasure/Mystery）添加标记支持
- [x] `PlayerAdventureStateStore` 新增预战斗状态快照方法（预留接口，本次不启用快照回滚）
- [x] 更新设计文档 `战斗结算与奖励计划.md`

### Out of Scope

- [ ] 不实现预战斗状态快照回滚（B 方案：不保存即回滚）
- [ ] 不修改战斗内逻辑（BattleController、BattleFlowCoordinator 的战斗启动逻辑不变）
- [ ] 不实现事件节点的具体业务逻辑（RestSite 恢复 HP、Store 购买等保持现有 stub）
- [ ] 不修改结算奖励计算逻辑（BattleRewardSystem 不变）
- [ ] 不添加新的 UI 界面（重进时直接回到地图，无需额外提示）

---

## Architecture Design

### B 方案：延迟路径提交（不保存即回滚）

**核心原则**：
- 进入节点 → 设置标记，**不提交路径**
- 事件完成 → 清除标记，**提交路径**
- 事件中断（退出）→ 标记残留，**路径未前进**，状态即回滚

**标记设计**：

| PlayerPrefs Key | 值 | 说明 |
|----------------|-----|------|
| `Tactics_EventInProgress` | `"Battle"` / `"Rest"` / `"Store"` / `"Treasure"` / `"Mystery"` | 当前进行中的事件类型 |
| `Tactics_EventNode` | `"{x},{y}"` | 当前进行中的节点坐标 |

**时序图**：

```
正常流程：
  玩家点击 Battle 节点
    → EnterBattleNode()
      → Set Tactics_EventInProgress = "Battle"
      → Set Tactics_EventNode = "{x},{y}"
      → StartBattleAsync()
    → 战斗结束（人类获胜）
    → OnBattleEnded()
      → 战后恢复 HP/MP
      → 启动结算流程
        → 结算UI → 升级 → 属性加点 → 技能选择
      → onComplete 回调
        → ApplyRoguelikePathAfterBattle()  [路径提交]
        → PlayerAdventureStateStore.Save()  [状态保存]
        → Delete Tactics_EventInProgress      [标记清除]
        → Delete Tactics_EventNode
        → EndBattleAsync() → 返回地图

中断流程：
  玩家点击 Battle 节点
    → EnterBattleNode() → 设置标记 → StartBattleAsync()
    → 战斗结束 → 启动结算流程
    → 玩家在结算UI中退出游戏
      → onComplete 未触发
      → 路径未提交，标记未清除
  玩家重新启动游戏
    → 加载地图
    → 检测到 Tactics_EventInProgress 存在
      → 清除标记（记录日志）
      → 地图路径未前进（节点仍可选）
    → 玩家重新点击该节点
      → 重新进入战斗
```

### 统一事件接口（预留）

```csharp
/// <summary>
/// Roguelike 事件节点重入标记管理器。
/// 所有事件节点（战斗、休息、商店、宝箱、神秘）共用同一套标记机制。
/// </summary>
public static class RoguelikeEventReentryManager
{
    public const string EventInProgressKey = "Tactics_EventInProgress";
    public const string EventNodeKey = "Tactics_EventNode";

    public static void MarkEventInProgress(string eventType, Vector2Int nodePoint)
    {
        PlayerPrefs.SetString(EventInProgressKey, eventType);
        PlayerPrefs.SetString(EventNodeKey, $"{nodePoint.x},{nodePoint.y}");
        PlayerPrefs.Save();
    }

    public static void ClearEventInProgress()
    {
        PlayerPrefs.DeleteKey(EventInProgressKey);
        PlayerPrefs.DeleteKey(EventNodeKey);
        PlayerPrefs.Save();
    }

    public static bool IsEventInProgress(out string eventType, out Vector2Int? nodePoint)
    {
        eventType = PlayerPrefs.GetString(EventInProgressKey, null);
        if (string.IsNullOrEmpty(eventType))
        {
            nodePoint = null;
            return false;
        }
        // parse nodePoint from EventNodeKey
        // ...
        return true;
    }
}
```

---

## Tasks

### Task 1: 创建 RoguelikeEventReentryManager 标记管理器

- **目标**：创建统一的事件标记管理类，封装 PlayerPrefs 的读写
- **输入**：节点坐标 `Vector2Int`、事件类型字符串
- **输出**：新文件 `Assets/Tactics/Scripts/Roguelike/RoguelikeEventReentryManager.cs`
- **验收标准**：
  - [x] 文件创建成功，编译通过
  - [x] `MarkEventInProgress("Battle", new Vector2Int(1, 2))` 正确写入 PlayerPrefs
  - [x] `ClearEventInProgress()` 正确删除两个 Key
  - [x] `IsEventInProgress()` 正确读取并解析坐标
  - [x] 所有方法都有 `TLog.Info` 日志

### Task 2: 延迟地图路径提交到结算完成

- **目标**：将 `ApplyRoguelikePathAfterBattle` 从 `OnBattleEnded` 开头移到 `onComplete` 回调
- **输入**：`RoguelikeBattleReturnHandler.cs` 当前代码
- **输出**：修改后的 `RoguelikeBattleReturnHandler.cs`
- **验收标准**：
  - [x] `OnBattleEnded` 不再立即调用 `ApplyRoguelikePathAfterBattle`
  - [x] `ApplyRoguelikePathAfterBattle` 在 `onComplete` 回调中调用（在 `Save(state)` 之后）
  - [x] 结算中退出时，地图路径不前进（PendingNode 保留，路径未更新）
  - [x] 人类失败时仍立即清除 PendingNode（失败不需要重入）

### Task 3: 为 Battle 节点添加标记设置/清除

- **目标**：在 `EnterBattleNode` 设置标记，在 `onComplete` 清除标记
- **输入**：`RoguelikeMapUIController.cs`、`RoguelikeBattleReturnHandler.cs`
- **输出**：修改后的两个文件
- **验收标准**：
  - [x] `EnterBattleNode` 调用 `RoguelikeEventReentryManager.MarkEventInProgress("Battle", point)`
  - [x] `onComplete` 回调调用 `RoguelikeEventReentryManager.ClearEventInProgress()`
  - [x] 标记设置后 PlayerPrefs 中可查询到 `Tactics_EventInProgress = "Battle"`

### Task 4: 为其他事件节点添加标记支持

- **目标**：RestSite、Store、Treasure、Mystery 节点也使用统一标记
- **输入**：`RoguelikeMapUIController.cs`
- **输出**：修改后的 `RoguelikeMapUIController.cs`
- **验收标准**：
  - [x] `EnterStubNode` 调用 `MarkEventInProgress`（事件类型根据 nodeType 映射）
  - [x] `CoUnlockAfterStub` 完成时调用 `ClearEventInProgress` 并提交路径
  - [x] 映射关系：RestSite→"Rest", Store→"Store", Treasure→"Treasure", Mystery→"Mystery"

### Task 5: 添加启动时事件中断检测

- **目标**：游戏启动/地图加载时检测残留标记并清理
- **输入**：`RoguelikeMapUIController.cs`
- **输出**：修改后的 `RoguelikeMapUIController.cs`
- **验收标准**：
  - [x] `LoadOrGenerateMap` 或 `OnShown` 中检测 `Tactics_EventInProgress`
  - [x] 检测到残留标记时：
    - 记录 `TLog.Warning` 日志
    - 调用 `ClearEventInProgress()`
    - 不修改地图路径（自然回滚）
  - [x] 地图正常显示，被中断的节点仍可选

### Task 6: 更新设计文档

- **目标**：在 `战斗结算与奖励计划.md` 中添加重入流程章节
- **输入**：当前 `.agents/plans/战斗结算与奖励计划.md`
- **输出**：更新后的文档
- **验收标准**：
  - [x] 新增 "## 战斗与事件重入流程" 章节
  - [x] 包含时序图（正常流程 + 中断流程）
  - [x] 列出所有新增/修改的文件
  - [x] 标记 Task 1-5 为待实现

---

## Execution Strategy

### Wave 1（并行 — 无依赖）
- Task 1: 创建 RoguelikeEventReentryManager
- Task 6: 更新设计文档

### Wave 2（并行 — 依赖 Wave 1）
- Task 2: 延迟路径提交
- Task 3: Battle 节点标记
- Task 4: 其他节点标记

### Wave 3（串行 — 依赖 Wave 2）
- Task 5: 启动时中断检测

### Wave FINAL（验证）
- 编译验证（`refresh_unity`）
- 代码审查（检查所有 Save/Delete 调用位置）
- 日志审查（确认标记读写有日志）

---

## Risk & Open Questions

1. **PlayerPrefs 可靠性**：如果游戏在标记写入和战斗启动之间崩溃，标记已设置但战斗未开始。重进时会看到标记残留并清理，玩家需重新点击节点。这是可接受的行为。
2. **多节点同时标记**：理论上不应发生（一次只能处理一个节点），但 `ClearEventInProgress` 会无条件清除，不会误删其他标记。
3. **未来扩展**：如需实现"战斗中途保存"（如 Roguelike 的 Suspend & Resume），可在此标记机制基础上扩展，添加预战斗状态快照。

---

## Success Criteria

- [ ] 战斗胜利后结算中退出 → 重进地图 → 该节点仍可选 → 重新点击进入 → 正常战斗
- [ ] 战斗失败后退出 → 重进地图 → 该节点仍可选（PendingNode 已被清除，但路径未前进）
- [ ] 所有事件节点（Rest/Store/Treasure/Mystery）退出后重进 → 节点仍可选
- [ ] 正常完成事件 → 路径前进 → 状态保存 → 标记清除
- [ ] 编译零错误
