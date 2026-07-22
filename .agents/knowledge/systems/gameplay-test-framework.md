---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将 Agent 编写的受控 gameplay spec 编译为 Unity adapters 可执行的确定性计划。
tags: [testing, gameplay, automation, unity]
timestamp: "2026-07-23T01:13:08+08:00"
status: active
catalog_scope: gameplay-test-framework
repo_paths:
  - .agents/docs/gameplay-test-framework.md
  - .agents/skills/gameplay-test-framework/SKILL.md
  - Tools/gameplay-test-spec
  - Assets/Tactics/Scripts/Common/Testing/Gameplay
  - Tests/gameplay-specs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:7cb7a7d2fa54409b28b4c40a6abc756a6b7617823974a9a9eda9848c6a9ac464
---

# Current State

Agent 编写的 `.gameplay-test.md`/`ScenarioSpec` 经 TypeScript validator 和 compiler 生成 `.plan.json`，Unity `GameplayRuntimeRunner` 再通过 Skill、Battle、Map、UI adapters 执行 setup、action 和 assertion。源 Spec 是维护对象，plan 是生成物。

框架支持真实 Unity 资产，并已有生命、法力、Buff、位置、行动状态、投射物和多阶段等专用断言。Battle adapter 还能创建并推进精确参战顺序，观察朝向、先攻、标准状态、召唤注册、技能可用性原因和有序目标选择；UI adapter 支持点击、右键、悬停、键盘输入，以及文本、样式类、子节点顺序和布局关系断言。adapter 与断言支持面以当前 schema、compiler、Unity 代码和 fixtures 为准，不再按历史 Phase 文档判断。

Map adapter 除 Pure Run 路线与胜场外，现可创建独立消耗品实例、操作角色携带/卸下、一步替换装备、购买通用商店货物，并断言背包、携带引用及商店数量/药水保底/去重。Battle adapter 可对明确单位使用角色携带药水并构造、断言 `CanReceiveHealing`；UI adapter 可验证统一 Inventory popover 和独立战斗消耗品槽。对应维护源位于 `Tests/gameplay-specs/consumables/`、`map/` 与 `ui/`，生成 plan 不手改。

Map adapter 还可断言角色稳定技能 ID 的实际等级、指定 Pure Run 候选是否包含目标等级，以及候选池是否同时含新技能和升级。`pure-run-mixed-levelup-candidates` 源 spec 覆盖 Fireball Lv1 角色在 Lv2 获得 Fireball Lv2 与新技能混合候选；跨存档绑定和真实 SkillGraph 行为由 PlayMode 测试补足。

`Tests/gameplay-specs/shared/` 是共享战斗原语的维护源，五个场景分别覆盖朝向/当前轮先攻重排、标准状态回合语义、召唤上限与最早替换、可点击禁用原因，以及可撤销的有序多段选择。对应 plan 必须由 CLI 生成，不手改。

`Tests/gameplay-specs/mage/` 以真实法师 SkillGraph 资产验证等级行为；当前场景覆盖火球术 Lv2 的主目标与正交溅射。运行时上下文区分“加载的项目资产”和“测试创建且由上下文拥有的临时资产”，Dispose 只销毁后者，避免真实资产被误销毁。

`Tests/gameplay-specs/necromancer/` 使用真实死灵法师等级资产验证尸体事务。轻量 Battle 世界中的 `spawnInteractableCorpse` 创建真实 `Corpse` 组件并按格记录实例，消耗与断言读取同一运行时状态；compiler 对尸体动作和断言显式路由到 Battle adapter，避免混合 Skill/Battle 场景被通用回退误分发。当前场景证明 Lv2 骷髅只消耗选中的一具尸体，未选尸体保留。

# Relationships

- Battle adapter 验证[Monster AI](monster-ai.md)与[Battle System](battle.md)。
- Map adapter 验证[Roguelike Run](roguelike-run.md)。
- Skill adapter 验证[SkillGraph](skill-graph.md)。
- 严格事件顺序、动画完成断言和 CI 接入记录在[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

修改 Spec 工具、adapter 或 fixtures 后运行工具测试、validate/compile 和对应 Unity PlayMode 测试。需要证明实际行为时必须加载真实资产，不能用手写结果或日志文本替代。

# Citations

[1] [Gameplay spec tool](https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec)
[2] [Unity gameplay adapters](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Testing/Gameplay)
