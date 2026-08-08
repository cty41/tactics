using System;
using System.Threading.Tasks;
using Tactics.Core.Runtime;
using UnityEngine;

namespace Tactics.Unity.Adapter.Runtime;

/// <summary>
/// Temporary Unity lifecycle bridge. Core owns cancellation semantics; Unity only owns the host object.
/// </summary>
public sealed class UnityBattleRuntimeScopeBridge : MonoBehaviour
{
    private BattleRuntimeScope _scope;

    public BattleRuntimeScope Scope => _scope ??= new BattleRuntimeScope();

    private void OnDestroy()
    {
        if (_scope == null)
            return;

        _scope.Cancel();
        _ = DrainAndDisposeAsync(_scope);
        _scope = null;
    }

    private static async Task DrainAndDisposeAsync(BattleRuntimeScope scope)
    {
        try
        {
            await scope.WhenIdleAsync().ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
        }
    }
}
