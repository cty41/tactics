using NUnit.Framework;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System;
using Tactics.AssetPipeline;
using Tactics.Common.Battle.Runtime;
using Tactics.Common.Battle;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Tween;
using Unity.Profiling;
using UnityEngine.TestTools;
using UnityEngine;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Covers fault observation and teardown ordering for battle-owned asynchronous work.
    /// </summary>
    public sealed class BattleRuntimeScopePlayModeTests
    {
        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(task, 10d, "Complete async test operation");
            Assert.That(task.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            bool runtimeTeardownCompleted = true;
            Exception runtimeTeardownException = null;
            bool managerRestoreCompleted = true;
            bool managerRestoreFaulted = false;
            var cleanupFailures = new List<Exception>();
            BattleController controller = null;
            RecordCleanupFailure(cleanupFailures, "Resolve BattleController", () =>
                controller = BattleController.Instance);
            if (controller != null)
            {
                Task teardownTask = null;
                RecordCleanupFailure(cleanupFailures, "Start BattleController runtime teardown", () =>
                    teardownTask = controller.TeardownRuntimeScopeAsync());
                if (teardownTask == null)
                {
                    runtimeTeardownCompleted = false;
                }
                else
                {
                    for (int frame = 0; frame < 30 && !teardownTask.IsCompleted; frame++)
                        yield return null;

                    runtimeTeardownCompleted = teardownTask.IsCompleted;
                    RecordCleanupFailure(cleanupFailures, "Read BattleController teardown result", () =>
                        runtimeTeardownException = controller.RuntimeScopeTeardownException);
                }

                RecordCleanupFailure(cleanupFailures, "Destroy BattleController", () =>
                    Object.Destroy(controller.gameObject));
                yield return null;
            }

            GameAssetManager manager = null;
            RecordCleanupFailure(cleanupFailures, "Resolve GameAssetManager", () =>
                manager = GameAssetManager.Instance);
            if (manager == null)
            {
                Task<GameAssetManager> restoreManagerTask = null;
                RecordCleanupFailure(cleanupFailures, "Start GameAssetManager restore", () =>
                    restoreManagerTask = TestGameAssetHelper.EnsureInitialized());
                if (restoreManagerTask == null)
                {
                    managerRestoreCompleted = false;
                    managerRestoreFaulted = true;
                }
                else
                {
                    for (int frame = 0; frame < 30 && !restoreManagerTask.IsCompleted; frame++)
                        yield return null;

                    managerRestoreCompleted = restoreManagerTask.IsCompleted;
                    managerRestoreFaulted = restoreManagerTask.IsFaulted;
                }
            }

            for (int frame = 0; frame < 3; frame++)
                yield return null;

            RecordCleanupFailure(cleanupFailures, "Destroy Battle UI", () =>
                UIManager.Instance?.Destroy(UIManager.UIId.Battle));
            RecordCleanupFailure(cleanupFailures, "Destroy CheatConsole UI", () =>
                UIManager.Instance?.Destroy(UIManager.UIId.CheatConsole));
            yield return null;

            RecordCleanupFailure(cleanupFailures, "Resume game time", GameTimeService.ForceResume);
            RecordCleanupFailure(cleanupFailures, "Reset playback speed", () =>
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal));
            RecordCleanupFailure(cleanupFailures, "Cleanup test assets", TestGameAssetHelper.Cleanup);
            yield return null;

            Assert.That(runtimeTeardownCompleted, Is.True,
                "BattleController runtime scope teardown must complete within 30 frames during fixture cleanup.");
            Assert.That(runtimeTeardownException, Is.Null,
                "BattleController runtime scope teardown must not observe tracked-task, cancellation, or disposal failures.");
            Assert.That(managerRestoreCompleted, Is.True,
                "Fixture cleanup must restore GameAssetManager within 30 frames.");
            Assert.That(managerRestoreFaulted, Is.False,
                "Fixture cleanup must restore GameAssetManager without faulting.");
            Assert.That(BattleController.Instance, Is.Null,
                "Fixture cleanup must leave no BattleController singleton behind.");
            Assert.That(cleanupFailures, Is.Empty,
                "Fixture cleanup steps failed after all remaining cleanup steps were attempted: " +
                string.Join(" | ", cleanupFailures.Select(exception => exception.Message)));
        }

        [Test]
        public void Track_AlreadyFaultedTask_IsObservedOnce()
        {
            using var scope = new BattleRuntimeScope();
            Task faulted = Task.FromException(new InvalidOperationException("already faulted"));

            scope.Track(faulted);
            scope.Track(faulted);

            var exception = Assert.ThrowsAsync<AggregateException>(async () =>
                await scope.WhenIdleAsync());
            Assert.That(exception.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(exception.InnerExceptions[0].Message, Is.EqualTo("already faulted"));
        }

        [Test]
        public async Task Track_TaskFaultingAfterRegistration_IsObserved()
        {
            using var scope = new BattleRuntimeScope();
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            scope.Track(completion.Task);

            completion.SetException(new InvalidOperationException("faulted after track"));
            await Task.Yield();

            var exception = Assert.ThrowsAsync<AggregateException>(async () =>
                await scope.WhenIdleAsync());
            Assert.That(exception.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(exception.InnerExceptions[0].Message, Is.EqualTo("faulted after track"));
        }

        [Test]
        public async Task Track_ConcurrentCompletionStress_ObservesEveryFaultWithoutDuplicates()
        {
            const int IterationCount = 512;
            using var scope = new BattleRuntimeScope();
            var producers = new List<Task>(IterationCount);

            for (int index = 0; index < IterationCount; index++)
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                int capturedIndex = index;
                producers.Add(Task.Run(() => completion.TrySetException(
                    new InvalidOperationException($"concurrent fault {capturedIndex}"))));
                scope.Track(completion.Task);
            }

            await Task.WhenAll(producers);
            await Task.Yield();

            var exception = Assert.ThrowsAsync<AggregateException>(async () =>
                await scope.WhenIdleAsync());
            Assert.That(exception.InnerExceptions, Has.Count.EqualTo(IterationCount));
        }

        [Test]
        public void Track_CanceledTask_IsNotReportedAsTeardownFault()
        {
            using var scope = new BattleRuntimeScope();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            scope.Track(Task.FromCanceled(cancellation.Token));

            Assert.DoesNotThrowAsync(async () => await scope.WhenIdleAsync());
        }

        [Test]
        public void Track_MultipleFaults_AreAggregatedWithoutDuplicateTasks()
        {
            using var scope = new BattleRuntimeScope();
            Task first = Task.FromException(new InvalidOperationException("first fault"));
            Task second = Task.FromException(new ArgumentException("second fault"));

            scope.Track(first);
            scope.Track(first);
            scope.Track(second);

            var exception = Assert.ThrowsAsync<AggregateException>(async () =>
                await scope.WhenIdleAsync());
            Assert.That(exception.InnerExceptions, Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DestroyingController_CancelsBeforeReleasingTrackedAssetPaths()
        {
            var controllerObject = new GameObject("DirectDestroyRuntimeScopeController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            controllerObject.SetActive(true);
            controllerObject.SetActive(false);
            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var releasedPaths = new List<string>();

            Task startTask = controller.StartBattleAsync();
            yield return WaitForTask(startTask, 5f, "battle start");
            Assert.That(startTask.IsFaulted, Is.False, startTask.Exception?.ToString());

            IBattleRuntimeScope scope = controller.RuntimeScope;
            Assert.That(scope, Is.Not.Null);
            scope.Track(trackedCompletion.Task);

            SetPrivateField(controller, "_runtimeAssetReleaseOverrideForTests",
                new Action<string>(path => releasedPaths.Add(path)));
            var loadedPaths = (HashSet<string>)GetPrivateField(controller, "_loadedPaths");
            loadedPaths.Add("test://runtime-owned-asset");

            UnityEngine.Object.DestroyImmediate(controllerObject);

            Assert.That(scope.Token.IsCancellationRequested, Is.True,
                "OnDestroy must synchronously cancel the runtime scope.");
            Assert.That(releasedPaths, Is.Empty,
                "Tracked asset paths must remain owned until runtime work drains.");

            Assert.That(trackedCompletion.TrySetResult(true), Is.True);
            float releaseDeadline = Time.realtimeSinceStartup + 2f;
            while (releasedPaths.Count == 0 && Time.realtimeSinceStartup < releaseDeadline)
                yield return null;

            Assert.That(releasedPaths, Is.EqualTo(new[] { "test://runtime-owned-asset" }),
                "The transferred path snapshot must release exactly once after drain.");
            Assert.That(BattleController.Instance, Is.Null);
        }

        [UnityTest]
        public IEnumerator TeardownRuntimeScopeAsync_ExposesFaultAndStillReleasesScope()
        {
            var controllerObject = new GameObject("FaultedRuntimeScopeController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            controllerObject.SetActive(true);
            controllerObject.SetActive(false);

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 5f, "battle start");
                Assert.That(startTask.IsFaulted, Is.False, startTask.Exception?.ToString());

                controller.RuntimeScope.Track(Task.FromException(
                    new InvalidOperationException("tracked teardown fault")));
                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex("Runtime scope drain failed"));

                Task teardownTask = controller.TeardownRuntimeScopeAsync();
                yield return WaitForTask(teardownTask, 5f, "runtime scope teardown");

                Assert.That(teardownTask.IsFaulted, Is.False,
                    "Controller teardown reports tracked faults without abandoning cleanup.");
                Assert.That(controller.RuntimeScope, Is.Null);
                Assert.That(controller.RuntimeScopeTeardownException, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }


        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
        [UnityTest]
        public System.Collections.IEnumerator StartBattleAsync_CreatesRuntimeScopeBeforeBattleStarted()
        {
            var controllerObject = new GameObject("StartScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            IBattleRuntimeScope scopeSeenByBattleStarted = null;
            controller.BattleStarted += () => scopeSeenByBattleStarted = controller.RuntimeScope;

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 10d, "Complete battle startup");

                Assert.That(startTask.IsFaulted, Is.False);
                Assert.That(controller.IsBattleActive, Is.True);
                Assert.That(controller.RuntimeScope, Is.Not.Null);
                Assert.That(controller.RuntimeScope, Is.TypeOf<BattleRuntimeScope>());
                Assert.That(controller.RuntimeScope.Token.CanBeCanceled, Is.True);
                Assert.That(controller.RuntimeScope.IsCancelling, Is.False);
                Assert.That(scopeSeenByBattleStarted, Is.SameAs(controller.RuntimeScope));
            }
            finally
            {
                controller.EndBattle(default);
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator StartBattleAsync_DoesNotOpenCheatConsole()
        {
            UIManager.Instance.Destroy(UIManager.UIId.CheatConsole);
            var controllerObject = new GameObject("BattleWithoutDefaultCheatConsole");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 10d, "Complete battle startup");
                Assert.That(startTask.IsFaulted, Is.False);

                Task startupUiTask = controller.RuntimeScope.WhenIdleAsync();
                yield return WaitForTask(startupUiTask, 10d, "Complete battle UI startup");

                Assert.That(UIManager.Instance.IsVisible(UIManager.UIId.CheatConsole), Is.False,
                    "Starting a battle must not open the cheat console; it remains available through ToggleConsole.");
            }
            finally
            {
                controller.EndBattle(default);
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator EndBattle_CancelsScopeBeforePublishingBattleEnded()
        {
            var controllerObject = new GameObject("EndScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 10d, "Complete battle startup");
                Assert.That(startTask.IsFaulted, Is.False);
                Assert.That(controller.RuntimeScope, Is.Not.Null);

                IBattleRuntimeScope scope = controller.RuntimeScope;
                CancellationToken token = scope.Token;
                Task trackedTask = trackedCompletion.Task;
                scope.Track(trackedTask);
                int battleEndedCount = 0;
                bool cancellationSeenByBattleEnded = false;
                controller.BattleEnded += _ =>
                {
                    battleEndedCount++;
                    cancellationSeenByBattleEnded = token.IsCancellationRequested;
                };

                controller.EndBattle(default);

                Assert.That(controller.IsBattleActive, Is.False);
                Assert.That(token.IsCancellationRequested, Is.True);
                Assert.That(battleEndedCount, Is.EqualTo(1));
                Assert.That(cancellationSeenByBattleEnded, Is.True);
                Assert.That(trackedTask.IsCompleted, Is.False);

                Assert.That(trackedCompletion.TrySetResult(true), Is.True);
                for (int frame = 0; frame < 10 && controller.RuntimeScope != null; frame++)
                    yield return null;

                Assert.That(controller.RuntimeScope, Is.Null);
            }
            finally
            {
                trackedCompletion.TrySetResult(true);
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator EndBattleAsync_WaitsForTrackedTaskThenReleasesScope()
        {
            var controllerObject = new GameObject("AsyncEndScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 10d, "Complete battle startup");
                Assert.That(startTask.IsFaulted, Is.False);
                Assert.That(controller.RuntimeScope, Is.Not.Null);

                IBattleRuntimeScope scope = controller.RuntimeScope;
                CancellationToken token = scope.Token;
                scope.Track(trackedCompletion.Task);
                int battleEndedCount = 0;
                controller.BattleEnded += _ => battleEndedCount++;

                Task endTask = controller.EndBattleAsync(default);
                yield return null;

                Assert.That(token.IsCancellationRequested, Is.True);
                Assert.That(endTask.IsCompleted, Is.False);
                Assert.That(battleEndedCount, Is.Zero);

                Assert.That(trackedCompletion.TrySetResult(true), Is.True);
                yield return WaitForTask(endTask, 10d, "Complete battle teardown");

                Assert.That(endTask.IsFaulted, Is.False);
                Assert.That(battleEndedCount, Is.EqualTo(1));
                Assert.That(controller.RuntimeScope, Is.Null);
            }
            finally
            {
                trackedCompletion.TrySetResult(true);
                if (controller != null && controller.IsBattleActive)
                    controller.EndBattle(default);
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator DestroyingBattleController_CancelsRuntimeScope()
        {
            var controllerObject = new GameObject("DestroyedScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            controllerObject.SetActive(true);
            controllerObject.SetActive(false);
            var cancellationObserved = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 10d, "Complete battle startup");
                Assert.That(startTask.IsFaulted, Is.False);
                Assert.That(controller.RuntimeScope, Is.Not.Null);

                IBattleRuntimeScope scope = controller.RuntimeScope;
                CancellationToken token = scope.Token;
                using var registration = token.Register(() => cancellationObserved.TrySetResult(true));

                Object.Destroy(controllerObject);
                yield return null;

                Assert.That(token.IsCancellationRequested, Is.True,
                    "Destroying a previously activated BattleController must cancel its runtime scope token.");
                Assert.That(cancellationObserved.Task.IsCompleted, Is.True,
                    "Destroying a previously activated BattleController must complete the cancellation callback.");
            }
            finally
            {
                if (controllerObject != null)
                    Object.Destroy(controllerObject);
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator BattleRuntimeScope_RejectsTrackAfterCancel()
        {
            using var scope = new BattleRuntimeScope();
            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                scope.Cancel();
                scope.Track(trackedCompletion.Task);
                Task idleTask = scope.WhenIdleAsync();
                yield return null;

                Assert.That(idleTask.IsCompleted, Is.True);
            }
            finally
            {
                trackedCompletion.TrySetResult(true);
            }
        }

        [Test]
        public void BattleRuntimeScope_TryTrackReportsWhetherOwnershipWasAccepted()
        {
            using var scope = new BattleRuntimeScope();
            var acceptedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var rejectedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                var tryTrack = typeof(BattleRuntimeScope).GetMethod(
                    "TryTrack",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(tryTrack, Is.Not.Null,
                    "The scope must expose an atomic ownership-acceptance result.");
                Assert.That((bool)tryTrack.Invoke(scope, new object[] { acceptedCompletion.Task }), Is.True);
                scope.Cancel();
                Assert.That((bool)tryTrack.Invoke(scope, new object[] { rejectedCompletion.Task }), Is.False);
            }
            finally
            {
                acceptedCompletion.TrySetResult(true);
                rejectedCompletion.TrySetResult(true);
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator PlayerSkillExecutionOwner_IsDrainedAndRejectedAfterCancellation()
        {
            var startTrackedExecution = typeof(Tactics.Common.Units.Abilities.SkillGraphAbilityImpl).GetMethod(
                "StartTrackedPlayerExecution",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(startTrackedExecution, Is.Not.Null,
                "Player-triggered SkillGraph execution must have one tracked ownership boundary.");

            using var scope = new BattleRuntimeScope();
            var executionGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            bool executionStarted = false;
            Func<Task> executionFactory = async () =>
            {
                executionStarted = true;
                await executionGate.Task;
            };

            startTrackedExecution.Invoke(null, new object[] { scope, executionFactory });
            Assert.That(executionStarted, Is.True);
            scope.Cancel();
            Task idleTask = scope.WhenIdleAsync();
            yield return null;
            Assert.That(idleTask.IsCompleted, Is.False,
                "Runtime teardown must drain the complete player SkillGraph task.");

            executionGate.TrySetResult(true);
            yield return WaitForTask(idleTask, 10d, "Drain tracked player SkillGraph execution");
            Assert.That(idleTask.IsFaulted, Is.False);

            bool rejectedFactoryStarted = false;
            startTrackedExecution.Invoke(
                null,
                new object[] { scope, new Func<Task>(() =>
                {
                    rejectedFactoryStarted = true;
                    return Task.CompletedTask;
                }) });
            Assert.That(rejectedFactoryStarted, Is.False,
                "A cancelling scope must reject a new cast before its execution factory starts.");
        }

        [UnityTest]
        public System.Collections.IEnumerator PendingStart_IsInvalidatedByEndBattleDuringPreviousScopeTeardown()
        {
            var controllerObject = new GameObject("PendingStartScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            var teardownBlocker = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int battleStartedCount = 0;
            controller.BattleStarted += () => battleStartedCount++;

            try
            {
                Task firstStartTask = controller.StartBattleAsync();
                for (int frame = 0; frame < 30 && !firstStartTask.IsCompleted; frame++)
                    yield return null;

                Assert.That(firstStartTask.IsCompleted, Is.True,
                    "The initial battle start must complete within 30 frames.");
                Assert.That(firstStartTask.IsFaulted, Is.False,
                    "The initial battle start must not fault.");
                Assert.That(battleStartedCount, Is.EqualTo(1),
                    "The initial battle start must publish BattleStarted exactly once.");

                IBattleRuntimeScope scope = controller.RuntimeScope;
                Assert.That(scope, Is.Not.Null,
                    "The initial battle must publish a runtime scope.");
                scope.Track(teardownBlocker.Task);

                controller.EndBattle(default);
                Task pendingStartTask = controller.StartBattleAsync();
                Assert.That(pendingStartTask.IsCompleted, Is.False,
                    "A second start must remain pending while the previous scope drains.");

                controller.EndBattle(default);
                Assert.That(teardownBlocker.TrySetResult(true), Is.True,
                    "The teardown blocker must be released exactly once.");

                for (int frame = 0; frame < 30 && !pendingStartTask.IsCompleted; frame++)
                    yield return null;

                Assert.That(pendingStartTask.IsCompleted, Is.True,
                    "The invalidated pending start must settle within 30 frames.");
                Assert.That(pendingStartTask.IsFaulted, Is.False,
                    "The invalidated pending start must not fault.");
                for (int frame = 0; frame < 3; frame++)
                    yield return null;

                Assert.That(battleStartedCount, Is.EqualTo(1),
                    "EndBattle during teardown must invalidate the pending start without publishing BattleStarted again.");
                Assert.That(controller.IsBattleActive, Is.False,
                    "An invalidated pending start must leave the battle inactive.");
                Assert.That(controller.RuntimeScope, Is.Null,
                    "An invalidated pending start must not publish a replacement runtime scope.");
            }
            finally
            {
                teardownBlocker.TrySetResult(true);
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator TimeoutCancellation_RejectsSubsequentTrack()
        {
            using var scope = new BattleRuntimeScope(System.TimeSpan.FromMilliseconds(30));
            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                var timeoutWait = System.Diagnostics.Stopwatch.StartNew();
                while (!scope.Token.IsCancellationRequested &&
                       timeoutWait.Elapsed < System.TimeSpan.FromSeconds(1))
                    yield return null;

                Assert.That(scope.Token.IsCancellationRequested, Is.True,
                    "The runtime scope timeout must cancel its token within one second of real time.");
                scope.Track(trackedCompletion.Task);
                Task idleTask = scope.WhenIdleAsync();
                yield return null;

                Assert.That(idleTask.IsCompleted, Is.True,
                    "A timed-out runtime scope must reject tasks tracked after cancellation.");
                Assert.That(scope.IsCancelling, Is.True,
                    "A timed-out runtime scope must report that it is cancelling.");
            }
            finally
            {
                trackedCompletion.TrySetResult(true);
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator BattleRuntimeScope_DisposeFromCancellationCallback_IsIdempotent()
        {
            var scope = new BattleRuntimeScope();
            using var registration = scope.Token.Register(scope.Dispose);

            Assert.DoesNotThrow(scope.Cancel,
                "Disposing the scope from a cancellation callback must not race cancellation dispatch.");
            Assert.That(scope.Token.IsCancellationRequested, Is.True,
                "Cancellation must remain observable after callback-driven disposal.");
            Assert.DoesNotThrow(scope.Dispose,
                "Repeated disposal after callback-driven disposal must be idempotent.");

            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            scope.Track(trackedCompletion.Task);
            Task idleTask = scope.WhenIdleAsync();
            yield return null;

            Assert.That(idleTask.IsCompleted, Is.True,
                "A disposed scope must reject subsequently tracked tasks.");
            trackedCompletion.TrySetResult(true);
        }

        [UnityTest]
        public System.Collections.IEnumerator BattleRuntimeScope_DisposeFromCancellationCallback_PreservesTrackedDrain()
        {
            var scope = new BattleRuntimeScope();
            var trackedCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            scope.Track(trackedCompletion.Task);
            using var registration = scope.Token.Register(scope.Dispose);

            scope.Cancel();
            Task idleTask = scope.WhenIdleAsync();
            yield return null;

            Assert.That(idleTask.IsCompleted, Is.False,
                "Callback-driven disposal must not discard tasks that teardown still needs to drain.");

            Assert.That(trackedCompletion.TrySetResult(true), Is.True,
                "The tracked drain blocker must be released exactly once.");
            for (int frame = 0; frame < 30 && !idleTask.IsCompleted; frame++)
                yield return null;

            Assert.That(idleTask.IsCompleted, Is.True,
                "The tracked drain must complete within 30 frames after its blocker is released.");
            Assert.That(idleTask.IsFaulted, Is.False,
                "Callback-driven disposal must preserve a successful drain.");
        }

        [Test]
        public void BattleRuntimeScope_TimeoutBoundary_ContainsCancellationCallbackExceptions()
        {
            using var scope = new BattleRuntimeScope();
            var timeoutMethod = typeof(BattleRuntimeScope).GetMethod(
                "CancelFromTimeout",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(timeoutMethod, Is.Not.Null,
                "Timeout cancellation must use a dedicated exception boundary.");

            using var registration = scope.Token.Register(
                () => throw new System.InvalidOperationException("timeout callback failure"));
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "BattleRuntimeScope.*timeout cancellation callback failed"));

            Assert.DoesNotThrow(() => timeoutMethod.Invoke(scope, null),
                "A cancellation callback exception must not escape the Timer boundary.");
            Assert.That(scope.Token.IsCancellationRequested, Is.True,
                "The token must remain cancelled when a timeout callback throws.");
        }

        [UnityTest]
        public System.Collections.IEnumerator BattleRuntimeScope_AlreadyFaultedTrackedTask_IsObservedByDrain()
        {
            using var scope = new BattleRuntimeScope();
            scope.Track(Task.FromException(
                new System.InvalidOperationException("already faulted tracked task")));

            Task idleTask = scope.WhenIdleAsync();
            yield return WaitForTask(idleTask, 10d, "Drain runtime scope");

            Assert.That(idleTask.IsFaulted, Is.True,
                "A task that is already faulted when tracked must remain observable by teardown.");
        }

        [UnityTest]
        public System.Collections.IEnumerator BattleRuntimeScope_CompletedFaultedTask_IsObservedAfterRemoval()
        {
            using var scope = new BattleRuntimeScope();
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            scope.Track(completion.Task);
            completion.SetException(
                new System.InvalidOperationException("completed faulted tracked task"));
            yield return WaitForTask(completion.Task, 10d, "Complete tracked task");
            yield return null;

            Task idleTask = scope.WhenIdleAsync();
            yield return WaitForTask(idleTask, 10d, "Drain runtime scope");

            Assert.That(idleTask.IsFaulted, Is.True,
                "A tracked task fault must remain observable after completion cleanup removes the task.");
        }

        [UnityTest]
        public System.Collections.IEnumerator BattleController_TeardownExposesTrackedFaultAfterCleanup()
        {
            var controllerObject = new GameObject("FaultedScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            controllerObject.SetActive(true);
            controllerObject.SetActive(false);

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 10d, "Complete battle startup");
                Assert.That(startTask.IsFaulted, Is.False);

                controller.RuntimeScope.Track(Task.FromException(
                    new InvalidOperationException("tracked runtime scope failure")));
                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex("Runtime scope drain failed"));

                Task teardownTask = controller.TeardownRuntimeScopeAsync();
                yield return WaitForTask(teardownTask, 10d, "Complete runtime teardown");

                Assert.That(teardownTask.IsFaulted, Is.False,
                    "Runtime cleanup must complete even when a tracked task faults.");
                Assert.That(controller.RuntimeScope, Is.Null,
                    "Runtime cleanup must dispose and release the faulted scope.");
                Assert.That(controller.RuntimeScopeTeardownException, Is.Not.Null,
                    "Runtime cleanup must expose the tracked drain fault without relying on log policy.");
            }
            finally
            {
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [Test]
        public void FixtureCleanupStep_RecordsFailureAndContinuesWithLaterSteps()
        {
            var failures = new List<Exception>();
            int laterStepCalls = 0;

            RecordCleanupFailure(failures, "throwing step", () =>
                throw new InvalidOperationException("fixture cleanup failure"));
            RecordCleanupFailure(failures, "later step", () => laterStepCalls++);

            Assert.That(laterStepCalls, Is.EqualTo(1),
                "A synchronous fixture cleanup failure must not skip later cleanup steps.");
            Assert.That(failures, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public System.Collections.IEnumerator StartBattleAsync_TracksBattleUiTaskInRuntimeScope()
        {
            TestGameAssetHelper.Cleanup();
            yield return null;
            if (GameAssetManager.Instance != null)
            {
                Object.DestroyImmediate(GameAssetManager.Instance.gameObject);
                yield return null;
            }

            Assert.That(GameAssetManager.Instance, Is.Null,
                "The startup UI tracking contract requires GameAssetManager to be absent.");

            var controllerObject = new GameObject("StartupUiScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();

            try
            {
                Task startTask = controller.StartBattleAsync();
                IBattleRuntimeScope scope = controller.RuntimeScope;
                Assert.That(scope, Is.Not.Null,
                    "StartBattleAsync must publish its runtime scope before scheduling startup UI tasks.");

                Task idleTask = scope.WhenIdleAsync();
                yield return null;
                Assert.That(idleTask.IsCompleted, Is.False,
                    "The battle UI readiness wait must remain tracked while GameAssetManager is absent.");
                Assert.That(startTask.IsFaulted, Is.False,
                    "Starting a battle without GameAssetManager must not fault.");

                Task endTask = controller.EndBattleAsync(default);
                for (int frame = 0; frame < 30 && !endTask.IsCompleted; frame++)
                    yield return null;

                Assert.That(endTask.IsCompleted, Is.True,
                    "Ending the battle must cancel and drain startup UI tasks within 30 frames.");
                Assert.That(endTask.IsFaulted, Is.False,
                    "Ending the battle while startup UI tasks are pending must not fault.");
                Assert.That(controller.RuntimeScope, Is.Null,
                    "Ending the battle must release the runtime scope after startup UI tasks drain.");
            }
            finally
            {
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator TeardownRuntimeScopeAsync_ReentrantCancellationReturnsPublishedTask()
        {
            var controllerObject = new GameObject("ReentrantTeardownScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();
            var teardownBlocker = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                Task startTask = controller.StartBattleAsync();
                for (int frame = 0; frame < 30 && !startTask.IsCompleted; frame++)
                    yield return null;

                Assert.That(startTask.IsCompleted, Is.True,
                    "Battle start must complete within 30 frames before testing reentrant teardown.");
                Assert.That(startTask.IsFaulted, Is.False,
                    "Battle start must not fault before testing reentrant teardown.");

                IBattleRuntimeScope scope = controller.RuntimeScope;
                Assert.That(scope, Is.Not.Null,
                    "Battle start must publish a runtime scope before testing reentrant teardown.");
                scope.Track(teardownBlocker.Task);

                Task callbackTask = null;
                using var registration = scope.Token.Register(
                    () => callbackTask = controller.TeardownRuntimeScopeAsync());

                Task endTask = controller.EndBattleAsync(default);
                Task outsideTask = controller.TeardownRuntimeScopeAsync();

                Assert.That(callbackTask, Is.Not.Null,
                    "Synchronous cancellation must invoke the reentrant teardown callback.");
                Assert.That(callbackTask, Is.SameAs(outsideTask),
                    "Reentrant and outside teardown callers must receive the same published task.");
                Assert.That(outsideTask.IsCompleted, Is.False,
                    "The published teardown task must remain incomplete while a tracked task is blocked.");

                Assert.That(teardownBlocker.TrySetResult(true), Is.True,
                    "The teardown blocker must be released exactly once.");
                for (int frame = 0; frame < 30 && (!outsideTask.IsCompleted || !endTask.IsCompleted); frame++)
                    yield return null;

                Assert.That(outsideTask.IsCompleted, Is.True,
                    "The published teardown task must complete within 30 frames after the blocker is released.");
                Assert.That(outsideTask.IsFaulted, Is.False,
                    "The published teardown task must not fault.");
                Assert.That(endTask.IsCompleted, Is.True,
                    "EndBattleAsync must complete within 30 frames after teardown drains.");
                Assert.That(endTask.IsFaulted, Is.False,
                    "EndBattleAsync must not fault after teardown drains.");
                Assert.That(controller.RuntimeScope, Is.Null,
                    "Completed teardown must clear the controller runtime scope.");
            }
            finally
            {
                teardownBlocker.TrySetResult(true);
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        private static void RecordCleanupFailure(
            ICollection<Exception> failures,
            string stepName,
            Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(
                    $"{stepName} failed during fixture cleanup.",
                    exception));
            }
        }

        private static System.Collections.IEnumerator WaitForTask(
            Task task,
            double timeoutSeconds,
            string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            int frameCount = 0;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
            {
                frameCount++;
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True,
                $"{label} timed out after {timeoutSeconds:F1}s and {frameCount} frames; status={task.Status}.");
        }

    }
}
