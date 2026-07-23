# Gameplay Test Framework

## 文档定位

Gameplay Test Framework 让 Agent 用受控 Markdown 场景描述生成稳定的 Unity 运行计划，并在 PlayMode 中验证真实战斗、技能、地图和 UI 行为。

## 数据流

```text
*.gameplay-test.md / ScenarioSpec
        ↓ TypeScript 校验与编译
*.plan.json
        ↓ Unity GameplayRuntimeRunner
场景构建 → 动作执行 → 断言 → 报告
```

- Markdown/ScenarioSpec 是主要源文件，应进入版本控制。
- `.plan.json` 是编译产物，不应手工维护。
- Unity Runner 可引用真实技能、单位和配置资产，避免只验证测试替身。

## 当前适配器

| 适配器 | 用途 |
|---|---|
| Skill | 施放技能、选择目标、验证伤害/Buff/位移/投射物/多阶段结果 |
| Battle | 创建参战单位、推进回合、使用角色携带消耗品，并验证生命、法力、治疗资格、Buff、朝向、先攻、召唤、技能可用性、多段选择和胜负 |
| Map | 构建并推进 Roguelike 节点，操作装备/消耗品装载与商店购买，并验证可达性、背包、角色状态和商品组成 |
| UI | 驱动点击、悬停、右键和键盘输入，并检查文本、样式类、子节点顺序、布局关系、技能卡可用性和多段目标标记 |
| PlayerInput | 通过 Input System 虚拟 Mouse/Keyboard 驱动生产 UI、地图和战斗输入链；语义定位只负责找目标和只读决策，不直接修改业务状态 |

具体 action、assertion 和参数集合以 `Tools/gameplay-test-spec` 的 schema、编译器和 Unity adapter 代码为准。

## 常用命令

在仓库根目录执行：

```powershell
npm --prefix Tools/gameplay-test-spec test
node Tools/gameplay-test-spec/dist/src/cli.js validate-spec -s <scenario.gameplay-test.md>
node Tools/gameplay-test-spec/dist/src/cli.js compile-spec -s <scenario.gameplay-test.md> -o <scenario.plan.json>
node Tools/gameplay-test-spec/dist/src/cli.js batch-validate -d <spec-directory>
node Tools/gameplay-test-spec/dist/src/cli.js batch-compile -d <spec-directory> -o <output-directory>
```

修改 TypeScript 源码后应先按工具目录的 package scripts 构建，再执行 CLI。Unity 侧通过 PlayMode 测试或项目既有测试入口运行生成计划。

## 编写原则

- 一个场景只证明一个清晰行为，失败信息必须能定位到步骤或断言。
- 优先验证可观察结果，不依赖内部实现细节。
- 需要真实资产语义时显式引用真实资产；纯框架测试才使用最小测试数据。
- 技能阶段、投射物落点、Buff 存在性、单位能否行动等应使用专用断言，不用日志文本代替状态验证。
- 共享战斗原语的维护源位于 `Tests/gameplay-specs/shared/`；其中五个场景分别验证朝向/先攻、状态回合、召唤顺序、禁用原因和有序多段选择。
- 带 `player-input-e2e` 标签的场景只能用 `PlayerInput` 执行动作；Map、Battle、Skill、UI 只允许做只读断言。点击必须经过 Panel picking 或正式 Camera 坐标转换，并由可观察状态推进等待。

## 测试分层

1. 逻辑测试验证规则、计算和事务。
2. 语义 UI 测试验证元素、布局和控制器状态。
3. 真实输入 E2E 使用虚拟鼠标/键盘覆盖生产输入、场景重入和自然战斗。
4. 最终人工测试只检查视觉裁切、动画反馈、文案可读性和操作手感。

`inventory-reentry-player-input` 覆盖同一缓存 Inventory 的重复打开；`battle-player-input-smoke` 覆盖单位、移动、取消、技能与目标输入；`pure-run-player-input-route` 从 Home 开始完成三场自然战斗、升级、Inventory、Store 和多次场景重入。原 `pure-run-real-player-route` 保留为快速 `journey-integration`，不再宣称是真实输入测试。

尚未覆盖的严格事件顺序与动画完成条件记录在 [项目已知缺口](project-known-gaps.md)。
