using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Testing.Gameplay;
using Tactics.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Isolated Home-scene smoke coverage for production PlayerInput UI interaction.
    /// </summary>
    public sealed class HomeSceneInputSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = false;
            PlayerInputGameplayStepAdapter.RemoveResidualVirtualTestDevices();

            var initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(initializeTask);
            Assume.That(initializeTask.Result, Is.Not.Null, "GameAssetManager should be initialized.");

            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (eventSystem != null)
                    Object.Destroy(eventSystem.gameObject);
            }
            yield return null;

            var loadHomeTask = initializeTask.Result.LoadSceneAsync(
                SceneProjectPathHelper.ToProjectPath("Home"),
                LoadSceneMode.Single);
            yield return WaitForTask(loadHomeTask);

            DestroyCachedUiInstances();
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

            Assert.That(homeController?.IsReadyForInput, Is.True,
                "Home UI should be wired to the current UIDocument tree.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            DestroyCachedUiInstances();
            yield return null;
            PlayerInputGameplayStepAdapter.RemoveResidualVirtualTestDevices();
            TestGameAssetHelper.Cleanup();
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_OpensOptionsThroughPlayerInput()
        {
            var task = ExecutePlan(GetPlanPath("home-options-player-input-smoke.plan.json"));
            yield return WaitForTask(task);

            AssertPlanPassed(task.Result);
            Assert.That(PlayerInputGameplayStepAdapter.HasVirtualTestDevices, Is.False,
                "The runtime runner should release all test-owned virtual input devices.");
        }

        private static async Task<GameplayTestResult> ExecutePlan(string planPath)
        {
            Assert.IsTrue(File.Exists(planPath), $"Plan file not found: {planPath}");
            var plan = ExecutableScenarioPlanLoader.FromFile(planPath);
            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[]
            {
                new PlayerInputGameplayStepAdapter(),
                new UiGameplayStepAdapter()
            });
            return await runner.ExecuteAsync(plan);
        }

        private static void AssertPlanPassed(GameplayTestResult result)
        {
            var stepTrace = string.Join("; ", result.ExecutedSteps.Select(step => $"{step.Kind}: {step.Message}"));
            var details = $"Passed={result.Passed}, Steps={result.ExecutedSteps.Count}, Assertions={result.Assertions.Count}, " +
                          $"Diagnostics=[{string.Join("; ", result.Diagnostics)}], StepTrace=[{stepTrace}]";
            Assert.IsTrue(result.Passed, details);
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

        private static void DestroyCachedUiInstances()
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
