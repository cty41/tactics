---
name: unity-menu-organizer
description: Use when organizing Unity MenuItem attributes, checking menu structure compliance, or migrating menu items to the Tactics root menu. Triggers: "organize menu", "check menu items", "menu structure", "MenuItem", "ContextMenu"
---

# Unity MenuItem 归组规范

## Quick Reference

**菜单路径标准**：`Tactics/<分类>/<功能>`

**分类检查清单**：
| 步骤 | 操作 |
|------|------|
| 1. 扫描 | `grep -rn '\[MenuItem\s*\(' --include="*.cs" Assets/` |
| 2. 分类 | 自有代码 → 迁移到 `Tactics/`，ThirdParty → 检查重复 |
| 3. 迁移 | 修改 MenuItem 路径为 `Tactics/<Category>/<Function>` |
| 4. 冲突处理 | 注释 ThirdParty 版重复的 `[MenuItem]` 行 |

**常见分类**：Role System, Roguelike, Party, Tilemap, Ability System, Scene

确保项目中所有 C# `[MenuItem]` 和 `[ContextMenu]` 统一归入 `Tactics/` 根菜单，遵循 `Tactics/<分类>/<功能>` 结构。

## When to use

- 用户要求 "organize menu"、"check menu items"、"menu structure"
- 新增 Editor 工具需要确定 MenuItem 路径
- 怀疑菜单结构不规范需要检查
- 添加了 ThirdParty 代码需要确认菜单不冲突

## Workflow

### Step 1: 扫描

扫描项目中所有 `[MenuItem]` 和 `[ContextMenu]` 定义：

```bash
# grep MenuItem 定义
grep -rn '\[MenuItem\s*\(' --include="*.cs" Assets/
grep -rn '\[ContextMenu\s*\(' --include="*.cs" Assets/
```

### Step 2: 分类

按归属分类：

| 分类 | 判断标准 | 处理方式 |
|------|---------|---------|
| ✅ 已规范 | 路径以 `Tactics/` 开头 | 无需修改 |
| ❌ 不规范（自有） | 在 `Assets/Tactics/` 下但路径不以 `Tactics/` 开头 | 迁移（Step 3） |
| ⚠️ ThirdParty | 在 `Assets/ThirdParty/` 下 | 检查是否与自有代码重复 |

### Step 3: 迁移

按以下映射表修改 MenuItem 路径：

| 文件 | 当前路径 | 目标路径 |
|------|---------|---------|
| `RoleSystemSetupEditor.cs` | `Tools/Tactics/Setup Role System` | `Tactics/Role System/Setup Role System` |
| `RoleSystemSetupEditor.cs` | `Tools/Tactics/Setup Test1 Scene` | `Tactics/Role System/Setup Test1 Scene` |
| `RoguelikeEventEditorWindow.cs` | `Tools/Tactics/Event Editor` | `Tactics/Roguelike/Event Editor` |
| `PartyBootstrapSetupEditor.cs` | `Tools/Tactics/Setup Party Bootstrap` | `Tactics/Party/Setup Party Bootstrap` |
| `HighlightLayerTools.cs` (Tactics) | `Tools/Tactics/Tilemap/Clear HighlightLayer Tiles` | `Tactics/Tilemap/Clear HighlightLayer Tiles` |
| `HighlightLayerTools.cs` (Tactics) | `Tools/Tactics/Tilemap/Clear Selected Tilemap Tiles` | `Tactics/Tilemap/Clear Selected Tilemap Tiles` |
| `CreateDefaultAbilityConfigs.cs` | `Tools/Ability System/Create Default Ability Configs` | `Tactics/Ability System/Create Default Ability Configs` |
| `AbilityConfigSetup.cs` | `Tools/Ability System/Setup Unit Abilities` | `Tactics/Ability System/Setup Unit Abilities` |
| `AbilityConfigMigrationTool.cs` | `Tools/Ability System/Migrate to AbilityConfig` | `Tactics/Ability System/Migrate to AbilityConfig` |
| `AbilityMigrationTool.cs` | `Tools/Ability Migration/Cleanup Orphaned Components` | `Tactics/Ability System/Cleanup Orphaned Components` |
| `GridHelper.cs` (Tactics) | `Window/Grid Helper` | `Tactics/Scene/Grid Helper` |

### Step 4: ThirdParty 重复处理

如果 ThirdParty 文件中的 MenuItem 与 Tactics 自有文件重复，**注释** ThirdParty 版的 `[MenuItem]` 行：

```
// [MenuItem("Window/Grid Helper")]  ← 注释掉，Tactics 版已接管
```

涉及文件：
- `Assets/ThirdParty/TBSFramework/Editor/GridHelper.cs`
- `Assets/ThirdParty/TBSFramework/Editor/HighlightLayerTools.cs`

### Step 5: ContextMenu 处理

右键菜单使用 `CONTEXT/` 前缀。Tactics fork 版需与原始版区分：

```
CONTEXT/RuleTile/Tactics Fork/Copy All Rules   ← Tactics 版
CONTEXT/RuleTile/Tactics Fork/Paste Rules      ← Tactics 版
CONTEXT/RuleTile/Copy All Rules                ← ThirdParty 原始版（不修改）
CONTEXT/RuleTile/Paste Rules                   ← ThirdParty 原始版（不修改）
```

### Step 6: 验证

1. **编译**：修改任何 `.cs` 文件后，调用 `refresh_unity(compile="request")`
2. **检查控制台**：`read_console` 确认无编译错误
3. **二次扫描**：重新 grep 确认无遗漏的旧路径

## 目标菜单结构

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
├── Party/
│   └── Setup Party Bootstrap
├── Roguelike/
│   └── Event Editor
├── Role System/
│   ├── Setup Role System
│   └── Setup Test1 Scene
├── Scene/
│   └── Grid Helper
└── Tilemap/
    ├── Clear HighlightLayer Tiles
    └── Clear Selected Tilemap Tiles
```

## 新增 MenuItem 规范

添加新 Editor 工具时，Menu path 必须遵循：

1. **根路径**：必须以 `Tactics/` 开头
2. **分类**：选择合适的子菜单（如已存在则复用，否则新建）
3. **命名**：使用英文 PascalCase / Title Case
4. **优先级**：考虑使用 `priority` 参数控制排序（可选）

示例：
```csharp
// ✅ 正确
[MenuItem("Tactics/Ability System/Create My Ability")]
// ❌ 错误
[MenuItem("Tools/My Tool")]
[MenuItem("Window/My Window")]
```

## 常见错误

- 使用 `Tools/` 或 `Window/` 而非 `Tactics/`
- 路径层级过深（建议不超过 3 层：`Tactics/Category/Item`）
- ThirdParty 代码的 MenuItem 未检查是否与自有代码重复
- 修改 MenuItem 后忘记调用 `refresh_unity` 编译
