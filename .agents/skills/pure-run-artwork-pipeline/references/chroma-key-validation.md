# 去幕与透明校验

## 选择幕色

- 主体不是绿色时优先使用纯 `#00ff00`；煤绿色或其他接近绿色的主体改用纯 `#ff00ff`。
- 生成提示中要求没有地面、阴影、反射、文字、水印或装饰框。去幕前保存原始源图，去幕后另存透明 PNG。

## 验收

透明化后必须满足：

1. 文件为 `256×256 RGBA` 或 `128×128 RGBA`；四角 alpha 为零。
2. alpha 包围盒无裁切，外轮廓没有明显绿/洋红色溢边。
3. 非透明像素中不能出现精确 `(0,255,0)` 或 `(255,0,255)` 幕色；软 matte 也要人工检查残边。
4. 线稿和色块在缩小后不因去幕而断裂，眼睛高光、武器尖端和脚掌仍可见。

项目中可复用 ImageGen 技能提供的去幕脚本，但要在脚本输出后再次用本 skill 的只读校验器检查；校验器只读，不自动覆盖 PNG。

## 命令

```powershell
python .agents/skills/pure-run-artwork-pipeline/scripts/validate_sprite_assets.py --root Tools/artworks --strict
python .agents/skills/pure-run-artwork-pipeline/scripts/validate_sprite_assets.py --root Tools/artworks --include-candidates
```
