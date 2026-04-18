# Ability 系统重构：数据驱动技能系统

## 用户需求

1. **移除 `_useTouchOptimizedControls`** - 不需要触屏优化逻辑
2. **移除 `_withConfirmation`** - 永远是 true，即移动始终需要双击确认
3. **Move 和 Attack 收束统一** - 某些技能可能同时包含移动和伤害（如冲锋攻击、冲锋治疗）
4. **数据驱动架构** - 使用 ScriptableObject 作为技能配置容器，AbilityEffect 和 TargetingStrategy 使用普通 `[Serializable]` 类内联序列化，采用 Effect + TargetingStrategy 模式
5. **移除 `_hasHalfScaling`** - 不再需要属性加成减半逻辑

## `_hasHalfScaling` 移除原因

原有的 `hasHalfScaling` 参数用于在复合技能（如冲锋攻击）中将属性加成减半。在新的数据驱动系统中：
- 伤害计算由 `DamageEffect` 直接处理，使用 `baseDamage` + 属性缩放
- 如果需要调整复合技能的伤害平衡，直接修改 `baseDamage` 数值即可
- 不再需要额外的 `halfScaling` 布尔标志，简化了伤害计算逻辑

---

## 重构方案：数据驱动技能系统

采用 ScriptableObject 为技能配置容器，**AbilityEffect 和 TargetingStrategy 使用普通 C# 类 + `[SerializeReference]` 内联序列化**，无需创建额外的 .asset 文件。将技能拆分为三个模块：

1. **AbilityConfig (ScriptableObject)**: 技能数据容器（名称、消耗、冷却等）
2. **AbilityEffect (`[Serializable]` 类)**: 效果逻辑（伤害、治疗、移动、AOE等）
3. **TargetingStrategy (`[Serializable]` 类)**: 目标选择策略（单体、AOE、自身等）

### 目标架构

```
Before:
Unit (MonoBehaviour)
  ├── AttackAbility (MonoBehaviour) → AttackAbilityImpl (pure C#)
  ├── MoveAbility (MonoBehaviour)   → MoveAbilityImpl (pure C#)
  └── AttackRangeHighlightAbility (MonoBehaviour) → Impl

After:
Unit (MonoBehaviour)
  ├── [SerializeField] List<AbilityConfig> _abilityConfigs ← Inspector 拖入 .asset 引用
  └── List<IAbility> _baseAbilities (runtime instances)

AbilityConfig.asset (Inspector 中配置):
  ├── displayName: "Fireball"
  ├── manaCost: 3
  ├── actionPointCost: 1
  ├── targetingStrategy ▼ [AoETargeting] (SerializeReference)
  │   ├── maxRange: 4
  │   ├── radius: 1
  │   └── shape: Cross
  └── effects ▼ [SerializeReference 列表]
       ├── [0] DamageEffect
       │   ├── baseDamage: 10
       │   ├── scalingType: Strength
       │   └── isRangedDamage: false
       └── [1] ApplyBuffEffect
           ├── buffType: Ignite
           └── duration: 3
```

### 核心组件设计

#### 1. AbilityConfig (技能数据容器)

```csharp
[CreateAssetMenu(menuName = "Game/Abilities/Ability Config")]
public class AbilityConfig : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _icon;
    [SerializeField] private string _description;

    [Header("Costs")]
    [SerializeField] private int _manaCost;
    [SerializeField] private int _actionPointCost = 1;
    [SerializeField] private float _cooldown;

    [Header("Targeting")]
    [SerializeReference] private TargetingStrategy _targetingStrategy;

    [Header("Effects")]
    [SerializeReference] private List<AbilityEffect> _effects = new List<AbilityEffect>();

    public string DisplayName => _displayName;
    public int ManaCost => _manaCost;
    public int ActionPointCost => _actionPointCost;
    public TargetingStrategy TargetingStrategy => _targetingStrategy;
    public IReadOnlyList<AbilityEffect> Effects => _effects;

    public IAbility CreateAbility(IUnit owner)
    {
        return new GenericAbilityImpl(owner, this);
    }
}
```

#### 2. AbilityEffect (效果基类 - 普通 C# 类)

```csharp
[Serializable]
public abstract class AbilityEffect
{
    public abstract Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController);
}
```

**具体效果实现:**

| 效果类 | 用途 | 配置字段 |
|--------|------|---------|
| `DamageEffect` | 造成伤害 | `_baseDamage`, `_isRangedDamage` |
| `HealEffect` | 治疗 | `_healAmount`, `_capAtMaxHealth` |
| `MoveEffect` | 移动 | `_requiresPathfinding` |
| `ApplyBuffEffect` | 施加Buff | `_buffType`, `_duration` |
| `DamageOverTimeEffect` | 持续伤害 | `_damagePerTurn`, `_duration` |
| `KnockbackEffect` | 击退 | `_distance` |
| `SpawnEffect` | 生成物 | `_prefab`, `_spawnOffset` |

示例：
```csharp
[Serializable]
public class DamageEffect : AbilityEffect
{
    [SerializeField] private float _baseDamage;
    [SerializeField] private AttributeScalingType _scalingType; // None, Strength, Agility
    [SerializeField] private bool _isRangedDamage;

    public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
    {
        foreach (var target in targets)
        {
            float damage = CalculateDamage(caster, target);
            target.ModifyHealth(-damage, caster);
            target.InvokeAttacked(new UnitAttackedEventArgs(target, caster, damage));
        }
    }

    private float CalculateDamage(IUnit caster, IUnit target)
    {
        float damage = _baseDamage;
        if (_scalingType != AttributeScalingType.None)
        {
            var scaling = CombatComponent.CalculateBaseDamageBeforeCrit(caster, _isRangedDamage) - caster.AttackFactor;
            damage += scaling;
        }
        if (UnityEngine.Random.value < CombatComponent.GetClampedCritChance(caster))
        {
            damage = CombatComponent.GetCriticalDamage(damage);
        }
        damage = target.CalculateDamageTaken(caster, damage, caster.CurrentCell, target.CurrentCell);
        return damage;
    }
}
```

#### 3. TargetingStrategy (目标选择策略 - 普通 C# 类)

```csharp
[Serializable]
public abstract class TargetingStrategy
{
    [SerializeField] protected TargetType _targetType;
    public abstract IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController);
    public abstract bool IsValidTarget(IUnit caster, IUnit target, IGridController gridController);
}
```

**具体策略实现:**

| 策略类 | 用途 | 配置字段 |
|--------|------|---------|
| `SelfTargeting` | 自身 | - |
| `SingleTargetEnemy` | 单个敌人 | `_maxRange` |
| `SingleTargetAlly` | 单个友方 | `_maxRange` |
| `AoETargeting` | 范围目标 | `_radius`, `_maxRange`, `_shape` (Circle/Cross/Line) |
| `MultiTargetEnemy` | 多个敌人 | `_maxTargets`, `_maxRange` |
| `MoveThenAttackTargeting` | 移动后攻击 | `_moveRange` |
| `MoveThenHealTargeting` | 移动后治疗 | `_moveRange`, `_healRange` |

示例：
```csharp
[Serializable]
public class AoETargeting : TargetingStrategy
{
    [SerializeField] private int _radius = 1;
    [SerializeField] private int _maxRange = 4;
    [SerializeField] private AoeShape _shape = AoeShape.Cross;

    public override IEnumerable<IUnit> GetTargets(IUnit caster, ICell selectedCell, IGridController gridController)
    {
        var aoeCells = GetAoeCells(selectedCell, gridController);
        var targets = new List<IUnit>();
        foreach (var cell in aoeCells)
        {
            targets.AddRange(cell.CurrentUnits);
        }
        return targets;
    }

    private HashSet<ICell> GetAoeCells(ICell center, IGridController gridController)
    {
        var cells = new HashSet<ICell> { center };
        if (_shape == AoeShape.Cross)
        {
            cells.UnionWith(center.GetNeighbours(gridController.CellManager));
        }
        else if (_shape == AoeShape.Circle)
        {
            cells.UnionWith(gridController.CellManager.GetCells().Where(c => c.GetDistance(center) <= _radius));
        }
        return cells;
    }
}
```

#### 4. IAbility 接口（保留）

`IAbility` 接口**保留**但角色重定义：

**保留原因：**
- `GridStateUnitSelected` 通过 `IEnumerable<IAbility>` 遍历并分发所有 UI 事件（点击、悬停、选中/取消）
- `GridController` 在 Unit 进入/离开格子、回合开始/结束时遍历 `GetBaseAbilities()` 调用对应方法
- `BattleUIController` 使用 `GetBaseAbilities().OfType<MoveAbilityImpl>()` 检查单位是否有移动能力

**IAbility 是 Grid 状态系统和效果执行之间的桥接层，不是多余的抽象。**

```
UI 事件 (GridStateUnitSelected)
    ↓
IAbility (GenericAbilityImpl) ← 事件协调器
    ↓
TargetingStrategy → 确定目标列表
    ↓
AbilityEffect.Execute() → 按顺序执行效果
```

#### 5. GenericAbilityImpl (事件协调器)

```csharp
public class GenericAbilityImpl : IAbility
{
    public event Action<IAbility> AbilitySelected;
    public event Action<IAbility> AbilityDeselected;

    private readonly IUnit _owner;
    private readonly AbilityConfig _config;
    private ICell _selectedCell;
    private IEnumerable<IUnit> _pendingTargets;

    public IUnit UnitReference { get; set; }

    public GenericAbilityImpl(IUnit owner, AbilityConfig config)
    {
        _owner = owner;
        _config = config;
        UnitReference = owner;
    }

    public void OnAbilitySelected(IGridController gridController)
    {
        // Setup phase - use targeting strategy to determine valid targets
    }

    public void Display(IGridController gridController)
    {
        if (_config.TargetingStrategy != null)
        {
            _config.TargetingStrategy.DisplayPreview(gridController);
        }
    }

    public void OnCellClicked(ICell cell, IGridController gridController)
    {
        if (!IsValidCell(cell, gridController))
        {
            gridController.GridState = new GridStateAwaitInput();
            return;
        }

        _selectedCell = cell;
        _pendingTargets = _config.TargetingStrategy.GetTargets(_owner, cell, gridController);
        ExecuteEffects(gridController);
    }

    private async void ExecuteEffects(IGridController gridController)
    {
        if (_owner.Mana < _config.ManaCost || _owner.ActionPoints < _config.ActionPointCost)
        {
            return;
        }

        _owner.Mana -= _config.ManaCost;
        _owner.ActionPoints -= _config.ActionPointCost;

        foreach (var effect in _config.Effects)
        {
            await effect.Execute(_owner, _pendingTargets, gridController);
        }

        CleanUp(gridController);
    }

    // ... other IAbility methods (CanPerform, CleanUp, etc.)
}
```

---

## 实施步骤

### Step 1: 创建 AbilityEffect 基类和具体效果

**新建文件:**
- `AbilityEffect.cs` - 抽象基类
- `DamageEffect.cs` - 伤害效果
- `HealEffect.cs` - 治疗效果
- `MoveEffect.cs` - 移动效果
- `ApplyBuffEffect.cs` - 施加Buff效果

### Step 2: 创建 TargetingStrategy 基类和具体策略

**新建文件:**
- `TargetingStrategy.cs` - 抽象基类
- `SelfTargeting.cs` - 自身目标
- `SingleTargetEnemy.cs` - 单体敌人
- `SingleTargetAlly.cs` - 单体友方
- `AoETargeting.cs` - AOE目标
- `MoveThenAttackTargeting.cs` - 移动后攻击
- `MoveThenHealTargeting.cs` - 移动后治疗

### Step 3: 创建通用 AbilityConfig

**新建文件:**
- `AbilityConfig.cs` - 通用技能配置容器

### Step 4: 创建 GenericAbilityImpl

**新建文件:**
- `GenericAbilityImpl.cs` - 通用技能实现，从 Config 读取 Effects 和 Targeting

### Step 5: 删除旧的 MonoBehaviour Ability 类

### Step 6: 修改 MoveAbilityImpl 移除 touch/confirmation 逻辑

### Step 7: 修改 Unit.cs 使用新系统

### Step 8: 迁移工具和默认配置

---

## 关键设计决策

### 1. Effects 组合 vs 硬编码 Impl

**方案 A（Effect 组合）**: 每个技能由多个 Effect 组合而成（如火球 = AOE Targeting + Damage Effect + Apply Buff Effect）
**方案 B（保留 Impl）**: 保留现有 *AbilityImpl 类，只是用 Config 配置参数

**选择方案 A**，因为：
- 真正的数据驱动，策划可自由组合效果
- 新增技能无需写代码
- 支持复杂技能链（移动→攻击→施加 Buff）

### 2. Command 系统复用

现有 Command 系统（MoveCommand, AttackCommand 等）可保留，Effect 执行时生成对应 Command：

```csharp
public class DamageEffect : AbilityEffect
{
    public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
    {
        foreach (var target in targets)
        {
            var command = new AttackCommand(target, damage);
            await command.Execute(caster, gridController);
        }
    }
}
```

### 3. Targeting + Effect 分离

- **TargetingStrategy** 负责"找目标"
- **AbilityEffect** 负责"对目标做什么"
- 一个技能可以有多个 Effect，共享同一批 Target

### 4. IAbility 接口保留

- `IAbility` 不是多余的抽象，是**事件分发层**
- `GridStateUnitSelected` 需要统一接口遍历所有技能并分发 UI 事件
- `GenericAbilityImpl` 实现 `IAbility`，接收事件后协调 Targeting + Effect 执行
- 旧的 `*AbilityImpl` 类也实现 `IAbility`，可逐步迁移

---

## 文件变更清单

### 新建文件
| 文件 | 用途 |
|------|------|
| `AbilityEffect.cs` | 效果基类 + 具体效果实现 (普通 `[Serializable]` 类) |
| `TargetingStrategy.cs` | 目标策略基类 + 具体策略实现 (普通 `[Serializable]` 类) |
| `AbilityConfig.cs` | 通用技能配置 (ScriptableObject，使用 `[SerializeReference]` 存储 Effects/Targeting) |
| `GenericAbilityImpl.cs` | 通用技能实现 (实现 IAbility，作为事件协调器) |
| `Editor/AbilityMigrationTool.cs` | 迁移工具 |
| `Editor/AbilityConfigInitializer.cs` | 默认配置初始化 |

### 修改文件
| 文件 | 变更内容 |
|------|---------|
| `Unit.cs` | `_abilityConfigs` 替换旧 Ability 列表 |
| `MoveAbilityImpl.cs` | 移除 touch/confirmation 逻辑 |
| `CombatComponent.cs` | 移除 `halfScaling` 参数，简化 `CalculateDamageDealt`/`CalculateTotalDamage`/`GetAttributeScalingBonus` 方法 |
| `IDamageScalingAbility.cs` | 移除 `HasHalfScaling` 属性 |
| `ICombatant.cs` | 更新接口方法签名，移除 `halfScaling` 参数 |

### 删除文件
| 文件 | 原因 |
|------|------|
| `Ability.cs` | MonoBehaviour 基类，事件协调职责由 GenericAbilityImpl 替代 |
| `AttackAbility.cs` | 薄代理层 |
| `MoveAbility.cs` | 薄代理层 |
| `FireballAbility.cs` | 薄代理层 |
| `MeleeHealAbility.cs` | 薄代理层 |
| `RangedAttackAbility.cs` | 薄代理层 |
| `MeleeAttackAbility.cs` | 薄代理层 |
| `AttackRangeHighlightAbility.cs` | 薄代理层 |

### 保留不变
| 文件 | 原因 |
|------|------|
| `IAbility.cs` | Grid 事件分发接口 |
| `ICommand.cs` | 命令系统接口 |
| `*Command.cs` | 所有命令结构体 |
| `*AbilityImpl.cs` (旧的) | 暂时保留，GenericAbilityImpl 成熟后可逐步废弃 |

---

## 风险和注意事项

1. **`[SerializeReference]` 多态序列化**: Unity 对 `[SerializeReference]` 的支持需要自定义 PropertyDrawer 才能在 Inspector 中显示类型选择下拉框。可使用第三方库如 [SerializeReferenceDropdown](https://github.com/karimov/SerializeReferenceDropdown) 或自行实现。
2. **Effect 执行顺序**: 技能效果按列表顺序执行，需要文档说明最佳实践
3. **Undo 支持**: 现有 Command 系统支持 Undo，Effect 组合后需要确保 Undo 正确
4. **网络同步**: 效果执行需要支持网络同步（Serialize/Deserialize）
5. **迁移成本**: 旧技能需要逐个迁移到 Effect 组合

## 后续优化（本 PR 范围外）

- `[ConditionalHide]` 根据 AbilityType 隐藏不相关的 Config 字段
- 技能组合编辑器（可视化节点编辑）
- 效果预览系统（编辑时预览效果）
- `MovementRules` 去 MonoBehaviour 化
- `BehaviourTreeResource` 去 MonoBehaviour 化
