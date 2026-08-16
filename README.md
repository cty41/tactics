# Tactics

Tactics 是一个使用 Godot 4.7 C# 开发的回合制战棋 Roguelike。当前核心模式 **Pure Run** 围绕法师、死灵法师与亚马逊女战士组成的固定三人小队展开：玩家需要在七层路线中完成战斗、事件与服务节点，在单局内组合技能、属性、装备和消耗品，并把队伍带到最终 Boss。

> 项目状态：开发中原型。主要流程与自动化回归已经建立，但视觉、操作体验和正式发布仍需持续人工验收；当前构建不代表最终发行版本。

## 核心内容

- 确定性的网格战斗、回合顺序、技能效果、状态与敌方 AI。
- 七层 Pure Run 地图，包含普通战斗、Elite、Rest、Store、Mystery、Treasure 与 Boss。
- 三职业 Lv1–Lv3 技能成长、属性分配、装备、消耗品与持续 Buff。
- Save V6 检查点、异常恢复和可重复验证的 Run 状态。
- Godot 原生 UI、等距棋盘、程序化战斗表现与 Content Workbench。
- NUnit、Frozen Oracle、GdUnit、Gameplay Specs 和双渲染器 smoke 组成的回归链。

## 技术架构

```text
src/Tactics.Core
    纯 .NET 9 玩法规则、确定性 RNG、Battle 与 Run 状态
            ↓
src/Tactics.Application
    用例、内容编译、保存与运行时投影
            ↓
godot/src/Tactics.Godot.Adapter
    Godot Node、Resource、Scene、UI、Input 与 Presentation
            ↓
godot/project.godot + godot/content + godot/scenes
```

`Tactics.Core` 和 `Tactics.Application` 不依赖 Godot。引擎对象、资源加载、文件系统和界面逻辑集中在 Adapter 层；运行时内容由 Godot Resource、PackedScene 和 Catalog 驱动。

## 快速开始

### 环境要求

- Windows
- [Git LFS](https://git-lfs.com/)
- Godot `4.7.1-stable` Mono/.NET 版本
- .NET SDK `9.0.312`，或同一 feature band 中兼容的更新 patch

### 获取与运行

```powershell
git clone git@github.com:cty41/tactics.git
Set-Location tactics
git lfs pull --include="godot/**" --exclude=""
```

在 Godot Project Manager 中导入并打开：

```text
godot/project.godot
```

项目主场景是 `godot/scenes/Main.tscn`。首次打开时等待 Godot 完成资源导入和 C# 编译，再从编辑器运行项目。

## 验证

仓库的统一验证入口会串行执行 locked restore、.NET build、NUnit、Frozen Oracle、Gameplay Specs、GdUnit、Headless Runtime/Editor、Catalog ownership、OKF 和渲染兼容性检查：

```powershell
pwsh -NoProfile -File .\Tools\godot\Verify-GodotProject.ps1 `
  -GodotExecutable "D:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe"
```

完整门禁还需要仓库工具使用的 Python、Node.js/npm 依赖。脚本会在缺少必要工具或版本不匹配时停止并报告原因。

## Windows 构建

GitHub Actions 的 **Godot Windows Debug and Release build** 工作流支持手动选择：

- `debug`：开发调试包，使用 Godot Debug Export。
- `release`：接近玩家交付形态的 Release 候选包。
- `both`：在同一次公共验证后依次生成两档，默认选项。

安装对应版本的 Godot Export Templates 后，也可以在本地构建：

```powershell
pwsh -NoProfile -File .\Tools\godot\Build-GodotWindows.ps1 `
  -GodotExecutable "D:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe" `
  -BuildFlavor Both
```

输出位于 `Build/Godot/Windows-Debug` 和 `Build/Godot/Windows-Release`。构建包的自动 smoke 不能替代干净 Windows 环境中的人工启动与玩法验收。

## 仓库导航

| 路径 | 职责 |
|---|---|
| `src/Tactics.Core` | 引擎无关的玩法规则与确定性状态 |
| `src/Tactics.Application` | 应用用例、内容转换和保存边界 |
| `godot/` | 唯一 Godot 项目、Adapter、Scene、Resource 和测试宿主 |
| `Tools/godot/` | 主线验证、Windows 构建、包审计和启动 smoke |
| `Tests/gameplay-specs/` | 平台中立的 Gameplay Spec 与 Godot 执行计划 |
| `.agents/docs/` | 当前设计、验收边界和项目约束 |
| `.agents/knowledge/` | OKF 项目知识索引与跨系统导航 |

## 项目历史与协作

当前 `main` 的产品与运行权威是 Godot。旧 Unity 工程已经退役；永久 Tag `unity-final-2026-08-08`、Frozen Oracle、Golden 和迁移 receipt 只用于历史行为与来源审计，不参与当前运行时。

在修改项目之前请阅读 [AGENTS.md](AGENTS.md)。其中定义了唯一 Godot 项目、C# 分层、Resource 写入、Editor 生命周期、验证和 dirty worktree 保护规则。
