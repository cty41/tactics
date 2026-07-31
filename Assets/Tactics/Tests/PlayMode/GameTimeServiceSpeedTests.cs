using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using System.Reflection;
using Tactics.Common.AI;
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
        public void CyclePlaybackSpeed_UsesOneTwoFourOneOrder()
        {
            Assert.That(GameTimeService.CyclePlaybackSpeed(), Is.EqualTo(GamePlaybackSpeed.Double));
            Assert.That(GameTimeService.CyclePlaybackSpeed(), Is.EqualTo(GamePlaybackSpeed.Quadruple));
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

            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Double));
            Assert.That(GameTimeService.PlaybackScale, Is.EqualTo(2f));
            Assert.That(GameTimeService.EffectiveTimeScale, Is.EqualTo(0f));
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            GameTimeService.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(2f));
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
