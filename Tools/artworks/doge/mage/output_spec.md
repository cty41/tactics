# 凯利蓝㹴法师动作姿态输出规格

- 每个姿态 1 张静态图，不制作序列帧、sprite sheet 或移动动画。
- 原生方向仅 `down-right` 与 `up-left`；运行时镜像补齐另外两向并接受法杖视觉换手。
- `256x256 RGBA`，核心中心 `x=128`，脚底基线 `y=236`，完整法杖必须位于安全区内。
- Unity 目标导入：Single Sprite、`128 PPU`、Pivot `(0.5, 0.078125)`；仅在 DR/UL 分别人工批准后执行。
- Review：Mitchell 等比缩小为 `128x128`、基线 `y=118`，并检查核心蒙版叠加与真实 `64x32` Tile 线框。
- Tween 在 60fps 游戏中控制时序；Sprite 本身没有播放 fps。
- 候选文件名：`doge_capsule_mage_<pose>_<direction>_vNN.png`，pose 为 `cast` 或 `hit`，方向为 `dr` 或 `ul`。
- 首批仅 `Cast` 与 `Hit` 两对；Melee、Ranged、Idle、移动和逐帧动画不在范围内。
