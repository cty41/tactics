# 赤柴猎人单帧动作提示词

所有动作都组合 `base_style_prompt.md` 与 `character_prompt.md`，一次只生成一个方向。动作图只表示姿态，不包含 VFX、投射物、阴影、文字或运动线。

所有动作姿态都继续采用无手臂胶囊结构：可见手掌必须直接嵌入身体边缘；位于身体后侧的远端手掌允许被胶囊体完全遮挡。手掌与身体之间不得出现任何橙色或奶油色前臂、肘部、连接段或肢体凸起；可见武器和盾牌必须直接从贴身手掌开始，被遮挡的武器可从身体后方穿过并在轮廓两侧露出。隐藏远端手掌时不得把身体轮廓向外填胖来覆盖手掌，必须继续保持母版瘦高胶囊体的核心宽高比、较直侧边和圆润底部。

## 通用方向层

```text
View: <down-right front three-quarter | up-left rear three-quarter>
Frame: one static action key pose, not an animation frame sheet
Head state: preserve the approved head size and target-facing orientation
Torso state: preserve the capsule core dimensions; lean or recoil only by pose, never by stretching the core
Near hand state: explicitly state contact and front/back layer
Far hand state: explicitly state partial occlusion by the body
Leg state: both feet remain readable and keep the common baseline
Weapon state: only the approved carried spear and shield, with explicit layer ordering
Consistency: exact character identity, palette, equipment count, body volume, line weight and canvas contract
```

## MeleeAttack / held

短促刺击蓄力关键姿态：身体微向目标压低，盾牌在近侧前方防守，持矛手把长矛收至身体侧后方，矛尖仍在画布内并指向即将突刺的攻击轴。down-right 与 up-left 必须表达同一个世界空间俯仰：刺击应近似水平或微向下，不得让 up-left 因矛尖高过耳朵或屏幕斜率过陡而读成向上挑刺。方向反转时以 down-right 批准图的约 30° 屏幕投影斜率为基准；up-left 矛尖应落在肩部或上躯干高度附近，而不是头顶上方。down-right 近侧视图中，持矛手掌直接嵌入胶囊体边缘，并握在矛杆中部或中央三分之一附近；矛尾与矛尖都从手掌两侧清晰伸出。up-left 远侧视图中，握持点应前移到胶囊体投影内部，由身体完全遮挡持矛手掌；不得在身体后缘露出白色手掌，长矛只在身体后方穿过并从轮廓两侧露出。不得让握点位于过于靠后的矛尾位置，也不得让手掌前方的矛杆显著长于后方。矛尖只需略微越过盾牌、头部或身体轮廓，不应远远伸出画面前方。盾手掌直接贴在身体边缘，盾牌从手掌处开始；两侧都不得用手臂或肢体连接段延伸动作。不得画已经脱手的矛或命中效果。

## ThrownAttack / held

赤柴首批不制作独立投掷图。`ThrownAttack` 的 down-right / up-left 直接复用已批准的 `MeleeAttack / held` 同方向 Sprite；姿态族仍保持独立，以便在 Release 当帧切换 `Unarmed` 并启动独立投射物。不得继续生成赤柴专用投掷姿态，也不得把历史投掷失败稿接入 Profile。

## Cast / spear-hidden shared

赤柴首批只制作一对无矛施法图，`Default` 与 `Unarmed` 两种视觉状态都引用同一方向 Sprite。施法姿态显示期间长矛完全缺席，圆盾仍在；空出的手掌从身体边缘作简洁引导姿势，必须直接嵌入胶囊轮廓，不得悬浮或长出手臂。up-left 中引导手属于远侧层：手掌必须先画在身体后方，再由橙色胶囊遮挡内侧大半，只允许轮廓外露出一小段奶油色月牙；不得把完整圆手掌盖在身体前层。盾手应由前左盾牌遮挡，不得出现前臂或连接段。双脚必须直接与胶囊底边多像素重叠，不得画橙色小腿、脚踝或连接楔形。只画角色姿势，不画光球、符文、毒雾、光环或文字。Cast 姿态在恢复段开始清除，随后按权威持矛状态恢复对应 idle；不得为持矛状态另行生成施法图。

## Hit / spear-hidden shared

赤柴只制作一对无矛受击图，`Default` 与 `Unarmed` 两种视觉状态共用。受击峰值采用中度 Q 版漫画夸张：身体整体向来击方向反侧后仰，耳朵后折，眼白放大、小瞳孔，并从双眼画出短而清晰的蓝白泪线；口鼻、头身比例、胶囊核心和盾牌尺寸保持角色身份，不做整体拉伸。长矛完全缺席、圆盾随身体偏转；不画手臂、浮空手掌、伤口、伤害数字、闪光、星星或额外粒子。`up-left` 必须把已批准 `down-right` 的同一冻结受击姿势在三维空间转到背向三分之四视角，保留身体倾斜、盾牌偏转和泪线惯性，不能用直立 UL idle 重画受伤表情。
