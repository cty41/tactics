using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Editor.MCP;

namespace Tactics.Tests.Editor
{
    public class UnityMcpProjectBootstrapTests
    {
        private const int ExpectedServerReadyAttempts = 240;

        [Test]
        public async Task WaitForServerAsync_ReturnsImmediately_WhenServerIsAlreadyReachable()
        {
            int probeCount = 0;
            int waitCount = 0;

            bool reachable = await InvokeWaitForServerAsync(
                () =>
                {
                    probeCount++;
                    return true;
                },
                () =>
                {
                    waitCount++;
                    return Task.CompletedTask;
                });

            Assert.IsTrue(reachable);
            Assert.AreEqual(1, probeCount);
            Assert.AreEqual(0, waitCount);
        }

        [Test]
        public async Task WaitForServerAsync_ReturnsTrue_WhenServerBecomesReachable_OnFinalAttempt()
        {
            int probeCount = 0;
            int waitCount = 0;

            bool reachable = await InvokeWaitForServerAsync(
                () => ++probeCount == ExpectedServerReadyAttempts,
                () =>
                {
                    waitCount++;
                    return Task.CompletedTask;
                });

            Assert.IsTrue(reachable);
            Assert.AreEqual(ExpectedServerReadyAttempts, probeCount);
            Assert.AreEqual(ExpectedServerReadyAttempts - 1, waitCount);
        }

        [Test]
        public async Task WaitForServerAsync_ReturnsFalse_AfterConfiguredAttemptLimit()
        {
            int probeCount = 0;
            int waitCount = 0;

            bool reachable = await InvokeWaitForServerAsync(
                () =>
                {
                    probeCount++;
                    return false;
                },
                () =>
                {
                    waitCount++;
                    return Task.CompletedTask;
                });

            Assert.IsFalse(reachable);
            Assert.AreEqual(ExpectedServerReadyAttempts, probeCount);
            Assert.AreEqual(ExpectedServerReadyAttempts, waitCount);
        }

        private static async Task<bool> InvokeWaitForServerAsync(
            Func<bool> isServerReachable,
            Func<Task> waitAsync)
        {
            MethodInfo method = typeof(UnityMcpProjectBootstrap).GetMethod(
                "WaitForServerAsync",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(Func<bool>), typeof(Func<Task>) },
                null);

            Assert.IsNotNull(method, "Expected the parameterized server wait helper to exist.");

            var task = (Task<bool>)method.Invoke(null, new object[] { isServerReachable, waitAsync });
            return await task;
        }
    }
}
