using System.Collections;
using System.IO;
using System.Linq;
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
        public IEnumerator RuntimeRunner_ReleasesVirtualDevicesAfterActionFailure()
        {
            var plan = CreateInputLifecyclePlan(
                timeoutMs: 10000,
                runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"clickPointerTarget\",\"target\":\"MissingButton\",\"parameters\":{\"targetKind\":\"UiElement\"}}");

            var task = ExecutePlan(plan);
            yield return WaitForTask(task);

            Assert.IsFalse(task.Result.Passed);
            AssertVirtualInputDevicesReleased();
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ReleasesVirtualDevicesAfterTimeout()
        {
            int initializationCountBefore = PlayerInputGameplayStepAdapter.TotalInitializationCount;
            var plan = CreateInputLifecyclePlan(
                timeoutMs: 20,
                runtimeActionJson: "{\"adapter\":\"PlayerInput\",\"kind\":\"waitForPlayerObservable\",\"parameters\":{\"observable\":\"uiElement\",\"elementName\":\"NeverCreatedElement\",\"maximumFrames\":10000}}");

            var task = ExecutePlan(plan);
            yield return WaitForTask(task);

            Assert.That(task.Result.FailureCategory, Is.EqualTo(FailureCategory.Timeout));
            Assert.That(PlayerInputGameplayStepAdapter.TotalInitializationCount, Is.GreaterThan(initializationCountBefore));
            AssertVirtualInputDevicesReleased();
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
