# Sprite 尺寸契约

## 发布规格

| 项目 | 母版 | 预览 |
| --- | --- | --- |
| 画布 | `256×256 RGBA` | `128×128 RGBA` |
| 标准完整轮廓 | `122 px` 高 | 约 `62 px` 高 |
| 脚底基线 | `y=236` | `y=118` |
| 身体水平锚点 | `x=128` | `x=64` |
| Tile Review | `64×32` 线框 | 同一线框 |

轮廓高度按 alpha 非透明像素的包围盒计算，基线是包围盒最大 `y`。角色保持右下 `45°` 等距方向；两脚可以沿等距对角线错开，但不能悬空或下沉。

## Unity 运行时映射

标准角色导入到 `Assets/Tactics/Arts/PureRun/Textures` 时，`256×256` 角色纹理必须使用 `128 Pixels Per Unit`，并保留底部 pivot `(0.5, 0.078125)`。这使 `122 px` 的可见轮廓约为 `0.95` 世界单位，约等于两格 Tile 高。

地面 Tile 保持 `64×32` 与 `64 Pixels Per Unit`，对应 `(1, 0.5, 1)` Grid Cell Size。不要修改 Tile PPU、Grid、相机或角色 Prefab 的 `localScale` 来补偿角色导入比例；先校正角色纹理 PPU，再检查脚底基线。

单位 Prefab 的根节点和 `Sprite` 子节点均为 `(1, 1, 1)`。状态反馈绘制在 `CurrentCell` 的程序化等距 Tile 高亮层，不使用方形角色 Marker；阴影以 `Sprite` 的底部 pivot 作为脚底锚点，仅允许极小下偏移。

## 外部轮廓

标准校准优先保持胶囊身体的高度、中心和脚位。长矛、刀、耳朵、翅膀、法杖和特效可能改变横向包围盒；如果使完整轮廓超出 `122 px`，必须标记为 `candidates`，不能伪装成发布 Sprite。候选 Review 报告应同时记录身体包围盒和外部轮廓包围盒。

## 文件配对

每个发布母版都要有同名 `_128.png` 预览，例如：

```text
doge_capsule_hunter_color_v01.png
doge_capsule_hunter_color_v01_128.png
```

等比缩小，不能单轴拉伸。Tile 参考资产可使用 `64×32`，但不应被当作角色 Sprite 或与角色母版配对。
