# 旧 AbilityConfig 依赖分析报告

> 分析日期：2026-06-23

## 架构概览

项目存在三层 Ability 体系：

| 层级 | 核心类型 | 执行方式 | 状态 |
|------|----------|----------|------|
| **旧 MonoBehaviour** | `AttackAbility` 等 | 挂载式 | ✅ 已废弃删除，有迁移工具 |
| **中层 AbilityConfig** | `AbilityConfig` + `AbilityEffect` + `TargetingStrategy` | Effect 链顺序执行 (`GenericAbilityImpl`) | ⚠️ 当前主力，需迁移 |
| **新层 SkillGraph** | `SkillGraphAbilityConfig` + `SkillGraphAsset` | 图节点执行 (`SkillGraphRunner`) | ✅ 新系统，正在替代 |

桥接机制：`SkillGraphAbilityConfig : AbilityConfig`，重写 `CreateAbility()` 返回 `SkillGraphAbilityImpl`，可无缝替换 `Unit._abilityConfigs` 列表中的条目。

---

## 战斗逻辑中仍依赖旧 AbilityConfig 的代码

### A. 直接战斗运行时（核心影响）

| # | 文件 | 行号 | 依赖点 | 说明 |
|---|------|------|--------|------|
| 1 | `GenericAbilityImpl.cs` | 26, 45-47 | 持有 `AbilityConfig _config`，读取 `_config.Effects` 和 `_config.TargetingStrategy` | **旧效果链执行器**，是旧系统的核心运行时。所有未迁移为 SkillGraph 的能力都走这条路 |
| 2 | `Unit.cs` | 64 | `[SerializeField] List<AbilityConfig> _abilityConfigs` | 单位直接序列化旧 AbilityConfig 列表（回退路径） |
| 3 | `Unit.cs` | 269 | 调用 `AbilityConfig.CreateDefaultMoveConfig()` | 移动能力使用旧 AbilityConfig 静态工厂 |
| 4 | `RoleConfig.cs` | 23, 28, 35 | `[SerializeField] List<AbilityConfig> _abilities` | 职业配置持有旧 AbilityConfig 列表，是单位技能的主要来源 |

### B. AI 系统

| # | 文件 | 行号 | 依赖点 | 说明 |
|---|------|------|--------|------|
| 5 | `IntentExecutor.cs` | 129, 225 | 遍历 `AbilityInfo` 查找含 `DamageEffect` 的 AbilityConfig | AI 意图执行器假设能力有 `AbilityEffect` 子类型 |
| 6 | `AttackActionNode.cs` | 16, 109 | 注释和日志引用 AbilityConfig 系统 | 行为树攻击节点的旧逻辑残留 |

### C. AbilityEffect / TargetingStrategy（旧系统的数据定义）

| # | 文件 | 说明 |
|---|------|------|
| 7 | `AbilityEffect.cs` | 效果基类 + 8 个子类（Damage/Heal/Move/Buff/DoT/AccuracyDamage/Knockback/Spawn） |
| 8 | `TargetingStrategy.cs` | 选目标策略基类 + 7 个子类（Self/SingleEnemy/SingleAlly/AoE/MultiEnemy/MoveThenAttack/MoveThenHeal） |
| 9 | `AbilityCommand.cs` | 命令模式封装，调用 `GenericAbilityImpl.ExecuteEffectsAsync()` |
| 10 | `AttackAbilityConfig.cs` | 空子类，仅用于向后兼容标记 |
| 11 | `IAiExecutableAbility.cs` | AI 可执行能力接口，与旧系统耦合 |

### D. 测试系统

| # | 文件 | 行号 | 依赖点 |
|---|------|------|--------|
| 12 | `SkillGameplayStepAdapter.cs` | 30, 50, 66-67, 314-335 | 动态创建 `SkillGraphAbilityConfig`（桥接层，非直接旧依赖） |
| 13 | `GameplayRuntimeContext.cs` | 27, 94, 99 | `Dictionary<string, SkillGraphAbilityConfig>` |
| 14 | `SkillGraphRuntimeTests.cs` | 405-406 | 断言 AbilityConfig bridge 存在 |

### E. Editor 工具（非运行时，但维护旧系统）

| # | 文件 | 说明 |
|---|------|------|
| 15 | `AbilityConfigSetup.cs` | 创建旧 AbilityConfig 资产 |
| 16 | `AbilityConfigMigrationTool.cs` | 从 MonoBehaviour 迁移到 AbilityConfig |
| 17 | `CreateDefaultAbilityConfigs.cs` | 批量创建旧 AbilityConfig |
| 18 | `RoleSystemSetupEditor.cs` | 加载/创建各角色 AbilityConfig |
| 19 | `SkillGraphLegacyAbilityAudit.cs` | 审计旧版 AbilityConfig 资产迁移状态 |

---

## SkillGraph 资产实际存在情况（已验证）

**Batch2-4 和 SpecialCase 的能力几乎全部已有 SkillGraph 实现**：

| 能力 | 旧 AbilityConfig | SkillGraph | SkillGraphAbilityConfig | 备注 |
|------|:---:|:---:|:---:|------|
| MeleeAttack | ✅ | ✅ | ✅ | Batch1，已就绪 |
| RangedAttack | ✅ | ✅ | ✅ | Batch2 |
| MagicAttack | ✅ | ✅ | ✅ | Batch2 |
| HeavyShot | ✅ | ✅ | ✅ | Batch2 |
| Fireball | ✅ | ✅ | ✅ | Batch2 |
| Freeze | ✅ | ✅ | ✅ | Batch3 |
| Mark | ✅ | ✅ | ✅ | Batch3 |
| Counter | ✅ | ✅ | ✅ | Batch3 |
| Uppercut | ❌ | ✅ | ✅ | Batch4，无旧资产 |
| ChargeHeal | ✅ | ✅ | ✅ | Batch4 |
| MeleeHeal | ✅ | ✅ | ✅ | Batch4 |
| Move | ✅ | ✅ | ✅ | SpecialCase |
| **ChargeAttack** | **❌** | **❌** | **❌** | **唯一完全缺失，ChargeStrike_Lv1 为替代** |
| FrostNova | — | ✅ | ✅ | 额外能力 |
| ChargeStrike_Lv1 | — | ✅ | ✅ | ChargeAttack 的替代 |
| AreaBlast_Lv1 | — | ✅ | ✅ | 额外能力 |

**结论**：LegacyAudit 中的状态标记（`NeedsProjectileSemantic` 等）已过时——实际资产层面 Batch2-4 的 SkillGraph 三件套已全部创建（ChargeAttack 除外，已由 ChargeStrike_Lv1 替代）。审计工具可退役。

**Necromancer 相关**：项目中未找到任何 necromancer 文件，确认在 main 分支重写为 SkillGraph。

**LegacyAudit 工具**：迁移基本完成后可退役（硬编码状态已过时，`HasSkillGraphBridge` 动态检测仍准确但不再需要）。

---

## 总结：仍依赖旧 AbilityConfig 的代码

### 运行时战斗代码（必须迁移才能移除旧系统）

| 文件 | 依赖点 | 迁移后可移除 |
|------|--------|:---:|
| `GenericAbilityImpl.cs` | 旧效果链执行器，读取 `AbilityConfig.Effects` / `TargetingStrategy` | ✅ |
| `Unit.cs:64` | `[SerializeField] List<AbilityConfig> _abilityConfigs` 回退字段 | ✅ |
| `Unit.cs:269` | `AbilityConfig.CreateDefaultMoveConfig()` 静态工厂 | ✅ |
| `RoleConfig.cs:23` | `[SerializeField] List<AbilityConfig> _abilities` | ✅ |
| `IntentExecutor.cs:129,225` | 查找 `DamageEffect` 子类型 | 需适配 |
| `AttackActionNode.cs:16,109` | 旧逻辑残留 | 需适配 |

### 旧系统数据定义（GenericAbilityImpl 消费，SkillGraph 不使用）

- `AbilityEffect.cs`（8 个效果子类）
- `TargetingStrategy.cs`（7 个策略子类）
- `AbilityCommand.cs`
- `AttackAbilityConfig.cs`（空子类）
- `IAiExecutableAbility.cs`

### Editor 工具（可随旧系统一起退役）

- `AbilityConfigSetup.cs` — 创建旧 AbilityConfig 资产
- `AbilityConfigMigrationTool.cs` — 从 MonoBehaviour 迁移到 AbilityConfig（已完成使命）
- `CreateDefaultAbilityConfigs.cs` — 批量创建旧 AbilityConfig
- `RoleSystemSetupEditor.cs` — 加载/创建各角色 AbilityConfig
- `SkillGraphLegacyAbilityAudit.cs` — 审计旧版迁移状态（**可退役**，迁移已完成，硬编码状态已过时）

### 待确认

1. **Prefab/RoleConfig 绑定**：SkillGraph 资产已全部创建，但 RoleConfig 和 Unit prefab 的序列化字段是否已切换为引用 `SkillGraphAbilityConfig`？需在 Unity Editor 中检查。
2. **Necromancer 相关能力**：确认在 main 分支重写为 SkillGraph。
