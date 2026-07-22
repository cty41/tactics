---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将 Agent 编写的受控 gameplay spec 编译为 Unity adapters 可执行的确定性计划。
tags: [testing, gameplay, automation, unity]
timestamp: "2026-07-22T20:36:34+08:00"
status: active
catalog_scope: gameplay-test-framework
repo_paths:
  - .agents/docs/gameplay-test-framework.md
  - .agents/skills/gameplay-test-framework/SKILL.md
  - Tools/gameplay-test-spec
  - Assets/Tactics/Scripts/Common/Testing/Gameplay
  - Tests/gameplay-specs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:27baff04c7334823ab16a6afe78c5687905ada5a843b01f68d744890167a2454
---

# Current State

Agent 编写的 `.gameplay-test.md`/`ScenarioSpec` 经 TypeScript validator 和 compiler 生成 `.plan.json`，Unity `GameplayRuntimeRunner` 再通过 Skill、Battle、Map、UI adapters 执行 setup、action 和 assertion。源 Spec 是维护对象，plan 是生成物。

框架支持真实 Unity 资产，并已有生命、法力、Buff、位置、行动状态、投射物和多阶段等专用断言。Battle adapter 还能创建并推进精确参战顺序，观察朝向、先攻、标准状态、召唤注册、技能可用性原因和有序目标选择；UI adapter 支持点击、右键、悬停、键盘输入，以及文本、样式类、子节点顺序和布局关系断言。adapter 与断言支持面以当前 schema、compiler、Unity 代码和 fixtures 为准，不再按历史 Phase 文档判断。

Map adapter 除 Pure Run 路线与胜场外，现可创建独立消耗品实例、操作角色携带/卸下、一步替换装备、购买通用商店货物，并断言背包、携带引用及商店数量/药水保底/去重。Battle adapter 可对明确单位使用角色携带药水并构造、断言 `CanReceiveHealing`；UI adapter 可验证统一 Inventory popover 和独立战斗消耗品槽。对应维护源位于 `Tests/gameplay-specs/consumables/`、`map/` 与 `ui/`，生成 plan 不手改。

Map adapter 还可断言角色稳定技能 ID 的实际等级、指定 Pure Run 候选是否包含目标等级，以及候选池是否同时含新技能和升级。`pure-run-mixed-levelup-candidates` 源 spec 覆盖 Fireball Lv1 角色在 Lv2 获得 Fireball Lv2 与新技能混合候选；跨存档绑定和真实 SkillGraph 行为由 PlayMode 测试补足。

`Tests/gameplay-specs/shared/` 是共享战斗原语的维护源，五个场景分别覆盖朝向/当前轮先攻重排、标准状态回合语义、召唤上限与最早替换、可点击禁用原因，以及可撤销的有序多段选择。对应 plan 必须由 CLI 生成，不手改。

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
