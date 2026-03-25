---
description: "Asset loading rules for GameAssetManager and AssetScopeManager"
globs: ["**/*.cs"]
alwaysApply: false
---

# Asset Loading Rules

## Core Principles

1. **Always use `GameAssetManager`** for loading project assets - never use `Resources.Load`
2. **Load/Release must be paired** - every successful `Load`/`LoadAsync` must have a corresponding `Release`
3. **Use async loading** (`LoadAsync`) as the preferred pattern
4. **Scene paths** must use full project paths (`Assets/...`) not scene names

## GameAssetManager API

### Loading Assets

```csharp
// ✅ Preferred: Async loading
var prefab = await GameAssetManager.Instance.LoadAsync<GameObject>("Assets/MyFolder/MyPrefab.prefab");

// ⚠️ Sync loading only for specific cases
var sprite = GameAssetManager.Instance.Load<Sprite>("Assets/UI/Sprites/icon.png");
```

### Releasing Assets

```csharp
// ✅ Always pair Load with Release
var prefab = await mgr.LoadAsync<GameObject>(path);
Instantiate(prefab);
mgr.Release(path);
```

### Loading Scenes

```csharp
// ✅ Use project paths, not scene names
await mgr.LoadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity", LoadSceneMode.Additive);

// ✅ Release when done
await mgr.UnloadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity");
```

### Initialization

```csharp
// Check if initialized before loading
var mgr = GameAssetManager.Instance;
if (mgr == null || !mgr.IsInitialized)
{
    // Handle error: Manager not available
}

// Or initialize if needed
if (!mgr.IsInitialized)
    await mgr.InitializeAsync();
```

## AssetScopeManager API

### Scene Boundaries

```csharp
// ✅ Mark scene scope when entering a scene
AssetScopeManager.BeginScene("Assets/Tactics/Scenes/MyLevel.unity");

// User code does NOT call RegisterLoadedPath - GameAssetManager calls it internally
```

### Key Points

- **`BeginScene`** - Call when a scene starts to establish asset lifetime scope
- **Auto-tracking** - `GameAssetManager.Load/LoadAsync` automatically registers paths with `AssetScopeManager`
- **Automatic instance** - `AssetScopeManager` auto-creates if not present
- **Deferred release** - Bundle releases are deferred to end of frame for safety

## Forbidden Patterns

### ❌ DO NOT Use Resources.Load

```csharp
// ❌ Forbidden - violates AssetBundle pipeline
Resources.Load<Sprite>("path/to/sprite");

// ✅ Correct approach
await GameAssetManager.Instance.LoadAsync<Sprite>("Assets/path/to/sprite.png");
```

### ❌ DO NOT Skip Release

```csharp
// ❌ Memory leak - bundle stays loaded
var prefab = await mgr.LoadAsync<GameObject>(path);
Instantiate(prefab);

// ✅ Correct - release after use
var prefab = await mgr.LoadAsync<GameObject>(path);
var instance = Instantiate(prefab);
mgr.Release(path);
```

### ❌ DO NOT Use Scene Names for Loading

```csharp
// ❌ Wrong - inconsistent with manifest lookup
mgr.LoadSceneAsync("MyLevel", LoadSceneMode.Single);

// ✅ Correct - use project path
mgr.LoadSceneAsync("Assets/Tactics/Scenes/MyLevel.unity", LoadSceneMode.Single);
```

## Reference Implementation

See [`UIManager.cs`](../../Assets/Tactics/Runtime/UIManager.cs) for correct usage pattern:

```csharp
private async Task<GameObject> LoadAndCreateUiInstanceAsync(UIId id, string prefabPath)
{
    var mgr = GameAssetManager.Instance;
    if (mgr == null)
        throw new InvalidOperationException("[UIManager] GameAssetManager.Instance is null.");
    if (!mgr.IsInitialized)
        throw new InvalidOperationException("[UIManager] GameAssetManager is not initialized.");

    var prefab = await mgr.LoadAsync<GameObject>(prefabPath);
    if (prefab == null)
        throw new InvalidOperationException($"[UIManager] Failed to load prefab: {prefabPath}");

    var go = Instantiate(prefab, _uiRoot, false);
    return go;
}
```

## Path Helper

Use [`SceneProjectPathHelper`](../../Assets/Tactics/AssetPipeline/Runtime/SceneProjectPathHelper.cs) for convenience:

```csharp
// Convert scene name to project path
string path = SceneProjectPathHelper.ToProjectPath("MyLevel");
// Returns: "Assets/Tactics/Scenes/MyLevel.unity"
```

## Loading Modes

| Mode | Use Case | Manifest Required |
|------|----------|-------------------|
| `StreamingBundles` | Production builds | Yes |
| `EditorAssetDatabase` | Editor debugging only | No |

In `EditorAssetDatabase` mode, resources load directly from the project without AssetBundle overhead. Do not use this mode in builds.
