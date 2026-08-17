# 凯利蓝㹴法师单帧动作提示词

所有动作组合 `base_style_prompt.md` 与 `character_prompt.md`，一次只生成一个角色、一个动作和一个方向。只画身体、法杖与空手掌，不画 VFX、投射物、阴影、文字或运动线。

## 通用方向层

```text
View: <down-right front three-quarter | up-left rear three-quarter>
Frame: one static action key pose, not an animation frame sheet
Head state: preserve the approved folded-ear silhouette, muzzle scale and target-facing orientation
Torso state: preserve the approved capsule-core dimensions; express motion through a modest whole-body lean, never by stretching or widening the core
Near paw state: overlap the capsule edge by multiple pixels and explicitly declare the front/back layer
Far paw state: remain alpha-connected and partially occluded according to the approved direction mother
Leg state: both tiny paws remain readable and share the common y=236 baseline
Weapon state: exactly one approved short crystal staff, continuously held, fully inside the canvas
Consistency: exact identity, palette, body volume, equipment count, line weight and canvas contract
```

完整组合模板：先附加母风格与角色提示词，再选择下方一个动作段和目标方向；明确“edit only the action pose from the approved mother image, preserve the capsule core and all identity features”。

## Cast

施法峰值关键姿态：身体轻微向目标方向前倾，唯一晶体法杖必须与身体同向前倾并形成清晰施法轴，晶体作为指向目标的最前端。持杖手的中心放在杆尾至晶体底座之间的直杆中点，手掌两侧露出的杆身约为 `45:55–55:45`，不得在杆尾三分之一处握持。杆身从眼睛与口鼻下方经过，不遮挡脸部。空手掌从另一侧胶囊边缘作简洁引导动作。两只手掌都必须直接嵌入身体边缘，不能出现手臂、连接段或浮空手。法杖保持母图长度和晶体结构，不复制、不脱手、不裁切。只表达施法姿势；不画光球、符文、闪电、冰霜、火焰、光环、投射物或地面法阵，使同一姿态可供全部 Cast 技能共用。

单帧骨架：head 保持专注朝向；torso 轻度前倾；near paw 稳定法杖并位于前层；far paw 贴身引导并按方向受身体遮挡；双脚保持基线；staff 形成清晰但紧凑的施法轴。

## Hit

受击峰值关键姿态：统一参考已批准赤柴 Hit 的中度 Q 版漫画夸张。身体整体向来击反方向明显后仰，两只折耳压低并向后偏；正面两眼可见时放大眼白、缩小瞳孔，从双眼各画一条短而清晰的蓝白泪线，口部为紧张的小波浪线。泪线只是面部反应符号，不扩展为水花、粒子或魔法特效。唯一晶体法杖仍牢牢握在手中并随身体偏转。不得让法杖穿过身体、离手、复制或变形。两只脚仍接触共同基线，不画伤口、数字、星星、命中闪光、魔法泄漏或击退轨迹。`up-left` 必须把已批准 DR 的同一冻结受击时刻原生转到背向三分之四视角，只保留解剖上可见的眼睛与泪线，不为展示表情改成正面。

单帧骨架：head 随后仰方向偏转；torso 保持核心体量并整体倾斜；持杖手贴身握持；空手掌贴合另一侧身体边缘；双脚保持基线；staff 与后仰动作形成一致惯性但完整留在画布内。
