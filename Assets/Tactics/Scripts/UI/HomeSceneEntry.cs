using Tactics.Flow.Home;
using UnityEngine;

namespace Tactics.UI
{
    /// <summary>
    /// Home scene entry point: delegates UI loading to <see cref="HomeFlowCoordinator"/>.
    /// </summary>
    public sealed class HomeSceneEntry : MonoBehaviour
    {
        private async void Start()
        {
            await HomeFlowCoordinator.Instance.ShowHomeUIAsync();
        }
    }
}
