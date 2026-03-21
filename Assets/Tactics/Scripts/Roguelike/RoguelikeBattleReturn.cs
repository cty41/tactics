using System.Collections;
using TurnBasedStrategyFramework.Common.Controllers.GameResolvers;
using TurnBasedStrategyFramework.Unity.Controllers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tactics.Roguelike
{
    /// <summary>
    /// After a tactics battle ends, loads the roguelike map scene stored in PlayerPrefs (see MapPlayerTracker).
    /// Attach to the same GameObject as <see cref="UnityGridController"/> (e.g. GridController in Test1).
    /// </summary>
    public class RoguelikeBattleReturn : MonoBehaviour
    {
        /// <summary>Must match MapPlayerTracker.RoguelikeReturnScenePrefsKey in Assets/Scripts.</summary>
        public const string ReturnScenePlayerPrefsKey = "RoguelikeReturnScene";

        [SerializeField] private float _returnDelaySeconds = 1.5f;
        [Tooltip("Used when PlayerPrefs key is missing (e.g. opened Test1 directly from editor).")]
        [SerializeField] private string _defaultMapSceneName = "SampleScene";

        private UnityGridController _grid;

        private void Awake()
        {
            _grid = GetComponent<UnityGridController>();
            if (_grid != null)
                _grid.GameEnded += OnGameEnded;
        }

        private void OnDestroy()
        {
            if (_grid != null)
                _grid.GameEnded -= OnGameEnded;
        }

        private void OnGameEnded(GameResult _)
        {
            StartCoroutine(CoReturnToMap());
        }

        private IEnumerator CoReturnToMap()
        {
            if (_returnDelaySeconds > 0f)
                yield return new WaitForSeconds(_returnDelaySeconds);

            string sceneName = PlayerPrefs.GetString(ReturnScenePlayerPrefsKey, _defaultMapSceneName);
            SceneManager.LoadScene(sceneName);
        }
    }
}
