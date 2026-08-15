# Godot Ownership 收口与 Unity 工程删除

## Summary

当前不能立即删除 Unity 工程。先完成 Lv3、Treasure、权威 Map、统一 Content Workbench、Audio 框架、Godot-only 验证和剩余人工/Windows RC；全部通过后才在 `migration/godot` 原地删除 Unity 根目录。

当前 Godot Run 拓扑是首个正式默认地图，不为了 Unity parity 扩展层数；Map Workbench 仍支持未来任意层数。第三方 Piloto 等载荷不迁移，只保留来源/hash 审计。

## Current State

- 基线：`migration/godot`，计划创建时 HEAD `41fe3193`，canonical Catalog 131。
- 已实现：Core/Application、三职业 Lv1/Lv2、Inventory/成长、完整当前 Run、Elite/Boss、Save V5、等距战斗、程序化表现、正式 Godot UI shell、Gameplay Spec Godot Runner。
- 未完成：9 个实际存在的玩家主分支 Lv3（含无独立 AbilityConfig 的 Combat Techniques Lv3 被动）、Skeleton Warrior Lv3 内部攻击、Treasure、正式 Audio、通用内容工作台、Godot-only Oracle/verifier、剩余人工 QA 与 Windows RC。高级分支不得虚构 Lv3。
- Unity Oracle 仍链接 `Assets/Tactics` 下的源码；统一 verifier 仍读取 Unity GameData 和源图片。
- 当前用户脏文件、缓存和 artifact 不属于本计划，不得覆盖或暂存。

## Implementation

### 1. 冻结最后一批 Unity 合同

- 通过 Unity AssetDatabase 两次导出 8 个 AbilityConfig Lv3、源码冻结的 Combat Techniques Lv3 被动、Skeleton Warrior Lv3 攻击、Treasure、Map/Event/Skill/Presentation/AI/BattleTest 有效合同。
- 固定 GUID、LocalFileId、dependency hash、源码 SHA 和项目自有 UI 来源。
- Party/Enemy/Corpse 测试槽位与格子吸附规则进入 typed DTO；Piloto 只审计不复制。
- converter 拒绝未知节点、虚构高级分支 Lv3、非法引用、重复 ID 和第三方 payload。

### 2. 完成 Lv3 与 Treasure

- 迁移 Unity 实际存在的 9 个玩家主分支 Lv3（8 个主动资源和 1 个 Combat Techniques 被动等级），以及 Skeleton Warrior Lv3 内部攻击。
- 成长支持合法 Lv2→Lv3；高级分支仍按 Unity 实际最大等级，不虚构 Lv3。
- Treasure 支持 Gold、Equipment、Consumable、Buff，首次判定确定性保存，Reload 不重掷，重复确认不重复奖励。
- Preview、AI、事件、描述、程序化表现、Inventory 与 Save 使用现有统一 Runtime。

### 3. Map Resource 成为正式 Run 权威

- 完整 `PureRunMapResource` 支持任意层数、稳定 Node ID、显式连接和 Battle/Elite/Boss/Rest/Store/Mystery/Treasure。
- 当前 Godot 可玩拓扑生成为默认正式 Map；Main、Run 和 Flow Projector 只消费编译后的 Map Definition。
- 删除硬编码 `LayerFourMap()`、静态 edge 和字符串 Layer 推断旁路。
- Save V6 保存 map identity/revision、访问状态、pending node/treasure transaction；V1-V5 确定性迁移，失败保留原档与 backup。

### 4. 统一 Godot Content Workbench

- Map：节点/连接/Inspector、Undo/Redo、自动布局、Validate、保存回滚和 seed 预览。
- Event/Treasure：选项、检定、结果、奖励表和 Map 关联跳转。
- Encounter/Battle Fixture：在 canonical 等距棋盘拖放 Party、Enemy、Summon、Corpse、Blocked Cell；编辑 Unit、等级、HP/MP、状态、技能和 AI；保存正式 Encounter/Layout 或 `ValidatedGodotRunCheckpoint`；一键由 Main/Gameplay Spec 启动。
- Skill/Presentation：将 Poison Spear 样板泛化为全部技能，使用 typed ChangeSet、Revision、Undo/Redo 和正式 SubViewport 播放器。
- AI：图、Intent、Rule、Score、Curve 与候选评分可视化；只允许已知安全参数编辑。
- Audio：cue/profile、bus、并发、资源引用和 Editor 试听。
- QA：选择 Gameplay Spec/checkpoint/renderer，后台运行并展示 trace、失败诊断、截图和清理结果。
- 所有保存通过 ResourceSaver/受测 compiler；不得手写 `.tres/.tscn`。

### 5. Audio 框架与合法素材

- Master/Music/SFX/UI buses；独立版本化音量/mute 设置，不写 Run Save。
- `AudioCueDefinition`、变体、并发、清理和 committed battle/page event 映射。
- 用户提供允许 Godot 发布的素材包与许可证后，记录文件级来源/hash 并接入 Music/SFX/UI。
- 素材未确认前不得宣称 Audio 完成，也不得进入 Unity 删除步骤。

### 6. 解除 Unity 验证依赖

- 用冻结 Golden/DTO 替换 Unity Oracle 对 Unity C# 文件的链接。
- verifier 不再读取 Unity GameData、图片或 AssetDatabase 输出；Unity 路径只可作为 provenance 字符串。
- Core/Application/Godot/测试/生成器不得依赖 UnityEngine 或活动 Unity 路径。
- 各内容类别完成自动、人工和来源审计后晋升 `GodotOwned`。
- 在排除 Unity 根目录的临时副本运行完整 Godot-only verifier。

### 7. 人工与 Windows RC

- 关闭正式 UI、Inventory、Defeated、Miss/Heal/MP 数字和真实 Editor Assembly Reload。
- 新增 9 个玩家 Lv3、Treasure、Map/Event/Skill/Presentation/AI/Encounter/QA Workbench 与 Audio 人工验收。
- 使用 Godot 4.7.1 Mono templates 或 GitHub Actions，从无 Unity 根目录的干净 checkout 导出并启动 EXE/PCK。
- Release 不得包含 Unity、GdUnit、TestPlatform 或迁移临时载荷。

### 8. 最终删除 Unity

- 删除前生成精确 deletion manifest 并停在最终人工确认点。
- 计划删除 `Assets/`、`Packages/`、`ProjectSettings/`、Unity 专用工程/启动配置和 Unity-only 工具测试。
- 保留 Core、Application、Godot、Workbench、Gameplay Spec、冻结 Golden/DTO/receipt、文档、OKF、许可证记录与 Git 历史。
- 删除后重新执行 Godot-only verifier、Windows RC、依赖扫描和 scoped code review；不 push、不建 PR、不改写历史。

## Public Interfaces

- `PureRunMapDefinition/Resource`、compiler、validator。
- `PureRunNodeKind.Treasure` 与 Save V6。
- Lv3 Skill Runtime。
- 通用 Workbench document/change-set/save 接口。
- `EncounterFixtureDocument`、fixture compiler、checkpoint exporter。
- `AudioCueDefinition` 与独立 Audio Settings。
- Core/Application 不引用 Godot；Godot 不引用 Unity。

## Test Plan

- Lv3、Treasure、Map V6、旧存档迁移和奖励幂等。
- Map/Event/Skill/Presentation/AI Workbench 的 Undo/Redo、校验、保存回滚与 Reload。
- Encounter Fixture 的拖拽吸附、占格、round-trip、正式 Main 启动，以及与正式 Encounter 共用 layout compiler。
- Audio bus、pause、并发和清理不改变 gameplay。
- ResourceSaver 两轮一致；Catalog、UID、receipt 精确匹配。
- 无 Unity 根目录时 Debug/Release、Core/Application、Gameplay Specs、GdUnit、Compatibility/Forward+、Windows export 全绿。
- 发布内容不存在 Unity 或未授权第三方 payload。

## Risks and Gates

- 合法音频素材是外部人工阻断点。
- Workbench、人工 QA、Windows RC 和 `GodotOwned` 未完成前不得删除 Unity。
- 删除前仍需一次精确路径清单确认。
- 实施完成后将长期结论并入权威 docs，更新 OKF，删除本 active plan，由 Git 保存历史。
