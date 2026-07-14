# Mewgenics 运行时反编译分析

## 分析范围

本文记录从 `Mewgenics.exe` 的字符串、符号邻近关系、交叉引用和小范围反编译中得到的运行时结构结论。它不是完整源码还原，也不把 Ghidra 自动生成的 `FUN_*` 名称当作稳定 API。

分析输入由 `Tools/reverse-engineering/manifests/mewgenics-analysis.json` 固定：SHA-256 为 `c3a41e436a93fa58cd386ec46dad5c2a6f21a583d33c3a57a15a2604c726439e`，大小 21,981,184 bytes。地址和 `FUN_*` 名称只对该输入版本有效。

## 可复现工作区

Ghidra 12.1.2 安装于仓库外，project 位于 `D:\Ghi`。`mew.rep` 约 462 MiB，主要由 Ghidra 数据库页构成；它保存程序字节、分析索引、符号、类型、引用和用户修改历史，因此不进入 Git。

仓库保存：

- 14 个目标函数的统一清单；
- headless 只读导出脚本；
- 输入与工具链指纹；
- 不含完整反编译代码和原始字符串的摘要索引。

任何结论复核前都应先运行 `verify-environment.ps1`。若可执行文件哈希变化，必须重新定位函数，不能沿用旧地址。

## 源码身份与模块边界

可执行文件中存在 C++ 命名空间和源文件路径线索，包括 `glaiel::`、`Ability.cpp`、`CatData.cpp`、`Character.cpp`、`SpawnAbility.cpp` 与 `SpawnDatabase.cpp`。这些字符串说明配置并非由通用脚本 VM 独立承担，而是被多个 C++ 领域模块读取和解释。

已定位的语义锚点包括：

- `glaiel::Ability::refresh_data`
- Ability trigger、`PayCost`、`AOETileIsValid`、`TargetIsValid`
- `SpawnDatabase::BuildItemPool`、`CreateAbility`、`init`、`hot_reload_events`
- `LevelUpClassPassives`、`LevelUpClassActives`、`LevelUpAtCombatEnd`
- `LEVELUP_CHOICE_*`
- `recompute_item_effects`

这些名称共同支持一条运行时主链：配置载入/热重载 → 数据库构建对象或池 → 能力合法性与成本检查 → 结算 → 角色成长或物品效果重算。

## 能力运行时

`refresh_data` 的存在表明能力对象保留运行时身份，同时允许从配置刷新数据。`TargetIsValid` 与 `AOETileIsValid` 分开，说明“中心目标是否可选”和“范围内每个地块是否受影响”是两个验证层。`PayCost` 又与触发逻辑分离，意味着合法性、支付和效果执行可以拥有不同失败边界。

对 Tactics 的意义：

1. SkillGraph 的目标选择、AOE 地块过滤和效果执行应继续保持独立节点/阶段。
2. AI 预估必须调用与真实执行相同的合法性语义，不能维护近似副本。
3. 热重载或资产刷新时，需要区分定义变化与战斗中实例状态，避免重置冷却、层数或临时目标。

## SpawnDatabase 与内容构建

`SpawnDatabase` 相关符号覆盖 item pool 构建、ability 创建、初始化和事件热重载。合理解释是：数据库负责把配置记录解析为运行时对象/池，而不是承担每个对象的战斗行为。

`BuildItemPool` 与 `CreateAbility` 的并列也印证了配置分析中的分层：池决定候选范围，对象工厂决定具体定义；抽取算法和最终写入角色状态还在更上层。

## 成长流程

`LevelUpClassPassives`、`LevelUpClassActives`、`LevelUpAtCombatEnd` 与 `LEVELUP_CHOICE_*` 字符串表明升级流程至少区分主动、被动、战后触发和选择类别。它们支持“配置池 + 运行时生成候选”的模型，但尚不足以证明具体权重、去重和等级门槛算法。

`recompute_item_effects` 表明部分派生状态通过集中重算维护。Tactics 若实现类似装备/被动叠加，应保留权威重算入口，避免多个 UI 或事件处理器增量修改同一派生值。

## PatternBrain 结构恢复

反编译与字符串证据确认 PatternBrain 读取多类 pattern 容器：主回合、普通额外回合、stacked/dispersed 额外回合以及回合开始/结束额外回合。指令集合覆盖随机、优先级、交替优先级、全部执行、乱序全部执行、单项、多个最佳项和空操作。

两个推进标志尤其重要：

- `fallback_advances_pattern`
- `stun_advances_pattern`

它们说明模式游标推进是显式政策：没有合法动作或因控制无法行动时，设计者可以选择保持当前步骤或继续推进。这个细节直接影响敌人是否会在玩家控制后重复高威胁预备动作。

当前仍无法仅凭已导出函数可靠确定 stacked 与 dispersed bonus turn 在调度队列中的精确位置。文档和实现提案必须把这一点标为待实验验证，而非既定事实。

## FormChanger 与触发被动

证据更支持 `FormChanger` 是形态状态容器，而具体何时变化由独立触发被动驱动。已见语义名称包括：

- `FormChangeWhileHasStatus`
- `FormChangeWhilePrimingAbility`
- `FormChangeDuringWeatherElement`
- `FormChangerMatchMonkStances`

因此，运行时很可能先由事件/状态系统评估触发器，再要求形态容器切换到目标状态。对 Tactics 应避免让 Form 组件主动轮询所有天气、状态和技能预备条件。

## virtual_abilities 的运行时假设

当前中等置信结论是：`virtual_abilities` 为 AI 提供已有能力的替代评估视图，允许同一真实能力带着不同移动或决策配置进入候选列表。最终执行仍应解析回真实 Ability。

验证这个结论需要继续追踪：

1. virtual entry 的构造点；
2. 候选评分时保存的 identity；
3. 执行前是否发生 unwrap/lookup；
4. 成本、冷却和目标合法性读取真实对象还是包装数据。

在完成这条调用链前，Tactics 只能借鉴“评估适配器”概念，不能假设其所有覆盖字段都安全。

## 证据等级

- **直接证据**：manifest 中的输入哈希；Ghidra project/导出存在；字符串和符号名称；14 个函数的调用者、被调用者和字符串引用数量。
- **交叉支持**：配置字段与运行时语义锚点一致，例如 ability pool、目标合法性、PatternBrain 指令和形态触发器。
- **推断**：数据库/工厂的具体所有权、virtual ability unwrap 方式、bonus turn 精确调度。

自动生成的函数签名常含 `undefined8`、`longlong` 等占位类型，不应据此声称已经恢复真实 C++ 类型。`FUN_*` 地址必须与 manifest 一起引用。

## 下一轮分析建议

1. 以 `TargetIsValid`、`AOETileIsValid` 和 `PayCost` 为根，恢复 Ability 的调用顺序与失败路径。
2. 为 PatternBrain 的 pattern cursor 找到读写点，并分别观察 stun、fallback、bonus turn。
3. 追踪 virtual ability 从配置解析到执行器的完整 identity 流。
4. 对 `FormChange*` 触发器记录订阅事件和状态切换入口。
5. 每个新结论都同步记录输入哈希、函数地址、证据类型和置信度；原始导出继续留在 `D:\Ghi\export`。

## 项目应用边界

本文是外部参考，不是 Tactics 当前实现说明。将结论转化为代码前，必须检查现有 Monster AI、Battle System 与 SkillGraph 的实际类型、资产和测试，并用项目术语重新建模。外部游戏的字段名不构成兼容性要求。
