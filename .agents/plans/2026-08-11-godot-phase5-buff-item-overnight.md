# Godot Phase 5A：Buff/Item 夜间迁移

状态：active

基线：`migration/godot` / `2b341cb3`（Phase 4 Unit 与 Editor lifecycle pending checkpoint）

关联总计划：`.agents/plans/2026-08-09-godot-migration-parity-and-agent-enablement.md`

## 目标

在不进入 Skill、AI、Encounter、Run/Persistence、UI/Input 或 Presentation 的前提下，冻结 14 个 Buff 与 15 个 Item 的 Unity/JSON 源合同，实现纯 .NET 的确定性 Status、Consumable、Equipment 语义，并通过 ResourceSaver 生成最终 Godot 内容。Phase 4 的 Unit 人工视觉闸门保持独立，不因 Phase 5A 自动门禁而晋升。

## 决策

- Unity 只作为冻结 Oracle 与 AssetDatabase 导出宿主；不读取 YAML，不启动 Windows Standalone。
- 原 Unity/JSON ID 保存在 `SourceId`，稳定业务身份使用小写 `ContentId`。
- Buff 图标只记录 GUID、路径与依赖 hash，不复制 PNG，不增加视觉闸门。
- `buff.poison` 由已经验证的 Poison Spear batch 持有；本批仅声明外部内容依赖。
- Core/Application 只依赖纯 .NET 合同；Godot Resource 与迁移 DTO 不越过 Adapter 边界。
- Phase 5A 无视觉载荷，自动门禁全绿后可到 `Validated/UnityOwned`，但不切换为 `GodotOwned`。

## Checkpoint 2：冻结源合同

状态：completed（`0d20bdf5`）

- 用固定 Unity 6000.3.11f1 Editor batchmode 连续独立导出 14 个 Buff 根资产，两份 DTO 必须 byte-identical。
- `Consumables.json` 与 `Equipment.json` 直接按最终 Tag blob/SHA 冻结，不经过 Unity YAML。
- typed converter 严格检查 14 Buff、3 Consumable、12 Equipment 的字段、枚举、引用、ContentId、SourceId 与 icon audit-only 边界。
- Oracle Matrix 绑定 BuffConfig、Buff/Behavior/Component、枚举、Consumable/Equipment 定义及两份 JSON。
- 批次停在 `Exported/UnityOwned`，不得生成 Godot 资产。

## Checkpoint 3：确定性运行时

状态：implemented，等待本 checkpoint 统一门禁与提交

- 新增 StatusDefinition、polarity/effect/trigger/refresh、运行时状态与 `StatusRuntimeService`。
- 兼容扩展 BattleStatusState/BattleUnitState，加入基础速度、状态参数、消耗品与本轮使用记录。
- 新增 Consumable/Equipment 定义、实例、投影、命令与事件；Battle Transition 升级为 v3，Golden 升级为 schema v7。
- Poison/Burning 按 ContentId 顺序 tick；Frozen/Stun 禁止非 EndTurn；Slow 基于冻结基础速度；curse category 后应用替换。
- 药水仅允许自身或曼哈顿距离 1 的存活友军；合法零恢复仍消耗，非法不消耗；净化只移除 Harmful；每单位每轮只成功使用一次。
- Equipment 按唯一 slot 投影六项属性，再复用 `unity-unit-derived-v1`。
- Mark、伤害增减、Counter、Ice Armor、Fear 只产生强类型 policy 结果，不强接未迁移的 Skill/AI。

## Checkpoint 4：Godot 内容与 Catalog

- ResourceSaver 生成 13 个新 Buff、3 个 Consumable 与 12 个 Equipment Resource；Poison 引用现有 `PoisonBuff.tres`。
- Buff/Item Catalog 共 29 个条目；canonical 全局 Catalog 合并 Poison 6、Unit 13、Buff 新增 13、Item 15，共 47 个唯一 ContentId。
- 连续生成两次，目标、UID、semantic hash 与 ledger 必须一致；Compatibility 与 Forward+ 均验证 catalog/runtime fixture。
- receipt 标记 `visualAcceptance=not_applicable_no_visual_payload`，自动门禁通过后晋升 `Validated/UnityOwned`。
- 完成后将长期设计与 OKF 同步，并删除本 active plan；Phase 4 active plan 继续保留。

## 统一门禁

每个 checkpoint 均要求 locked restore、Debug/Release 零警告零错误、Core/Application/Unity Oracle/GdUnit/Python/Agent policy/Skill/Incident/OKF 全绿、Poison Spear 与 Unit 回归、UID/receipt/hash/幂等和 `git diff --check` 通过。任何 checkpoint 失败即停在最后一个绿色提交；不 push、不建 PR、不改写历史、不切换 worktree 或 MCP Profile。
