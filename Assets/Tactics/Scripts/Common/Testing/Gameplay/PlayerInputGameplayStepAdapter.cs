using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Tactics.UI;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
                    "initializePlayerInput" => InitializePlayerInput(context, action),
                    "movePointerToTarget" => await MovePointerToTarget(context, action),
                    "clickPointerTarget" => await ClickPointerTarget(context, action, MouseButton.Left),
                    "rightClickPointerTarget" => await ClickPointerTarget(context, action, MouseButton.Right),
                    "pressInputKey" => await PressInputKey(context, action),
                    "waitForPlayerObservable" => await WaitForPlayerObservable(context, action),
                    "playBattleThroughInput" => GameplayStepResult.Fail(
                        PlayerInputAdapterName,
                        action.Kind,
                        "Battle input policy is added by the battle journey slice."),
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

        private static GameplayStepResult InitializePlayerInput(GameplayRuntimeContext context, ExecutableScenarioAction action)
        {
            if (context.PlayerInputMouse != null || context.PlayerInputKeyboard != null)
                return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, "Virtual player input is already initialized.");

            var mouse = InputSystem.AddDevice<Mouse>("GameplayTestMouse");
            var keyboard = InputSystem.AddDevice<Keyboard>("GameplayTestKeyboard");
            mouse.MakeCurrent();
            keyboard.MakeCurrent();

            context.PlayerInputMouse = mouse;
            context.PlayerInputKeyboard = keyboard;
            context.OwnedInputDevices.Add(mouse);
            context.OwnedInputDevices.Add(keyboard);
            System.Threading.Interlocked.Increment(ref _totalInitializationCount);

            EnsureInputSystemUiModule(context);
            return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, "Initialized virtual Mouse and Keyboard devices.");
        }

        private static void EnsureInputSystemUiModule(GameplayRuntimeContext context)
        {
            var existingModule = UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (existingModule != null)
            {
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
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
                context.OwnedRuntimeGameObjects.Add(eventSystemObject);
                return;
            }

            var module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            context.OwnedRuntimeComponents.Add(module);
        }

        private static async Task<GameplayStepResult> MovePointerToTarget(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action)
        {
            if (!TryResolveScreenPosition(context, action, out var position, out var description, out var failure))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, failure);

            if (context.PlayerInputMouse == null || !context.PlayerInputMouse.added)
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "initializePlayerInput must run before pointer actions.");

            InputSystem.QueueDeltaStateEvent(context.PlayerInputMouse.position, position);
            if (!await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Pointer movement cancelled.");
            return GameplayStepResult.Pass(PlayerInputAdapterName, action.Kind, $"Moved pointer to {description} at {position}.");
        }

        private static async Task<GameplayStepResult> ClickPointerTarget(
            GameplayRuntimeContext context,
            ExecutableScenarioAction action,
            MouseButton button)
        {
            var moveResult = await MovePointerToTarget(context, action);
            if (!moveResult.Passed)
                return moveResult;

            var control = button == MouseButton.Left
                ? context.PlayerInputMouse.leftButton
                : context.PlayerInputMouse.rightButton;

            InputSystem.QueueDeltaStateEvent(control, true);
            if (!await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Pointer press cancelled.");
            InputSystem.QueueDeltaStateEvent(control, false);
            if (!await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Pointer release cancelled.");

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

            var control = context.PlayerInputKeyboard[key];
            InputSystem.QueueDeltaStateEvent(control, true);
            if (!await WaitForInputFrame(context))
                return GameplayStepResult.Fail(PlayerInputAdapterName, action.Kind, "Key press cancelled.");
            InputSystem.QueueDeltaStateEvent(control, false);
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
                $"Observable '{observable}' was not satisfied after {maximumFrames} frames in scene '{SceneManager.GetActiveScene().name}'.");
        }

        private static bool IsObservableSatisfied(ExecutableScenarioAction action, string observable)
        {
            switch (observable)
            {
                case "uiElement":
                    return IsElementLayoutReady(FindActiveElement(action.Target ?? action.Parameters["elementName"]?.ToString()));
                case "uiVisible":
                    return TryParseUiId(action, out var visibleUiId) && UIManager.Instance.IsVisible(visibleUiId);
                case "uiHidden":
                    return TryParseUiId(action, out var hiddenUiId) && !UIManager.Instance.IsVisible(hiddenUiId);
                case "mapReady":
                    return UIManager.Instance.IsVisible(UIManager.UIId.RoguelikeMap) &&
                        FindActiveElement("InventoryButton") != null;
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
                string elementName = targetKind == "MapNode" && !locator.StartsWith("MapNode_", StringComparison.Ordinal)
                    ? $"MapNode_{locator}"
                    : locator;
                return TryResolveUiScreenPosition(elementName, out screenPosition, out failure);
            }

            if (targetKind == "BattleUnit")
            {
                if (!context.Units.TryGetValue(locator, out var unit) || unit is not Component component)
                {
                    screenPosition = default;
                    failure = $"BattleUnit '{locator}' was not registered as a scene component.";
                    return false;
                }

                return TryResolveWorldScreenPosition(component.transform.position, description, out screenPosition, out failure);
            }

            if (targetKind == "BattleCell")
            {
                if (!context.Cells.TryGetValue(locator, out var cell))
                {
                    screenPosition = default;
                    failure = $"BattleCell '{locator}' was not registered.";
                    return false;
                }

                var worldPosition = cell.WorldPosition.ToVector3();
                return TryResolveWorldScreenPosition(worldPosition, description, out screenPosition, out failure);
            }

            screenPosition = default;
            failure = $"Unsupported pointer target kind '{targetKind}'.";
            return false;
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
                .FirstOrDefault(element => element != null && element.resolvedStyle.display != DisplayStyle.None && element.visible);
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

        private enum MouseButton
        {
            Left,
            Right
        }
    }
}
