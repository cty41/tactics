# Gameplay Test Framework

## 文档定位

Gameplay Test Framework 让 Agent 用受控 Markdown 场景描述生成稳定的 Unity 或 Godot 运行计划，并通过生产场景验证真实战斗、技能、地图、UI 与输入行为。

## 数据流

```text
*.gameplay-test.md / ScenarioSpec
        ↓ TypeScript 校验与编译
*.plan.json
        ├─ Unity v1 → GameplayRuntimeRunner / PlayMode
        └─ Godot v2 → GodotGameplayRuntimeRunner / Main.tscn
场景构建 → 生产输入 → 状态等待 → 断言 → 结构化报告
```

- Markdown/ScenarioSpec 是主要源文件，应进入版本控制。
- `.plan.json` 是编译产物，不应手工维护。
- Unity Runner 可引用真实技能、单位和配置资产；Godot Runner 加载正式 `Main.tscn` 并使用 validated checkpoint 与隔离存档，避免只验证测试替身。

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
node Tools/gameplay-test-spec/dist/src/cli.js batch-compile -d Tests/gameplay-specs/godot -o artifacts/gameplay-specs/godot --runtime godot
```

修改 TypeScript 源码后应先按工具目录的 package scripts 构建，再执行 CLI。未指定 runtime 时保持 Unity v1 输出；`--runtime godot` 生成 Godot v2 plan。

## Godot Runtime Runner

- `GodotPlayableRunTestContext` 必须在正式 Main 节点进入 SceneTree 前注入；只允许隔离 `IRunSaveStore`、固定 seed、validated checkpoint、Quit 拦截和初始播放速度。
- 玩家鼠标和键盘动作通过 `Viewport.PushInput` 进入正式 GUI/Input/UnhandledInput 链；每一步等待权威页面、targeting、BattleState 或表现状态变化。
- validated checkpoint 由受控 catalog 构造，plan metadata、唯一 `loadValidatedCheckpoint` setup、canonical V5 hash 与加载结果必须一致，任一不一致 fail-closed。
- watchdog 区分 step timeout、scenario timeout、battle action/round limit 和 no-progress；失败 trace 仍必须记录并清理场景。
- 每个场景使用 `user://qa-runner/<scenario>/<attempt>/`，执行前后记录生产 save 与 `.bak` 的长度、时间戳和 SHA-256。
- 批量报告为 `godot-gameplay-spec-result-v1.json`，包含步骤 trace、checkpoint、生产存档证据和剩余临时节点；统一门禁要求五个首批场景全部通过。

首批 Godot 场景覆盖 Inventory 投影进入 BattleState、有/无召唤物的 Defeated 终局、Mana/Miss 动态数字，以及 Main 重启、Continue 和表现清理。真实 Godot Editor C# Assembly Reload、文案可读性和动画观感仍是人工边界。

## 编写原则

- 一个场景只证明一个清晰行为，失败信息必须能定位到步骤或断言。
- 优先验证可观察结果，不依赖内部实现细节。
- 需要真实资产语义时显式引用真实资产；纯框架测试才使用最小测试数据。
- 技能阶段、投射物落点、Buff 存在性、单位能否行动等应使用专用断言，不用日志文本代替状态验证。
- 共享战斗原语的维护源位于 `Tests/gameplay-specs/shared/`；其中五个场景分别验证朝向/先攻、状态回合、召唤顺序、禁用原因和有序多段选择。
- 带 `player-input-e2e` 标签的场景只能用 `PlayerInput` 执行动作；Map、Battle、Skill、UI 只允许做只读断言。点击必须经过 Panel picking 或正式 Camera 坐标转换，并由可观察状态推进等待。虚拟设备事件排队后必须显式推进测试拥有的 Input System 队列，并由设备状态确认已消费；战场格点击和取消等生产输入使用 InputAction 回调接收同一事件，单纯等待渲染帧不能证明输入已生效。

## 测试分层

1. 逻辑测试验证规则、计算和事务。
2. 语义 UI 测试验证元素、布局和控制器状态。
3. 真实输入 E2E 使用虚拟鼠标/键盘覆盖生产输入、场景重入和自然战斗。
4. 最终人工测试只检查视觉裁切、动画反馈、文案可读性和操作手感。

`inventory-reentry-player-input` 覆盖同一缓存 Inventory 的重复打开；`battle-player-input-smoke` 覆盖单位、移动、取消、技能与目标输入；`pure-run-player-input-route` 从 Home 开始完成三场自然战斗、升级、Inventory、Store 和多次场景重入。原 `pure-run-real-player-route` 保留为快速 `journey-integration`，不再宣称是真实输入测试。

尚未覆盖的严格事件顺序与动画完成条件记录在 [项目已知缺口](project-known-gaps.md)。
