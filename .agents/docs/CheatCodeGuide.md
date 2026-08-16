# CheatCode 添加指南

本文说明如何为 Tactics 项目添加作弊码命令。

## 原理

作弊码系统由两个类组成：

1. **`CheatCommandManager`** (`Assets/Tactics/Scripts/Common/Cheats/CheatCommandManager.cs`)
   - 单例模式，管理所有作弊命令
   - 命令注册：`RegisterCommand(string name, Func<string[], string> handler)`
   - 命令执行：`Execute(string commandLine)` → 返回结果字符串（`[Error]` 前缀表示错误）

2. **`CheatConsoleUI`** (`Assets/Tactics/Scripts/UI/CheatConsoleUI.cs`)
   - UI Toolkit 控制器，处理用户输入和输出显示
   - 调用 `CheatCommandManager.Instance.Execute()` 执行命令

## 如何添加新命令

在 `CheatCommandManager.RegisterBuiltInCommands()` 方法中添加：

```csharp
RegisterCommand("命令名", args =>
{
    // args[0], args[1]... 获取参数

    // 业务逻辑

    return "成功消息";  // 或 "[Error] 错误消息"
});
```

## 命名规范

- 命令名全小写英文：`additem`, `clearitem`
- 错误消息以 `[Error]` 开头
- 成功消息简洁描述操作结果

## 数据访问

| 数据 | 访问方式 |
|------|----------|
| 玩家状态 | `PlayerAdventureStateStore.LoadRepairAndSave()` |
| 保存状态 | `PlayerAdventureStateStore.Save(state)` |
| 背包道具 | `state.Inventory` (`List<string>`) |
| 角色装备 | `character.Equipment` (`Dictionary<EquipmentSlot, string>`) |
| 装备定义 | `EquipmentDatabase.GetById(id)` / `EquipmentDatabase.Contains(id)` |
| 角色列表 | `state.Roster` (`List<CharacterDefinition>`) |

## 现有命令

| 命令 | 功能 | 示例 |
|------|------|------|
| `additem` | 添加装备到背包 | `additem IronSword` |
| `clearitem` | 清除背包中所有未装备的道具 | `clearitem` |
