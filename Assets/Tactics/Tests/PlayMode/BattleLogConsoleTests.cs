using System;
using NUnit.Framework;
using Tactics.Cheats;
using Tactics.Runtime.BattleLog;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class BattleLogConsoleTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            TBattleLog.SetOutputToUI(false);
            TBattleLog.EndBattle();
        }

        [TearDown]
        public void TearDown()
        {
            TBattleLog.EndBattle();
            TBattleLog.SetOutputToUI(true);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void BattleScope_CachesLatestFiftyEntriesAndClearsAtEnd()
        {
            TBattleLog.BeginBattle();

            for (int i = 0; i < 55; i++)
                TBattleLog.Log(new TestLogData($"entry-{i}"));

            var entries = TBattleLog.GetCurrentBattleLogs();
            Assert.That(TBattleLog.IsBattleActive, Is.True);
            Assert.That(entries, Has.Count.EqualTo(50));
            Assert.That(entries[0].GetDisplayString(), Is.EqualTo("entry-5"));
            Assert.That(entries[49].GetDisplayString(), Is.EqualTo("entry-54"));

            TBattleLog.EndBattle();
            Assert.That(TBattleLog.IsBattleActive, Is.False);
            Assert.That(TBattleLog.GetCurrentBattleLogs(), Is.Empty);
        }

        [Test]
        public void ClearCurrentBattleLogs_RaisesClearEventWithoutDisablingBattleScope()
        {
            int clearCount = 0;
            Action onCleared = () => clearCount++;
            TBattleLog.OnLogsCleared += onCleared;

            try
            {
                TBattleLog.BeginBattle();
                TBattleLog.Log(new TestLogData("entry"));
                TBattleLog.ClearCurrentBattleLogs();

                Assert.That(clearCount, Is.EqualTo(2), "BeginBattle and explicit clear should both notify subscribers.");
                Assert.That(TBattleLog.IsBattleActive, Is.True);
                Assert.That(TBattleLog.GetCurrentBattleLogs(), Is.Empty);
            }
            finally
            {
                TBattleLog.OnLogsCleared -= onCleared;
            }
        }

        [Test]
        public void ClearLogCommand_ClearsUiReplayBuffer()
        {
            TBattleLog.BeginBattle();
            TBattleLog.Log(new TestLogData("entry"));

            string result = CheatCommandManager.Instance.Execute("clearlog");

            Assert.That(result, Is.EqualTo("Battle log cleared."));
            Assert.That(TBattleLog.GetCurrentBattleLogs(), Is.Empty);
            Assert.That(TBattleLog.IsBattleActive, Is.True);
        }

        private sealed class TestLogData : BattleLogData
        {
            private readonly string _message;

            public TestLogData(string message)
            {
                _message = message;
            }

            public override BattleActionType ActionType => BattleActionType.Damage;

            public override string GetDisplayString()
            {
                return _message;
            }
        }
    }
}
