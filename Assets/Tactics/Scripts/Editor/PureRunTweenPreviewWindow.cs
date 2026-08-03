#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DG.DOTweenEditor;
using DG.Tweening;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Tween;
using UnityEditor;
using UnityEngine;
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
    public sealed class PureRunTweenPreviewWindow : EditorWindow
    {
        private const string DefaultActorPath =
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab";
        private const string DefaultTargetPath =
            "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunGoatCharger.prefab";
        private const string DefaultUnitProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/StandardUnitTweenProfile.asset";
        private const string DefaultProjectileProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/MagicBasic.asset";

        private PreviewRenderUtility _previewUtility;
        private GameObject _actorPrefab;
        private GameObject _targetPrefab;
        private GameObject _actorInstance;
        private GameObject _targetInstance;
        private StandardUnitTweenProfile _unitProfile;
        private StandardUnitTweenProfile _unitSandbox;
        private ProjectileVisualProfile _projectileProfile;
        private ProjectileVisualProfile _projectileSandbox;
        private ProjectileVisualProfile _transientProjectileProfile;
        private BattlePresentationGraph _presentationGraph;
        private PresentationCueKind _presentationCue = PresentationCueKind.Action;
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
        private bool _showParameters = true;
        private float _playbackSpeed = 1f;
        private float _distanceTiles = 3f;
        private float _singleLoopDuration = 1f;
        private float _releaseTime = -1f;
        private float _impactTime = -1f;
        private PreviewAction _action = PreviewAction.Idle;
        private FacingDirection _facing = FacingDirection.South;

        [MenuItem("Tactics/Pure Run/Tween Preview")]
        private static void Open()
        {
            var window = GetWindow<PureRunTweenPreviewWindow>();
            window.titleContent = new GUIContent("Pure Run Tween");
            window.minSize = new Vector2(720f, 620f);
            window.Show();
        }

        internal static void OpenPresentationGraph(BattlePresentationGraph graph)
        {
            var window = GetWindow<PureRunTweenPreviewWindow>();
            window.titleContent = new GUIContent("Presentation Preview");
            window.minSize = new Vector2(720f, 620f);
            window._presentationGraph = graph;
            if (graph != null)
            {
                PresentationEntryNodeRecord firstEntry = graph.Nodes
                    .Find(node => node is PresentationEntryNodeRecord) as PresentationEntryNodeRecord;
                if (firstEntry != null)
                    window._presentationCue = firstEntry.Cue;
            }
            window.RebuildStage();
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = "Apply the Pure Run tween preview sandbox changes before closing?";
            _actorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultActorPath);
            _targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTargetPath);
            SetUnitProfile(AssetDatabase.LoadAssetAtPath<StandardUnitTweenProfile>(DefaultUnitProfilePath));
            SetProjectileProfile(
                AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(DefaultProjectileProfilePath));
            CreatePreviewUtility();
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            Cleanup();
        }

        private void OnGUI()
        {
            DrawAssetControls();
            DrawPlaybackControls();

            Rect previewRect = GUILayoutUtility.GetRect(
                100f,
                2048f,
                300f,
                720f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            DrawPreview(previewRect);
            DrawTimeline();
            DrawSandboxEditors();

            if (_previewSequence != null && _previewSequence.IsActive() && _previewSequence.IsPlaying())
                Repaint();
        }

        private void DrawAssetControls()
        {
            EditorGUILayout.LabelField("Preview Stage", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var actor = (GameObject)EditorGUILayout.ObjectField(
                "Actor Prefab", _actorPrefab, typeof(GameObject), false);
            var target = (GameObject)EditorGUILayout.ObjectField(
                "Target Prefab", _targetPrefab, typeof(GameObject), false);
            var unitProfile = (StandardUnitTweenProfile)EditorGUILayout.ObjectField(
                "Unit Tween Profile", _unitProfile, typeof(StandardUnitTweenProfile), false);
            var projectileProfile = (ProjectileVisualProfile)EditorGUILayout.ObjectField(
                "Projectile Profile", _projectileProfile, typeof(ProjectileVisualProfile), false);
            var presentationGraph = (BattlePresentationGraph)EditorGUILayout.ObjectField(
                "Presentation Graph", _presentationGraph, typeof(BattlePresentationGraph), false);
            if (EditorGUI.EndChangeCheck() &&
                TryCommitAssetSelection(actor, target, unitProfile, projectileProfile))
            {
                _presentationGraph = presentationGraph;
                RebuildStage();
            }

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_presentationGraph == null)
                    _action = (PreviewAction)EditorGUILayout.EnumPopup("Action", _action);
                else
                    _presentationCue = (PresentationCueKind)EditorGUILayout.EnumPopup(
                        "Entry", _presentationCue);
                _facing = (FacingDirection)EditorGUILayout.EnumPopup("Facing", _facing);
                _distanceTiles = EditorGUILayout.Slider("Distance", _distanceTiles, 2f, 6f);
            }
            if (EditorGUI.EndChangeCheck())
                RebuildStage();

            if (UsesProjectile(_action) && !CanRenderSelectedProjectile())
            {
                EditorGUILayout.HelpBox(
                    "Projectile visual is incomplete. Preview uses an editor-only magenta " +
                    "placeholder; runtime remains invisible while preserving travel timing.",
                    MessageType.Warning);
            }
        }

        private void DrawPlaybackControls()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Play", EditorStyles.toolbarButton))
                    PlayPreview();
                if (GUILayout.Button("Pause", EditorStyles.toolbarButton))
                    _previewSequence?.Pause();
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton))
                    StopPreview(true);
                if (GUILayout.Button("Restart", EditorStyles.toolbarButton))
                    RestartPreview();

                GUILayout.Space(12f);
                bool nextLoop = GUILayout.Toggle(_loop, "Loop", EditorStyles.toolbarButton);
                if (nextLoop != _loop)
                {
                    _loop = nextLoop;
                    RebuildSequence(false);
                }

                GUILayout.Label("Speed", GUILayout.Width(42f));
                foreach (float speed in new[] { 0.25f, 0.5f, 1f, 2f })
                {
                    bool selected = Mathf.Approximately(_playbackSpeed, speed);
                    if (GUILayout.Toggle(selected, $"{speed:0.##}x", EditorStyles.toolbarButton) && !selected)
                    {
                        _playbackSpeed = speed;
                        if (_previewSequence != null)
                            _previewSequence.timeScale = speed;
                    }
                }
            }
        }

        private void DrawTimeline()
        {
            float elapsed = 0f;
            if (_previewSequence != null && _previewSequence.IsActive())
            {
                elapsed = _previewSequence.Elapsed(false);
                if (_loop && _singleLoopDuration > 0f)
                    elapsed %= _singleLoopDuration;
                else
                    elapsed = Mathf.Min(elapsed, _singleLoopDuration);
            }

            float normalized = _singleLoopDuration > 0f ? elapsed / _singleLoopDuration : 0f;
            Rect sliderRect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            float next = EditorGUI.Slider(sliderRect, "Timeline", normalized, 0f, 1f);
            if (EditorGUI.EndChangeCheck() && _previewSequence != null && _previewSequence.IsActive())
            {
                _previewSequence.Pause();
                _previewSequence.Goto(next * _singleLoopDuration, false);
                Repaint();
            }
            DrawMarker(sliderRect, _releaseTime, "Release", new Color(0.35f, 0.85f, 1f));
            DrawMarker(sliderRect, _impactTime, "Impact", new Color(1f, 0.55f, 0.25f));
        }

        private void DrawMarker(Rect sliderRect, float markerTime, string label, Color color)
        {
            if (markerTime < 0f || _singleLoopDuration <= 0f)
                return;

            float controlLabelWidth = EditorGUIUtility.labelWidth;
            float availableWidth = sliderRect.width - controlLabelWidth;
            float normalized = Mathf.Clamp01(markerTime / _singleLoopDuration);
            float x = sliderRect.x + controlLabelWidth + availableWidth * normalized;
            EditorGUI.DrawRect(new Rect(x, sliderRect.y + 1f, 2f, sliderRect.height - 2f), color);
            var labelRect = new Rect(
                Mathf.Clamp(x - 24f, sliderRect.x + controlLabelWidth, sliderRect.xMax - 48f),
                sliderRect.yMax,
                52f,
                EditorGUIUtility.singleLineHeight);
            GUI.Label(labelRect, label, EditorStyles.miniLabel);
        }

        private void DrawSandboxEditors()
        {
            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            _showParameters = EditorGUILayout.Foldout(_showParameters, "Sandbox Parameters", true);
            if (!_showParameters)
                return;

            if (_unitSandboxEditor != null)
            {
                EditorGUILayout.LabelField("Unit Profile", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _unitSandboxEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    _unitSandboxDirty = true;
                    UpdateUnsavedChangesState();
                    RebuildSequence(false);
                }
                DrawApplyButtons(
                    _unitSandboxDirty,
                    ApplyUnitSandbox,
                    RevertUnitSandbox);
            }

            if (_projectileSandboxEditor != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Projectile Profile", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _projectileSandboxEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    _projectileSandboxDirty = true;
                    UpdateUnsavedChangesState();
                    RebuildSequence(false);
                }
                DrawApplyButtons(
                    _projectileSandboxDirty,
                    ApplyProjectileSandbox,
                    RevertProjectileSandbox);
            }
        }

        private static void DrawApplyButtons(bool dirty, Action apply, Action revert)
        {
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("Apply"))
                    apply();
                if (GUILayout.Button("Revert"))
                    revert();
            }
        }

        private void DrawPreview(Rect rect)
        {
            rect.width = Mathf.Clamp(rect.width, 1f, 2048f);
            rect.height = Mathf.Clamp(rect.height, 1f, 1024f);
            if (_previewUtility == null)
                CreatePreviewUtility();
            if (_actorInstance == null)
                RebuildStage();
            if (_previewUtility == null)
                return;

            Vector3 stageCenter = ResolveTargetPosition() * 0.5f;
            float verticalSpan = Mathf.Abs(ResolveTargetPosition().y) + 1.3f;
            _previewUtility.BeginPreview(rect, GUIStyle.none);
            _previewUtility.camera.orthographic = true;
            _previewUtility.camera.orthographicSize = Mathf.Max(1.55f, verticalSpan * 0.65f);
            _previewUtility.camera.transform.position = new Vector3(stageCenter.x, stageCenter.y + 0.55f, -10f);
            _previewUtility.camera.transform.rotation = Quaternion.identity;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.backgroundColor = new Color(0.08f, 0.075f, 0.08f, 1f);
            _previewUtility.Render(true, false);
            _previewUtility.EndAndDrawPreview(rect);
        }

        private void PlayPreview()
        {
            if (_previewSequence == null || !_previewSequence.IsActive())
                RebuildSequence(false);
            if (_previewSequence == null)
                return;

            _previewSequence.timeScale = _playbackSpeed;
            _previewSequence.Play();
            DOTweenEditorPreview.Start(Repaint);
        }

        private void RestartPreview()
        {
            if (_previewSequence == null || !_previewSequence.IsActive())
                RebuildSequence(false);
            _previewSequence?.Restart();
            if (_previewSequence == null)
                return;

            _previewSequence.timeScale = _playbackSpeed;
            DOTweenEditorPreview.Start(Repaint);
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
            Repaint();
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
            CaptureStandingSpriteStates();
            UnitTweenVisual actorVisual = ResolveVisual(_actorInstance, _unitSandbox);
            UnitTweenVisual targetVisual = ResolveVisual(_targetInstance, _unitSandbox);
            if (actorVisual == null)
                return;

            Vector3 direction = ResolveTargetPosition();
            _releaseTime = -1f;
            _impactTime = -1f;
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
                    _previewSequence = UnitTweenSequenceBuilder.BuildHit(
                        actorVisual.VisualRoot,
                        _unitSandbox,
                        actorVisual.BasePosition,
                        actorVisual.BaseRotation,
                        actorVisual.BaseScale,
                        -direction);
                    break;
                case PreviewAction.CorpseLanding:
                    _previewSequence = BuildCorpsePreview(actorVisual);
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
            Repaint();
        }

        private void BuildActionPreview(
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction)
        {
            UnitVisualAction action = ResolveUnitAction(_action);
            UnitTweenActionPlan actionPlan = UnitTweenSequenceBuilder.BuildAction(
                action,
                actorVisual.VisualRoot,
                _unitSandbox,
                actorVisual.BasePosition,
                actorVisual.BaseRotation,
                actorVisual.BaseScale,
                direction);
            _previewSequence = actionPlan.Sequence;
            _releaseTime = actionPlan.ReleaseTime;

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
            _previewSequence.Insert(
                hitTime,
                UnitTweenSequenceBuilder.BuildHit(
                    targetVisual.VisualRoot,
                    _unitSandbox,
                    targetVisual.BasePosition,
                    targetVisual.BaseRotation,
                    targetVisual.BaseScale,
                    direction));
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
            PresentationEntryNodeRecord entry = _presentationGraph.FindEntry(_presentationCue);
            if (entry == null)
                return DOTween.Sequence().AppendInterval(0.01f);
            var visited = new HashSet<string>();
            return BuildPresentationPath(
                entry.NodeId,
                null,
                actorVisual,
                targetVisual,
                direction,
                visited);
        }

        private Sequence BuildPresentationPath(
            string sourceNodeId,
            string stopBeforeNodeId,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction,
            HashSet<string> visited,
            bool firstIdIsNode = false)
        {
            var result = DOTween.Sequence();
            string currentId = sourceNodeId;
            while (true)
            {
                PresentationNodeRecord node;
                if (firstIdIsNode)
                {
                    node = _presentationGraph.FindNode(currentId);
                    firstIdIsNode = false;
                }
                else
                {
                    List<PresentationEdgeRecord> edges = _presentationGraph.GetEdgesFrom(currentId);
                    if (edges.Count == 0)
                        break;
                    node = _presentationGraph.FindNode(edges[0].TargetNodeId);
                }
                if (node == null || node.NodeId == stopBeforeNodeId || node is PresentationFinishNodeRecord)
                    break;
                if (!visited.Add(node.NodeId))
                    break;

                float insertionTime = result.Duration(false);
                switch (node)
                {
                    case PresentationUnitTweenNodeRecord tween:
                    {
                        UnitTweenActionPlan plan = UnitTweenSequenceBuilder.BuildAction(
                            tween.Action,
                            actorVisual.VisualRoot,
                            _unitSandbox,
                            actorVisual.BasePosition,
                            actorVisual.BaseRotation,
                            actorVisual.BaseScale,
                            direction);
                        result.Append(plan.Sequence);
                        if (tween.EmitReleaseMarker)
                            _releaseTime = insertionTime + plan.ReleaseTime;
                        break;
                    }
                    case PresentationProjectileNodeRecord projectile:
                    {
                        float duration = ResolvePresentationProjectileDuration(projectile);
                        result.Append(BuildProjectilePreview(
                            projectile.Profile,
                            projectile.Speed,
                            projectile.FallbackTravelTime));
                        if (projectile.EmitImpactMarker)
                            _impactTime = insertionTime + duration;
                        break;
                    }
                    case PresentationPrefabFxNodeRecord prefabFx:
                        result.Append(BuildPrefabFxPreview(prefabFx.Profile));
                        break;
                    case PresentationProceduralVfxNodeRecord procedural:
                        result.Append(BuildProceduralVfxPreview(procedural));
                        break;
                    case PresentationDelayNodeRecord delay:
                        result.AppendInterval(delay.Duration);
                        break;
                    case PresentationMarkerNodeRecord marker:
                        if (marker.Marker == PresentationMarkerKind.Release)
                            _releaseTime = insertionTime;
                        else if (marker.Marker == PresentationMarkerKind.Impact)
                            _impactTime = insertionTime;
                        break;
                    case PresentationForkNodeRecord fork:
                    {
                        var parallel = DOTween.Sequence();
                        foreach (PresentationEdgeRecord branch in _presentationGraph.GetEdgesFrom(fork.NodeId))
                        {
                            var branchVisited = new HashSet<string>(visited);
                            Sequence branchSequence = BuildPresentationBranch(
                                branch.TargetNodeId,
                                fork.JoinNodeId,
                                actorVisual,
                                targetVisual,
                                direction,
                                branchVisited);
                            parallel.Insert(0f, branchSequence);
                        }
                        result.Append(parallel);
                        PresentationNodeRecord join = _presentationGraph.FindNode(fork.JoinNodeId);
                        currentId = join?.NodeId;
                        if (string.IsNullOrEmpty(currentId))
                            return result;
                        continue;
                    }
                }
                currentId = node.NodeId;
            }
            return result;
        }

        private Sequence BuildPresentationBranch(
            string firstNodeId,
            string joinNodeId,
            UnitTweenVisual actorVisual,
            UnitTweenVisual targetVisual,
            Vector3 direction,
            HashSet<string> visited)
        {
            var shim = DOTween.Sequence();
            PresentationNodeRecord first = _presentationGraph.FindNode(firstNodeId);
            if (first == null || first.NodeId == joinNodeId)
                return shim;
            return BuildPresentationPath(
                firstNodeId,
                joinNodeId,
                actorVisual,
                targetVisual,
                direction,
                visited,
                true);
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
            instance.transform.localScale = Vector3.one * profile.Scale;
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
            Vector3 source = _actorInstance.transform.position + Vector3.up * 0.45f;
            Vector3 target = (_targetInstance != null
                ? _targetInstance.transform.position
                : ResolveTargetPosition()) + Vector3.up * 0.45f;
            return _proceduralVfxAdapter.Build(node.Recipe, node.Cue, source, target);
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
        }

        private Sequence BuildCorpsePreview(UnitTweenVisual actorVisual)
        {
            var directional = _actorInstance.GetComponent<FourDirectionSpriteVisual>();
            if (directional?.DeathSprite != null && actorVisual.PrimaryRenderer != null)
            {
                actorVisual.PrimaryRenderer.sprite = directional.DeathSprite;
                actorVisual.PrimaryRenderer.flipX = false;
            }

            return UnitTweenSequenceBuilder.BuildCorpseLanding(
                actorVisual.VisualRoot,
                _unitSandbox,
                actorVisual.BasePosition,
                actorVisual.BaseRotation,
                actorVisual.BaseScale);
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

        private void RevertUnitSandbox()
        {
            SetUnitProfile(_unitProfile);
            RebuildSequence(false);
        }

        private void RevertProjectileSandbox()
        {
            SetProjectileProfile(_projectileProfile);
            RebuildSequence(false);
        }

        public override void SaveChanges()
        {
            ApplyUnitSandbox();
            ApplyProjectileSandbox();
            UpdateUnsavedChangesState();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            SetUnitProfile(_unitProfile);
            SetProjectileProfile(_projectileProfile);
            RebuildSequence(false);
            base.DiscardChanges();
        }

        private void UpdateUnsavedChangesState()
        {
            hasUnsavedChanges = _unitSandboxDirty || _projectileSandboxDirty;
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
        }

        private void CaptureStandingSpriteStates()
        {
            _actorSpriteState = PreviewSpriteState.Capture(FindSpriteRenderer(_actorInstance));
            _targetSpriteState = PreviewSpriteState.Capture(FindSpriteRenderer(_targetInstance));
        }

        private void RestorePreviewVisuals()
        {
            RestoreVisual(_actorInstance, _actorSpriteState);
            RestoreVisual(_targetInstance, _targetSpriteState);
        }

        private static void RestoreVisual(GameObject instance, PreviewSpriteState spriteState)
        {
            UnitTweenVisual visual = instance != null ? instance.GetComponent<UnitTweenVisual>() : null;
            visual?.StopAllVisualTweens();
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
}
#endif
