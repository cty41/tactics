# 裂颚羊魔单帧动作提示词

所有动作组合 `base_style_prompt.md` 与 `character_prompt.md`，一次只生成一个方向。只画身体与现有长柄武器，不画 VFX、独立投射物、阴影、文字或运动线。

## 通用方向层

```text
View: <down-right front three-quarter | up-left rear three-quarter>
Frame: one static action key pose, not an animation sheet
Head state: preserve horn, ear and muzzle proportions while turning toward the action axis
Torso state: preserve capsule-core dimensions; express motion through lean and limb placement, not stretching
Near hand state: overlap the body edge and use the action-specific declared layer
Far hand state: overlap the body edge and use the action-specific declared layer
Leg state: both hooves readable on the common baseline
Weapon state: exactly one approved pole weapon, with shaft, grip and blade kept inside the canvas
Projection state: declare world-facing direction, fixed isometric camera, top-center versus hoof-center screen x, weapon endpoints and draw order
Consistency: exact identity, body volume, palette, line weight, equipment count and canvas contract
```

## 方向与投影硬约束

- 不单独使用“向左倾”“后仰”或顺/逆时针。先写角色在 3D 世界中的面向与蓄力方向，再写固定等距摄像机，最后以屏幕坐标验收。
- DR 与 UL 都必须逐项声明：主体顶部相对脚底中心的 `x`、斧刃与杆尾象限、整根杆的连续可见/遮挡关系，以及两只手的绘制顺序。
- 羊魔动作不单独绘制尾巴；红棕杆连接的深色分叉轮廓是唯一斧刃，不得误判为尾巴或复制第二把武器。
- 无手臂策略是硬约束：手掌必须与胶囊边缘形成多像素接触，不得浮空，也不得用紫色长臂、细线或透明间隙连接。

## MeleeAttack

横扫或劈砍前的峰值关键姿态：身体向目标压低，近手引导长柄武器，远手贴身稳定杆身，黑色刃头形成清晰攻击轴。武器仍在手中，不画斩击弧或命中效果。

## ThrownAttack

远程投掷释放前关键姿态：把唯一长柄武器收至头肩后侧，杆身对准目标形成过顶投掷轴，双手与身体保持清晰接触。武器尚未脱手，不复制第二把武器，不画飞行物；Release 后投射物由独立链路表现。

- **DR 已批准契约：** 斧刃位于屏幕左上、杆尾位于右下；紧凑武器、整根斧杆和双手位于身体前层，双手直接贴住胶囊边缘，不生成手臂。
- **UL 生产契约：** 正式 UL 母图锁定背向解剖；角色在世界空间面向西北，固定等距投影后主体顶部中心必须位于脚底中心左侧。把 DR 的同一过顶姿势转入背向三分之四视图：保留斧刃高于杆尾，但水平斜向翻转为斧刃屏幕右上、杆尾屏幕左下。整把武器与双手改为身体后层，身体遮挡杆身中段和手掌内侧，只保留与胶囊边缘多像素接触的手掌外弧。

## Cast

施法峰值关键姿态：长柄武器斜置或竖置在身体一侧作为仪式媒介，一只手稳定武器，另一只手掌抬起作简洁引导姿势。手掌必须贴合胶囊边缘；不画爆破、诅咒、毒雾、符文或光球，因此同一姿态可供不同技能族共用。

## Hit

受击峰值关键姿态：身体远离来击方向后仰，耳朵、眼神和口鼻表现短促受惊，双手仍握住长柄武器，尾巴随身体偏转；两蹄保持共同基线，不画伤口、数字、闪光或击退轨迹。
