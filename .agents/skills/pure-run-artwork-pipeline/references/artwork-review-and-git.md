# Artwork Review 与 Git

## 目录状态

- `concepts`：设计锚点和未发布迭代，不作为运行时 Sprite。
- `calibrated`：通过尺寸、透明和 Review 的发布集。
- `candidates`：外轮廓或尺寸仍待确认的候选；不能混入发布目录。
- `rejected`：明确否决但保留用于复盘的失败资产。
- `tmp`：ImageGen、去幕、叠加图和实验输出，永不提交。

Review 至少包含透明四角、128 预览、`64×32` Tile 线框、脚底接触、角色中心、物件分离和母图漂移检查。未校准资产要在状态说明中明确“不可用 Sprite”。

## 提交前检查

```powershell
python Tools/okf/catalog_impact.py report --worktree
python Tools/okf/catalog_impact.py sync --worktree --scope pure-run-artwork --write
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
git diff --cached --check
git status --short
```

按路径暂存文档、OKF 和 skill 文件；不要使用 `git add -A`，不要暂存 `.hermes/`、`tmp/` 或 Unity 运行时文件。Tools/artworks 位于项目艺术资源目录而非 Unity `Assets`，本计划不要求 `.meta` 配对，但后续若移动到 `Assets` 必须一并检查。

## 提交边界

先展示精确暂存清单、变更理由和提交消息，等待用户确认后再 commit/push。已有美术 revision 保持原提交，不在文档收口任务中重提交或覆盖。
