# Mewgenics 配置体系分析

## 目的与证据边界

本文把 Mewgenics 的配置组织方式整理为 Tactics 可复用的设计参考。主要证据来自本机已提取的 GON 配置（外部工作区 `D:\codes\mewgenics_assets\data`）以及配套分析笔记；仓库不复制原始游戏资源。字段含义以配置中的稳定重复模式为高置信证据，未通过运行时或反编译交叉验证的语义会明确降级。

## 总体模型

Mewgenics 并未把技能、单位、AI 和冒险流程揉成一个巨大表，而是使用“模板 + 池 + 运行时解释”的分层结构：

1. **能力模板层**描述一次行动可以如何选择目标、覆盖地块、产生命中和伤害。
2. **角色与成长层**通过职业、能力池、被动池和升级规则装配单位。
3. **AI 层**在现有能力之上描述选择偏好、移动偏好和行动序列。
4. **冒险层**使用事件、商店、奖励池、天气和精英修饰器组合长期流程。

这个结构的关键价值是：战斗规则只定义一次，玩家、敌人和事件通过数据组合复用；AI 不另造一套“AI 专用技能执行器”。

## 能力模板

`ability_templates.gon` 中的能力通常可以拆为以下字段组：

- `meta`：标识、分类、展示与检索元数据。
- `cost`：行动资源或其他施放成本。
- `target`：`target_mode`、`range_mode`、`aoe_mode`、限制条件与击退等几何规则。
- `graphics`：表现引用；它与核心命中/伤害规则分离。
- `damage_instance`：伤害、元素、命中修饰与附加效果。

常见能力类包括 `MoveAbility`、`TeleportAbility`、`SwapperAbility`、近战、远程、范围法术、冲刺、践踏、生成和治疗等。不同类别共享目标与效果词汇，同时由各自实现补充专属字段。

### 继承与变体

`variant_of` 用于从现有模板派生变体，只覆写差异字段。这比复制完整模板更适合维护同源技能的升级版、敌方版或特殊事件版。应用到 Tactics 时，应把“继承后的有效值”与“当前资产显式覆写值”区分开，否则 Agent 容易把缺省字段误判为缺失规则。

### 被动与触发

被动通常由触发类、触发时机、过滤条件和效果组成。其设计重点不是“每个被动一种硬编码生命周期函数”，而是让触发器决定何时把效果送入统一结算链。Tactics 的 SkillGraph 若吸收此思路，应优先复用公共事件上下文与条件节点。

## 角色、职业与成长

职业配置中反复出现：

- `attack_pool`、`ability_pool`、`passive_pool`
- `starter_abilities`
- `complicated_abilities`、`ability_groups`
- `stat_mods`、`levelup_stats`

这些字段分别表达候选内容、初始装配、互斥/分组关系和数值成长。配置池本身并不等于最终获得结果；运行时仍需处理等级门槛、已拥有项、候选数量、权重与重抽规则。

对 Tactics 的直接启示是把三件事分开：

- **内容定义**：技能/被动本身是什么。
- **可获取集合**：当前职业、等级或事件允许抽到什么。
- **选择算法**：如何从集合生成候选并写回角色状态。

## 敌人 AI 配置

### GenericBrain

`GenericBrain` 使用 `decision_weights` 和 `move_weights` 对候选行动与位置打分。它适合“每回合根据局势重新评估”的敌人，例如靠近、拉开距离、寻找范围命中或优先特定目标。

可以把其决策拆为三层：

1. 能力几何产生合法候选。
2. 移动评分比较可到达位置。
3. brain 的决策模式和权重选择行动。

Tactics 现有的候选生成、规则过滤、评分和执行器划分与这一思想相容。借鉴重点应是配置表达力与可观察性，而不是照搬字段名。

### PatternBrain

`PatternBrain` 用模式序列表达更可读、更可预测的敌人节奏。已识别的容器包括：

- `mainturn_pattern`
- `bonusturn_pattern`
- `stacked_bonusturn_pattern`
- `dispersed_bonusturn_pattern`
- `round_start_bonusturn_pattern`
- `round_end_bonusturn_pattern`

已识别的指令包括 `do_random`、`do_priority`、`do_priority_alternating`、`do_all`、`do_all_shuffle`、`do_one`、`do_best_multiple` 和 `do_nothing`。`fallback_advances_pattern` 与 `stun_advances_pattern` 表明模式推进和行动成功并非天然绑定，而是可配置策略。

这类 AI 的优势是玩家能学习敌人节奏，设计师也能直接审查序列。尚未完全确认的是 stacked/dispersed bonus turn 在运行时的精确调度顺序，因此不能把字段名称直接当作完整时序规范。

### virtual_abilities

`virtual_abilities` 更像 AI 对已有能力的包装视图：同一执行能力可用另一组移动/决策偏好参与候选，而不是复制一个新的战斗技能。这个模式适合 Tactics 中“同一技能因敌人意图不同而有不同站位价值”的场景，但执行阶段仍必须落到唯一权威能力定义。

### 形态切换

`FormChanger` 表达形态状态集合；触发条件由独立被动承担，例如拥有状态、正在预备能力、当前天气元素或武僧姿态匹配。状态容器和触发机制解耦，使同一组形态可以被不同条件复用。

## 代表性敌人机制

- `ChargeyMaggot`：以直线冲击制造走位压力，适合验证移动几何与路径风险。
- `SwappyMaggot`：交换位置，改变阵型而不只造成伤害。
- `SecurityBot`：规则化行动序列，适合 PatternBrain 式可预判节奏。
- `BombFly` / `BomberRat`：预告区域与延迟危险，迫使玩家处理未来地块价值。
- `FlySwarm`：通过数量与局部聚集改变范围技能价值。
- `Rager`：状态或血量驱动的行为变化。
- `DrMangler`：多阶段或多能力组合，适合验证复杂优先级。

这些例子共同说明：有辨识度的敌人不依赖更高数值，而依赖移动、地块、延迟和状态对战场几何的改变。

## 冒险与奖励配置

冒险数据采用事件 DSL、商店、物品池、天气和精英增益组合。战斗外系统主要做两件事：构建候选集合，以及把选择转换为后续战斗的状态。对 Tactics 的 pure-run 设计而言，服务竞争、事件代价和短流程奖励应继续使用数据组合，避免每个节点类型都发展成独立流程代码。

## 对 Tactics 的可执行结论

1. 保持能力定义、AI 偏好和执行逻辑分离；AI 只选择，不复制结算规则。
2. 为敌人同时支持“局势评分型”和“模式序列型”决策，两者共享候选与执行接口。
3. 将模式推进条件显式建模，至少区分成功、fallback、受控跳过和额外回合。
4. 让形态状态与触发器解耦，避免把天气、状态和预备动作写进同一巨型组件。
5. 对数据继承提供有效值查看和来源追踪，保证 Agent 能判断字段来自模板还是覆写。
6. 将代表性敌人作为几何压力测试样本，而不是只测伤害输出。

## 置信度与待验证项

- **高**：能力字段分组、`variant_of`、职业池、GenericBrain/PatternBrain 双路线、形态容器与触发器分离。
- **中**：`virtual_abilities` 是 AI 视图而非独立执行能力；需继续结合运行时调用链确认边界。
- **低**：stacked/dispersed bonus turn 的精确插入时机和模式游标推进顺序。

后续验证应使用 `Tools/reverse-engineering` 的 manifest 固定输入版本，以 Ghidra 交叉引用和实际战斗日志补齐中低置信结论。
