# 裂颚羊魔单帧动作提示词

所有动作组合 `base_style_prompt.md` 与 `character_prompt.md`，一次只生成一个方向。只画身体与现有长柄武器，不画 VFX、独立投射物、阴影、文字或运动线。

## 通用方向层

```text
View: <down-right front three-quarter | up-left rear three-quarter>
Frame: one static action key pose, not an animation sheet
Head state: preserve horn, ear and muzzle proportions while turning toward the action axis
Torso state: preserve capsule-core dimensions; express motion through lean and limb placement, not stretching
Near hand state: overlap the body edge and declare front layer
Far hand state: remain partially occluded according to the approved direction mother
Leg state: both hooves readable on the common baseline
Weapon state: exactly one approved pole weapon, with shaft, grip and blade kept inside the canvas
Consistency: exact identity, body volume, palette, line weight, equipment count and canvas contract
```

## MeleeAttack

横扫或劈砍前的峰值关键姿态：身体向目标压低，近手引导长柄武器，远手贴身稳定杆身，黑色刃头形成清晰攻击轴。武器仍在手中，不画斩击弧或命中效果。

## ThrownAttack

远程投掷释放前关键姿态：把唯一长柄武器收至头肩后侧，杆身对准目标形成过顶投掷轴，双手与身体保持清晰接触。武器尚未脱手，不复制第二把武器，不画飞行物；Release 后投射物由独立链路表现。

## Cast

施法峰值关键姿态：长柄武器斜置或竖置在身体一侧作为仪式媒介，一只手稳定武器，另一只手掌抬起作简洁引导姿势。手掌必须贴合胶囊边缘；不画爆破、诅咒、毒雾、符文或光球，因此同一姿态可供不同技能族共用。

## Hit

受击峰值关键姿态：身体远离来击方向后仰，耳朵、眼神和口鼻表现短促受惊，双手仍握住长柄武器，尾巴随身体偏转；两蹄保持共同基线，不画伤口、数字、闪光或击退轨迹。
