---
name: pure-run-artwork-pipeline
description: "Use when generating, editing, chroma-keying, calibrating, reviewing, organizing, or preparing commits for Pure Run character artwork; enforce the project sprite contract and keep runtime assets untouched."
---

# Pure Run Artwork Pipeline

## Quick Reference

| 操作 | 入口 |
| --- | --- |
| 读取尺寸契约 | `references/sprite-size-contract.md` |
| 规划 ImageGen 单图迭代 | `references/imagegen-iteration.md` |
| 生产 Melee / Thrown / Cast / Hit 单帧动作 | `references/single-frame-action-poses.md` |
| 规划四方向静态图 | `references/imagegen-iteration.md` 的“方向变体 / 双原生视图” |
| 核对运行时四向映射 | `references/imagegen-iteration.md` 的“运行时双原生图接入” |
| 查看正反案例与正式母图 | `references/review-casebook.md` 与 `examples/cases.json` |
| 设计与 Review 死亡状态 | `references/death-state-sprites.md` |
| 参考驱动死亡整形 | 参考角度和压扁度只作为生成/验收指标；身体、耳朵和相连四爪必须作为完整轮廓生成，禁止拆层几何整形 |
| 设计与 Review 投射物 | `references/projectile-sprites.md` |
| 锁定方向图核心体量 | 以同角色已确认的 `down-right` 为唯一体量锚点；核心蒙版只用于测量、三截面比较和 QA |
| 校对核心体量 | 地面单位对齐核心胶囊主体；飞行单位对齐球核并以球核中心作为水平锚点 |
| 校对 Tile 落点 | 脚底锚点或飞行单位虚拟落点必须精确位于 `64×32` Tile 几何中心 |
| 去幕与 alpha 校验 | `references/chroma-key-validation.md` |
| Review、归档和提交 | `references/artwork-review-and-git.md` |
| 只读批量检查 | `python scripts/validate_sprite_assets.py --root Tools/artworks --strict --review-examples`；状态机已晋升输出的几何权威委托给绑定 report/receipt |
| 创建/重试/摄取任务 | `python scripts/artwork_pipeline.py --root <repo> create-job|retry|ingest ...` |
| 连续姿态生产 | `create-series` → 每版 `record-feedback` → `select-attempt` → `advance-series` |
| 限定几何例外 | `approve-exception`；首版只允许 `core_size_out_of_tolerance`，并绑定完整证据哈希 |
| 已人工确认成图的诚实收编 | `render-size-comparison` → `adopt-reviewed-sprite` → `approve` → `promote`；不得伪造 invocation |
| 装备风格防漂移 | contract 绑定 `--style-profile` 与可见高度范围，再用 `prepare-equipment-candidate` 原子完成真实边界裁切、有限色阶、基线定位、预览与硬门禁报告 |
| 公开许可变更 | `relicense-public-artifact`；仅允许 `cty41` 将哈希匹配的 approved `project-owned` 成图显式发布为 `CC-BY-4.0`，并生成不可变 receipt |
| 登记离线辅助图 | `register-supporting-artifact`；PNG/SVG 导引、蒙版和参考源按当前哈希登记为 `supporting-derived`，不得作为运行时 Sprite |
| 状态机严格门禁 | `python scripts/artwork_pipeline.py --root <repo> check --strict` |
| 运行时视觉 QA | 使用 Godot 后台测试、生产输入注入或已有截图；不控制真实 Editor 窗口 |

## When to use

- 生成或编辑 Pure Run 的角色、怪物、武器或职业变体 Sprite。
- 对绿幕/洋红幕图去幕、透明化、缩放、定位或生成 128 预览。
- Review 胶囊体比例、等距朝向、脚底基线、Tile 占用和遮挡层级。
- 整理 `concepts`、`calibrated`、`candidates`、`rejected`、`tmp`，或准备素材提交。

## Workflow

0. **先建立合同与 job。** 读取器兼容且不重写 schema v1/v2；普通新合同沿用 v2，组件化资产写 schema v3。`action_pose`、`death_pose`、遮挡任务或使用姿态参考的 job 必须先 `create-composition`，再 `render-pose-guide`；导引是 `supporting-derived`，不能晋升为 Sprite。`create-contract` 固定核心锚点、构图规范、容差、发布路径与授权，`create-job` 固定每张输入图的职责与 SHA-256。随后用 `compile-prompt` 重复冻结不变量并只合并待修项。ImageGen 不属于 CLI；每次外部调用必须先 `begin-generation`，成功图使用匹配的 `invocation-id` 摄取，交付失败则用 `record-generation-failure` 留证且不计 raw 版本。

   多姿态任务先用 `create-series` 固定顺序；`maxUniqueOutputs: null` 表示无限迭代，正整数表示显式预算。每个不同 raw SHA 计一次输出；相同 SHA 的重新摄取、去幕、蒙版或验证不增加计数。每个已摄取 attempt 都必须用 `record-feedback` 记录优点、缺陷、技术结论、选择及下一版 prompt delta；`retry --feedback-id` 和 `advance-series` 均不得绕过该记录。只有有限预算达到上限后才能进入 `exhausted`；只有耗尽的首个 `idle-dr` 可显式选择 provisional anchor，且其下游 job 自动标记 `conceptOnly`，禁止批准或晋升。已创建 series 的预算变化必须通过 `set-series-output-limit` 写入审核人、原因和时间，不得手改注册表。

1. **先查案例，再锁定母图。** 阅读 `references/review-casebook.md` 中与任务相关的正反案例，并从 `examples/cases.json` 的 `approved_assets` 选择唯一正式母图。记录必须保持的身体、脚位、盾牌、武器和构图；把其他图片标为犬种、武器、姿态或色彩参考。不要让参考图替换母图的比例，禁止从 `rejected`、`superseded`、案例快照或 `tmp` 开始编辑。
2. **一次生成一个变体。** ImageGen 只处理一个角色、一个局部变体或一个投射物。动作姿态先读取 `references/single-frame-action-poses.md`，明确角色母图、已选 DR 动作图与跨角色姿态参考的独立责任。明确“保持不变”区域；新武器、耳朵、翅膀和法术必须在屏幕空间中有清晰的前后层级，并完整留在画布内。普通任务的单帧候选展示后停止；显式 series 任务按合同中的审批节奏推进，但任何 `cty41` approval 都必须来自用户确认并落成 receipt，不能由自动推进替代。
3. **去幕。** 选择纯 `#00ff00` 或 `#ff00ff` 背景，移除背景、软化 matte 边缘并检查绿色/洋红残留。保留真实透明 alpha，不把带色幕截图当母版。
4. **尺寸校准。** 方向图必须以同角色已确认的 `down-right` 为唯一核心体量锚点，并制作只包含中央胶囊体或球核的纯核心主体蒙版；耳朵、口鼻、眼睛、手掌、脚掌、武器、盾牌、法杖、翅膀和特效全部排除。蒙版只用于测量和 QA，禁止粘贴或参与成品合成。比较主体上缘、下缘、中心、最大宽度以及上中下三个截面的宽度分布；中段近似平行，下段只能持平或内收。飞行单位改用球核体量，把球核中心固定在身体水平锚点 `x=128`，并在垂直方向对齐基准胶囊体上部圆帽中心。完整 alpha 包围盒只用于画布安全、裁切和技术校验：标准母版 `256×256 RGBA`、脚底或虚拟基线 `y=236`。禁止单轴拉伸；外部轮廓超出标准时记录为例外或候选。
5. **缩小和逐角色 Review。** 生成 `128×128` Mitchell 等比预览，确认脚底基线 `y=118`。再使用真实错列等距 Tile 排布，将预览的脚底锚点 `(64,118)` 精确映射到目标 `64×32` Tile 的几何中心；飞行单位使用同一虚拟落点，不能用可见身体下沿替代。带身份蒙版的动作/身体层还必须输出 Idle 锚点与候选同屏的 `anchorTileCompare`，两者使用相同 Tile 与脚底中心。死亡图先按 `references/death-state-sprites.md` 分类拓扑，并使用完整尸体 AABB 中心对齐 Tile。检查脚掌接触或悬浮间距、角色占用、脸部识别和装备分离。每次只校正一个角色，必须展示基准与当前角色并排并等待人工确认；确认后才进入下一个角色或生成其方向变体。运行时 Game View Review 必须遵循[前台交互与焦点保护规则](../../rules/foreground-interaction.md)：使用 MCP 截图、自动测试或虚拟输入；如果代表状态只能靠点击真实窗口获得，停止并标记 `manual_visual_qa_pending`。
6. **执行状态转换。** `prepare` 只做确定性去幕、RGBA 规范化和透明 RGB 清零；对 ImageGen 的近似纯色色幕可显式传入 `--chroma-tolerance`。`attach-mask` 绑定源图同坐标语义蒙版；v2 高风险任务还必须 `attach-annotations` 绑定眼区、武器出口、剑尖和宝石区域。高分辨率输出再用 `calibrate-core` 统一等比缩放，并以语义脚爪校准地面基线；图像与蒙版共用同一变换。`validate` 确定性检查倾角、出口窗口、禁入区、宝石面积、装备状态与旧几何门禁。技术失败只能新建 retry；用户已经选中的同一 raw 可用 `--technical-remediation` 创建技术子 attempt，不增加唯一 ImageGen 输出数。
7. **人工批准并晋升。** `approve` 前必须已经生成且哈希仍匹配 overlay、128 预览和 Tile Review；只有 `approve --reviewer cty41 --reason ... --decided-at ...` 落成绑定候选与蒙版哈希的 receipt 才算批准。若报告唯一失败项为 `core_size_out_of_tolerance`，且用户明确接受该候选，可使用 `approve-exception --issue core_size_out_of_tolerance --reviewer cty41 ...` 生成限定例外 receipt；它保留 `report.passed=false`，并额外绑定报告、合同、候选、蒙版和全部 Review 哈希及实际/锚点核心尺寸。任何其他失败项、未知 reviewer、哈希变化或全局容差放宽都不允许。`promote` 只接受有效 `approved` attempt，并同步正式输出、正式母图清单与公开 provenance。`legacy-unresolved` 不得作为母图；旧正式图的核心蒙版也必须先有同一候选/蒙版哈希组合的批准 receipt，才可写入新合同的几何锚点。

   带 `styleSpec` 的装备合同只能收编由 `prepare-equipment-candidate` 产生、且候选/预览哈希完全匹配的通过报告。报告硬性约束真实 Alpha 可见高度、基线、内部色阶和连续平滑渐变；角色、武器与已批准装备锚点的同屏风格对照仍由用户人工确认，自动指标不得代替审美批准。

   若某张精确成图已经在对话中逐轮产生并由用户明确确认，但生成前没有 invocation，只能走 `reviewed_import`：先用 `render-size-comparison` 冻结四栏尺寸证据，再由 `adopt-reviewed-sprite --reviewer cty41` 绑定原始源图、256 候选、128 预览、对比图和确认时间。该入口不补写或伪造 ImageGen receipt，只接受居中、透明四角、无精确色幕残留的完整 Sprite；批准后 `promote` 原样复制候选与预览字节。它是历史缺口的受限收编入口，不是日常生成捷径。

### 受控组件与确定性合成（schema v3）

- 胶囊角色 `component` 仅限 `body`、`equipment`、`paw_overlay`、`foot_overlay`；每个 Assembly 必须且只能包含远/近脚、远/近手、身体、装备六个 role 各一次。`layers` 数组就是姿态专属的由后到前深度计划，不得把 near/far 机械等同于固定前/后层。
- 混合来源使用 `generated`、`derived`、`pre_v3_import`：独立生成组件必须有 `cty41` approval；确定性派生组件以获批源、派生 receipt 和通过的验证报告代替重复人工审批；旧组件迁入必须绑定迁入 receipt 和通过的验证报告。
- `body` / `equipment` 可独立生成，也可从完整正式姿势的语义蒙版确定性派生；手爪与脚爪优先确定性派生，派生不可行时才回退到独立生成。
- 旧的明确自有输入只能用 `migrate-component` 进入 `pre_v3_import`；必须保留原始哈希、处理参数和用户确认，禁止伪造 ImageGen invocation。
- 手爪与脚爪优先使用 `derive-component` 从获批身体及 `near_hand`、`far_hand`、`near_foot`、`far_foot` 标签确定性派生；只有源图缺失、粘连或结构错误时才建立独立 `generated` 局部任务。逐层 Review 若暴露眼睛、身体或其他错误像素，必须修正源语义蒙版并重新派生，不能在 overlay 上手工擦除。
- `create-assembly` / `render-assembly` 只允许等比整数百分比缩放、整数平移、水平翻转和 Alpha 合成。输入顺序、组件/蒙版 SHA 与变换共同决定 assembly ID；任何变化都会使旧 Review 与 approval 失效。
- 完整 `assembled_sprite` 必须重新验证手脚多像素接触、核心连续性、基线与语义纯度，并生成同时含单层和累积合成的 `assemblyLayerReview`；组件证据不能代替完整 Sprite approval。只有完整 Sprite 获得单独的 `cty41` receipt 后才能晋升。
- 组件化制作仍是离线美术管线；不得据此实现换装系统、运行时动态组装或程序 FX，除非任务另行明确授权。
8. **归档并验证。** 运行 `artwork_pipeline.py check --strict` 与 `validate_sprite_assets.py --review-examples`；需要查看未确认候选时加 `--include-candidates`。再运行 OKF report/sync、bundle 校验和单元测试；按路径暂存，排除临时文件和任何运行时文件。展示精确暂存清单，等待用户确认后再提交。

## Guardrails

- 默认不修改 Godot Resource、AI、遭遇配置或运行时代码；只有用户明确授权“运行时美术接入”时，才可将已确认原生图接入 canonical Godot Resource，并且不得改变玩法朝向语义。
- 运行时接入授权、视觉 QA、截图要求或“补齐代表单位”都不授权 Computer Use、`activate_window` 或真实鼠标键盘输入。后台验证不足时记录 `manual_visual_qa_pending`，不得抢占用户焦点。
- 不覆盖已确认版本；新设计使用新的版本号，失败候选移动到 `rejected`，而不是删除历史证据。
- 不手改 registry 状态、report 或 receipt；所有转换必须经 `artwork_pipeline.py`。同一 attempt 不得摄取不同字节，`technical_failed` 不得走普通批准或直接晋升；只有下述限定例外命令可以原子转为 `approved`。
- 不手改公开素材许可证。`relicense-public-artifact` 只接受 `cty41`、当前文件哈希与 provenance 完全匹配、状态为 approved 的 `project-owned → CC-BY-4.0` 决策；其他权利人、许可证方向或未批准素材必须拒绝。
- `approve-exception` 不是通用跳过门禁：首版只豁免报告中唯一的 `core_size_out_of_tolerance`，且只能由 `cty41` 签发。基线、Alpha、透明 RGB、色幕、蒙版、缺爪、接触、错误侧、梨形、遮挡、裁切、路径和哈希问题一律不可豁免。
- Series 中满意稿和失败稿同样必须留下不可变 feedback；无限 series 可在逐版反馈与人工 Review 门禁下继续产生不同输出，有限 series 不得越过其显式预算；不得把失败稿作为母图。provisional anchor 只允许维持有限预算耗尽后的生产连续性，不能成为正式资产血缘。
- Feedback v2 必须区分 `authorType: agent|human`，使用结构化缺陷分类和 `selected|backup|retry|technical_failed|exhausted` disposition；Agent 或视觉模型的建议不得签发 `cty41` approval。视觉语义辅助只能用 `record-advisory-review` 留下非绑定风险，不参与确定性通过判定。
- 纯美术生产默认只运行 Artwork、公开 provenance/LFS 与 OKF 门禁；除非任务同时改动运行时资源或 Godot 代码，不运行完整 `Verify-GodotProject.ps1`。
- 背向合同用 `--layer-rule near_hand=behind-core`、`far_hand=behind-core`、`equipment=behind-core` 明确绘制层级，并用逐标签 `--visibility-cap` 限制外露面积。后层标签不得侵入批准锚点的核心区域，核心逐行必须连续；双手和武器只能露出贴着胶囊轮廓的外弧。
- 不把未校准候选标为可用 Sprite；武器或耳朵超出标准包围盒时，优先保持胶囊身体并显式记录例外。
- 不把正反案例快照当成生成母图；快照只服务于快速 Review，正式母图以 `examples/cases.json` 的 `approved_assets` 原图路径为准。
- 不把 `rejected` 或 `superseded` 中局部看似正确的版本继续传递到下一轮；反例只能用于写明禁止项和验收失败原因。
- 不批量生成多个角色来“碰运气”，不复制完整聊天记录或完整提示词到 OKF；稳定结论写入指南，提示词文档仍由 `artworks-prompt-library` skill 负责。
- 不用耳朵、武器或脚掌的最高/最低点替代核心胶囊体量；未获人工确认不得批量套用校准结果或推进下一个角色。
- 不用完整 Sprite 的耳尖到脚底高度或左右装备宽度校准方向图主体；核心蒙版未叠加通过时不得进入 128 预览或 Tile Review。
- 不把核心蒙版当作成品图层，也不通过擦线修补蒙版接缝；出现双轮廓、后脑鼓包或局部变胖时，回到正确母图原生重绘。
- `up-left` 默认将画面左侧近手放在前层完整显示、画面右侧远手放在后层并由身体部分遮挡；任务若有不同三维关系，必须在生成前显式覆盖。
- 对采用“无手臂”策略的胶囊角色，手掌必须以多像素接触面直接重叠主体边缘；删除手臂后不得留下浮空手掌、单像素切点或透明间隙。
- “无手臂”不等于“没有前爪”：胶囊角色始终保留两只前爪和两只后爪。四爪直接贴合身体，任何连接肢体都必须被语义蒙版标为禁止 arm/leg 标签并由状态机拒绝。需要复用正式身份的姿态还须绑定独立身份蒙版；灰白额斑必须是与锚点轮廓相符的贴服毛色，不能变成菱形、徽记或装饰物。
- 不用左右翼完整包围盒居中飞行单位；球核中心、虚拟落点与 Tile 中心必须处于同一垂直轴线。
- 不把飞行球核下沿当作脚底放在基线附近；球核中心应对齐地面基准角色的上部圆帽中心，让悬浮高度直接可读。
- 不把 `Tools/artworks/amazon` 的黑白设定图当成正式 Sprite 或方向母图；四方向生产只从已确认的胶囊体信徒/怪物基础图开始。
- 不把跨角色动作参考当成身份母图；赤柴 Hit 只锁定标准受击的漫画夸张程度，不向法师、死灵或羊魔迁移犬种、体量、无矛状态或圆盾。
- 法杖类 Cast 不接受 Idle 竖直法杖或杆尾偏置握持；法杖必须与主体共同建立施法轴，握点位于直杆中点容差内，且不遮挡脸部。
- 不用“向左倾”“顺时针/逆时针”单独描述等距动作。先声明角色世界朝向、固定等距摄像机和局部动作，再写屏幕空间硬验收点：主体顶部与脚底中心的相对 `x`、武器两端位置、手与武器绘制层级。模型生成后必须按屏幕坐标复核，不能把轮廓弯曲误判为倾斜方向。
- 裂颚羊魔 `up-left` 长柄动作采用项目特定的 3D 投影契约：主体顶部位于脚底中心左侧，双手和整把武器位于身体后层，身体遮挡杆身中段与手掌内侧。Melee UL 已批准为斧刃左上、杆尾右下；Thrown UL 从 DR 的同一过顶姿势转到背向视图时保留斧刃在上，但水平斜向翻转为斧刃右上、杆尾左下。不得把两种动作的屏幕轴混用。
- 死亡图生成前必须先按 `references/death-state-sprites.md` 分类核心拓扑。赤柴死亡图只为胶囊地面单位提供身体姿态；对球形飞行单位只提供头部朝画面右上的屏幕方向。
- 死亡姿态的身体、耳朵及与身体接触的四爪属于一个连续软体轮廓。参考蒙版只能测量主轴角与长宽比，禁止据此把核心、耳朵或四爪切开后分别旋转、压缩或重投影；压扁程度必须在完整姿态生成阶段解决。只有真正脱手的装备和限定眼区 expression overlay 可以独立合成。
- 死亡图只以胶囊核心或球核校准体量，完整 AABB 只用于裁切和 Tile Review；禁止把耳、四肢、装备、翅膀或特效纳入身体缩放，也禁止单轴拉伸。
- 死亡道具必须脱手，默认移除所有职业特效；未经人工确认的死亡图只能进入 `concepts` 或 `candidates`，不得接入 Unity。
- 投射物按 `references/projectile-sprites.md` 使用画布中心锚点并与施法者 `_128` 同屏定尺寸；禁止套用角色脚底基线、孤立判断 AABB 或在前一张未确认时批量生成下一张。

## Checklist

- [ ] 唯一母图和参考图职责已声明。
- [ ] 已阅读适用正反案例，并确认母图来自正式资产清单而非案例快照、反例或临时目录。
- [ ] 方向变体的纯核心主体蒙版已排除耳、口鼻、四肢和装备，并完成叠加校验。
- [ ] 方向变体已使用同角色确认正面作为唯一体量锚点，并检查上中下三个截面；下段没有梨形外扩。
- [ ] 单角色生成、去幕和透明四角检查完成。
- [ ] 动作姿态已按 `single-frame-action-poses.md` 检查 Cast 设备轴、Hit 夸张语言、DR/UL 参考职责和逐图停止门禁。
- [ ] 等距动作已同时写明世界空间、摄像机与屏幕空间验收点，并实际复核主体顶部/脚底、武器端点和绘制层级。
- [ ] 无手臂角色的手掌与主体形成稳定接触面，没有浮空或连接细线。
- [ ] 母版/预览尺寸、基线和等距脚位符合契约，或已明确归入候选。
- [ ] 方向变体已标明母图、原生目标方向、镜像边界及视觉换手取舍。
- [ ] 128 预览的脚底/虚拟落点已对准 `64×32` Tile 几何中心，且 Tile Review 通过。
- [ ] 死亡图已先分类核心拓扑：胶囊单位平直短厚，球形单位保持近圆；两者的头部线索均朝画面右上。
- [ ] 死亡图只比较核心体量，保留道具已脱手且无未经批准的特效；无脚底图使用尸体 AABB 中心完成 Tile 居中。
- [ ] 投射物已使用中心锚点、与施法者 `_128` 同屏定尺寸，并在 Tilemap 中按真实攻击方向旋转 Review。
- [ ] 运行时视觉 QA 仅使用 Godot 后台测试、生产输入注入或已有截图；没有通过 Computer Use 控制真实 Godot 窗口。
- [ ] 后台无法得到代表状态时已标记 `manual_visual_qa_pending`，没有自动降级到前台交互。
- [ ] 目录状态、版本号、OKF 验证和暂存范围已复核。
