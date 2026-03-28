using UnityEngine;

namespace Tactics.AssetPipeline
{
    /// <summary>
    /// Scene-placed MonoBehaviour singleton: register in <see cref="Awake"/>, destroy duplicate instances.
    /// Do not rely on lazy <c>new GameObject</c> creation.
    /// </summary>
    public abstract class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviourSingleton<T>
    {
        public static T Instance { get; private set; }

        [SerializeField]
        private bool _persistAcrossScenes;

        protected bool PersistAcrossScenes => _persistAcrossScenes;

        /// <summary>Call before the object is activated (e.g. while inactive after Instantiate) so <see cref="Awake"/> sees the final value.</summary>
        protected void SetPersistAcrossScenes(bool persist) => _persistAcrossScenes = persist;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[MonoBehaviourSingleton] Duplicate {typeof(T).Name} in scene; destroying '{name}'.");
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
            if (_persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
