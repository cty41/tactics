using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Reflection;
using Tactics.Common.AI;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Players;
using Tactics.Common.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class GameTimeServiceSpeedTests
    {
        [SetUp]
        public void SetUp()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
        }

        [TearDown]
        public void TearDown()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
        }

        [TestCase(GamePlaybackSpeed.Half, 0.5f)]
        [TestCase(GamePlaybackSpeed.Normal, 1f)]
        [TestCase(GamePlaybackSpeed.Double, 2f)]
        [TestCase(GamePlaybackSpeed.Quadruple, 4f)]
        public void SetPlaybackSpeed_AppliesSupportedGlobalScale(GamePlaybackSpeed speed, float expectedScale)
        {
            GameTimeService.SetPlaybackSpeed(speed);

            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(speed));
            Assert.That(GameTimeService.PlaybackScale, Is.EqualTo(expectedScale));
            Assert.That(GameTimeService.EffectiveTimeScale, Is.EqualTo(expectedScale));
            Assert.That(Time.timeScale, Is.EqualTo(expectedScale));
        }

        [Test]
        public void SetPlaybackSpeed_InvalidValue_IsRejectedWithoutMutatingState()
        {
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                GameTimeService.SetPlaybackSpeed((GamePlaybackSpeed)3));

            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Double));
            Assert.That(GameTimeService.PlaybackScale, Is.EqualTo(2f));
            Assert.That(GameTimeService.EffectiveTimeScale, Is.EqualTo(2f));
            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        [Test]
        public void CyclePlaybackSpeed_UsesOneTwoFourHalfOneOrder()
        {
            Assert.That(GameTimeService.CyclePlaybackSpeed(), Is.EqualTo(GamePlaybackSpeed.Double));
            Assert.That(GameTimeService.CyclePlaybackSpeed(), Is.EqualTo(GamePlaybackSpeed.Quadruple));
            Assert.That(GameTimeService.CyclePlaybackSpeed(), Is.EqualTo(GamePlaybackSpeed.Half));
            Assert.That(GameTimeService.CyclePlaybackSpeed(), Is.EqualTo(GamePlaybackSpeed.Normal));
            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Normal));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void NestedPause_FinalResumeRestoresSelectedSpeed()
        {
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Quadruple);

            GameTimeService.Pause();
            GameTimeService.Pause();
            Assert.That(GameTimeService.IsPaused, Is.True);
            Assert.That(GameTimeService.EffectiveTimeScale, Is.EqualTo(0f));
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            GameTimeService.Resume();
            Assert.That(GameTimeService.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            GameTimeService.Resume();
            Assert.That(GameTimeService.IsPaused, Is.False);
            Assert.That(GameTimeService.EffectiveTimeScale, Is.EqualTo(4f));
            Assert.That(Time.timeScale, Is.EqualTo(4f));
        }

        [Test]
        public void SetPlaybackSpeedWhilePaused_DefersEffectiveScaleUntilResume()
        {
            GameTimeService.Pause();

            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Half);

            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Half));
            Assert.That(GameTimeService.PlaybackScale, Is.EqualTo(0.5f));
            Assert.That(GameTimeService.EffectiveTimeScale, Is.EqualTo(0f));
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            GameTimeService.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void ForceResume_RestoresSelectedSpeedAndClearsNestedDepth()
        {
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Quadruple);
            GameTimeService.Pause();
            GameTimeService.Pause();

            GameTimeService.ForceResume();

            Assert.That(GameTimeService.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(4f));

            GameTimeService.Resume();
            Assert.That(GameTimeService.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(4f));
        }

        [Test]
        public void ResumeWithoutPause_IsIdempotentAtSelectedSpeed()
        {
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            GameTimeService.Resume();

            Assert.That(GameTimeService.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(2f));
        }

        [Test]
        public void GamePauseService_DelegatesToGameTimeServiceState()
        {
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Quadruple);

            GamePauseService.Pause();
            Assert.That(GamePauseService.IsPaused, Is.True);
            Assert.That(GameTimeService.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            GamePauseService.Resume();
            Assert.That(GamePauseService.IsPaused, Is.False);
            Assert.That(GameTimeService.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(4f));
        }

        [Test]
        public void SubsystemReset_RestoresNormalUnpausedState()
        {
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Quadruple);
            GameTimeService.Pause();

            var reset = typeof(GameTimeService).GetMethod(
                "ResetStatics",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(reset, Is.Not.Null, "GameTimeService must expose a subsystem reset hook.");
            reset.Invoke(null, null);

            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Normal));
            Assert.That(GameTimeService.IsPaused, Is.False);
            Assert.That(GameTimeService.EffectiveTimeScale, Is.EqualTo(1f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator DelayScaledAsync_StopsDuringPauseAndCompletesAfterResume()
        {
            GameTimeService.Pause();
            var delay = GameTimeService.DelayScaledAsync(0.05f);

            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(delay.IsCompleted, Is.False, "Scaled delay must not advance while paused.");

            GameTimeService.Resume();
            var deadline = Time.realtimeSinceStartup + 1f;
            while (!delay.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(delay.IsCompletedSuccessfully, Is.True, delay.Exception?.ToString());
        }

        [UnityTest]
        public IEnumerator DelayScaledAsync_QuadrupleSpeedCompletesFasterThanNormal()
        {
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            float normalStartedAt = Time.realtimeSinceStartup;
            var normalDelay = GameTimeService.DelayScaledAsync(0.16f);
            yield return new WaitUntil(() => normalDelay.IsCompleted);
            float normalElapsed = Time.realtimeSinceStartup - normalStartedAt;

            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Quadruple);
            float quadrupleStartedAt = Time.realtimeSinceStartup;
            var quadrupleDelay = GameTimeService.DelayScaledAsync(0.16f);
            yield return new WaitUntil(() => quadrupleDelay.IsCompleted);
            float quadrupleElapsed = Time.realtimeSinceStartup - quadrupleStartedAt;

            Assert.That(normalDelay.IsCompletedSuccessfully, Is.True, normalDelay.Exception?.ToString());
            Assert.That(quadrupleDelay.IsCompletedSuccessfully, Is.True, quadrupleDelay.Exception?.ToString());
            Assert.That(
                quadrupleElapsed,
                Is.LessThan(normalElapsed * 0.65f),
                $"Expected 4× delay to be materially faster. Normal={normalElapsed:F3}s, 4×={quadrupleElapsed:F3}s.");
        }

        [UnityTest]
        public IEnumerator DelayScaledAsync_CanBeCancelledWhilePaused()
        {
            using var cancellation = new CancellationTokenSource();
            GameTimeService.Pause();
            var delay = GameTimeService.DelayScaledAsync(10f, cancellation.Token);

            yield return null;
            cancellation.Cancel();
            var deadline = Time.realtimeSinceStartup + 1f;
            while (!delay.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(delay.IsCanceled, Is.True, delay.Exception?.ToString());
        }

        [UnityTest]
        public IEnumerator AIPlayer_CancelDuringTurnStartDelay_DoesNotEnterUnitSelection()
        {
            var gridController = new GridController();
            var selector = new CountingUnitSelector();
            var aiPlayer = new AIPlayer(debugMode: false, turnStartDelay: 200, unitDelay: 0)
            {
                UnitSelector = selector
            };
            var turnContext = new Tactics.Common.Controllers.TurnResolvers.TurnContext(
                aiPlayer,
                System.Array.Empty<IUnit>());
            typeof(GridController).GetProperty(nameof(GridController.TurnContext))?
                .SetValue(gridController, turnContext);
            aiPlayer.Initialize(gridController);

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                aiPlayer.Play(gridController);
                yield return null;

                typeof(AIPlayer).GetMethod(
                        "CancelOngoingAction",
                        BindingFlags.Instance | BindingFlags.NonPublic)?
                    .Invoke(aiPlayer, null);
                yield return new WaitForSecondsRealtime(0.3f);

                Assert.That(
                    selector.InvocationCount,
                    Is.Zero,
                    "A canceled AI turn must not continue beyond its turn-start delay.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public async Task AiBrainRunner_Execute_ThrowsWhenCancellationAlreadyRequested()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.CatchAsync<System.OperationCanceledException>(async () =>
                await AiBrainRunner.Execute(null, null, null, cts.Token));
        }

        [UnityTest]
        public IEnumerator AIPlayer_CancelDuringBrainExecution_SkipsUnitFinalization()
        {
            var gridController = new GridController();
            var unitManager = new RecordingUnitManager();
            gridController.UnitManager = unitManager;

            var unitObject = new GameObject("CancelDuringBrainUnit");
            var unit = unitObject.AddComponent<Unit>();
            unit.ApplyAiBrain(ScriptableObject.CreateInstance<AiBrainAsset>());

            var aiPlayer = new AIPlayer(debugMode: false, turnStartDelay: 0, unitDelay: 0)
            {
                UnitSelector = new SingleUnitSelector(unit)
            };
            var turnContext = new Tactics.Common.Controllers.TurnResolvers.TurnContext(
                aiPlayer,
                new IUnit[] { unit });
            typeof(GridController).GetProperty(nameof(GridController.TurnContext))?
                .SetValue(gridController, turnContext);
            aiPlayer.Initialize(gridController);

            var brainExecutorField = typeof(AIPlayer).GetField(
                "_brainExecutor",
                BindingFlags.Instance | BindingFlags.NonPublic);

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            bool deselected = false;
            unit.UnitDeselected += _ => deselected = true;
            try
            {
                Assert.That(
                    brainExecutorField,
                    Is.Not.Null,
                    "AIPlayer must expose a brain-execution seam so mid-action cancellation can be tested.");
                brainExecutorField.SetValue(
                    aiPlayer,
                    new System.Func<IUnit, GridController, AiBrainAsset, CancellationToken, Task>(
                        (u, g, brain, ct) =>
                        {
                            // Reproduces the reviewed race: cancellation lands while the AI
                            // action runs, but the action itself completes normally.
                            typeof(AIPlayer).GetMethod(
                                    "CancelOngoingAction",
                                    BindingFlags.Instance | BindingFlags.NonPublic)?
                                .Invoke(aiPlayer, null);
                            return Task.CompletedTask;
                        }));

                aiPlayer.Play(gridController);
                yield return new WaitUntil(() => unitManager.MarkAsSelectedCount > 0);
                yield return new WaitForSecondsRealtime(0.3f);

                Assert.That(
                    unitManager.MarkAsSelectedCount,
                    Is.EqualTo(1),
                    "The AI turn must enter unit selection before cancellation lands.");
                Assert.That(
                    unitManager.MarkAsFriendlyCount,
                    Is.Zero,
                    "A unit whose AI action was cancelled mid-execution must not be marked friendly afterwards.");
                Assert.That(
                    unitManager.MarkAsFinishedCount,
                    Is.Zero,
                    "A unit whose AI action was cancelled mid-execution must not be marked finished.");
                Assert.That(
                    deselected,
                    Is.False,
                    "A cancelled AI action must not emit UnitDeselected during finalization.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                if (unitObject != null)
                {
                    Object.DestroyImmediate(unitObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator AIPlayer_CancelOngoingAction_WhenIdleAsyncDrainsPlayContinuation()
        {
            var gridController = new GridController();
            var unitManager = new RecordingUnitManager();
            gridController.UnitManager = unitManager;

            var unitObject = new GameObject("DrainCancelledBrainUnit");
            var unit = unitObject.AddComponent<Unit>();
            var brain = ScriptableObject.CreateInstance<AiBrainAsset>();
            unit.ApplyAiBrain(brain);

            var aiPlayer = new AIPlayer(debugMode: false, turnStartDelay: 0, unitDelay: 0)
            {
                UnitSelector = new SingleUnitSelector(unit)
            };
            typeof(GridController).GetProperty(nameof(GridController.TurnContext))?
                .SetValue(
                    gridController,
                    new Tactics.Common.Controllers.TurnResolvers.TurnContext(
                        aiPlayer,
                        new IUnit[] { unit }));
            aiPlayer.Initialize(gridController);

            var brainEntered = new TaskCompletionSource<bool>();
            typeof(AIPlayer).GetField("_brainExecutor", BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(
                    aiPlayer,
                    new System.Func<IUnit, GridController, AiBrainAsset, CancellationToken, Task>(
                        async (u, g, brain, ct) =>
                        {
                            brainEntered.TrySetResult(true);
                            await Task.Delay(Timeout.Infinite, ct);
                        }));

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                aiPlayer.Play(gridController);
                yield return new WaitUntil(() => brainEntered.Task.IsCompleted);
                typeof(AIPlayer).GetMethod(
                        "CancelOngoingAction",
                        BindingFlags.Instance | BindingFlags.NonPublic)?
                    .Invoke(aiPlayer, null);

                Task idleTask = aiPlayer.WhenIdleAsync();
                float deadline = Time.realtimeSinceStartup + 1f;
                while (!idleTask.IsCompleted && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(idleTask.IsCompletedSuccessfully, Is.True,
                    idleTask.Exception?.ToString() ?? "Cancelled AI Play continuation did not drain.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(brain);
            }
        }

        [Test]
        public async Task AIPlayer_WaitForKeypress_ThrowsWhenCancellationAlreadyRequested()
        {
            var aiPlayer = new AIPlayer(debugMode: true, turnStartDelay: 0, unitDelay: 0);
            var method = typeof(AIPlayer).GetMethod(
                "WaitForKeypress",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "WaitForKeypress should exist.");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task task;
            try
            {
                var keyType = method.GetParameters()[0].ParameterType;
                var keyN = System.Enum.Parse(keyType, "N");
                task = (Task)method.Invoke(aiPlayer, new object[] { keyN, cts.Token });
            }
            catch (TargetParameterCountException)
            {
                Assert.Fail("WaitForKeypress must accept a CancellationToken.");
                return;
            }

            Assert.CatchAsync<System.OperationCanceledException>(async () => await task);
        }

#pragma warning disable CS0067
        private sealed class RecordingUnitManager : IUnitManager
        {
            public int MarkAsSelectedCount { get; private set; }
            public int MarkAsFriendlyCount { get; private set; }
            public int MarkAsFinishedCount { get; private set; }

            public event System.Action<IUnit> UnitAdded;
            public event System.Action<IUnit> UnitRemoved;
            public Transform ContainerTransform => null;

            public void Initialize(IGridController gridController) { }
            public IEnumerable<IUnit> GetUnits() => System.Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetFriendlyUnits(IPlayer player) => System.Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetFriendlyUnits(int playerNumber) => System.Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetEnemyUnits(IPlayer player) => System.Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetEnemyUnits(int playerNumber) => System.Array.Empty<IUnit>();
            public void AddUnit(IUnit unit) { }
            public void RemoveUnit(IUnit unit) { }
            public Task UnMark(IEnumerable<IUnit> units) => Task.CompletedTask;

            public Task MarkAsSelected(IUnit unit)
            {
                MarkAsSelectedCount++;
                return Task.CompletedTask;
            }

            public Task MarkAsFriendly(IEnumerable<IUnit> units)
            {
                MarkAsFriendlyCount++;
                return Task.CompletedTask;
            }

            public Task MarkAsFinished(IEnumerable<IUnit> units)
            {
                MarkAsFinishedCount++;
                return Task.CompletedTask;
            }

            public Task MarkAsTargetable(IEnumerable<IUnit> units) => Task.CompletedTask;
            public Task MarkAsAttacking(IUnit unit, IUnit target) => Task.CompletedTask;
            public Task MarkAsDefending(IUnit unit, IUnit aggressor) => Task.CompletedTask;
            public Task MarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path)
                => Task.CompletedTask;
            public Task UnMarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path)
                => Task.CompletedTask;
            public Task MarkAsDestroyed(IUnit unit) => Task.CompletedTask;
        }
#pragma warning restore CS0067

        private sealed class SingleUnitSelector : IUnitSelector
        {
            private readonly IUnit _unit;

            public SingleUnitSelector(IUnit unit)
            {
                _unit = unit;
            }

            public IEnumerable<IUnit> SelectNext(
                System.Func<IEnumerable<IUnit>> getUnits,
                GridController gridController)
            {
                return new[] { _unit };
            }
        }

        private sealed class CountingUnitSelector : IUnitSelector
        {
            public int InvocationCount { get; private set; }

            public IEnumerable<IUnit> SelectNext(
                System.Func<IEnumerable<IUnit>> getUnits,
                GridController gridController)
            {
                InvocationCount++;
                return System.Array.Empty<IUnit>();
            }
        }
    }
}
