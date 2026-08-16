# Godot 主线查漏补缺与 Unity 工程完整退役

## Summary

当前三职业 Pure Run 的自动化迁移主体已经完成：Catalog 142、Lv1–Lv3、完整 Run、Treasure/Map/Save V6、Godot Workbench、正式 UI、Gameplay Spec Runner、Godot-only 验证和 Windows RC Actions 均已有实现证据。

Unity 工程删除前仍需关闭三类阻断：完整可达性与历史内容分类、live Unity Oracle/工具依赖、仍标记为 `UnityOwned` 的内容和治理状态。Godot 人工验收不阻断 Unity 源工程删除，但继续作为发布质量闸门保留。

本计划采用完整退役：Barbarian、Hunter 及旧技能原型归档退役，不迁入当前 Pure Run；Audio payload、Unity PlayerPrefs 导入和未授权第三方视觉不进入本版本。真正删除前必须展示精确 manifest 和成功的物理无 Unity 预演，并再次取得用户确认。

## Current State

- 基线分支 `migration/godot`；远程 annotated tag `unity-final-2026-08-08` 的 tag object 为 `b881177a7a34eff2d4ef8bc3ca6e47c12f5a468d`，实际指向 commit `168d19345d7e0f7f22ce2516351eda9cef2e1cb1`。
- canonical Catalog 为 142；Godot-only staging 和 hosted Windows RC 已通过。
- `src/Tactics.UnityOracle.Tests` 仍 linked compile `Assets/Tactics/**`；`Tactics.Migration.slnx` 和完整 verifier 仍包含它。
- `Tools/migration/manifest/asset-categories.json`、batch/state 仍大量写成 `UnityOwned`。
- `-GodotOwned` 当前通过排除 Unity 根目录和跳过 Oracle/迁移测试证明可运行，但不是最终主线验证形态。
- Unity 中额外存在 Barbarian、Hunter、Uppercut、Counter、Mark、Freeze、Frost Nova、Heal 等非当前 Pure Run 内容，统一归类 `retired_legacy_prototype`。
- 当前工作树存在用户/既有修改和缓存；不得覆盖、暂存或清理这些路径。

## Implementation

### 1. 冻结最终 Unity 退役清单

- 建立 `unity-retirement-inventory-v1`，覆盖最终 Build Scene 依赖、代码注册表、UI/Input、Map/Event/Treasure、Editor 工具、BattleTest、测试规范和许可证边界。
- 每项分类为 `migrated_equivalent`、`replaced_by_godot_design`、`retired_legacy_prototype`、`excluded_third_party`、`deferred_audio_payload`、`provenance_only` 或 `unresolved`。
- 两次独立导出必须 byte-identical；只有 `unresolved=0` 才可继续。
- 修正文档漂移，包括 Bone Spear 的 Charisma 门槛、Catalog 142、Save V6 与 Windows RC 状态。

Checkpoint：`docs: freeze final Unity retirement inventory`

### 2. 解除 live Unity Oracle 依赖

- 以仓库内规范化源码快照建立 `src/Tactics.FrozenOracle.Tests`；每份快照记录 Tag、Git blob、原路径和 SHA-256。
- 测试不再读取 `Assets/`，但保持原 Oracle 断言和冻结语义。
- solution、lock file 和 verifier 改为运行 FrozenOracle。

Checkpoint：`test: detach frozen gameplay oracles from Unity sources`

### 3. 切换 Godot 内容所有权

- 建立 `godot-content-ownership-v1` receipt；当前 category/batch/state 晋升 `GodotOwned`，历史 export receipt 保持原始 `UnityOwned`。
- Godot Resource、Catalog、ContentId、Save V6 和 Workbench 成为唯一编辑/生成权威。
- Unity GUID/LocalFileId/SourcePath 只保留为 provenance；删除 disposable DTO、旧 exporter/converter、临时 GUID 映射和会覆盖 canonical Resource 的生成入口。
- 人工验收状态与所有权分离，允许 `GodotOwned + manual_qa_pending`，不得伪装为人工通过。

Checkpoint：`feat: promote canonical content to Godot ownership`

### 4. 建立正式 Godot 主线验证链

- 建立 `Tactics.Godot.slnx` 和 `Tools/godot/Verify-GodotProject.ps1`，迁移稳定 Windows build/RC/config 脚本。
- 移除 `-GodotOwned` 双模式；正式 verifier 默认要求 Unity 根目录不存在。
- Core/Application/FrozenOracle/NUnit/GdUnit/Gameplay Specs/Python/OKF/Compatibility/Forward+/Windows package 进入同一串行门禁。
- 禁止编译引用 UnityEngine/UnityEditor、live Unity 路径访问和 Release 中的迁移/TestHost 载荷；provenance 字段中的历史路径例外。

Checkpoint：`chore: establish the Godot-owned project baseline`

### 5. 退役 Unity 治理、工具与历史计划

- 将 `AGENTS.md`、Godot skills、hooks 和配置改为 Godot-first。
- 删除 Unity-only rules/skills/auto-compile hooks/MCP/TBSF GUID/AssetDatabase 工具。
- 当前 Pure Run Gameplay Specs 保留；Unity-only 历史 specs 生成带 blob 的退役需求索引。仍被平台中立编译器测试消费的文本 fixture 保留为测试资产，不作为当前产品内容。
- 迁移设计/OKF 标记 archived，建立当前 Godot 项目权威页；Unity-only known gaps 归档。
- Godot 人工验收账本保持 pending；完成计划的长期结论迁移后删除旧 active plans。

Checkpoint：`docs: switch project governance to the Godot mainline`

### 6. 删除 manifest 与物理预演

- 生成受版本控制的精确 deletion manifest，覆盖 Unity 工程、旧 Oracle、Unity-only 治理/工具和已归档测试。
- 删除前要求 tracked worktree 无未归属修改，并确认远程 Unity Tag 不漂移。
- 在系统临时副本应用 manifest，运行正式 Godot verifier、Catalog/Save/Workbench 和 Windows RC smoke。
- 扫描副本确认无活动 Unity 工程、`.meta`、`.unity`、Unity package 或 live source dependency。

Checkpoint：`chore: prepare the archived Unity project retirement`

完成后暂停，展示精确路径、文件数、字节数和预演证据，等待最终 destructive confirmation。

### 7. 删除与关闭

- 确认后严格按 manifest 删除，不扩大范围，不改写历史，不删除远程 Tag/归档分支。
- 保留 FrozenOracle、Golden、receipt、许可证、退役索引和 Git 历史。
- 提交 `chore: retire the archived Unity project`。
- 删除后运行完整 Godot/Windows 门禁，更新 OKF，删除完成计划；Windows Actions 仅在另行 push 授权后运行。

## Test Plan

- 退役清单两次导出一致且 `unresolved=0`。
- FrozenOracle 在没有 `Assets/` 时与旧 Oracle 断言一致。
- Catalog 保持 142，Save V1–V6 round-trip 和迁移稳定。
- 所有当前 batch/category 为 `GodotOwned`，历史 receipt 不变。
- 正式 verifier 不使用 Unity 运行时跳过分支；OKF 仅对 deletion manifest 已审计的历史来源前缀允许缺失，当前 Godot/FrozenOracle 路径仍必须存在。
- 删除预演与真实删除后均通过 Debug/Release、NUnit、GdUnit、Gameplay Specs、两个 renderer、OKF 和 Windows package/startup smoke。
- Release 不包含 Unity、GdUnit、TestPlatform、迁移 DTO 或未授权第三方 payload。
- 自动测试不把任何人工验收项改为 passed。

## Assumptions and Handoff

- 当前产品范围是三职业 Pure Run，不复刻 Unity 历史原型。
- Audio payload、Unity PlayerPrefs 和第三方视觉不进入本版本。
- `GodotOwned` 表示编辑/生成/运行权威切换，不表示人工体验验收完成。
- 不 push、不建 PR、不切换 worktree、不改写历史；真正删除前再次确认。
- 完成实现后将长期结论并入 `.agents/docs/`，更新 OKF scope，删除本计划，由 Git 历史保留。
