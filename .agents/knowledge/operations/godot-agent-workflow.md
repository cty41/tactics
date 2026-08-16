---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot agent workflow
description: Current verified routing, research, testing and incident-promotion boundaries for the Godot 4.7 C# mainline.
tags: [godot, agent, workflow, research, incidents]
timestamp: "2026-08-16T13:39:26+08:00"
status: active
catalog_scope: godot-agent-workflow
repo_paths:
  - AGENTS.md
  - .agents/rules/godot-agent-workflow.md
  - .agents/skills/godot-workflow
  - .agents/skills/godot-editor-lifecycle
  - .agents/incidents/godot
  - Tactics.Godot.slnx
  - Tools/godot/Verify-GodotProject.ps1
  - Tools/godot/Build-GodotWindows.ps1
  - Tools/godot/New-GodotOwnedRcSource.ps1
  - Tools/godot/Test-GodotWindowsPackage.ps1
  - Tools/godot/Test-GodotWindowsLaunch.ps1
  - .github/workflows/godot-windows-build.yml
  - Tools/godot/Sync-GodotAiCodexConfig.ps1
  - Tools/migration/godot_ai_codex_config.py
  - Tools/migration/manifest/godot-tooling.json
verified_revision: d092a955
source_fingerprint: sha256:e03b94568cfe86761d727fd4aad59559a0096d34e10c057fa71dd393e3c7b102
---

# Current state

远程 `main` 是 Godot 产品与治理权威，使用唯一项目 `godot/project.godot`、Godot 4.7.1 Mono 和 .NET SDK 9。现有 `migration/godot` worktree 可在切换期间继续承载同一 tracked tree，但分支名不再定义权威，也不要求为切换移动本地 worktree。Agent 入口为 `godot-workflow`，再按任务加载 C#、内容、编辑器工具、测试诊断或 godot-ai 专项 Skill。未知或版本敏感结论必须按 Research Guide 从本地复现、官方文档/源码到上游和社区逐级调查，并标记证据等级。

Windows 内部 RC 由 `Tools/godot/Build-GodotWindows.ps1` 提供唯一只读构建入口，`godot/export_presets.cfg` 固定 `Windows Desktop` Release 预设。GitHub Actions 在相关 `main` push 或手动触发时只 materialize `godot/**` LFS，再由 `New-GodotOwnedRcSource.ps1` 从 tracked clean HEAD 建立物理排除 Unity 根、Unity Oracle、本地 MCP、缓存和 artifact 的临时 staging；staging 必须携带 tracked `godot/Tactics.Godot.Adapter.sln`，否则 Godot .NET export 会在退出码为 0 时逐文件报告缺少 solution。staging 内初始化本地只读 Git snapshot，以便统一 verifier 和 build 继续执行 tracked-tree mutation guard。官方 Godot Mono 编辑器与导出模板 URL/SHA-512 固定在 tooling manifest；CI 不运行会刷新迁移证据的完整生成链。

RC export 后由 `Test-GodotWindowsPackage.ps1` 验证 x86_64 PE、PCK、managed runtime packaging、顶层 allowlist 及测试/Unity/godot-ai/save payload 禁令，并生成 source/semantic/provenance manifest 与 `SHA256SUMS.txt`。Godot 4.7 Windows C# export 可将托管程序集封装进 PCK；manifest 必须记录 `LooseAssemblies` 或 `PckEmbedded`，后者由随后的真实 EXE 启动证明 C# 入口可加载，不能以缺少松散 DLL 误判失败。`Test-GodotWindowsLaunch.ps1` 在隔离 APPDATA/LOCALAPPDATA 下以 Compatibility 和默认 renderer 有界启动导出 EXE；成功包与筛选后的失败诊断分别作为保留 14 天的 Actions artifact，内部测试不自动创建 GitHub Release。当前版本明确不登记 Audio payload，静默运行是 RC 合法状态。

## Verified boundaries

- `Tactics.Core` 与 `Tactics.Application` 是纯 .NET，Godot 对象只存在于 Adapter。
- `ContentId` 使用严格小写业务 ID；Catalog 保存 ResourceType、UID、诊断路径和 SchemaVersion。
- `UnitInstanceId` 是战斗/重放实体身份，不能用单位定义 `ContentId` 替代；冻结 Unity 纯 C# Oracle 只存在于独立测试程序集。
- GdUnit4Net 3.1.1 Runtime Runner 需要脚本位于 `project.godot` 指定的主程序集。`Tactics.Godot.TestHost.csproj` 使用同一程序集名，但拥有独立 `obj`、lock 和测试包；生产 `Tactics.Godot.Adapter.csproj` 不引用 GdUnit/TestPlatform。Core/Application NUnit 仍为独立测试程序集。
- 构建和测试必须串行；并行 Core/Godot 进程已在本地造成共享 `obj` 争抢。
- godot-ai 固定 v3.1.2，只做通用 Editor/MCP 操作，不是 Runtime、Core、Application 或资产真相源。
- Codex godot-ai 采用项目级 Attach：本机忽略的 `.codex/config.toml` 优先于用户配置，用户级 godot-ai 表只允许作为 Godot Configure 的一次性输入。`Sync-GodotAiCodexConfig.ps1` 验证 `pythonw.exe` 无窗口 bootstrap、v3.1.2、8000/9500，并按 `phase3-observe → content-authoring → ui-input → presentation` 累积白名单生成配置。
- `script_create/script_attach/script_patch`、`filesystem_manage`、`client_manage` 与 `autoload_manage` 始终禁用；写操作前必须用 Session/Editor 状态确认唯一 canonical 项目。
- `godot-editor-lifecycle` 可在已授权 Godot 修改任务需要 session `0` 时自动挂起并恢复唯一 canonical Editor：只使用精确 PID 的正常窗口关闭与 pinned GUI executable，不强杀、不注入输入、不打开原本关闭的 Editor。
- Engine/toolchain 踩坑先进入 `.agents/incidents/godot`；verified 摘要才进入 OKF，重复流程才进入 Skill。
- Standalone headless ResourceSaver 新增路径时，UID 注册只对当前进程可见；生成器必须固定并持久化 ledger UID，随后先运行 headless Editor filesystem scan，再由独立 Runtime 验证 Catalog。
- Catalog 的 UID 继续作为生成/审计身份验证；运行时加载使用 receipt 固定的 `DiagnosticPathValue`。连续 headless ResourceSaver 批次可能让 Godot UID cache 暂时指向旧路径，因此不得把该缓存当作运行时 locator 真相源。完整 GdUnit 从版本控制的 `[TestSuite]` 声明发现 suite，并让每个 suite 使用独立串行原生 testhost；完整 Main 页 replacement cleanup 进一步单独运行，Gameplay Spec journeys 也使用独立宿主，避免累积的 SceneTree/ResourceLoader 对象在最终 teardown 互相污染，同时不减少断言覆盖。
- Ownership closure 的 Map/Treasure 生成器维护 layout v3 的 16 节点/23 边权威地图与确定性 Treasure Resource；连续两轮 ResourceSaver、ledger/receipt 和 Catalog 142 必须一致。旧 Layer 4 批次可以重建自身冻结证据，但检测到权威 Treasure batch 后不得替换 canonical run-map entry。
- Buff/Item disposable DTO 存在时，统一入口会严格编译 14 Buff、3 Consumable、12 Equipment typed draft，重建 export receipt，再连续两次通过 ResourceSaver 生成 28 个定义 Resource 与 29 项分批 Catalog；Phase 6A 存在时 canonical Catalog 为 73 项。该链路不复制只审计的 Buff icon，并在两个 renderer 的 typed runtime 验证后才刷新 `Validated/UnityOwned` generation receipt。
- Starting Skill disposable DTO 存在时，统一入口会编译 12 项 typed draft，通过 ResourceSaver 生成 11 个新 Skill Resource、12 项分批 Catalog 与原生 1600×900 Gameplay Fixture；`skill.poison-spear.lv1` 保持外部依赖。两轮生成比较 13 个批次独占 artifact；Phase 6A 存在时 canonical Catalog 组合为 73 个唯一 ContentId，并在 Compatibility/Forward+ 后保留已人工接受的 `Validated/UnityOwned` receipt。
- AI/Encounter disposable DTO 存在时，统一入口会编译共享 BasicMeleeGraph 的 13 节点/12 边、六类 AI、四项敌方技能、两个 Layout 与 N1–N3 Encounter，连续两次通过 ResourceSaver 比较 17 个批次 artifact，并在 Compatibility/Forward+ 验证 15 项分批 Catalog 与 73 项 canonical Catalog。AI 必须共同生成 Engage、当前格攻击和移动后技能候选；不可退化为“无攻击才移动”。
- Pure Run persistence disposable DTO 存在时，统一入口会重编三战 typed draft，通过 ResourceSaver 连续两次比较 Run Resource、分批 Catalog、自动诊断 Fixture 与 ledger，并在 Editor UID scan 后用 Compatibility/Forward+ 验证 1/74 Catalog、canonical JSON、revision、temp/backup/quarantine 恢复与终局摘要。该批没有视觉载荷，自动可观测性门禁通过后直接保持 `Validated/UnityOwned`。
- Phase 7A UI/Input DTO 存在时，统一入口会重编 audit-only draft，通过 `PlayableRunSceneBuilder` 校验或生成 canonical `Main.tscn`，显式保留 Main UID 并比较两轮 SHA-256；随后刷新 `Generated/UnityOwned` evidence，并在 Compatibility/Forward+ 启动 Home/74 Catalog smoke。UI/Input 的鼠标、键盘、resize、完整三战导航与 Reload 仍保留人工闸门。
- Phase 7A UI/Input evidence 同时绑定 Adapter-owned 的 `godot-playable-enemy-speed-v1` Resource；统一入口连续两次运行 `PlayableRunSceneBuilder`，比较 Main Scene 与敌方速度 Resource 的 SHA-256，并由 Python receipt 校验 contract/hash。该配置不进入 gameplay Catalog，也不改写冻结 Unity Unit Resource。

## Validation

统一入口为 `Tools/godot/Verify-GodotProject.ps1` 与 `Tactics.Godot.slnx`：锁定 restore、单节点 build、Core/Application/FrozenOracle NUnit、Gameplay Spec 编译与 Main.tscn 报告、Python、Skill/Incident lint、GdUnit、Release build、Godot Runtime/Editor headless、双 renderer、ownership receipt 与 OKF。它不暴露 Unity ownership skip 参数，并默认拒绝 Unity 四个工程根目录。Godot Gameplay Spec 报告必须由本轮 GdUnit 新生成，并精确包含预期 scenario/checkpoint、生产 save 前后证据和零临时节点；真实 Editor Assembly Reload 和表现可读性仍单独记录为人工边界。

删除前预演由 `Test-UnityRetirementManifest.ps1` 在系统临时副本逐文件应用 `unity-deletion-manifest-v1` 后调用正式主线 verifier。首次干净项目扫描使用 Godot `--import` 等待 UID/import 完成；GdUnit test host 位于 `godot/tests/`，生成的 runner source由验证器从版本控制模板临时注入并在 finally 清理。每个 suite 使用独立 native host；仅对没有断言失败计数的已知 Windows native crash 或 ResourceLoader 空资源宿主故障重试一次，真实测试失败立即终止。OKF 只允许 deletion manifest 已审计的历史 Unity 来源前缀缺失，当前 Godot 和 FrozenOracle 路径必须存在。

Windows RC staging 必须执行正式主线 verifier，随后通过同一 .NET 9 feature band、单节点 build、headless Editor scan、生产 Release 测试依赖排除、Compatibility smoke、Windows export、包审计和 EXE 启动 smoke。2026-08-15 hosted run `31889338418` 已通过 ExportRelease、199 文件包审计、Compatibility/default renderer EXE 启动并上传 14 天 artifact；剩余外部交付闸门是下载后的 clean-machine 人工启动与玩法 smoke。

ExportRelease publish 必须同时使用临时 `GodotProjectDir` 和 `--artifacts-path` 隔离中间产物；脚本在 publish 前后校验 canonical Editor `project.assets.json` 哈希不变，统一验证器也要求其中存在 `GodotSharpEditor/4.7.1`，防止 export 污染 Editor 依赖图并令 typed C# Resource 退化为基础 `Resource`。

EditorPlugin 直接实例化的 C# custom Resource 必须同时具备 `[GlobalClass]` 与 `[Tool]`；前者注册类型，后者允许 Editor 执行。GraphEdit 重建只清理工具自身创建的 `GraphNode`，不得遍历删除全部子节点，否则会破坏引擎 connection layer。

Phase 7E 的等距棋盘由 `IsometricBattleBoardLayout` 提供唯一投影/逆投影合同，`GodotIsometricBattleBoard` 只绘制 Application Snapshot 并发送已有 cell intent。Phase 8A 的 `BattlePresentationFrameCompiler` 只消费 transition 前后 Snapshot 与已提交事件，Godot Tween 队列只消费 cue，不参与 RNG、伤害或 BattleResult。Phase 8B 的 Fireball/Bone Spear/Thrust 临时 FX 只消费 cue 路径和真实 affected-unit 集合，并严格排除 Piloto Prefab、纹理、材质、Shader 与 Audio。统一入口连续两次运行 `IsometricPresentationAssetBuilder`，比较 canonical Catalog、Board、Standard Unit 与三个技能 Resource SHA-256；新增路径固定 ledger UID。当前 canonical Catalog 精确为 119。

Editor lifecycle Skill 的 PowerShell 内核另由迁移 Python 测试验证 canonical path、精确 PID、dry-run 和禁用强杀；真实可见 close/reopen smoke 只在用户允许窗口出现后执行，并以新 MCP session/path/version/plugin/readiness 和日志为验收。

若本机 `.codex/config.toml` 已存在，统一入口会先运行项目 MCP 配置检查；CI 或尚未执行首次 Configure 的机器明确跳过该本机接入检查。配置脚本的 Python unittest 独立覆盖首次迁入、重复 no-op、无关用户配置字节保持、Profile 累积、永久禁用、版本/端口/launcher/双配置漂移拒绝、事务回滚与 PowerShell 默认项目根解析。

2026-08-09 已在 canonical Editor 执行一次 Codex Configure；同步脚本验证 Windows `pythonw.exe` 无窗口 bootstrap、`godot-ai==3.1.2` 和 8000/9500 后，将表原子迁入本机忽略的项目配置，并从用户配置移除。重复迁入为 no-op，`-Check` 与 Godot worktree 的 `codex mcp list` 均确认 `phase3-observe` 的 16 个工具和 pinned Attach。随后从 Godot worktree 重启 Codex 并完成在线 smoke：唯一 Session 指向 `godot/project.godot`，Editor/Scene/Resource 读取与 `godot_ai_smoke` 3/3 通过；main run 的 helper/log/screenshot、stop 以及 plugin reload/reconnect 均成功。首次 cold `project_run` 曾在调用侧 5 秒窗口报 timeout，但即时回读证明同一次 run 已进入 live；reload 后窄重试正常返回，此非稳定复现问题记录在 `.agents/incidents/godot/godot-ai-project-run-cold-timeout.md`。

2026-08-09 Phase 1A worktree 完整验证通过：Core NUnit 23、Application NUnit 3、迁移工具 14、Agent policy 8、OKF 14、GdUnit 4、verified Incident 7、Godot Skill 6；生产 Release 的程序集和 `.deps.json` 均不含 GdUnit/TestPlatform，Poison Spear catalog/Core/Tween/Scope 与 EditorPlugin enter/exit headless 通过。Core 与 Godot TestHost 均重放 Golden schema v3 的命令、显式 RNG、顺序事件和最终状态；这证明 Core replay 合同，不代表冻结 Unity Poison Spear 的完整资产与数值等价。

同日人工重新打开 canonical Editor 后确认：`Tactics Tooling` 正常进入 tree 并注册 Dock，Poison Spear Lv1 Presentation 加载 3 个节点，GraphEdit/SubViewport 就绪，godot-ai v3.1.2 启动并连接，未复现 duplicate type 或 assembly unload/reload 错误。Zed `settings.json` 的 JSON parse warning 不影响本次 godot-ai 连接闸门。

Phase 1B 完整验证通过：Core NUnit 24、Application NUnit 3、冻结 Unity linked-source Oracle 3、迁移工具 16、Agent policy 8、OKF 14、GdUnit 4、verified Incident 7、Godot Skill 6。迁移工具同时核对 Oracle Matrix blob、冻结 commit 与 C# harness 常量；生产 Release 不含测试依赖，Poison Spear runtime/presentation、EditorPlugin headless 生命周期通过，并在 GdUnit 后恢复生产 Debug Adapter。

Phase 1C 扩展 linked-source Oracle 到 9 个冻结 blob，覆盖当前轮 remaining 动态重排、RuntimeScope ownership/fault/re-entrant dispose/timeout 和 Presentation Fork/Join。Golden schema v5 由 Core、冻结 Oracle 与 Godot TestHost 消费；完整门禁为 Core 26、Application 3、Unity Oracle 8、迁移工具 16、Agent policy 8、OKF 14、GdUnit 4，生产 Release/Debug、Poison Spear 和 EditorPlugin headless 均通过。

Phase 1D 将无可逐语句对齐的边界显式记为版本化合同：`battle-transition-v1` 是迁移合同，`splitmix64-v1` 是替代冻结 Unity 混合随机源的确定性合同；六个证据 blob、运行时 ID、Golden 与 Godot TestHost 相互校验。完整门禁为 Core 27、Application 3、Unity Oracle 8、迁移工具 20、Agent policy 8、OKF 14、GdUnit 4。

Phase 2 源侧真实管线已运行：Unity 6000.3.11f1 后台 AssetDatabase exporter 从最终 Tag 对应的 7 个 Poison Spear Lv1 根资产导出 25 个对象/24761 个字段，记录 GUID/LocalFileId/blob/dependency hash/引用，补入 `buff.poison`，并在 Gradient 支持补齐后达到 0 warning。连续两次导出 byte-identical，receipt 固定为 `Tools/migration/manifest/receipts/poison-spear-lv1-export.json`。真实 batch 当前只到 `Exported/UnityOwned`；`Tools/migration/staging.py` 的冲突、UID、回滚和幂等测试通过，但 ResourceSaver 真正生成目标资产仍是 Phase 3。

Phase 3 自动生成已把真实 DTO 编译为一次性 typed Draft，并经 Application 生成 6 个内容条目。ResourceSaver 直接生成 Poison、Skill、Presentation、10×10 fixture、Projectile/Impact PackedScene 和 Catalog；连续两次运行 7 个目标与 ledger 全部 byte-identical，generation receipt 由 draft/ledger 自动刷新。最终资产显式序列化迁移数值，不依赖 C# 默认值。Core `battle-transition-v2` 采用真实 Range=5、Mana=6、Damage=8、Poison AddDuration、TurnStart 2 点 tick、TurnEnd duration、持矛与半径 3 确定性掉落语义；Oracle Matrix 绑定 15 个冻结 blob。Application/Godot Editor Adapter 已加入 SHA-256 Revision、expectedRevision、typed ChangeSet、Undo plan 同步与保存失败回滚。当前 Godot VFX 是未复制 Piloto 资产的程序化占位；batch 为 `Generated/UnityOwned`，人工 Editor/视觉验收仍 pending。

Phase 3 人工闸门前统一验证通过：Core NUnit 31、Application NUnit 10、冻结 Unity Oracle 9、迁移 Python 44、Agent policy 8、GdUnit 5、OKF 14；ResourceSaver 双生成、UID scan、Release 测试依赖隔离、Compatibility/Forward+、Runtime/Tween/Scope、6 scopes/0 unmapped 与 patch whitespace 均通过。

Phase 3 Editor authoring 可用性修复后再次通过统一门禁：Core 31、Application 13、Unity Oracle 9、迁移 Python 44、Agent policy 8、GdUnit 5、OKF 14；真实坐标、Revision/position ChangeSet、ResourceSaver 双生成、UID scan、Release 隔离、两个 renderer、Runtime/Tween/Scope 和 EditorPlugin headless 生命周期均通过。人工 Graph drag/Undo、Split Preview、Save 与 Assembly Reload 仍必须在 canonical Editor 验收。

Tactics Tooling 随后从 Bottom `EditorDock` 迁为官方 Main Screen Plugin，在中央工作区通过 `Tactics Tooling` 入口切换；内部 Graph/SubViewport 改为 64/36 左右分栏。首次热重载暴露了 live tool 字段从 `VSplitContainer` 直接改为 `HSplitContainer` 时的 `RestoreGodotObjectData` 类型恢复错误，字段收敛为共同 `SplitContainer` 基类后，后续 Build/Reload 无新增错误。横向初始比例使用 child stretch ratio，而不是在 dragger 初始化前调用 `ClampSplitOffset`；Preview 使用居中的 `AspectRatioContainer(Fit)` 保持 `640:180`，避免 SubViewport 随右侧面板非等比压缩。Graph 的 `SaveWithRollback` 在 `ResourceSaver.Save` 后恢复原 Resource UID，避免语义 revision 正确但迁移 ledger target hash 漂移；成功保存的 UID 保留、失败保存的 byte rollback 和 Preview Fit 配置由 GdUnit 覆盖。完整 Editor 重启后，Main Screen、6 节点/4 edge、Undo/Redo、Save、Assembly Reload、Runtime 与等比 Preview 人工验收均通过；随后完整门禁为 Core 31、Application 13、Unity Oracle 9、GdUnit 6，headless Editor 日志干净。

Phase 4 执行前项目级 MCP Profile 已从 `phase3-observe` 切换到 `content-authoring`，统一入口确认 pinned Attach 与 23 个白名单工具；写入前均确认 live Editor session 为 0，因此 Unity AssetDatabase export 通过后台 Unity batchmode，Godot 生成/导入/双 renderer/截图均通过 console/headless 完成。Unit 方向/Reset/山羊身体 shader、Sprite pivot/Shadow PPU/Transform 修复后，Gallery 使用脚底/尸体落点合同。项目已从 1280×720 stretch 源改为原生 1600×900 逻辑/窗口画布；Spawn 固定出生格收在1..8，并验证8px网格外框与24px viewport完整 Body/Shadow AABB安全区。运行 fixture、软件截图、ResourceSaver semantic、receipt 和 GdUnit 共用 `native-1600x900-v1`。canonical Editor 人工验收已覆盖四向、死亡/Reset、Goat tint、比例/Shadow、resize、Spawn 外框与 Assembly Reload/Output；batch 为 `Validated/UnityOwned + passed_for_migrated_project_owned_unit_visuals`。

Phase 5A 冻结的 14 Buff/3 Consumable/12 Equipment draft 生成 13 个新 Buff、3 个 Consumable、12 个 Equipment Resource，`buff.poison` 只引用既有 Poison Spear Resource；分批 Catalog 为 29 项，29 个批次独占 artifact 两轮 byte-identical。该批不含视觉 payload，保持 `Validated/UnityOwned`。

Phase 5B 已生成三名角色的起始 Lv1/隐藏技能与两个公共基础攻击，共 11 个新 Skill Resource；Poison Spear 继续由原 batch 所有。Core/Application 使用 `battle-transition-v4` 与统一 `UseSkillCommand` 解释伤害、状态、直线首目标、召唤、拾矛及被动修正。12 项 Skill Catalog 与 canonical 58 项 Catalog、UID、13 个批次 artifact、双次 byte-identical、GdUnit 17、Compatibility/Forward+ 均通过；用户已验收 1600×900 Fixture、操作与 Reload smoke，batch 晋升为 `Validated/UnityOwned`。正式 VFX 仍未复制。

Phase 6A 已生成四项敌方 Skill、六类 AI、两个 Layout 与 N1–N3 Encounter，共 15 项分批 Catalog、17 个批次 artifact 和 canonical 73 项 Catalog。Core/Application 使用 `battle-transition-v5`，AI 候选通过通用 Skill/Transition 合法性和结算路径；两轮 ResourceSaver、Compatibility/Forward+、GdUnit 26 与统一回归门禁通过。Fixture 自动验证单 turn/整 round、N1–N3 的 3/3/4 turns、Elite 1 turn、Reset 重放、pattern cursor 与 command guard；用户已确认 1600×900 可读性、操作响应和 Output smoke，batch 晋升为 `Validated/UnityOwned`。

## Navigation

- 规则：`.agents/rules/godot-agent-workflow.md`
- 研究方法：`.agents/skills/godot-workflow/references/research-guide.md`
- Incident 路由：`.agents/incidents/godot/index.md`
- 工具链版本：`Tools/migration/manifest/godot-tooling.json`
- 迁移架构与状态：`.agents/knowledge/plans/godot-migration.md`
