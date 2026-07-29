---
title: Pure Run 角色美术指南
status: active
verified_revision: c68dbebe
---

# Pure Run 角色美术指南

这份文档是 Pure Run 角色 Sprite 的稳定设计契约。它记录可复用的尺寸、目录和验收规则，不复制完整生成提示词或一次性候选讨论；实际 PNG 仍是外观真相源。

## 身体与屏幕契约

- 角色采用右下方 `45°` 等距视角，胶囊体、短手、短脚和粗深色轮廓构成统一的屏幕语言。角色身体锚点居于画布 `x=128`，脚掌沿等距方向错开但必须接触同一地面基线。
- 发布母版为 `256×256 RGBA`，完整可见轮廓高 `122 px`，底部基线为 `y=236`。游戏预览为 `128×128 RGBA`，按等比缩小后目标轮廓约 `62 px`，基线为 `y=118`。
- `64×32` 是 Tile Review 的参考占用，不是角色母版尺寸。武器、耳朵、翅膀或法术等外部轮廓可以改变横向包围盒，但不能借此缩放或漂移胶囊身体；未校准外部轮廓只能进入候选目录。

## Unity 运行时导入映射

- `Assets/Tactics/Arts/PureRun/Textures` 中用于标准胶囊角色的 `256×256` 纹理使用 `128 Pixels Per Unit`；以 `122 px` 可见轮廓计算，角色世界高度约为 `0.95`，即约两格 Tile 高。
- `64×32` 地面 Tile 继续使用 `64 Pixels Per Unit`，对应 Grid 的 `(1, 0.5, 1)` Cell Size。不要为了匹配角色而改变 Tile PPU、Grid 或相机。
- 角色纹理保持底部 pivot `(0.5, 0.078125)`；单位 Prefab 根节点与 Sprite 子节点保持 `localScale = (1, 1, 1)`。新角色在接入 Prefab 前先设置 PPU，不用 Prefab 缩放补救导入比例。
- 单位状态不使用角色子节点上的方形 `Marker`。待命、选中、已行动和可攻击状态由 `ProceduralTileHighlightRenderer` 在 `CurrentCell` 的等距 Tile 面上绘制；友方为低饱和蓝灰，选中为柔和琥珀金，已行动为弱灰蓝，敌对/攻击范围为暖红。
- Sprite 的底部 pivot 是运行时脚底锚点。阴影必须从主 `Sprite` 子节点的该锚点加极小向下偏移定位；不要按整张透明画布的 `Sprite.bounds.min` 或固定负 Y 值定位。

## Tile 表面契约

- Pure Run 地面 Tile 使用严格 `64×32` 的平面菱形，四角透明、无侧壁；暖灰与冷蓝灰版本必须共享完全一致的 alpha、轮廓和岩面位置。
- 菱形边缘保留约 1px 的低对比细边，颜色接近各自底色，不使用深炭粗描边或亮色外框。
- 顶面使用单一低饱和底色表达简化岩石；禁止内部亮暗色块、渐变、镜面高光、裂缝、苔藓、颗粒噪声、随机污渍和独立装饰插画。
- 颜色变化只承担地面节奏和格子辨识，不改变 Grid Cell Size、Tilemap 几何、相机或角色逻辑占位；角色向上超出 Tile 的可见图像属于正常视觉范围。

## 母图、参考图与候选

- 母图（source/mother image）锁定角色身份、身体比例、脚位、盾牌和已确认的构图。局部编辑必须声明“保持不变”的区域，不得让 ImageGen 重新发明整个人物。
- 参考图只提供犬种特征、武器结构、姿态骨架或色彩启发，不迁移对方的固定比例、装备、材质、地图或画风。提示词中应明确每张输入图片的职责。
- 候选图用于比较和复盘；失败图必须隔离，不能被后续任务误当作母图或可用 Sprite。一次只生成一个角色或一个变体，先确认身体再迭代脸部、武器和层级。

## 标准流水线

1. 先确认唯一母图、角色方向、保留区域和尺寸契约；不同时生成多个角色。
2. 使用纯绿色或洋红色幕生成/编辑单个高分辨率角色，外部武器和特效完整留在画布内。
3. 去除色幕并检查 alpha 边缘；不要以带绿边的截图作为 Sprite。
4. 在 `256×256` 画布内等比缩放并定位到标准脚底基线，再生成 `128×128` 预览。
5. 在 128 预览和临时 `64×32` Tile 线框中确认脸部识别、武器轮廓、脚掌接触和遮挡关系。
6. 按 `concepts / calibrated / candidates / rejected / tmp` 的语义归档，给每个版本保留可追溯的角色名和尺寸状态。
7. 运行只读校验脚本、OKF 影响报告和 bundle/unit 检查；提交时按路径暂存，排除临时目录。

## 目录语义与当前状态

| 目录 | 语义 | 是否可作为运行时 Sprite |
| --- | --- | --- |
| `Tools/artworks/amazon` | 亚马逊线稿与提示词探索记录 | 否，除非另有校准输出 |
| `Tools/artworks/doge/concepts` | 角色设计锚点和未发布变体 | 否 |
| `Tools/artworks/doge/calibrated` | 已按统一尺寸契约校准的 Doge 发布集 | 是设计层面的可用 Sprite |
| `Tools/artworks/pure_run/enemies/candidates` | 外轮廓或尺寸仍待 Review 的怪物候选 | 否 |
| `Tools/artworks/pure_run/enemies/rejected` | 已明确否决的失败资产 | 否 |
| `Tools/artworks/pure_run/tiles` | Tile 占用与配色 Review 参考 | 否 |
| `tmp` | ImageGen、去幕和临时比较文件 | 否，永不提交 |

`c68dbebe` 是当前已提交美术资源的初始验证 revision。Doge `calibrated` 六角色为发布集；羊魔和蝙蝠仍是候选，未校准怪物不能标记为可用 Sprite；旧横胖蛤蟆位于 `rejected`，仅供失败复盘。

## 边界与关联

本契约不修改 Unity Prefab、AI、遭遇配置或运行时代码。可复用提示词文档仍由 `artworks-prompt-library` skill 负责；本项目的 [Pure Run Artwork Pipeline skill](../skills/pure-run-artwork-pipeline/SKILL.md) 负责执行、验收、归档和提交准备。详细提示词继续保存在 `Tools/artworks/amazon` 等实际资源目录，不在 OKF 页面重复。
