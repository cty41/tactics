# 墨西哥无毛犬死灵法师动作姿态参考笔记

## Keep

- 唯一身份母图：`../calibrated/doge_capsule_necromancer_color_calibrated_v04.png`（down-right）与 `../calibrated/doge_capsule_necromancer_color_ul_v07.png`（up-left）。
- 粉灰胶囊身体、深粉内耳、长尖耳、窄口鼻、异色极简眼、短脚掌和唯一短匕首。
- 粗深棕轮廓、平涂色块和极少内部线条；目标是 Pure Run 小体量卡通 Sprite。
- 身体中心 `x=128`、脚底基线 `y=236`；核心体量仅与对应正式母图比较。

## Ignore

- 母图右手蓝色鬼火；它只属于 Idle，Cast 与 Hit 必须完全移除。
- 运行时 Tile、阴影、Marker、血条、施法 VFX、投射物、命中特效和透明画布空白。
- 写实肌肉、长手臂、长袍、兜帽、骷髅装饰、地面、透视背景和运动线。

## Avoid Drift

- 不改变核心胶囊高度、宽度趋势、长耳轮廓、口鼻位置、眼睛不对称关系、脚位或匕首结构。
- 不新增第二把匕首、法杖、书、骨饰、头饰、护甲、鬼火、光球或职业专属 VFX。
- 移除鬼火后，空手掌仍须与身体形成多像素接触，不能浮空或被错误删除。
- up-left 必须以批准 UL 母图重建近远手、匕首和耳部遮挡，不能镜像 DR 或使用历史错误远手版本。
