using System.Threading.Tasks;

using Tactics.AssetPipeline;

using UnityEngine;



namespace Tactics

{

    /// <summary>

    /// Bootstrap component for Home scene.

    /// Ensures GameAssetManager is initialized when Home scene is loaded directly,

    /// while also supporting the normal Splash → Home flow.

    /// </summary>

    public sealed class HomeBootstrap : MonoBehaviour

    {

        [Tooltip("Shared options applied to the manager before it activates. Required for bootstrap.")]

        [SerializeField]

        private GameAssetRuntimeSettings _runtimeSettings;



        private async void Start()

        {

            // Check if GameAssetManager is already initialized (e.g., from Splash scene)

            if (GameAssetManager.Instance != null && GameAssetManager.Instance.IsInitialized)

            {

                Debug.Log("[HomeBootstrap] GameAssetManager already initialized by Splash scene.");

                return;

            }



            // If not initialized, initialize it now

            if (_runtimeSettings == null)

            {

                Debug.LogError("[HomeBootstrap] Assign Game Asset Runtime Settings (ScriptableObject).");

                return;

            }



            Debug.Log("[HomeBootstrap] Initializing GameAssetManager...");



            // Create GameAssetManager if it doesn't exist

            if (GameAssetManager.Instance == null)

            {

                GameAssetManager.CreateBootstrap(_runtimeSettings);

            }



            var instance = GameAssetManager.Instance;

            if (instance == null)

            {

                Debug.LogError("[HomeBootstrap] GameAssetManager.Instance is still null after bootstrap.");

                return;

            }



            // Initialize if not already initialized

            if (!instance.IsInitialized)

            {

                if (!await instance.InitializeAsync())

                {

                    Debug.LogError("[HomeBootstrap] GameAssetManager.InitializeAsync failed.");

                    return;

                }

            }



            Debug.Log("[HomeBootstrap] GameAssetManager initialized successfully.");

        }

    }

}

