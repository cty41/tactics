# Pure Run 伯恩山犬商人裸身份母图执行计划

## Summary

将当前偏离项目角色契约的人类商人候选替换为伯恩山犬胶囊体裸身份候选。本计划只完成 `idle DR` 的静态生成、管线 Review 和用户造型确认；不添加服饰或职业道具，不 promote，不修改 Godot 运行时。

成功标准：新候选在 `128×128` 与 `64×32` Review 中可明确读作伯恩山犬，符合 Pure Run 刚性胶囊体、无手臂/无腿、四爪贴体、右下视角和 `y=236` 基线规则，并获得用户对静态造型的明确确认。

## Current State

- 设计权威：`.agents/docs/2026-08-23-pure-run-bernese-merchant-base-design.md`。
- 现有商人合同：`contract-530b97d5974ad506`，资产 `prop-merchant_idle`，类型为 `projectile`，`maskRequired=false`，无 anchor。
- 现有 job：`job-ed5f76ac4fb68184`。
- `job-ed5f76ac4fb68184-a002` 是已完成静态 Review 的人类兜帽商人候选；用户明确判定其偏离“犬类 + 胶囊体”设计。该 attempt 保留为失败审阅证据，不 approve、不 promote、不删除。
- 当前严格状态机检查为 385 项全绿。
- 现有 contract 不具备 `ground_character`、语义蒙版要求或 approved anchor，不能真实执行 `calibrate-core`。本计划不得伪造该证据。

## Relevant Context

- 所有状态转换只通过 `.agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py`。
- 单图流程：记录用户反馈 → retry → ImageGen → ingest → prepare → validate → render-review → 用户确认。
- 生成阶段使用 Codex 内置 ImageGen；生成结果需去幕、清除边缘污染、等比缩放和基线校准。
- 母版 `256×256 RGBA`，核心中心 `x=128`，最低可见脚爪像素 `y=236`。
- 当前工作树存在其他用户修改；仅操作本计划列出的路径，不暂存、不提交、不清理其他文件。

## File Structure

- `.agents/docs/2026-08-23-pure-run-bernese-merchant-base-design.md` — 已确认的伯恩山犬裸身份设计权威。
- `Tools/artworks/pure_run/props/prompt_merchant_idle.md` — 当前 merchant job 的提示词入口；按已确认设计改为伯恩山犬裸胶囊体要求。
- `Tools/artworks/pure_run/props/concepts/pure_run_prop_merchant_idle_*` — ImageGen 源图与确定性规范化候选。
- `Tools/artworks/pipeline/feedback/` — 通过 CLI 记录用户对人类 a002 的重试反馈。
- `Tools/artworks/pipeline/attempts/`、`artifacts/`、`reports/`、`reviews/` — CLI 生成的新 attempt、验证报告与 Review 证据。
- `Tools/artworks/pure_run/props/approved/` — 本计划不写入；只有后续正式胶囊 contract、蒙版、校准和再次人工确认完成后才允许 promote。

## Scope

### In Scope

- 保留人类商人 a002 为失败审阅证据。
- 在现有 merchant job 上记录结构化用户反馈并创建 retry。
- 生成一张无服饰、无道具的伯恩山犬 idle DR 静态候选。
- 完成透明背景、尺寸、中心、基线、边缘污染与管线严格检查。
- 输出 `128×128`、overlay 和 `64×32` Tile Review，等待用户确认。

### Out of Scope

- 商人服饰、货袋、背包、金币、账本和交易反馈姿态。
- idle UL、动作、受击、死亡或动画。
- 语义蒙版、核心校准、正式 approve/promote。
- 新建或修改正式胶囊 contract；该动作须在本轮造型确认后另行决策。
- Godot Resource、场景、运行时代码、共享库、工具链、CI 或项目结构修改。
- Git 暂存、提交、推送或清理。

## Implementation

### Task 1: 记录人类商人候选的用户否决

- 目标：将用户反馈写入不可变状态机证据，并保留 a002。
- 输入：`job-ed5f76ac4fb68184-a002` 与已确认设计文档。
- 输出：human/identity/retry feedback；基于该 feedback 的新 attempt。
- 涉及文件：
  - Pipeline CLI 生成 `Tools/artworks/pipeline/feedback/<feedback-id>.json`。
  - Pipeline CLI 生成 `Tools/artworks/pipeline/attempts/job-ed5f76ac4fb68184-a003.json` 或实际下一 ordinal。
- 验收标准：
  - feedback 明确记录“人类结构偏离犬类胶囊体”。
  - frozen invariants 包含 idle DR、无服饰、无道具、伯恩山犬身份和标准画布规则。
  - a002 文件与 Review 仍存在，状态不被 approve 或 promote。

### Task 2: 更新提示词并生成伯恩山犬裸身份候选

- 目标：生成只验证犬种与胶囊体的单张 idle DR。
- 输入：设计文档、现有 merchant prompt、新 attempt。
- 输出：一张 ImageGen 源图和项目内 concept 源图。
- 涉及文件：
  - Modify `Tools/artworks/pure_run/props/prompt_merchant_idle.md`。
  - Create `Tools/artworks/pure_run/props/concepts/pure_run_prop_merchant_idle_bernese_base_v01_*`。
- 验收标准：
  - 黑色主体毛、窄白额纹至口鼻、暖棕眉点/口鼻两侧/四爪、短垂耳均清楚。
  - 身体为等宽刚性胶囊体，无人形躯干、手臂或腿。
  - 四爪直接贴住胶囊边缘；不含服饰、项圈、货袋或其他道具。
  - 表情闭嘴、放松、轻微友善，不困倦、不夸张微笑。
  - ImageGen 调用与最终源图路径在交付说明中可追溯。

### Task 3: 规范化并完成静态管线 Review

- 目标：把候选转换为符合画布和基线规则的可审阅静态资产。
- 输入：Task 2 源图与新 attempt。
- 输出：prepared PNG、验证报告、overlay、`preview128`、`tile64x32`。
- 涉及文件：
  - Pipeline CLI 生成对应 `artifacts/`、`reports/`、`reviews/` 记录。
- 验收标准：
  - `256×256 RGBA`，透明四角，无色幕残边或孤立低 alpha 噪点。
  - 主体横向中心与 `x=128` 对齐，最低可见脚爪像素为 `y=236`。
  - 可见胶囊核心约为项目标准体量，耳朵或毛色标记不用于掩盖错误缩放。
  - `validate` 通过，`check --strict` 无 issue。
  - overlay、`128×128` 与 Tile Review 中犬种、四爪接触和面部标记仍可读。

### Task 4: 用户静态造型确认门禁

- 目标：只获取对伯恩山犬裸身份造型的人工结论。
- 输入：Task 3 的三张 Review 图。
- 输出：用户明确确认、要求 retry 或终止该方向。
- 涉及文件：用户确认前不新增 formal approval 或 promoted 文件。
- 验收标准：
  - 回复中明确说明当前 contract 不支持正式胶囊校准，因此本轮不 promote。
  - 若用户要求修改，反馈必须先经 `record-feedback`，再创建新 attempt。
  - 若用户确认，结束本计划，并将“建立正式 ground_character contract、制作语义蒙版、批准 anchor、核心校准、最终 approve/promote”作为下一独立计划的输入。

## Test Plan

- 自动验证：
  - `python .agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py --root . validate --attempt-id <attempt>`
  - `python .agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py --root . check --strict`
  - 使用 ImageMagick 检查尺寸、alpha bbox、透明四角、最低可见像素和可疑绿边。
- 人工验证：
  - 用户审阅 overlay、`preview128` 与 `tile64x32`。
  - 人工结论只代表静态身份造型通过，不代表语义蒙版、核心校准、Godot 运行时或场景验收通过。
- 回归范围：
  - 只核对新增 merchant feedback/attempt 与 prompt；既有已 promoted 物品、节点图和篝火不重新生成。

## Risks / Assumptions

- ImageGen 容易回到人形身体、自然犬四足体或添加项圈/服饰；发现任一情况应重生，不以擦除方式修补结构。
- 伯恩山犬白额纹可能被误画成围巾或服装；它必须限制在毛色标记范围。
- 现有 merchant contract 是历史上按非胶囊道具建立的。复用它仅满足本轮静态造型迭代，不足以产出正式胶囊校准证据。
- 用户已明确授权本轮只修改 `Tools/artworks` 资产和设计/计划文档；未授权任何运行时或工具链变更。

## Handoff Notes

- 首先阅读设计文档，再读取 `job-ed5f76ac4fb68184`、a002 和对应 contract 的当前 JSON；不要依赖本计划中的状态快照替代现场检查。
- 第一项写操作必须是经 CLI 记录 a002 的用户 feedback；禁止手改 registry、attempt、report 或 receipt。
- 一次只生成一张图，用户确认前不开始服饰、交易反馈或其他方向。
- 不 approve、不 promote 当前裸身份候选；现有 contract 缺少正式胶囊校准条件。
- 完成实现与验证后，按 `project-doc-organization`：把长期结论并入权威设计文档；将正式胶囊 contract/蒙版/校准列入经用户批准的新计划；更新受影响 OKF scope；删除已完成计划，由 Git 保存历史。
