# Pure Run 装备图输出规格

## 用途与画布

- 单件装备同时用于背包/商店 UI 图标与商店 Tilemap 摆件。
- 母版为 `256×256 RGBA`，透明背景，水平中心 `x=128`，可见物底部基线 `y=236`。
- 预览为 `128×128 RGBA`，底部基线 `y=118`；运行时圆盘、边框与稀有度颜色不画入资产。

## 视觉语言

- 沿用 Pure Run 粗深色轮廓、扁平纯色块、少量内部结构线。
- 无渐变、无写实高光、无柔光、无地面、无阴影、无文字、无水印、无 UI。
- 武器统一沿屏幕左下到右上排列，尖端或攻击端朝右上；盾牌采用等距三分之四俯视。
- 生成图使用纯 `#00ff00` 色幕，装备内部不得出现该颜色。

## 风格状态机

- 新装备合同绑定 `equipment_production_profile_v2.json`、品类、目标可见高度及允许范围；v1 profile 仅用于读取历史记录。
- 正式流程为 `create-equipment-contract` → `create-job` → `compile-equipment-prompt` → `begin-generation` → `ingest` → `prepare-equipment-candidate` → `render-equipment-review` → `record-equipment-style-verdict` → `approve` → `promote`。
- 默认候选处理保留生成图色阶，不执行量化；自动硬门禁只负责尺寸、基线、透明与哈希。内部色阶和平滑渐变是 advisory，AI 感、手绘线条和样式合理性由 cty41 对固定锚点 Review 人工确认。
- 第三方本机参考只用 `register-local-reference` 登记来源标签、职责和 SHA；不复制原图、不记录绝对路径、不写入公开 provenance。
- 只有明确技术修复才使用 `remediate-equipment-candidate` 建立 child attempt；不得覆盖原始 attempt。新装备不得使用 `reviewed_import`。

## 第一组编号

1. 亚马逊标枪
2. 亚马逊小圆盾
3. 法师橡木法杖
4. 死灵法师匕首
5. 魔剑士绑定魔剑
6. 普通单手铁剑
