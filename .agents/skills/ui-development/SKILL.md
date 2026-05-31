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
| 引用样式 | UXML 顶部 `<Style src="project://database/...MyPanel.uss?..."/>` |

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
    <Style src="project://database/Assets/Tactics/Arts/UI/MyPanel.uss?fileID=7433441132597879392&amp;guid=<MyPanel.uss.meta guid>&amp;type=3#MyPanel"/>
    <ui:VisualElement name="MyPanel" class="panel-root">
        <ui:Label name="Title" text="标题"/>
        <ui:Button name="CloseButton" text="关闭"/>
    </ui:VisualElement>
</ui:UXML>
```

**必须**在 UXML 顶部显式引用对应 USS。`ui_config.json` 的 `uss` 字段用于 `UIManager` 运行时加载，但不能替代 UXML 内的 `<Style>` 引用；否则 UI Builder 预览、部分编辑器检查或其他加载路径可能丢失样式。

新增 `<Style>` 时读取同名 `.uss.meta` 的真实 `guid`，不要手写占位值。已有 UI 文件通常使用：

```xml
<Style src="project://database/Assets/Tactics/Arts/UI/MyPanel.uss?fileID=7433441132597879392&amp;guid=<真实GUID>&amp;type=3#MyPanel"/>
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
| 只在 `ui_config.json` 配 USS | UXML 顶部也写 `<Style>` | UI Builder/编辑器预览等路径不会读取 `ui_config.json` |

## Checklist

新增 UI 面板前检查：

- [ ] UXML 文件放在 `Assets/Tactics/Arts/UI/`
- [ ] UXML 顶部已引用对应 USS，并使用 `.uss.meta` 中的真实 GUID
- [ ] UIId 已添加到 `UIManager.UIId` 枚举
- [ ] `ui_config.json` 已添加条目
- [ ] 代码中使用 `UIManager.Instance.ShowAsync(UIId)`
- [ ] 代码中使用 `Ui.GetRootElement(UIId)` 获取根元素
- [ ] 没有使用 `AddComponent<UIDocument>()`
- [ ] 没有使用 `[SerializeField] VisualTreeAsset`
