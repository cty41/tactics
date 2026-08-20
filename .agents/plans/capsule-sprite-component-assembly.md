# 胶囊角色 Sprite 混合组件拼装实施计划

## Summary

依据 `.agents/docs/2026-08-20-capsule-sprite-component-assembly-design.md`，把现有 schema v3 组件能力收口为胶囊角色固定六层混合拼装状态机。成功标准是：状态机正确表达 `far foot → far paw → body → equipment → near paw → near foot`，派生/独立生成/旧组件迁入边界可审计，机器验证能拒绝语义污染、错误层序、断裂接触与核心裂缝，并以魔剑士三个 UL 姿态作为逐张人工审核入口。

## Current State

- `artwork_pipeline.py` 已声明 `body`、`equipment`、`paw_overlay`、`foot_overlay`，并支持 `generated`、`derived`、`pre_v3_import`。
- `derive-component` 已能按 `near_hand`、`far_hand`、`near_foot`、`far_foot` 从获批 body 组件语义蒙版确定性提取。
- `create-assembly` / `render-assembly` 已绑定组件和蒙版哈希，但当前六层顺序错误地把 `near_foot_overlay` 放在 `body` 前面，且仍接受旧四层 Assembly。
- 当前测试覆盖基本确定性、组件不可晋升和脚爪 role 类型，但未覆盖完整六层的唯一顺序、必需层、组件来源审批差异、接触/遮挡与核心连续性。
- 魔剑士 Idle UL、Melee UL 已晋升但远脚遮挡观感不正确；Cast UL 只有实验组件与 Review，不得自动晋升。

## Relevant Context

- 设计权威：`.agents/docs/2026-08-20-capsule-sprite-component-assembly-design.md`
- 状态机：`.agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py`
- 自动测试：`.agents/skills/pure-run-artwork-pipeline/tests/test_artwork_pipeline.py`
- 使用规范：`.agents/skills/pure-run-artwork-pipeline/SKILL.md`
- 魔剑士组件与审核：`Tools/artworks/doge/components/`、`Tools/artworks/doge/demonbound/`、`Tools/artworks/doge/reviews/`
- 只改离线美术管线、测试、设计/知识文档和审核资产；不改 Godot 运行时、Resource、Profile 或玩法代码。

## File Structure

- `.agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py` — 六层合同、组件来源与确定性验证。
- `.agents/skills/pure-run-artwork-pipeline/tests/test_artwork_pipeline.py` — 状态转换、层序、语义、接触、哈希与审批回归。
- `.agents/skills/pure-run-artwork-pipeline/SKILL.md` — 对外工作流和六层使用边界。
- `.agents/docs/2026-08-20-capsule-sprite-component-assembly-design.md` — 已确认设计权威；实施后只写入验证结论。
- `.agents/knowledge/operations/pure-run-artwork.md` — OKF 当前能力和验证证据。
- `Tools/artworks/doge/**` — 魔剑士三张 UL 姿态的现有组件、spec 与审核输出；不得覆盖已确认原图。

## Scope

### In Scope

- 固定且只接受完整六层顺序。
- 区分派生组件的机器审批与独立生成组件的人工审批要求。
- 验证组件角色、语义纯度、接触、远侧遮挡、身体核心连续性和变换限制。
- 让 Review 显示单层、累积层和完整结果。
- 用魔剑士 Cast UL 现有组件验证首个完整六层 Assembly；Idle UL、Melee UL 只准备修复/审核入口，不自动替换或晋升。
- 更新 Skill、测试、设计验证结论与 OKF。

### Out of Scope

- Godot 运行时换装或动态拼装。
- 翅膀、尾巴、真实手臂/腿和任意骨骼角色。
- 任意图层名、任意层数、旋转、非等比缩放或自由形变。
- 自动批准或自动替换已晋升魔剑士 Sprite。
- Hit DR/UL、Death 或新的 ImageGen 调用。

## Implementation

### Task 1: 收口固定六层合同

- 目标：修正层序并删除旧四层兼容入口。
- 输入：设计文档固定六层。
- 输出：唯一合法顺序 `far_foot_overlay → far_paw_overlay → body → equipment → near_paw_overlay → near_foot_overlay`。
- 涉及文件：状态机与对应测试。
- 验收标准：缺层、重复层、未知层、旧四层和任意顺序均被明确拒绝；合法六层可创建稳定 Assembly ID。

### Task 2: 完成混合来源审批合同

- 目标：让派生组件与生成/迁入组件按设计使用不同证据门禁。
- 输入：组件 contract、attempt、derivation/migration/approval receipt。
- 输出：Assembly 创建时验证来源模式及所需审批证据。
- 涉及文件：状态机与测试。
- 验收标准：`derived` 必须绑定获批源和派生 receipt；`generated` 必须有组件人工 approval；`pre_v3_import` 必须有迁入 receipt；组件仍不可直接晋升。

### Task 3: 加入组件与完整 Assembly 几何门禁

- 目标：机械拒绝语义污染、浮空、单像素接触、错误遮挡和核心裂缝。
- 输入：组件图、语义蒙版、Assembly 合成图与合同阈值。
- 输出：可重复的验证指标和明确 issue ID。
- 涉及文件：状态机与测试。
- 验收标准：测试分别构造并拒绝污染标签、缺手脚、断裂接触、远侧可见面积超限和核心逐行不连续；合法 fixture 通过且两次报告一致。

### Task 4: 改进逐层 Review 并验证魔剑士 Cast UL

- 目标：让人工能同时看单层、累积层、最终 Sprite 和 Tile 落点。
- 输入：现有 Cast UL body/foot 组件及必要的手爪/装备组件。
- 输出：完整 `assemblyLayerReview` 和 Cast UL Tile Review 候选。
- 涉及文件：状态机、测试及 `Tools/artworks/doge/**` 新版本审核输出。
- 验收标准：Review 明确展示远脚/远手位于身体后、近手/近脚位于身体前；不覆盖现有资产、不自动批准或晋升。

### Task 5: 文档、OKF 与门禁收口

- 目标：使代码、Skill、设计和知识状态一致。
- 输入：实现与测试证据。
- 输出：更新 Skill/设计验证结论/OKF，记录仍需人工确认的三个 UL 姿态。
- 涉及文件：Skill、设计文档和 pure-run-artwork OKF。
- 验收标准：状态机单测、strict check、目标素材验证、OKF impact/sync/test/bundle 和 `git diff --check` 通过；人工观感不写成自动通过。

## Test Plan

- `python .agents/skills/pure-run-artwork-pipeline/tests/test_artwork_pipeline.py`
- `python .agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py --root . check --strict`
- 对新生成的魔剑士审核目标运行定向 `validate_sprite_assets.py`，不把历史候选失败归因于本次工作。
- `python Tools/okf/catalog_impact.py report --worktree`，同步受影响 scope 后运行 OKF 单测与 bundle 校验。
- `git diff --check`。
- 人工门禁：逐张确认 Idle UL、Melee UL、Cast UL 的远近手脚遮挡、身份一致性和 Tile 可读性；本计划不自动签发 `cty41` approval。

## Risks / Assumptions

- 假设现有 schema v3 组件记录仍可读取；旧四层 Assembly 只保留历史记录可读性，不再允许创建新记录。
- 某些现有 Cast UL 组件可能缺少合格手爪或装备层；若无法从获批源确定性派生，本轮只建立 `generated` 任务入口，不调用 ImageGen。
- 几何阈值必须从现有 256×256 胶囊合同推导，不能为通过单张素材而放宽全局规则。
- 公开 provenance strict check 可能继续被既有未登记候选阻断；必须区分本次新增问题与历史工作区状态。

## Handoff Notes

- 先运行现有 artwork pipeline 单测，保存基线，再修改六层顺序。
- 不把当前 `doge_capsule_demonbound_all_pose_tilemap_review_v01.png` 当作获批 Sprite；它只是审核入口。
- 不修改 `godot/default_bus_layout.tres`，也不触碰 Godot 运行时资产。
- 完成实现和验证后，按 `project-doc-organization` 将长期结论合入设计/Skill，未完成项写入统一缺口或经用户批准建立新计划，更新 OKF，然后删除本计划，由 Git 保存历史。
