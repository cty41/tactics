# Unity MenuItem 归组 Skill 开发计划

> 创建日期：2026-05-13
> 状态：待执行

## Background

### 当前问题

项目中存在 **14 个 MenuItem** 分布在不同的菜单根路径下，缺乏统一规范：

| 当前根路径 | 数量 | 问题 |
|-----------|------|------|
| `Tools/Tactics/` | 5 | 冗余 `Tools` 前缀，应与 `Tactics/` 统一 |
| `Tactics/` | 4 | ✅ 已规范 |
| `Tools/Ability System/` | 3 | 游离在 Tactics 之外 |
| `Tools/Ability Migration/` | 1 | 游离在 Tactics 之外 |
| `Window/Grid Helper` | 1 | 不在 Tactics 下 |

此外还存在 2 对 **重复文件**（`GridHelper.cs`、`HighlightLayerTools.cs` 在 `Tactics/` 和 `ThirdParty/TBSFramework/` 各有一份），会造成 Unity 菜单中显示重复项。

### 目标

1. 创建一个 **Skill**（`.agents/skills/unity-menu-organizer/SKILL.md`）来规范化和自动化 MenuItem 管理
2. 将所有项目自有 MenuItem 统一到 `Tactics/<分类>/<功能>` 结构下
3. 移除 ThirdParty 重复文件的 MenuItem（注释方式）

### 预期收益

- 菜单结构清晰可预测
- Agent 和开发者都能快速找到工具
- 新工具加入时有明确的归属规范
- Skill 可重复运行进行合规检查

---

## Scope

### In Scope

- 创建 `.agents/skills/unity-menu-organizer/SKILL.md` 技能定义文件
- 迁移 **10 个不规范的 MenuItem** 到 `Tactics/` 根目录下
- 注释 **2 个 ThirdParty 重复文件**（`GridHelper.cs`、`HighlightLayerTools.cs`）的 `[MenuItem]` 行
- 扫描项目中是否有通过字符串引用旧菜单路径的代码
- 编译验证（`refresh_unity`）
- ContextMenu 统一：`CONTEXT/RuleTile/Tactics Fork/` → `CONTEXT/RuleTile/`

### Out of Scope

- 不修改 `OneLine`、`UIExtensions`、`TBSFramework`（除注释重复 MenuItem 外）的菜单结构
- 不修改 `Assets > Create` 菜单（CreateAssetMenu）
- 不删除 ThirdParty 文件（仅注释 MenuItem）
- 不创建 Unity Editor 自动化脚本（EditorCoroutine 等），仅通过 MCP 工具操作

---

## Tasks

### Task 1: 创建 Skill 定义文件

- **目标**：创建 `.agents/skills/unity-menu-organizer/SKILL.md`
- **输入**：现有 skill 格式规范（参考 `unity-git-commit/SKILL.md`）
- **输出**：完整的 Skill 定义文件，包含：
  - 触发条件（手动 + 关键词：`organize menu`, `check menu items`, `menu structure`）
  - 扫描流程（grep `[MenuItem(` 和 `[ContextMenu(`）
  - 分类逻辑（自有/ThirdParty、已规范/不规范）
  - 迁移规则（菜单路径映射表）
  - 验证步骤（`refresh_unity` + 二次扫描确认）
- **验收标准**：
  - 文件格式符合项目 skill 规范（YAML frontmatter + Markdown body）
  - 包含完整的扫描 → 分类 → 迁移 → 验证 工作流
  - 描述清晰，Agent 可据此独立执行

### Task 2: 扫描旧菜单路径引用

- **目标**：确认是否有代码通过字符串引用旧菜单路径
- **输入**：项目全部 `.cs` 文件
- **输出**：引用清单（有/无）
- **验收标准**：
  - 搜索 `ExecuteMenuItem`、`EditorApplication.ExecuteMenuItem`、菜单路径字符串
  - 如无引用则跳过，如有则记录在 Task 3 中一并处理

### Task 3: 迁移项目自有 MenuItem（10 项）

- **目标**：修改 6 个文件中的 MenuItem 路径
- **输入**：当前菜单路径 → 目标路径映射表
- **输出**：修改后的文件，所有菜单项归入 `Tactics/` 下

| 文件 | 当前路径 | 目标路径 |
|------|---------|---------|
| `RoleSystemSetupEditor.cs` | `Tools/Tactics/Setup Role System` | `Tactics/Role System/Setup Role System` |
| `RoleSystemSetupEditor.cs` | `Tools/Tactics/Setup Test1 Scene` | `Tactics/Role System/Setup Test1 Scene` |
| `RoguelikeEventEditorWindow.cs` | `Tools/Tactics/Event Editor` | `Tactics/Roguelike/Event Editor` |
| `PartyBootstrapSetupEditor.cs` | `Tools/Tactics/Setup Party Bootstrap` | `Tactics/Party/Setup Party Bootstrap` |
| `HighlightLayerTools.cs` | `Tools/Tactics/Tilemap/Clear HighlightLayer Tiles` | `Tactics/Tilemap/Clear HighlightLayer Tiles` |
| `HighlightLayerTools.cs` | `Tools/Tactics/Tilemap/Clear Selected Tilemap Tiles` | `Tactics/Tilemap/Clear Selected Tilemap Tiles` |
| `CreateDefaultAbilityConfigs.cs` | `Tools/Ability System/Create Default Ability Configs` | `Tactics/Ability System/Create Default Ability Configs` |
| `AbilityConfigSetup.cs` | `Tools/Ability System/Setup Unit Abilities` | `Tactics/Ability System/Setup Unit Abilities` |
| `AbilityConfigMigrationTool.cs` | `Tools/Ability System/Migrate to AbilityConfig` | `Tactics/Ability System/Migrate to AbilityConfig` |
| `AbilityMigrationTool.cs` | `Tools/Ability Migration/Cleanup Orphaned Components` | `Tactics/Ability System/Cleanup Orphaned Components` |

- **验收标准**：
  - 每个 MenuItem 路径以 `Tactics/` 开头
  - 分类合理：Role System、Roguelike、Party、Tilemap、Ability System
  - `DamageNumberConfigGenerator.cs` (`Tactics/Generate Damage Number Settings`) 保持不变但考虑归入 `Tactics/Damage Number/` 子菜单

### Task 4: 统一 GridHelper 菜单路径

- **目标**：将 `Window/Grid Helper` 迁移到 `Tactics/Scene/Grid Helper`
- **输入**：`Assets/Tactics/Scripts/Common/Editor/GridHelper.cs`
- **输出**：Menu path 改为 `Tactics/Scene/Grid Helper`
- **验收标准**：
  - MenuItem 路径为 `Tactics/Scene/Grid Helper`
  - 同时注释 `ThirdParty/TBSFramework/Editor/GridHelper.cs` 的 MenuItem 行（见 Task 5）

### Task 5: 移除 ThirdParty 重复文件的 MenuItem

- **目标**：注释 2 个 ThirdParty 文件中与 Tactics 重复的 MenuItem
- **输入**：
  - `Assets/ThirdParty/TBSFramework/Editor/GridHelper.cs` — 第 58 行
  - `Assets/ThirdParty/TBSFramework/Editor/HighlightLayerTools.cs` — 第 10、28、49 行
- **输出**：`[MenuItem(...)]` 行改为 `// [MenuItem(...)]`（注释保留原内容）
- **验收标准**：
  - ThirdParty 文件中的 MenuItem 行已注释
  - 项目 Tactics 版菜单项正常保留
  - Unity 菜单中不再出现重复项

### Task 6: 统一 ContextMenu（右键菜单）

- **目标**：将 Tactics fork 的 RuleTile 右键菜单与 ThirdParty 版差异化
- **输入**：`Assets/Tactics/Scripts/Common/RuleTiles/Editor/RuleTileEditor.cs`
- **当前**：`CONTEXT/RuleTile/Tactics Fork/Copy All Rules`
- **目标**：保持 `Tactics Fork` 标识，确保不与 ThirdParty 版混淆（无需修改，已区分）
- **输出**：确认无需修改，或简化路径
- **验收标准**：
  - Tactics fork 版右键菜单可用
  - 不与 ThirdParty 版冲突

### Task 7: 编译与验证

- **目标**：确认所有修改编译通过，菜单结构正确
- **输入**：所有已修改的 `.cs` 文件
- **输出**：
  - `refresh_unity(compile="request")` — 编译成功
  - `read_console` — 无编译错误
  - 二次 grep 扫描 — 确认不再有 `Tools/Tactics/`、`Tools/Ability`、`Window/Grid` 等旧路径
- **验收标准**：
  - 编译 0 错误
  - 所有 MenuItem 路径以 `Tactics/` 开头（ThirdParty 注释掉的除外）
  - 菜单层级结构：`Tactics/Asset Pipeline/`、`Tactics/Ability System/`、`Tactics/Tilemap/`、`Tactics/Role System/` 等

---

## 最终菜单结构（目标状态）

```
Tactics/
├── Ability System/
│   ├── Create Default Ability Configs
│   ├── Setup Unit Abilities
│   ├── Migrate to AbilityConfig
│   └── Cleanup Orphaned Components
├── Asset Pipeline/
│   ├── Asset Pipeline Window
│   ├── Build Game Asset Bundles
│   ├── Clear And Build Game Asset Bundles
│   └── Setup Sample (Prefab + Build Config)
├── Generate Damage Number Settings
├── Scene/
│   └── Grid Helper
├── Party/
│   └── Setup Party Bootstrap
├── Roguelike/
│   └── Event Editor
├── Role System/
│   ├── Setup Role System
│   └── Setup Test1 Scene
└── Tilemap/
    ├── Clear HighlightLayer Tiles
    └── Clear Selected Tilemap Tiles
```

---

## Assumptions

1. 项目中无其他代码通过字符串引用旧菜单路径（将在 Task 2 验证）
2. ThirdParty 文件的注释修改不会被 Unity Package Manager 或后续升级覆盖（TBSFramework 为本地拷贝，非 UPM 包）
3. `DamageNumberConfigGenerator` 的 `Tactics/Generate Damage Number Settings` 直接放在 `Tactics/` 根下可接受（或可按需移入 `Tactics/Damage Number/` 子菜单）
4. 修改 MenuItem 路径不影响功能逻辑，仅影响菜单显示位置

## Risks & Open Questions

- **Risk**: ThirdParty 文件更新后注释可能被恢复 → **Mitigation**: Skill 包含重新检查步骤
- ~~**Question**: `GridHelper` 路径~~ → ✅ 已确认：`Tactics/Scene/Grid Helper`
- ~~**Question**: `DamageNumberConfigGenerator` 子菜单~~ → ✅ 已确认：保持 `Tactics/Generate Damage Number Settings`
