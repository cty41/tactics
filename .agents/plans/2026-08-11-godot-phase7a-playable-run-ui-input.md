# Godot Phase 7A：三战可玩 Scene/UI/Input 闭环

> 收口修订：人工验收发现 Phase 6A 把 Unity DecisionGraph 压扁为 archetype/权重，且 Phase 7A 没有登记死亡尸体、逐步展示 AI 或渲染合法格。本计划的关闭闸门现包含完整 DecisionGraph/移动后施法 parity、共享尸体事务、Poison Spear/Pickup、Tile 高亮、AI playback、Turn Order 与结构化日志；在这些修复人工通过前继续保持 `manual_ui_input_qa_pending`。

## Summary

以 `1c613c20` 为基线，将 Phase 4–6B 的 Unit、Skill、AI、Encounter、Run 与存档组合为 `Home → N1 → N2 → N3 → Summary → Home` 的原生 1600×900 可玩闭环。鼠标主导、键盘辅助；成长只展示 PendingProgression，不消费；正式 VFX、Audio、Inventory 与完整七层 Run 延后。

## Current State

- canonical Catalog 74 项；Run 存档使用 `user://pure-run/save-v1.json`。
- Core 的移动、技能、AI、结算是唯一玩法真相源；Godot 目前只有诊断 Fixture，没有 main scene 或生产 UI flow。
- MCP Profile 在实施开始时切换为 `ui-input`；Editor 修改期间由 lifecycle 正常挂起并按原状态恢复。

## Implementation

1. 冻结 Unity Home/Battle/Settlement/Summary/Input 合同与源码 hash，建立 `pure-run-ui-input-v1` batch。
2. Application 增加 PlayableBattleSessionService、BattleUiSnapshot 和结构化 UI intent；所有命令复用 BattleTransitionService，AI 复用 AiTurnService，终局交还 PureRunSessionService。
3. ResourceSaver/PackedScene 生成 Main、Home、Battle、Settlement、Summary；鼠标点击与 Esc/Enter 共用 intent 路径，保持 1600×900 与 `canvas_items + keep`。
4. 自动门禁后保持 `Generated/UnityOwned + manual_ui_input_qa_pending`；用户完成 Home、三战、失败/恢复、resize 与 Reload 验收后才关闭。

## Validation

- Core/Application/GdUnit/Python/Oracle、Debug/Release、Compatibility/Forward+、UID、两次生成幂等与完整 `Verify-GodotMigration.ps1`。
- 人工验收覆盖 New/Continue/覆盖确认、移动/技能/取消/结束回合、AI 自动推进、N1–N3 结算、Summary、失败恢复与 Reload/Output。

## Boundaries

- 不迁移 Inventory、成长消费、Rest/Store/Mystery、N4–N6、Elite/Boss、正式 VFX/Audio。
- 不拆程序集或目录，不建立第二套战斗、AI、奖励或存档逻辑，不改变 canonical Catalog 74 项。
- 完成后把长期结论并入权威设计/OKF，删除本计划，由 Git 保留历史。
