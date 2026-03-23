using System.Threading.Tasks;

using Tactics.AssetPipeline;

using UnityEngine;



namespace Tactics

{

    /// <summary>

    /// Splash / boot entry: creates <see cref="GameAssetManager"/> via <see cref="GameAssetManager.CreateBootstrap"/>,

    /// then initializes and loads the first scene (e.g. Home).

    /// </summary>

    public sealed class GameMain : MonoBehaviour

    {

        [Tooltip("Shared options applied to the manager before it activates. Required for bootstrap.")]

        [SerializeField]

        private GameAssetRuntimeSettings _runtimeSettings;



        [Tooltip("Short name under Assets/Tactics/Scenes/ or full Assets/.../Home.unity. Must be in the asset pipeline manifest.")]

        [SerializeField]

        private string _firstSceneNameOrPath = "Home";



        [Tooltip("Minimum time to keep the splash scene visible before loading Home.")]

        [SerializeField]

        private float _minimumSplashSeconds = 0.5f;



        private async void Start()

        {

            if (_minimumSplashSeconds > 0f)

                await Task.Delay((int)(_minimumSplashSeconds * 1000f));



            await new GameFlowManager().RunAsync(_runtimeSettings, _firstSceneNameOrPath);
            return;

        }

    }

}

