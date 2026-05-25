---
name: unity-asset-workflow
description: "Use when loading or releasing project assets at runtime via GameAssetManager, managing asset lifecycle scopes, converting scene names to project paths, or troubleshooting asset loading patterns"
---

# unity-asset-workflow

> **约束部分**请查阅 `rules/unity-asset-loading.md`。本文为 API 操作指南。

## Quick Reference

| 操作 | API |
|------|-----|
| 加载资源 | `GameAssetManager.Instance.LoadAsync<T>(path)` |
| 释放资源 | `GameAssetManager.Instance.Release(path)` |
| 初始化 Manager | `await GameAssetManager.Instance.InitializeAsync()` |
| 标记场景作用域 | `AssetScopeManager.BeginScene(scenePath)` |
| 场景名转路径 | `SceneProjectPathHelper.ToProjectPath("LevelName")` |

---

## When to use

- 运行时代码需要加载、释放或实例化 Unity 资源时
- 需要把场景名转换成 `Assets/.../*.unity` 项目路径时
- 需要管理场景级资源生命周期或排查 Bundle 未释放问题时
- 需要确认代码没有使用被禁止的 `Resources.Load` 时

## Workflow

## Workflow: GameAssetManager 初始化

```csharp
var mgr = GameAssetManager.Instance;
if (mgr == null || !mgr.IsInitialized)
{
    // 处理错误：Manager 不可用
}
// 或按需初始化
if (!mgr.IsInitialized)
    await mgr.InitializeAsync();
```

### 初始化时机

- **自动初始化**：`GameAssetManager` 作为场景单例，通常在首个场景加载时自动完成
- **手动初始化**：如果需要在 Manager 就绪前加载资源，调用 `InitializeAsync()`
- **检查状态**：始终通过 `IsInitialized` 确认 Manager 可用后再调用 Load

---

## 加载与释放

### 基本加载

```csharp
// 异步加载（推荐）
var prefab = await GameAssetManager.Instance.LoadAsync<GameObject>("Assets/MyFolder/MyPrefab.prefab");

// Load 与 Release 必须配对
var prefab = await mgr.LoadAsync<GameObject>(path);
var instance = Instantiate(prefab);
mgr.Release(path);
```

### 加载模式

| 模式 | 适用场景 | 需要 Manifest |
|------|----------|---------------|
| `StreamingBundles` | 正式构建 | 是 |
| `EditorAssetDatabase` | 仅 Editor 调试 | 否 |

`EditorAssetDatabase` 模式下，资源直接从项目加载，无 AssetBundle 开销。**不要在构建中使用此模式。**

---

## 场景加载

```csharp
// 使用项目路径，而不是场景名
await mgr.LoadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity", LoadSceneMode.Additive);
await mgr.UnloadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity");
```

### 场景路径规则

| ✅ 正确 | ❌ 错误 |
|---------|--------|
| `"Assets/Tactics/Scenes/MyLevel.unity"` | `"MyLevel"` |

必须使用完整的项目路径（`Assets/...`），不能使用场景名。原因是 manifest 按项目路径索引，场景名无法正确匹配。

---

## AssetScopeManager 场景作用域

```csharp
// 进入场景时标记场景作用域
AssetScopeManager.BeginScene("Assets/Tactics/Scenes/MyLevel.unity");

// 用户代码无需调用 RegisterLoadedPath — GameAssetManager 内部自动处理
```

| 特性 | 说明 |
|------|------|
| **`BeginScene`** | 场景开始时调用，建立资源生命周期作用域 |
| **自动追踪** | `GameAssetManager.Load/LoadAsync` 自动向 `AssetScopeManager` 注册路径 |
| **自动创建实例** | `AssetScopeManager` 不存在时自动创建 |
| **延迟释放** | Bundle 释放推迟到帧末以确保安全 |

---

## 路径转换

使用 `SceneProjectPathHelper` 将场景名转为项目路径：

```csharp
string path = SceneProjectPathHelper.ToProjectPath("MyLevel");
// 返回: "Assets/Tactics/Scenes/MyLevel.unity"
```

适用于将用户友好的场景名转换为 manifest 可识别的完整路径的场景。

---

## Reference Files

- `Assets/Tactics/Scripts/AssetPipeline/Runtime/GameAssetManager.cs` — Manager 实现
- `Assets/Tactics/Scripts/AssetPipeline/Runtime/GameAssetPaths.cs` — 路径常量
- `Assets/Tactics/Scripts/AssetPipeline/Runtime/AssetScopeManager.cs` — 作用域管理

## Anti-patterns

| Wrong | Correct | Why |
|-------|---------|-----|
| `Resources.Load(...)` | `GameAssetManager.Instance.LoadAsync<T>(path)` | 项目资源必须走 AssetBundle 管线 |
| 加载后不释放 | `Load/LoadAsync` 与 `Release` 配对 | 避免 Bundle 生命周期泄漏 |
| 用场景名加载 | 使用 `Assets/.../*.unity` 路径 | Manifest 按项目路径索引 |
| 在构建中使用 `EditorAssetDatabase` 模式 | 使用 `StreamingBundles` | Editor 模式仅用于调试 |

## Checklist

- [ ] 所有资源路径都是 `Assets/...` 项目路径
- [ ] 优先使用 `LoadAsync`
- [ ] 每次成功加载都有对应 `Release`
- [ ] 场景加载使用完整 `.unity` 路径
- [ ] 没有新增 `Resources.Load`
