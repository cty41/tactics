# Tactics Core Boundary Spike

This is a disposable boundary proof created from the frozen Unity `w1` commit.
It is not a second long-term source of gameplay rules and does not change the
canonical Unity worktree.

The first slice proves that migration contracts can compile and test without
`UnityEngine`, Godot, Unity assets, or third-party packages. It intentionally
contains only small engine-neutral contracts: `ContentId`, 10x10 board bounds,
grid coordinates, and damage outcomes.

## Verification

```powershell
dotnet test .\tests\Tactics.Core.Tests\Tactics.Core.Tests.csproj --configuration Release
```

The next slice must replace prototypes with extracted, parity-tested domain
behavior before this worktree can become migration input.
