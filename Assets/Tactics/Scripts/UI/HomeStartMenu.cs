using Tactics.AssetPipeline;
using UnityEngine;
using UnityEngine.UI;

namespace Tactics.UI
{
    /// <summary>
    /// Home / main menu: wires the first <see cref="Button"/> under this <see cref="Canvas"/> to load the roguelike map scene.
    /// </summary>
    public class HomeStartMenu : MonoBehaviour
    {
        [Tooltip("Short name (e.g. SampleScene) under Assets/Tactics/Scenes/, or full path Assets/.../Scene.unity. Must be listed in the asset pipeline manifest.")]
        [SerializeField]
        private string _mapSceneName = "SampleScene";

        private Button _button;

        private void Awake()
        {
            _button = GetComponentInChildren<Button>(true);
            if (_button != null)
                _button.onClick.AddListener(LoadRoguelikeMap);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(LoadRoguelikeMap);
        }

        public void LoadRoguelikeMap()
        {
            SceneProjectPathHelper.TryLoadSceneViaAssetManager(_mapSceneName);
        }
    }
}
