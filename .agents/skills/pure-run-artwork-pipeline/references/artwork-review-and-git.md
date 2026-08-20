# Artwork Review 与 Git

## 目录状态

- `concepts`：设计锚点和未发布迭代，不作为运行时 Sprite。
- `calibrated`：通过尺寸、透明和 Review 的发布集。
- `approved`：通过人工 Review 的非 Doge 正式敌人集；可以作为后续方向或变体母图。
- `candidates`：外轮廓或尺寸仍待确认的候选；不能混入发布目录。
- `rejected`：明确否决但保留用于复盘的失败资产。
- `rejected/superseded`：已由正式版本替代的历史失败稿；保留原文件名，但禁止作为母图。
- `tmp`：ImageGen、去幕、叠加图和实验输出，永不提交。

Review 至少包含透明四角、128 预览、`64×32` Tile 线框、脚底接触、角色中心、物件分离和母图漂移检查。未校准资产要在状态说明中明确“不可用 Sprite”。

最小正反案例位于 `../examples/`，清单是 `../examples/cases.json`。案例快照不是发布资产；新增案例前必须确认反例确实表现所描述的错误，不能因为文件名含有 `candidate` 就自动判为失败。

## 提交前检查

```powershell
python Tools/okf/catalog_impact.py report --worktree
python Tools/okf/catalog_impact.py sync --worktree --scope pure-run-artwork --write
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
python .agents/skills/pure-run-artwork-pipeline/scripts/validate_sprite_assets.py --root Tools/artworks --strict --review-examples
git diff --cached --check
git status --short
```

按路径暂存文档、OKF 和 skill 文件；不要使用 `git add -A`，不要暂存 `.hermes/`、`tmp/` 或未授权的 Godot 运行时文件。`Tools/artworks` 是候选与审计目录；只有获得运行时接入授权后，才可通过项目资产管线复制到 `godot/assets`。

## 提交边界

先展示精确暂存清单、变更理由和提交消息，等待用户确认后再 commit/push。已有美术 revision 保持原提交，不在文档收口任务中重提交或覆盖。
