# AbilityConfig → SkillGraph 迁移计划

## Background

- **当前问题**：项目存在两套并行的 Ability 运行时（GenericAbilityImpl 与 SkillGraphAbilityImpl），AI 行为树节点 AttackActionNode 深度耦合旧系统，新增能力需同步维护两套配置
- **目标**：将所有运行时消费方从 GenericAbilityImpl 迁移到 SkillGraphAbilityImpl，使旧 AbilityConfig 仅作为序列化兼容层存在
- **预期收益**：消除双系统维护负担，统一技能图驱动的工作流，AI 行为树原生支持新系统

## File Structure

```
Assets/Tactics/Scripts/Common/Units/abilities/
├── IAbility.cs                    — 能力公共接口（不改）
├── AbilityConfig.cs               — SO 基类（保留兼容，Phase 2 微调）
├── GenericAbilityImpl.cs          — 旧运行时（保留，标记 Obsolete）
├── AbilityEffect.cs               — 效果定义（保留，被旧系统引用）
├── TargetingStrategy.cs           — 瞄准策略（保留，被旧系统引用）
├── AbilityCommand.cs              — 命令包装（保留）
├── AttackAbilityConfig.cs         — 向后兼容空壳（保留）
├── IAiExecutableAbility.cs        — AI 执行接口（不改）
├── SkillGraphAbilityConfig.cs     — 桥接配置（Phase 2 增强）
├── SkillGraphAbilityImpl.cs       — 新运行时（Phase 1 增加接口实现）
├── IAbilityCombatQueryable.cs     — 【新建】战斗元数据查询接口

Assets/Tactics/Scripts/Common/ai/
├── behaviourTrees/customNodes/
│   └── AttackActionNode.cs        — 行为树攻击节点（Phase 1 重构）
├── MonsterAI/
│   ├── AiContextBuilder.cs        — AI 上下文构建（不改，已有双路径）
│   └── IntentExecutor.cs          — 意图执行器（不改，通过接口调用）

Assets/Tactics/Scripts/Common/Units/
├── Unit.cs                        — 单位主类（Phase 2 微调默认配置）
├── Classes/
│   └── RoleConfig.cs              — 职业配置（Phase 2 微调默认配置）

Assets/Tactics/Scripts/Common/Skills/Graph/
├── SkillGraphAsset.cs             — 图数据定义（不改）
└── SkillGraphRunner.cs            — 图执行器（不改）
```

## Scope

### In Scope

- 解耦 AttackActionNode 对 GenericAbilityImpl 的直接依赖
- 引入 IAbilityCombatQueryable 统一战斗元数据查询
- 为 Unit/RoleConfig 提供 SkillGraph 优先的默认配置路径
- 更新 Editor 工具默认创建 SkillGraph 资产
- 补充 PlayMode 测试覆盖新路径
- 标记旧系统类型为 `[Obsolete]`

### Out of Scope

- 删除 GenericAbilityImpl / AbilityEffect / TargetingStrategy（旧系统保留为兼容层）
- 迁移已有的 16 个 SkillGraph 资产（已完成）
- 修改 IAiExecutableAbility 接口签名
- 修改 IAbility 接口签名
- 网络同步层改造

---

## Phase 1: 解耦 AttackActionNode（核心阻塞点）

**目标**：消除 AttackActionNode 对 GenericAbilityImpl 的硬编码依赖，使其同时支持两种实现

### Task 1.1: 创建 IAbilityCombatQueryable 接口

- **目标**：定义统一的战斗元数据查询契约
- **输入**：AiContextBuilder.BuildAbilityMetadata 的现有模式
- **输出**：新文件 `IAbilityCombatQueryable.cs`
- **涉及文件**：
  - Create: `Assets/Tactics/Scripts/Common/Units/abilities/IAbilityCombatQueryable.cs`
- **验收标准**：
  - 接口包含 `bool HasDamageEffect { get; }` 属性
  - 接口包含 `int AttackRange { get; }` 属性
  - 接口包含 `bool IsMeleeRange { get; }` 属性
  - 文件编译通过

**接口定义**：

```csharp
namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// 战斗元数据查询接口。
    /// 抽象 AI 行为树与具体能力实现之间的战斗信息耦合。
    /// </summary>
    public interface IAbilityCombatQueryable
    {
        bool HasDamageEffect { get; }
        int AttackRange { get; }
        bool IsMeleeRange { get; }
    }
}
```

**设计说明**：不暴露 `Effects` 和 `TargetingStrategy`，因为：
1. `SingleTargetEnemy._maxRange` 是私有字段无 setter，SkillGraphAbilityImpl 无法构造
2. AttackActionNode 实际只需要：是否有伤害效果、是否近战
3. 最小接口原则 —— 只暴露消费方真正需要的信息

### Task 1.2: GenericAbilityImpl 实现 IAbilityCombatQueryable

- **目标**：使旧运行时实现新接口，零行为变更
- **输入**：IAbilityCombatQueryable 接口定义
- **输出**：GenericAbilityImpl 实现接口
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/Units/abilities/GenericAbilityImpl.cs` L20（类声明）、L45 附近（新增属性）
- **验收标准**：
  - 类声明添加 `IAbilityCombatQueryable`
  - `HasDamageEffect` 返回 `_config.Effects.Any(e => e is DamageEffect)`
  - `AttackRange` 返回 `(_config.TargetingStrategy as SingleTargetEnemy)?.MaxRange ?? 1`
  - `IsMeleeRange` 返回 `AttackRange <= 1`
  - 编译通过，现有功能无回归

**修改内容**：

```csharp
// L20: 类声明
public class GenericAbilityImpl : IAbility, IAiExecutableAbility, IAbilityCombatQueryable

// L45 附近新增：
public bool HasDamageEffect => _config.Effects?.Any(e => e is DamageEffect) == true;
public int AttackRange => (_config.TargetingStrategy as SingleTargetEnemy)?.MaxRange ?? 1;
public bool IsMeleeRange => AttackRange <= 1;
```

### Task 1.3: SkillGraphAbilityImpl 实现 IAbilityCombatQueryable

- **目标**：使新运行时实现新接口，从图节点提取战斗元数据
- **输入**：IAbilityCombatQueryable 接口定义、SkillGraphAsset 节点结构
- **输出**：SkillGraphAbilityImpl 实现接口
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs` L20（类声明）、L36 附近（新增属性和缓存）
- **验收标准**：
  - 类声明添加 `IAbilityCombatQueryable`
  - `HasDamageEffect` 检查图中是否存在 ApplyDamageNodeRecord
  - `AttackRange` 返回 `_config.TargetRange`
  - `IsMeleeRange` 返回 `_config.TargetRange <= 1`
  - 编译通过

**修改内容**：

```csharp
// L20: 类声明
public class SkillGraphAbilityImpl : IAbility, IAiExecutableAbility, IAbilityCombatQueryable

// L36 附近新增：
public bool HasDamageEffect => _config.SkillGraph?.Nodes?.Any(n => n is ApplyDamageNodeRecord) == true;
public int AttackRange => _config.TargetRange;
public bool IsMeleeRange => _config.TargetRange <= 1;
```

### Task 1.4: 重构 AttackActionNode 使用 IAbilityCombatQueryable

- **目标**：将 AttackActionNode 从 GenericAbilityImpl 硬依赖改为接口驱动
- **输入**：IAbilityCombatQueryable 接口
- **输出**：AttackActionNode 支持两种实现
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/ai/behaviourTrees/customNodes/AttackActionNode.cs` L98, L124-140
- **验收标准**：
  - `FindAttackAbility` 返回类型从 `GenericAbilityImpl` 改为 `IAbility`
  - 使用 `IAbilityCombatQueryable` 而非 `OfType<GenericAbilityImpl>()` 进行过滤
  - `ExecuteAttackAbility` 参数类型从 `GenericAbilityImpl` 改为 `IAbility`
  - 通过 `IAiExecutableAbility` 调用 `ExecuteEffectsAsync`
  - 编译通过

**修改内容**：

```csharp
// L98: 参数类型变更
private async void ExecuteAttackAbility(IUnit target, IAbility attackAbility, TaskCompletionSource<bool> tcs)

// L124-140: FindAttackAbility 重构
private static IAbility FindAttackAbility(IUnit unit)
{
    var attackAbilities = unit.GetBaseAbilities()
        .Where(a => a is IAbilityCombatQueryable combat && combat.HasDamageEffect)
        .ToList();

    if (!attackAbilities.Any())
        return null;

    // Priority: Melee (range <= 1) > Any
    return attackAbilities.FirstOrDefault(a =>
            a is IAbilityCombatQueryable combat && combat.IsMeleeRange)
        ?? attackAbilities.First();
}

// L54-58: Execute 方法中 CanPerform 调用保持不变（IAbility 已有此方法）
// L91: ExecuteAttackAbility 调用保持不变
// L104: ExecuteEffectsAsync 通过 IAiExecutableAbility 调用
```

**L104 处的调用需要类型转换**：

```csharp
// L102-106 修改为：
if (attackAbility is IAiExecutableAbility executable)
{
    await executable.ExecuteEffectsAsync(new List<IUnit> { target }, _gridController);
    tcs.SetResult(true);
}
```

### Phase 1 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| SkillGraphAbilityImpl 节点类型检查不完整 | AI 无法识别某些图能力为攻击 | 以 AiContextBuilder.BuildAbilityMetadata 的节点映射为权威参考 |
| GenericAbilityImpl 的 HasDamageEffect 与现有 LINQ 逻辑不一致 | 攻击能力识别差异 | 保持与原 L128 行 `Effects.Any(e => e is DamageEffect)` 完全等价 |
| IAiExecutableAbility 类型转换失败 | ExecuteEffectsAsync 无法调用 | GenericAbilityImpl 和 SkillGraphAbilityImpl 均实现该接口，转换必定成功 |

---

## Phase 2: 默认配置迁移

**目标**：新创建的 Unit/RoleConfig 默认使用 SkillGraphAbilityConfig

### Task 2.1: Unit.cs 默认 Move 配置支持 SkillGraph 路径

- **目标**：CreateDefaultMoveConfig 保持兼容，Unit 初始化逻辑增加 SkillGraph 优先判断
- **输入**：Unit.cs L250-271 现有逻辑
- **输出**：Unit 初始化优先使用 SkillGraph 资产
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/Units/Unit.cs` L250-271
- **验收标准**：
  - 当 `_roleConfig` 提供 SkillGraphAbilityConfig 资产时，创建 SkillGraphAbilityImpl
  - 当无 SkillGraph 资产时，回退到现有 AbilityConfig.CreateDefaultMoveConfig()
  - 旧 Unit 资产（序列化了 AbilityConfig 列表）仍正常工作
  - 编译通过

**修改内容**：

```csharp
// L266-271: Move 能力回退逻辑不变
// AbilityConfig.CreateDefaultMoveConfig() 创建的是 AbilityConfig 实例
// 其 CreateAbility() 返回 GenericAbilityImpl —— 这是预期行为
// 因为 Move 图资产可能尚未创建，保留旧路径作为回退

// L250-264: 能力创建循环不变
// config.CreateAbility(this) 的多态调用已自动支持 SkillGraphAbilityConfig
// 无需修改 —— 这是 Phase 1 完成后的自然结果
```

**注意**：此 Task 实际修改量极小。Unit.cs 的核心逻辑已通过 `config.CreateAbility(this)` 的虚方法调用天然支持两种配置。关键验证点是确认序列化兼容性。

### Task 2.2: RoleConfig 默认资产类型标注

- **目标**：在 Inspector 中引导用户创建 SkillGraphAbilityConfig
- **输入**：RoleConfig.cs L22-23
- **输出**：RoleConfig 字段添加类型约束或提示
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/Units/Classes/RoleConfig.cs` L22-23
- **验收标准**：
  - Inspector 中 `_abilities` 列表的创建按钮默认显示 SkillGraphAbilityConfig
  - 已有的 AbilityConfig 资产仍可拖入列表
  - 编译通过

**修改内容**：

```csharp
// L22-23: 添加 Odin 类型约束提示
[ListDrawerSettings(...)]
[Tooltip("优先使用 SkillGraphAbilityConfig。旧 AbilityConfig 仍可用但不推荐。")]
private List<AbilityConfig> _abilities = new List<AbilityConfig>();
```

### Task 2.3: SkillGraphAbilityConfig 增强 Move 支持

- **目标**：为未来完全移除旧系统做准备，提供 Move 图配置路径
- **输入**：AbilityConfig.CreateDefaultMoveConfig() 逻辑
- **输出**：SkillGraphAbilityConfig 可选地支持 Move 图
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityConfig.cs`
  - 可能 Create: Move SkillGraph 资产
- **验收标准**：
  - 存在一个 Move 专用的 SkillGraphAsset 资产（仅含 MoveEffect 节点）
  - SkillGraphAbilityConfig 可引用此资产
  - Unit 初始化中可识别 Move 图能力
  - 编译通过

### Phase 2 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 旧 Unit 资产序列化了 AbilityConfig 引用 | 反序列化失败 | 不改字段类型，AbilityConfig 是基类，子类自动兼容 |
| Move 图资产不存在 | 默认 Move 能力创建失败 | 保留 CreateDefaultMoveConfig 旧路径作为回退 |
| Inspector 用户误选旧类型 | 新 Unit 使用旧系统 | 通过 Tooltip 和文档引导 |

---

## Phase 3: Editor 工具与标记更新

**目标**：更新开发工具，默认走新路径；标记旧系统为废弃

### Task 3.1: 标记旧系统类型为 Obsolete

- **目标**：IDE 警告引导开发者使用新系统
- **输入**：GenericAbilityImpl、AttackAbilityConfig
- **输出**：添加 `[Obsolete]` 特性
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/Units/abilities/GenericAbilityImpl.cs` L20
  - Modify: `Assets/Tactics/Scripts/Common/Units/abilities/AttackAbilityConfig.cs`
- **验收标准**：
  - GenericAbilityImpl 类声明添加 `[Obsolete("Use SkillGraphAbilityImpl instead. This class is kept for backward compatibility.")]`
  - AttackAbilityConfig 类声明添加 `[Obsolete("Use SkillGraphAbilityConfig instead.")]`
  - 编译产生 CS0618 警告（非错误）
  - 所有内部使用处添加 `#pragma warning disable CS0618` 以抑制噪音

### Task 3.2: Editor 菜单默认创建 SkillGraph 资产

- **目标**：右键菜单默认创建 SkillGraphAbilityConfig 而非 AbilityConfig
- **输入**：现有 `[CreateAssetMenu]` 属性
- **输出**：菜单优先级调整
- **涉及文件**：
  - Modify: `Assets/Tactics/Scripts/Common/Units/abilities/AbilityConfig.cs` L14
  - Modify: `Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityConfig.cs` L11
- **验收标准**：
  - SkillGraphAbilityConfig 的菜单项排在 AbilityConfig 之前
  - 两种菜单项均可正常使用

### Task 3.3: 添加迁移验证工具

- **目标**：Editor 工具检查项目中残留的旧系统使用
- **输入**：现有 Editor 工具体系
- **输出**：验证窗口或菜单项
- **涉及文件**：
  - Create: Editor 脚本（路径待定，参考现有 Editor 工具目录）
- **验收标准**：
  - 扫描所有 Unit 资产，报告使用旧 AbilityConfig 的实例
  - 扫描所有 RoleConfig 资产，报告使用旧 AbilityConfig 的实例
  - 扫描代码中 `OfType<GenericAbilityImpl>()` 的硬编码引用
  - 输出可读的迁移报告

### Phase 3 风险与缓解

| 风险 | 缓解措施 |
|------|---------|
| Obsolete 警告过多淹没真正问题 | 内部调用处使用 `#pragma warning disable` |
| 验证工具误报 | 仅报告，不自动修复；人工确认后执行 |

---

## Phase 4: 测试补全

**目标**：确保迁移后新旧路径均正常工作

### Task 4.1: AttackActionNode 新路径 PlayMode 测试

- **目标**：验证 AttackActionNode 能找到并执行 SkillGraphAbilityImpl
- **涉及文件**：
  - Create/Modify: PlayMode 测试文件（参考现有测试目录）
- **验收标准**：
  - 测试用 Unit 配置 SkillGraphAbilityConfig（含 DamageEffect 图节点）
  - AttackActionNode.FindAttackAbility 能找到该能力
  - ExecuteEffectsAsync 正确执行
  - 无 GenericAbilityImpl 参与

### Task 4.2: 混合配置兼容性测试

- **目标**：验证同一 Unit 可混合使用两种配置
- **涉及文件**：
  - Create/Modify: PlayMode 测试文件
- **验收标准**：
  - Unit 同时拥有 AbilityConfig（Move）和 SkillGraphAbilityConfig（Attack）
  - Move 能力通过旧系统正常工作
  - Attack 能力通过新系统正常工作
  - AI 行为树正确选择攻击能力

### Task 4.3: 回归测试

- **目标**：确认旧路径未被破坏
- **涉及文件**：
  - 运行现有 12+ PlayMode 测试
- **验收标准**：
  - 所有现有测试通过
  - 无新的编译错误或警告（除 Obsolete 警告）

### Phase 4 风险与缓解

| 风险 | 缓解措施 |
|------|---------|
| 测试环境缺少图资产 | 使用现有 16 个图资产之一 |
| 行为树测试需要完整场景 | 使用最小场景 + mock |

---

## 关键依赖验证清单

| 依赖点 | 文件:行号 | 当前状态 | 迁移后状态 | 需要修改 |
|--------|----------|---------|-----------|---------|
| `_abilityConfigs` 字段类型 | Unit.cs:64 | `List<AbilityConfig>` | 不变（多态兼容） | 否 |
| 能力创建循环 | Unit.cs:256-263 | `config.CreateAbility(this)` | 不变（虚方法调用） | 否 |
| `CreateDefaultMoveConfig()` | Unit.cs:269 / AbilityConfig:67 | 静态工厂，返回 AbilityConfig | 保留旧路径 | 否 |
| `_abilities` 字段类型 | RoleConfig.cs:23 | `List<AbilityConfig>` | 不变 | 否 |
| `GenericAbilityImpl._config` | GenericAbilityImpl.cs:26 | 持有 AbilityConfig | 不变 | 否 |
| `Config.Effects` 访问 | AttackActionNode.cs:128 | 直接访问 GenericAbilityImpl | 改为 IAbilityCombatQueryable | **是** |
| AI 双路径支持 | AiContextBuilder.cs:112-193 | 已支持两种实现 | 不变 | 否 |
| IAiExecutableAbility 调用 | IntentExecutor.cs | 通过接口调用 | 不变 | 否 |

## Assumptions

1. 现有 16 个 SkillGraph 资产已覆盖所有旧 AbilityConfig 的功能
2. Move 图资产可延后创建，旧 `CreateDefaultMoveConfig()` 路径足够
3. 不需要修改 IAbility 或 IAiExecutableAbility 接口签名
4. Odin Inspector 的序列化机制允许 List<AbilityConfig> 混合存储子类实例
5. `ApplyDamageNodeRecord` 等图节点类型在 `SkillGraphAbilityImpl` 的程序集中可访问

## Self-Review

| 检查项 | 结果 |
|--------|------|
| 需求覆盖 | 6 项设计要求全部有对应 Task |
| 占位符扫描 | 无 TBD/TODO |
| 一致性 | `IAbilityCombatQueryable` 在所有 Task 中命名一致 |
| 验收标准可执行 | 每个 Task 有编译/测试/行为验证 |
| 未授权范围 | 未修改 IAbility/IAiExecutableAbility 接口、未改项目结构 |
| 接口最小化 | IAbilityCombatQueryable 仅 3 个属性，无冗余暴露 |
