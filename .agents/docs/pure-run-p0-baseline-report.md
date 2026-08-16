# Pure Run P0 非 C# 基线报告

更新时间：2026-08-01 00:52 +08:00。仓库 revision：`bbbf3555af9d`。`git pull --ff-only origin main` 返回 `Already up to date`；本地 `main` 比 `origin/main` 的 `b80c6fa0b33c` 领先 1 个提交。该本地提交把导出期间已经存在并已加载的 UI 字体生命周期实现与测试正式提交，当前没有脏 `.cs` 或 Unity 资产；P0 文档/CSV 的未提交状态不影响运行时代码，因此后续试玩的 `build_id` 使用 `bbbf3555af9d`。本轮 P0 没有修改任何 `.cs`、Unity 资产、场景、游戏数值或存档。

## 1. 报告状态

| P0 交付 | 状态 | 证据 |
|---|---|---|
| Unity 运行时/资产真值导出 | 已完成 | [当前战斗数值](pure-run-current-combat-values.md) |
| 体验目标与绿黄红阈值 | 已完成 | [原型平衡目标](pure-run-balance-goals.md) |
| Mewgenics 聚合参考带 | 已完成 | [Mewgenics 归一化参考](pure-run-mewgenics-benchmark.md) |
| 当前全池随机试玩协议 | 已完成 | [试玩协议](pure-run-playtest-protocol.md) |
| 40 列 CSV 模板 | 已完成 | `Tools/balance/pure-run-playtest-template.csv` |
| 当前池确定性结构审计 | 已完成 | 本文第 4 节 |
| 三局人工完整 run 数据 | **等待人工输入** | 当前无生产固定 seed UI、无逐战持久导出；CSV 保持空模板，没有伪造数据 |
| P1/P2 调优 | 未开始 | 等三局人工数据与人工分析后再决定 |

## 2. 本轮最重要的运行时真值修正

1. Pure Run 三名角色并非“六维均 5”：法师智力 6、死灵法师魅力 6、亚马逊敏捷 6，其余属性 5。
2. 三名角色 HP 均为 20；最大 MP 分别为 15/18/15，但**首战当前 MP 只有 5/6/5**，不是满 MP。
3. 单位自身回合结束恢复 `Intelligence` MP；存活角色战后恢复 HP `Constitution × 2`、MP `Charisma`，受上限约束。
4. 正式普通怪在初始化公式下基础 HP 为 20；E1/E2 应用 1.3 HP 倍率后为 26；Special 应用 1.8 后为 36。
5. Ranged 的遭遇最低起始 MP 为 15，可支付 Heavy Shot；其他正式怪通常从 Charisma=5 的当前 MP 开始。
6. `ElitePoisonCaster` 当前实际装配的是 Area Blast，没有从资产确认到 Poison 能力。
7. Special 当前只确认单实体、1.8/1.25 倍率、AI Profile 和两步 Pattern；没有确认额外行动、阶段或召唤。

这些修正意味着后续不能再以“开局 15 MP”评估技能经济，也不能仅凭类名把 Special 当作已实现毒素 Boss。

## 3. Mewgenics 参考带

### 已验证聚合

- 解析 `.lvl`：2361 个；
- 识别敌方单位的战斗关：2335 个；
- 其中 4 玩家出生位：2308 个，占 98.84%；
- easy 战敌人数中位数：6；
- authored weight：小型单位常见 0–0.5、标准约 1、强标准约 1.5、大型约 2；
- 三人队换算带约为 3.75–4.5 个标准单位等价物；
- 多区域 Boss 敌方实体数中位数为 1，但其有效行动预算仍未从运行时量化。

### 使用边界

- 不把 Mewgenics 的 6 个身体复制成 6 个 Tactics 标准怪；
- 当前三人对 3–4 个标准怪不明显偏少；
- Boss 比较有效行动数，而不是只比较身体数和 HP；
- 技能比较使用伤害/目标 HP、Mana 消耗/战斗可用 Mana、AOE 期望目标数，不复制绝对数值。

## 4. 当前 N1–N6 全池随机结构审计

审计严格复现 `RoguelikeMapRuntimeState.DeriveSeed` 与 `GetPureRunEncounterRecipeId`，对 run seed 1–10000 逐层枚举。该结果是**静态确定性结构数据，不是试玩结果**。

| 普通节点 | 抽到 4 身体配方 N3–N6 | 平均敌方身体数 |
|---|---:|---:|
| `layer_01_battle` | 66.75% | 3.6675 |
| `layer_02_battle` | 66.96% | 3.6696 |
| `layer_03_battle` | 66.47% | 3.6647 |
| `layer_04_battle` | 66.92% | 3.6692 |

结论：当前普通层没有结构性递增。第一场和第四场抽到四身体配方的概率都约为三分之二，平均身体数也近乎相同。该事实证明当前池会把层级难度与配方噪声混在一起，但尚不能证明 N3–N6 在真实游玩中一定过强。

用于后续人工覆盖的候选 seed 序列：

| seed | L1 | L2 | L3 | 可选 L4 battle | L5 | 可选 L6 battle | L7 |
|---:|---|---|---|---|---|---|---|
| 1 | N1 | N4 | N5 | N6 | E1 | E2 | Special |
| 2 | N4 | N5 | N6 | N1 | E2 | E1 | Special |
| 13 | N3 | N2 | N3 | N2 | E1 | E2 | Special |

前三个强制普通战合计覆盖 N1–N6。但当前正式 UI 不能输入 seed，因此这张表只用于未来可复现测试或人工开局后核对，不能声称已经按这三个 seed 完成试玩。

## 5. 当前可以与不能评价的指标

### 已有证据可评价

- 运行时属性、HP/MP、技能资产和执行语义；
- 怪物能力、资源门槛、Brain/Profile/Pattern；
- 遭遇组成、布局和倍率；
- 当前全池随机结构与身体数分布；
- Mewgenics 的人数、身体数和 authored weight 聚合参考。

### 尚无证据，不能评价

- 前三战胜率与完整 run 通关率；
- 普通、精英、Boss 实际回合数；
- 减员位置；
- 技能施放占比与支配性；
- 恢复是否抹平跨战资源压力；
- meaningful choice 与 clarity 评分；
- Special 实际 Pattern 循环和有效行动预算。

上述缺失项必须保持“未知”，不能由静态资产或自动化 pass/fail 代替。

## 6. 为什么 Agent 不能在当前约束下伪装完成三局人工 baseline

- 正式 New Run 使用 `Guid.NewGuid().GetHashCode()`，没有固定 seed UI/命令；
- 现有严格 `player-input-e2e` 只由自动策略完成前三战，不是人类试玩，也不覆盖完整 Boss run；
- `RunSummary` 没有逐战 HP/MP、KO、技能次数与双方有效行动；
- `TBattleLog` 内存最多 50 条，`EndBattle()` 清空，默认没有持久导出；
- meaningful choice、clarity、dominant strategy 和失败第一判断必须由真实测试者填写。

因此本轮没有向 CSV 写入猜测值，也没有把自动测试冒充体验数据。

## 7. 人工数据采集停点

1. 在 Unity `6000.3.11f1` 中从正式 Home → New Run 流程完成 3 局；
2. 按 [试玩协议](pure-run-playtest-protocol.md) 记录；
3. 将数据填入 `Tools/balance/pure-run-playtest-template.csv`，无法可靠观察的字段留空，不填假 0；
4. Agent 可在每局开局后只读提取 PlayerPrefs，帮助补 `run_seed`、route、node 和 recipe；
5. 三局结束后先检查记录成本和缺失率，再进行人工平衡分析；
6. 在用户明确决定前，不进入 P1/P2，不修改遭遇池或任何战斗数值。

## 8. 验证结果

- Unity EditMode：`EncounterConfigTests + StartingBranchSkillTests`，23/23 通过，0 失败，0 跳过；
- 远端快进后 Unity 重新导出：3 名角色状态、18 个正式技能/42 个已实现等级资产、6 类怪物定义、N1–N6/E1–E2/Special 配方均与本文数值一致；新增的是 VisualAction/Tween/投射物表现字段；
- OKF bundle：`OKF_CHECK_OK concepts=15`；
- OKF 单元测试：14/14 通过；
- CSV：40 个唯一必需列、0 数据行、无重复表头；包含独立 `run_result` 与 `encounter_variant`，可分别计算完整 run 通关率并区分两个 Special 变体；
- 5 份主题 Markdown：本地链接存在，无尾随空白；
- `git diff --check`：通过；
- Unity：无 dirty scene；测试/导出产生的临时场景文件已清理；
- 用户此前的 `UIManager.cs` 与 `HomeSceneInputSmokeTests.cs` 修改现已由本地提交 `bbbf3555af9d` 收录；本任务没有吸收或覆盖其代码，当前工作树没有脏 C#/Unity 资产。

这些门禁只证明配置契约、文档结构和工具完整性，不替代人工可玩性数据。

## 9. 当前停止结论

P0 的静态与运行时真值报告已经足以暴露两个必须先人工验证的假设：

1. 首战起始 Mana 远低于旧认知，资源经济可能比旧文档推断更紧；
2. 第一层约 66.75% 概率抽到四身体配方，当前池缺少层级递增结构。

但两者都还不是调值结论。下一步唯一有效输入是三局真实人工数据，而不是开始 P1/P2。
