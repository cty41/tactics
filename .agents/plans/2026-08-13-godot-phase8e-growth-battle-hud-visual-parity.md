# Godot Phase 7B–8E 收口修复：成长卡片、战斗 HUD 与目标表现 parity

## Summary

以 `migration/godot`、HEAD `2e7ba7c5`、canonical Catalog 124 为实施基线，将成长三选一、Battle HUD、Lightning、重叠单位命中和 Amazon 持矛视觉修复并入现有 Phase 7B–8E 人工闸门。保持 Core 玩法、AI、Save V5、奖励、技能数值与 Catalog 数量不变；当前用户存档与 backup 只读保护。

自动实现、review、统一门禁和计划内 scoped checkpoint 已获授权；不 push、不建 PR。最终停在 `manual_inventory_run_flow_growth_hud_and_presentation_qa_pending`。

## Current State

- Unity `PureRunProgression.BuildSkillChoices` 的权威规则是 Learn/Upgrade 混合三选一，并保留一次性 starting-branch advanced guarantee。
- Godot Progression 已显示原始当前技能行，但缺正式名称、描述、MP 和清晰卡片；部分 Phase 5 Lv1 Resource 缺规范 BranchId，产生 `Learn Lv1 Lv1`。
- Battle Main 仍是右侧常驻诊断布局；Pause/Step/速度已存在，日志尚未迁入 CheatConsole。
- Lightning 从 caster 画向 target；鼠标使用整张 Sprite AABB；Amazon 投矛后未切换项目已有 unarmed idle 图。
- 工作树有三个既有假状态：`OneLineSettings.asset.meta`、Exporter `.meta`、`project.godot` 默认归一化；只允许后者真正新增的 Input action hunk进入提交。

## Implementation

### 1. 成长卡片与技能元数据

- 冻结 Unity 三选一、SkillSelection UXML/Controller、18 个正式分支和 Backquote 输入的源码/hash。
- 为九个基础 Lv1 Resource 写入规范 BranchId；Poison Spear 固定为 `amazon.poison-spear`，连续两次 ResourceSaver 生成一致。
- 增加 Adapter-owned `SkillUiMetadata`，从 Resource 保存名称、描述、类型、MP、射程、前置和属性门槛，不污染 Core gameplay Definition。
- Growth offer 按 ContentId 与 BranchId+Level 去重；严格三项；已拥有同级不得出现；Pickup、基础攻击和隐藏动作不得出现。
- Progression Step 2 显示更新后六项属性、完整当前技能卡及三张 Learn/Upgrade 候选卡。New Run 复用卡片但只显示三个基础 Lv1。

Checkpoint：`fix: restore Pure Run growth card parity in Godot`

### 2. 单位命中与 Amazon 持矛视觉

- 增加共享指针解析器，使用当前 Body Texture 的本地 Alpha 命中；多个命中按实际绘制顺序和稳定实例 ID 消歧；未命中单位才回退 Tile 反投影。
- Hover、Meter、click 和技能目标共用同一解析结果；Meter/状态/Shadow 不参与命中。
- 通过迁移管线加入 Amazon unarmed DR/UL idle，自有图仅作为现有 Unit Resource 依赖，不新增 ContentId。
- `DroppedSpears` owner 驱动 Held/Unarmed；Pickup/Recover 恢复 Held；死亡、镜像和 Shadow 不变。

Checkpoint：`fix: correct Godot actor targeting and Amazon spear visuals`

### 3. Lightning、Battle HUD 与 CheatConsole

- Lightning 以真实目标头部锚点为终点，从同 X 的棋盘可视上边缘外 32px 向下显现；无 caster projectile；Impact/Stun 只消费 committed events。
- HUD 改为 Unity 结构：顶部 Round/Turn Order，左上单位状态，左下 Move/Consumable/技能，右下 End Turn，右上 Pause/Resume、暂停时 Step 与 0.5x/1x/2x/4x。
- 日志、AI 分数、筛选与 Clear 移入默认关闭的顶部 25% CheatConsole；Backquote 切换，打开时输入不穿透且不自动暂停。
- framing 只变换棋盘表现根节点，按棋盘 bounds 与 HUD 安全边距适配 1600x900 和非 16:9。

Checkpoint：`feat: align Godot battle HUD and skill presentation with Unity`

### 4. 证据和知识同步

- 更新 receipt、权威迁移设计与实际受影响 OKF scope；Catalog 精确保持 124。
- 完整统一门禁、Compatibility/Forward+、Reload、UID、敏感信息和 whitespace 全绿后，如有独立稳定性补强，提交 `test: harden Godot growth and battle HUD parity`。
- 人工通过后再晋升 Phase 7B–8E、迁移长期知识、删除完成计划并创建关闭提交；本轮不提前晋升。

## Test Plan

- Unity Oracle 固定三选一、Learn/Upgrade、当前技能描述和 Backquote。
- Mage Lightning Lv1 的合法成长 offer 精确三项，含 Upgrade、可含新 Learn，且无同级/分支重复。
- New Run 三职业分别只出现三个基础 Lv1；当前技能和候选显示正式名称、描述、MP、等级与门槛。
- Alpha 命中覆盖前景不透明、前景透明穿透、单位优先 Tile、hover/click 同一实例。
- Poison Spear Held→Dropped→Pickup/Recover 视觉与 Core 状态、Reload 一致。
- Lightning 起点非 caster、终点为目标头部且无 projectile；表现不改变 BattleState/RNG。
- HUD bounds、Console toggle/输入隔离、Pause/Step/速度、framing、退出与 Reload 清理正确。
- 执行 `Tools/migration/Verify-GodotMigration.ps1`，要求 Debug/Release、Core/Application、Oracle、GdUnit、Python、Compatibility/Forward+、UID、OKF、敏感信息和 whitespace 全绿。

## Manual Acceptance

1. New Run 三职业各三个基础技能。
2. Progression 严格三选一，无 `Learn Lv1 Lv1` 或重复项。
3. 当前技能区和候选卡显示名称、等级、类型、描述、MP 与门槛。
4. 合法场景能同时看到新分支 Learn 和已有技能 Upgrade。
5. Lightning 从上方劈到目标头顶。
6. 重叠单位和透明区域 hover/click 正确。
7. Amazon 投矛后变为 unarmed，拾回后恢复。
8. HUD 接近 Unity，Pause/Resume、Step、速度保留。
9. Backquote 打开 CheatConsole，日志不常驻且输入不穿透。
10. 非 16:9、Continue、Assembly Reload 与 Output 正常。

## Handoff Notes

- 先确认 canonical Editor session=0；完成后只恢复本流程关闭的 Editor。
- 不手写 `.tres/.tscn`，全部通过 ResourceSaver/受测转换器；不复制 Piloto 或第三方载荷。
- 不修改用户真实 Save，不重置工作树，不暂存三个既有假状态的无关部分。
- 完成自动门禁后停在人工验收；人工通过后按 `project-doc-organization` 迁移长期设计、同步 OKF、删除本计划，由 Git 保留历史。
