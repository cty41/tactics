---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph
title: SkillGraph
description: 技能资产、解释器、Ability 桥接、共享目标规则和 Agent-first 创作验证主链。
tags: [gameplay, skills, skill-graph, unity]
timestamp: "2026-07-16T10:16:53+08:00"
status: active
catalog_scope: skill-graph
repo_paths:
  - .agents/docs/skill-graph-system.md
  - .agents/skills/skill-graph-creation/SKILL.md
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphSpec.cs
  - Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphSpecCompiler.cs
  - Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
  - Assets/Tactics/Tests/PlayMode/SkillGraphRuntimeTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:cfc076a4fb606f80df1f884ffa698befce7351d59a46120809c7517fadc046ee
---

# Current State

`SkillGraphAsset` 保存编辑态节点图，`SkillGraphRunner` 解释执行，`SkillGraphAbilityImpl` 接入既有 `IAbility`、共享 targeting 和计划执行接口。玩家预览、AI 候选及执行前重验证复用射程、阵营、AOE 展开和 LOS 结论；多目标 AOE 只执行一次图并扣除一次资源。

Unity 图编辑器支持创建、连线、属性编辑、搜索和校验。Agent 可通过 `SkillGraphSpec`、`SkillGraphSpecCompiler` 与 `SkillGraphSpecAutoFixer` 建立结构化输入，并使用 MCP 工具生成、校验和应用资产；运行语义继续由 Gameplay Test/PlayMode 测试证明。

节点集合现包含 `ApplyMana`，`SelectAlly` 可显式允许自身成为合法友军目标。运行时能力可注入使用策略：策略负责额外合法性、动态显示名和成功完成后的资源提交；图失败时不会扣除资源。Pure Run 消耗品使用该边界实现每名角色每个队伍回合一次，并在图完成后扣除对应实例耐久。

# Relationships

- [Battle System](battle.md)提供单位、格子、目标和效果环境。
- [Monster AI](monster-ai.md)通过共享合法性和执行接口选择技能。
- [Gameplay Test Framework](gameplay-test-framework.md)验证目标、阶段、状态与投射物结果。
- [Roguelike Run](roguelike-run.md)使用运行时 SkillGraph 模板执行战斗消耗品。
- 三职业首批技能的完成记录保留在[Archived Outcome](../plans/first-slice-three-class-skills.md)。
- 后续静态校验增强见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

验证单个技能时同时检查 `SkillGraphAsset`、AbilityConfig/节点执行器和对应 PlayMode 或 gameplay spec。Unity 资产通过编辑器、MCP 或项目资产工具修改，不直接写 YAML。

# Citations

[1] [SkillGraph runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph)
[2] [SkillGraph MCP tools](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs)
[3] [FirstSliceSkillAssetTests](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs)
