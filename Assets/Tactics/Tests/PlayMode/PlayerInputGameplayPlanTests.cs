using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Testing.Gameplay;
using Tactics.Roguelike;
using Tactics.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Runs player-input E2E plans without inheriting the Battle UI fixture. Each test
    /// starts from the real Home UI and owns its cached UI instances for isolation.
    /// </summary>
    public sealed class PlayerInputGameplayPlanTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = false;

            var initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(initializeTask);
            Assume.That(initializeTask.Result, Is.Not.Null, "GameAssetManager should be initialized.");

            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (eventSystem != null)
                    Object.Destroy(eventSystem.gameObject);
            }
            yield return null;

            // Always reload Home so prior UI/EventSystem fixtures cannot leave a stale
            // input module or cached scene object behind even when their final scene name
            // is also "Home".
            var loadHomeTask = initializeTask.Result.LoadSceneAsync(
                SceneProjectPathHelper.ToProjectPath("Home"),
                LoadSceneMode.Single);
            yield return WaitForTask(loadHomeTask);

            // HomeFlowCoordinator shows Home asynchronously on scene load. Let that production
            // show finish before destroying cached UI; otherwise its late continuation can wire
            // a controller to the tree that this fixture just destroyed.
            HomeUIController automaticallyShownHome = null;
            for (int frame = 0; frame < 120; frame++)
            {
                automaticallyShownHome = Object.FindFirstObjectByType<HomeUIController>();
                if (automaticallyShownHome?.IsReadyForInput == true)
                    break;
                yield return null;
            }
            Assert.That(automaticallyShownHome?.IsReadyForInput, Is.True,
                "Production Home auto-show must settle before the fixture replaces its cached UI tree.");

            ResetPureRunState();
            DestroyOwnedUiInstances();
            yield return null;

            var showHomeTask = UIManager.Instance.ShowAsync(UIManager.UIId.Home);
            yield return WaitForTask(showHomeTask);

            HomeUIController homeController = null;
            for (int frame = 0; frame < 120; frame++)
            {
                homeController = Object.FindFirstObjectByType<HomeUIController>();
                if (homeController?.IsReadyForInput == true)
                    break;
                yield return null;
            }
            Assert.That(homeController?.IsReadyForInput, Is.True, "Home UI should be wired to the current UIDocument tree.");
            AssertHomeControllerUsesActiveTree(homeController);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            ResetPureRunState();
            DestroyOwnedUiInstances();
            yield return null;
            TestGameAssetHelper.Cleanup();
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesInventoryReentryThroughPlayerInput()
        {
            var task = ExecutePlan(GetPlanPath("inventory-reentry-player-input.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            var stepTrace = string.Join("; ", result.ExecutedSteps.Select(step => $"{step.Kind}: {step.Message}"));
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, " +
                          $"Diagnostics=[{string.Join("; ", result.Diagnostics)}], StepTrace=[{stepTrace}]";
            Assert.IsTrue(result.Passed, details);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesBattleSmokeThroughPlayerInput()
        {
            var task = ExecutePlan(GetPlanPath("battle-player-input-smoke.plan.json"));
            yield return WaitForTask(task);

            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesPureRunJourneyThroughPlayerInput()
        {
            var task = ExecutePlan(GetPlanPath("pure-run-player-input-route.plan.json"));
            yield return WaitForTask(task);

            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesPureRunMysteryResultPageThroughPlayerInput()
        {
            var task = ExecutePlan(GetPlanPath("pure-run-mystery-real-player-result-page.plan.json"));
            yield return WaitForTask(task);

            AssertPlanPassed(task.Result);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesPureRunMysteryCommitThroughPlayerInput()
        {
            var task = ExecutePlan(GetPlanPath("pure-run-mystery-real-player-commit.plan.json"));
            yield return WaitForTask(task);

            AssertPlanPassed(task.Result);
        }


        [UnityTest]
        public IEnumerator RuntimeRunner_ReleasesVirtualDevicesAfterActionFailure()
        {
            var inputStateBefore = CaptureInputState();
            var plan = CreateInputLifecyclePlan(
                timeoutMs: 10000,
                runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"pressInputKey\",\"parameters\":{\"key\":\"NotARealKey\"}}");

            var task = ExecutePlan(plan);
            yield return WaitForTask(task);

            Assert.That(task.Result.Failures, Is.Not.Empty,
                $"The invalid key action unexpectedly returned a passing result. Steps=[{string.Join("; ", task.Result.ExecutedSteps.Select(step => step.Message))}]");
            Assert.IsFalse(task.Result.Passed,
                $"Failures={task.Result.Failures.Count}, Diagnostics={task.Result.Diagnostics.Count}");
            AssertInputStateRestored(inputStateBefore);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ReleasesVirtualDevicesAfterTimeout()
        {
            var inputStateBefore = CaptureInputState();
            int initializationCountBefore = PlayerInputGameplayStepAdapter.TotalInitializationCount;
            var plan = CreateInputLifecyclePlan(
                timeoutMs: 20,
                runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"waitForPlayerObservable\",\"parameters\":{\"observable\":\"uiElement\",\"elementName\":\"NeverCreatedElement\",\"maximumFrames\":10000}}");

            var task = ExecutePlan(plan);
            yield return WaitForTask(task);

            Assert.That(task.Result.Failures, Is.Not.Empty,
                $"The observable wait unexpectedly returned a passing result. Steps=[{string.Join("; ", task.Result.ExecutedSteps.Select(step => step.Message))}]");
            Assert.That(task.Result.FailureCategory, Is.EqualTo(FailureCategory.Timeout));
            Assert.That(PlayerInputGameplayStepAdapter.TotalInitializationCount, Is.GreaterThan(initializationCountBefore));
            AssertInputStateRestored(inputStateBefore);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_FailsClosedWithoutProductionInputModule()
        {
            var productionModules = Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None)
                .Where(module => module.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule")
                .ToArray();
            Assert.That(productionModules, Is.Not.Empty, "Home must provide a production InputSystemUIInputModule for this contract test.");
            var enabledStates = productionModules.Select(module => module.enabled).ToArray();
            int moduleCountBefore = productionModules.Length;

            try
            {
                foreach (var module in productionModules)
                    module.enabled = false;
                yield return null;

                var plan = CreateInputLifecyclePlan(
                    timeoutMs: 10000,
                    runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"pressInputKey\",\"parameters\":{\"key\":\"NotARealKey\"}}");
                var task = ExecutePlan(plan);
                yield return WaitForTask(task);

                Assert.That(task.Result.Passed, Is.False);
                Assert.That(
                    task.Result.ExecutedSteps.Any(step => step.Message.Contains("production InputSystemUIInputModule")),
                    Is.True,
                    $"Initialization must fail at the production input boundary. Steps=[{string.Join("; ", task.Result.ExecutedSteps.Select(step => step.Message))}]");
                Assert.That(
                    Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None)
                        .Count(module => module.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule"),
                    Is.EqualTo(moduleCountBefore),
                    "PlayerInput E2E must not create a replacement InputSystemUIInputModule.");
            }
            finally
            {
                for (int index = 0; index < productionModules.Length; index++)
                {
                    if (productionModules[index] != null)
                        productionModules[index].enabled = enabledStates[index];
                }
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RestoresDisabledProductionPointerActionBaseline()
        {
            var module = Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule" &&
                    candidate.isActiveAndEnabled);
            Assert.That(module, Is.Not.Null, "Home must provide an active production InputSystemUIInputModule.");
            object scrollAction = GetInputAction(module, "scrollWheel");
            Assert.That(scrollAction, Is.Not.Null, "The production module must provide a scrollWheel action.");
            bool originallyEnabled = IsInputActionEnabled(scrollAction);
            Assert.That(originallyEnabled, Is.True, "The clean Home baseline must start with scrollWheel enabled.");

            SetInputActionEnabled(scrollAction, false);
            try
            {
                var plan = CreateInputLifecyclePlan(
                    timeoutMs: 10000,
                    runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"pressInputKey\",\"parameters\":{\"key\":\"NotARealKey\"}}");
                var task = ExecutePlan(plan);
                yield return WaitForTask(task);

                Assert.That(task.Result.Passed, Is.False);
                Assert.That(module.enabled, Is.True, "Production module enabled state must remain at its captured baseline.");
                Assert.That(IsInputActionEnabled(scrollAction), Is.False,
                    "Cleanup must restore a production pointer action that was disabled before initialization.");
            }
            finally
            {
                SetInputActionEnabled(scrollAction, originallyEnabled);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_DoesNotTreatPointerDownDetachAsClickSuccess()
        {
            var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate?.rootVisualElement?.panel != null);
            Assert.That(document, Is.Not.Null, "Home must provide an active UIDocument.");
            var button = new VisualElement { name = "DetachOnPointerDownButton" };
            button.style.position = Position.Absolute;
            button.style.left = 100f;
            button.style.top = 100f;
            button.style.width = 160f;
            button.style.height = 60f;
            button.RegisterCallback<PointerDownEvent>(_ => button.RemoveFromHierarchy());
            document.rootVisualElement.Add(button);

            try
            {
                yield return null;
                var plan = CreateInputLifecyclePlan(
                    timeoutMs: 10000,
                    runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"clickPointerTarget\",\"target\":\"DetachOnPointerDownButton\",\"parameters\":{\"targetKind\":\"UiElement\"}}");
                var task = ExecutePlan(plan);
                yield return WaitForTask(task);

                Assert.That(task.Result.Passed, Is.False,
                    "PointerDown followed by target detach must not be reported as a completed click.");
                Assert.That(
                    task.Result.ExecutedSteps.Any(step => step.Message.Contains("no ClickEvent")),
                    Is.True,
                    $"Expected a release-without-click diagnostic. Steps=[{string.Join("; ", task.Result.ExecutedSteps.Select(step => step.Message))}]");
            }
            finally
            {
                button.RemoveFromHierarchy();
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_DoesNotTreatPreObserverDetachAsClickSuccess()
        {
            var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate?.rootVisualElement?.panel != null);
            Assert.That(document, Is.Not.Null, "Home must provide an active UIDocument.");
            var target = new VisualElement { name = "DetachBeforeClickObserverTarget" };
            target.style.position = Position.Absolute;
            target.style.left = 520f;
            target.style.top = 100f;
            target.style.width = 160f;
            target.style.height = 60f;
            target.RegisterCallback<PointerMoveEvent>(_ => target.RemoveFromHierarchy());
            document.rootVisualElement.Add(target);

            try
            {
                yield return null;
                var plan = CreateInputLifecyclePlan(
                    timeoutMs: 10000,
                    runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"clickPointerTarget\",\"target\":\"DetachBeforeClickObserverTarget\",\"parameters\":{}}");
                var task = ExecutePlan(plan);
                yield return WaitForTask(task);

                Assert.That(task.Result.Passed, Is.False,
                    "A default-kind UI target that disappears during pointer geometry resolution must not be reported as clicked.");
            }
            finally
            {
                target.RemoveFromHierarchy();
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_InvokesButtonCallbackAcrossVirtualDeviceContexts()
        {
            var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate?.rootVisualElement?.panel != null);
            Assert.That(document, Is.Not.Null, "Home must provide an active UIDocument.");
            int callbackCount = 0;

            for (int run = 1; run <= 2; run++)
            {
                var button = new Button(() => callbackCount++)
                {
                    name = "CrossContextCallbackButton"
                };
                button.style.position = Position.Absolute;
                button.style.left = 720f;
                button.style.top = 100f;
                button.style.width = 180f;
                button.style.height = 60f;
                document.rootVisualElement.Add(button);

                try
                {
                    yield return null;
                    var plan = CreateInputLifecyclePlan(
                        timeoutMs: 10000,
                        runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"clickPointerTarget\",\"target\":\"CrossContextCallbackButton\",\"parameters\":{\"targetKind\":\"UiElement\"}}");
                    var task = ExecutePlan(plan);
                    yield return WaitForTask(task);

                    Assert.That(task.Result.Passed, Is.True,
                        $"Production pointer transaction {run} failed. Steps=[{string.Join("; ", task.Result.ExecutedSteps.Select(step => step.Message))}]");
                    Assert.That(callbackCount, Is.EqualTo(run),
                        $"ClickEvent observation is insufficient: production Button.clicked callback {run} was not invoked.");
                }
                finally
                {
                    button.RemoveFromHierarchy();
                }

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ObservesVisibleNonPickableUiElement()
        {
            var document = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate?.rootVisualElement?.panel != null);
            Assert.That(document, Is.Not.Null, "Home must provide an active UIDocument.");
            var label = new Label("Ready")
            {
                name = "NonPickableReadyLabel",
                pickingMode = PickingMode.Ignore
            };
            label.style.position = Position.Absolute;
            label.style.left = 300f;
            label.style.top = 100f;
            label.style.width = 160f;
            label.style.height = 40f;
            document.rootVisualElement.Add(label);

            try
            {
                yield return null;
                var plan = CreateInputLifecyclePlan(
                    timeoutMs: 10000,
                    runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"waitForPlayerObservable\",\"parameters\":{\"observable\":\"uiElement\",\"elementName\":\"NonPickableReadyLabel\",\"maximumFrames\":3}}");
                var task = ExecutePlan(plan);
                yield return WaitForTask(task);

                Assert.That(task.Result.Passed, Is.True,
                    $"Visible non-interactive UI readiness must not require center picking. Steps=[{string.Join("; ", task.Result.ExecutedSteps.Select(step => step.Message))}]");
            }
            finally
            {
                label.RemoveFromHierarchy();
            }
        }

        private static async Task<GameplayTestResult> ExecutePlan(string planPath)
        {
            Assert.IsTrue(File.Exists(planPath), $"Plan file not found: {planPath}");
            var plan = ExecutableScenarioPlanLoader.FromFile(planPath);
            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[]
            {
                new PlayerInputGameplayStepAdapter(),
                new UiGameplayStepAdapter(),
                new MapGameplayStepAdapter(),
                new BattleGameplayStepAdapter()
            });
            return await runner.ExecuteAsync(plan);
        }

        private static Task<GameplayTestResult> ExecutePlan(ExecutableScenarioPlan plan)
        {
            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[]
            {
                new PlayerInputGameplayStepAdapter(),
                new UiGameplayStepAdapter(),
                new MapGameplayStepAdapter(),
                new BattleGameplayStepAdapter()
            });
            return runner.ExecuteAsync(plan);
        }

        private static void AssertPlanPassed(GameplayTestResult result)
        {
            var stepTrace = string.Join("; ", result.ExecutedSteps.Select(step => $"{step.Kind}: {step.Message}"));
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, " +
                          $"Diagnostics=[{string.Join("; ", result.Diagnostics)}], StepTrace=[{stepTrace}]";
            Assert.IsTrue(result.Passed, details);
        }

        private static ExecutableScenarioPlan CreateInputLifecyclePlan(
            int timeoutMs,
            string runtimeActionJson)
        {
            string json = "{" +
                "\"schemaVersion\":1," +
                "\"scenarioName\":\"PlayerInputLifecycle\"," +
                "\"requiredAdapters\":[\"PlayerInput\",\"UI\"]," +
                "\"setupActions\":[{\"adapter\":\"PlayerInput\",\"kind\":\"initializePlayerInput\",\"parameters\":{}}]," +
                $"\"runtimeActions\":[{runtimeActionJson}]," +
                "\"assertionPlans\":[{\"adapter\":\"UI\",\"kind\":\"elementExists\",\"target\":\"NewGameButton\",\"expected\":true,\"parameters\":{}}]," +
                $"\"timeoutMs\":{timeoutMs}," +
                "\"probeRequests\":[]" +
                "}";
            return ExecutableScenarioPlanLoader.FromJson(json);
        }

        private static void AssertVirtualInputDevicesReleased()
        {
            Assert.That(PlayerInputGameplayStepAdapter.HasVirtualTestDevices, Is.False);
        }

        private static InputStateSnapshot CaptureInputState()
        {
            var inputSystemType = System.Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            Assert.That(inputSystemType, Is.Not.Null, "Unity Input System runtime type must be available.");
            object settings = inputSystemType.GetProperty("settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            Assert.That(settings, Is.Not.Null, "Unity Input System settings must be available.");

            var snapshot = new InputStateSnapshot
            {
                EditorInputBehavior = settings.GetType().GetProperty("editorInputBehaviorInPlayMode")?.GetValue(settings)?.ToString(),
                BackgroundBehavior = settings.GetType().GetProperty("backgroundBehavior")?.GetValue(settings)?.ToString()
            };

            var devices = inputSystemType.GetProperty("devices", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as IEnumerable;
            Assert.That(devices, Is.Not.Null, "Unity Input System devices must be enumerable.");
            foreach (object device in devices)
            {
                string typeName = device.GetType().Name;
                if (typeName is not ("Mouse" or "Keyboard"))
                    continue;

                var deviceType = device.GetType();
                int deviceId = (int)deviceType.GetProperty("deviceId")?.GetValue(device);
                string name = deviceType.GetProperty("name")?.GetValue(device)?.ToString();
                if (name is "GameplayTestMouse" or "GameplayTestKeyboard")
                    continue;
                bool enabled = (bool)deviceType.GetProperty("enabled")?.GetValue(device);
                snapshot.PhysicalDevices[deviceId] = new PhysicalDeviceState(name, enabled);
            }

            return snapshot;
        }

        private static void AssertHomeControllerUsesActiveTree(HomeUIController controller)
        {
            const BindingFlags fieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var boundButton = typeof(HomeUIController)
                .GetField("_newGameButton", fieldFlags)
                ?.GetValue(controller) as Button;
            var activeButton = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .Select(document => document?.rootVisualElement?.Q<Button>("NewGameButton"))
                .FirstOrDefault(button => button?.panel != null &&
                                          button.resolvedStyle.display != DisplayStyle.None);

            Assert.That(boundButton, Is.Not.Null, "Home controller must retain its wired NewGameButton.");
            Assert.That(activeButton, Is.Not.Null, "The active Home tree must expose NewGameButton.");
            Assert.That(boundButton, Is.SameAs(activeButton),
                "Home controller must be wired to the active UIDocument tree, not a destroyed cached tree.");
        }

        private static object GetInputAction(BaseInputModule module, string actionReferenceProperty)
        {
            object actionReference = module?.GetType()
                .GetProperty(actionReferenceProperty, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(module);
            return actionReference?.GetType()
                .GetProperty("action", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(actionReference);
        }

        private static bool IsInputActionEnabled(object action)
        {
            return (bool)(action?.GetType()
                .GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(action) ?? false);
        }

        private static void SetInputActionEnabled(object action, bool enabled)
        {
            string methodName = enabled ? "Enable" : "Disable";
            action?.GetType()
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(action, null);
        }

        private static void AssertInputStateRestored(InputStateSnapshot expected)
        {
            var actual = CaptureInputState();
            Assert.That(actual.EditorInputBehavior, Is.EqualTo(expected.EditorInputBehavior));
            Assert.That(actual.BackgroundBehavior, Is.EqualTo(expected.BackgroundBehavior));
            foreach (var pair in expected.PhysicalDevices)
            {
                Assert.That(actual.PhysicalDevices.ContainsKey(pair.Key), Is.True,
                    $"Physical input device '{pair.Value.Name}' ({pair.Key}) disappeared during cleanup.");
                Assert.That(actual.PhysicalDevices[pair.Key].Enabled, Is.EqualTo(pair.Value.Enabled),
                    $"Physical input device '{pair.Value.Name}' ({pair.Key}) enabled state was not restored.");
            }
            AssertVirtualInputDevicesReleased();
        }

        private sealed class InputStateSnapshot
        {
            public string EditorInputBehavior { get; set; }
            public string BackgroundBehavior { get; set; }
            public Dictionary<int, PhysicalDeviceState> PhysicalDevices { get; } = new();
        }

        private sealed class PhysicalDeviceState
        {
            public PhysicalDeviceState(string name, bool enabled)
            {
                Name = name;
                Enabled = enabled;
            }

            public string Name { get; }
            public bool Enabled { get; }
        }

        private static string GetPlanPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Tests",
                "gameplay-specs",
                "compiled",
                fileName));
        }

        private static void ResetPureRunState()
        {
            PureRunSessionStore.Clear();
            RoguelikeMapRuntimeState.ClearAll();
            BattleSettlementFlow.Instance.Unsubscribe();
            BattleSettlementCoordinator.Instance.Reset();
        }

        private static void DestroyOwnedUiInstances()
        {
            if (UIManager.Instance == null)
                return;

            foreach (UIManager.UIId uiId in System.Enum.GetValues(typeof(UIManager.UIId)))
                UIManager.Instance.Destroy(uiId);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                Assert.Fail($"Task failed: {task.Exception}");
        }
    }
}
