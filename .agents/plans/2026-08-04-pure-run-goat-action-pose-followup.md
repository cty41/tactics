# Pure Run 羊魔单帧动作姿态后续计划

状态：待执行（交给独立会话）

上级背景：[`2026-08-03-pure-run-single-frame-action-pose.md`](./2026-08-03-pure-run-single-frame-action-pose.md)

## Summary

在赤柴单帧动作姿态提交、同步并推送后，完成该功能剩余的羊魔垂直切片。第一步不是生成羊魔图片，而是在真实 Pure Run 战斗中确认已接入的赤柴无矛 Hit DR/UL；只有用户明确认可实际受击效果，才允许按 `down-right -> 人工批准 -> up-left -> 人工批准` 的顺序制作羊魔近战、投掷、施法、受击四对动作图。

成功标准：六个羊魔 Prefab 共用一个动作 Profile 和八张获批动作 Sprite，保留各自材质 Tint、排序、阴影、死亡图和玩法行为；缺图或表现异常不影响技能结算；全部自动验证和真实战斗人工 QA 通过后，才可请求 Git 提交授权。

## Current State

- 当前主功能提交预计已经包含通用 `UnitPoseFamily`、`UnitActionPoseProfile`、`Default / Unarmed` 状态、四向解析、安全回退、Release/Pose Restore 时标和 Tween Preview。
- 赤柴预计已接入八张运行时图：空手 Idle、近战（同时供投掷复用）、无矛施法、无矛受击，各 DR/UL 一张；毒矛 Release 和持矛状态闭环已有测试。
- 羊魔目前只有已批准基础 Idle DR/UL 与死亡图；六个职责 Prefab 共用外观，但依靠各自 Material/Tint 区分职责。
- 羊魔动作提示词库已经位于 `Tools/artworks/pure_run/enemies/splitjaw_goat/`；不得在未复核当前 Git 状态前重建提示词库。
- 羊魔动作 PNG、`GoatActionPoseProfile.asset` 以及相关 Prefab 引用尚未创建。
- 当前会话存在其他未提交的平衡文档和测试数据；新会话必须重新检查工作区，不能假定 checkout 干净。

## Relevant Context

### 先读文件

- `.agents/docs/pure-run-single-frame-action-pose-design.md` — 姿态族、回退顺序、时间标记和美术边界。
- `.agents/docs/pure-run-artwork-guidelines.md` — Pure Run Sprite、方向、Pivot、审核和运行时契约。
- `.agents/skills/pure-run-artwork-pipeline/SKILL.md` — 生成、去幕、校准、Review 和批准门禁。
- `.agents/skills/unity-agentic-tools/SKILL.md` — Unity 序列化资产读写规则。
- `Tools/artworks/pure_run/enemies/splitjaw_goat/*.md` — 已有羊魔母图、风格、动作和输出提示词。

### 运行时与测试入口

- `Assets/Tactics/Scripts/Common/Units/Tween/UnitPoseFamily.cs`
- `Assets/Tactics/Scripts/Common/Units/Tween/UnitActionPoseProfile.cs`
- `Assets/Tactics/Scripts/Common/Units/Tween/UnitTweenVisual.cs`
- `Assets/Tactics/Scripts/Common/Units/FourDirectionSpriteVisual.cs`
- `Assets/Tactics/Arts/PureRun/Tween/ActionPoses/`
- `Assets/Tactics/Tests/Editor/PureRunTweenAssetTests.cs`
- `Assets/Tactics/Tests/PlayMode/UnitActionPosePlayModeTests.cs`
- `Assets/Tactics/Tests/PlayMode/PureRunTweenPlayModeTests.cs`

### 羊魔现有资产

- 基础图：
  - `Assets/Tactics/Arts/PureRun/Textures/splitjaw_goat.png`
  - `Assets/Tactics/Arts/PureRun/Textures/splitjaw_goat_ul.png`
  - `Assets/Tactics/Arts/PureRun/Textures/splitjaw_goat_death.png`
- 六个目标 Prefab：
  - `PureRunGoatCharger.prefab`
  - `PureRunGoatEliteCharger.prefab`
  - `PureRunGoatRanged.prefab`
  - `PureRunGoatAoe.prefab`
  - `PureRunGoatElitePoisonCaster.prefab`
  - `PureRunGoatSupport.prefab`
- 职责材质位于 `Assets/Tactics/Arts/PureRun/Materials/goat_*.mat`，不得合并、替换或改色。

## Scope

### In Scope

- 真实战斗检查赤柴持矛/空手受击的切图时机、方向、恢复和死亡抢占。
- 在赤柴受击获得用户明确批准后，制作羊魔 `MeleeAttack / ThrownAttack / Cast / Hit` 四对 DR/UL Sprite。
- 创建一个六 Prefab 共用的 `GoatActionPoseProfile.asset`，复用现有四个姿态族资产。
- 将八张获批羊魔动作图接入六个 Prefab，同时保持每个 Prefab 的 Material/Tint 等现有视觉契约。
- 扩展资产、方向、共享 Profile、打断恢复和真实 Prefab 烟雾测试。
- 更新相关设计、Pure Run artwork、battle、skill-graph OKF 内容，并在完成后清理已完成计划。

### Out of Scope

- 不制作羊魔 Idle、死亡、移动或逐帧动画。
- 不制作赤柴新图，也不修改已批准赤柴 Sprite，除非赤柴受击真实战斗 QA 明确失败。
- 不新增 `ShotAttack`、新的姿态族或装备状态。
- 不修改技能伤害、命中时序、AI、投射物、VFX、死亡逻辑、材质 Shader、构建、CI、asmdef 或模块边界。
- 不默认修改共享姿态公共 API；若现有 API 无法完成羊魔接线，停止实现并向用户说明具体阻塞和最小 API 变更方案。
- 不提交用户其他平衡、地图或测试数据改动。

## File Structure

- `Tools/artworks/pure_run/enemies/splitjaw_goat/candidates/` — 当前待审方向稿与 `_128` Review 图；仅保存本轮候选。
- `Tools/artworks/pure_run/enemies/splitjaw_goat/rejected/superseded/` — 被明确否决或被后续版本替代的方向稿。
- `Assets/Tactics/Arts/PureRun/Textures/Actions/SplitjawGoat/` — 仅存八张最终获批的运行时 PNG 及 Unity `.meta`。
- `Assets/Tactics/Arts/PureRun/Tween/ActionPoses/GoatActionPoseProfile.asset` — 羊魔默认动作族和四对方向 Sprite 映射。
- 六个 `Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoat*.prefab` — 只新增共享 Profile 引用。
- `Assets/Tactics/Tests/Editor/PureRunTweenAssetTests.cs` — Importer、Profile、Prefab 和渲染契约校验。
- `Assets/Tactics/Tests/PlayMode/UnitActionPosePlayModeTests.cs` — 羊魔真实 Prefab 的方向、动作、Hit 和恢复烟雾测试。
- `.agents/docs/pure-run-single-frame-action-pose-design.md` — 完成后记录羊魔最终映射和范围。
- `.agents/knowledge/operations/pure-run-artwork.md`、`.agents/knowledge/systems/battle.md`、`.agents/knowledge/systems/skill-graph.md` — OKF 当前实现摘要。

## Implementation

### Task 1：建立独立会话基线并确认赤柴 Hit 闸门

- 目标：证明新会话基于已推送的动作姿态提交工作，并取得继续生成羊魔图的人工授权。
- 输入：最新 `origin/main`、当前工作区状态、赤柴八张运行时图和 Amazon Profile。
- 输出：基线审计记录，以及用户对真实战斗赤柴受击效果的明确“通过”或具体调整意见。
- 涉及文件：只读检查本计划列出的运行时、资产和测试入口。
- 验收标准：
  - `git fetch origin --prune` 后记录 `HEAD`、`origin/main` 和工作区脏文件，确认赤柴动作提交已存在。
  - Unity 强制刷新编译成功，Console 无编译错误。
  - 真实战斗分别覆盖持矛与空手受击、连续受击、四方向、受击后恢复和受击后死亡。
  - 未获得用户明确批准时停止，不调用 ImageGen，也不创建羊魔候选图。

### Task 2：逐方向制作并审核羊魔四类动作图

- 目标：在不改变羊魔身份、体量和装备层级的前提下得到八张获批动作 Sprite。
- 输入：正式 `splitjaw_goat` DR/UL 母图、现有提示词库、Pure Run Sprite 契约和用户逐张反馈。
- 输出：近战、投掷、施法、受击各 DR/UL 一张批准图及对应 `_128` Review 图。
- 涉及文件：`Tools/artworks/pure_run/enemies/splitjaw_goat/` 下的 candidates 与 rejected/superseded。
- 验收标准：
  - 固定动作顺序为 `MeleeAttack -> ThrownAttack -> Cast -> Hit`；每个动作先 DR，DR 获批后才生成 UL。
  - 每次 ImageGen 只推进一个方向，不并行生成后续方向或后续动作。
  - 图像为 `256x256 RGBA`、`128 PPU` 目标规格、底部基线和 Pivot 契约一致；图内不含 VFX、投射物、阴影、文字或伤害数字。
  - 身体、武器和手部层级在 DR/UL 的三维方向感一致；128 预览中动作可读，胶囊体、角、脸和武器没有身份漂移。
  - 每张图获得用户明确批准后才标记为可导入；不合格版本移入 rejected/superseded。

### Task 3：导入八张批准图并创建共享羊魔 Profile

- 目标：用现有运行时 API 完成最小 Unity 接线。
- 输入：Task 2 的八张批准图、现有四个 `UnitPoseFamily` 资产和基础羊魔 Idle 对。
- 输出：八张运行时纹理和 `GoatActionPoseProfile.asset`。
- 涉及文件：`Assets/Tactics/Arts/PureRun/Textures/Actions/SplitjawGoat/`、`Assets/Tactics/Arts/PureRun/Tween/ActionPoses/GoatActionPoseProfile.asset`。
- 验收标准：
  - 运行时 PNG 与批准候选逐字节一致，不导入 `_128` 或 rejected 图。
  - Importer 为 Single Sprite、`128 PPU`、Pivot `(0.5, 0.078125)`、无 Mipmap，并与现有动作纹理契约一致。
  - Profile 的 Melee/Ranged/Cast/Hit 默认族分别指向 `MeleeAttack/ThrownAttack/Cast/Hit`。
  - `Default Idle` 指向现有羊魔 DR/UL；四个动作族各有完整 Default DR/UL 对；不创建 Unarmed 映射。
  - Unity 序列化资产仅通过 Unity MCP 或 unity-agentic-tools 修改，不直接编辑 YAML。

### Task 4：让六个羊魔 Prefab 共用 Profile

- 目标：六种职责使用同一动作图，同时保留原有职责换色和视觉配置。
- 输入：`GoatActionPoseProfile.asset` 和六个现有羊魔 Prefab。
- 输出：六个 Prefab 的 `FourDirectionSpriteVisual.ActionPoseProfile` 引用。
- 涉及文件：六个 `Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoat*.prefab`。
- 验收标准：
  - 六个 Prefab 引用同一个 Profile。
  - Material、Renderer Color、Sorting Layer/Order、Transform、Shadow、Collider、基础 Idle、死亡 Sprite 和已有组件数量不因接线发生意外变化。
  - 各职责仍通过原材质呈现独立 Tint；动作切图期间颜色不丢失。
  - 不为单个 Ability 显式绑定羊魔 PoseFamily；由 Profile 按现有 `VisualAction` 解析。

### Task 5：自动验证与真实战斗验收

- 目标：证明共享接线不会破坏表现或玩法，并获得最终人工视觉认可。
- 输入：接入后的八张图、Profile、六个 Prefab 和现有 Tween/姿态测试。
- 输出：自动测试结果、Preview 检查记录和真实战斗人工结论。
- 涉及文件：`PureRunTweenAssetTests.cs`、`UnitActionPosePlayModeTests.cs`，仅在已有覆盖不足时修改 `PureRunTweenPlayModeTests.cs`。
- 验收标准：
  - Editor 测试逐字节校验八张图、Importer/Pivot/PPU、Profile 映射和六 Prefab 共用引用。
  - PlayMode 覆盖四向镜像、近战、投掷 Release 清姿态、施法恢复、Hit 抢占/连续受击、移动打断、取消、销毁和死亡不继承动作姿态。
  - Tween Preview 在 `0.5x / 1x / 4x` 下检查全部四方向及 Release/Pose Restore 标记。
  - 真实战斗覆盖普通/精英冲锋近战、远程重射、范围爆破、毒法或辅助施法、非致死受击和死亡。
  - 人工验收无武器闪现、方向跳变、Tint 丢失、盾/手/武器层级错误、VFX 遮挡异常或死亡图继承动作镜像。

### Task 6：知识同步、计划收尾与提交门禁

- 目标：把最终事实写回权威文档，保持提交范围独立可审计。
- 输入：全部自动验证结果和用户最终视觉批准。
- 输出：更新后的设计/OKF、已清理的完成计划和待确认 Git 提交范围。
- 涉及文件：本计划列出的设计与 OKF 页面，以及 `.agents/knowledge/log.md`。
- 验收标准：
  - 运行 `python Tools/okf/catalog_impact.py report --worktree`，无 unmapped 路径；同步本任务实际影响的 scope。
  - `python Tools/okf/validate_bundle.py` 与 OKF 单元测试通过。
  - 将羊魔最终映射并入权威设计；删除已完成的本计划和已完全完成的上级计划，由 Git 保存历史。
  - 精确暂存本任务路径，检查全部 Unity 源文件与 `.meta` 配对、GUID 非零、`git diff --cached --check` 通过。
  - 向用户报告提交信息和精确文件数并等待确认；未获确认不得 commit 或 push。

## Test Plan

- Unity：强制 `refresh_unity(compile="request", mode="force", scope="all", wait_for_ready=true)`，确认 Console 无编译错误。
- EditMode：运行 `PureRunTweenAssetTests`、`FourDirectionSpriteVisualEditorTests` 及新增羊魔资产契约用例。
- PlayMode：运行 `UnitActionPosePlayModeTests`、`PureRunTweenPlayModeTests` 中相关用例。
- Sprite：运行项目现有 Pure Run Sprite 校验器，要求失败数为 0。
- OKF：impact report、相关 scope sync、14 个 OKF 单元测试与 bundle validation 全部通过。
- 人工：赤柴 Hit 闸门一次；羊魔每方向生成批准一次；最终 Tween Preview 和真实战斗整体验收一次。

## Risks and Assumptions

- 假设本计划开始时，当前赤柴动作姿态提交已经推送到 `origin/main`；若没有，先停止并让原会话完成同步，避免基于临时工作树继续开发。
- 假设羊魔现有 Ability 已正确提供 `UnitVisualAction`；若资产审计发现某技能为 `None` 或语义不匹配，只报告具体 Ability，不擅自修改玩法配置。
- 羊魔共用材质 Tint 可能让个别动作线稿在不同颜色下可读性下降；应优先通过候选图轮廓调整解决，不新增专用 Shader 或六套动作图。
- 图片生成具有随机性；任何未获得批准的方向都不能以“技术校验通过”替代视觉批准。
- 默认不修改共享运行时 API、工具链、程序集或模块边界；确有必要时形成最小变更提案并等待用户授权。

## Handoff Notes

1. 新会话先读本计划、上级计划、动作姿态设计和 Pure Run artwork pipeline skill。
2. 首个写操作之前执行 `git status --short`、`git fetch origin --prune` 和 Unity 实例/编译检查，确认工作区和 Editor 对应同一 checkout。
3. 第一个实际任务是赤柴 Hit 真实战斗 QA，不是生成羊魔图。
4. 用户未明确批准赤柴 Hit 时停止；用户批准后仍按一个方向一次的节奏推进 ImageGen。
5. 不使用前台 Computer Use 或抢焦点；真实战斗中后台无法完成的视觉步骤标记为人工 QA，并把具体检查项交给用户。
6. 全部任务达到各自验收标准后，按 `project-doc-organization` 将长期结论并入权威文档、同步 OKF、删除完成计划，再请求 Git 提交确认。
