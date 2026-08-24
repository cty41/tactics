# Tactics

> [English](README.md) · [中文](README.zh-CN.md)


Tactics 是一个使用 Godot 4.7 C# 开发的回合制战棋 Roguelike。当前核心模式 **Pure Run** 围绕法师、死灵法师与亚马逊女战士组成的固定三人小队展开：玩家需要在七层路线中完成战斗、事件与服务节点，在单局内组合技能、属性、装备和消耗品，并把队伍带到最终 Boss。

> 项目状态：开发中原型。主要流程与自动化回归已经建立，但视觉、操作体验和正式发布仍需持续人工验收；当前构建不代表最终发行版本。

## 开源许可

- 源代码、文档与仓库工具按 [Apache License 2.0](LICENSE) 发布。
- `godot/assets/` 以及来源清单明确列出的项目自有图像按 [CC BY 4.0](ASSET_LICENSE.md) 发布，署名为 `cty41`。
- 项目名称、标识及发行品牌不因代码或资产许可而授予商标使用权，详见 [TRADEMARKS.md](TRADEMARKS.md)。
- 第三方组件及其许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。逐文件资产来源与哈希记录在 `Tools/public-release/asset-provenance.json`。

许可声明只覆盖仓库中明确纳入公开来源清单的内容，不覆盖私有历史归档中的 Unity 工程、候选美术、逆向分析材料或第三方参考载荷。

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
git clone git@github.com:cty41/wooftactics.git
Set-Location wooftactics
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

## LLM 辅助策划与开发

项目支持将自然语言策划需求整理为稳定 `gameplay-contract`，再通过 OpenCode Go 或本地 Ollama 提出带原文证据的合同与测试候选，并由严格 Schema、capability、compiler、typed authoring 和 Godot ResourceSaver 确定性把关。外接 LLM 是可替换、可批处理、可审计的候选生成层，不替代 Codex/开发者判断，也不能直接写 Resource、运行时代码或批准人工体验。

从需求收束、Provider 配置到 Scenario/Enemy Draft、Godot 写入、自动测试和人工验收的完整操作见 [LLM 辅助策划到 Godot 开发](.agents/docs/gameplay-design-to-development-workflow.md)。

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
| `Tools/gameplay-test-spec/` | 玩法合同、LLM 候选、Gameplay Spec 与确定性编译工具 |
| `Tests/gameplay-specs/` | 平台中立的 Gameplay Spec 与 Godot 执行计划 |
| `.agents/docs/` | 当前设计、验收边界和项目约束 |
| `.agents/knowledge/` | OKF 项目知识索引与跨系统导航 |
| `.agents/skills/` | 项目专属 Agent 技能；通用技能来自全局共享仓（见「共享 Agent 技能」） |

## 共享 Agent 技能

通用 Agent 工作流技能（`grill-me`/`grilling`、`brainstorming`、`make-dev-plan`、`plan-mode-plan-writer`、`project-doc-organization`、`skill-writing` 等）从公开仓 [`cty41/skills`](https://github.com/cty41/skills) 安装到用户级 `~/.agents/skills`（`git clone git@github.com:cty41/skills.git` 后运行该 checkout 的 `scripts/install-user.ps1`；macOS/Linux 在 pwsh 下运行同一脚本）。它们不随本仓库 vendored；更新方式为 `git -C <skills-checkout> pull` 后重跑安装脚本。

本仓库 `.agents/skills/` 只保留项目专属技能（`godot-*`、`gameplay-*`、`artworks-prompt-library`、`pure-run-artwork-pipeline`）与 `knowledge-maintenance`（完整 `Tools/okf`）、`manual-qa-handoff`（被 `Tools/agent-policy` 硬引用）两个有意特化；项目本地技能优先于全局安装。详细契约见 [AGENTS.md](AGENTS.md) 的「共享 Agent 技能」章节。

## 项目历史与协作

当前 `main` 的产品与运行权威是 Godot。旧 Unity 工程已经退役；永久 Tag `unity-final-2026-08-08`、Frozen Oracle、Golden 和迁移 receipt 只用于历史行为与来源审计，不参与当前运行时。

在修改项目之前请阅读 [AGENTS.md](AGENTS.md)。其中定义了唯一 Godot 项目、C# 分层、Resource 写入、Editor 生命周期、验证和 dirty worktree 保护规则。

欢迎提交 Issue 和 Pull Request。开发环境、验证要求、资产贡献条件和提交边界见 [CONTRIBUTING.md](CONTRIBUTING.md)；安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。
