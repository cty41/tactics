# Phase 7B–8E 收口修复：Run 路由、地图、Inventory 与战斗表现 parity

## Summary

以 `migration/godot`、HEAD `a3e4d66a`、canonical Catalog 124 为基线，修复人工验收暴露的 Run 页面路由、Roguelike Map、New Run 起始技能、Progression、Inventory 和战斗表现缺口。保持 Unity 正式掉率、玩法数值、AI、ContentId 与 Catalog 不变；当前用户 `user://` 主档和 backup 只读保护。

自动实现、review、验证和本地 checkpoint 不等待确认；最终停在 `manual_inventory_run_flow_and_presentation_qa_pending`。

## Current State

- 当前 V4 主档证据：N1 已完成、N2 Ready、存在一条 PendingProgression、背包为空。
- backup 证据：N1 PendingBattle 且 checkpoint 完整。
- Core 可恢复 PendingBattle；Adapter 页面旁路会落入不可点击的 Pending Map 节点。
- Map 仅绘制 Revealed connection；Unity 会建立全部 outgoing connection。
- Hit cue 当前动画主体错误地使用伤害来源，导致 Thrust 后 Amazon 延迟红闪。
- Inventory 事务已存在，但当前页面仅在背包非空时按角色重复生成按钮。

## Implementation

### Checkpoint 1：Run 路由、地图和 New Run Setup

- 新增统一 `RouteRunState`：Terminal → PendingBattle → PendingProgression → Resolving node → Map。Continue、结算、Reload、Inventory 返回和 Pending 节点均通过同一 action 路由。
- N1–N3 胜利显示 Settlement；存在成长时 Continue 进入 Progression，完成后才解锁下一战。
- Map 始终绘制完整 19 条拓扑；Locked、Available、Traversed 只改变线型/亮度，线段从节点圆边缘起止。
- `PureRunPartyTemplate` 显式冻结三职业各三个 Starting Lv1。New Run 依次选择 Mage、Necromancer、Amazon；完成前三人选择前不替换旧 Active Run。
- 新增可恢复 `PendingRunSetup` 和 Save V5；V1–V4 确定性迁移，canonical JSON、SHA、revision、temp/backup/quarantine 语义不变。
- Checkpoint commit：`fix: restore Pure Run map and progression routing`。

### Checkpoint 2：Progression 与战斗表现

- Progression 保持属性分配→技能三选一两阶段；Step 2 显示加点后属性、当前技能/等级/类型/说明和三个合法候选。
- `BattlePresentationCue.ActorId` 固定为实际动画主体，新增 `InstigatorId`；Hit 作用于受伤目标并在攻击 Impact marker 触发，Defeat 排在 Hit 后。
- 新增表现专用 Active Unit Foot Marker：平时对齐 Snapshot cell，Move Tween 期间跟随 Actor，Frame 后对齐 After Snapshot。
- HP/MP 改为 hover-only 双条；宽度由 Sprite AABB 计算并 clamp 到 38–48px，同一时间最多一组。
- Checkpoint commit：`fix: align Godot battle feedback with committed action markers`。

### Checkpoint 3：Inventory 闭环

- 单一 Inventory 页面：角色/属性/技能/派生值、Equipment/Consumable 背包筛选、装备槽、详情和操作区。
- 支持 Equip、Replace、Unequip、Carry、Replace Carried、Unload；成功操作停留在 Inventory 并刷新，Back 回到原页面。
- 正式掉率不变；自动 QA 使用隔离 Store 路线获得 Equipment 与 Consumable。Settlement 显示实际掉落或 `No item drop`。
- Checkpoint commit：`feat: complete playable Godot inventory interaction`。

## Public Interfaces

- `PureRunPartyTemplate` 墹加 Starting Skill choices。
- 新增 `PendingRunSetup`、选择/提交/取消结果和 Save V5。
- `PureRunFlowPage/Action` 增加 NewRunSetup、Inventory、ResumeBattle、ChooseStartingSkill。
- `BattlePresentationCue` 增加 `InstigatorId` 和 Impact trigger。
- Core/Application 保持 engine-neutral；Godot Adapter 持有页面、hover、marker 和 Tween 生命周期。

## Test Plan

- TDD 覆盖 19 条连接、PendingBattle/Progression 路由、三职业起始三选一、V4→V5、旧 Run 原子覆盖和 Reload。
- 覆盖 Growth 三项、当前技能显示、重复/旧 revision 无副作用。
- 覆盖 Move marker 连续跟随、Thrust 目标 Hit/Impact、Pause/Step/0.5×/1×/2×/4×和清理。
- 覆盖 hover-only HP/MP、38–48px 宽度、尸体和销毁清理。
- 覆盖 Store→Inventory→Equip/Replace/Unequip→Carry/Replace/Unload→Reload 实例唯一。
- Catalog 精确 124；完整运行 `Tools/migration/Verify-GodotMigration.ps1`、Debug/Release、Core/Application/Oracle/GdUnit/Python、Compatibility/Forward+、UID、OKF、敏感信息和 whitespace。

## Manual Acceptance

1. 三名角色各自起始技能三选一后进入 Map。
2. 全地图连接可见，PendingBattle 可恢复。
3. N1 胜利完成属性和技能成长后 N2 解锁。
4. 脚底标记随移动，Thrust 目标及时红闪且 Amazon 不误闪。
5. HP/MP 双条只在 hover 时显示且宽度接近 Sprite。
6. Store 获得物品后完成 Inventory 全部操作并 Reload。
7. 非 16:9、Assembly Reload、Continue 和 Output smoke。

## Handoff and Completion

- 不覆盖用户真实存档；测试使用隔离 Store/fixture。
- 不迁移正式 UI 美术、地图背景、Lv3、Treasure、Audio 或 Windows Release/PCK。
- 自动门禁后保持 `Generated/UnityOwned + manual_inventory_run_flow_and_presentation_qa_pending`。
- 人工验收后迁移长期设计、同步 OKF、删除完成的 Phase 7B–8E active plans，并创建关闭提交。
