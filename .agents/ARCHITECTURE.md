# Tactics Godot 架构

## 产品主线

远程 `main` 是 Godot 产品与治理权威。唯一项目为 `godot/project.godot`，入口 solution 为 `Tactics.Godot.slnx`。Unity 工程已从当前树退役；历史源码与资产只能从永久 annotated tag `unity-final-2026-08-08`、临时归档分支和 Frozen Oracle 证据恢复。

## 代码分层

```text
src/Tactics.Core
    纯 .NET 9 玩法、确定性 RNG、战斗与 Run 状态
            ↓
src/Tactics.Application
    用例、Content 编译、保存与运行时投影
            ↓
godot/Tactics.Godot.Adapter
    Godot Node、Resource、Scene、UI、Input 与 Presentation
            ↓
godot/project.godot + godot/content + godot/scenes
```

- Core/Application 不引用 Godot、Editor API、文件系统或迁移 DTO。
- Godot Adapter 负责 Resource 到纯 Draft/Snapshot 的边界转换。
- `ContentId` 是稳定业务身份；Godot UID 只负责项目内资源定位。
- `.tres`/`.tscn` 只通过 ResourceSaver、Editor API 或受测生成器写入。

## 当前产品系统

- 三职业 Pure Run、七层地图、战斗、成长、Inventory、Treasure 与 Save V6。
- Godot 原生 Theme/Control UI、等距棋盘、程序化 Presentation 和静默 Audio framework。
- Tactics Content Workbench 负责 Map、Event、Treasure、Encounter、Skill、AI 与 Presentation 的统一编辑入口。
- Frozen Oracle、Golden、NUnit、GdUnit、Gameplay Spec 与双 renderer smoke 共同提供回归证据。

## 构建与验证

- 本地主线门禁：`Tools/godot/Verify-GodotProject.ps1`。
- Windows RC：`Tools/godot/Build-GodotWindows.ps1`。
- `.github/workflows/godot-windows-build.yml` 通过手动选择 Debug、Release 或 Both 生成短期 Windows artifact。
- 自动门禁不替代真实 Editor Reload、视觉/UI/Input 和无引擎 clean-machine artifact 人工验收。

## 架构原则

1. 单一 Godot 项目和单一产品主线。
2. 引擎无关的 Core/Application 与 Godot Adapter 明确隔离。
3. 运行时只消费最终 Godot Resource/Scene，不消费迁移 DTO。
4. 内容所有权、自动验证和人工验收分别记录，不互相冒充。
5. 未知 Godot 行为先研究、复现并分级记录证据。
