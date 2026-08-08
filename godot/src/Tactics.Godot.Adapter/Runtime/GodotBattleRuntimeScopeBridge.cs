using Godot;
using Tactics.Core.Runtime;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Godot node owns a Core scope and cancels/drains it before the node is freed.
/// </summary>
public partial class GodotBattleRuntimeScopeBridge : Node
{
    private BattleRuntimeScope? _scope;

    public BattleRuntimeScope Scope => _scope ??= new BattleRuntimeScope();

    public override void _ExitTree()
    {
        _scope?.Cancel();
        _ = DrainAndDisposeAsync();
    }

    private async Task DrainAndDisposeAsync()
    {
        if (_scope is null)
            return;

        try
        {
            await _scope.WhenIdleAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            GD.PushError($"BattleRuntimeScope fault during node exit: {exception}");
        }
        finally
        {
            _scope.Dispose();
            _scope = null;
        }
    }
}
