using System.Collections.Generic;
using Tactics.AssetPipeline;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    public enum DamageNumberType
    {
        Normal,
        Critical,
        Heal,
        Miss
    }

    public sealed class DamageNumberManager : MonoBehaviour
    {
        private static DamageNumberManager _instance;
        public static DamageNumberManager Instance => _instance;

        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private DamageNumberConfig normalConfig;
        [SerializeField] private DamageNumberConfig critConfig;
        [SerializeField] private DamageNumberConfig healConfig;
        [SerializeField] private DamageNumberConfig missConfig;
        [SerializeField] private int poolSize = 20;

        private const string PanelSettingsPath = "Assets/Tactics/UIToolkit/PanelSettings.asset";
        private const string StyleSheetPath = "Assets/Tactics/Arts/UI/DamageNumber.uss";

        private UIDocument _uiDocument;
        private VisualElement _container;
        private Queue<Label> _labelPool;
        private List<DamageNumberInstance> _activeInstances = new();

        private struct DamageNumberInstance
        {
            public Label Label;
            public Vector3 WorldStartPosition;
            public float SpawnTime;
            public float Lifetime;
            public float MoveSpeed;
            public float StartScale;
            public float PeakScale;
            public float EndScale;
            public float FadeInDuration;
            public float FadeOutDuration;
        }

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            var mgr = GameAssetManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[DamageNumberManager] GameAssetManager.Instance is null. Ensure bootstrap ran before calling UI methods.");
                return;
            }

            if (!mgr.IsInitialized)
            {
                Debug.LogError("[DamageNumberManager] GameAssetManager is not initialized. Call Initialize/InitializeAsync before calling UI methods.");
                return;
            }

            // Load PanelSettings if not assigned in Inspector
            if (panelSettings == null)
            {
                try
                {
                    panelSettings = mgr.Load<PanelSettings>(PanelSettingsPath);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DamageNumberManager] Failed to load PanelSettings: {e.Message}");
                    return;
                }
            }

            // Load StyleSheet
            StyleSheet styleSheet = null;
            try
            {
                styleSheet = mgr.Load<StyleSheet>(StyleSheetPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DamageNumberManager] Failed to load StyleSheet: {e.Message}");
            }

            // Create UIDocument
            _uiDocument = gameObject.AddComponent<UIDocument>();
            _uiDocument.panelSettings = panelSettings;

            // Add StyleSheet
            if (styleSheet != null && _uiDocument.rootVisualElement != null)
            {
                _uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
            }

            // Create container
            _container = new VisualElement();
            _container.name = "DamageNumberContainer";
            _container.style.position = Position.Absolute;
            _container.style.width = Length.Percent(100);
            _container.style.height = Length.Percent(100);
            _container.pickingMode = PickingMode.Ignore;
            
            if (_uiDocument.rootVisualElement != null)
            {
                _uiDocument.rootVisualElement.Add(_container);
                Debug.Log($"[DamageNumberManager] Container added. Root child count: {_uiDocument.rootVisualElement.childCount}");
            }
            else
            {
                Debug.LogError("[DamageNumberManager] rootVisualElement is null after creating UIDocument!");
            }

            // Initialize pool
            _labelPool = new Queue<Label>();
            for (int i = 0; i < poolSize; i++)
            {
                _labelPool.Enqueue(CreatePooledLabel());
            }
        }

        private Label CreatePooledLabel()
        {
            var label = new Label();
            label.style.position = Position.Absolute;
            label.pickingMode = PickingMode.Ignore;
            label.style.display = DisplayStyle.None;
            return label;
        }

        public void Spawn(DamageNumberType type, int value, Vector3 worldPosition)
        {
            if (_container == null)
            {
                Debug.LogWarning("[DamageNumberManager] Container is null. UI not initialized yet.");
                return;
            }

            var config = GetConfig(type);
            if (config == null) return;

            Label label;
            if (_labelPool.Count > 0)
            {
                label = _labelPool.Dequeue();
            }
            else
            {
                if (_activeInstances.Count > 0)
                {
                    var oldest = _activeInstances[0];
                    _activeInstances.RemoveAt(0);
                    _container.Remove(oldest.Label);
                    label = oldest.Label;
                }
                else
                {
                    label = CreatePooledLabel();
                }
            }

            string text = type == DamageNumberType.Miss ? "Miss" : value.ToString();
            label.text = text;
            label.style.display = DisplayStyle.Flex;
            label.AddToClassList("damage-number");
            label.AddToClassList(config.ussClassName);

            var instance = new DamageNumberInstance
            {
                Label = label,
                WorldStartPosition = worldPosition,
                SpawnTime = Time.time,
                Lifetime = config.lifetime,
                MoveSpeed = config.moveSpeed,
                StartScale = config.startScale,
                PeakScale = config.peakScale,
                EndScale = config.endScale,
                FadeInDuration = config.fadeInDuration,
                FadeOutDuration = config.fadeOutDuration
            };

            _activeInstances.Add(instance);
            _container.Add(label);
        }

        private DamageNumberConfig GetConfig(DamageNumberType type)
        {
            return type switch
            {
                DamageNumberType.Normal => normalConfig,
                DamageNumberType.Critical => critConfig,
                DamageNumberType.Heal => healConfig,
                DamageNumberType.Miss => missConfig,
                _ => normalConfig
            };
        }

        private void Update()
        {
            float currentTime = Time.time;
            var camera = Camera.main;
            if (camera == null) return;

            for (int i = _activeInstances.Count - 1; i >= 0; i--)
            {
                var instance = _activeInstances[i];
                float elapsed = currentTime - instance.SpawnTime;

                if (elapsed >= instance.Lifetime)
                {
                    Despawn(i);
                    continue;
                }

                Vector3 screenPos = camera.WorldToScreenPoint(instance.WorldStartPosition);
                if (screenPos.z < 0) continue;

                float uiX = screenPos.x;
                float uiY = Screen.height - screenPos.y;
                float moveOffset = instance.MoveSpeed * elapsed;

                instance.Label.style.left = uiX;
                instance.Label.style.top = uiY - moveOffset;

                float alpha;
                if (elapsed < instance.FadeInDuration)
                {
                    alpha = elapsed / instance.FadeInDuration;
                }
                else if (elapsed > instance.Lifetime - instance.FadeOutDuration)
                {
                    float fadeElapsed = elapsed - (instance.Lifetime - instance.FadeOutDuration);
                    alpha = 1f - (fadeElapsed / instance.FadeOutDuration);
                }
                else
                {
                    alpha = 1f;
                }
                instance.Label.style.opacity = alpha;

                float scale;
                if (elapsed < instance.FadeInDuration)
                {
                    float t = elapsed / instance.FadeInDuration;
                    scale = Mathf.Lerp(instance.StartScale, instance.PeakScale, t);
                }
                else
                {
                    float holdDuration = instance.Lifetime - instance.FadeInDuration;
                    float t = Mathf.Clamp01((elapsed - instance.FadeInDuration) / (holdDuration * 0.5f));
                    scale = Mathf.Lerp(instance.PeakScale, instance.EndScale, t);
                }
                instance.Label.style.scale = new Scale(new Vector2(scale, scale));

                _activeInstances[i] = instance;
            }
        }

        private void Despawn(int index)
        {
            var instance = _activeInstances[index];
            instance.Label.style.display = DisplayStyle.None;
            instance.Label.style.opacity = 1f;
            instance.Label.style.scale = new Scale(Vector2.one);
            
            instance.Label.RemoveFromClassList("damage-number");
            instance.Label.RemoveFromClassList(normalConfig?.ussClassName);
            instance.Label.RemoveFromClassList(critConfig?.ussClassName);
            instance.Label.RemoveFromClassList(healConfig?.ussClassName);
            instance.Label.RemoveFromClassList(missConfig?.ussClassName);
            
            _container.Remove(instance.Label);
            _labelPool.Enqueue(instance.Label);
            _activeInstances.RemoveAt(index);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
