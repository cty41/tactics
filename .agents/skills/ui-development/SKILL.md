---
name: ui-development
description: "Use when creating new UI panels, adding UIDocument components, or working with UXML/USS files — ensures all UI goes through UIManager for proper PanelSettings"
---

# UI Development

所有 UI 面板必须通过 UIManager 注册和创建，确保 PanelSettings 正确配置。

## Quick Reference

| 操作 | API |
|------|-----|
| 显示 UI | `await UIManager.Instance.ShowAsync(UIManager.UIId.MyPanel)` |
| 获取根元素 | `Ui.GetRootElement(UIManager.UIId.MyPanel)` |
| 隐藏 UI | `UIManager.Instance.Hide(UIManager.UIId.MyPanel)` |
| 注册 UIId | `UIManager.UIId` 枚举 + `ui_config.json` |

## When to use

- 创建新的 UI 面板（UXML/USS）
- 修改现有的 UI 显示逻辑
- 添加新的 UIId 到 UIManager
- 任何涉及 `UIDocument`、`VisualTreeAsset` 的任务

## Workflow

### Step 1: 创建 UXML/USS 文件

**位置**: `Assets/Tactics/Arts/UI/`

```xml
<!-- MyPanel.uxml -->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="MyPanel" class="panel-root">
        <ui:Label name="Title" text="标题"/>
        <ui:Button name="CloseButton" text="关闭"/>
    </ui:VisualElement>
</ui:UXML>
```

### Step 2: 注册 UIId

**文件**: `Assets/Tactics/Scripts/Common/UIManager.cs`

```csharp
public enum UIId
{
    // ... 现有值 ...
    MyPanel,  // 新增
}
```

### Step 3: 配置 ui_config.json

**文件**: `Assets/Tactics/GameData/ui_config.json`

```json
{
  "id": "MyPanel",
  "type": "UiToolkitUxml",
  "uxml": "Assets/Tactics/Arts/UI/MyPanel.uxml",
  "uss": "Assets/Tactics/Arts/UI/MyPanel.uss"
}
```

### Step 4: 在代码中显示 UI

```csharp
// 显示 UI
await UIManager.Instance.ShowAsync(UIManager.UIId.MyPanel);

// 获取根元素并操作
var root = Ui.GetRootElement(UIManager.UIId.MyPanel);
var title = root.Q<Label>("Title");
if (title != null)
    title.text = "动态标题";
```

### Step 5: 隐藏 UI

```csharp
UIManager.Instance.Hide(UIManager.UIId.MyPanel);
```

## Anti-patterns

| ❌ 错误 | ✅ 正确 | 原因 |
|---------|---------|------|
| `gameObject.AddComponent<UIDocument>()` | `UIManager.ShowAsync(UIId)` | 没有 PanelSettings，UI 不渲染 |
| `[SerializeField] VisualTreeAsset` | `Ui.GetRootElement(UIId)` | 无法保证 PanelSettings |
| `GetComponent<UIDocument>()` | `Ui.GetRootElement(UIId)` | 可能获取到未初始化的 UIDocument |
| 直接操作 `rootVisualElement` | 通过 `Ui.GetRootElement()` 获取 | 无法保证根元素已连接到 PanelSettings |

## Checklist

新增 UI 面板前检查：

- [ ] UXML 文件放在 `Assets/Tactics/Arts/UI/`
- [ ] UIId 已添加到 `UIManager.UIId` 枚举
- [ ] `ui_config.json` 已添加条目
- [ ] 代码中使用 `UIManager.Instance.ShowAsync(UIId)`
- [ ] 代码中使用 `Ui.GetRootElement(UIId)` 获取根元素
- [ ] 没有使用 `AddComponent<UIDocument>()`
- [ ] 没有使用 `[SerializeField] VisualTreeAsset`
