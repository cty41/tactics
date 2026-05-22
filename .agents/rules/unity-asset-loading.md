# 资源加载规范

> **API 参考**：`skills/unity-asset-workflow/SKILL.md`
> 本文仅保留强制约束，详细 API 请查阅对应 skill。

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
