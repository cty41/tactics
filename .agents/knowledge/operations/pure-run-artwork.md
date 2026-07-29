---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/Tools/artworks
title: Pure Run Artwork Pipeline
description: Pure Run 角色美术的生成、去幕、尺寸校准、Review 与提交入口。
tags: [operations, pure-run, artwork, sprite, unity]
timestamp: "2026-07-29T10:44:04+08:00"
status: active
catalog_scope: pure-run-artwork
repo_paths:
  - .agents/docs/pure-run-artwork-guidelines.md
  - .agents/skills/pure-run-artwork-pipeline
  - Tools/artworks/amazon
  - Tools/artworks/doge
  - Tools/artworks/pure_run
verified_revision: c68dbebe
source_fingerprint: sha256:1af5404faddeab30c21bd0838c75125aaa82b6fe6fbf4ceed94e22b7a95d4cec
---

# Pure Run 角色美术流水线

## Current State

- `c68dbebe` 是当前角色美术提交的验证锚点，Doge `calibrated` 目录包含六个按统一尺寸输出的角色母版和 128 预览。
- `Tools/artworks/pure_run/enemies/candidates` 中的羊魔和蝙蝠仍待统一校准；`rejected` 中的横胖蛤蟆不符合标准胶囊体宽度。
- 设计、尺寸和目录语义见 `.agents/docs/pure-run-artwork-guidelines.md`，执行与只读校验见 `.agents/skills/pure-run-artwork-pipeline/SKILL.md`。

## Workflow

确认唯一母图后，一次只生成一个角色或变体；参考图只承担犬种、武器或姿态的局部信息。完成去幕、alpha 检查、`256×256` 母版定位、`128×128` 缩小和 `64×32` Tile Review 后，再将资产归入 calibrated、candidates 或 rejected。未通过统一基线的图不能冒充运行时 Sprite。

## Relationships

- 设计契约：`.agents/docs/pure-run-artwork-guidelines.md`
- 执行 skill：`.agents/skills/pure-run-artwork-pipeline`
- 相关资产：`Tools/artworks/amazon`、`Tools/artworks/doge`、`Tools/artworks/pure_run`
- 提示词库边界：可复用 GPT Image 提示词文档由 `artworks-prompt-library` skill 维护，本 scope 只维护项目执行和验收状态。

## Verification Guidance

```powershell
python .agents/skills/pure-run-artwork-pipeline/scripts/validate_sprite_assets.py --root Tools/artworks --strict
python Tools/okf/catalog_impact.py report --worktree
python Tools/okf/catalog_impact.py sync --worktree --scope pure-run-artwork --write
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
```

校验脚本只读 PNG 并输出机器可读摘要；候选资产需使用 `--include-candidates` 额外查看，但外部武器轮廓不会被错误地当成发布尺寸失败。Git 提交前按路径暂存并排除 `.hermes/`、`tmp/` 和任何 Unity 运行时文件。

## Citations

暂无外部引用；当前状态以仓库中的指南、PNG 和验证命令为准。
