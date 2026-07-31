---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/Tools/artworks
title: Pure Run Artwork Pipeline
description: Pure Run 角色美术的生成、去幕、尺寸校准、Review 与提交入口。
tags: [operations, pure-run, artwork, sprite, unity]
timestamp: "2026-07-31T15:13:45+08:00"
status: active
catalog_scope: pure-run-artwork
repo_paths:
  - .agents/docs/pure-run-artwork-guidelines.md
  - .agents/skills/pure-run-artwork-pipeline
  - Tools/artworks/amazon
  - Tools/artworks/doge
  - Tools/artworks/pure_run
  - Assets/Tactics/Arts/PureRun
verified_revision: c68dbebe
source_fingerprint: sha256:58633f3252add6003beae028f5867527154f2db1535909eaa82efd02f35db055
---

# Pure Run 角色美术流水线

## Current State

- `c68dbebe` 是当前已提交角色美术的初始验证锚点；设计层正式资产由 Doge `calibrated` 与敌人 `approved` 共同组成，旧版本保留在 `rejected/superseded`，不得作为母图。
- 运行时标准角色纹理为 `128 PPU`，根节点与 `Sprite` 子节点均为 `localScale = 1`；单位状态由等距 Tile 高亮而非角色方形 Marker 表达，阴影锚定 Sprite 底部 pivot。
- 羊魔 `down-right v05 / up-left v01` 与蝙蝠 `down-right v06 / up-left v01` 已通过人工 Review，并从 `candidates` 升级到 `Tools/artworks/pure_run/enemies/approved`。小型蝙蝠按普通单位约 `75%` 的球核体量校准，球核中心在垂直方向对齐地面胶囊体上部圆帽中心，翅膀属于外部轮廓，球核中心、虚拟落点与 Tile 中心保持同轴。
- 亚马逊黑白资产只作为造型设定集保留，不进入正式四方向 Sprite 生产；方向变体从已确认的胶囊体信徒或胶囊规则怪物基础图开始。
- 方向变体以同角色已确认的 `down-right` 为唯一体量锚点；纯核心主体蒙版排除耳朵、口鼻、手脚、装备与特效，只用于测量和 QA，不参与成品合成。验收同时比较上下缘、中心、最大宽度与上中下三个截面，避免窄柱体或梨形下段。采用无手臂策略时，手掌必须以多像素接触面直接嵌入主体边缘，不能浮空或用细线连接。
- 死亡状态必须先按核心拓扑分类：胶囊地面单位以赤柴死亡图约束仰面、头朝右上、约 `60°` 且短厚平直的姿态；球形飞行单位保持近圆球核，赤柴只提供头部朝右上的屏幕方向。尺寸只比较胶囊核心或球核，完整 AABB 仅用于裁切与 Tile Review；详细约束见 skill 的死亡状态参考。
- 死灵法师概念迭代中，鬼火被移除，仅保留紧凑匕首；`doge_capsule_necromancer_death_color_v03` 是当前体量较短厚、方向正确的最新候选，`v01/v02` 保留为历史对照，均尚未接入 Unity。
- 凯利蓝㹴法师死亡图沿用同一方向与体量规则，`doge_capsule_mage_death_color_v02` 保留折耳、梗犬口鼻、蓝灰配色和 X 眼；法杖不再握持，而是像盾牌一样斜搭在下腹、与尸体轮廓重叠，作为唯一职业识别道具，未加入奥术光效，尚未接入 Unity；`v01` 保留为历史候选。
- 羊魔死亡图 `splitjaw_goat_charger_death_color_v01` 当前位于 `Tools/artworks/pure_run/enemies/candidates`：保留完整卷角、羊脸和暗紫配色，按赤柴方向仰面平躺，长柄大砍刀脱手斜搭在下腹并与尸体轮廓重叠；该版本只完成素材级与 Tile Review，尚待人工确认，未接入 Unity。
- 蝙蝠死亡图 `tomb_maw_bat_ranged_death_color_v02` 当前位于 `Tools/artworks/pure_run/enemies/candidates`：保持近圆球核并缩小到活体球核之下，耳朵与脸部线索朝画面右上，双翼随朝向旋转后贴地瘫软；赤柴只提供屏幕朝向，不能提供胶囊体轮廓或细长身体轴。`v01` 保留为球核过大的历史候选；两版均未接入 Unity。
- 无脚底尸体使用完整死亡尸体 AABB 中心对齐 Tile，不沿用站立脚底或活体悬浮锚点；道具必须脱手，默认移除常驻职业特效。未经人工确认的死亡图继续留在 `concepts/candidates`。
- 三组代表性正反案例覆盖核心胶囊体、远近手/装备层级和飞行球核。案例快照只用于 Review，正式原图路径与禁止复用的反例路径由 skill 的 `examples/cases.json` 管理。
- 设计、尺寸和目录语义见 `.agents/docs/pure-run-artwork-guidelines.md`，执行、案例库与只读校验见 `.agents/skills/pure-run-artwork-pipeline/SKILL.md`。
- 七个已接入视觉原型（猎人、死灵、法师、两类骷髅、火魔、羊魔）使用两张原生图补齐四向：East/up-left 镜像、West/down-right 镜像、North/up-left、South/down-right。该映射遵循 Unity 等距网格轴，而不是直接把原画文件名当作逻辑方向。`FourDirectionSpriteVisual` 只负责 `Sprite` 子节点的显示，不改变 `FacingResolver`、移动、技能或 AI；12 个现有 Pure Run Prefab 已分别配置对应的两张 Sprite，六个羊魔职责共用同一对羊魔图。蝙蝠仍是设计层资产，尚未接入运行时 Prefab。

## Workflow

先从案例清单选择 `calibrated/approved` 中的唯一母图，并检查适用反例；一次只生成一个角色或变体，参考图只承担犬种、武器或姿态的局部信息。方向图从同角色正确基础图原生重绘，再用纯核心主体蒙版做双色叠加与三截面验收；出现双轮廓、后脑鼓包或局部变胖时回到正式母图重生，禁止通过蒙版合成或擦线修补。死亡图先分类为胶囊地面单位或球形飞行单位，再分别锁定胶囊核心或球核；赤柴死亡图不是球形单位的身体母图。经批准保留的职业道具必须脱手，落在尸体表面或贴近对应手掌，并保持单件剪影。核心通过后才检查外部耳朵、口鼻、手脚、装备、翅膀和特效；每个方向单独声明远近手与装备绘制层级。完成去幕、alpha 检查、`256×256` 母版定位和 `128×128` 缩小后，使用真实错列等距棋盘做 `64×32` Tile Review：地面单位脚底锚点、飞行单位虚拟落点或无脚底死亡图的尸体 AABB 中心必须精确映射到目标 Tile 几何中心。通过人工确认后，再将资产归入 calibrated、approved、candidates 或 rejected。

## Relationships

- 设计契约：`.agents/docs/pure-run-artwork-guidelines.md`
- 执行 skill：`.agents/skills/pure-run-artwork-pipeline`
- 正反案例：`.agents/skills/pure-run-artwork-pipeline/references/review-casebook.md`
- 死亡状态 Sprite 约束：`.agents/skills/pure-run-artwork-pipeline/references/death-state-sprites.md`
- 正式母图清单：`.agents/skills/pure-run-artwork-pipeline/examples/cases.json`
- 相关资产：`Tools/artworks/amazon`、`Tools/artworks/doge`、`Tools/artworks/pure_run`；已接入 Unity 的纹理、Prefab、Tile 与导入设置位于 `Assets/Tactics/Arts/PureRun`，显示委托实现位于 `Assets/Tactics/Scripts/Common/Units/FourDirectionSpriteVisual.cs`。
- 提示词库边界：可复用 GPT Image 提示词文档由 `artworks-prompt-library` skill 维护，本 scope 只维护项目执行和验收状态。

## Verification Guidance

```powershell
python .agents/skills/pure-run-artwork-pipeline/scripts/validate_sprite_assets.py --root Tools/artworks --strict --review-examples
python Tools/okf/catalog_impact.py report --worktree
python Tools/okf/catalog_impact.py sync --worktree --scope pure-run-artwork --write
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
```

校验脚本只读 PNG 并输出机器可读摘要；`--review-examples` 同时验证正式母图清单、正反路径状态和 128 案例快照。候选资产需使用 `--include-candidates` 额外查看，但外部武器轮廓不会被错误地当成发布尺寸失败。Git 提交前按路径暂存并排除 `.hermes/`、`tmp/` 和任何 Unity 运行时文件。

概念死亡图当前以人工 QA 为主：胶囊单位检查与赤柴同向且平直短厚，球形单位检查近圆球核与头部朝右上；两类都只以核心体量比较大小，检查脱手道具层级、去幕后的 RGBA/透明四角、无精确色幕残边，以及以死亡尸体 AABB 中心完成的 Tile 居中。未经人工确认不得移入 `calibrated/approved` 或接入运行时。

## Citations

暂无外部引用；当前状态以仓库中的指南、PNG 和验证命令为准。
