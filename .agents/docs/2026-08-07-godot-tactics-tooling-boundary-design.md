# Godot 迁移：Tactics 工具边界设计

## 状态

本设计已由用户确认。`d092a955` 完成了 C# EditorPlugin、GraphEdit、Undo、SubViewport、ResourceSaver、Assembly Reload、GdUnit 和 headless 技术 Spike。当前真实 Poison Spear 已由 AssetDatabase DTO → typed Draft → ResourceSaver 生成；Application 的 SHA-256 Revision/typed ChangeSet/原子校验与 Godot Undo/保存回滚已受测。图节点位置是 Resource 中受 Revision 保护的 authoring 数据，拖动和自动布局各对应一个 typed ChangeSet/Undo action；稳定 ID 只用于引用和诊断，GraphEdit 显示语义标题。Tactics Tooling 现为 Godot 中央 Main Screen；Graph 与 SubViewport 使用可折叠、可调的 64/36 左右分栏，Preview 由居中的 `AspectRatioContainer(Fit)` 保持 `640:180`。完整重启后的 Main Screen、Graph、Undo/Redo、保存、Assembly Reload、Runtime 与等比 Preview 人工验收均已通过。

## 结论

Tactics 的领域操作不全部实现为 Godot MCP Tool。领域逻辑首先存在于 C# Application/Core 服务中，再由不同入口适配：

```text
Tactics.Application / Tactics.Core
├─ C# 领域服务与 typed DTO
├─ C# CLI / headless 适配器
├─ Godot Editor UI 适配器
└─ MCP 适配器
   ├─ godot-ai 通用操作
   └─ 少数需要当前 Editor 事务状态的 Tactics 操作
```

目标是让 `godot-ai` 的 MCP 扩展层可替换，不让领域模型、校验和资产事务依赖某个 MCP 实现。

## 操作分类

### 当前 Unity 中的有效 Presentation 操作

实现位置：`Assets/Tactics/Scripts/Editor/MCP/PresentationGraphMcpTools.cs`。

| 操作 | 职责 | 迁移判断 |
|---|---|---|
| `list_presentation_graphs` | 发现 Graph、路径和 GUID | 可由通用读取或领域 snapshot 服务承担 |
| `get_presentation_graph` | 返回 Graph、叶资产、依赖、诊断和 revision 的规范化快照 | 保留语义能力，入口可调整 |
| `validate_presentation_changeset` | 在临时副本上校验 typed ChangeSet，不写正式资产 | 保留为 C# Service；CLI 和 MCP 均可调用 |
| `apply_presentation_changeset` | 检查 expected revision 后进行跨 Graph/叶资产原子 Apply、Undo 和保存 | 需要当前 Godot Editor 的专用适配器 |
| `preview_presentation` | 用和 Runtime 相同的执行计划生成预览图、时间线和诊断 | 需要 Godot Editor/Preview 服务；不应仅靠低层属性组合 |

这些操作目前仍注册在 Unity MCP 中。旧的 Presentation Graph、Tween Preview 和 Skill VFX Preview 菜单已经由 Presentation Workbench 替代，但不等于上述 MCP 操作废弃。

### 当前 Unity 中的有效 SkillGraph 操作

实现位置：`Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs`。

| 操作 | 职责 | 迁移判断 |
|---|---|---|
| `generate_skill_graph_spec` | 从自然语言生成 `SkillGraphSpec` JSON | 优先保留为 CLI/纯工具链能力 |
| `generate_skill_graph_spec_from_answers` | 从结构化答案生成 `SkillGraphSpec` JSON | 优先保留为 CLI/纯工具链能力 |
| `generate_gameplay_test_spec` | 从自然语言生成 `.gameplay-test.md` | 保留兼容；底层 `generate-spec` 已标为 legacy helper |
| `validate_skill_graph_spec` | 当前调用 `validate-spec` 校验 Gameplay Test Spec | 仍可用，但名称未来可改为 `validate_gameplay_test_spec` |
| `apply_skill_graph_spec` | 将 Spec 编译并写入 Unity SkillGraph 资产 | Godot 侧分成 C# 编译/校验与 Godot 资产写入两层 |

### 仅为迁移设计提出、当前尚未实现的候选操作

以下名称不是当前 Tactics 或 Godot 的已实现 API：

- `validate_encounter`
- `migrate_unity_content`
- `audit_content_ids`
- `build_presentation_preview`

它们应作为 C# Service/CLI 用例设计，不应直接当成必须注册的 MCP Tool 名称。

### `godot-ai` 通用操作

场景树、Node、Resource、文件、运行、输入、截图和日志继续优先使用 `godot-ai` 原始接口。它们适合基础编辑和观察，但不自动提供 Tactics 的 revision、领域校验、跨资产 ChangeSet 或领域级事务。

Codex 接入采用项目级 Attach 与分阶段白名单：被 Git 忽略的 `.codex/config.toml` 只在 `migration/godot` worktree 生效，启动块固定 `godot-ai==3.1.2`、Windows `pythonw.exe` 无窗口 bootstrap、8000/9500 端口。Profile 从只读观察逐步扩展到内容、UI/Input 和 Presentation authoring；脚本、任意文件系统、客户端自配置与 Autoload 管理始终禁用。配置策略和精确 Tool 集合由 `Tools/migration/manifest/godot-tooling.json` 与受测同步脚本维护，不复制到用户级长期配置。

MCP 能自动承担 Editor 状态、资源/场景读取、运行、日志、截图和已批准的重复编辑，但统一验证、台账晋升和语义 Diff 仍由确定性工具执行；Graph drag/Undo、Dock/SubViewport、Assembly Reload 后视觉状态和最终 VFX/UX 接受仍是人工门禁。

## C# 与 GDScript 边界

Godot 项目代码和 EditorPlugin 可以使用 C#。`godot-ai` 自身的 Editor addon 主要使用 GDScript；当前 custom-tool 扩展设计的 handler 也以 GDScript 加载为主。

推荐的临时接入结构为：

```text
godot-ai custom handler（薄 GDScript）
        ↓ 参数/结果转换
Tactics Godot C# Editor Service
        ↓
Tactics.Core / Godot Resource / Scene
```

GDScript 不负责 Graph 规则、ChangeSet 校验或资产事务。若上游未来直接支持 C# handler，只替换 Adapter，不重写 C# 领域服务。

## `godot-ai` custom-tool 依赖

若 Tactics 操作要通过同一条 `godot-ai` MCP 通道暴露，需要 custom-tool registry 能力（当前对应 PR #820 或其后续正式等价实现）。

这不是整个 Godot 迁移的前置条件：

- `godot-ai` 通用场景/节点/资源/运行能力可以先用正式版本；
- C# CLI、headless 校验和迁移可以独立开发；
- 只有在线 Editor 的 Tactics custom adapter 依赖 custom-tool registry；
- 如果上游长期不合并，保留 backport/fork 或独立 Tactics MCP 作为后备路线。

## 事务与安全合同

需要保留以下项目级语义：

- 规范化 snapshot 和 revision；
- `expectedRevision` 冲突保护；
- dry-run validate；
- typed Graph、节点、边和叶资产操作；
- 跨资产原子 Apply；
- 单一 Undo 事务；
- 失败清理新资产和临时对象；
- Preview 与 Runtime 共用同一 `PresentationExecutionPlan`；
- 不接受任意 SerializedProperty/路径写入作为领域接口。

这些语义属于 Tactics 的 C# 领域服务，而不是 `godot-ai` 原始接口自动提供的能力。

## 测试闸门

在实现 Godot Editor Adapter 前必须验证：

1. Pure .NET Service 的编译、单元测试和 ChangeSet/revision 测试；
2. Godot C# assembly reload 后 Editor Service 的重新绑定；
3. GDScript adapter 与 C# service 的调用和异常转换；
4. Godot AI plugin reload 后 custom catalog 的重注册；
5. 多 worktree/session 不串项目；
6. EditorUndoRedo、ResourceSaver、UID/路径和失败回滚；
7. Preview 的 SubViewport/RenderTexture 清理、停止和 reload；
8. headless 迁移与当前打开 Editor 并发时的隔离策略。

## 明确不做

- 不把所有 Unity MCP 操作名称原样复制到 Godot；
- 不让 GDScript Adapter 成为领域逻辑的第二份实现；
- 不把未实现的 `validate_encounter`、`migrate_unity_content` 或 `audit_content_ids` 当成现有 API；
- 不在 custom-tool PR 尚未稳定前阻塞全部 Godot 迁移；
- 不用通用低层属性写入替代跨资产 ChangeSet 事务。

## 下一阶段边界

技术 Spike、Application/Parity、真实导出管线、Presentation ChangeSet/Revision 和 Poison Spear 程序化占位视觉验收均已完成，real batch 为 `Validated/UnityOwned`。下一阶段进入 Unit 批次并将项目 MCP Profile 切换为 `content-authoring`；custom MCP adapter 继续延后，届时再比较上游 registry、backport/fork 或独立 Tactics MCP。Piloto 第三方视觉仍需单独的购买/EULA 证据与验收，不能由本次 Phase 3 结论外推。
