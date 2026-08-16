# Phase 7B–8E：战场取景、暂停菜单、动态数值与原子成长

## Summary

以 `cb232e9d`、Catalog 124 为基线，恢复 Unity 的完整棋盘取景、Esc 菜单、动态战斗数字和成长候选/保存边界。实现后保持人工验收 pending，不 push。

## Current State

- Godot 战场仍以 1200×900 Control 呈现，右侧留有历史诊断空间。
- 战斗与地图仍暴露 Abandon；Esc 只取消 targeting。
- Core 已有 Damage/Heal/Mana/CombatRoll 事件，但表现帧没有动态数值事实。
- 成长候选已具备 guarantee 骨架，但 UI 会持久化 ProposedAttributes，并允许 Back to Map。

## Implementation

1. 从 100 个菱形计算 AABB，将 BoardPresentationRoot 等比拟合到扣除 HUD 安全边距后的完整 1600×900；输入通过逆变换解析。
2. 删除 Abandon/Back to Map，增加 Unity 同构 Esc 菜单，暂停表现和 gameplay intent；Main Menu 保留 run，Save and Quit 只保存已提交状态。
3. 从 committed events 编译 Normal/Critical/Heal/Mana/Miss 数字事实，在 Impact/Tick marker 播放并保证退出清理。
4. 精确复刻 Unity 三槽候选顺序：起始进阶 guarantee、合法 Upgrade、确定性剩余候选。
5. 属性与技能选择只保存在 UI 草稿；CompleteProgression 一次性应用并保存。历史 V5 ProposedAttributes 恢复时丢弃草稿但保留资格。

## Test Plan

- TDD 覆盖 AABB/fit/逆变换、Esc 优先级、数字事件关联、固定 seed 三槽、原子成长及历史 V5 恢复。
- 运行 `Tools/migration/Verify-GodotMigration.ps1`、双 renderer、Catalog 124、OKF/UID/whitespace。
- 人工检查棋盘居中、菜单、动态数字、成长优先级与中途退出恢复。

## Handoff / Closing

禁止改战斗数值、AI、奖励、ContentId、Catalog 或 Save schemaVersion。验证后同步权威迁移设计与 OKF；人工验收通过后删除本计划，由 Git 保留历史。
