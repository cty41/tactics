# Tactics Architecture

## Project Structure

```
Assets/
├── Tactics/
│   ├── AssetPipeline/       # AssetBundle building and runtime loading
│   ├── Runtime/            # Core game runtime
│   │   ├── UI/            # UI system
│   │   └── ...           
│   ├── Scripts/           # Game logic
│   └── Scenes/            # Unity scenes
└── StreamingAssets/      # Built AssetBundles
```

## Asset Pipeline

See `docs/references/GameAssetPipeline.md` for full documentation.

### Key Components

| Component | Purpose |
|-----------|---------|
| `GameAssetManager` | Scene singleton for asset loading |
| `AssetScopeManager` | Automatic bundle lifetime per scene |
| `GameAssetBuildConfig` | Editor configuration for bundling |

### Loading Flow

```
Build: GameAssetBuildConfig → BuildPipeline → StreamingAssets
Load:  GameAssetManager.InitializeAsync → LoadAsync → Release
```

## Core Systems

### Game Flow

```
GameMain → GameAssetManager.InitializeAsync → Load首场景
```

### UI System

- `UIManager` loads prefabs via `GameAssetManager`
- UI prefabs in AssetBundles
- UI layers managed by `UILayer`

## Architecture Principles

1. **Data-Oriented**: Use ScriptableObjects for configuration
2. **Separation**: UI logic separate from game logic
3. **Async**: Prefer Awaitable for Unity 6.2
4. **No Resources**: Never use Resources.Load
