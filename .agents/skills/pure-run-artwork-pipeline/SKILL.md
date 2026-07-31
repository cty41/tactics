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
| 规划四方向静态图 | `references/imagegen-iteration.md` 的“方向变体 / 双原生视图” |
| 核对运行时四向映射 | `references/imagegen-iteration.md` 的“运行时双原生图接入” |
| 查看正反案例与正式母图 | `references/review-casebook.md` 与 `examples/cases.json` |
| 锁定方向图核心体量 | 以同角色已确认的 `down-right` 为唯一体量锚点；核心蒙版只用于测量、三截面比较和 QA |
| 校对核心体量 | 地面单位对齐核心胶囊主体；飞行单位对齐球核并以球核中心作为水平锚点 |
| 校对 Tile 落点 | 脚底锚点或飞行单位虚拟落点必须精确位于 `64×32` Tile 几何中心 |
| 去幕与 alpha 校验 | `references/chroma-key-validation.md` |
| Review、归档和提交 | `references/artwork-review-and-git.md` |
| 只读批量检查 | `python scripts/validate_sprite_assets.py --root Tools/artworks --strict --review-examples` |

## When to use

- 生成或编辑 Pure Run 的角色、怪物、武器或职业变体 Sprite。
- 对绿幕/洋红幕图去幕、透明化、缩放、定位或生成 128 预览。
- Review 胶囊体比例、等距朝向、脚底基线、Tile 占用和遮挡层级。
- 整理 `concepts`、`calibrated`、`candidates`、`rejected`、`tmp`，或准备素材提交。

## Workflow

1. **先查案例，再锁定母图。** 阅读 `references/review-casebook.md` 中与任务相关的正反案例，并从 `examples/cases.json` 的 `approved_assets` 选择唯一正式母图。记录必须保持的身体、脚位、盾牌、武器和构图；把其他图片标为犬种、武器、姿态或色彩参考。不要让参考图替换母图的比例，禁止从 `rejected`、`superseded`、案例快照或 `tmp` 开始编辑。
2. **一次生成一个变体。** ImageGen 只处理一个角色或一个局部变体。明确“保持不变”区域；新武器、耳朵、翅膀和法术必须在屏幕空间中有清晰的前后层级，并完整留在画布内。
3. **去幕。** 选择纯 `#00ff00` 或 `#ff00ff` 背景，移除背景、软化 matte 边缘并检查绿色/洋红残留。保留真实透明 alpha，不把带色幕截图当母版。
4. **尺寸校准。** 方向图必须以同角色已确认的 `down-right` 为唯一核心体量锚点，并制作只包含中央胶囊体或球核的纯核心主体蒙版；耳朵、口鼻、眼睛、手掌、脚掌、武器、盾牌、法杖、翅膀和特效全部排除。蒙版只用于测量和 QA，禁止粘贴或参与成品合成。比较主体上缘、下缘、中心、最大宽度以及上中下三个截面的宽度分布；中段近似平行，下段只能持平或内收。飞行单位改用球核体量，把球核中心固定在身体水平锚点 `x=128`，并在垂直方向对齐基准胶囊体上部圆帽中心。完整 alpha 包围盒只用于画布安全、裁切和技术校验：标准母版 `256×256 RGBA`、脚底或虚拟基线 `y=236`。禁止单轴拉伸；外部轮廓超出标准时记录为例外或候选。
5. **缩小和逐角色 Review。** 生成 `128×128` Mitchell 等比预览，确认脚底基线 `y=118`。再使用真实错列等距 Tile 排布，将预览的脚底锚点 `(64,118)` 精确映射到目标 `64×32` Tile 的几何中心；飞行单位使用同一虚拟落点，不能用可见身体下沿替代。检查脚掌接触或悬浮间距、角色占用、脸部识别和装备分离。每次只校正一个角色，必须展示基准与当前角色并排并等待人工确认；确认后才进入下一个角色或生成其方向变体。
6. **归档并验证。** 用版本名保存成对的母版与 `_128` 预览，按目录语义归类；正式敌人进入 `approved`，失败历史进入 `rejected/superseded`。运行 `scripts/validate_sprite_assets.py --review-examples`，需要查看尚未确认的候选时再加 `--include-candidates`。
7. **提交准备。** 运行 OKF report/sync、bundle 校验和单元测试；按路径暂存，排除 `.hermes/`、`tmp/` 和运行时 Unity 文件。展示精确暂存清单，等待用户确认后再提交。

## Guardrails

- 默认不修改 Unity Prefab、AI、遭遇配置或运行时代码；只有用户明确授权“运行时美术接入”时，才可将已确认原生图配置到 Prefab，并且不得改变玩法朝向语义。
- 不覆盖已确认版本；新设计使用新的版本号，失败候选移动到 `rejected`，而不是删除历史证据。
- 不把未校准候选标为可用 Sprite；武器或耳朵超出标准包围盒时，优先保持胶囊身体并显式记录例外。
- 不把正反案例快照当成生成母图；快照只服务于快速 Review，正式母图以 `examples/cases.json` 的 `approved_assets` 原图路径为准。
- 不把 `rejected` 或 `superseded` 中局部看似正确的版本继续传递到下一轮；反例只能用于写明禁止项和验收失败原因。
- 不批量生成多个角色来“碰运气”，不复制完整聊天记录或完整提示词到 OKF；稳定结论写入指南，提示词文档仍由 `artworks-prompt-library` skill 负责。
- 不用耳朵、武器或脚掌的最高/最低点替代核心胶囊体量；未获人工确认不得批量套用校准结果或推进下一个角色。
- 不用完整 Sprite 的耳尖到脚底高度或左右装备宽度校准方向图主体；核心蒙版未叠加通过时不得进入 128 预览或 Tile Review。
- 不把核心蒙版当作成品图层，也不通过擦线修补蒙版接缝；出现双轮廓、后脑鼓包或局部变胖时，回到正确母图原生重绘。
- `up-left` 默认将画面左侧近手放在前层完整显示、画面右侧远手放在后层并由身体部分遮挡；任务若有不同三维关系，必须在生成前显式覆盖。
- 对采用“无手臂”策略的胶囊角色，手掌必须以多像素接触面直接重叠主体边缘；删除手臂后不得留下浮空手掌、单像素切点或透明间隙。
- 不用左右翼完整包围盒居中飞行单位；球核中心、虚拟落点与 Tile 中心必须处于同一垂直轴线。
- 不把飞行球核下沿当作脚底放在基线附近；球核中心应对齐地面基准角色的上部圆帽中心，让悬浮高度直接可读。
- 不把 `Tools/artworks/amazon` 的黑白设定图当成正式 Sprite 或方向母图；四方向生产只从已确认的胶囊体信徒/怪物基础图开始。

## Checklist

- [ ] 唯一母图和参考图职责已声明。
- [ ] 已阅读适用正反案例，并确认母图来自正式资产清单而非案例快照、反例或临时目录。
- [ ] 方向变体的纯核心主体蒙版已排除耳、口鼻、四肢和装备，并完成叠加校验。
- [ ] 方向变体已使用同角色确认正面作为唯一体量锚点，并检查上中下三个截面；下段没有梨形外扩。
- [ ] 单角色生成、去幕和透明四角检查完成。
- [ ] 无手臂角色的手掌与主体形成稳定接触面，没有浮空或连接细线。
- [ ] 母版/预览尺寸、基线和等距脚位符合契约，或已明确归入候选。
- [ ] 方向变体已标明母图、原生目标方向、镜像边界及视觉换手取舍。
- [ ] 128 预览的脚底/虚拟落点已对准 `64×32` Tile 几何中心，且 Tile Review 通过。
- [ ] 目录状态、版本号、OKF 验证和暂存范围已复核。
