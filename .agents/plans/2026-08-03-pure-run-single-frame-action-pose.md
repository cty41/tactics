# Pure Run 单帧动作姿态增强执行计划

状态：执行中

## 已批准范围

- 在当前脏 `main` 谨慎实现，不新建 worktree，不覆盖既有未提交改动。
- 新增 `UnitPoseFamily`、`UnitActionPoseProfile`、方向/状态解析与安全回退。
- 打通 Ability、`UnitAnimationCoordinator`、`UnitTweenVisual` 与 Tween Preview。
- 闭环赤柴持矛/空手视觉状态。
- 首批美术为赤柴 3 对、羊魔 4 对，并复审两张现有空手 idle；赤柴 `ThrownAttack` 复用已批准的 `MeleeAttack` 方向对，`Cast` 与 `Hit` 的 `Default / Unarmed` 分别共用各自一对无矛图。
- Unity 资产只通过 MCP 或 agentic tools 修改；人工美术确认前不接入运行时。
- 完成后同步 battle、skill-graph 与 pure-run-artwork OKF scope。
- 人工视觉确认前不提交运行时美术；任何 Git 提交仍需用户再次确认。

## 执行阶段

1. 保护工作区并保存设计与计划。
2. 实现运行时姿态模型、Tween 时标、视觉状态与 Preview。
3. 使用合成测试 Sprite 验证四向、回退、打断、Release 与状态闭环。
4. 建立角色动作提示词库，逐张执行美术审核。
5. 已完成赤柴试玩切片：只导入获批的空手 idle、近战和无矛施法 3 对，创建 `MeleeAttack / ThrownAttack / Cast` 与 Amazon Profile，并配置毒矛和 `PureRunHunter`。
6. 已完成资产契约、四向解析、Release/恢复时序与长矛状态自动化；赤柴基础动作真实战斗试玩已通过。
7. 已批准并接入赤柴无矛 Hit 方向对；等待真实战斗受击 QA，通过后才恢复羊魔 4 对动作图生产。

## 当前闸门

- 当前运行时只允许使用已批准的 8 张赤柴图；羊魔动作图和 `_128` Review 图不得进入 `Assets`。
- “赤柴受击真实战斗 QA 通过”是继续制作羊魔批量动作图的强制闸门；若不通过，先调整 Hit 时序、缩放或映射。
- 完成全部视觉 QA 前保留本活跃计划；长期结论迁移到 OKF 后再删除。
