# 赤柴猎人动作姿态输出规格

- 每个姿态是 1 张静态图，不制作序列帧或 sprite sheet。
- 原生方向仅 `down-right` 与 `up-left`；运行时镜像补齐另外两向并接受视觉换手。
- 画布 `256×256 RGBA`，主体中心 `x=128`，脚底基线 `y=236`，完整 alpha 包围盒不得裁切。
- Unity 目标导入：Single Sprite、`128 PPU`、Pivot `(0.5, 0.078125)`；只在人工批准后执行。
- Review 预览：Mitchell 等比缩小为 `128×128`，脚底基线 `y=118`，另检查真实 `64×32` Tile 线框。
- Tween 在 60fps 游戏中控制持续时间；Sprite 本身没有播放 fps。
- 文件名：`doge_hunter_<pose>_<state>_<direction>_vNN.png`，其中 state 为 `held` 或 `unarmed`，方向为 `dr` 或 `ul`。
- 首批唯一方向图：`melee_attack_held`、`cast_spear_hidden`、`hit_spear_hidden`，每项两方向；`ThrownAttack` 复用近战图，`Cast` 与 `Hit` 的 `Default / Unarmed` 分别共用对应无矛方向对。
