# 战斗单位朝向规则

## 状态与表现边界

`FacingDirection` 是战斗逻辑中的四方向状态，`FacingResolver` 只负责从两个格子坐标计算方向。所有由玩家行为、AI 行为或位移产生的运行时转向统一经过程序集内部 `FacingCoordinator`；`Unit.Facing` 继续负责触发 Animator 与 Sprite 刷新。

默认出生朝向保持为玩家 `East`、敌方 `West`。Pure Run 的两张原生 Sprite 与镜像映射只解释逻辑方向，不反向修改移动、技能或 AI 的朝向状态。

## 行为策略

| 行为 | 朝向策略 |
|---|---|
| 普通玩家/AI 路径移动 | `FollowPath`，每个新路径段开始前按该段更新 |
| 冲锋者、Dash 施法者 | `FollowPath` |
| 恐惧逃跑 | 换格前朝向逃跑目标格 |
| 冲锋退让目标、击退、抛飞 | `Preserve`，保留受影响单位原朝向 |
| 瞬移 | 按技能目标格确认施法者朝向 |
| 召唤 | 继承召唤者当前朝向 |
| 受击、AOE、持续伤害 | 不改变受击者朝向 |

`FacingCoordinator.AnimateMovementAsync` 在动画期间临时订阅 `UnitLeftCell`，由每个路径段的离格事件驱动转向，并在成功、取消或异常结束时解除订阅。不得在移动结束后再用整条路径的最后一步补写另一套朝向逻辑。

同一段事件也驱动可选的 `UnitTweenVisual` 纸片移动表现：每段开始时按当前路径方向启动摆动，进入格子后复位。`Preserve` 被动位移既不改朝向，也不播放主动移动摆动；Tween 只改变主 Sprite 的视觉 Transform，不改变单位逻辑位置。

## 玩家预览

技能进入目标选择后，悬停任意有格位的单位或棋盘格都会更新施法者视觉朝向，不要求目标当前合法。移动技能对可达格按路径第一段预览；无有效路径时直接朝向鼠标格。其他目标技能直接朝向悬停格。

鼠标离开、取消技能或释放失败不会恢复进入瞄准前的朝向，而是保留最后一次有效预览。点击目标时仍会再次确认目标方向；自身恢复、无目标和同格目标不额外转向。

有序多目标技能在进入选择时锁定用于合法锥形计算的方向。后续悬停可以改变视觉朝向，但不能改变该次选择已经锁定的合法范围。

## 验证

核心回归位于：

- `Assets/Tactics/Tests/PlayMode/FacingBehaviorPlayModeTests.cs`
- `Assets/Tactics/Tests/PlayMode/SharedBattlePrimitivesTests.cs`
- `Assets/Tactics/Tests/PlayMode/SkillGraphRuntimeTests.cs`

测试需覆盖转弯路径的逐段事件顺序、主动/被动位移策略、悬停合法与非法目标、有序目标锁定，以及动画异常后的订阅清理。
