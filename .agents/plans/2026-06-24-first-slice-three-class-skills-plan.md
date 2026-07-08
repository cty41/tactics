# 第一版三职业技能实现任务文档

## Background

- 当前问题：pure run 三人小队的职业、专长、技能框架已经基本收敛，但仍停留在设计层。若直接整体实现，会同时碰到地图、成长、结算、技能、UI 多个系统，风险过高。
- 当前现状：
  - 三个核心职业已明确：`法师`、`死灵法师`、`亚马逊女战士`
  - 三职业都已具备 3 条职业专长线骨架
  - 通用专长池、生存/资源/机会类规则、属性与专长门槛规则已成文
  - 法师大部分关键技能已经有实现定义；死灵法师与亚马逊也已形成足够清晰的技能树骨架
- 目标：把“三职业技能系统”单独切成第一版实现任务，先跑通开局随机基础专长、基础技能战斗表现、部分高级技能分化，以及 UI/候选逻辑的主链路。
- 预期收益：
  - 先验证三职业是否各自站得住，而不被地图和成长全系统绑架
  - 为后续 vertical slice 地图整合提供稳定的职业输入面
  - 把大师技能和复杂技能延后，降低第一版实现复杂度

## Scope

### In Scope

- 三职业基础专长自动解锁基础技能的链路
- 三职业基础技能实现
- 三职业第一批关键高级技能实现
- 职业专长与技能候选前置规则
- 专长解锁型特殊技能的自动升级提示链路
- 与技能实现强绑定的最小 UI 改造

### Out of Scope

- 通用专长池的完整实现
- 地图、节点、掉落、商店、结算全链路
- 大师技能第一版完整实现
- 全部技能数值平衡
- 最终表现层、美术、完整特效与音效

## File Structure

- `Assets/Tactics/Scripts/Common/Units/Classes/RoleConfig.cs` — 职业定义入口，承接基础专长与技能池关系
- `Assets/Tactics/Scripts/Common/Battle/SkillSystem.cs` — 技能候选、学习、前置规则与特殊技能升级规则
- `Assets/Tactics/Scripts/Common/Battle/SkillDatabase.cs` — 技能数据注册与查询
- `Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs` — 升级后技能/专长奖励流程的接入点
- `Assets/Tactics/Scripts/UI/LevelUpPanelController.cs` — 升级 UI，承接专长二选一、技能二选一与特殊技能自动升级提示
- `Assets/Tactics/Scripts/Common/Units/Unit.cs` — 运行时属性、技能效果、状态和战斗行为挂载点
- `Assets/Tactics/Scripts/Common/Battle/CombatComponent.cs` — 技能伤害、状态、 projectile 等结算入口
- `Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/` — 可复用现有技能图配置，补齐法师/死灵/亚马逊技能 asset
- `Assets/Tactics/Battle/Classes/` 或 `Assets/Tactics/ScriptableObjects/Roles/` — 三职业 Role asset 与默认技能入口
- `Tests/gameplay-specs/` — 三职业基础技能和升级链路的验证用例文档

## Assumptions

- 第一版允许复用现有技能图、角色表现链路和基础 projectile/状态系统
- 专长与技能候选的逻辑优先在数据层与 UI 层打通，不要求第一版做到最终表现完整
- 大师专长可存在，但对应大师技能若未实现，可先不进入技能候选

## Tasks

### Task 1: 打通三职业基础专长入场链路

- 目标：实现“职业开局随机一个基础专长，并自动获得对应基础技能”。
- 输入：三职业职业专长线骨架、基础技能清单。
- 输出：法师/死灵/亚马逊都能在开局拿到一条基础专长和对应技能。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/Common/Units/Classes/RoleConfig.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillSystem.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillDatabase.cs`
  - Modify: 三职业 Role asset / 配置入口
- 验收标准：
  - 每个职业至少存在 3 条基础专长线配置
  - 开局时系统会随机选中其中 1 条基础专长
  - 基础专长会自动授予对应基础技能
  - 基础技能不占用技能二选一

### Task 2: 实现三职业 9 个基础技能

- 目标：让三职业开局技能都能在战斗中正确使用。
- 输入：当前已讨论的技能定义。
- 输出：三职业基础技能可施放、可结算、可显示基础反馈。
- 涉及文件：
  - Modify/Create: 三职业对应技能 asset、脚本或技能图配置
  - Modify: `Assets/Tactics/Scripts/Common/Battle/CombatComponent.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Units/Unit.cs`
- 技能清单：
  - 法师：`火球术`、`寒冰箭`、`霹雳闪电`
  - 死灵法师：`召唤骷髅`、`虚弱诅咒`、`骨矛`
  - 亚马逊：`突刺`、`毒矛`、`战斗技巧`
- 验收标准：
  - 9 个技能都能在战斗内成功触发
  - 目标选择、命中规则、状态施加与数值结算符合设计文档
  - 至少为 `火球术`、`寒冰箭`、`霹雳闪电`、`毒矛`、`骨矛` 补齐 projectile / 直击 / debuff 规则

### Task 3: 实现三职业第一批关键高级技能

- 目标：让每个职业至少有一个高级技能可用于验证 build 分化。
- 输入：三职业高级技能定义。
- 输出：三职业的第一个高级技能闭环可用。
- 涉及文件：
  - Modify/Create: 高级技能 asset / 脚本 / 技能图
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillSystem.cs`
  - Modify: `Assets/Tactics/Scripts/UI/LevelUpPanelController.cs`
- 高级技能清单：
  - 法师：`瞬移术`、`召唤火魔`、`冰甲`（优先至少实现 1 个）
  - 死灵法师：`骷髅法师`、`恐惧诅咒`、`骨盾`（优先至少实现 1 个）
  - 亚马逊：`连续刺击`、`回收长矛`、`分身`（优先至少实现 1 个）
- 验收标准：
  - 每个职业至少有 1 个高级技能进入可实现状态
  - 高级技能需要通过对应高级专长前置进入技能候选
  - 高级技能出现时占用一次技能二选一

### Task 4: 实现专长前置与属性门槛过滤

- 目标：让专长和技能候选池按设计规则正确过滤。
- 输入：专长规则、属性门槛、固定池抽取规则。
- 输出：候选池不会出现不满足前置或主属性门槛的项。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillSystem.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillDatabase.cs`
  - Optional Modify: 升级流程相关数据模型
- 验收标准：
  - 基础/高级/大师专长按 `5 / 7 / 9` 的主属性门槛过滤
  - 技能候选必须满足专长前置和主属性值限制
  - 不同职业共享同一套候选过滤规则

### Task 5: 实现专长解锁技能的自动升级提示

- 目标：让特殊技能的升级方式与普通技能选择分离，但仍对玩家可见。
- 输入：特殊技能升级规则。
- 输出：高级/大师专长推进后，已获得的特殊技能自动升级，并在 UI 中提示。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/UI/LevelUpPanelController.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillSystem.cs`
  - Modify: 升级队列与结算流程入口
- 验收标准：
  - `高级 / 大师技能` 首次获得时仍占用技能二选一
  - 技能后续升级不再占用技能二选一
  - 当专长阶段推进时，相关技能会自动升级
  - UI 会正确提示自动升级结果

### Task 6: 第一版暂缓内容的保护规则

- 目标：避免未实现的大师技能和复杂技能误入候选池。
- 输入：当前三职业完整技能树设计。
- 输出：第一版只开放已实现技能，不因专长存在而暴露未完成技能。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillDatabase.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Battle/SkillSystem.cs`
- 验收标准：
  - 未实现的大师技能不会进入技能候选
  - 已实现的大师专长仍可获得，并只承担强化已有技能的职责
  - 第一版技能池与文档范围一致，不会出现“候选可见但功能未接”的死链

## Risks & Open Questions

- `亚马逊` 的第三条线已从旧的 `盾步反攻` 改为 `被动与魔法专长`，实现时要避免误用旧命名和旧技能占位。
- `死灵法师` 与 `法师` 的召唤、尸体、debuff 系统如果过度绑定现有旧职业规则，后续扩展会很重。
- `大师专长存在但大师技能未实现` 的过渡期逻辑必须非常明确，否则玩家会误以为系统坏掉。
- 专长前置、属性门槛、特殊技能自动升级三套规则叠加后，升级 UI 复杂度会明显提高。

## 验证方式

- 开局多次新建 run，确认三职业都能随机获得基础专长并自动拿到对应技能。
- 在战斗内分别验证三职业基础技能的命中、状态、消耗和反馈。
- 至少验证每个职业 1 个高级技能能通过专长前置进入技能候选并正常使用。
- 验证当专长阶段提升时，已获得的特殊技能会自动升级且显示提示。
- 验证未实现的大师技能不会进入候选池。

## 推荐执行顺序

1. Task 1：基础专长入场链路
2. Task 2：三职业 9 个基础技能
3. Task 4：专长前置与属性门槛过滤
4. Task 3：第一批关键高级技能
5. Task 5：特殊技能自动升级提示
6. Task 6：第一版暂缓内容保护规则
