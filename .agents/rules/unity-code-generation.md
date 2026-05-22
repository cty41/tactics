# Unity C# 代码生成规则 - 防止编译错误

## 为什么需要这个规则

Agent 写 C# 时的 #1 编译错误：**缺少 `using` 或使用了不存在的类型**。

根本原因：Agent 凭训练数据记忆猜测命名空间和类型名，而不是查证项目实际代码。`auto-compile.js` 只是事后编译，无法阻止写出根本不能编译的代码。本规则在写代码 *之前* 就阻断错误。

## 强制工作流

```
┌─────────────────────────────────────────────────────────┐
│ Step 1: 类型验证（Pre-Write）                            │
│   unity_reflect — 确认目标类型在当前项目确实存在         │
└──────────────────────┬──────────────────────────────────┘
                       ↓
┌──────────────────────┴──────────────────────────────────┐
│ Step 2: Using 模式参考                                   │
│   在同一个模块找 1-2 个已有 .cs 文件，复制它们的 using   │
│   块作为模板                                             │
└──────────────────────┬──────────────────────────────────┘
                       ↓
┌──────────────────────┴──────────────────────────────────┐
│ Step 3: 编写代码                                         │
│   使用已验证的类型 + 参考文件的 using 模板                │
└──────────────────────┬──────────────────────────────────┘
                       ↓
┌──────────────────────┴──────────────────────────────────┐
│ Step 4: 立即验证（Post-Write）                           │
│   validate_script → 修复错误 → 重新验证直到零错误        │
└──────────────────────┬──────────────────────────────────┘
                       ↓
┌──────────────────────┴──────────────────────────────────┐
│ Step 5: 批量编译                                         │
│   refresh_unity(compile="request") 触发 Unity 编译       │
└─────────────────────────────────────────────────────────┘
```

## asmdef 程序集边界检查（重要）

**即使 `using` 写对了，代码也可能编译失败。** 因为目标目录的 `.asmdef` 文件可能没有引用目标类型所在的程序集。

### 项目 asmdef 引用矩阵

| 程序集 | 引用 | 能用的类型来源 |
|--------|------|--------------|
| `Tactics.AssetPipeline` | `[]`（空！） | 仅 Unity 内置 + 自身 |
| `com.tactics` | Sirenix, DOTween, OneLine, TBSFramework, UI Extensions | 几乎所有项目类型 |
| `Tactics.AssetPipeline.Editor` | Tactics.AssetPipeline, Sirenix.* | AssetPipeline + Sirenix |

### 写代码前的强制检查

1. 目标目录下是否有 `.asmdef` 文件？
2. 有的话，读它的 `references` 数组
3. 你要用的类型所在程序集是否在这个数组里？
4. **不在 → 不能写在这里。** 要么换目标目录，要么加引用（加引用需人工确认）

## 红线

| 禁止行为 | 原因 |
|---------|------|
| 凭记忆写 `using` | 训练数据的命名空间和项目实际命名空间可能不同 |
| 不验证就写下一个文件 | 错误会累积，后面排查成本指数增长 |
| 忽略 `validate_script` 报错 | 错误 = 编译必然失败，必须归零 |
| 假设类型"应该存在" | 必须通过 `unity_reflect` 或已有代码确认 |

## 工具速查表

| 场景 | 工具 | 用法 |
|------|------|------|
| 验证类型是否存在 | `unity_reflect` | `action="get_type"`, `class_name="TLog"` |
| 搜索项目中某类型 | `unity_reflect` | `action="search"`, `query="BattleLogger"` |
| 检查现有 using | `Read` 同模块 1-2 个 .cs 文件 | 复制它们的 using 块 |
| 写完后验证 | `validate_script` | `uri="Assets/.../MyScript.cs"` |
| 批量编译 | `refresh_unity` | `compile="request"` |

## 项目命名空间速查表

### 项目模块

| 模块 | 命名空间 | 典型类型 |
|------|---------|---------|
| 日志 | `Tactics.Runtime.Utilities` | TLog, LogLevel |
| 战斗日志 | `Tactics.Runtime.BattleLog` | TBattleLog, BattleLogData, AttackLogData, SkillLogData, HealLogData, DamageLogData, BuffLogData |
| 资源加载 | `Tactics.AssetPipeline` | GameAssetManager（⚠️ AssetPipeline 程序集引用为空，只能用自己的类型！） |
| 战斗 | `Tactics.Common.Battle` | SkillSystem, BattleSettlementCoordinator |
| 单位 | `Tactics.Common.Units` | Unit, IUnit, ICombatant, UnitType |
| 技能/能力 | `Tactics.Common.Units.Abilities` | TargetingStrategy, UnityMoveComponent |
| AI | `Tactics.Common.AI` | IUnitSelector |
| AI 评估器 | `Tactics.Common.AI.Evaluators` | IPositionEvaluator, ITargetEvaluator |
| AI 行为树 | `Tactics.Common.AI.BehaviourTrees` | RegularBehaviourTreeResource |
| 控制器 | `Tactics.Common.Controllers` | IGridController |
| 控制器-格子状态 | `Tactics.Common.Controllers.GridStates` | GridState, GridStateAwaitInput |
| 控制器-解决器 | `Tactics.Common.Controllers.TurnResolvers` | ITurnResolver |
| 控制器-结算 | `Tactics.Common.Controllers.GameResolvers` | GameResult |
| 格子 | `Tactics.Common.Cells` | Square, ITypedCell, SquareHelper |
| 玩家 | `Tactics.Common.Players` | HumanPlayer, AIPlayer, IPlayer |
| 装备 | `Tactics.Equipment` | EquipmentSlot, EquipmentDefinition |
| 流程-战斗 | `Tactics.Flow.Battle` | BattleFlowCoordinator |
| 流程-主页 | `Tactics.Flow.Home` | HomeFlowCoordinator |
| 流程-Roguelike | `Tactics.Flow.Roguelike` | RoguelikeFlowCoordinator |
| Roguelike | `Tactics.RoguelikeMap` | NodeStateManager, RunSummary |
| Roguelike UI | `Tactics.RoguelikeMap.UI` | EventUIController |
| Roster | `Tactics.Roster` | CharacterDefinition, PlayerAdventureState |
| UI | `Tactics.UI` | UIControllerBase, MenuUIController |

### 第三方库

| 库 | 命名空间 | 用途 |
|----|---------|------|
| Odin Inspector | `Sirenix.OdinInspector` | 序列化属性（[SerializeField] 替代） |
| Odin Inspector Editor | `Sirenix.OdinInspector.Editor` | Odin 编辑器扩展 |
| DOTween | `DG.Tweening` | 动画补间 |
| OneLine | `OneLine` | 单行属性绘制 |
| Unity UI Extensions | `UnityEngine.UI.Extensions` | UI 扩展组件 |

## 始终需要的 using

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
```

这些是 Unity 内置命名空间，99% 的情况下都需要。但 Safe = 安全，不是"不用检查"。还是用参考文件确认。

## 常见陷阱速查

1. **TLog vs TBattleLog**：TLog（`Tactics.Runtime.Utilities`）用于通用日志，TBattleLog（`Tactics.Runtime.BattleLog`）用于战斗结构化日志。选错 = 命名空间也错。

2. **Resources.Load 严禁**：任何时候都要用 `GameAssetManager`（见 `rules/unity-asset-loading.md`）。

3. **#if UNITY_EDITOR 中的 using**：编辑器专用代码段里的类型（如 `UnityEditor` 命名空间）需要 `using UnityEditor;`，可以放在 `#if` 块内，也可以放在文件顶部。

4. **Object 歧义**：同时用到 `UnityEngine.Object` 和 `System.Object` 时，加一条：
   ```csharp
   using Object = UnityEngine.Object;
   ```

5. **AssetPipeline 程序集隔离（#1 隐藏编译错误）**：`Tactics.AssetPipeline` 的 `.asmdef` 引用数组为空。这个目录下的代码**不能**用 `Tactics.Runtime.Utilities`、`Tactics.Runtime.BattleLog`、`Tactics.Common.Units` 等任何项目类型。只能用自己的类型 + Unity 内置类型。

6. **Tactics.Roster 不是 Tactics.Common.Roster**：Roster 命名空间是 `Tactics.Roster`，不要写成 `Tactics.Common.Roster`。

7. **异步方法 Async 后缀**：.NET 规范要求异步方法以 `Async` 结尾。Unity 6.2 用 `Awaitable` 代替 `Task`。

## 与 auto-compile.js 的分工

| | auto-compile.js | 本规则 |
|---|---------------|--------|
| 时机 | 写完之后 | 写之前 + 写之后 |
| 职能 | 确保最终编译通过 | 确保每一步产出可编译的代码 |
| 触发方式 | 系统自动注入（始终生效） | Agent 写代码时主动遵循 |
| 解决的问题 | "忘了编译" | "写出不能编译的代码" |
