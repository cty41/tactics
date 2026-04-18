# Ability 系统优化：去除 MonoBehaviour 继承

## 背景分析

当前 `Ability` 继承自 `MonoBehaviour`，但实际情况是：

1. **Ability 抽象基类** (`Ability.cs`) 继承 `MonoBehaviour`，仅因为：
   - 需要 `[SerializeField]` 字段来配置 `_isRangedDamage` 和 `_hasHalfScaling`
   - 需要在 Inspector 中查看和配置

2. **具体 Ability 组件** (如 `AttackAbility`, `MoveAbility`, `FireballAbility` 等) 继承自 `Ability`，同样挂载在 Unit 的 GameObject 上，但它们实际只是 **薄代理层**，所有逻辑都转发给对应的 `*Impl` 类。

3. **Impl 类** (`*AbilityImpl`) 已经是纯 C# 类，不依赖 Unity，实现所有业务逻辑。

## 核心问题

- `Ability` 继承 `MonoBehaviour` 只是为了能够在 Inspector 中配置参数
- 每次 Unit 初始化时通过 `GetComponents<Ability>()` 获取挂载的 Ability 组件
- 每个 Unity 侧的 Ability 类都写了一遍几乎相同的转发代码（boilerplate）

## 设计方案

### 方案：AbilityConfig (ScriptableObject) + Unit 管理

将 Ability 的配置从 MonoBehaviour 中解耦，使用 ScriptableObject 管理配置，Unit 直接管理 `IAbility` 实例列表。

### 架构变更

```
Before:
Unit (MonoBehaviour)
  └── AttackAbility (MonoBehaviour, [SerializeField] _isRangedDamage)
       └── AttackAbilityImpl (pure C#)

After:
Unit (MonoBehaviour)
  ├── [SerializeField] List<AbilityConfig> _abilityConfigs  ← Inspector 可配置
  └── List<IAbility> _abilities (managed by Unit)
       └── AttackAbilityImpl (pure C#, configured by AbilityConfig)
```

### AbilityConfig 结构

```csharp
/// <summary>
/// ScriptableObject 配置，用于在 Inspector 中定义 Ability 的参数。
/// </summary>
public abstract class AbilityConfig : ScriptableObject
{
    public abstract IAbility CreateAbility(IUnit owner);
}

/// <summary>
/// 攻击类 Ability 的配置（支持 isRangedDamage 和 hasHalfScaling）
/// </summary>
[CreateAssetMenu(menuName = "Game/Abilities/Attack Ability Config")]
public class AttackAbilityConfig : AbilityConfig, IDamageScalingAbility
{
    [SerializeField] private bool _isRangedDamage;
    [SerializeField] private bool _hasHalfScaling;

    public bool IsRangedDamage => _isRangedDamage;
    public bool HasHalfScaling => _hasHalfScaling;

    public override IAbility CreateAbility(IUnit owner)
    {
        return new AttackAbilityImpl(owner, this);
    }
}

/// <summary>
/// 移动 Ability 的配置
/// </summary>
[CreateAssetMenu(menuName = "Game/Abilities/Move Ability Config")]
public class MoveAbilityConfig : AbilityConfig
{
    [SerializeField] private bool _withConfirmation;
    [SerializeField] private bool _useTouchOptimizedControls;

    public override IAbility CreateAbility(IUnit owner)
    {
        return new MoveAbilityImpl(owner, _withConfirmation, _useTouchOptimizedControls);
    }
}
```

## 实施步骤

### Step 1: 创建 AbilityConfig 基类和具体配置类

**新建文件:**
- `Assets/Tactics/Scripts/Common/Units/abilities/AbilityConfig.cs` - 基类
- `Assets/Tactics/Scripts/Common/Units/abilities/AttackAbilityConfig.cs` - 攻击配置
- `Assets/Tactics/Scripts/Common/Units/abilities/MoveAbilityConfig.cs` - 移动配置
- `Assets/Tactics/Scripts/Common/Units/abilities/FireballAbilityConfig.cs` - 火球配置
- `Assets/Tactics/Scripts/Common/Units/abilities/MeleeHealAbilityConfig.cs` - 治疗配置
- `Assets/Tactics/Scripts/Common/Units/abilities/RangedAttackAbilityConfig.cs` - 远程攻击配置
- `Assets/Tactics/Scripts/Common/Units/abilities/MeleeAttackAbilityConfig.cs` - 近战攻击配置
- `Assets/Tactics/Scripts/Common/Units/abilities/AttackRangeHighlightAbilityConfig.cs` - 攻击范围高亮配置

**设计要点:**
- `AbilityConfig` 继承 `ScriptableObject`
- 实现 `IDamageScalingAbility` 接口的配置类需要暴露 `IsRangedDamage` 和 `HasHalfScaling`
- 每个配置类提供 `CreateAbility(IUnit)` 工厂方法

### Step 2: 修改 Unit.cs - 用 AbilityConfig 替换 MonoBehaviour Ability

**修改文件:**
- `Assets/Tactics/Scripts/Common/Units/Unit.cs`

**关键变更:**
```csharp
// Before:
[SerializeField] private List<Ability> _baseAbilities;

// After:
[SerializeField] private List<AbilityConfig> _abilityConfigs;
private List<IAbility> _baseAbilities;  // runtime only

// Initialize 方法中:
foreach (var config in _abilityConfigs)
{
    var ability = config.CreateAbility(this);
    RegisterAbility(ability, gridController);
}

// Remove Reset() method that auto-adds Ability components
```

### Step 3: 修改 Unit.Initialize 和 RegisterAbility

**修改内容:**
```csharp
// Initialize 方法:
// Remove: _baseAbilities = GetComponents<Ability>().ToList();
// Replace with:
_baseAbilities = new List<IAbility>();
foreach (var config in _abilityConfigs)
{
    var ability = config.CreateAbility(this);
    RegisterAbility(ability, gridController);
}

// RegisterAbility (unchanged interface, just stores and initializes):
public virtual void RegisterAbility(IAbility ability, IGridController gridController)
{
    ability.UnitReference = this;
    _baseAbilities.Add(ability);
    ability.Initialize(gridController);
}
```

### Step 4: 删除旧的 MonoBehaviour Ability 类

**删除文件:**
- `Assets/Tactics/Scripts/Common/Units/abilities/Ability.cs` (抽象基类，继承 MonoBehaviour)
- `Assets/Tactics/Scripts/Common/Units/abilities/AttackAbility.cs`
- `Assets/Tactics/Scripts/Common/Units/abilities/MoveAbility.cs`
- `Assets/Tactics/Scripts/Common/Units/abilities/FireballAbility.cs`
- `Assets/Tactics/Scripts/Common/Units/abilities/MeleeHealAbility.cs`
- `Assets/Tactics/Scripts/Common/Units/abilities/RangedAttackAbility.cs`
- `Assets/Tactics/Scripts/Common/Units/abilities/MeleeAttackAbility.cs`
- `Assets/Tactics/Scripts/Common/Units/abilities/AttackRangeHighlightAbility.cs`

**注意:** `*AbilityImpl` 文件全部保留不变，它们已经是纯 C# 实现。

### Step 5: 删除 ThirdParty 中对应的旧文件（如果有）

需要检查并删除 `Assets/ThirdParty/TBSFramework/Scripts/units/abilities/Ability.cs` 如果存在且是同样的模式。

### Step 6: 更新 Unit.Reset() 方法

移除 `Reset()` 中自动添加 Ability 组件的逻辑，因为不再需要：

```csharp
// Before:
private void Reset()
{
    if (GetComponent<AttackAbility>() == null)
        _ = gameObject.AddComponent<AttackAbility>();
    // ...
}

// After: 
// 删除整个 Reset 方法，或保留但不再添加 Ability 组件
```

### Step 7: 处理现有 Prefab 迁移方案

由于现有 Prefab 上挂载了旧的 `Ability` MonoBehaviour 组件，需要提供迁移方案：

**方案 A（推荐）：Editor 迁移脚本**
- 创建 `AbilityMigrationTool` Editor 脚本
- 遍历所有 Unit Prefab
- 检测挂载的旧 Ability 组件
- 自动创建对应的 AbilityConfig ScriptableObject 资产
- 从 GameObject 上移除旧组件

**方案 B（手动）：**
- 在 Inspector 中手动创建 AbilityConfig 资产
- 拖入 Unit 的 `_abilityConfigs` 列表
- 移除旧 Ability 组件

### Step 8: 验证和测试

- 确保所有 Unit  prefab 正确配置了 AbilityConfig
- 测试战斗流程中 Ability 的选中、显示、执行、清理流程
- 确保 Inspector 中可以看到并配置参数

---

## 其他可类似优化的 MonoBehaviour 类

### 1. Highlighter (可优化)

**当前:** `Highlighter.cs` 继承 `MonoBehaviour`，仅有抽象方法 `Apply()`

**分析:**
- Highlighter 通过 `[SerializeReference]` 列表挂载在 Unit 上
- 实际需要 MonoBehaviour 仅因为 Unity 组件挂载机制
- 但如果需要引用场景中的 Material/Renderer 等，可能仍需 MonoBehaviour

**建议:** 评估 Highlighter 实现是否真正需要 MonoBehaviour。如果只需要对 GameObject 施加效果，可以保留 MonoBehaviour 但改为通过 Unit 上的引用直接获取，而非作为组件挂载。

**优先级:** 中

### 2. MovementRules (可优化)

**当前:** `LandUnitMovementRules`, `SeaUnitMovementRules`, `AirUnitMovementRules` 继承 `MonoBehaviour` 实现 `IMovementRules`

**分析:** 
- 这些类看起来是纯逻辑判断（能否通过某类型地形）
- 如果不需要引用其他 Unity 组件，完全可以转为纯 C# 类
- 通过 `[SerializeReference]` 在 Unit Inspector 中配置

**建议:** 转为纯 C# 类，使用 `[SerializeReference]` 在 Unit 中配置

**优先级:** 高

### 3. BehaviourTreeResource (可优化)

**当前:** 继承 `MonoBehaviour`，仅持有 `ITreeNode` 引用和 `Initialize` 方法

**分析:**
- 需要 MonoBehaviour 是为了在 Inspector 中配置具体的 BehaviourTree 实现
- 可以改为 ScriptableObject 或通过 `[SerializeReference]` 配置

**建议:** 转为 ScriptableObject 或使用 `[SerializeReference]`

**优先级:** 中

### 4. Buff (已优化 ✓)

`Buff.cs` 已经是纯 C# 抽象类，由 `BuffComponent` 管理。无需改动。

### 5. CombatComponent (已优化 ✓)

`CombatComponent.cs` 已经是纯 C# 类，由 Unit 创建和管理。无需改动。

### 6. MoveComponent (半优化)

`MoveComponent` 是抽象类，`UnityMoveComponent` 继承它处理动画。逻辑部分已分离，动画部分需要 Unity API。结构合理。

---

## 实施优先级建议

1. **P0 (本次):** Ability 系统去 MonoBehaviour 化
2. **P1:** MovementRules 去 MonoBehaviour 化（类似的纯逻辑类）
3. **P2:** BehaviourTreeResource 去 MonoBehaviour 化
4. **P3 (可选):** Highlighter 评估和优化

## 风险和注意事项

1. **Prefab 破坏风险:** 旧 Prefab 上的 Ability 组件需要通过迁移脚本处理
2. **Inspector 可见性:** ScriptableObject 配置在 Inspector 中可编辑，但需要额外创建 .asset 文件
3. **Alternative 方案:** 使用 `[SerializeReference]` 配合 `[SerializeReferenceDropdown]` 可以实现内联编辑（无需单独 .asset 文件），但需要 Unity 2021.3+ 的 Polyfill 或第三方工具

## 备选方案讨论

### 方案 B: [SerializeReference] 内联配置

如果不希望创建大量 .asset 文件，可以使用 `[SerializeReference]`：

```csharp
[SerializeField] private List<IAbilityData> _abilityData;

public interface IAbilityData { }

[Serializable]
public class AttackAbilityData : IAbilityData, IDamageScalingAbility
{
    public bool IsRangedDamage;
    public bool HasHalfScaling;
    
    public IAbility Create(IUnit owner) => new AttackAbilityImpl(owner, this);
}
```

**优点:**
- 无需创建 .asset 文件
- 所有配置在 Unit Inspector 中直接编辑
- 更符合"组件内联"的直觉

**缺点:**
- Unity 默认不支持 `[SerializeReference]` 的 Inspector 下拉选择（需要自定义 PropertyDrawer 或第三方插件如 SerializeReferenceDropdown）
- 配置无法跨 Unit 复用

**建议:** 如果项目中已有 SerializeReferenceDropdown 或类似工具，优先使用方案 B。否则方案 A（ScriptableObject）更稳定。
