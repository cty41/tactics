---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/Tools/artworks
title: Pure Run Artwork Pipeline
description: Pure Run 角色美术的生成、去幕、尺寸校准、Review 与提交入口。
tags: [operations, pure-run, artwork, sprite, unity]
timestamp: "2026-08-07T19:15:44+08:00"
status: active
catalog_scope: pure-run-artwork
repo_paths:
  - .agents/docs/pure-run-artwork-guidelines.md
  - .agents/skills/pure-run-artwork-pipeline
  - Tools/artworks/amazon
  - Tools/artworks/doge
  - Tools/artworks/pure_run
  - Assets/Tactics/Arts/PureRun
  - Assets/Tactics/Arts/Prefabs/Units/Fighter.prefab
  - Assets/Tactics/Scripts/Common/Units/TilemapUnit.cs
  - Assets/Tactics/Tests/Editor/PureRunUnitShadowEditorTests.cs
  - Assets/Tactics/Tests/Editor/PureRunUnitShadowEditorTests.cs.meta
verified_revision: c68dbebe
source_fingerprint: sha256:c76f0f290332940dcf149ad3533d9e7127351ae31f7c47e1f2e799822dff2fd4
---

# Pure Run 角色美术流水线

## Current State

- `c68dbebe` 是当前已提交角色美术的初始验证锚点；设计层正式资产由 Doge `calibrated` 与敌人 `approved` 共同组成，旧版本保留在 `rejected/superseded`，不得作为母图。
- 运行时标准角色纹理为 `128 PPU`，根节点与 `Sprite` 子节点均为 `localScale = 1`；单位状态由等距 Tile 高亮而非角色方形 Marker 表达。标准 `Sprite` 表现高度使用根空间 `localY=0.15`，降低原 `0.25` 带来的脚底悬空感；逻辑 Root、格子坐标、occupancy、碰撞与排序点不变。阴影继续锚定单位根节点代表的 Tile 几何落点，不跟随 Sprite Tween。
- 已确认的单格单位通用阴影以 `Tools/artworks/pure_run/shadows/approved/pure_run_unit_shadow_1x1_v01.png` 为设计源，并原字节复制到 `Assets/Tactics/Arts/PureRun/Textures/pure_run_unit_shadow_1x1_v01.png`。它是屏幕水平的 `64×32` 等距软椭圆，导入为 Single Sprite、`64 PPU`、中心 Pivot、Full Rect、Bilinear、Clamp、无 Mipmap/压缩/Read-Write/fallback physics shape。保守贴地参数为：地面 Scale `0.80` / Renderer alpha `0.90`，飞行为同图 Scale `0.60` / alpha `0.54`；两者都以 Tile 几何中心为虚拟落点。
- `Fighter.prefab` 的共享 Shadow 与 `PureRunGoatSupport`、`PureRunSkeletonMage`、`PureRunSkeletonWarrior` 的三个直接 Shadow 已指向新通用阴影，因而覆盖当前 12 个 Pure Run 单位。它们统一使用 `Assets/Tactics/Arts/PureRun/Materials/PureRunUnitShadow.mat` 的静态 `Sprites/Default` Shader；不得复用会摆动顶点且忽略 Renderer 颜色/alpha 的第三方 `HeliSprite/FloatingUnitShader`。Prefab 与生产初始化都把 Shadow 固定在单位根空间 `localY=-0.03`，即 Tile 几何落点附近；不能再从可能被 Idle/动作 Tween 改写的 `Sprite.localPosition` 推导阴影位置。12 个单位的 Shadow 均默认激活，`PureRunNecromancer` 不再保留禁用覆盖。legacy `Skeleton.prefab`、双方 GroundTiles Palette 与第三方 Heli 继续保留历史引用；目录级 Editor 测试自动检查新增 Pure Run 单位的 Land/Air 二选一、对应阴影参数、激活状态、静态材质和 Tile 根空间落点。
- 羊魔 `down-right v05 / up-left v01` 与蝙蝠 `down-right v06 / up-left v01` 已通过人工 Review，并从 `candidates` 升级到 `Tools/artworks/pure_run/enemies/approved`。小型蝙蝠按普通单位约 `75%` 的球核体量校准，球核中心在垂直方向对齐地面胶囊体上部圆帽中心，翅膀属于外部轮廓，球核中心、虚拟落点与 Tile 中心保持同轴。
- 蝙蝠风刃攻击 `down-right` 单帧姿态 `tomb_maw_bat_wind_blade_attack_dr_v03` 已获人工批准并于 2026-08-06 从 `candidates` 升级到 `approved`，作为当前生成状态下的临时收尾：双翼同步横扫、球核仅轻微反向旋转，设计与验收契约见 `.agents/docs/2026-08-06-pure-run-bat-wind-blade-pose-design.md`。`up-left` 姿态、飞行单位专用 Tween/Profile 与运行时接入均未开始；`v01/v02/v04` 失败稿保留在 `rejected/superseded`，`v03` 的色幕源图保留在 `concepts`。
- 亚马逊黑白资产只作为造型设定集保留，不进入正式四方向 Sprite 生产；方向变体从已确认的胶囊体信徒或胶囊规则怪物基础图开始。
- 方向变体以同角色已确认的 `down-right` 为唯一体量锚点；纯核心主体蒙版排除耳朵、口鼻、手脚、装备与特效，只用于测量和 QA，不参与成品合成。验收同时比较上下缘、中心、最大宽度与上中下三个截面，避免窄柱体或梨形下段。采用无手臂策略时，手掌必须以多像素接触面直接嵌入主体边缘，不能浮空或用细线连接。
- 死亡状态必须先按核心拓扑分类：胶囊地面单位以赤柴死亡图约束仰面、头朝右上、约 `60°` 且短厚平直的姿态；球形飞行单位保持近圆球核，赤柴只提供头部朝右上的屏幕方向。尺寸只比较胶囊核心或球核，完整 AABB 仅用于裁切与 Tile Review；详细约束见 skill 的死亡状态参考。
- 赤柴 `doge_capsule_hunter_death_color_v04`、死灵 `doge_capsule_necromancer_death_color_v05`、法师 `doge_capsule_mage_death_color_v04` 与羊魔 `splitjaw_goat_charger_death_color_v03` 已获人工授权并复制为运行时死亡纹理。它们使用 `256×256`、`128 PPU`、中心 Pivot 与 Tight Mesh，由单位视觉配置传给通用 `Corpse`；尸体通过 `Sprite.bounds.center` 抵消透明画布偏移，不按生前朝向镜像，羊魔尸体继承生前材质以保留六种职责换色。
- 骷髅战士、骷髅法师和火魔属于召唤物，死亡后不生成尸体，因而没有配置死亡纹理；蝙蝠仍无运行时 Prefab。
- 蝙蝠死亡图 `tomb_maw_bat_ranged_death_color_v02` 当前位于 `Tools/artworks/pure_run/enemies/candidates`：保持近圆球核并缩小到活体球核之下，耳朵与脸部线索朝画面右上，双翼随朝向旋转后贴地瘫软；赤柴只提供屏幕朝向，不能提供胶囊体轮廓或细长身体轴。`v01` 保留为球核过大的历史候选；两版均未接入 Unity。
- 无脚底尸体使用完整死亡尸体 AABB 中心对齐 Tile，不沿用站立脚底或活体悬浮锚点；道具必须脱手，默认移除常驻职业特效。未经人工确认或未获得运行时授权的死亡图继续留在 `concepts/candidates`。
- 法师基础奥术弹 `doge_capsule_mage_arcane_bolt_projectile_color_v02` 已通过人工尺寸 Review并接入运行时：使用短梭形蓝紫轮廓、单帧中心锚点，在 `_128` 中约 `22×10 px`，对应法师主体宽度约 `42%`。`v01` 是偏大的历史候选；奥术、火焰和冰霜 Profile 共用该 Sprite 并通过 Tint 区分。
- 死灵基础投射物以静态鬼火和飞行版分工：`doge_capsule_necromancer_pale_orb_projectile_color_v02` 保留近圆核心与向上火舌，作为静态造型锚点；正式飞行版 `v03` 朝右、亮核略靠前、短火舌向左后拖曳，`_128` 可见 AABB 约 `22×13 px`，继续用于死灵基础魔法表现，不再作为 Bone Spear 的运行时 Sprite。粗黑圆环的 `v01` 已归入 `rejected/superseded`。
- 骨矛实体 Sprite `doge_capsule_necromancer_bone_spear_projectile_color_v01` 已完成中心校准、Tile Review 和人工确认：母版约 `66×14 px`、`_128` 约 `34×8 px`。运行时使用独立 `pure_run_bone_spear_projectile.png`、中心 Pivot、`128 PPU`、`Scale=1` 和切线旋转；最多两个短残影由 Profile 驱动，交叉闪光与骨屑继续由 Skill VFX Recipe 表达。
- 赤柴长矛 `doge_capsule_hunter_spear_projectile_color_v01`、法师奥术弹 `v02`、死灵飞行能量球 `v03` 与骨矛 `v01` 的运行时 PNG 由幂等配置器从批准源复制并做内容/导入约束校验。物理基础、普通/毒矛和羊魔临时物理远程复用长矛；毒矛只使用绿色 Tint，不新增专用 Sprite 或 Shader。
- 三组代表性正反案例覆盖核心胶囊体、远近手/装备层级和飞行球核。案例快照只用于 Review，正式原图路径与禁止复用的反例路径由 skill 的 `examples/cases.json` 管理。
- 设计、尺寸和目录语义见 `.agents/docs/pure-run-artwork-guidelines.md`，执行、案例库与只读校验见 `.agents/skills/pure-run-artwork-pipeline/SKILL.md`。
- 七个已接入视觉原型（猎人、死灵、法师、两类骷髅、火魔、羊魔）使用两张原生图补齐四向：East/up-left 镜像、West/down-right 镜像、North/up-left、South/down-right。该映射遵循 Unity 等距网格轴，而不是直接把原画文件名当作逻辑方向。`FourDirectionSpriteVisual` 只负责 `Sprite` 子节点的显示，不改变 `FacingResolver`、移动、技能或 AI；12 个现有 Pure Run Prefab 已分别配置对应的两张 Sprite，六个羊魔职责共用同一对羊魔图。蝙蝠仍是设计层资产，尚未接入运行时 Prefab。
- 标准地面单位现共用一套 `StandardUnitTweenProfile`，主 `Sprite` Transform 承担 Idle、移动、攻击、施法和受击纸片 Tween；Shadow 与逻辑 Root 不参与。运行时已具备 `UnitPoseFamily` 与 `UnitActionPoseProfile` 的单帧姿态切换、`Default/Unarmed` 状态、双原生方向解析和安全回退；Sprite 可配置化切换，但 Material、Color、Sorting、Transform、Shadow、死亡图与 VFX 链保持独立。赤柴已接入空手 idle、近战/投掷复用、无矛施法和无矛受击共 4 对运行时图；Hit 的 `Default/Unarmed` 共用同一方向对并在恢复段按权威长矛状态回 idle。羊魔资产必须等待赤柴受击真实战斗 QA 通过。
- Pure Run 运行时视觉 QA 只使用 Unity MCP 截图、自动测试或 Input System 虚拟输入；运行时接入、补截图、点击技能或补齐代表单位都不授权 Computer Use、窗口激活或真实输入。后台无法构造目标状态时标记 `manual_visual_qa_pending` 并交由用户手动确认，完整边界见[前台交互与焦点保护规则](https://github.com/cty41/tactics/blob/main/.agents/rules/foreground-interaction.md)。
- `Tactics/Pure Run/Presentation Graph Editor` 是新的统一表现编排入口：GraphView 连接 Tween、投射物、第三方 Prefab FX 与程序化 Recipe，隔离舞台以固定随机种子和运行时采样逻辑预览完整语义子图，并标记 Release/Impact。旧 Tween Preview 与 Skill VFX Preview 暂时保留为叶资产调试入口；蝙蝠专用悬浮/翼展动画仍为后续任务。
- 旧 `Tactics/Pure Run/Tween Preview` 作为叶资产调试入口继续复用运行时动作与姿态解析，支持 Pose Family、`Default/Unarmed`、四方向、实际回退、`0.5×/1×/4×` 以及 Release/Pose Restore 标记；复杂语义子图仍由 Presentation Graph Editor 负责。
- 赤柴猎人与裂颚羊魔的可复用单帧动作提示词库已分别保存到 `Tools/artworks/doge/hunter` 与 `Tools/artworks/pure_run/enemies/splitjaw_goat`。赤柴 `ThrownAttack` 保留独立 Release 退出语义但复用已批准的 `MeleeAttack` 方向 Sprite，`Cast` 与 `Hit` 的 `Default / Unarmed` 分别共用各自一对无矛 Sprite；羊魔四对动作图尚未生产，仍受逐方向人工批准门禁约束。
- `Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted` 保存 Piloto Roguelike VFX Pack 的项目侧轻量适配：毒矛飞行/命中、霹雳闪电落点爆发和伤害加深诅咒法阵。适配器只复制选中的粒子子节点，去除供应商 Showcase 的绝对摆放坐标、3D 朝向、力场、碰撞、软粒子和无关烟柱/散点，并复制独立材质；项目材质副本关闭 Piloto Shader 的 `_USESOFTALPHA`，必要时使用 `Tactics/PureRun/ParticleTextureUnlit` 保留原纹理与顶点色。诅咒正式表现由三个项目自有 V2 适配 Prefab 构成：`AmplifyDamageSigilGroundV2` 分别校准暗盘、双圆环、低亮符文和中央符号，`AmplifyDamageSigilRearFlamesV2` 与 `AmplifyDamageSigilForegroundFlamesV2` 将八个固定尺寸主火柱按屏幕远近拆为三根后层与五根前层；火柱可见根部锚定外环，从 12 点方向开始以 `0.06s` 间隔顺时针点燃，火尖允许向上越过圆环。三层分别使用目标主 Sprite Sorting Order 的 `-2/-1/+2`；Lv2/Lv3 只扩大法阵和节点半径。旧 `AmplifyDamageCurse` 及 V1 双层法阵保留回退但不再由正式 Presentation Graph 引用。第三方原 Prefab和材质不修改。Lightning 与贴地法阵的 `PrimaryTargetGround` 统一锚定单位逻辑 Root 对应的 Tile 落点，不使用包含透明画布留白的 Sprite Bounds 底边；雷击适配 Prefab仍把可见下边界归一到根原点，因此向上贯穿主体但不穿过地面。运行时通过共享池重播和回收。供应商 Showcase 脚本被 Editor-only asmdef 隔离。本轮只确认技术闭包，不宣称购买来源或 EULA 已审核；授权事项按项目决定延期处理，不阻塞本轮技术提交。
- 8 个 Lightning 实例在 640×360 RenderTexture、正交相机和显式逐帧渲染下的 Profiler 样本为 66 Draw Calls、10 Batches、10 SetPass、514 Triangles、1030 Vertices；同路径空相机基线为 0。原始 Draw Calls 严格 `<10` 的目标尚未满足，Frame Debugger 在 Test Runner 手动渲染路径没有提供事件，因此 overdraw 仍需真实 Game View/目标设备人工采样。不得把暖池 Rent/Return 的 0 B 回归或混合帧 GC 数字替代为渲染性能结论。

## Workflow

先从案例清单选择 `calibrated/approved` 中的唯一母图，并检查适用反例；一次只生成一个角色、变体或投射物，参考图只承担犬种、武器、姿态或配色的局部信息。方向图从同角色正确基础图原生重绘，再用纯核心主体蒙版做双色叠加与三截面验收；出现双轮廓、后脑鼓包或局部变胖时回到正式母图重生，禁止通过蒙版合成或擦线修补。死亡图先分类为胶囊地面单位或球形飞行单位，再分别锁定胶囊核心或球核；赤柴死亡图不是球形单位的身体母图。投射物使用画布中心锚点，与施法者 `_128` 主体同屏校准，并在 Tilemap 中按真实攻击方向旋转；前一张未获人工确认前不开始下一张。完成去幕、alpha 检查、母版定位和预览缩小后，再按资产类型使用脚底、虚拟落点、尸体 AABB 中心或投射物中心完成 Tile Review。通过人工确认后，再将资产归入 calibrated、approved、concepts 或 rejected。

## Relationships

- 设计契约：`.agents/docs/pure-run-artwork-guidelines.md`
- 执行 skill：`.agents/skills/pure-run-artwork-pipeline`
- 正反案例：`.agents/skills/pure-run-artwork-pipeline/references/review-casebook.md`
- 死亡状态 Sprite 约束：`.agents/skills/pure-run-artwork-pipeline/references/death-state-sprites.md`
- 投射物 Sprite 约束：`.agents/skills/pure-run-artwork-pipeline/references/projectile-sprites.md`
- 正式母图清单：`.agents/skills/pure-run-artwork-pipeline/examples/cases.json`
- 相关资产：`Tools/artworks/amazon`、`Tools/artworks/doge`、`Tools/artworks/pure_run`；已接入 Unity 的纹理、Prefab、Tile 与导入设置位于 `Assets/Tactics/Arts/PureRun`，共享阴影入口位于 `Assets/Tactics/Arts/Prefabs/Units/Fighter.prefab`，Tile 落点布局实现位于 `Assets/Tactics/Scripts/Common/Units/TilemapUnit.cs`，显示委托实现位于 `Assets/Tactics/Scripts/Common/Units/FourDirectionSpriteVisual.cs`，阴影目录回归位于 `Assets/Tactics/Tests/Editor/PureRunUnitShadowEditorTests.cs`。
- 第三方 VFX 适配构建入口：`Tactics/Tools/Pure Run/Rebuild Piloto VFX Sample Assets`；生成器只重建毒矛、闪电、诅咒回退稿与正式三层 V2 法阵，以及对应的代表技能表现图，不批量重写其他职业资产。
- 提示词库边界：可复用 GPT Image 提示词文档由 `artworks-prompt-library` skill 维护，本 scope 只维护项目执行和验收状态。
- 前台交互边界：[前台交互与焦点保护规则](https://github.com/cty41/tactics/blob/main/.agents/rules/foreground-interaction.md)；本 scope 只补充 Pure Run 视觉 QA 的具体停止条件，不另行定义授权例外。

## Verification Guidance

```powershell
python .agents/skills/pure-run-artwork-pipeline/scripts/validate_sprite_assets.py --root Tools/artworks --strict --review-examples
python Tools/okf/catalog_impact.py report --worktree
python Tools/okf/catalog_impact.py sync --worktree --scope pure-run-artwork --write
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
```

校验脚本只读 PNG 并输出机器可读摘要；`--review-examples` 同时验证正式母图清单、正反路径状态和 128 案例快照。`PureRunUnitShadowEditorTests` 还必须覆盖当前全部 Pure Run 单位的 Sprite、静态材质、Renderer 启用状态、Land/Air 参数、Tile 根空间落点，以及 `ApplyVisualYOffset` 不会把阴影重新挂到 Sprite 姿态。候选资产需使用 `--include-candidates` 额外查看，但外部武器轮廓不会被错误地当成发布尺寸失败。Git 提交前按路径暂存并排除 `.hermes/`、`tmp/` 和任何 Unity 运行时文件。

死亡图仍以人工 QA 为发布门槛：胶囊单位检查与赤柴同向且平直短厚，球形单位检查近圆球核与头部朝右上；两类都只以核心体量比较大小，检查脱手道具层级、去幕后的 RGBA/透明四角、无精确色幕残边，以及以死亡尸体 AABB 中心完成的 Tile 居中。未经人工确认和明确运行时授权不得接入 Unity。

投射物同样以逐图人工 QA 为门槛：检查与施法者的相对体量、中心偏差、Tile 攻击轴、精确色幕和透明像素 RGB；确认前不得开始下一职业或接入运行时。

Piloto VFX 技术提交仍要求人工完成真实 Battle Camera 下的三个技能视觉 Review与 Frame Debugger overdraw 检查。购买来源/EULA 核验是独立延期事项，当前状态不得被描述为已通过。自动化性能 harness 必须通过 exact test name 单独选择；它们标记为 Explicit，不进入常规 PlayMode 全量运行。

## Citations

暂无外部引用；当前状态以仓库中的指南、PNG 和验证命令为准。
