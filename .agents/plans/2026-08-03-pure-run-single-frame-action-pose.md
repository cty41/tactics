# Pure Run 单帧动作姿态增强执行计划

状态：执行中

## 已批准范围

- 在 `w1` 工作树谨慎实现，不覆盖既有未提交改动；每次续跑重新核对 `HEAD`、`origin/main` 与精确工作区状态。
- 新增 `UnitPoseFamily`、`UnitActionPoseProfile`、方向/状态解析与安全回退。
- 打通 Ability、`UnitAnimationCoordinator`、`UnitTweenVisual` 与 Tween Preview。
- 闭环赤柴持矛/空手视觉状态。
- 扩展美术为赤柴 3 对、羊魔 4 对、法师 2 对与死灵法师 2 对，并复审两张现有空手 idle；赤柴 `ThrownAttack` 复用已批准的 `MeleeAttack` 方向对，`Cast` 与 `Hit` 的 `Default / Unarmed` 分别共用各自一对无矛图。
- 法师与死灵法师只制作当前技能语义实际使用的 `Cast / Hit`；死灵动作图保留匕首并移除只属于 Idle 的蓝色鬼火。
- 后续生图使用用户手动启动的两晚队列：DR 与 UL 分阶段，每个角色/动作/方向顺序生成两个候选；夜间只完成候选加工和技术报告，不自动批准或接入运行时。
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
7. 已批准并接入赤柴无矛 Hit 方向对；后台编译、EditMode 79/79 与 PlayMode 28/28 已通过，等待真实战斗受击人工 QA。
8. 已建立法师与死灵法师动作提示词库；羊魔后续仍按逐角色、逐动作、逐方向的人工批准门禁生产。
9. 已批准并接入法师 `Cast DR v04 / UL v01`、`Hit DR v02 / UL v04`，以及死灵 `Cast DR v03 / UL v01`、`Hit DR v01 / UL v01`；8 张正式源均已晋升到 `calibrated` 并生成 `_128` Review 图。
10. 已创建两个 Cast/Hit-only Profile 并绑定对应 Prefab；自动化覆盖源文件一致性、Importer、四向镜像、恢复 Idle 与非主 Renderer 不翻转。后续会话可在保持 Profile 接口不变的前提下替换获批图片。

## 当前闸门

- 当前法师与死灵法师已使用本轮明确批准的 8 张 Cast/Hit 图；其他候选不得进入 `Assets`。
- 羊魔仍须逐张完成人工批准后才能晋升、导入 Unity 或绑定 Profile；单张候选生成授权不解除该门禁。
- 仓库暂无能提供真实战斗代表画面的后台 Gameplay Test；当前状态为 `manual_visual_qa_pending`，不得用前台窗口控制绕过。
- 完成全部视觉 QA 前保留本活跃计划；长期结论迁移到 OKF 后再删除。
