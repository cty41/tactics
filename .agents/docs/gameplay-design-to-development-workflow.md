# LLM 辅助策划到 Godot 开发

本文说明如何把自然语言策划需求转成可追踪的玩法合同、受约束的模型候选、确定性测试计划、Godot typed Resource 和验收证据。它是端到端使用入口；测试 DSL 细节见 [Gameplay Test Framework](gameplay-test-framework.md)，资源写入细节见 [Godot Authoring Spec → MCP 工作流](godot-authoring-spec-workflow.md)。

## 解决的核心痛点

单次 Codex 会话可以阅读设计、提出草案并实现代码，外接 LLM 并不是因为 Codex 在能力上做不到。真正的问题是：一次会话中的语言理解与中间判断默认不可复用、不可批量执行，也不天然形成稳定的项目接口。设计散落在长文档时，不同 Agent 还可能遗漏约束、把灵感误当规则，或让测试、Resource 与 Core 行为逐渐漂移。

这套流程把职责拆开：

- 人类与 Codex 负责收束意图、审查候选、补齐底层能力和承担最终实现判断。
- OpenCode Go、Ollama 等 Provider 只负责受限的文本提取与结构化候选生成。
- Schema、Contract ID、逐字证据、capability 和 compiler 负责确定性裁决。
- Application 作者合同、Catalog revision、Godot Editor service 与 ResourceSaver 负责正式资源写入。
- 自动测试证明规则和接线；人工验收判断视觉、手感与平衡。

因此 Provider 可替换、命令可跨会话重复、失败模式可单测，而模型输出永远不是玩法权威。

## 总览

```text
需求收束
  → gameplay-contract
  → Provider 检查
  → 合同提取
  → ScenarioDraft / EnemySliceDraft
  → validate / compile
  → typed authoring / ResourceSaver
  → 自动测试
  → 人工验收
```

每一箭头都是门禁。上一阶段没有得到明确、可验证的输出时，不进入下一阶段。

## 1. 需求收束

**目的**：把“做一个新敌人/技能”转成没有关键产品歧义的权威设计，而不是让模型补全缺失决定。

**输入**：自然语言想法、当前玩法文档、代码和测试。新增角色、技能、状态、棋盘交互、朝向或机制时，先按 [Gameplay Design Constraints Skill](../skills/gameplay-design-constraints/SKILL.md) 核对既有合同。

**需要明确**：

- 单位定位、基础数值、移动类型和遭遇位置。
- 技能的目标、费用、次数、结算顺序和特殊效果。
- AI 何时行动、如何排序、如何稳定打破平局。
- 新规则与沿用规则的边界，以及尚未实现的底层能力。
- 所需素材、运行时表现、自动验证和人工验收条件。

**输出**：`.agents/docs/` 中唯一的当前设计文档。未确认灵感不能写成已实现事实；无法从仓库证明的行为必须标为待确认或缺口。

**失败方式**：仍存在会改变 Core 语义的选择，例如“吸血按面板伤害还是实际伤害”“飞行能否停在障碍上”。此时继续追问，不能调用模型替人决定。

**大嘴蝠实例**：最初的“飞行蝙蝠怪”被收束为 HP 14、MoveRange 5、Air、咬击实际伤害 50% 吸血、Predatory Diver 目标排序、浅水成本与飞越/落点规则，记录在 [大嘴蝠敌人纵切设计](maw-bat-enemy-slice-design.md)。

## 2. 建立 `gameplay-contract`

**目的**：给一个稳定、原子、可验证的规则分配长期身份，使设计、测试和实现可以引用同一个 Contract ID。

只有语言标记为 `gameplay-contract` 的 fenced block 才是正式合同。下面用普通 YAML 展示字段结构，避免使用指南本身重复注册示例 ID：

```yaml
id: SKILL-BITE-LIFESTEAL-001
status: verified_current
statement: 咬击按最终实际 HP 伤害的 50% 向下取整恢复攻击者，伤害事件先于恢复事件，恢复事件先于死亡结算。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
dsl_support: partial
```

字段含义：

- `id`：稳定且语义唯一；改变规则含义时应审查是否需要新 ID，而不是悄悄复用。
- `status`：当前结论状态；不能把计划中的行为标成 `verified_current`。
- `statement`：完整、可测试的原子规则，不混入背景故事和实现建议。
- `verification`：指向真实 Core、Application、Godot 或其他权威证据。
- `dsl_support`：`supported` 表示通用 Scenario DSL 可完整覆盖；`partial` 表示仍需专用测试；`unsupported` 表示当前 DSL 无法表达，不表示规则无效。

**输入**：已收束的权威设计。**输出**：带稳定 ID 的合同注册内容。普通散文、模型候选 JSON 和 brainstorm 都不能替代它。

**门禁**：

```powershell
npm --prefix Tools/gameplay-test-spec run build
node Tools/gameplay-test-spec/dist/src/cli.js validate-contracts -d .agents/docs
node Tools/gameplay-test-spec/dist/src/cli.js contract-coverage --docs .agents/docs --specs Tests/gameplay-specs
```

`missing-spec` 是尚无通用 ScenarioSpec，`unsupported` 是 DSL 暂不支持；两者都应如实保留，不能为了绿色报告虚构测试。

**大嘴蝠实例**：`MOVE-TERRAIN-COST-001`、`MOVE-AIR-FLYOVER-001`、`SKILL-BITE-LIFESTEAL-001`、`AI-PREDATORY-TARGET-001` 分别约束移动成本、飞越落点、吸血顺序和 AI 目标选择。

## 3. Provider 检查

**目的**：在发送项目正文前验证配置、密钥权限、模型发现和结构化输出协议。

默认配置位于仓库外：

```text
%LOCALAPPDATA%\Tactics\gameplay-test-spec\providers.json
%LOCALAPPDATA%\Tactics\gameplay-test-spec\secrets.json
```

模板位于 `Tools/gameplay-test-spec/examples/`。`secrets.json` 必须撤销继承，只允许当前用户、SYSTEM 或 Administrators；Key 不得进入仓库、命令参数、日志或对话。

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js provider-doctor
```

`provider-doctor` 只发送固定 JSON 探针，不发送项目文档。它检查配置 Schema、secrets ACL、鉴权、OpenCode Go `/models` 中的 exact `deepseek-v4-flash`，以及 JSON Output 是否符合约定。

**输出**：不含 Key、Authorization、prompt 或响应正文的诊断与审计元数据。缺模型、权限过宽、鉴权/配额错误、超时、截断或协议错误均 fail-closed，且不会静默切换 Provider。

本地试验可显式使用 Ollama：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js extract-contracts `
  -d <design.md> -o <candidates.json> `
  --provider ollama --host http://127.0.0.1:11434 --model qwen3.5:2b
```

任何曾暴露在对话、日志或命令行中的 Key 都应撤销并重新生成，不能仅依靠删除文本补救。

## 4. 提取合同候选

**目的**：让模型从长设计文档中定位少量明确规则，降低人工漏读成本；不是让模型批准合同。

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js extract-contracts `
  -d <design.md> `
  -o <contract-candidates.json>
```

Provider 必须返回精确的一基行号、连续原文、逐字一致引用和非权威建议 ID，并且每次最多提取限定数量的规则。程序会拒绝改写原句、错误行号、拼接非连续段落、虚构证据或从上下文推断不存在的行为。

**输入**：单份设计文档。**输出**：候选 JSON。候选必须由 Codex/开发者对照产品意图审查，再写入正式 `gameplay-contract` block；候选文件本身不进入权威合同注册表。

**进入下一步条件**：合同已在权威设计文档中落地，并通过 `validate-contracts`。仅有“模型说这是规则”不能继续。

## 5. 生成 Scenario/Enemy Draft

### ScenarioDraft

**目的**：为一个明确 Contract 提出“怎样证明它”的受控测试草案。

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js generate-drafts `
  -d <design.md> `
  -c <CONTRACT-ID> `
  -o <scenario-draft.json>
```

ScenarioDraft 只能使用 Schema 注册的 adapter、setup、action 和 assertion，并必须携带目标 Contract ID。在线模型生成稿保留明确的候选 checkpoint 路径和 hash，因此不能未经审查直接晋升为正式测试。

### EnemySliceDraft

**目的**：用一个严格对象描述敌人纵切中的 Unit、Skill、AI、Battle Layout、Encounter 和素材角色。当前 `EnemySliceDraft` 是 `Tools/gameplay-test-spec` 内的受测 authoring 投影 API，不是一个可跳过审查的“一键创建敌人”CLI。

它会检查：

- 单位、技能、AI、布局和遭遇字段符合严格 Schema，额外字段被拒绝。
- AI archetype 来自支持枚举，内容引用数量一致。
- 所有 Presentation 路径命中 approved allowlist。
- Catalog 中已存在内容使用 update revision fence，新内容使用 create。

**输出**：确定性的 Unit/Skill/AI/Layout/Encounter Authoring V2 batch 候选；尚未写入 Godot Resource。

**大嘴蝠实例**：在线模型曾产生三份结构化输出，用于验证 Provider、Schema 和 compiler，但因为仍使用候选 checkpoint，没有晋升为正式 gameplay spec。最终纵切由 Agent 对照权威设计实施，不能描述为“模型一键生成完成”。

## 6. Validate 与 Compile

**目的**：把“格式看起来正确”与“项目确实支持并可执行”分开。

ScenarioDraft：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js validate-draft -d <scenario-draft.json>
node Tools/gameplay-test-spec/dist/src/cli.js compile-draft `
  -d <scenario-draft.json> `
  -o <scenario.plan.json> `
  --runtime godot
```

正式 ScenarioSpec：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js validate-spec -s <scenario.gameplay-test.md>
node Tools/gameplay-test-spec/dist/src/cli.js compile-spec `
  -s <scenario.gameplay-test.md> `
  -o <scenario.plan.json> `
  --runtime godot
```

AuthoringAssetSpec：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js validate-authoring-spec -s <authoring-spec.json>
node Tools/gameplay-test-spec/dist/src/cli.js compile-authoring-spec `
  -s <authoring-spec.json> `
  -o <authoring-batch.json>
```

Validate 检查结构、类型、Contract ID、引用、参数和 capability；Compile 只把已经合法的源输入投影成稳定 plan/batch。相同输入应产生相同输出。`.plan.json` 和 compiled batch 都是生成物，不得手改。

如果 Draft 合法但 capability 不支持，应补 runtime/DSL 或改回可证明的测试设计；如果设计本身无法被当前 DSL 表达，应保留 `partial/unsupported` 并添加专用 Core/Application/Godot 测试，不能绕过 validator。

## 7. Typed authoring 与 ResourceSaver

**目的**：将受控作者输入安全地写成正式 Godot 内容，同时保留 identity、revision、Undo/Redo、引用和 reload 证据。

固定步骤：

1. 通过 Tactics Authoring 查询现有 Catalog identity、document revision 和 reference revision。
2. 编写或由受测投影生成 `AuthoringAssetSpecV1`；update/delete/rebind 携带 revision fence。
3. 执行 `validate-authoring-spec` 和 `compile-authoring-spec`。
4. 将完整 batch 交给 `tactics_authoring_validate`，通过后原样交给 `tactics_authoring_apply`。
5. Application typed ChangeSet 校验字段、引用和 revision。
6. canonical Godot Editor service 使用 ResourceSaver 写入 Resource、Catalog 和 UID。
7. 检查 created/modified/deleted、路径、revision 和 typed reload evidence。

模型或 Agent 都不得直接手写 `.tres/.tscn`、开放任意字段 patch、绕过 revision fence，或让 EditorPlugin 自己裁决玩法语义。写入成功只证明资源事务完成，不证明运行时行为正确。

**大嘴蝠实例**：正式内容包含 Unit、咬击 Skill、Predatory Diver AI、N2 浅水 Layout 和 Encounter 引用；相关玩法能力先在 Core/Application 中实现，再由 Godot 作者服务和 ResourceSaver 形成 typed Resource。

## 8. 自动测试

自动验证按职责分层：

| 层 | 证明内容 |
| --- | --- |
| TypeScript | Schema、Provider、逐字证据、compiler、非法输入拒绝 |
| Core | 移动成本、飞越、伤害/吸血顺序、AI 评分等玩法语义 |
| Application | DTO、typed authoring、Catalog、revision 和运行时投影 |
| Godot | Resource reload、Catalog ownership、场景接线和表现消费 |
| Gameplay runner | 正式 `Main.tscn`、生产输入链、隔离存档、watchdog 和结构化结果 |
| Artwork/public release | 素材 SHA、许可、provenance 和公开边界 |

基础验证：

```powershell
npm --prefix Tools/gameplay-test-spec test
node Tools/gameplay-test-spec/dist/src/cli.js validate-contracts -d .agents/docs
node Tools/gameplay-test-spec/dist/src/cli.js contract-coverage --docs .agents/docs --specs Tests/gameplay-specs
```

Godot 计划由 `GodotGameplayRuntimeRunner` 加载正式 `Main.tscn`，使用 `Viewport.PushInput` 进入生产输入链，并隔离在 `user://qa-runner/<scenario>/<attempt>/`。完整项目变更最终走 `Tools/godot/Verify-GodotProject.ps1`。

固定 Seed、AI-vs-AI、自动截图和绿色测试只是诊断或回归证据，不能证明数值平衡、可读性、动画质量或操作手感。

## 9. 人工验收

**目的**：确认自动化无法客观裁决的体验属性。结果写入 [人工验收账本](manual-acceptance.md)。

新增敌人至少检查：

- 身份和方向辨识度，动作与受击反馈是否清楚。
- 飞行高度、Tile 落点、遮挡、移动和死亡表现是否自然。
- AI 行为是否符合玩家预期，而不只是确定性正确。
- 数值压力、遭遇节奏和技能反馈是否有趣且可读。
- 真实 Godot Editor Reload 和运行流程是否可靠。

自动化只能记录实现通过或 `manual_qa_pending`，不得替人工写成通过。失败应回到对应的设计、实现、Resource 或表现层，不应在验收账本中掩盖。

## 大嘴蝠实战复盘

大嘴蝠纵切展示了各层的责任边界：

1. 人类确认敌人定位、咬击吸血、AI 偏好、飞行和浅水规则。
2. 权威文档建立四个 Contract ID，模型只用于结构化候选 smoke。
3. Compiler 验证合同引用、ScenarioDraft 和 EnemySliceDraft 结构；候选 checkpoint 阻止在线输出自动晋升。
4. Agent 补齐 Dijkstra 地形成本、Air flyover/CanStop、实际伤害吸血和 Predatory Diver 决策等底层能力。
5. typed authoring/ResourceSaver 生成并登记 Unit、Skill、AI、Layout 和 Encounter Resource。
6. Core、Application、Godot、素材和公开发布门禁分别验证行为与血缘。
7. 视觉、手感与平衡仍保留人工验收边界。

当前实现事实必须继续核对代码、Resource 和测试；历史提交只用于复盘。主要纵切提交为 `3c62c877`，素材和公开 provenance 由后续独立提交完成。

## 日常操作清单

```text
[ ] 阅读 gameplay-design-constraints 并收束关键决策
[ ] 更新唯一权威设计文档
[ ] 写入并 validate gameplay-contract
[ ] 运行 provider-doctor
[ ] extract-contracts，只审查候选
[ ] 人工确认并注册正式合同
[ ] generate-drafts 或构造受控 EnemySliceDraft
[ ] validate，再 compile；不手改生成物
[ ] 为 partial/unsupported 合同添加专用测试
[ ] 经 typed authoring 与 ResourceSaver 写入
[ ] 跑分层自动门禁
[ ] 更新 manual-acceptance，完成真实体验验收
```

常见故障按边界处理：Provider/鉴权失败时修配置、ACL、模型或配额；候选被拒绝时修原文证据或 Draft，不放宽 Schema；Draft 合法但 runtime 不支持时扩展受测 capability 或保留明确缺口，不伪造已支持状态。
