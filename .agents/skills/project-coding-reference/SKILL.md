---
name: project-coding-reference
description: "Use when writing or modifying C# code, before adding using statements, project type references, singleton calls, logging, or asset loading APIs"
---

# Project Coding Reference

写 C# 代码前的统一参考，避免命名空间缺失和 API 访问错误。

## Quick Reference

### Namespace Reference

| Namespace | Key Classes | Common Use |
|-----------|------------|------------|
| `Tactics.Runtime.Utilities` | `TLog` | Logging (never use `Debug.Log`) |
| `Tactics` | `UIManager` | UI singleton and UIId enum |
| `Tactics.UI` | `UIControllerBase` | UI controller base class |
| `Tactics.AssetPipeline` | `GameAssetManager` | Asset loading (never use `Resources.Load`) |
| `Tactics.RoguelikeMap` | `RoguelikeMapNode`, `NodeState` | Map nodes |
| `Tactics.RoguelikeMap.Events` | `RoguelikeEvent`, `EventOption`, `EventResult`, `EventEffectContext` | Event system |
| `Tactics.RoguelikeMap.Interaction` | `NodeInteractionManager` | Node interaction |
| `Tactics.RoguelikeMap.Economy` | `RunGoldManager` | Gold economy |
| `Tactics.RoguelikeMap.UI` | `EventUIController` | Event UI |
| `Tactics.Roster` | `CharacterDefinition` | Character attributes |
| `Tactics.Flow.Home` | `HomeFlowCoordinator` | Home scene flow |
| `Tactics.Flow.Roguelike` | `RoguelikeFlowCoordinator` | Roguelike scene flow |
| `Tactics.Flow.Battle` | `BattleFlowCoordinator` | Battle scene flow |
| `Newtonsoft.Json` | `JsonConvert`, `JsonProperty` | JSON serialization |

### Access Constraint Reference

| API | Availability | Reason |
|-----|-------------|--------|
| `Ui.GetRootElement()` | ❌ Only in `UIControllerBase` subclasses | `Ui` is a protected property |
| `UIManager.Instance.GetRootElement()` | ✅ Anywhere | Singleton access |
| `GameAssetManager.Instance.Load<T>()` | ✅ Editor mode | Synchronous |
| `GameAssetManager.Instance.LoadAsync<T>()` | ✅ Recommended | Asynchronous |
| `Resources.Load()` | ❌ Forbidden | Project convention violation |
| `Debug.Log()` | ❌ Forbidden | Use `TLog.Info/Warning/Error` instead |

### Singleton Reference

| Singleton | Access Pattern |
|-----------|---------------|
| UIManager | `UIManager.Instance` |
| GameAssetManager | `GameAssetManager.Instance` |
| RunGoldManager | `RunGoldManager.Instance` |
| NodeInteractionManager | `NodeInteractionManager.Instance` |
| EventManager | `EventManager.Instance` |
| TreasureNodeHandler | `TreasureNodeHandler.Instance` |
| StoreNodeHandler | `StoreNodeHandler.Instance` |
| RestSiteNodeHandler | `RestSiteNodeHandler.Instance` |
| EventUIController | `EventUIController.Instance` |
| HomeFlowCoordinator | `HomeFlowCoordinator.Instance` |
| BattleFlowCoordinator | `BattleFlowCoordinator.Instance` |
| RoguelikeFlowCoordinator | `RoguelikeFlowCoordinator.Instance` |
| SceneController | `SceneController.Instance` |

### High-Risk Project Types

以下类型在 `Assets/ThirdParty/TBSFramework/` 中存在同名定义，容易误引用第三方版本。

| Type | Namespace | Why it matters |
|------|-----------|----------------|
| `CombatComponent` | `Tactics.Common.Units` | 与 ThirdParty 中同名类型重名 |
| `IUnit` | `Tactics.Common.Units` | 与 ThirdParty 中同名接口重名 |
| `ElementType` | `Tactics.Common.Units.Buffs` | 枚举常用于伤害/Buff 逻辑，易漏 namespace |
| `Vector3ImplExtension` | `Tactics.Common.Utilities` | 扩展类命名不直观，且存在同名 ThirdParty 版本 |

## When to use

- Writing any new C# file in the project
- Using a project class but unsure of its namespace
- Calling a singleton API but unsure of the access pattern
- Fixing "type or namespace not found" compilation errors
- Adding new using statements

## Workflow

### Step 0: Never guess project types

Quick Reference 未列出的类型，禁止凭经验猜命名空间。必须先搜索定义，并优先确认 `Assets/Tactics/Scripts/**` 下是否已有项目内实现，避免误用 ThirdParty 同名类型。

### Step 1: Look up namespace

Before using a class, check the Namespace Reference table above.

### Step 2: Add using statements

```csharp
using Tactics.Runtime.Utilities;   // TLog
using Tactics;                     // UIManager
using Tactics.UI;                  // UIControllerBase
using Tactics.AssetPipeline;        // GameAssetManager
using Tactics.RoguelikeMap.Events;  // Event classes
```

### Step 3: Verify access pattern

- Inheriting `UIControllerBase` → can use `Ui.GetRootElement()`
- Inheriting `MonoBehaviour` → must use `UIManager.Instance.GetRootElement()`
- Static/regular class → must use `UIManager.Instance.GetRootElement()`

### Step 4: Use singleton correctly

```csharp
// ✅ Correct
var gold = RunGoldManager.Instance.CurrentGold;
await UIManager.Instance.ShowAsync(UIManager.UIId.MyPanel);

// ❌ Wrong — missing .Instance
var gold = RunGoldManager.CurrentGold;
await UIManager.ShowAsync(UIManager.UIId.MyPanel);
```

## Anti-patterns

| ❌ Wrong | ✅ Correct | Why |
|----------|-----------|-----|
| `Ui.GetRootElement()` in MonoBehaviour | `UIManager.Instance.GetRootElement()` | `Ui` is protected, only in UIControllerBase |
| Missing `using Tactics;` for UIManager | Add `using Tactics;` | `UIManager` is in the root Tactics namespace |
| `Debug.Log()` | `TLog.Info()` | Project convention |
| `Resources.Load()` | `GameAssetManager.Instance.Load()` | Project convention |
| `RunGoldManager.CurrentGold` | `RunGoldManager.Instance.CurrentGold` | Singleton needs `.Instance` |
| 未列在 Quick Reference 的项目类型凭经验猜 namespace | 先搜索定义，优先确认 `Assets/Tactics/Scripts/**` 下的项目内实现 | 项目内与 ThirdParty 存在同名类型，猜测极易引用错 |

## Checklist

Before submitting C# code, verify:

- [ ] All required `using` statements are present
- [ ] Singleton APIs accessed via `.Instance`
- [ ] Non-UIControllerBase classes use `UIManager.Instance` not `Ui`
- [ ] Logging uses `TLog` not `Debug.Log`
- [ ] Asset loading uses `GameAssetManager` not `Resources.Load`
- [ ] Quick Reference 未列出的类型，已先搜索定义而不是凭经验猜 namespace
