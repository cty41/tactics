# Tactics 架构

## 项目结构

```
Assets/
├── Tactics/
│   ├── AssetPipeline/       # AssetBundle 构建与运行时加载
│   ├── Runtime/            # 核心游戏运行时
│   │   ├── UI/            # UI 系统
│   │   └── ...           
│   ├── Scripts/           # 游戏逻辑
│   └── Scenes/            # Unity 场景
└── StreamingAssets/      # 构建完成的 AssetBundle
```

## 资源管线

完整文档见 `rules/game-asset-pipeline.md`。

### 核心组件

| 组件 | 用途 |
|------|------|
| `GameAssetManager` | 场景单例，负责资源加载 |
| `AssetScopeManager` | 自动管理各场景的 Bundle 生命周期 |
| `GameAssetBuildConfig` | 编辑器中的 Bundle 打包配置 |

### 加载流程

```
构建: GameAssetBuildConfig → BuildPipeline → StreamingAssets
加载:  GameAssetManager.InitializeAsync → LoadAsync → Release
```

## 核心系统

### 游戏流程

```
GameMain → GameAssetManager.InitializeAsync → 加载首场景
```

### UI 系统

- `UIManager` 通过 `GameAssetManager` 加载预制体
- UI 预制体在 AssetBundle 中
- UI 层级由 `UILayer` 管理

## 架构原则

1. **数据驱动**：使用 ScriptableObject 做配置
2. **职责分离**：UI 逻辑与游戏逻辑分离
3. **异步优先**：Unity 6.2 中优先使用 Awaitable
4. **禁止 Resources**：绝不使用 Resources.Load

## 规则与指南

以下规则按领域分类，按需读取。相关领域工作时，使用 Read 工具加载对应文件：

| 规则文件 | 适用场景 | 说明 |
|----------|----------|------|
| `rules/unity-core.md` | 编写/修改 C# 脚本 | 命名规范、MonoBehaviour 生命周期、序列化 |
| `rules/unity-asset-loading.md` | 加载资源 | GameAssetManager API、Load/Release 配对 |
| `rules/unity-input.md` | 处理输入 | Unity Input System 使用规范 |
| `rules/unity-logging.md` | 添加日志 | 通用日志用 Logger，战斗日志用 BattleLogger，禁止 Debug.Log |
| `rules/game-asset-pipeline.md` | 构建/加载资源 | AssetBundle 构建与加载完整指南 |
