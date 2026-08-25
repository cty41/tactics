# Pure Run 节点类型标识图输出规格

## 用途

地图集（Atlas）每个节点的 Preview 图与出口目标类型徽标共用的节点类型标识。只借《杀戮尖塔》语义映射，不复制像素画风；画风沿用项目现有粗轮廓扁平语言。

## 清单（8 张）

| 节点 | 语义映射（借 StS） | 文件名（待 promote） |
| --- | --- | --- |
| Start | 旗帜 / 路标（起点） | `pure_run_node_start_v01.png` |
| Battle | 怪物头像（普通战斗） | `pure_run_node_battle_v01.png` |
| Elite | 强化怪物头像（精英） | `pure_run_node_elite_v01.png` |
| Boss | 骷髅 / 王冠级强敌（Boss） | `pure_run_node_boss_v01.png` |
| Rest | 篝火（休息） | `pure_run_node_rest_v01.png` |
| Store | 钱袋 / 商店（商店） | `pure_run_node_store_v01.png` |
| Mystery | `?`（事件） | `pure_run_node_mystery_v01.png` |
| Treasure | 宝箱（宝箱） | `pure_run_node_treasure_v01.png` |

## 尺寸与锚点

- 母版 `256×256 RGBA`；图形内容居中，占画布中央约 `128–160 px`（保持充足透明边距，供 Atlas 实际显示缩放）。
- 预览 `128×128 RGBA`，运行时显示约 `48–64 px`。
- **无独立底衬**：图标 PNG 本身透明，由 Preview 地图或总览承载背景；运行时圆点/状态色叠在图标之下。
- 图标内容垂直居中、水平居中；不是摆件，不使用底部基线契约。

## 风格

- 粗深色外轮廓、扁平纯色块、少量内部结构线；与已批准的 doge 角色语言一致。
- 每张只表达一个类型语义；头像类图标（Battle/Elite/Boss）使用最简单的怪物剪影轮廓，不画细节毛皮。
- 无渐变、无镜面高光、无柔和光照、无像素噪点。
- 透明背景、无文字（Mystery 的 `?` 除外）、无水印、无 UI。
- 生成使用均匀纯 `#00ff00` 色幕；图形内部不得出现该颜色。

## Review

- `128×128` 预览 + 在仿 Atlas 深色背景上并排检查 8 张的辨识度：缩小到 48px 时彼此不混淆。
- Battle / Elite / Boss 三张必须从远处可分辨（建议用轮廓强度 / 角 / 骷髅元素区分，而不是只靠细节）。

## 命名

`pure_run_node_<type>_vNN.png`，母版与 `_128` 预览成对；`vNN` 自 `v01` 起递增。