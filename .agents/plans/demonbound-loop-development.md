# Demonbound 持续循环开发计划

## Summary

目标是在 Godot Pure Run 中完整交付 `.agents/docs/demonbound-class-design.md` 已确认的全部非大师内容，并修复魔剑士自动化所依赖的 Gameplay Spec Unity→Godot 迁移缺口。计划采用持续垂直切片循环；只有全部自动门禁、人工验收、固定种子与 same-seed A/B、文档和 OKF 收尾完成后才能结束。

明确排除召唤恶魔、大师技能、正式角色美术、完整 VFX 和音频。

## Current State

- Demonbound 非大师运行时、腐化/冥想、附身 AI、永久死亡和资源已经实现；厄运魔刃已由旧自身附魔改为两格推进的紫色半月斩，Catalog 因新增 Debuff 与 Presentation 两项正式资源固定为 162。左上行动单位状态卡、腐化条、Hover 浮层和新半月斩表现仍待人工可读性验收。
- Pure Run 当前四名候选择三名参战；存档为 V7，并显式清理 V1–V6 进行中 Run/未完成开局。
- Gameplay Spec 保持 Godot v2 基线，并以向后兼容 v3、`demonbound-ready-v1` checkpoint 和状态 probe 覆盖魔剑士运行时场景。
- 三个队伍组合 × 10 seed 的开局/分支稳定性 30 样本已经通过；Core 规则层自动战斗代理也已能稳定完成并重放 30 场、输出腐化峰值/冥想/附身/友伤/Down/永久死亡/技能次数。该代理使用诊断夹具而非生产 Run 路线与完整职业 AI，不能冒充完整战斗指标样本；三局人工 Run、生产配置 30 样本与 same-seed A/B 尚未完成。
- 最终 Unity Tag `unity-final-2026-08-08` 只作为历史行为 Oracle；不得恢复 Unity 工程或把 Unity API 引回主线。
- 工作树包含与本计划无关的 artwork、文档、验证脚本和 Godot 资源改动；实施时必须按路径隔离。

## Goal Acceptance Matrix

- [x] Gameplay Spec 迁移矩阵完成，相关缺口均有复现、历史证据、Godot 处理和回归测试。
- [x] 四选三开局、Demonbound 基础角色和 V7 存档升级完成。
- [x] 腐化、正念、冥想和全部非大师技能按正式设计实现。
- [x] 阵营/控制者分离、附身 AI、友军永久死亡和特殊胜利完成。
- [x] Typed Resource、Catalog、Workbench 和自动 Reload round-trip 完成。
- [x] 腐化 UI、置灰原因、紫色推进半月斩与附身占位表现完成（人工可读性及两格命中节奏待账本验收）。
- [x] Core、Application、Godot、Gameplay Spec 和统一验证全部通过。
- [ ] 三局人工完整 Run 基线完成。
- [ ] 三个 Demonbound 队伍组合各 10 个固定 seed，共 30 个完整战斗指标样本完成（开局/分支稳定性 30 样本已通过）。
- [ ] 所有采用的数值修改均完成单变量 same-seed A/B 和人工复验。
- [x] 人工验收账本、长期文档和 OKF 已更新，真正延期项进入统一缺口。

## Development Loop

每轮只选择一个可独立验收的垂直切片：

1. 从矩阵选择最高优先级未完成项，顺序为框架阻塞、核心规则、Run 集成、UI/Workbench、测试与调参。
2. 核对正式设计、当前实现和必要的最终 Unity Tag 历史行为。
3. 先建立最小失败测试或 Gameplay Spec。
4. 实现当前切片，不修改无关 dirty 路径；Core/Application 保持 Godot-free；Resource 只经受测生成链写入。
5. 运行局部测试、受影响模块测试、Spec validate/compile、tracked Plan 对比和相关 Runner 场景。
6. 运行统一 Godot verifier，更新本矩阵及人工验收账本。
7. 只要仍有可推进缺口，就进入下一轮。

## Gameplay Spec Repair Sub-loop

遇到 compiler/schema/adapter/Runner 缺口时：

1. 用最小场景区分编译、能力、执行或断言缺口。
2. 检查 `unity-final-2026-08-08` 对应 Plan model、Runner、adapter 和测试。
3. 通用语义实现 Godot 等价；Godot 已替代的行为建立明确映射；Unity 专属行为由 validator 明确拒绝。
4. 先恢复 v2 基线，再扩展向后兼容的 v3；compiler capability、schema、Runner switch 与 probe/assertion 必须一致。
5. 完成回归后返回原切片，不进行无目标的 Unity adapter 全量移植。

## Ordered Slices

1. Gameplay Spec 迁移矩阵、CLI 契约与 v2 基线。
2. 四选三、基础角色与 V7。
3. 腐化、正念与冥想。
4. 厄运魔刃与横扫。
5. 狱火冲击与地狱火。
6. 恶魔再生与腐化溢出。
7. 控制者分离与附身 AI。
8. 友军致命伤、永久死亡和胜负结算。
9. Resource、Catalog 与 Workbench。
10. UI 与占位表现。
11. Gameplay Spec v3 端到端场景。
12. 人工基线、30-seed 批测与单变量 A/B。

## Validation and Manual Boundary

- 自动化必须覆盖成功、失败、取消、非法目标、冥想组合矩阵、技能范围与成本、附身 RNG、Down/永久死亡、四选三、seed 起始技能和 V7 迁移。
- 真实玩家 Gameplay Spec 只通过 `Viewport.PushInput` 进入生产输入链，并证明生产存档与 backup 未变化。
- 自动测试不能将腐化可读性、置灰理解、附身辨识度或操作手感标记为人工通过。
- 数值批测不自动写回生产值；一次只改一个变量，使用相同 seed、路线、成长和配置回归，并由用户决定采用或回退。
- `DemonboundFixedSeedBattleProbeTests` 是可重放的规则层诊断代理：它验证批测与指标管线，但在接入生产 Run 路线、遭遇配置和完整三职业行动策略前，不计入上面的“完整战斗指标样本”。

## Blocking and Completion

- 单次失败、实现困难、dirty worktree 或仍有调查路径均不构成阻塞。
- 仅当同一外部阻塞连续至少三个 Goal 回合出现、所有安全替代路径已穷尽且无法推进其他工作时，才可标记 Goal blocked。
- 全部矩阵、自动门禁、人工验收、调参证据和知识收尾完成后，按 `project-doc-organization` 合并长期结论、更新 OKF、删除本计划，再将 Goal 标记 complete。

## Handoff Notes

- 首先阅读 `.agents/docs/demonbound-class-design.md`、Gameplay Spec 当前 schema/capabilities/Runner，以及 `.agents/rules/godot-agent-workflow.md`。
- 先记录精确 dirty scope；不得覆盖或暂存用户已有 artwork、文档、验证脚本和资源改动。
- Editor 当前打开；任何 reload-sensitive C#、ResourceSaver 或生成工作必须通过 `godot-editor-lifecycle` 正常关闭并恢复。
