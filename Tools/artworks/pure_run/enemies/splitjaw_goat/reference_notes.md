# 裂颚羊魔动作姿态参考笔记

## Keep

- 唯一身份母图：`../approved/splitjaw_goat_charger_color_v05.png`（down-right）与 `../approved/splitjaw_goat_charger_color_ul_v01.png`（up-left）。
- 紫色胶囊身体、粉色卷角和蹄、黑色耳朵、紫灰口鼻，以及唯一红棕长柄黑刃武器。母图中与红棕杆相连的深色分叉轮廓是武器刃头，不是独立尾巴。
- 粗深色轮廓、平涂色块、极少内部线条，以及现有前后手和武器遮挡关系。
- 六类羊魔共享同一套轮廓 Sprite，职责差异继续由 Unity 材质 Tint 表达。

## Ignore

- Prefab 材质色、阴影、Tile、Marker、VFX、投射物和命中效果。
- 参考动作中的写实羊体、肌肉、长手臂、地面与透视背景。

## Avoid Drift

- 不改变核心胶囊高度、卷角方向、耳尾形状、口鼻位置、蹄子间距或武器结构。
- 不增加盔甲、衣服、第二把武器、盾牌或职业专属挂件。
- 不把爆破、诅咒、毒液、火焰、光球、投射物或阴影烘进 Sprite。
- up-left 必须保留批准母图的背向三分之四解剖与遮挡，不能镜像 down-right。
- up-left Melee/Thrown 的主体顶部必须投影到脚底中心左侧，整把武器与双手位于身体后层。Melee UL 使用斧刃左上/杆尾右下；Thrown UL 从 DR 过顶姿势做背向投影，使用斧刃右上/杆尾左下。手掌外弧仍须贴住胶囊边缘，禁止浮空手和任何手臂。
