---
description: "Asset loading rules for GameAssetManager and AssetScopeManager"
globs: ["**/*.cs"]
alwaysApply: true
---

# Asset Loading Rules

## 强制约束（必读）

1. **Always use `GameAssetManager`** for loading project assets — never use `Resources.Load`
2. **Load/Release must be paired** — every successful `Load`/`LoadAsync` must have a corresponding `Release`
3. **Use async loading** (`LoadAsync`) as the preferred pattern
4. **Scene paths** must use full project paths (`Assets/...`) not scene names

### 禁忌模式

```csharp
// ❌ Forbidden — violates AssetBundle pipeline
Resources.Load<Sprite>("path/to/sprite");

// ❌ Memory leak — bundle stays loaded
var prefab = await mgr.LoadAsync<GameObject>(path);
Instantiate(prefab);
// Missing mgr.Release(path);

// ❌ Wrong — inconsistent with manifest lookup
mgr.LoadSceneAsync("MyLevel", LoadSceneMode.Single);
```

### 正确模式

```csharp
// ✅ Preferred: Async loading
var prefab = await GameAssetManager.Instance.LoadAsync<GameObject>("Assets/MyFolder/MyPrefab.prefab");

// ✅ Always pair Load with Release
var prefab = await mgr.LoadAsync<GameObject>(path);
var instance = Instantiate(prefab);
mgr.Release(path);

// ✅ Use project paths, not scene names
await mgr.LoadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity", LoadSceneMode.Additive);
await mgr.UnloadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity");
```

---

## 参考：GameAssetManager API（按需查阅）

### Initialization

```csharp
var mgr = GameAssetManager.Instance;
if (mgr == null || !mgr.IsInitialized)
{
    // Handle error: Manager not available
}
// Or initialize if needed
if (!mgr.IsInitialized)
    await mgr.InitializeAsync();
```

### Loading Modes

| Mode | Use Case | Manifest Required |
|------|----------|-------------------|
| `StreamingBundles` | Production builds | Yes |
| `EditorAssetDatabase` | Editor debugging only | No |

In `EditorAssetDatabase` mode, resources load directly from the project without AssetBundle overhead. Do not use this mode in builds.

## 参考：AssetScopeManager API

```csharp
// ✅ Mark scene scope when entering a scene
AssetScopeManager.BeginScene("Assets/Tactics/Scenes/MyLevel.unity");

// User code does NOT call RegisterLoadedPath — GameAssetManager calls it internally
```

- **`BeginScene`** — Call when a scene starts to establish asset lifetime scope
- **Auto-tracking** — `GameAssetManager.Load/LoadAsync` automatically registers paths with `AssetScopeManager`
- **Automatic instance** — `AssetScopeManager` auto-creates if not present
- **Deferred release** — Bundle releases are deferred to end of frame for safety

## 参考：Path Helper

Use `SceneProjectPathHelper` to convert scene names to project paths:

```csharp
string path = SceneProjectPathHelper.ToProjectPath("MyLevel");
// Returns: "Assets/Tactics/Scenes/MyLevel.unity"
```
