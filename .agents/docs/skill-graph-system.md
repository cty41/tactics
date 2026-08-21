# 技能定义与执行图合同

## 定位

当前 `SkillDefinition`、目标协议和 `BattleTransitionService` 是技能语义权威。所谓 Skill Graph 是数据化技能定义与确定性解释链，不是 Godot 可视节点自行执行玩法代码的许可。

```gameplay-contract
id: SKILL-SHARED-LEGALITY-001
status: verified_current
statement: 玩家预览、AI 候选和执行提交必须共享 Core 的资源、次数、射程、视线、阵营、状态和目标合法性；提交前必须重新验证，失败不得部分扣费或改写状态。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
dsl_support: partial
```

```gameplay-contract
id: SKILL-PRESENTATION-BOUNDARY-001
status: verified_current
statement: 技能玩法结果在表现播放前已经提交；Godot 动画、Tween、投射物、音效、取消或缺失表现资源不得改变命中、伤害、状态、资源消耗与终局。
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/Battle/BattlePresentationFrameCompilerTests.cs
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: partial
```

```gameplay-contract
id: SKILL-AUTHORING-RESOURCE-001
status: verified_current
statement: 正式技能通过 typed Godot Resource、Catalog 和 Application compiler 映射到 Core SkillDefinition；资源修改必须经过受测作者服务、revision 与 ResourceSaver，不能手写 tres 或让编辑器插件直接裁决玩法。
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/SkillDefinitionCompilerTests.cs
  - layer: godot_test
    path: godot/tests/StartingSkillBatchGodotTests.cs
dsl_support: unsupported
```

## 新技能检查表

1. 明确目标阵营、形状、射程、LOS、费用、次数和失败语义。
2. 优先复用现有 execution kind 与效果原语；新增原语必须同时补 Core 合法性、transition、AI/预览和测试。
3. 定义 Contract ID，并让相关 gameplay spec 在 `contractIds` 中引用；DSL 暂不支持时明确标记 `partial` 或 `unsupported`。
4. 最后添加 Godot Resource 与表现；视觉和手感另走人工验收。
