# 资源加载规范

## 强制约束（必读）

1. **必须使用 `GameAssetManager`** 加载项目资源，严禁使用 `Resources.Load`
2. **Load/Release 必须配对**，每次成功的 `Load`/`LoadAsync` 必须有对应的 `Release`
3. **优先使用异步加载**（`LoadAsync`）
4. **场景路径**必须使用完整项目路径（`Assets/...`），不能使用场景名

### 禁忌模式

```csharp
// ❌ 禁止 — 违反 AssetBundle 管线
Resources.Load<Sprite>("path/to/sprite");

// ❌ 内存泄漏 — Bundle 一直未释放
var prefab = await mgr.LoadAsync<GameObject>(path);
Instantiate(prefab);
// 缺少 mgr.Release(path);

// ❌ 错误 — 与 manifest 查找不一致
mgr.LoadSceneAsync("MyLevel", LoadSceneMode.Single);
```

### 正确模式

```csharp
// ✅ 推荐：异步加载
var prefab = await GameAssetManager.Instance.LoadAsync<GameObject>("Assets/MyFolder/MyPrefab.prefab");

// ✅ Load 与 Release 必须配对
var prefab = await mgr.LoadAsync<GameObject>(path);
var instance = Instantiate(prefab);
mgr.Release(path);

// ✅ 使用项目路径，而不是场景名
await mgr.LoadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity", LoadSceneMode.Additive);
await mgr.UnloadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity");
```

---

## 参考：GameAssetManager API（按需查阅）

### 初始化

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

### 加载模式

| 模式 | 适用场景 | 需要 Manifest |
|------|----------|---------------|
| `StreamingBundles` | 正式构建 | 是 |
| `EditorAssetDatabase` | 仅 Editor 调试 | 否 |

`EditorAssetDatabase` 模式下，资源直接从项目加载，无 AssetBundle 开销。不要在构建中使用此模式。

## 参考：AssetScopeManager API

```csharp
// ✅ 进入场景时标记场景作用域
AssetScopeManager.BeginScene("Assets/Tactics/Scenes/MyLevel.unity");

// 用户代码无需调用 RegisterLoadedPath — GameAssetManager 内部自动处理
```

- **`BeginScene`** — 场景开始时调用，建立资源生命周期作用域
- **自动追踪** — `GameAssetManager.Load/LoadAsync` 自动向 `AssetScopeManager` 注册路径
- **自动创建实例** — `AssetScopeManager` 不存在时自动创建
- **延迟释放** — Bundle 释放推迟到帧末以确保安全

## 参考：路径辅助方法

使用 `SceneProjectPathHelper` 将场景名转为项目路径：

```csharp
string path = SceneProjectPathHelper.ToProjectPath("MyLevel");
// 返回: "Assets/Tactics/Scenes/MyLevel.unity"
```
