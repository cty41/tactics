# 裂颚羊魔动作姿态输出规格

- 每个姿态 1 张静态图，不制作序列帧或 sprite sheet。
- 原生方向仅 `down-right` 与 `up-left`，运行时镜像补齐另外两向。
- `256×256 RGBA`，核心中心 `x=128`，蹄底基线 `y=236`；完整武器必须留在安全区内。
- Unity 目标导入：Single Sprite、`128 PPU`、Pivot `(0.5, 0.078125)`；只在人工批准后执行。
- Review：`128×128` Mitchell 预览，基线 `y=118`，并检查真实 `64×32` Tile 线框。
- Tween 在 60fps 游戏中控制时序；Sprite 无播放 fps。
- 文件名：`splitjaw_goat_<pose>_<direction>_vNN.png`，方向为 `dr` 或 `ul`。
- 首批：`melee_attack`、`thrown_attack`、`cast`、`hit`，每项两方向。
