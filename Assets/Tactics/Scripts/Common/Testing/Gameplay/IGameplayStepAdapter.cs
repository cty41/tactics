using System.Threading.Tasks;

namespace Tactics.Common.Testing.Gameplay
{
    public interface IGameplayStepAdapter
    {
        string AdapterName { get; }
        bool CanExecute(ExecutableScenarioAction action);
        Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action);
        bool CanAssert(ExecutableScenarioAssertion assertion);
        Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion);
        ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request);
    }
}
