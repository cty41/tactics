# 墨西哥无毛犬死灵法师单帧动作提示词

所有动作组合 `base_style_prompt.md` 与 `character_prompt.md`，一次只生成一个角色、一个动作和一个方向。只画身体、唯一匕首和空手掌；Cast 与 Hit 都必须完全移除 Idle 蓝色鬼火，不画任何替代 VFX、投射物、阴影、文字或运动线。

## 通用方向层

```text
View: <down-right front three-quarter | up-left rear three-quarter>
Frame: one static action key pose, not an animation frame sheet
Head state: preserve the approved tall-ear silhouette, muzzle scale, asymmetric eyes and target-facing orientation
Torso state: preserve the approved capsule-core dimensions; express motion through a modest whole-body lean, never by stretching or widening the core
Near paw state: overlap the capsule edge by multiple pixels and explicitly declare the front/back layer
Far paw state: remain alpha-connected and partially occluded according to the approved direction mother
Leg state: both tiny paws remain readable and share the common y=236 baseline
Weapon state: exactly one approved short dagger, continuously held, fully inside the canvas
Off-hand state: empty attached paw; no blue wisp, flame, orb, glow or replacement prop
Consistency: exact identity, palette, body volume, equipment count, line weight and canvas contract
```

完整组合模板：先附加母风格与角色提示词，再选择下方一个动作段和目标方向；明确“edit only the action pose from the approved mother image, preserve the capsule core and remove the blue idle wisp completely”。

## Cast

施法峰值关键姿态：身体轻微向目标方向前倾，持匕首手把唯一短匕首靠近身体侧前方作为仪式性指向，另一只空手掌从胶囊边缘抬起作简洁引导。两手都直接嵌入身体轮廓，不出现手臂或连接段。右手蓝色鬼火必须完全消失，原位置只能有贴身空手掌；不得生成光球、骨头、符文、毒雾、诅咒、火焰、光环或地面法阵，使同一姿态可供召唤、诅咒、骨矛和骨盾技能共用。

单帧骨架：head 保持异色眼和目标朝向；torso 轻度前倾；匕首手贴身并形成短促指向；空手掌贴合另一侧胶囊边缘；双脚保持基线；蓝色鬼火及其辉光像素为零。

## Hit

受击峰值关键姿态：统一参考已批准赤柴 Hit 的中度 Q 版漫画夸张。身体整体向来击反方向明显后仰，两只长耳随惯性向后偏转；正面两眼可见时保留异色眼身份，同时放大眼白、缩小瞳孔，从双眼各画一条短而清晰的蓝白泪线，口部为紧张的小波浪线。泪线只是面部反应符号，不扩展为水花、粒子、骨片或鬼火泄漏。唯一匕首仍牢牢握在手中并随身体偏转，空手掌保持贴身，蓝色鬼火完全缺席。双脚仍接触共同基线，不画伤口、数字、星星、命中闪光或击退轨迹。`up-left` 必须把已批准 DR 的同一冻结受击时刻原生转到背向三分之四视角，只保留解剖上可见的眼睛与泪线，不为展示表情改成正面。

单帧骨架：head 随后仰方向偏转且耳型不变；torso 保持核心体量并整体倾斜；匕首手贴身握持；空手掌与身体多像素接触；双脚保持基线；蓝色鬼火及替代法术物件为零。
