# 临时想法

## 使用约定

- 本文是未经验证的灵感收集箱，不是当前设计、已确认缺口或活跃开发计划。
- 记录在这里不代表一定实现，也不要求立即补充代码证据。
- 想法经过讨论并形成稳定设计后，迁移到对应权威设计文档；确认是现有实现缺口时，迁移到 [项目已知缺口](project-known-gaps.md)；决定执行后再建立 `.agents/plans/` 计划。
- 迁移完成的条目从本文删除，历史由 Git 保留。

## 参考方向

- 核心战斗：《喵喵的结合》、《幻世录》。
- 职业和故事背景：《暗黑破坏神》。
- 美术风格：《Kingdom Rush》？
- 定期村庄防守：《最后的咒语》。
- 城镇建造：《球比伦战记》、《英雄无敌 3》。
- 外出肉鸽：《喵喵的结合》、《杀戮尖塔》、FTL。

## 表现想法

- 开宝箱时使用类似《暗黑破坏神》的金币和装备弹出效果。
- 事件属性检定未来可增加类似《博德之门 3》的投骰演出，展示检定角色、最终成功率、骰子结果以及成功或失败结算。

## 关卡与地图想法

- 加入防御式战棋关卡。
- 类似《英雄无敌 3》中敌方英雄回来追击玩家的逃生体验。
- 墓园地图中的棺材可能开出装备，也可能出现敌人僵尸。
- 隐藏房间是否值得加入？参考《暗黑破坏神 2》和《博德之门 3》。
- 开场选择 2 名角色；每通过一张地图，可在新角色、珍贵装备或其他等价奖励之间选择。
- 添加相机平移操作,从而支持大于屏幕的战斗地图

## Boss 想法

- 参考希斯·莱杰版小丑的 Boss。

## 成长与技能想法

- 角色升级采用类似《英雄无敌》的模式，每个技能提供 3 档升级。
- 召唤骷髅的高级能力可以让玩家手动控制骷髅。
- 亚马逊方向：电标马、变身女武神、远程投掷长矛、瞬移到长矛落点。

## Godot 迁移开源机会（2026-08-07，尚未决定）

### 状态与边界

- 本节只记录 Unity → Godot 迁移中可能具有公共价值的技术缺口，不代表决定开源、确定仓库结构或承诺维护周期。
- 开源工作不能成为迁移前置条件；优先完成 Tactics 的真实迁移验证，再从至少两个实际领域中提炼稳定公共接口。
- Tactics 的具体技能、表现、角色、关卡、资产映射和业务规则保持项目私有；候选开源范围只包含通用基础设施、适配接口、测试工具与最小样例。

### 已识别的候选缺口

1. **Godot C# 内容图作者框架**
   - Godot [`GraphEdit`](https://docs.godotengine.org/en/stable/classes/class_graphedit.html) 提供图形交互控件，但连接、删除、复制、持久化和领域约束仍由使用者实现。
   - Orchestrator、LimboAI、Beehave 和各类对话插件已经覆盖可视化脚本、行为树或对话等特定领域，当前未发现成熟的、面向自定义 C# `Resource` 的通用生产级作者框架。
   - 可复用候选包括稳定 Node/Edge/Port ID、端口基数、NodeDescriptor 注册、Graph 根与叶 Resource 下钻、Undo/Redo、SchemaVersion、类型迁移、分级校验、规范保存、SubViewport 预览扩展和 assembly reload 恢复。

2. **事务化 Resource 修改与语义 Diff**
   - Godot 已提供 `ResourceSaver`、UID 和 `EditorUndoRedoManager`，但没有直接提供可序列化 typed ChangeSet、`expectedRevision`、dry-run、多 Resource 预检/失败恢复、外部变更 fence 和领域语义 Diff 的组合层。
   - 候选公共内核应让编辑器 UI、CLI 和 Agent/MCP 适配器共享同一 mutation kernel，避免任意属性路径写入和界面逻辑、自动化逻辑各自实现资产修改。

3. **Unity 自定义内容到最终 Godot Resource 的迁移 SDK**
   - [Unidot Importer](https://github.com/V-Sekai/unidot_importer) 已覆盖 `.unitypackage`、GUID 数据库及部分场景、Prefab、模型、材质和动画转换，但明确不转换 MonoBehaviour/C# 逻辑和 UI；[unity_to_godot_converter](https://github.com/Zylann/unity_to_godot_converter) 仍属于实验性简单场景转换器。
   - 我们潜在的公共价值不是完整项目一键转换，而是为 ScriptableObject、项目配置和领域图提供可注册的导出/导入适配器：Unity GUID/LocalFileId 解析、临时 DTO、`GUID → ContentId → Godot Resource/uid://` 台账、dry-run、Diff、幂等重复导入、引用诊断和所有权切换。
   - 中立 DTO 只作为迁移过程中的临时传输数据；公共框架最终写入使用者定义的 Godot `Resource`/`PackedScene`，不引入需要长期维护的中间资产格式。

4. **C# EditorPlugin reload 与 SubViewport 生命周期测试工具**
   - Godot 社区仍在讨论 C# tool script 的构造器、属性 setter、副作用、信号和反序列化恢复顺序；参考 [godot-proposals #9001](https://github.com/godotengine/godot-proposals/issues/9001)。
   - 可复用候选包括 reload 前后状态快照、Dock/信号/Viewport 清理模板、重复 UI 与泄漏检测、连续重编译压力测试和 Godot 版本兼容矩阵。
   - 插件只能提供约束、workaround 和回归测试；确认属于引擎内部的问题时，优先向 Godot 上游提交文档、测试或源码修复。

5. **后续候选**
   - 表现编排图：语义 Marker、并行轨道、取消、非阻塞视觉尾巴，以及运行时和预览共用执行计划；应先作为真实领域验证通用 Graph Kernel，而不是立即冻结成公共 API。
   - ContentId/UID Catalog 与不可变 ContentSnapshot 编译器：适合作为内容作者内核的模块，不优先独立成插件。
   - Pure .NET Tactics Core：可能形成独立战棋库，但属于更大的领域框架，不视为 Godot 生态缺少的迁移工具。

### 当前不建议重复建设

- 不优先制作 Odin Lite；Godot 4.7 已有 C# 属性驱动的 [Zeus Inspector](https://store.godotengine.org/asset/notclerick/zeus-inspector/) 等 Inspector 扩展，项目只补具体领域编辑器缺口。
- 不另建测试框架；优先使用并向 [GdUnit4/GdUnit4Net](https://github.com/godot-gdunit-labs/gdUnit4) 贡献兼容性修复。
- 不另建通用 MCP Server；可考虑让 typed ChangeSet 和领域工具作为现有 Agent/MCP 工具的安全资产修改后端。
- 不重复实现 PCK/常规资源加载、输入、音频、Tween、Steam 基础接入或完整 Unity 项目一键转换。

### 暂定提炼方式与决策闸门

- 候选第一项目：`Godot.ContentAuthoring`，内部可分为 Core、Godot adapter、Graph 和 Testing；名称、许可证、仓库形态均未决定。
- 候选第二项目：独立的 `UnityGodot.ContentBridge`，避免长期 Godot 工具依赖 Unity Editor，并允许迁移完成后整体移除。
- 只有 Presentation Graph 与 Skill Graph 证明复用边界、C# EditorPlugin 技术 Spike 通过既定 reload 压力验证、项目专用映射能够彻底隔离后，才评估首个公开版本。
- 正式决定前仍需评估维护成本、Godot 4.x 兼容策略、包分发方式、许可证、公共 API 稳定承诺以及是否先以上游贡献替代独立项目。
