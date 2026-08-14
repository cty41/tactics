# Godot Gameplay Spec 定向 QA Runner

## Summary

复用现有 `*.gameplay-test.md`、TypeScript validator/compiler 和领域 Adapter 语义，为 Godot 4.7 增加 v2 执行计划与真 Play 测试后端。首批覆盖 Inventory 战斗属性、Defeated 终局、Miss/治疗/MP 动态数字以及 Scene/process Reload 清理。

成功标准：默认 Unity v1 编译产物不漂移；Godot v2 capability 校验、隔离 checkpoint、Main 场景执行、结构化 trace 和四组场景全部进入统一 verifier；生产存档和 Catalog 不变化。

## Current State

- Unity Spec 工具链已有 Battle、Skill、Map、UI、PlayerInput 五类 Adapter。
- Godot 已有 Application/GdUnit 定向测试，但没有消费 Gameplay Spec 的 Runner。
- 当前四项已有规则级测试，缺少 Main、表现排空、导航和隔离存档组成的端到端证据。

## Implementation

1. 为 CLI 增加 `--runtime godot`，保持默认 Unity v1 byte-identical；Godot 输出带 runtime、capabilities、checkpoint/save-isolation 和 watchdog 的 v2 plan。
2. 增加 test-only Godot Runner、领域 Adapter、Viewport 输入、隔离 Store/TestContext、checkpoint 校验、timeout/no-progress 与结构化报告。
3. 新增 Inventory、Defeat、Damage Number、Reload 四组 Spec 和合法 checkpoint；不使用生产存档，不伪造终局。
4. 将编译、Runner、双 renderer 和报告校验接入 `Verify-GodotMigration.ps1`；更新权威测试文档、OKF 和人工验收账本。

## Boundaries

- 不实现完整策略 Planner、自动通关率、36 技能矩阵或多 seed 基准。
- 新能力只要求 Godot 后端；既有 Unity v1 与 fixture 保持兼容。
- 不修改 Core 玩法、Save V5 wire、ContentId、Catalog 131、CI 或程序集边界。
- 真实 Editor Assembly Reload、文字可读性和动画观感保留最小人工检查。

## Handoff / Closing

完成后将稳定 Runner 设计并入 `.agents/docs/gameplay-test-framework.md`，更新受影响 OKF scope，将未完成项写入统一缺口，删除本 active plan，由 Git 历史保留。
