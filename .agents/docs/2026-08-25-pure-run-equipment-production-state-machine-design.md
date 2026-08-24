# Pure Run 装备美术生产状态机 v2

## 目标

新装备图在通用 artwork contract/job/attempt 之上使用独立策略层，固化本轮装备生产形成的有效方法：共享基础画风、按品类选择已批准锚点、正式 ImageGen 血缘、保真后处理、固定对比 Review，以及由 cty41 作出的最终风格判断。

历史 `styleSpec` 与 `reviewed_import` 记录保持只读兼容；v2 装备合同不得走 reviewed import。该设计不修改 Godot runtime、Resource 或玩法代码。

## 数据与状态

- `equipmentProductionSpec`：绑定 profile ID/path/hash、品类、品类锚点及尺寸范围。
- 品类为 weapon、shield、armor、jewelry、consumable；共享基础规则，各自绑定批准资产锚点。
- `local-reference`：只记录角色、来源标签、文件名和 SHA-256；不保存绝对路径、不复制第三方图片、不进入公开 provenance。
- 新装备 job 强制 `requiresInvocation=true`，并使用 `compile-equipment-prompt` 生成确定性任务包。
- retry 必须引用上一 attempt 的结构化 feedback；技术修复产生 child attempt，保留原始输出。
- 默认候选处理为 chroma 清理、透明 RGB 归零、真实 AABB、等比缩放、居中和 baseline 对齐；不量化。

## 门禁

硬门禁仅覆盖可机械确认的画布、可见尺寸、baseline、透明背景和哈希一致性。色阶复杂度与平滑渐变只作为 advisory，不能冒充“AI 味”自动判定。

装备 Review 固定包含基础/品类锚点、上一 attempt（如有）、当前候选和 128px 预览。promote 同时要求：

1. 技术报告通过；
2. approval 与候选、Review 哈希匹配；
3. cty41 的 equipment style verdict 为 approved，且绑定同一候选与 Review 哈希。

## 第三方参考边界

第三方截图仅作为本机形状、材质或风格参考，descriptor 不代表项目拥有图片版权。公开仓库只保留不可反推本机目录的描述信息和摘要；生成结果仍按项目自身 provenance 登记。

## 兼容与验证

既有 v1 profile、合同和 promoted attempt 不迁移、不重写。测试使用本地 fixture 模拟生成与参考输入，不调用在线 ImageGen；严格检查额外验证 v2 profile、锚点、local-reference 和 style verdict 的结构与哈希。
