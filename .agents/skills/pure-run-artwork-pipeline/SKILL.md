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
| 去幕与 alpha 校验 | `references/chroma-key-validation.md` |
| Review、归档和提交 | `references/artwork-review-and-git.md` |
| 只读批量检查 | `python scripts/validate_sprite_assets.py --root Tools/artworks --strict` |

## When to use

- 生成或编辑 Pure Run 的角色、怪物、武器或职业变体 Sprite。
- 对绿幕/洋红幕图去幕、透明化、缩放、定位或生成 128 预览。
- Review 胶囊体比例、等距朝向、脚底基线、Tile 占用和遮挡层级。
- 整理 `concepts`、`calibrated`、`candidates`、`rejected`、`tmp`，或准备素材提交。

## Workflow

1. **锁定母图。** 选出唯一角色母图，记录必须保持的身体、脚位、盾牌、武器和构图；把其他图片标为犬种、武器、姿态或色彩参考。不要让参考图替换母图的比例。
2. **一次生成一个变体。** ImageGen 只处理一个角色或一个局部变体。明确“保持不变”区域；新武器、耳朵、翅膀和法术必须在屏幕空间中有清晰的前后层级，并完整留在画布内。
3. **去幕。** 选择纯 `#00ff00` 或 `#ff00ff` 背景，移除背景、软化 matte 边缘并检查绿色/洋红残留。保留真实透明 alpha，不把带色幕截图当母版。
4. **尺寸校准。** 在 `256×256 RGBA` 画布内等比缩放和定位：标准完整轮廓高 `122 px`，脚底基线 `y=236`，身体锚点 `x=128`。一并缩放外部武器和特效，禁止单轴拉伸；未满足标准的外部轮廓先留在候选目录。
5. **缩小和 Review。** 生成 `128×128` Mitchell 等比预览，确认目标轮廓约 `62 px`、基线 `y=118`。再放入临时 `64×32` Tile 线框，检查脚掌接触、角色占用、脸部识别和武器分离。
6. **归档并验证。** 用版本名保存成对的母版与 `_128` 预览，按目录语义归类；运行 `scripts/validate_sprite_assets.py`，需要查看候选时加 `--include-candidates`。
7. **提交准备。** 运行 OKF report/sync、bundle 校验和单元测试；按路径暂存，排除 `.hermes/`、`tmp/` 和运行时 Unity 文件。展示精确暂存清单，等待用户确认后再提交。

## Guardrails

- 不修改 Unity Prefab、AI、遭遇配置或运行时代码；本 skill 只负责项目艺术资源的执行与验收。
- 不覆盖已确认版本；新设计使用新的版本号，失败候选移动到 `rejected`，而不是删除历史证据。
- 不把未校准候选标为可用 Sprite；武器或耳朵超出标准包围盒时，优先保持胶囊身体并显式记录例外。
- 不批量生成多个角色来“碰运气”，不复制完整聊天记录或完整提示词到 OKF；稳定结论写入指南，提示词文档仍由 `artworks-prompt-library` skill 负责。

## Checklist

- [ ] 唯一母图和参考图职责已声明。
- [ ] 单角色生成、去幕和透明四角检查完成。
- [ ] 母版/预览尺寸、基线和等距脚位符合契约，或已明确归入候选。
- [ ] 128 预览与 `64×32` Tile Review 通过。
- [ ] 目录状态、版本号、OKF 验证和暂存范围已复核。
