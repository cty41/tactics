# 战斗视线与遮挡规则

本文是 Godot 主线战斗 Line of Sight（LoS）的设计真相源。当前实现位于
`src/Tactics.Core/Pathfinding/LineOfSight.cs`；Application 预览、AI 候选和战斗结算不得维护另一套近似规则。

## 适用范围

- `SkillDefinition.RequiresLineOfSight` 为真的技能使用本规则。
- `IgnoreLineOfSight` 显式关闭检查；它不改变射程、目标阵营或技能自己的命中方式。
- Bone Spear 保留沿直线由首个敌人截获的专用语义，不把中间活单位作为普通 LoS 阻挡物。
- 射程和 LoS 是两项独立检查：在射程内不代表一定可见。

## 遮挡锥

每个格子按其正方形开放内部参与几何判断。查询从施法者格中心指向目标格中心；若这条中心射线穿过某个
阻挡格的开放内部，该格就向后形成覆盖目标中心的遮挡锥，目标不可见。若射线只接触格边或格角而没有进入
开放内部，则目标仍可见。

该边界意味着：位于对角射线单侧的单位或地形不会仅因共享角点而阻挡；真正站在射线经过格内的单位仍会
阻挡。计算必须使用确定性的整数或有理数比较，不能让浮点舍入决定边界结果。

## 阻挡资格

- 中间存活单位会阻挡，包括友军、敌军和召唤物。
- `BlocksLineOfSight` 的地形会阻挡。
- 施法者所在格和最终目标格不作为中间阻挡格。
- 尸体与落地长矛不阻挡。
- 多个阻挡物同时成立时，报告沿射线最近的阻挡格；相同进入距离按 `X`、`Y` 稳定排序。

`LineOfSightResult` 同时返回中心射线穿过的中间格、最近阻挡格、阻挡类型和单位身份。Godot 悬停详情只展示
这一结果，不重新计算视线。

## 一致性与验证

- `SkillRuntimeService` 是执行和 AI 合法性使用的权威入口。
- `PlayableBattleSessionService.PreviewSkillTarget` 使用相同 `ILineOfSightService` 合同生成预览诊断。
- Core 测试覆盖角点相切、格内穿越、非轴向射线和最近阻挡物；Application/Godot 测试覆盖寒冰箭预览与结算。
- 最终 Unity 的 supercover 规则只作为 FrozenOracle/Golden 历史证据保留；当前 Godot 产品合同为
  `godot-los-shadow-cone-v1`。

## 外部参考边界

《Mewgenics》的公开机制说明确认非拾取物单位会在其后方形成扩张的范围遮挡锥，并以从施法者延伸的向量
限制范围。它支持本项目采用遮挡锥的设计方向，但没有提供所有逐格边界案例；本文定义的是 Tactics 的当前
产品合同，不声称逐语句复刻其二进制实现。

- [Mewgenics Wiki: Range and Area](https://mewgenics.wiki.gg/wiki/Range_and_Area)
