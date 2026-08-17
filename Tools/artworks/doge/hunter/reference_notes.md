# 赤柴猎人动作姿态参考笔记

## Keep

- 唯一身份母图：`../calibrated/doge_capsule_hunter_color_calibrated_v01.png`（down-right）与 `../calibrated/doge_capsule_hunter_color_ul_v02.png`（up-left）。
- 赤柴橙色胶囊身体、白色口鼻与腹部、三角立耳、短脚掌、圆盾和长矛的既有造型与配色。
- 粗深棕轮廓、少量平涂阴影、清晰剪影；目标是当前 Pure Run 小体量卡通 Sprite，不是像素画或精致插画。
- 身体中心 `x=128`、脚底基线 `y=236`、中央胶囊核心体量与母图一致。

## Ignore

- 透明画布中的空白、运行时 Tile、阴影、Marker、血条和 VFX。
- `Tools/artworks/amazon` 的黑白设定稿比例与线稿表现；它只提供早期职业想法，不能替代批准母图。
- 参考动作中的写实肌肉、手臂长度、透视背景、地面和运动线。

## Avoid Drift

- 不改变头身比例、耳型、口鼻大小、盾牌直径、矛头结构或线宽。
- 不增加弓、箭、第二面盾、第二根矛、盔甲、衣服、头饰或法术物件。
- 不把 VFX、投射物、阴影、文字、速度线或命中闪光烘进角色 Sprite。
- up-left 必须原生重绘为背向三分之四视角，不得把 down-right 镜像冒充原生图。
