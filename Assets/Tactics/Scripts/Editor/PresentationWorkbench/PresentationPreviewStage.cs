#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DG.DOTweenEditor;
using DG.Tweening;
using Tactics.Common.Interactables;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Tween;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Tactics.EditorTools
{
    /// <summary>
    /// Previews the runtime Pure Run unit tween language in an isolated editor scene.
    /// </summary>
    /// <remarks>
    /// Preview objects and sandbox profiles are transient. Profile assets are only changed by
    /// explicit Apply operations, which participate in Unity Undo. Skill VFX recipes remain in
    /// the separate Skill VFX Preview; this window only marks release and projectile impact.
    /// </remarks>
    public sealed partial class PresentationWorkbenchWindow : EditorWindow
    {
        internal enum PresentationPreviewScopeKind
        {
            FullScenario,
            Phase,
            Entry,
            Leaf,
            ForkRegion
        }

        internal sealed class PresentationPreviewScope
        {
            internal PresentationPreviewScopeKind Kind { get; set; } = PresentationPreviewScopeKind.FullScenario;
            internal int PhaseIndex { get; set; }
            internal PresentationCueKind Cue { get; set; } = PresentationCueKind.Action;
            internal string NodeId { get; set; }

            internal PresentationPreviewScope Clone() => new()
            {
                Kind = Kind,
                PhaseIndex = PhaseIndex,
                Cue = Cue,
                NodeId = NodeId
            };
        }

        internal sealed class PresentationPreviewTimelineEvent
        {
            internal string Event { get; set; }
            internal string NodeId { get; set; }
            internal string NodeType { get; set; }
            internal float Time { get; set; }
            internal float Duration { get; set; }
            internal int Lane { get; set; }
            internal int PhaseIndex { get; set; } = -1;
            internal string Marker { get; set; }
        }

        internal sealed class PresentationPreviewRenderResult
        {
            internal Texture2D Texture { get; set; }
            internal PresentationPreviewScope RequestedScope { get; set; }
            internal PresentationPreviewScope ResolvedScope { get; set; }
            internal IReadOnlyList<PresentationPreviewTimelineEvent> Timeline { get; set; }
            internal IReadOnlyList<string> ActualFallbacks { get; set; }
            internal int RandomSeed { get; set; }
        }

        private const string DefaultActorPath =
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab";
        private const string DefaultTargetPath =
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatCharger.prefab";
        private const string DefaultUnitProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/StandardUnitTweenProfile.asset";
        private const string DefaultProjectileProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/MagicBasic.asset";
        private const string DefaultCorpsePrefabPath =
            "Assets/Tactics/Arts/Prefabs/Units/TestCorpse.prefab";
        private const float InteractivePreviewWidth = 1280f;
        private const float InteractivePreviewHeight = 720f;

        private PreviewRenderUtility _previewUtility;
        private GameObject _actorPrefab;
        private GameObject _targetPrefab;
        private GameObject _actorInstance;
        private GameObject _targetInstance;
        private GameObject _corpseInstance;
        private StandardUnitTweenProfile _unitProfile;
        private StandardUnitTweenProfile _unitSandbox;
        private ProjectileVisualProfile _projectileProfile;
        private ProjectileVisualProfile _projectileSandbox;
        private ProjectileVisualProfile _transientProjectileProfile;
        private BattlePresentationGraph _presentationGraph;
        private readonly List<GameObject> _presentationPreviewObjects = new();
        private UnityEditor.Editor _unitSandboxEditor;
        private UnityEditor.Editor _projectileSandboxEditor;
        private ProjectileVisualPreviewAdapter _projectileAdapter;
        private ProceduralVfxPreviewAdapter _proceduralVfxAdapter;
        private Sequence _previewSequence;
        private Texture2D _tileTexture;
        private Texture2D _placeholderTexture;
        private Sprite _tileSprite;
        private Sprite _placeholderSprite;
        private PreviewSpriteState _actorSpriteState;
        private PreviewSpriteState _targetSpriteState;
        private bool _unitSandboxDirty;
        private bool _projectileSandboxDirty;
        private bool _loop = true;
        private float _playbackSpeed = 1f;
        private float _distanceTiles = 3f;
        private float _singleLoopDuration = 1f;
        private float _releaseTime = -1f;
        private float _poseRestoreTime = -1f;
        private float _impactTime = -1f;
        private float _blockingTime = -1f;
        private float _hitTime = -1f;
        private float _corpseDropTime = -1f;
        private float _corpseImpactTime = -1f;
        private float _corpseImpactEndTime = -1f;
        private float _corpseSettledTime = -1f;
        private float _deathHandoffTime = -1f;
        private float _lethalRecoilTime = -1f;
        private float _lethalShakeTime = -1f;
        private float _lethalCollapseTime = -1f;
        private PreviewAction _action = PreviewAction.Idle;
        private FacingDirection _facing = FacingDirection.South;
        private UnitPoseFamily _poseFamily;
        private UnitVisualState _visualState;
        private string _poseResolution = "No pose requested";
        private PresentationPreviewScope _previewScope = new();
        private PresentationPreviewScope _resolvedPreviewScope = new();
        private readonly List<PresentationPreviewTimelineEvent> _previewTimeline = new();
        private readonly List<string> _previewFallbacks = new();
        private ObjectField _previewActorField;
        private ObjectField _previewTargetField;
        private ObjectField _previewUnitProfileField;
        private ObjectField _previewProjectileProfileField;
        private ObjectField _previewPoseFamilyField;
        private EnumField _previewActionField;
        private EnumField _previewFacingField;
        private EnumField _previewVisualStateField;
        private Slider _previewDistanceField;
        private Slider _previewTimelineSlider;
        private Label _previewTimeLabel;
        private Label _previewPoseResolutionLabel;
        private HelpBox _previewModeHelpBox;
        private HelpBox _previewWarningHelpBox;
        private VisualElement _previewMarkerLayer;
        private VisualElement _previewTimelineEvents;
        private int _retainedStageRefreshVersion;

        [MenuItem("Tactics/Pure Run/Presentation Workbench")]
        private static void Open()
        {
            var window = GetWindow<PresentationWorkbenchWindow>();
            window.titleContent = new GUIContent("Presentation Workbench");
            window.minSize = new Vector2(1180f, 680f);
            window.Show();
        }

        internal static void OpenGraph(BattlePresentationGraph graph)
        {
            var window = GetWindow<PresentationWorkbenchWindow>();
            window.titleContent = new GUIContent("Presentation Workbench");
            window.minSize = new Vector2(1180f, 680f);
            window.SetWorkbenchGraph(graph);
            window.Show();
        }

        internal static PresentationCueKind ResolveDefaultPreviewCue(BattlePresentationGraph graph)
        {
            if (graph == null)
                return PresentationCueKind.Action;
            if (graph.FindEntry(graph.DefaultPreviewEntry) != null)
                return graph.DefaultPreviewEntry;

            PresentationEntryNodeRecord firstEnabledEntry = graph.Nodes.Find(node =>
                node is PresentationEntryNodeRecord entry && entry.Enabled) as PresentationEntryNodeRecord;
            if (firstEnabledEntry != null)
                return firstEnabledEntry.Cue;

            PresentationEntryNodeRecord firstEntry = graph.Nodes
                .Find(node => node is PresentationEntryNodeRecord) as PresentationEntryNodeRecord;
            return firstEntry?.Cue ?? PresentationCueKind.Action;
        }

        private void OnEnable()
        {
            saveChangesMessage = "Apply the Pure Run tween preview sandbox changes before closing?";
            _actorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultActorPath);
            _targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTargetPath);
            if (_presentationGraph?.PreviewActorPrefab != null)
                _actorPrefab = _presentationGraph.PreviewActorPrefab;
            if (_presentationGraph?.PreviewTargetPrefab != null)
                _targetPrefab = _presentationGraph.PreviewTargetPrefab;
            SetUnitProfile(AssetDatabase.LoadAssetAtPath<StandardUnitTweenProfile>(DefaultUnitProfilePath));
            SetProjectileProfile(
                AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(DefaultProjectileProfilePath));
            CreatePreviewUtility();
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            _retainedStageRefreshVersion++;
            _previewRenderController?.Dispose();
            _previewRenderController = null;
            _previewSurface = null;
            Cleanup();
            DisposeWorkbenchSession();
        }

        private void BeforeAssemblyReload()
        {
            _retainedStageRefreshVersion++;
            _previewRenderController?.Dispose();
            _previewRenderController = null;
            Cleanup();
        }

        internal static Rect ResolveInteractivePreviewRenderRect()
        {
            return new Rect(0f, 0f, InteractivePreviewWidth, InteractivePreviewHeight);
        }
        private VisualElement BuildRetainedPreviewWorkspace()
        {
            var root = new VisualElement
            {
                name = "presentation-preview-workspace"
            };
            root.style.flexGrow = 1f;
            root.style.minWidth = 320f;
            root.style.minHeight = 0f;
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;

            root.Add(BuildRetainedPreviewSettings());
            root.Add(BuildRetainedPlaybackToolbar());
            _previewSurface = new PresentationPreviewSurface();
            root.Add(_previewSurface);
            root.Add(BuildRetainedTimeline());
            SyncRetainedPreviewControls();
            return root;
        }

        private VisualElement BuildRetainedPreviewSettings()
        {
            var foldout = new Foldout
            {
                text = "Preview Stage",
                value = false
            };

            var assets = new VisualElement();
            assets.style.flexDirection = FlexDirection.Row;
            assets.style.flexWrap = Wrap.Wrap;
            _previewActorField = CreatePreviewObjectField("Actor", typeof(GameObject), _actorPrefab, assets);
            _previewTargetField = CreatePreviewObjectField("Target", typeof(GameObject), _targetPrefab, assets);
            _previewUnitProfileField = CreatePreviewObjectField(
                "Unit Profile", typeof(StandardUnitTweenProfile), _unitProfile, assets);
            _previewProjectileProfileField = CreatePreviewObjectField(
                "Projectile", typeof(ProjectileVisualProfile), _projectileProfile, assets);
            foldout.Add(assets);

            foreach (ObjectField field in new[]
                     {
                         _previewActorField,
                         _previewTargetField,
                         _previewUnitProfileField,
                         _previewProjectileProfileField
                     })
            {
                field.RegisterValueChangedCallback(_ => ApplyRetainedPreviewAssetSelection());
            }

            var options = new VisualElement();
            options.style.flexDirection = FlexDirection.Row;
            options.style.flexWrap = Wrap.Wrap;
            _previewActionField = new EnumField("Action", _action);
            _previewFacingField = new EnumField("Facing", _facing);
            _previewVisualStateField = new EnumField("Visual State", _visualState);
            _previewDistanceField = new Slider("Distance", 2f, 6f) { value = _distanceTiles };
            _previewPoseFamilyField = new ObjectField("Pose Family")
            {
                objectType = typeof(UnitPoseFamily),
                allowSceneObjects = false,
                value = _poseFamily
            };
            foreach (VisualElement field in new VisualElement[]
                     {
                         _previewActionField,
                         _previewFacingField,
                         _previewVisualStateField,
                         _previewDistanceField,
                         _previewPoseFamilyField
                     })
            {
                field.style.minWidth = 180f;
                field.style.flexGrow = 1f;
                options.Add(field);
            }
            foldout.Add(options);

            _previewActionField.RegisterValueChangedCallback(evt =>
            {
                _action = (PreviewAction)evt.newValue;
                QueueRetainedStageRefresh();
            });
            _previewFacingField.RegisterValueChangedCallback(evt =>
            {
                _facing = (FacingDirection)evt.newValue;
                QueueRetainedStageRefresh();
            });
            _previewVisualStateField.RegisterValueChangedCallback(evt =>
            {
                _visualState = (UnitVisualState)evt.newValue;
                QueueRetainedStageRefresh();
            });
            _previewDistanceField.RegisterValueChangedCallback(evt =>
            {
                _distanceTiles = evt.newValue;
                QueueRetainedStageRefresh();
            });
            _previewPoseFamilyField.RegisterValueChangedCallback(evt =>
            {
                _poseFamily = evt.newValue as UnitPoseFamily;
                QueueRetainedStageRefresh();
            });

            _previewModeHelpBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _previewWarningHelpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            _previewPoseResolutionLabel = new Label();
            _previewPoseResolutionLabel.style.marginLeft = 4f;
            _previewPoseResolutionLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            foldout.Add(_previewModeHelpBox);
            foldout.Add(_previewWarningHelpBox);
            foldout.Add(_previewPoseResolutionLabel);
            return foldout;
        }

        private ObjectField CreatePreviewObjectField(
            string label,
            Type objectType,
            UnityEngine.Object value,
            VisualElement parent)
        {
            var field = new ObjectField(label)
            {
                objectType = objectType,
                allowSceneObjects = false,
                value = value
            };
            field.style.minWidth = 220f;
            field.style.flexGrow = 1f;
            parent.Add(field);
            return field;
        }

        private VisualElement BuildRetainedPlaybackToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(PlayPreview) { text = "Play" });
            toolbar.Add(new ToolbarButton(() =>
            {
                _previewSequence?.Pause();
                RequestInteractivePreviewFrame();
            }) { text = "Pause" });
            toolbar.Add(new ToolbarButton(() => StopPreview(true)) { text = "Stop" });
            toolbar.Add(new ToolbarButton(RestartPreview) { text = "Restart" });
            toolbar.Add(new ToolbarSpacer());
            var loopToggle = new ToolbarToggle { text = "Loop", value = _loop };
            loopToggle.RegisterValueChangedCallback(evt =>
            {
                _loop = evt.newValue;
                RebuildSequence(false);
            });
            toolbar.Add(loopToggle);

            var speedMenu = new ToolbarMenu { text = $"Speed {_playbackSpeed:0.##}x" };
            foreach (float speed in new[] { 0.25f, 0.5f, 1f, 2f, 4f })
            {
                float selectedSpeed = speed;
                speedMenu.menu.AppendAction($"{speed:0.##}x", _ =>
                {
                    _playbackSpeed = selectedSpeed;
                    speedMenu.text = $"Speed {selectedSpeed:0.##}x";
                    if (_previewSequence != null)
                        _previewSequence.timeScale = selectedSpeed;
                    RequestInteractivePreviewFrame();
                });
            }
            toolbar.Add(speedMenu);
            return toolbar;
        }

        private VisualElement BuildRetainedTimeline()
        {
            var root = new VisualElement
            {
                name = "presentation-preview-timeline"
            };
            root.style.flexShrink = 0f;
            _previewTimelineSlider = new Slider("Timeline", 0f, 1f);
            _previewTimelineSlider.RegisterValueChangedCallback(evt =>
            {
                if (_previewSequence == null || !_previewSequence.IsActive())
                    return;
                _previewSequence.Pause();
                _previewSequence.Goto(evt.newValue * _singleLoopDuration, false);
                RequestInteractivePreviewFrame();
                RefreshRetainedPreviewUi();
            });
            root.Add(_previewTimelineSlider);

            _previewMarkerLayer = new VisualElement
            {
                name = "presentation-preview-markers",
                pickingMode = PickingMode.Ignore
            };
            _previewMarkerLayer.style.height = 12f;
            _previewMarkerLayer.style.position = Position.Relative;
            root.Add(_previewMarkerLayer);

            _previewTimeLabel = new Label("0 / 0s");
            _previewTimeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            root.Add(_previewTimeLabel);

            var eventScroll = new ScrollView(ScrollViewMode.Horizontal);
            eventScroll.style.height = 28f;
            _previewTimelineEvents = new VisualElement
            {
                name = "presentation-preview-node-events"
            };
            _previewTimelineEvents.style.flexDirection = FlexDirection.Row;
            eventScroll.Add(_previewTimelineEvents);
            root.Add(eventScroll);
            return root;
        }

        private void ApplyRetainedPreviewAssetSelection()
        {
            if (!TryCommitAssetSelection(
                    _previewActorField?.value as GameObject,
                    _previewTargetField?.value as GameObject,
                    _previewUnitProfileField?.value as StandardUnitTweenProfile,
                    _previewProjectileProfileField?.value as ProjectileVisualProfile))
            {
                SyncRetainedPreviewControls();
                return;
            }

            DrawWorkbenchInspector(null);
            RebuildStage();
            SyncRetainedPreviewControls();
        }

        private void QueueRetainedStageRefresh()
        {
            int refreshVersion = ++_retainedStageRefreshVersion;
            if (_previewSurface == null)
            {
                RebuildStage();
                return;
            }
            _previewSurface.schedule.Execute(() =>
            {
                if (refreshVersion != _retainedStageRefreshVersion || this == null)
                    return;
                RebuildStage();
            }).StartingIn(100);
        }

        private void SyncRetainedPreviewControls()
        {
            _previewActorField?.SetValueWithoutNotify(_actorPrefab);
            _previewTargetField?.SetValueWithoutNotify(_targetPrefab);
            _previewUnitProfileField?.SetValueWithoutNotify(_unitProfile);
            _previewProjectileProfileField?.SetValueWithoutNotify(_projectileProfile);
            _previewActionField?.SetValueWithoutNotify(_action);
            _previewFacingField?.SetValueWithoutNotify(_facing);
            _previewVisualStateField?.SetValueWithoutNotify(_visualState);
            _previewDistanceField?.SetValueWithoutNotify(_distanceTiles);
            _previewPoseFamilyField?.SetValueWithoutNotify(_poseFamily);
            if (_previewActionField != null)
                _previewActionField.style.display = _presentationGraph == null
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            if (_previewModeHelpBox != null)
            {
                _previewModeHelpBox.text = _presentationGraph == null
                    ? "Standalone profile preview."
                    : _presentationGraph.HasPreviewScenario
                        ? "Full Preview Scenario"
                        : $"Legacy {_presentationGraph.DefaultPreviewEntry} fallback";
            }
            if (_previewWarningHelpBox != null)
            {
                bool legacyFallback = _presentationGraph != null && !_presentationGraph.HasPreviewScenario;
                bool projectileFallback = UsesProjectile(_action) && !CanRenderSelectedProjectile();
                _previewWarningHelpBox.text = legacyFallback
                    ? "This graph has no full Preview Scenario and uses DefaultPreviewEntry."
                    : "Projectile visual is incomplete; preview uses an editor-only placeholder.";
                _previewWarningHelpBox.style.display = legacyFallback || projectileFallback
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            if (_previewPoseResolutionLabel != null)
            {
                _previewPoseResolutionLabel.text = $"Pose resolution: {_poseResolution}";
                _previewPoseResolutionLabel.style.display =
                    _action is PreviewAction.Melee or PreviewAction.Ranged or PreviewAction.Cast or
                        PreviewAction.Hit or PreviewAction.LethalHitToCorpse
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }
        }

        private void RebuildRetainedTimeline()
        {
            if (_previewMarkerLayer == null || _previewTimelineEvents == null)
                return;
            _previewMarkerLayer.Clear();
            AddRetainedTimelineMarker(_releaseTime, "Release", new Color(0.35f, 0.85f, 1f));
            AddRetainedTimelineMarker(_poseRestoreTime, "Pose Restore", new Color(0.55f, 1f, 0.45f));
            AddRetainedTimelineMarker(_impactTime, "Projectile Impact", new Color(1f, 0.55f, 0.25f));
            AddRetainedTimelineMarker(_blockingTime, "VFX Contact", new Color(1f, 0.78f, 0.3f));
            AddRetainedTimelineMarker(_hitTime, "Hit", new Color(1f, 0.35f, 0.35f));
            AddRetainedTimelineMarker(_lethalRecoilTime, "Recoil", new Color(1f, 0.46f, 0.3f));
            AddRetainedTimelineMarker(_lethalShakeTime, "Shake", new Color(1f, 0.7f, 0.25f));
            AddRetainedTimelineMarker(_lethalCollapseTime, "Collapse", new Color(0.95f, 0.55f, 1f));
            AddRetainedTimelineMarker(_deathHandoffTime, "Death Handoff", new Color(1f, 0.3f, 0.65f));
            AddRetainedTimelineMarker(_corpseDropTime, "Drop", new Color(0.65f, 0.85f, 1f));
            AddRetainedTimelineMarker(_corpseImpactTime, "Impact", new Color(1f, 0.62f, 0.25f));
            AddRetainedTimelineMarker(_corpseImpactEndTime, "Impact End", new Color(1f, 0.78f, 0.45f));
            AddRetainedTimelineMarker(_corpseSettledTime, "Settled", new Color(0.55f, 1f, 0.55f));

            _previewTimelineEvents.Clear();
            _previewTimelineEvents.Add(new Label("Node Events"));
            foreach (PresentationPreviewTimelineEvent value in _previewTimeline
                         .Where(item => item.Event == "NodeStart" && !string.IsNullOrEmpty(item.NodeId))
                         .OrderBy(item => item.Time)
                         .ThenBy(item => item.Lane))
            {
                PresentationPreviewTimelineEvent timelineEvent = value;
                var button = new Button(() =>
                {
                    if (_previewSequence != null && _previewSequence.IsActive())
                    {
                        _previewSequence.Pause();
                        _previewSequence.Goto(timelineEvent.Time, false);
                    }
                    SelectWorkbenchTimelineNode(timelineEvent.NodeId);
                    RequestInteractivePreviewFrame();
                    RefreshRetainedPreviewUi();
                })
                {
                    text = $"{value.Time:0.###} {value.NodeType}"
                };
                button.style.marginLeft = 3f;
                _previewTimelineEvents.Add(button);
            }
        }

        private void AddRetainedTimelineMarker(float time, string label, Color color)
        {
            if (time < 0f || _singleLoopDuration <= 0f)
                return;
            var marker = new VisualElement
            {
                tooltip = $"{label}: {time:0.###}s",
                pickingMode = PickingMode.Ignore
            };
            marker.style.position = Position.Absolute;
            marker.style.left = Length.Percent(Mathf.Clamp01(time / _singleLoopDuration) * 100f);
            marker.style.top = 0f;
            marker.style.bottom = 0f;
            marker.style.width = 2f;
            marker.style.backgroundColor = color;
            _previewMarkerLayer.Add(marker);
        }

        private void RefreshRetainedPreviewUi()
        {
            bool sequenceActive = _previewSequence != null && _previewSequence.IsActive();
            bool isPlaying = sequenceActive && _previewSequence.IsPlaying();
            float elapsed = 0f;
            if (sequenceActive)
            {
                elapsed = _previewSequence.Elapsed(false);
                elapsed = _loop && _singleLoopDuration > 0f
                    ? elapsed % _singleLoopDuration
                    : Mathf.Min(elapsed, _singleLoopDuration);
            }
            float normalized = _singleLoopDuration > 0f
                ? Mathf.Clamp01(elapsed / _singleLoopDuration)
                : 0f;
            _previewTimelineSlider?.SetValueWithoutNotify(normalized);
            if (_previewTimeLabel != null)
                _previewTimeLabel.text = $"{elapsed:0.###} / {_singleLoopDuration:0.###}s";
            _previewSurface?.SetPlaying(isPlaying);

            IEnumerable<string> activeNodeIds = isPlaying
                ? _previewTimeline.Where(value => value.Event == "NodeStart" &&
                        value.Time <= elapsed && elapsed <= value.Time + Mathf.Max(value.Duration, 0.001f))
                    .Select(value => value.NodeId)
                    .Where(value => !string.IsNullOrEmpty(value))
                : Enumerable.Empty<string>();
            _workbenchGraphView?.SetPreviewActiveNodes(activeNodeIds);
        }

        private void RequestInteractivePreviewFrame()
        {
            _previewRenderController?.RequestRender();
        }

        private bool IsInteractivePreviewPlaying()
        {
            return _previewSequence != null && _previewSequence.IsActive() &&
                   _previewSequence.IsPlaying();
        }

        private void RenderInteractivePreviewFrame(RenderTexture target)
        {
            if (target == null)
                return;
            if (_previewUtility == null)
                CreatePreviewUtility();
            if (_actorInstance == null)
                RebuildStage();
            if (_previewUtility == null)
                return;

            Vector3 stageCenter = ResolveTargetPosition() * 0.5f;
            float verticalSpan = Mathf.Abs(ResolveTargetPosition().y) + 1.3f;
            bool previewBegan = false;
            try
            {
                _previewUtility.BeginPreview(ResolveInteractivePreviewRenderRect(), GUIStyle.none);
                previewBegan = true;
                _previewUtility.camera.orthographic = true;
                _previewUtility.camera.orthographicSize = Mathf.Max(1.55f, verticalSpan * 0.65f);
                _previewUtility.camera.transform.position =
                    new Vector3(stageCenter.x, stageCenter.y + 0.55f, -10f);
                _previewUtility.camera.transform.rotation = Quaternion.identity;
                _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
                _previewUtility.camera.backgroundColor = new Color(0.08f, 0.075f, 0.08f, 1f);
                _previewUtility.Render(true, false);
                Texture previewTexture = _previewUtility.EndPreview();
                previewBegan = false;
                Graphics.Blit(previewTexture, target);
                RefreshRetainedPreviewUi();
            }
            finally
            {
                if (previewBegan)
                    _previewUtility.EndPreview();
            }
        }

        internal static Texture2D RenderOffscreen(
            BattlePresentationGraph graph,
            int width,
            int height)
        {
            PresentationPreviewRenderResult result = RenderOffscreen(
                graph,
                new PresentationPreviewScope(),
                width,
                height,
                1337);
            return result.Texture;
        }

        internal static PresentationPreviewRenderResult RenderOffscreen(
            BattlePresentationGraph graph,
            PresentationPreviewScope scope,
            int width,
            int height,
            int randomSeed)
        {
            var window = CreateInstance<PresentationWorkbenchWindow>();
            UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(randomSeed);
                window._loop = false;
                window._previewScope = scope?.Clone() ?? new PresentationPreviewScope();
                window.SetWorkbenchGraph(graph);
                if (window._previewSequence != null && window._previewSequence.IsActive())
                    window._previewSequence.Goto(window._previewSequence.Duration(false) * 0.35f, false);
                return new PresentationPreviewRenderResult
                {
                    Texture = window.RenderOffscreenFrame(width, height),
                    RequestedScope = window._previewScope.Clone(),
                    ResolvedScope = window._resolvedPreviewScope.Clone(),
                    Timeline = window._previewTimeline
                        .OrderBy(value => value.Time)
                        .ThenBy(value => value.Lane)
                        .ThenBy(value => value.Event, StringComparer.Ordinal)
                        .ToList(),
                    ActualFallbacks = window._previewFallbacks.ToList(),
                    RandomSeed = randomSeed
                };
            }
            finally
            {
                UnityEngine.Random.state = previousRandomState;
                DestroyImmediate(window);
            }
        }

        private Texture2D RenderOffscreenFrame(int width, int height)
        {
            width = Mathf.Clamp(width, 64, 2048);
            height = Mathf.Clamp(height, 64, 1024);
            if (_previewUtility == null)
                CreatePreviewUtility();
            if (_actorInstance == null)
                RebuildStage();

            var rect = new Rect(0f, 0f, width, height);
            Vector3 stageCenter = ResolveTargetPosition() * 0.5f;
            float verticalSpan = Mathf.Abs(ResolveTargetPosition().y) + 1.3f;
            RenderTexture previous = RenderTexture.active;
            Texture2D result = null;
            bool previewBegan = false;
            try
            {
                _previewUtility.BeginPreview(rect, GUIStyle.none);
                previewBegan = true;
                _previewUtility.camera.orthographic = true;
                _previewUtility.camera.orthographicSize = Mathf.Max(1.55f, verticalSpan * 0.65f);
                _previewUtility.camera.transform.position = new Vector3(stageCenter.x, stageCenter.y + 0.55f, -10f);
                _previewUtility.camera.transform.rotation = Quaternion.identity;
                _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
                _previewUtility.camera.backgroundColor = new Color(0.08f, 0.075f, 0.08f, 1f);
                _previewUtility.Render(true, false);

                RenderTexture.active = _previewUtility.camera.targetTexture;
                result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                result.ReadPixels(rect, 0, 0, false);
                result.Apply(false, false);
                Texture2D completed = result;
                result = null;
                return completed;
            }
            finally
            {
                RenderTexture.active = previous;
                if (previewBegan)
                    _previewUtility.EndPreview();
                if (result != null)
                    DestroyImmediate(result);
            }
        }

        private void PlayPreview()
        {
            if (_previewSequence == null || !_previewSequence.IsActive())
                RebuildSequence(false);
            if (_previewSequence == null)
                return;

            _previewSequence.timeScale = _playbackSpeed;
            _previewSequence.Play();
            DOTweenEditorPreview.Start(RequestInteractivePreviewFrame);
            RequestInteractivePreviewFrame();
        }

        private void RestartPreview()
        {
            if (_previewSequence == null || !_previewSequence.IsActive())
                RebuildSequence(false);
            _previewSequence?.Restart();
            if (_previewSequence == null)
                return;

            _previewSequence.timeScale = _playbackSpeed;
            DOTweenEditorPreview.Start(RequestInteractivePreviewFrame);
            RequestInteractivePreviewFrame();
        }

        private void StopPreview(bool restoreVisuals)
        {
            DOTweenEditorPreview.Stop(true, true);
            _previewSequence = null;
            _projectileAdapter?.Dispose();
            _projectileAdapter = null;
            ClearPresentationPreviewObjects();
            if (restoreVisuals)
                RestorePreviewVisuals();
            RequestInteractivePreviewFrame();
            RefreshRetainedPreviewUi();
        }

        private void RebuildStage()
        {
            StopPreview(false);
            CreatePreviewUtility();
            if (_previewUtility == null || _actorPrefab == null)
                return;

            _actorInstance = _previewUtility.InstantiatePrefabInScene(_actorPrefab);
            if (_targetPrefab != null)
                _targetInstance = _previewUtility.InstantiatePrefabInScene(_targetPrefab);
            SetPreviewObjectState(_actorInstance, Vector3.zero, _facing);
            SetPreviewObjectState(_targetInstance, ResolveTargetPosition(), Opposite(_facing));
            // Edit Mode does not invoke UnitTweenVisual.Awake. Capture each authored Sprite pose
            // before RebuildSequence stops/restores any preview state, otherwise the default
            // zero-valued baseline collapses the renderer.
            ResolveVisual(_actorInstance, _unitSandbox);
            ResolveVisual(_targetInstance, _unitSandbox);
            CaptureStandingSpriteStates();
            CreateTileStage();
            RebuildSequence(false);
        }

        private void RebuildSequence(bool keepPlaying)
        {
            bool wasPlaying = keepPlaying && _previewSequence != null && _previewSequence.IsPlaying();
            StopPreview(true);
            if (_actorInstance == null || _unitSandbox == null)
                return;

            ApplyFacing(_actorInstance, _facing);
            ApplyFacing(_targetInstance, Opposite(_facing));
            _actorInstance?.GetComponent<FourDirectionSpriteVisual>()?
                .SetVisualState(_visualState, _facing);
            CaptureStandingSpriteStates();
            UnitTweenVisual actorVisual = ResolveVisual(_actorInstance, _unitSandbox);
            UnitTweenVisual targetVisual = ResolveVisual(_targetInstance, _unitSandbox);
            if (actorVisual == null)
                return;

            Vector3 direction = ResolveTargetPosition();
            _releaseTime = -1f;
            _poseRestoreTime = -1f;
            _impactTime = -1f;
            _blockingTime = -1f;
            _hitTime = -1f;
            _corpseDropTime = -1f;
            _corpseImpactTime = -1f;
            _corpseImpactEndTime = -1f;
            _corpseSettledTime = -1f;
            _deathHandoffTime = -1f;
            _lethalRecoilTime = -1f;
            _lethalShakeTime = -1f;
            _lethalCollapseTime = -1f;
            _poseResolution = "No pose requested";
            if (_presentationGraph != null)
            {
                _previewSequence = BuildPresentationPreview(actorVisual, targetVisual, direction);
            }
            else
            {
            switch (_action)
            {
                case PreviewAction.Idle:
                    _previewSequence = UnitTweenSequenceBuilder.BuildIdle(
                        actorVisual.VisualRoot,
                        _unitSandbox,
                        actorVisual.BasePosition,
                        actorVisual.BaseScale);
                    _previewSequence.SetLoops(1);
                    break;
                case PreviewAction.Move:
                    _previewSequence = UnitTweenSequenceBuilder.BuildMoveLoop(
                        actorVisual.VisualRoot,
                        _unitSandbox,
                        actorVisual.BasePosition,
                        actorVisual.BaseScale,
                        direction);
                    _previewSequence.SetLoops(1);
                    break;
                case PreviewAction.Hit:
                {
                    var hitPlan = UnitTweenSequenceBuilder.BuildHitPlan(
                        actorVisual.VisualRoot,
                        _unitSandbox,
                        actorVisual.BasePosition,
                        actorVisual.BaseRotation,
                        actorVisual.BaseScale,
                        -direction);
                    UnitPoseFamily hitFamily = ResolvePreviewHitFamily(_actorInstance);
                    ApplyPreviewPose(_actorInstance, hitFamily, _facing);
                    hitPlan.Sequence.InsertCallback(0f, () =>
                        ApplyPreviewPose(_actorInstance, hitFamily, _facing));
                    hitPlan.Sequence.InsertCallback(hitPlan.PoseRestoreTime, () =>
                        ClearPreviewPose(_actorInstance, _facing));
                    _previewSequence = hitPlan.Sequence;
                    _poseRestoreTime = hitPlan.PoseRestoreTime;
                    break;
                }
                case PreviewAction.CorpseLanding:
                    _previewSequence = BuildCorpsePreview(actorVisual);
                    break;
                case PreviewAction.LethalHitToCorpse:
                    _previewSequence = BuildLethalDeathPreview(actorVisual);
                    break;
                case PreviewAction.ProjectileOnly:
                    _releaseTime = 0f;
                    _previewSequence = BuildProjectilePreview();
                    _impactTime = ResolvePreviewProjectileDuration();
                    break;
                default:
                    BuildActionPreview(actorVisual, targetVisual, direction);
                    break;
            }
            }

            if (_previewSequence == null)
                return;

            _singleLoopDuration = Mathf.Max(0.01f, _previewSequence.Duration(false));
            _previewSequence.SetAutoKill(false);
            if (_loop)
                _previewSequence.SetLoops(-1, LoopType.Restart);
            DOTweenEditorPreview.PrepareTweenForPreview(_previewSequence, true, true, false);
            _previewSequence.timeScale = _playbackSpeed;
            _previewSequence.Goto(0f, false);
            if (wasPlaying)
                PlayPreview();
            RebuildRetainedTimeline();
            SyncRetainedPreviewControls();
            RequestInteractivePreviewFrame();
        }

        private void BuildActionPreview(
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            UnitVisualAction action = ResolveUnitAction(_action);
            UnitPoseFamily resolvedFamily = ResolvePreviewFamily(_actorInstance, action);
            UnitPoseExitPolicy exitPolicy = resolvedFamily != null
                ? resolvedFamily.ExitPolicy
                : UnitPoseExitPolicy.RecoveryStart;
            UnitTweenActionPlan actionPlan = UnitTweenSequenceBuilder.BuildAction(
                action,
                actorVisual.VisualRoot,
                _unitSandbox,
                actorVisual.BasePosition,
                actorVisual.BaseRotation,
                actorVisual.BaseScale,
                direction,
                exitPolicy);
            _previewSequence = actionPlan.Sequence;
            _releaseTime = actionPlan.ReleaseTime;
            _poseRestoreTime = actionPlan.PoseRestoreTime;
            ApplyPreviewPose(_actorInstance, resolvedFamily, _facing);
            _previewSequence.InsertCallback(0f, () =>
                ApplyPreviewPose(_actorInstance, resolvedFamily, _facing));
            _previewSequence.InsertCallback(_poseRestoreTime, () =>
                ClearPreviewPose(_actorInstance, _facing));

            float projectileDuration = 0f;
            if (UsesProjectile(_action))
            {
                Sequence projectile = BuildProjectilePreview();
                projectileDuration = ResolvePreviewProjectileDuration();
                if (projectile != null)
                    _previewSequence.Insert(_releaseTime, projectile);
                _impactTime = _releaseTime + projectileDuration;
            }

            if (targetVisual == null)
                return;

            float hitTime = UsesProjectile(_action) ? _impactTime : _releaseTime;
            var targetHitPlan = UnitTweenSequenceBuilder.BuildHitPlan(
                targetVisual.VisualRoot,
                _unitSandbox,
                targetVisual.BasePosition,
                targetVisual.BaseRotation,
                targetVisual.BaseScale,
                direction);
            UnitPoseFamily targetHitFamily = ResolvePreviewHitFamily(_targetInstance);
            targetHitPlan.Sequence.InsertCallback(0f, () =>
                ApplyPreviewPose(_targetInstance, targetHitFamily, Opposite(_facing)));
            targetHitPlan.Sequence.InsertCallback(targetHitPlan.PoseRestoreTime, () =>
                ClearPreviewPose(_targetInstance, Opposite(_facing)));
            _previewSequence.Insert(hitTime, targetHitPlan.Sequence);
        }

        private UnitPoseFamily ResolvePreviewFamily(GameObject instance, UnitVisualAction action)
        {
            if (_poseFamily != null)
                return _poseFamily;
            return instance?.GetComponent<FourDirectionSpriteVisual>()?.ActionPoseProfile?
                .ResolveFamily(action);
        }

        private UnitPoseFamily ResolvePreviewHitFamily(GameObject instance)
        {
            return _poseFamily != null
                ? _poseFamily
                : instance?.GetComponent<FourDirectionSpriteVisual>()?.ActionPoseProfile?.HitFamily;
        }

        private void ApplyPreviewPose(GameObject instance, UnitPoseFamily family, FacingDirection facing)
        {
            var directional = instance?.GetComponent<FourDirectionSpriteVisual>();
            if (directional == null || family == null)
            {
                _poseResolution = directional == null ? "No FourDirectionSpriteVisual" : "No family; idle fallback";
                return;
            }

            directional.SetPose(family, facing);
            _poseResolution = $"{family.StableId} -> {directional.LastResolution}";
        }

        private static void ClearPreviewPose(GameObject instance, FacingDirection facing)
        {
            instance?.GetComponent<FourDirectionSpriteVisual>()?.ClearPose(facing);
        }

        private Sequence BuildProjectilePreview()
        {
            ProjectileVisualProfile profile = _projectileSandbox != null
                ? _projectileSandbox
                : CreateTransientProjectileProfile();
            return BuildProjectilePreview(profile, 10f, 0.3f);
        }

        private Sequence BuildProjectilePreview(
            ProjectileVisualProfile profile,
            float speed,
            float fallbackTravelTime)
        {
            _projectileAdapter?.Dispose();
            _projectileAdapter = new ProjectileVisualPreviewAdapter(
                gameObject => _previewUtility?.AddSingleGO(gameObject),
                CreatePlaceholderSprite());
            Renderer sourceRenderer = FindSpriteRenderer(_actorInstance);
            Vector3 direction = ResolveTargetPosition().normalized;
            Vector3 start = _actorInstance.transform.position + Vector3.up * 0.45f + direction * 0.12f;
            Vector3 end = (_targetInstance != null
                ? _targetInstance.transform.position
                : ResolveTargetPosition()) + Vector3.up * 0.45f;
            return _projectileAdapter.Build(
                profile,
                sourceRenderer,
                start,
                end,
                ProjectileVisualFactory.ResolveDuration(
                    Vector3.Distance(start, end),
                    speed,
                    fallbackTravelTime));
        }

        private Sequence BuildPresentationPreview(
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            _previewTimeline.Clear();
            _previewFallbacks.Clear();
            _resolvedPreviewScope = _previewScope.Clone();
            switch (_previewScope.Kind)
            {
                case PresentationPreviewScopeKind.FullScenario when _presentationGraph.HasPreviewScenario:
                    return BuildFullPresentationPreview(actorVisual, targetVisual, direction);
                case PresentationPreviewScopeKind.FullScenario:
                    _previewFallbacks.Add("FullScenarioMissing: default entry was used.");
                    _resolvedPreviewScope = new PresentationPreviewScope
                    {
                        Kind = PresentationPreviewScopeKind.Entry,
                        Cue = ResolveDefaultPreviewCue(_presentationGraph)
                    };
                    return BuildEntryPreview(_resolvedPreviewScope.Cue, actorVisual, targetVisual, direction, -1);
                case PresentationPreviewScopeKind.Phase:
                    if (_previewScope.PhaseIndex < 0 || _previewScope.PhaseIndex >= _presentationGraph.PreviewPhases.Count)
                        throw new InvalidOperationException($"Preview phase {_previewScope.PhaseIndex} does not exist.");
                    return BuildSinglePhasePreview(
                        _presentationGraph.PreviewPhases[_previewScope.PhaseIndex],
                        _previewScope.PhaseIndex,
                        actorVisual,
                        targetVisual,
                        direction);
                case PresentationPreviewScopeKind.Entry:
                    return BuildEntryPreview(_previewScope.Cue, actorVisual, targetVisual, direction, -1);
                case PresentationPreviewScopeKind.Leaf:
                case PresentationPreviewScopeKind.ForkRegion:
                    return BuildNodeScopePreview(actorVisual, targetVisual, direction);
                default:
                    throw new InvalidOperationException($"Unsupported preview scope '{_previewScope.Kind}'.");
            }
        }

        private Sequence BuildEntryPreview(
            PresentationCueKind cue,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction,
            int phaseIndex)
        {
            PresentationEntryNodeRecord entry = _presentationGraph.FindEntry(cue);
            if (entry == null || !entry.Enabled)
                throw new InvalidOperationException($"Preview entry '{cue}' does not exist or is disabled.");
            PresentationPreviewTrack track = BuildPresentationEntryTrack(
                cue,
                actorVisual,
                targetVisual,
                direction,
                phaseIndex,
                0f);
            RecordTrackMarkers(track, 0f);
            return track.Sequence;
        }

        private Sequence BuildNodeScopePreview(
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            PresentationNodeRecord node = _presentationGraph.FindNode(_previewScope.NodeId)
                ?? throw new InvalidOperationException($"Preview node '{_previewScope.NodeId}' does not exist.");
            if (_previewScope.Kind == PresentationPreviewScopeKind.ForkRegion &&
                node is not PresentationForkNodeRecord)
            {
                throw new InvalidOperationException($"Preview node '{_previewScope.NodeId}' is not a Fork.");
            }
            if (_previewScope.Kind == PresentationPreviewScopeKind.Leaf &&
                node is PresentationForkNodeRecord or PresentationEntryNodeRecord or
                    PresentationFinishNodeRecord or PresentationJoinNodeRecord)
            {
                throw new InvalidOperationException("Leaf scope requires an executable leaf node.");
            }
            PresentationExecutionPlan plan = PresentationExecutionPlanCompiler.CompileNodeScope(
                _presentationGraph,
                node.NodeId);
            return BuildPresentationPlanStep(plan.Root, actorVisual, targetVisual, direction, 0f, 0, -1);
        }

        private Sequence BuildFullPresentationPreview(
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            var result = DOTween.Sequence();
            float phaseStart = 0f;
            foreach (PresentationPreviewPhaseRecord phase in _presentationGraph.PreviewPhases)
            {
                int phaseIndex = _presentationGraph.PreviewPhases.IndexOf(phase);
                if (phase == null || phase.Cues == null || phase.Cues.Count == 0)
                    continue;
                phaseStart += AppendPresentationPhase(
                    result, phase, phaseIndex, phaseStart, actorVisual, targetVisual, direction);
            }
            if (result.Duration(false) <= 0f)
                result.AppendInterval(0.01f);
            return result;
        }

        private Sequence BuildSinglePhasePreview(
            PresentationPreviewPhaseRecord phase,
            int phaseIndex,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            var result = DOTween.Sequence();
            AppendPresentationPhase(result, phase, phaseIndex, 0f, actorVisual, targetVisual, direction);
            if (result.Duration(false) <= 0f)
                result.AppendInterval(0.01f);
            return result;
        }

        private float AppendPresentationPhase(
            Sequence result,
            PresentationPreviewPhaseRecord phase,
            int phaseIndex,
            float phaseStart,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            PresentationPreviewTrack continuationTrack = null;
            foreach (PresentationCueKind cue in phase.Cues)
            {
                PresentationPreviewTrack track = BuildPresentationEntryTrack(
                    cue, actorVisual, targetVisual, direction, phaseIndex, phaseStart);
                result.Insert(phaseStart, track.Sequence);
                RecordTrackMarkers(
                    track,
                    phaseStart,
                    phase.AdvanceKind == PresentationPreviewAdvanceKind.Blocking &&
                    cue == phase.ContinuationCue);
                if (cue == phase.ContinuationCue)
                    continuationTrack = track;
            }

            float advance = continuationTrack?.ResolveAdvanceTime(phase.AdvanceKind) ?? 0f;
            if (phase.PlayTargetHitReaction && targetVisual != null)
            {
                float hitOffset = phase.AdvanceKind == PresentationPreviewAdvanceKind.Impact ? advance : 0f;
                Sequence hit = BuildRepresentativeTargetHitPreview(targetVisual, direction);
                result.Insert(phaseStart + hitOffset, hit);
                RecordTimelineMarker("Hit", phaseStart + hitOffset, phaseIndex, 0, null);
                if (_hitTime < 0f)
                    _hitTime = phaseStart + hitOffset;
            }
            RecordTimelineMarker("PhaseAdvance", phaseStart + advance, phaseIndex, 0, phase.AdvanceKind.ToString());
            return advance;
        }

        private PresentationPreviewTrack BuildPresentationEntryTrack(
            PresentationCueKind cue,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction,
            int phaseIndex = -1,
            float timelineOffset = 0f)
        {
            float savedRelease = _releaseTime;
            float savedPoseRestore = _poseRestoreTime;
            float savedImpact = _impactTime;
            float savedBlocking = _blockingTime;
            _releaseTime = -1f;
            _poseRestoreTime = -1f;
            _impactTime = -1f;
            _blockingTime = -1f;

            Sequence sequence;
            PresentationEntryNodeRecord entry = _presentationGraph.FindEntry(cue);
            if (entry == null || !entry.Enabled)
            {
                sequence = DOTween.Sequence().AppendInterval(0.01f);
            }
            else
            {
                PresentationExecutionPlan plan = PresentationExecutionPlanCompiler.Compile(
                    _presentationGraph,
                    cue);
                sequence = BuildPresentationPlanStep(
                    plan.Root,
                    actorVisual,
                    targetVisual,
                    direction,
                    timelineOffset,
                    0,
                    phaseIndex);
            }

            var track = new PresentationPreviewTrack(
                sequence,
                ToTrackLocalTime(_releaseTime, timelineOffset),
                ToTrackLocalTime(_poseRestoreTime, timelineOffset),
                ToTrackLocalTime(_impactTime, timelineOffset),
                ToTrackLocalTime(_blockingTime, timelineOffset));
            _releaseTime = savedRelease;
            _poseRestoreTime = savedPoseRestore;
            _impactTime = savedImpact;
            _blockingTime = savedBlocking;
            return track;
        }

        private static float ToTrackLocalTime(float absoluteTime, float timelineOffset)
        {
            return absoluteTime < 0f ? absoluteTime : absoluteTime - timelineOffset;
        }

        private void RecordTrackMarkers(
            PresentationPreviewTrack track,
            float phaseStart,
            bool includeBlocking = true)
        {
            if (_releaseTime < 0f && track.ReleaseTime >= 0f)
                _releaseTime = phaseStart + track.ReleaseTime;
            if (_poseRestoreTime < 0f && track.PoseRestoreTime >= 0f)
                _poseRestoreTime = phaseStart + track.PoseRestoreTime;
            if (_impactTime < 0f && track.ImpactTime >= 0f)
                _impactTime = phaseStart + track.ImpactTime;
            if (includeBlocking && _blockingTime < 0f && track.BlockingTime >= 0f)
                _blockingTime = phaseStart + track.BlockingTime;
        }

        private Sequence BuildRepresentativeTargetHitPreview(
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            UnitTweenPosePlan hitPlan = UnitTweenSequenceBuilder.BuildHitPlan(
                targetVisual.VisualRoot,
                _unitSandbox,
                targetVisual.BasePosition,
                targetVisual.BaseRotation,
                targetVisual.BaseScale,
                direction);
            UnitPoseFamily targetHitFamily = _targetInstance?
                .GetComponent<FourDirectionSpriteVisual>()?
                .ActionPoseProfile?
                .HitFamily;
            hitPlan.Sequence.InsertCallback(0f, () =>
                ApplyPreviewPose(_targetInstance, targetHitFamily, Opposite(_facing)));
            hitPlan.Sequence.InsertCallback(hitPlan.PoseRestoreTime, () =>
                ClearPreviewPose(_targetInstance, Opposite(_facing)));
            return hitPlan.Sequence;
        }

        private Sequence BuildPresentationPlanStep(
            PresentationPlanStep step,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction,
            float absoluteStart,
            int lane,
            int phaseIndex)
        {
            var result = DOTween.Sequence();
            switch (step)
            {
                case PresentationSequenceStep sequence:
                    foreach (PresentationPlanStep child in sequence.Children)
                    {
                        float childStart = absoluteStart + result.Duration(false);
                        result.Append(BuildPresentationPlanStep(
                            child,
                            actorVisual,
                            targetVisual,
                            direction,
                            childStart,
                            lane,
                            phaseIndex));
                    }
                    break;
                case PresentationParallelStep parallel:
                    for (int branchIndex = 0; branchIndex < parallel.Branches.Count; branchIndex++)
                    {
                        result.Insert(0f, BuildPresentationPlanStep(
                            parallel.Branches[branchIndex],
                            actorVisual,
                            targetVisual,
                            direction,
                            absoluteStart,
                            lane + branchIndex + 1,
                            phaseIndex));
                    }
                    break;
                case PresentationLeafStep leaf:
                    result.Append(BuildPresentationLeaf(
                        leaf.Node,
                        actorVisual,
                        targetVisual,
                        direction,
                        absoluteStart,
                        lane,
                        phaseIndex));
                    break;
            }
            return result;
        }

        private Sequence BuildPresentationLeaf(
            PresentationNodeRecord node,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction,
            float insertionTime,
            int lane,
            int phaseIndex)
        {
            var result = DOTween.Sequence();
            switch (node)
            {
                case PresentationUnitTweenNodeRecord tween:
                {
                    UnitPoseFamily family = ResolvePreviewFamily(_actorInstance, tween.Action);
                    UnitPoseExitPolicy exitPolicy = family != null
                        ? family.ExitPolicy
                        : UnitPoseExitPolicy.RecoveryStart;
                    UnitTweenActionPlan plan = UnitTweenSequenceBuilder.BuildAction(
                        tween.Action,
                        actorVisual.VisualRoot,
                        _unitSandbox,
                        actorVisual.BasePosition,
                        actorVisual.BaseRotation,
                        actorVisual.BaseScale,
                        direction,
                        exitPolicy);
                    plan.Sequence.InsertCallback(0f, () =>
                        ApplyPreviewPose(_actorInstance, family, _facing));
                    plan.Sequence.InsertCallback(plan.PoseRestoreTime, () =>
                        ClearPreviewPose(_actorInstance, _facing));
                    result.Append(plan.Sequence);
                    if (tween.EmitReleaseMarker)
                    {
                        RecordEarliest(ref _releaseTime, insertionTime + plan.ReleaseTime);
                        RecordTimelineMarker("Release", insertionTime + plan.ReleaseTime, phaseIndex, lane, "Release");
                    }
                    RecordEarliest(ref _poseRestoreTime, insertionTime + plan.PoseRestoreTime);
                    RecordTimelineMarker(
                        "PoseRestore",
                        insertionTime + plan.PoseRestoreTime,
                        phaseIndex,
                        lane,
                        null);
                    break;
                }
                case PresentationProjectileNodeRecord projectile:
                {
                    if (projectile.Profile == null)
                        _previewFallbacks.Add($"ProjectileProfileMissing:{projectile.NodeId}:placeholder visual used.");
                    float projectileDuration = ResolvePresentationProjectileDuration(projectile);
                    result.Append(BuildProjectilePreview(
                        projectile.Profile,
                        projectile.Speed,
                        projectile.FallbackTravelTime));
                    if (projectile.EmitImpactMarker)
                    {
                        RecordEarliest(ref _impactTime, insertionTime + projectileDuration);
                        RecordTimelineMarker(
                            "Impact",
                            insertionTime + projectileDuration,
                            phaseIndex,
                            lane,
                            "Impact");
                    }
                    break;
                }
                case PresentationPrefabFxNodeRecord prefabFx:
                    if (prefabFx.Profile?.Prefab == null)
                        _previewFallbacks.Add($"PrefabFxMissing:{prefabFx.NodeId}:empty interval used.");
                    result.Append(BuildPrefabFxPreview(prefabFx.Profile));
                    break;
                case PresentationProceduralVfxNodeRecord procedural:
                {
                    if (procedural.Recipe == null)
                        _previewFallbacks.Add($"RecipeMissing:{procedural.NodeId}:empty interval used.");
                    result.Append(BuildProceduralVfxPreview(procedural));
                    float blockingMarker = procedural.Recipe?.GetLayers(procedural.Cue)
                        ?.Where(layer => layer != null)
                        .Select(layer => layer.BlockingMarker)
                        .DefaultIfEmpty(0f)
                        .Max() ?? 0f;
                    if (blockingMarker > 0f)
                    {
                        _blockingTime = Mathf.Max(_blockingTime, insertionTime + blockingMarker);
                        RecordTimelineMarker(
                            "Blocking",
                            insertionTime + blockingMarker,
                            phaseIndex,
                            lane,
                            "Blocking");
                    }
                    break;
                }
                case PresentationDelayNodeRecord delay:
                    result.AppendInterval(delay.Duration);
                    break;
                case PresentationMarkerNodeRecord marker:
                    if (marker.Marker == PresentationMarkerKind.Release)
                    {
                        RecordEarliest(ref _releaseTime, insertionTime);
                        RecordTimelineMarker("Release", insertionTime, phaseIndex, lane, marker.Marker.ToString());
                    }
                    else if (marker.Marker == PresentationMarkerKind.Impact)
                    {
                        RecordEarliest(ref _impactTime, insertionTime);
                        RecordTimelineMarker("Impact", insertionTime, phaseIndex, lane, marker.Marker.ToString());
                    }
                    break;
            }
            float duration = result.Duration(false);
            _previewTimeline.Add(new PresentationPreviewTimelineEvent
            {
                Event = "NodeStart",
                NodeId = node.NodeId,
                NodeType = node.NodeType.ToString(),
                Time = insertionTime,
                Duration = duration,
                Lane = lane,
                PhaseIndex = phaseIndex
            });
            _previewTimeline.Add(new PresentationPreviewTimelineEvent
            {
                Event = "NodeEnd",
                NodeId = node.NodeId,
                NodeType = node.NodeType.ToString(),
                Time = insertionTime + duration,
                Duration = duration,
                Lane = lane,
                PhaseIndex = phaseIndex
            });
            return result;
        }

        private void RecordTimelineMarker(
            string eventName,
            float time,
            int phaseIndex,
            int lane,
            string marker)
        {
            _previewTimeline.Add(new PresentationPreviewTimelineEvent
            {
                Event = eventName,
                Time = time,
                Lane = lane,
                PhaseIndex = phaseIndex,
                Marker = marker
            });
        }

        private static void RecordEarliest(ref float destination, float value)
        {
            if (destination < 0f || value < destination)
                destination = value;
        }

        private float ResolvePresentationProjectileDuration(PresentationProjectileNodeRecord node)
        {
            Vector3 direction = ResolveTargetPosition().normalized;
            Vector3 start = _actorInstance.transform.position + Vector3.up * 0.45f + direction * 0.12f;
            Vector3 end = (_targetInstance != null
                ? _targetInstance.transform.position
                : ResolveTargetPosition()) + Vector3.up * 0.45f;
            return ProjectileVisualFactory.ResolveDuration(
                Vector3.Distance(start, end),
                node.Speed,
                node.FallbackTravelTime);
        }

        private Sequence BuildPrefabFxPreview(VisualCueProfile profile)
        {
            if (profile?.Prefab == null || _previewUtility == null)
                return DOTween.Sequence().AppendInterval(0.01f);
            GameObject instance = _previewUtility.InstantiatePrefabInScene(profile.Prefab);
            if (instance == null)
                return DOTween.Sequence().AppendInterval(0.01f);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = ResolveVisualCueAnchor(
                profile,
                _actorInstance,
                _targetInstance,
                ResolveTargetPosition());
            Vector3 sourcePosition = ResolveSpriteCenter(
                _actorInstance,
                _actorInstance != null ? _actorInstance.transform.position : Vector3.zero);
            Vector3 targetPosition = ResolveSpriteCenter(
                _targetInstance,
                ResolveTargetPosition());
            instance.transform.rotation = VisualCueTransformUtility.ResolveRotation(
                profile,
                sourcePosition,
                targetPosition);
            instance.transform.localScale = VisualCueTransformUtility.ResolveScale(
                profile,
                sourcePosition,
                targetPosition);
            SpriteRenderer referenceRenderer = FindSpriteRenderer(_targetInstance) ??
                FindSpriteRenderer(_actorInstance);
            int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
            int sortingOrder = (referenceRenderer != null ? referenceRenderer.sortingOrder : 0) +
                profile.SortingOrderOffset;
            TransientVfxPool.ApplySorting(instance, sortingLayerId, sortingOrder);
            _presentationPreviewObjects.Add(instance);
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems)
            {
                system.useAutoRandomSeed = false;
                system.randomSeed = 1u;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            float duration = Mathf.Max(0.05f, profile.Lifetime);
            return DOTween.Sequence().Append(DOTween.To(
                    () => 0f,
                    elapsed => SimulatePrefabFx(systems, elapsed),
                    duration,
                    duration)
                .SetEase(Ease.Linear));
        }

        internal static Vector3 ResolveVisualCueAnchor(
            VisualCueProfile profile,
            GameObject actor,
            GameObject target,
            Vector3 targetPoint)
        {
            if (profile == null)
                return targetPoint;

            return profile.Anchor switch
            {
                VisualCueAnchor.Caster => ResolveSpriteCenter(actor, actor != null
                    ? actor.transform.position
                    : Vector3.zero),
                VisualCueAnchor.PrimaryTarget => ResolveSpriteCenter(target, targetPoint),
                VisualCueAnchor.PrimaryTargetGround => ResolveSpriteGround(target, targetPoint),
                _ => targetPoint
            };
        }

        private static Vector3 ResolveSpriteCenter(GameObject instance, Vector3 fallback)
        {
            SpriteRenderer renderer = FindSpriteRenderer(instance);
            return renderer != null ? renderer.bounds.center : fallback;
        }

        private static Vector3 ResolveSpriteGround(GameObject instance, Vector3 fallback)
        {
            // Unit roots are authored at the logical tile landing point. Sprite bounds
            // include transparent padding below the feet and must not move ground FX.
            return instance != null ? instance.transform.position : fallback;
        }

        private static void SimulatePrefabFx(ParticleSystem[] systems, float elapsed)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                    continue;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Simulate(elapsed, true, true, true);
                system.Pause(true);
            }
        }

        private Sequence BuildProceduralVfxPreview(PresentationProceduralVfxNodeRecord node)
        {
            if (node?.Recipe == null)
                return DOTween.Sequence().AppendInterval(0.01f);
            _proceduralVfxAdapter ??= new ProceduralVfxPreviewAdapter(
                gameObject => _previewUtility?.AddSingleGO(gameObject));
            ResolveProceduralVfxAnchors(
                node.Cue,
                _actorInstance,
                _targetInstance,
                ResolveTargetPosition(),
                out Vector3 source,
                out Vector3 target);
            return _proceduralVfxAdapter.Build(node.Recipe, node.Cue, source, target);
        }

        internal static void ResolveProceduralVfxAnchors(
            SkillVfxCueKind cue,
            GameObject actor,
            GameObject target,
            Vector3 targetPoint,
            out Vector3 source,
            out Vector3 targetPosition)
        {
            Vector3 actorRoot = actor != null ? actor.transform.position : Vector3.zero;
            Vector3 targetRoot = target != null ? target.transform.position : targetPoint;
            targetPosition = targetRoot + Vector3.up * 0.45f;
            source = actorRoot + Vector3.up * 0.45f;
            if (cue != SkillVfxCueKind.DirectionalStrike)
                return;

            Vector3 visualCenter = ResolveSpriteCenter(actor, source);
            Vector3 direction = targetPosition - visualCenter;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            source = visualCenter + direction * 0.10f;
        }

        private void ClearPresentationPreviewObjects()
        {
            _proceduralVfxAdapter?.Dispose();
            _proceduralVfxAdapter = null;
            foreach (GameObject value in _presentationPreviewObjects)
            {
                if (value != null)
                    DestroyImmediate(value);
            }
            _presentationPreviewObjects.Clear();
            _corpseInstance = null;
        }

        private Sequence BuildCorpsePreview(UnitTweenVisual actorVisual)
        {
            var directional = _actorInstance.GetComponent<FourDirectionSpriteVisual>();
            SpriteRenderer sourceRenderer = actorVisual.PrimaryRenderer;
            if (directional?.DeathSprite == null || sourceRenderer == null)
                return null;

            Corpse corpse = CreatePreviewCorpse();
            if (corpse == null)
                return null;
            corpse.InheritSortingFrom(sourceRenderer);
            Sequence sequence = corpse.ApplyVisualForPreview(
                directional.DeathSprite,
                sourceRenderer.sharedMaterial,
                sourceRenderer.color,
                _unitSandbox);
            if (sequence == null)
                return null;

            _actorInstance.SetActive(false);
            _corpseDropTime = 0f;
            _corpseImpactTime = _unitSandbox.CorpseDropDuration;
            _corpseImpactEndTime = _corpseImpactTime + _unitSandbox.CorpseImpactDuration;
            _corpseSettledTime = _corpseImpactEndTime + _unitSandbox.CorpseSettleDuration;
            return sequence;
        }

        private Sequence BuildLethalDeathPreview(UnitTweenVisual actorVisual)
        {
            var directional = _actorInstance.GetComponent<FourDirectionSpriteVisual>();
            SpriteRenderer sourceRenderer = actorVisual.PrimaryRenderer;
            if (directional?.DeathSprite == null || sourceRenderer == null ||
                _targetInstance == null)
            {
                return null;
            }

            Corpse corpse = CreatePreviewCorpse();
            if (corpse == null)
                return null;
            corpse.InheritSortingFrom(sourceRenderer);
            if (!corpse.PrepareVisual(
                    directional.DeathSprite,
                    sourceRenderer.sharedMaterial,
                    sourceRenderer.color,
                    _unitSandbox,
                    false))
            {
                return null;
            }

            ApplyPreviewPose(_actorInstance, ResolvePreviewHitFamily(_actorInstance), _facing);
            Sequence lethalHit = actorVisual.PlayDying(
                _targetInstance.transform.position,
                corpse.ShowPreparedVisual);
            Sequence landing = corpse.BuildLandingSequenceForPreview();
            if (lethalHit == null || landing == null)
                return null;

            _hitTime = -1f;
            _lethalRecoilTime = 0f;
            _lethalShakeTime = _unitSandbox.HitRecoilDuration;
            _lethalCollapseTime = _lethalShakeTime + _unitSandbox.LethalShakeDuration;
            _deathHandoffTime = _lethalCollapseTime + _unitSandbox.LethalCollapseDuration;
            _corpseDropTime = _deathHandoffTime;
            _corpseImpactTime = _corpseDropTime + _unitSandbox.CorpseDropDuration;
            _corpseImpactEndTime = _corpseImpactTime + _unitSandbox.CorpseImpactDuration;
            _corpseSettledTime = _corpseImpactEndTime + _unitSandbox.CorpseSettleDuration;
            return DOTween.Sequence()
                .Append(lethalHit)
                .Append(landing);
        }

        private Corpse CreatePreviewCorpse()
        {
            GameObject corpsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCorpsePrefabPath);
            if (corpsePrefab != null)
            {
                _corpseInstance = _previewUtility.InstantiatePrefabInScene(corpsePrefab);
            }
            else
            {
                _corpseInstance = new GameObject("PreviewCorpse");
                _previewUtility.AddSingleGO(_corpseInstance);
            }

            _corpseInstance.name = "PreviewCorpse";
            _corpseInstance.hideFlags = HideFlags.HideAndDontSave;
            _corpseInstance.transform.position = _actorInstance.transform.position;
            _corpseInstance.transform.rotation = Quaternion.identity;
            _corpseInstance.transform.localScale = Vector3.one;
            _presentationPreviewObjects.Add(_corpseInstance);

            return _corpseInstance.GetComponent<Corpse>() ??
                _corpseInstance.AddComponent<Corpse>();
        }

        private ProjectileVisualProfile CreateTransientProjectileProfile()
        {
            DestroyImmediateSafe(_transientProjectileProfile);
            _transientProjectileProfile = CreateInstance<ProjectileVisualProfile>();
            _transientProjectileProfile.hideFlags = HideFlags.HideAndDontSave;
            return _transientProjectileProfile;
        }

        private float ResolvePreviewProjectileDuration()
        {
            return ProjectileVisualFactory.ResolveDuration(_distanceTiles, 10f, 0.3f);
        }

        private UnitTweenVisual ResolveVisual(GameObject instance, StandardUnitTweenProfile profile)
        {
            if (instance == null)
                return null;

            var visual = instance.GetComponent<UnitTweenVisual>();
            if (visual == null)
                visual = instance.AddComponent<UnitTweenVisual>();
            SpriteRenderer renderer = FindSpriteRenderer(instance);
            Transform visualRoot = ResolvePreviewVisualRoot(visual, renderer);
            visual.ConfigureForPreview(visualRoot, renderer, profile);
            return visual;
        }

        private static Transform ResolvePreviewVisualRoot(
            UnitTweenVisual visual,
            SpriteRenderer renderer)
        {
            Transform authoredRoot = visual?.VisualRoot;
            if (renderer == null)
                return authoredRoot;

            Transform spriteTransform = renderer.transform;
            if (authoredRoot != null &&
                (authoredRoot == spriteTransform || spriteTransform.IsChildOf(authoredRoot)))
            {
                return authoredRoot;
            }

            // Some Pure Run prefabs contain an unrelated sibling named VisualRoot. It must not
            // receive the tween because it does not own the visible Sprite hierarchy.
            return spriteTransform;
        }

        private static SpriteRenderer FindSpriteRenderer(GameObject instance)
        {
            if (instance == null)
                return null;
            foreach (SpriteRenderer renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name == "Sprite")
                    return renderer;
            }
            return null;
        }

        private void SetPreviewObjectState(GameObject instance, Vector3 position, FacingDirection facing)
        {
            if (instance == null)
                return;
            instance.transform.position = position;
            ApplyFacing(instance, facing);
        }

        private static void ApplyFacing(GameObject instance, FacingDirection facing)
        {
            instance?.GetComponent<FourDirectionSpriteVisual>()?.TryApply(facing);
        }

        private Vector3 ResolveTargetPosition()
        {
            Vector3 tileStep = _facing switch
            {
                FacingDirection.East => new Vector3(0.5f, 0.25f, 0f),
                FacingDirection.West => new Vector3(-0.5f, -0.25f, 0f),
                FacingDirection.North => new Vector3(-0.5f, 0.25f, 0f),
                FacingDirection.South => new Vector3(0.5f, -0.25f, 0f),
                _ => new Vector3(0.5f, -0.25f, 0f)
            };
            return tileStep * Mathf.Round(_distanceTiles);
        }

        private static FacingDirection Opposite(FacingDirection facing)
        {
            return facing switch
            {
                FacingDirection.North => FacingDirection.South,
                FacingDirection.East => FacingDirection.West,
                FacingDirection.South => FacingDirection.North,
                FacingDirection.West => FacingDirection.East,
                _ => FacingDirection.South
            };
        }

        private void CreateTileStage()
        {
            Sprite tile = CreateTileSprite();
            Vector3 target = ResolveTargetPosition();
            int distance = Mathf.RoundToInt(_distanceTiles);
            Vector3 step = distance > 0 ? target / distance : Vector3.right * 0.5f;
            for (int index = -1; index <= distance + 1; index++)
            {
                var tileObject = new GameObject($"PreviewTile_{index}");
                var renderer = tileObject.AddComponent<SpriteRenderer>();
                renderer.sprite = tile;
                renderer.color = index % 2 == 0
                    ? new Color(0.44f, 0.41f, 0.37f, 1f)
                    : new Color(0.36f, 0.41f, 0.44f, 1f);
                renderer.sortingOrder = -20;
                tileObject.transform.position = step * index;
                tileObject.hideFlags = HideFlags.HideAndDontSave;
                _previewUtility.AddSingleGO(tileObject);
            }
        }

        private Sprite CreateTileSprite()
        {
            if (_tileSprite != null)
                return _tileSprite;

            _tileTexture = new Texture2D(64, 32, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < 32; y++)
            {
                float halfWidth = y <= 15 ? y * 2f : (31 - y) * 2f;
                for (int x = 0; x < 64; x++)
                {
                    bool inside = Mathf.Abs(x - 31.5f) <= halfWidth;
                    _tileTexture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }
            _tileTexture.Apply();
            _tileSprite = Sprite.Create(
                _tileTexture,
                new Rect(0f, 0f, 64f, 32f),
                new Vector2(0.5f, 0.5f),
                64f);
            _tileSprite.hideFlags = HideFlags.HideAndDontSave;
            return _tileSprite;
        }

        private Sprite CreatePlaceholderSprite()
        {
            if (_placeholderSprite != null)
                return _placeholderSprite;

            _placeholderTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                bool diamond = Mathf.Abs(x - 7.5f) + Mathf.Abs(y - 7.5f) <= 7.5f;
                _placeholderTexture.SetPixel(
                    x,
                    y,
                    diamond ? new Color(1f, 0.2f, 1f, 1f) : Color.clear);
            }
            _placeholderTexture.Apply();
            _placeholderSprite = Sprite.Create(
                _placeholderTexture,
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.5f),
                64f);
            _placeholderSprite.hideFlags = HideFlags.HideAndDontSave;
            return _placeholderSprite;
        }

        private bool TryCommitAssetSelection(
            GameObject actor,
            GameObject target,
            StandardUnitTweenProfile unitProfile,
            ProjectileVisualProfile projectileProfile)
        {
            bool changesProfile = unitProfile != _unitProfile || projectileProfile != _projectileProfile;
            if (changesProfile && !ResolvePendingSandboxChanges())
                return false;

            _actorPrefab = actor;
            _targetPrefab = target;
            if (unitProfile != _unitProfile)
                SetUnitProfile(unitProfile);
            if (projectileProfile != _projectileProfile)
                SetProjectileProfile(projectileProfile);
            return true;
        }

        private bool ResolvePendingSandboxChanges()
        {
            if (!_unitSandboxDirty && !_projectileSandboxDirty)
                return true;

            int choice = EditorUtility.DisplayDialogComplex(
                "Sandbox changes",
                "The selected profile change would replace both preview sandboxes.",
                "Apply All",
                "Cancel",
                "Discard All");
            if (choice == 1)
                return false;
            if (choice == 0)
            {
                ApplyUnitSandbox();
                ApplyProjectileSandbox();
            }
            return true;
        }

        private void SetUnitProfile(StandardUnitTweenProfile profile)
        {
            DestroyImmediateSafe(_unitSandboxEditor);
            DestroyImmediateSafe(_unitSandbox);
            _unitProfile = profile;
            _unitSandbox = profile != null ? Instantiate(profile) : null;
            if (_unitSandbox != null)
            {
                _unitSandbox.hideFlags = HideFlags.HideAndDontSave;
                _unitSandboxEditor = UnityEditor.Editor.CreateEditor(_unitSandbox);
            }
            _unitSandboxDirty = false;
            UpdateUnsavedChangesState();
        }

        private void SetProjectileProfile(ProjectileVisualProfile profile)
        {
            DestroyImmediateSafe(_projectileSandboxEditor);
            DestroyImmediateSafe(_projectileSandbox);
            DestroyImmediateSafe(_transientProjectileProfile);
            _projectileProfile = profile;
            _projectileSandbox = profile != null ? Instantiate(profile) : null;
            if (_projectileSandbox != null)
            {
                _projectileSandbox.hideFlags = HideFlags.HideAndDontSave;
                _projectileSandboxEditor = UnityEditor.Editor.CreateEditor(_projectileSandbox);
            }
            _projectileSandboxDirty = false;
            UpdateUnsavedChangesState();
        }

        private void ApplyUnitSandbox()
        {
            if (_unitProfile == null || _unitSandbox == null || !_unitSandboxDirty)
                return;
            Undo.RecordObject(_unitProfile, "Apply Unit Tween Preview Parameters");
            EditorUtility.CopySerialized(_unitSandbox, _unitProfile);
            EditorUtility.SetDirty(_unitProfile);
            _unitSandboxDirty = false;
            UpdateUnsavedChangesState();
        }

        private void ApplyProjectileSandbox()
        {
            if (_projectileProfile == null || _projectileSandbox == null || !_projectileSandboxDirty)
                return;
            Undo.RecordObject(_projectileProfile, "Apply Projectile Preview Parameters");
            EditorUtility.CopySerialized(_projectileSandbox, _projectileProfile);
            EditorUtility.SetDirty(_projectileProfile);
            _projectileSandboxDirty = false;
            UpdateUnsavedChangesState();
        }

        public override void SaveChanges()
        {
            if (!ApplyWorkbench())
                return;
            UpdateUnsavedChangesState();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            RevertWorkbench();
            UpdateUnsavedChangesState();
            base.DiscardChanges();
        }

        private void UpdateUnsavedChangesState()
        {
            bool graphDirty = _sourceGraph != null && _graphSandbox != null &&
                PresentationAuthoringFacade.Revision(_sourceGraph) !=
                PresentationAuthoringFacade.Revision(_graphSandbox);
            hasUnsavedChanges = graphDirty || _unitSandboxDirty || _projectileSandboxDirty ||
                _dirtyLeafSandboxes.Count > 0 || _pendingLeafPaths.Count > 0;
        }

        private bool CanRenderSelectedProjectile()
        {
            return ProjectileVisualFactory.CanRender(_projectileSandbox);
        }

        private void CreatePreviewUtility()
        {
            _projectileAdapter?.Dispose();
            _projectileAdapter = null;
            if (_previewUtility != null)
                _previewUtility.Cleanup();
            _previewUtility = new PreviewRenderUtility
            {
                ambientColor = Color.white
            };
            _actorInstance = null;
            _targetInstance = null;
            _corpseInstance = null;
        }

        private void CaptureStandingSpriteStates()
        {
            _actorSpriteState = PreviewSpriteState.Capture(FindSpriteRenderer(_actorInstance));
            _targetSpriteState = PreviewSpriteState.Capture(FindSpriteRenderer(_targetInstance));
        }

        private void RestorePreviewVisuals()
        {
            if (_actorInstance != null)
                _actorInstance.SetActive(true);
            RestoreVisual(_actorInstance, _actorSpriteState);
            RestoreVisual(_targetInstance, _targetSpriteState);
        }

        private static void RestoreVisual(GameObject instance, PreviewSpriteState spriteState)
        {
            UnitTweenVisual visual = instance != null ? instance.GetComponent<UnitTweenVisual>() : null;
            visual?.ResetPresentationForPreview();
            spriteState.Restore(FindSpriteRenderer(instance));
        }

        private void Cleanup()
        {
            try
            {
                StopPreview(false);
            }
            finally
            {
                if (_previewUtility != null)
                {
                    _previewUtility.Cleanup();
                    _previewUtility = null;
                }
                _actorInstance = null;
                _targetInstance = null;
                _corpseInstance = null;
                DestroyImmediateSafe(_unitSandboxEditor);
                DestroyImmediateSafe(_projectileSandboxEditor);
                DestroyImmediateSafe(_unitSandbox);
                DestroyImmediateSafe(_projectileSandbox);
                DestroyImmediateSafe(_transientProjectileProfile);
                _transientProjectileProfile = null;
                DestroyImmediateSafe(_tileSprite);
                DestroyImmediateSafe(_tileTexture);
                DestroyImmediateSafe(_placeholderSprite);
                DestroyImmediateSafe(_placeholderTexture);
            }
        }

        private static void DestroyImmediateSafe(Object value)
        {
            if (value != null)
                DestroyImmediate(value);
        }

        private static UnitVisualAction ResolveUnitAction(PreviewAction action)
        {
            return action switch
            {
                PreviewAction.Melee => UnitVisualAction.Melee,
                PreviewAction.Ranged => UnitVisualAction.Ranged,
                PreviewAction.Cast => UnitVisualAction.Cast,
                PreviewAction.RangedWithProjectile => UnitVisualAction.Ranged,
                PreviewAction.CastWithProjectile => UnitVisualAction.Cast,
                _ => UnitVisualAction.None
            };
        }

        private static bool UsesProjectile(PreviewAction action)
        {
            return action is PreviewAction.ProjectileOnly or
                PreviewAction.RangedWithProjectile or
                PreviewAction.CastWithProjectile;
        }

        internal enum PreviewAction
        {
            Idle,
            Move,
            Melee,
            Ranged,
            Cast,
            Hit,
            CorpseLanding,
            LethalHitToCorpse,
            ProjectileOnly,
            RangedWithProjectile,
            CastWithProjectile
        }
    }

    /// <summary>
    /// Captures the authored standing sprite state so corpse preview never leaks into another action.
    /// </summary>
    internal readonly struct PreviewSpriteState
    {
        private PreviewSpriteState(Sprite sprite, bool flipX, Color color)
        {
            Sprite = sprite;
            FlipX = flipX;
            Color = color;
            IsValid = true;
        }

        internal Sprite Sprite { get; }
        internal bool FlipX { get; }
        internal Color Color { get; }
        internal bool IsValid { get; }

        internal static PreviewSpriteState Capture(SpriteRenderer renderer)
        {
            return renderer != null
                ? new PreviewSpriteState(renderer.sprite, renderer.flipX, renderer.color)
                : default;
        }

        internal void Restore(SpriteRenderer renderer)
        {
            if (!IsValid || renderer == null)
                return;
            renderer.sprite = Sprite;
            renderer.flipX = FlipX;
            renderer.color = Color;
        }
    }

    internal sealed class PresentationPreviewTrack
    {
        internal PresentationPreviewTrack(
            Sequence sequence,
            float releaseTime,
            float poseRestoreTime,
            float impactTime,
            float blockingTime)
        {
            Sequence = sequence ?? DOTween.Sequence().AppendInterval(0.01f);
            ReleaseTime = releaseTime;
            PoseRestoreTime = poseRestoreTime;
            ImpactTime = impactTime;
            BlockingTime = blockingTime;
        }

        internal Sequence Sequence { get; }
        internal float ReleaseTime { get; }
        internal float PoseRestoreTime { get; }
        internal float ImpactTime { get; }
        internal float BlockingTime { get; }

        internal float ResolveAdvanceTime(PresentationPreviewAdvanceKind advanceKind)
        {
            float marker = advanceKind switch
            {
                PresentationPreviewAdvanceKind.Release => ReleaseTime,
                PresentationPreviewAdvanceKind.Impact => ImpactTime,
                PresentationPreviewAdvanceKind.Blocking => BlockingTime,
                _ => Sequence.Duration(false)
            };
            return marker >= 0f ? marker : Sequence.Duration(false);
        }
    }
}
#endif
