# Pure Run 物品图输出规格

## 用途

本目录存放 Pure Run 商店/背包的物品图。一张图双用途：商店与背包的 UI 图标（运行时缩放 + 程序化圆盘底衬），以及商店场景摆件（tile 落点锚定）。**不制作像素风**；沿用项目现有粗轮廓扁平语言。

## 首批清单

| 物品 | ContentId | 文件名（待 promote） | 视觉 |
| --- | --- | --- | --- |
| 幸运戒指 | `item.equipment.lucky-ring-01` | `pure_run_item_lucky_ring_v01.png` | 金色环 + 红色宝石 |
| 银戒指 | `item.equipment.silver-ring-01` | `pure_run_item_silver_ring_v01.png` | 银色环 + 蓝色宝石 |
| 生命药剂 | `item.consumable.life-potion` | `pure_run_item_life_potion_v01.png` | 圆肚玻璃瓶，红色液体 |
| 魔法药剂 | `item.consumable.mana-potion` | `pure_run_item_mana_potion_v01.png` | 圆肚玻璃瓶，蓝色液体 |
| 净化药水 | `item.consumable.cleansing-potion` | `pure_run_item_cleansing_potion_v01.png` | 圆肚玻璃瓶，紫色液体 |

## 尺寸与锚点

- 母版 `256×256 RGBA`，物品底部基线 `y=236`（作为摆件时脚底对齐 `64×32` Tile 几何中心）。
- 预览 `128×128 RGBA`，底部基线 `y=118`。
- 物品本体占母版高度约 `80–110 px`，水平居中 `x=128`；作为 UI 图标时由运行时缩放约到 `48 px` 显示。
- 物品本身不带圆盘底衬；程序化深色圆盘在运行时层叠加。

## 风格

- 粗深色外轮廓（约 3–4 px 视觉粗线）、扁平纯色块、少量内部结构线；与已批准的 doge 角色语言一致。
- 无渐变、无镜面高光、无柔和光照、无像素噪点。
- 透明背景、无地面、无阴影、无文字、无水印、无 UI。
- 生成使用均匀纯 `#00ff00` 色幕；物品内部不得出现该颜色。

## Review

- `128×128` 预览 + 真实 `64×32` 错列 Tile 线框，验证物品在 tile 上的比例与可读性。
- 商店三件并排与背包列表的缩放可读性属于运行时 UI 验收，不纳入本规格。

## 命名

`pure_run_item_<snake_case>_vNN.png`，母版与 `_128` 预览成对；`vNN` 自 `v01` 起递增。