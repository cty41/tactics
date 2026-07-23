using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Players;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.UI;
using Tactics.Common.Utilities;
using Tactics.Cells;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Tactics.Common.Testing.Gameplay
{
    /// <summary>
    /// Drives production input paths with virtual Input System devices. This adapter
    /// resolves semantic targets but never mutates product state directly.
    /// </summary>
    public sealed class PlayerInputGameplayStepAdapter : IGameplayStepAdapter
    {
        private const string PlayerInputAdapterName = "PlayerInput";
        private static int _totalInitializationCount;

        public string AdapterName => PlayerInputAdapterName;
        public static int TotalInitializationCount => System.Threading.Volatile.Read(ref _totalInitializationCount);
        public static bool HasVirtualTestDevices => InputSystem.devices.Any(device =>
            device.name is "GameplayTestMouse" or "GameplayTestKeyboard");

        public bool CanExecute(ExecutableScenarioAction action)
        {
            return action.Kind is "initializePlayerInput"
                or "movePointerToTarget"
                or "clickPointerTarget"
                or "rightClickPointerTarget"
                or "pressInputKey"
                or "waitForPlayerObservable"
                or "playBattleThroughInput";
        }

        public async Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            try
            {
                return action.Kind switch
                {
                    "initializePlayerInput" => await InitializePlayerInput(context, action),
                    "movePointerToTarget" => await MovePointerToTarget(context, action),
                    "clickPointerTarget" => await ClickPointerTarget(context, action, PointerButton.Left),
                    "rightClickPointerTarget" => await ClickPointerTarget(context, action, PointerButton.Right),
                    "pressInputKey" => await PressInputKey(context, action),
                    "waitForPlayerObservable" => await WaitForPlayerObservable(context, action),
                    "playBattleThroughInput" => await PlayBattleThroughInput(context, action),
                    _ => GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, $"Unsupported PlayerInput action '{action.Kind}'.")
                };
            }
            catch (Exception ex)
            {
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, ex.Message);
            }
        }

        public bool CanAssert(ExecutableScenarioAssertion assertion) => false;

        public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
        {
            return Task.FromResult(GameplayAssertionResult.Fail(
                PlayerInputAdapterName,
                assertion.Kind,
                "PlayerInput exposes actions only; use read-only adapters for assertions."));
        }

        public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
        {
            return new ProbeSnapshot
            {
                Adapter = PlayerInputAdapterName,
                Kind = request.Kind,
                Target = request.Target,
                Data = new JObject()
            };
        }

        private static async Task<GameplayStepResult> InitializePlayerInput(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            if (context.PlayerInputMouse != null || context.PlayerInputKeyboard != null)
                return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, "Virtual player input is already initialized.");

            var mouse = InputSystem.AddDevice<Mouse>("GameplayTestMouse");
            var keyboard = InputSystem.AddDevice<Keyboard>("GameplayTestKeyboard");
            foreach (var physicalMouse in InputSystem.devices.OfType<Mouse>()
                         .Where(device => device != mouse && device.enabled)
                         .ToList())
            {
                var device = physicalMouse;
                InputSystem.DisableDevice(device);
                context.OwnedCleanupActions.Add(() => InputSystem.EnableDevice(device));
            }
            foreach (var physicalKeyboard in InputSystem.devices.OfType<Keyboard>()
                         .Where(device => device != keyboard && device.enabled)
                         .ToList())
            {
                var device = physicalKeyboard;
                InputSystem.DisableDevice(device);
                context.OwnedCleanupActions.Add(() => InputSystem.EnableDevice(device));
            }
            mouse.MakeCurrent();
            keyboard.MakeCurrent();

            context.PlayerInputMouse = mouse;
            context.PlayerInputKeyboard = keyboard;
            context.OwnedInputDevices.Add(mouse);
            context.OwnedInputDevices.Add(keyboard);
            System.Threading.Interlocked.Increment(ref _totalInitializationCount);

            EnsureInputSystemUiModule(context);
            if (!await WaitForInputFrame(context) || !await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Virtual player input initialization was cancelled.");
            return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, "Initialized virtual Mouse and Keyboard devices.");
        }

        private static void EnsureInputSystemUiModule(GameplayRuntimeContext context)
        {
            var existingModule = UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None)
                .FirstOrDefault(module => module != null && module.isActiveAndEnabled);
            if (existingModule != null)
            {
                var existingEventSystem = existingModule.GetComponent<EventSystem>();
                if (existingEventSystem != null)
                {
                    existingEventSystem.enabled = true;
                    EventSystem.current = existingEventSystem;
                }
                if (existingModule.actionsAsset == null)
                {
                    existingModule.AssignDefaultActions();
                    context.OwnedCleanupActions.Add(existingModule.UnassignActions);
                }
                return;
            }

            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("GameplayTestInputEventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                var createdModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                createdModule.AssignDefaultActions();
                context.OwnedCleanupActions.Add(createdModule.UnassignActions);
                context.OwnedRuntimeGameObjects.Add(eventSystemObject);
                EventSystem.current = eventSystem;
                return;
            }

            var module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            context.OwnedCleanupActions.Add(module.UnassignActions);
            context.OwnedRuntimeComponents.Add(module);
            EventSystem.current = eventSystem;
        }

        private static async Task<GameplayStepResult> MovePointerToTarget(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            if (action.Parameters["targetKind"]?.ToString() == "MapNode")
            {
                var scrollResult = await ScrollMapNodeIntoViewThroughInput(context, action);
                if (!scrollResult.Passed)
                    return scrollResult;
            }

            if (!TryResolveScreenPosition(context, action, out var position, out var description, out var failure))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, failure);

            if (context.PlayerInputMouse == null || !context.PlayerInputMouse.added)
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "initializePlayerInput must run before pointer actions.");

            InputSystem.QueueStateEvent(context.PlayerInputMouse, new MouseState { position = position });
            if (!await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Pointer movement cancelled.");
            Vector2 actualPosition = context.PlayerInputMouse.position.ReadValue();
            if (Vector2.Distance(actualPosition, position) > 1f)
            {
                return GameplayStepResult.Fail(
                    PlayerInputAdapterName,
                    action.Kind,
                    $"Pointer movement resolved {description} to {position}, but the device reported {actualPosition}.");
            }
            return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Moved pointer to {description} at {position}.");
        }

        private static async Task<GameplayStepResult> ScrollMapNodeIntoViewThroughInput(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            if (context.PlayerInputMouse == null || !context.PlayerInputMouse.added)
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "initializePlayerInput must run before pointer actions.");

            string elementName = ResolveMapNodeElementName(action, ResolveLocator(action));
            if (string.IsNullOrWhiteSpace(elementName))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "No reachable map node is available.");
            if (TryResolveUiScreenPosition(elementName, out _, out _))
                return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Map node '{elementName}' is already visible.");

            var target = FindActiveElement(elementName);
            var scrollView = FindActiveElement("MapScrollView");
            if (target?.panel == null || scrollView?.panel == null || target.panel != scrollView.panel)
            {
                return GameplayStepResult.Fail(
                    PlayerInputAdapterName,
                    action.Kind,
                    $"Map node '{elementName}' cannot be brought into view because MapScrollView is unavailable.");
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Rect viewport = scrollView.worldBound;
                Rect targetBounds = target.worldBound;
                Vector2 panelStart = viewport.center;
                float horizontalStep = Mathf.Sign(targetBounds.center.x - viewport.center.x) * viewport.width * 0.35f;
                float verticalStep = Mathf.Sign(targetBounds.center.y - viewport.center.y) * viewport.height * 0.35f;
                if (Mathf.Abs(targetBounds.center.x - viewport.center.x) <= viewport.width * 0.35f)
                    horizontalStep = 0f;
                if (Mathf.Abs(targetBounds.center.y - viewport.center.y) <= viewport.height * 0.35f)
                    verticalStep = 0f;

                if (Mathf.Approximately(horizontalStep, 0f) && Mathf.Approximately(verticalStep, 0f))
                    break;

                Vector2 panelEnd = new Vector2(
                    panelStart.x - horizontalStep,
                    panelStart.y - verticalStep);
                float scale = scrollView.panel.scaledPixelsPerPoint;
                Vector2 deviceStart = new Vector2(panelStart.x * scale, Screen.height - panelStart.y * scale);
                Vector2 deviceEnd = new Vector2(panelEnd.x * scale, Screen.height - panelEnd.y * scale);

                InputSystem.QueueStateEvent(context.PlayerInputMouse, new MouseState { position = deviceStart });
                if (!await WaitForInputFrame(context))
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Map scroll pointer movement cancelled.");

                InputSystem.QueueStateEvent(
                    context.PlayerInputMouse,
                    new MouseState { position = deviceStart }
                        .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));
                if (!await WaitForInputFrame(context))
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Map scroll pointer press cancelled.");

                InputSystem.QueueStateEvent(
                    context.PlayerInputMouse,
                    new MouseState { position = deviceEnd }
                        .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));
                if (!await WaitForInputFrame(context))
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Map scroll pointer drag cancelled.");

                InputSystem.QueueStateEvent(context.PlayerInputMouse, new MouseState { position = deviceEnd });
                if (!await WaitForInputFrame(context) || !await WaitForInputFrame(context))
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Map scroll pointer release cancelled.");

                if (TryResolveUiScreenPosition(elementName, out _, out _))
                    return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Scrolled map node '{elementName}' into view through pointer input.");
            }

            TryResolveUiScreenPosition(elementName, out _, out string failure);
            return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, failure);
        }

        private static async Task<GameplayStepResult> ClickPointerTarget(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action,
            PointerButton button)
        {
            var moveResult = await MovePointerToTarget(context, action);
            if (!moveResult.Passed)
                return moveResult;

            VisualElement observedElement = null;
            bool observedPointerDown = false;
            bool observedClick = false;
            EventCallback<PointerDownEvent> pointerDownObserver = _ => observedPointerDown = true;
            EventCallback<ClickEvent> clickObserver = _ => observedClick = true;
            string targetKind = action.Parameters["targetKind"]?.ToString();
            if (targetKind is "UiElement" or "MapNode")
            {
                string elementName = targetKind == "MapNode"
                    ? ResolveMapNodeElementName(action, ResolveLocator(action))
                    : ResolveUiElementName(ResolveLocator(action));
                observedElement = FindActiveElement(elementName);
                observedElement?.RegisterCallback(pointerDownObserver, TrickleDown.TrickleDown);
                observedElement?.RegisterCallback(clickObserver, TrickleDown.TrickleDown);
            }
            void UnregisterPointerObservers()
            {
                observedElement?.UnregisterCallback(pointerDownObserver, TrickleDown.TrickleDown);
                observedElement?.UnregisterCallback(clickObserver, TrickleDown.TrickleDown);
            }

            Vector2 pointerPosition = context.PlayerInputMouse.position.ReadValue();
            var pressedState = new MouseState { position = pointerPosition }
                .WithButton(
                    button == PointerButton.Left
                        ? UnityEngine.InputSystem.LowLevel.MouseButton.Left
                        : UnityEngine.InputSystem.LowLevel.MouseButton.Right,
                    true);
            InputSystem.QueueStateEvent(context.PlayerInputMouse, pressedState);
            if (!await WaitForInputFrame(context))
            {
                UnregisterPointerObservers();
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Pointer press cancelled.");
            }
            InputSystem.QueueStateEvent(
                context.PlayerInputMouse,
                new MouseState { position = pointerPosition });
            if (!await WaitForInputFrame(context))
            {
                UnregisterPointerObservers();
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Pointer release cancelled.");
            }

            UnregisterPointerObservers();
            if (observedElement != null && !observedPointerDown && !observedClick)
            {
                return GameplayStepResult.Fail(
                    PlayerInputAdapterName,
                    action.Kind,
                    $"Pointer input reached device position {pointerPosition}, but UI element '{observedElement.name}' received no pointer event.");
            }

            return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Clicked '{ResolveLocator(action)}' with {button} button.");
        }

        private static async Task<GameplayStepResult> PressInputKey(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            string keyName = action.Parameters["key"]?.ToString();
            if (!Enum.TryParse(keyName, true, out Key key) || key == Key.None)
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, $"Unknown input key '{keyName}'.");
            if (context.PlayerInputKeyboard == null || !context.PlayerInputKeyboard.added)
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "initializePlayerInput must run before keyboard actions.");

            InputSystem.QueueStateEvent(context.PlayerInputKeyboard, new KeyboardState(key));
            if (!await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Key press cancelled.");
            InputSystem.QueueStateEvent(context.PlayerInputKeyboard, new KeyboardState());
            if (!await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Key release cancelled.");
            return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Pressed input key '{key}'.");
        }

        private static async Task<GameplayStepResult> WaitForPlayerObservable(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            string observable = action.Parameters["observable"]?.ToString();
            int maximumFrames = Math.Max(1, action.Parameters["maximumFrames"]?.ToObject<int>() ?? 180);

            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (context.RuntimeScope?.IsCancelling == true)
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, $"Observable '{observable}' wait cancelled.");

                if (IsObservableSatisfied(action, observable))
                    return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Observable '{observable}' satisfied after {frame} frames.");

                if (!await WaitForInputFrame(context))
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, $"Observable '{observable}' wait cancelled.");
            }

            return GameplayStepResult.Fail(
                PlayerInputAdapterName,
                action.Kind,
                $"Observable '{observable}' was not satisfied after {maximumFrames} frames in scene '{SceneManager.GetActiveScene().name}'. " +
                DescribeObservableState());
        }

        private static string DescribeObservableState()
        {
            string visibleUi = UIManager.Instance == null
                ? "UIManager unavailable"
                : string.Join(
                    ",",
                    Enum.GetValues(typeof(UIManager.UIId))
                        .Cast<UIManager.UIId>()
                        .Where(UIManager.Instance.IsVisible));
            string activeElements = string.Join(
                ",",
                FindActiveElements()
                    .Select(element => element.name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .Take(30));
            return $"VisibleUI=[{visibleUi}], ActiveElements=[{activeElements}], " +
                $"SettlementPhase={BattleSettlementCoordinator.Instance.CurrentPhase}, " +
                $"SettlementVictory={BattleSettlementCoordinator.Instance.IsPlayerVictory}, " +
                $"ActiveRun={RoguelikeMapRuntimeState.HasActiveRun}, " +
                $"PendingBattleNode={RoguelikeMapRuntimeState.PendingBattleNodeId ?? "<none>"}.";
        }

        private static bool IsObservableSatisfied(ExecutableScenarioAction action, string observable)
        {
            switch (observable)
            {
                case "uiElement":
                    var element = FindActiveElement(ResolveUiElementName(
                        action.Target ?? action.Parameters["elementName"]?.ToString()));
                    bool requiresInteractable = action.Parameters["interactable"]?.ToObject<bool>() == true;
                    return IsElementLayoutReady(element) &&
                        (!requiresInteractable || element.enabledInHierarchy);
                case "uiVisible":
                    return TryParseUiId(action, out var visibleUiId) && UIManager.Instance.IsVisible(visibleUiId);
                case "uiHidden":
                    return TryParseUiId(action, out var hiddenUiId) && !UIManager.Instance.IsVisible(hiddenUiId);
                case "mapReady":
                    return UIManager.Instance.IsVisible(UIManager.UIId.RoguelikeMap) &&
                        FindActiveElement("InventoryButton") != null &&
                        FindActiveElements().Any(element =>
                            element.name?.StartsWith("MapNode_", StringComparison.Ordinal) == true &&
                            IsElementLayoutReady(element));
                case "battleReady":
                    return ResolveBattleController(null)?.IsBattleActive == true &&
                        IsElementLayoutReady(FindActiveElement("EndTurnButton"));
                case "humanTurn":
                    var battleController = ResolveBattleController(null);
                    return battleController?.IsBattleActive == true &&
                        battleController.TurnContext.CurrentPlayer?.PlayerType == PlayerType.HumanPlayer &&
                        battleController.GridState is GridStateAwaitInput &&
                        FindActiveElement("EndTurnButton")?.enabledInHierarchy == true;
                case "battleEnded":
                    return ResolveBattleController(null)?.IsBattleActive != true;
                default:
                    return false;
            }
        }

        private static bool TryParseUiId(ExecutableScenarioAction action, out UIManager.UIId uiId)
        {
            return Enum.TryParse(action.Parameters["uiId"]?.ToString(), true, out uiId);
        }

        private static bool TryResolveScreenPosition(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action,
            out Vector2 screenPosition,
            out string description,
            out string failure)
        {
            string targetKind = action.Parameters["targetKind"]?.ToString() ?? "UiElement";
            string locator = ResolveLocator(action);
            description = $"{targetKind} '{locator}'";

            if (targetKind is "UiElement" or "MapNode")
            {
                string elementName = targetKind == "MapNode"
                    ? ResolveMapNodeElementName(action, locator)
                    : ResolveUiElementName(locator);
                if (string.IsNullOrWhiteSpace(elementName))
                {
                    screenPosition = default;
                    failure = $"No reachable map node matched locator '{locator}' and parameters {action.Parameters}.";
                    return false;
                }
                return TryResolveUiScreenPosition(elementName, out screenPosition, out failure);
            }

            if (targetKind == "BattleUnit")
            {
                var unit = ResolveBattleUnit(context, locator);
                if (unit is not Component component)
                {
                    screenPosition = default;
                    failure = $"BattleUnit '{locator}' could not be resolved from the active production battle.";
                    return false;
                }

                Vector3 pointerWorldPosition = unit.CurrentCell != null
                    ? ResolveBattleCellPointerWorldPosition(context, unit.CurrentCell)
                    : component.transform.position;
                return TryResolveWorldScreenPosition(pointerWorldPosition, description, out screenPosition, out failure);
            }

            if (targetKind == "BattleCell")
            {
                var cell = ResolveBattleCell(context, action, locator);
                if (cell == null)
                {
                    screenPosition = default;
                    failure = $"BattleCell '{locator}' could not be resolved from the active production battle.";
                    return false;
                }

                var worldPosition = ResolveBattleCellPointerWorldPosition(context, cell);
                return TryResolveWorldScreenPosition(worldPosition, description, out screenPosition, out failure);
            }

            screenPosition = default;
            failure = $"Unsupported pointer target kind '{targetKind}'.";
            return false;
        }

        private static string ResolveMapNodeElementName(ExecutableScenarioAction action, string locator)
        {
            if (!string.IsNullOrWhiteSpace(locator) &&
                !locator.Equals("Reachable", StringComparison.OrdinalIgnoreCase))
            {
                return locator.StartsWith("MapNode_", StringComparison.Ordinal)
                    ? locator
                    : $"MapNode_{locator}";
            }

            var map = RoguelikeMapRuntimeState.CurrentMap;
            if (map?.nodes == null)
                return null;

            IEnumerable<RoguelikeMapNode> typedNodes = map.nodes.Where(node => node != null);
            string nodeTypeName = action.Parameters["nodeType"]?.ToString();
            if (!string.IsNullOrWhiteSpace(nodeTypeName))
            {
                if (nodeTypeName.Equals("Battle", StringComparison.OrdinalIgnoreCase))
                {
                    typedNodes = typedNodes.Where(global::Tactics.RoguelikeMap.RoguelikeMap.IsBattleNode);
                }
                else
                {
                    if (!Enum.TryParse(nodeTypeName, true, out RoguelikeNodeType nodeType))
                        return null;
                    typedNodes = typedNodes.Where(node => node.nodeType == nodeType);
                }
            }

            var typedNodeList = typedNodes.ToList();
            IEnumerable<RoguelikeMapNode> candidates = typedNodeList
                .Where(node => node.IsReachable && !node.IsConsumed);
            int reachableIndex = Math.Max(0, action.Parameters["reachableIndex"]?.ToObject<int>() ?? 0);
            var selected = candidates
                .OrderBy(node => node.LayerIndex)
                .ThenBy(node => node.nodeId, StringComparer.Ordinal)
                .Skip(reachableIndex)
                .FirstOrDefault();
            if (selected != null)
                return $"MapNode_{selected.nodeId}";

            // The map panel can finish layout one frame before the runtime-state
            // singleton publishes its repaired reachability flags. The rendered node's
            // picking mode is the production authority during that narrow transition.
            // Preserve the requested semantic type so a transient state cannot silently
            // turn a Store click into a Battle, Rest, or Mystery click.
            var eligibleElementNames = typedNodeList
                .Select(node => $"MapNode_{node.nodeId}")
                .ToHashSet(StringComparer.Ordinal);
            return FindActiveElements()
                .Where(element => element.name?.StartsWith("MapNode_", StringComparison.Ordinal) == true &&
                    element.pickingMode == PickingMode.Position &&
                    eligibleElementNames.Contains(element.name))
                .OrderBy(element => element.name, StringComparer.Ordinal)
                .Skip(reachableIndex)
                .Select(element => element.name)
                .FirstOrDefault();
        }

        private static string ResolveUiElementName(string locator)
        {
            string prefix = locator switch
            {
                "FirstLevelUpSkillCard" => "LevelUpSkillCard_",
                "FirstStoreBuyButton" => "StoreBuyButton_",
                "FirstEventOption" => "EventOption_",
                _ => null
            };
            if (prefix == null)
                return locator;

            return FindActiveElements()
                .Where(element => element.name?.StartsWith(prefix, StringComparison.Ordinal) == true)
                .OrderBy(element => element.name, StringComparer.Ordinal)
                .Select(element => element.name)
                .FirstOrDefault();
        }

        private static IUnit ResolveBattleUnit(GameplayRuntimeContext context, string locator)
        {
            if (!string.IsNullOrWhiteSpace(locator) && context.Units.TryGetValue(locator, out var registered))
                return registered;

            var controller = ResolveBattleController(context);
            var units = controller?.GetUnits()?.Where(unit => unit != null && !unit.IsDowned).ToList();
            if (units == null || units.Count == 0)
                return null;

            var current = controller.TurnContext.PlayableUnits?.Invoke()?.FirstOrDefault(unit => unit != null && !unit.IsDowned);
            if (locator is "CurrentUnit" || string.IsNullOrWhiteSpace(locator))
                return current;

            if (locator.Equals("CurrentPlayer", StringComparison.OrdinalIgnoreCase))
            {
                int humanPlayerNumber = controller.PlayerManager.GetPlayers()
                    .FirstOrDefault(player => player.PlayerType == PlayerType.HumanPlayer)
                    ?.PlayerNumber ?? current?.PlayerNumber ?? -1;
                return units
                    .Where(unit => unit.PlayerNumber == humanPlayerNumber)
                    .OrderBy(unit => unit.UnitID)
                    .FirstOrDefault();
            }

            if (locator.Equals("NearestEnemy", StringComparison.OrdinalIgnoreCase) ||
                locator.Equals("PriorityEnemy", StringComparison.OrdinalIgnoreCase))
            {
                int playerNumber = current?.PlayerNumber ?? controller.TurnContext.CurrentPlayer?.PlayerNumber ?? -1;
                var enemies = units.Where(unit => unit.PlayerNumber != playerNumber);
                return locator.Equals("PriorityEnemy", StringComparison.OrdinalIgnoreCase)
                    ? enemies
                        .OrderBy(unit => unit.Health)
                        .ThenByDescending(unit => unit.MaxMovementPoints)
                        .ThenBy(unit => current?.CurrentCell?.GetDistance(unit.CurrentCell) ?? int.MaxValue)
                        .ThenBy(unit => unit.UnitID)
                        .FirstOrDefault()
                    : enemies
                        .OrderBy(unit => current?.CurrentCell?.GetDistance(unit.CurrentCell) ?? int.MaxValue)
                        .ThenBy(unit => unit.UnitID)
                        .FirstOrDefault();
            }

            return units.FirstOrDefault(unit =>
                unit.UnitID.ToString() == locator ||
                (unit is Component component &&
                 component.gameObject.name.Equals(locator, StringComparison.OrdinalIgnoreCase)));
        }

        private static ICell ResolveBattleCell(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action,
            string locator)
        {
            if (!string.IsNullOrWhiteSpace(locator) && context.Cells.TryGetValue(locator, out var registered))
                return registered;

            var controller = ResolveBattleController(context);
            var cells = controller?.CellManager?.GetCells()?.Where(cell => cell != null).ToList();
            if (cells == null)
                return null;

            if (TryParseCoordinates(locator, out int x, out int y) ||
                TryReadCoordinates(action.Parameters, out x, out y))
            {
                return cells.FirstOrDefault(cell =>
                    cell.GridCoordinates.x == x && cell.GridCoordinates.y == y);
            }

            if (!locator.Equals("NearestLegalMove", StringComparison.OrdinalIgnoreCase))
                return null;

            var current = ResolveBattleUnit(context, "CurrentUnit");
            var enemy = ResolveBattleUnit(context, "PriorityEnemy");
            if (current == null)
                return null;

            var humanUnits = controller.GetUnits()
                .Where(unit => unit != null &&
                    !unit.IsDowned &&
                    unit.PlayerNumber == current.PlayerNumber &&
                    unit.CurrentCell != null)
                .OrderBy(unit => unit.UnitID)
                .ToList();
            int formationIndex = humanUnits.FindIndex(unit => unit.UnitID == current.UnitID);
            float centeredIndex = formationIndex >= 0
                ? formationIndex - (humanUnits.Count - 1) * 0.5f
                : 0f;
            int currentDistance = enemy?.CurrentCell == null
                ? 0
                : current.CurrentCell.GetDistance(enemy.CurrentCell);
            int flankOffset = Mathf.RoundToInt(centeredIndex * Mathf.Min(4f, Mathf.Max(0f, currentDistance - 2f)));
            int targetX = enemy?.CurrentCell?.GridCoordinates.x ?? current.CurrentCell.GridCoordinates.x;
            int targetY = enemy?.CurrentCell?.GridCoordinates.y ?? current.CurrentCell.GridCoordinates.y;
            int deltaX = targetX - current.CurrentCell.GridCoordinates.x;
            int deltaY = targetY - current.CurrentCell.GridCoordinates.y;
            if (Mathf.Abs(deltaX) >= Mathf.Abs(deltaY))
                targetY += flankOffset;
            else
                targetX -= flankOffset;

            return current.GetAvailableDestinations(cells)
                .Where(cell => cell != null &&
                    !ReferenceEquals(cell, current.CurrentCell) &&
                    IsWorldTargetVisible(ResolveBattleCellPointerWorldPosition(context, cell)))
                .OrderBy(cell =>
                    Mathf.Abs(cell.GridCoordinates.x - targetX) +
                    Mathf.Abs(cell.GridCoordinates.y - targetY))
                .ThenBy(cell => enemy?.CurrentCell?.GetDistance(cell) ?? 0)
                .ThenByDescending(cell => current.CurrentCell?.GetDistance(cell) ?? 0)
                .ThenBy(cell => cell.GridCoordinates.x)
                .ThenBy(cell => cell.GridCoordinates.y)
                .FirstOrDefault();
        }

        private static Vector3 ResolveBattleCellPointerWorldPosition(
            GameplayRuntimeContext context,
            ICell cell)
        {
            var controller = ResolveBattleController(context);
            if (controller?.CellManager is TilemapCellManager tilemapCellManager &&
                tilemapCellManager.GridLayer != null)
            {
                var coordinates = cell.GridCoordinates;
                return tilemapCellManager.GridLayer.CellToWorld(
                    new Vector3Int(coordinates.x, coordinates.y, 0));
            }

            return cell.WorldPosition.ToVector3();
        }

        private static bool TryParseCoordinates(string locator, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (string.IsNullOrWhiteSpace(locator))
                return false;
            string[] parts = locator.Split(',');
            return parts.Length == 2 &&
                int.TryParse(parts[0], out x) &&
                int.TryParse(parts[1], out y);
        }

        private static bool TryReadCoordinates(JObject parameters, out int x, out int y)
        {
            x = parameters["x"]?.ToObject<int>() ?? 0;
            y = parameters["y"]?.ToObject<int>() ?? 0;
            return parameters["x"] != null && parameters["y"] != null;
        }

        private static BattleController ResolveBattleController(GameplayRuntimeContext context)
        {
            return context?.BattleController
                ?? BattleController.Instance
                ?? UnityEngine.Object.FindFirstObjectByType<BattleController>();
        }

        private static async Task<GameplayStepResult> PlayBattleThroughInput(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            int maximumActions = Math.Clamp(action.Parameters["maximumActions"]?.ToObject<int>() ?? 100, 1, 100);
            int actions = 0;
            int consecutiveNoEffectTurns = 0;

            while (actions < maximumActions)
            {
                if (context.RuntimeScope?.IsCancelling == true)
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Battle input policy cancelled.");

                var controller = ResolveBattleController(context);
                if (controller == null)
                {
                    bool settlementStarted = IsBattleSettlementObservable();
                    if (!settlementStarted)
                    {
                        settlementStarted = await WaitForObservableChange(
                            context,
                            IsBattleSettlementObservable,
                            180);
                    }
                    if (settlementStarted)
                        return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Battle completed through input after {actions} unit actions.");

                    return GameplayStepResult.Fail(
                        PlayerInputAdapterName,
                        action.Kind,
                        "No production BattleController is active and no settlement became observable.");
                }
                if (!controller.IsBattleActive)
                    return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Battle completed through input after {actions} unit actions.");

                var player = controller.TurnContext.CurrentPlayer;
                var current = controller.TurnContext.PlayableUnits?.Invoke()
                    ?.FirstOrDefault(unit => unit != null && unit.CanAct && !unit.IsDowned);
                if (player == null || current == null)
                {
                    if (!await WaitForInputFrame(context))
                        return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Battle turn wait cancelled.");
                    continue;
                }

                if (player.PlayerType != PlayerType.HumanPlayer)
                {
                    if (!await WaitForObservableChange(context, () =>
                            !controller.IsBattleActive ||
                            controller.TurnContext.CurrentPlayer?.PlayerType == PlayerType.HumanPlayer, 3600))
                    {
                        return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Automated turn did not return control within 3600 frames.");
                    }
                    continue;
                }

                actions++;
                bool inputReady = await WaitForObservableChange(
                    context,
                    () => FindActiveElement("MoveButton") != null &&
                        FindActiveElements().Any(element =>
                            element.name?.StartsWith("AbilityCard_", StringComparison.Ordinal) == true),
                    180);
                if (!inputReady)
                {
                    return GameplayStepResult.Fail(
                        PlayerInputAdapterName,
                        action.Kind,
                        $"Production battle controls were not ready for active unit {current.UnitID}.");
                }

                bool changed = await TryUseFirstEnabledAbility(context, controller, current);
                if (!changed)
                {
                    changed = await TryMoveTowardEnemy(context, controller, current);
                    if (changed && controller.IsBattleActive)
                    {
                        // Movement does not consume the unit's attack/skill action. Retry the
                        // production skill cards from the new position before ending the turn.
                        bool postMoveInputReady = await WaitForObservableChange(
                            context,
                            () => controller.GridState is GridStateAwaitInput &&
                                FindActiveElements().Any(element =>
                                    element.name?.StartsWith("AbilityCard_", StringComparison.Ordinal) == true),
                            180);
                        if (postMoveInputReady)
                            await TryUseFirstEnabledAbility(context, controller, current);
                    }
                }
                consecutiveNoEffectTurns = changed ? 0 : consecutiveNoEffectTurns + 1;

                if (controller.IsBattleActive &&
                    controller.TurnContext.CurrentPlayer?.PlayerType == PlayerType.HumanPlayer)
                {
                    var endTurnResult = FindActiveElement("EndTurnButton") != null
                        ? await ClickSemanticTarget(context, "UiElement", "EndTurnButton")
                        : await PressInputKey(
                            context,
                            new ExecutableScenarioAction
                            {
                                Kind = "pressInputKey",
                                Parameters = new JObject { ["key"] = Key.M.ToString() }
                            });
                    if (!endTurnResult.Passed)
                    {
                        return GameplayStepResult.Fail(
                            PlayerInputAdapterName,
                            action.Kind,
                            $"No legal input changed state and EndTurn failed: {endTurnResult.Message}");
                    }
                }

                if (!changed && controller.IsBattleActive &&
                    !await WaitForObservableChange(context, () =>
                        !ReferenceEquals(controller.TurnContext.PlayableUnits?.Invoke()?.FirstOrDefault(), current), 60))
                {
                    return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, $"Input did not advance battle state for unit {current.UnitID}.");
                }

                // A legal player flow may deliberately end several consecutive unit actions
                // while a ranged enemy approaches or a blocked formation opens. Treat a full
                // multi-unit round without any effective action as diagnostic, but allow up to
                // four such rounds before declaring the input policy stuck.
                if (consecutiveNoEffectTurns >= 12 && controller.IsBattleActive)
                {
                    return GameplayStepResult.Fail(
                        PlayerInputAdapterName,
                        action.Kind,
                        "No ability or movement input changed battle state for twelve consecutive human turns. " +
                        DescribeBattleInputAvailability(context));
                }
            }

            return GameplayStepResult.Fail(
                PlayerInputAdapterName,
                action.Kind,
                $"Battle exceeded the maximum of {maximumActions} player-controlled unit actions.");
        }

        private static bool IsBattleSettlementObservable()
        {
            return BattleSettlementCoordinator.Instance.CurrentPhase != SettlementPhase.None ||
                FindActiveElement("BattleSettlementRoot") != null;
        }

        private static string DescribeBattleInputAvailability(GameplayRuntimeContext context)
        {
            string cards = string.Join(
                ",",
                FindActiveElements()
                    .Where(element => element.name?.StartsWith("AbilityCard_", StringComparison.Ordinal) == true)
                    .Select(element => $"{element.name}:{element.enabledInHierarchy}"));
            string move = FindActiveElement("MoveButton") is { } moveButton
                ? $"ready:{moveButton.enabledInHierarchy}"
                : "unavailable";
            var enemyProbe = new ExecutableScenarioAction
            {
                Target = "NearestEnemy",
                Parameters = new JObject { ["targetKind"] = "BattleUnit" }
            };
            TryResolveScreenPosition(context, enemyProbe, out _, out _, out string enemyFailure);
            return $"Cards=[{cards}], Move={move}, EnemyTarget={enemyFailure ?? "ready"}.";
        }

        private static async Task<bool> TryUseFirstEnabledAbility(
            GameplayRuntimeContext context,
            BattleController controller,
            IUnit current)
        {
            var abilitiesByCardName = current.GetBaseAbilities()
                .Where(ability => ability != null)
                .GroupBy(ToAbilityCardName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(GetBattleInputAbilityPriority).First(),
                    StringComparer.Ordinal);
            var cards = FindActiveElements()
                .Where(element =>
                    element.name?.StartsWith("AbilityCard_", StringComparison.Ordinal) == true &&
                    element.enabledInHierarchy &&
                    (element.userData is not AbilityAvailability availability || availability.CanExecute))
                .OrderByDescending(element =>
                    abilitiesByCardName.TryGetValue(element.name, out var ability)
                        ? GetBattleInputAbilityPriority(ability)
                        : 0)
                .ThenBy(element => element.name, StringComparer.Ordinal)
                .ToList();
            if (cards.Count == 0)
                return false;

            foreach (var card in cards)
            {
                if (!abilitiesByCardName.TryGetValue(card.name, out var ability) ||
                    GetBattleInputAbilityPriority(ability) <= 0 ||
                    ability is not IAbilityTargetingProvider targetingProvider)
                {
                    continue;
                }

                var units = controller.GetUnits()
                    .Where(unit => unit != null && !unit.IsDowned)
                    .ToList();
                var enemies = units
                    .Where(unit => unit.PlayerNumber != current.PlayerNumber)
                    .ToList();
                if (enemies.Count == 0)
                    return !controller.IsBattleActive;
                var priorityEnemy = ResolveBattleUnit(context, "PriorityEnemy");
                if (priorityEnemy == null)
                    return !controller.IsBattleActive;

                var targetOption = targetingProvider.QueryTargets(new AbilityTargetQuery(
                        current,
                        current.CurrentCell,
                        controller,
                        units))
                    .Options
                    .Where(option => option?.TargetPoint != null &&
                        option.Targets.Any(target => target != null &&
                            !target.IsDowned &&
                            target.PlayerNumber != current.PlayerNumber &&
                            target.UnitID == priorityEnemy.UnitID))
                    .OrderByDescending(option => option.Targets
                        .Where(target => target.PlayerNumber != current.PlayerNumber)
                        .Max(target => target.MaxMovementPoints))
                    .ThenBy(option => option.Targets
                        .Where(target => target.PlayerNumber != current.PlayerNumber)
                        .Min(target => target.Health))
                    .ThenBy(option => current.CurrentCell?.GetDistance(option.TargetPoint) ?? int.MaxValue)
                    .FirstOrDefault();
                if (targetOption == null)
                    continue;

                var observedEnemy = targetOption.Targets
                    .Where(target => target != null &&
                        !target.IsDowned &&
                        target.PlayerNumber != current.PlayerNumber)
                    .OrderBy(target => target.Health)
                    .ThenBy(target => target.UnitID)
                    .First();
                var before = BattleInputSnapshot.Capture(controller, current, observedEnemy);
                var cardResult = await ClickSemanticTarget(context, "UiElement", card.name);
                if (!cardResult.Passed)
                    continue;
                var targetResult = targetOption.PrimaryTarget != null
                    ? await ClickSemanticTarget(
                        context,
                        "BattleUnit",
                        targetOption.PrimaryTarget.UnitID.ToString())
                    : await ClickSemanticTarget(
                        context,
                        "BattleCell",
                        $"{targetOption.TargetPoint.GridCoordinates.x},{targetOption.TargetPoint.GridCoordinates.y}");
                if (!targetResult.Passed)
                    continue;

                if (await WaitForObservableChange(
                        context,
                        () => !before.Equals(BattleInputSnapshot.Capture(controller, current, observedEnemy)),
                        90))
                {
                    return true;
                }

                await ClickPointerTarget(
                    context,
                    new ExecutableScenarioAction
                    {
                        Kind = "rightClickPointerTarget",
                        Target = current.UnitID.ToString(),
                        Parameters = new JObject { ["targetKind"] = "BattleUnit" }
                    },
                    PointerButton.Right);
            }

            return false;
        }

        private static string ToAbilityCardName(IAbility ability)
        {
            string displayName = string.IsNullOrWhiteSpace(ability?.DisplayName)
                ? "Unknown"
                : ability.DisplayName;
            return "AbilityCard_" + new string(displayName
                .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray());
        }

        private static int GetBattleInputAbilityPriority(IAbility ability)
        {
            if (ability is not SkillGraphAbilityImpl graphAbility || graphAbility.SkillGraphAsset?.Nodes == null)
            {
                return ability?.DisplayName?.Contains("攻击", StringComparison.Ordinal) == true ||
                    ability?.DisplayName?.Contains("Attack", StringComparison.OrdinalIgnoreCase) == true
                    ? 80
                    : 0;
            }

            var nodes = graphAbility.SkillGraphAsset.Nodes;
            bool hasClassDamageNode = nodes.Any(node =>
                node is MageSkillNodeRecord mage &&
                    mage.SkillKind is MageSkillKind.Fireball or MageSkillKind.IceBolt or MageSkillKind.Lightning ||
                node is NecromancerSkillNodeRecord necromancer &&
                    necromancer.SkillKind == NecromancerSkillKind.BoneSpear ||
                node is AmazonSkillNodeRecord amazon &&
                    amazon.SkillKind is AmazonSkillKind.Thrust or AmazonSkillKind.MultiStab or AmazonSkillKind.PoisonSpear);
            if (hasClassDamageNode)
                return 200;

            return nodes.Any(node => node is ApplyDamageNodeRecord) ? 100 : 0;
        }

        private static async Task<bool> TryMoveTowardEnemy(
            GameplayRuntimeContext context,
            BattleController controller,
            IUnit current)
        {
            if (FindActiveElement("MoveButton")?.enabledInHierarchy != true)
                return false;

            var beforeCell = current.CurrentCell;
            var moveResult = await ClickSemanticTarget(context, "UiElement", "MoveButton");
            if (!moveResult.Passed)
                return false;

            // The production move ability populates its path cache when the button
            // enters targeting state, so reachable cells must be resolved afterward.
            var destination = ResolveBattleCell(
                context,
                new ExecutableScenarioAction { Parameters = new JObject() },
                "NearestLegalMove");
            if (destination == null)
            {
                await ClickPointerTarget(
                    context,
                    new ExecutableScenarioAction
                    {
                        Kind = "rightClickPointerTarget",
                        Target = current.UnitID.ToString(),
                        Parameters = new JObject { ["targetKind"] = "BattleUnit" }
                    },
                    PointerButton.Right);
                return false;
            }

            var targetResult = await ClickSemanticTarget(
                context,
                "BattleCell",
                $"{destination.GridCoordinates.x},{destination.GridCoordinates.y}");
            if (!targetResult.Passed)
                return false;

            return await WaitForObservableChange(
                context,
                () => !ReferenceEquals(current.CurrentCell, beforeCell) || !controller.IsBattleActive,
                180);
        }

        private static bool IsWorldTargetVisible(Vector3 worldPosition)
        {
            return TryResolveWorldScreenPosition(
                worldPosition,
                "battle world target",
                out _,
                out _);
        }

        private static Task<GameplayStepResult> ClickSemanticTarget(
            GameplayRuntimeContext context,
            string targetKind,
            string target)
        {
            return ClickPointerTarget(
                context,
                new ExecutableScenarioAction
                {
                    Adapter = PlayerInputAdapterName,
                    Kind = "clickPointerTarget",
                    Target = target,
                    Parameters = new JObject { ["targetKind"] = targetKind }
                },
                PointerButton.Left);
        }

        private static async Task<bool> WaitForObservableChange(
            GameplayRuntimeContext context,
            Func<bool> predicate,
            int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (predicate())
                    return true;
                if (!await WaitForInputFrame(context))
                    return false;
            }
            return predicate();
        }

        private static IEnumerable<VisualElement> FindActiveElements()
        {
            return UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .Where(document => document != null && document.isActiveAndEnabled && document.rootVisualElement != null)
                .SelectMany(document => document.rootVisualElement.Query<VisualElement>().ToList())
                .Where(element => element != null && element.panel != null &&
                    element.resolvedStyle.display != DisplayStyle.None && element.visible &&
                    IsElementLayoutReady(element));
        }

        private readonly struct BattleInputSnapshot : IEquatable<BattleInputSnapshot>
        {
            private readonly bool _battleActive;
            private readonly int _round;
            private readonly int _currentUnitId;
            private readonly float _actorMana;
            private readonly int _actorX;
            private readonly int _actorY;
            private readonly float _targetHealth;

            private BattleInputSnapshot(
                bool battleActive,
                int round,
                int currentUnitId,
                float actorMana,
                int actorX,
                int actorY,
                float targetHealth)
            {
                _battleActive = battleActive;
                _round = round;
                _currentUnitId = currentUnitId;
                _actorMana = actorMana;
                _actorX = actorX;
                _actorY = actorY;
                _targetHealth = targetHealth;
            }

            public static BattleInputSnapshot Capture(BattleController controller, IUnit actor, IUnit target)
            {
                bool actorAvailable = IsUnityReferenceAvailable(actor);
                bool targetAvailable = IsUnityReferenceAvailable(target);
                return new BattleInputSnapshot(
                    controller?.IsBattleActive == true,
                    controller?.CurrentRound ?? 0,
                    controller == null
                        ? -1
                        : controller.TurnContext.PlayableUnits?.Invoke()?.FirstOrDefault()?.UnitID ?? -1,
                    actorAvailable ? actor.Mana : 0f,
                    actorAvailable ? actor.CurrentCell?.GridCoordinates.x ?? int.MinValue : int.MinValue,
                    actorAvailable ? actor.CurrentCell?.GridCoordinates.y ?? int.MinValue : int.MinValue,
                    targetAvailable ? target.Health : float.MinValue);
            }

            public bool Equals(BattleInputSnapshot other)
            {
                return _battleActive == other._battleActive &&
                    _round == other._round &&
                    _currentUnitId == other._currentUnitId &&
                    Math.Abs(_actorMana - other._actorMana) < 0.001f &&
                    _actorX == other._actorX &&
                    _actorY == other._actorY &&
                    Math.Abs(_targetHealth - other._targetHealth) < 0.001f;
            }

            private static bool IsUnityReferenceAvailable(object value)
            {
                return value != null &&
                    (value is not UnityEngine.Object unityObject || unityObject != null);
            }
        }

        private static bool TryResolveUiScreenPosition(string elementName, out Vector2 screenPosition, out string failure)
        {
            var element = FindActiveElement(elementName);
            if (element?.panel == null)
            {
                screenPosition = default;
                failure = $"UI element '{elementName}' is not visible on an active Panel in scene '{SceneManager.GetActiveScene().name}'.";
                return false;
            }
            if (!element.enabledInHierarchy)
            {
                screenPosition = default;
                failure = $"UI element '{elementName}' is visible but not interactable.";
                return false;
            }

            var worldBound = element.worldBound;
            if (!IsFinitePositive(worldBound.width) || !IsFinitePositive(worldBound.height) ||
                !IsFinite(worldBound.x) || !IsFinite(worldBound.y))
            {
                screenPosition = default;
                failure = $"UI element '{elementName}' has invalid worldBound {worldBound}.";
                return false;
            }

            float scale = element.panel.scaledPixelsPerPoint;
            var panelCenter = worldBound.center;
            screenPosition = new Vector2(panelCenter.x * scale, Screen.height - panelCenter.y * scale);

            // Mouse positions use a bottom-left origin. UI Toolkit panel positions use
            // a top-left origin, so validate the exact device coordinate after applying
            // the same Y conversion performed by the production UI input module.
            var panelInputPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            var picked = element.panel.Pick(RuntimePanelUtils.ScreenToPanel(element.panel, panelInputPosition));
            if (picked == element || (picked != null && element.Contains(picked)))
            {
                failure = null;
                return true;
            }

            failure = $"Panel picking mismatch for '{elementName}' at device screen {screenPosition}; picked '{picked?.name ?? "<none>"}'.";
            return false;
        }

        private static bool TryResolveWorldScreenPosition(
            Vector3 worldPosition,
            string description,
            out Vector2 screenPosition,
            out string failure)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                screenPosition = default;
                failure = $"Camera.main is unavailable while resolving {description}.";
                return false;
            }

            Vector3 projected = camera.WorldToScreenPoint(worldPosition);
            if (projected.z <= 0f || projected.x < 0f || projected.y < 0f || projected.x > Screen.width || projected.y > Screen.height)
            {
                screenPosition = default;
                failure = $"{description} projects outside the active camera at {projected}.";
                return false;
            }

            screenPosition = projected;
            failure = null;
            return true;
        }

        private static VisualElement FindActiveElement(string elementName)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                return null;

            return UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .Where(document => document != null && document.isActiveAndEnabled && document.rootVisualElement != null)
                .Select(document => document.rootVisualElement.Q<VisualElement>(elementName))
                .FirstOrDefault(element => element != null &&
                    element.resolvedStyle.display != DisplayStyle.None &&
                    element.visible &&
                    IsElementLayoutReady(element));
        }

        private static bool IsElementLayoutReady(VisualElement element)
        {
            if (element?.panel == null)
                return false;

            Rect bounds = element.worldBound;
            return IsFinite(bounds.x) && IsFinite(bounds.y) &&
                IsFinitePositive(bounds.width) && IsFinitePositive(bounds.height);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string ResolveLocator(ExecutableScenarioAction action)
        {
            return action.Target
                ?? action.Parameters["elementName"]?.ToString()
                ?? action.Parameters["nodeId"]?.ToString()
                ?? action.Parameters["unitId"]?.ToString()
                ?? action.Parameters["cell"]?.ToString()
                ?? string.Empty;
        }

        private static async Task<bool> WaitForInputFrame(GameplayRuntimeContext context)
        {
            int startingFrame = Time.frameCount;
            do
            {
                if (context.RuntimeScope?.IsCancelling == true)
                    return false;
                await Task.Yield();
            }
            while (Time.frameCount == startingFrame);

            return context.RuntimeScope?.IsCancelling != true;
        }

        private enum PointerButton
        {
            Left,
            Right
        }
    }
}
