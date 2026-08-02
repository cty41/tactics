using System;
using System.IO;
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

        [Test]
        public async Task ReconcileConnectionAsync_LeavesVerifiedBridgeRunning()
        {
            int stopCount = 0;
            int startServerCount = 0;
            int startBridgeCount = 0;

            string error = await InvokeReconcileConnectionAsync(
                () => true,
                () => Task.FromResult(true),
                () => stopCount++,
                () => true,
                () =>
                {
                    startServerCount++;
                    return true;
                },
                () => Task.FromResult(true),
                () =>
                {
                    startBridgeCount++;
                    return Task.FromResult(true);
                });

            Assert.IsNull(error);
            Assert.AreEqual(0, stopCount);
            Assert.AreEqual(0, startServerCount);
            Assert.AreEqual(0, startBridgeCount);
        }

        [Test]
        public async Task ReconcileConnectionAsync_ConnectsToReachableServer_WithoutOwnershipFile()
        {
            int startServerCount = 0;
            int startBridgeCount = 0;

            string error = await InvokeReconcileConnectionAsync(
                () => false,
                () => Task.FromResult(true),
                () => { },
                () => true,
                () =>
                {
                    startServerCount++;
                    return true;
                },
                () => Task.FromResult(true),
                () =>
                {
                    startBridgeCount++;
                    return Task.FromResult(true);
                });

            Assert.IsNull(error);
            Assert.AreEqual(0, startServerCount);
            Assert.AreEqual(1, startBridgeCount);
        }

        [Test]
        public async Task ReconcileConnectionAsync_StartsServerOnce_WhenUnavailable()
        {
            int startServerCount = 0;
            int waitForServerCount = 0;

            string error = await InvokeReconcileConnectionAsync(
                () => false,
                () => Task.FromResult(true),
                () => { },
                () => false,
                () =>
                {
                    startServerCount++;
                    return true;
                },
                () =>
                {
                    waitForServerCount++;
                    return Task.FromResult(true);
                },
                () => Task.FromResult(true));

            Assert.IsNull(error);
            Assert.AreEqual(1, startServerCount);
            Assert.AreEqual(1, waitForServerCount);
        }

        [Test]
        public async Task ReconcileConnectionAsync_RetriesBridgeAndStopsOnlyFailedBridge()
        {
            bool bridgeRunning = false;
            int verifyCount = 0;
            int stopCount = 0;
            int startBridgeCount = 0;

            string error = await InvokeReconcileConnectionAsync(
                () => bridgeRunning,
                () => Task.FromResult(++verifyCount >= 2),
                () =>
                {
                    stopCount++;
                    bridgeRunning = false;
                },
                () => true,
                () => true,
                () => Task.FromResult(true),
                () =>
                {
                    startBridgeCount++;
                    bridgeRunning = true;
                    return Task.FromResult(true);
                });

            Assert.IsNull(error);
            Assert.AreEqual(2, startBridgeCount);
            Assert.AreEqual(2, verifyCount);
            Assert.AreEqual(1, stopCount);
        }

        [Test]
        public async Task ReconcileConnectionAsync_DoesNotStopUnknownReachableProcess()
        {
            int stopCount = 0;
            int startBridgeCount = 0;

            string error = await InvokeReconcileConnectionAsync(
                () => false,
                () => Task.FromResult(false),
                () => stopCount++,
                () => true,
                () => true,
                () => Task.FromResult(true),
                () =>
                {
                    startBridgeCount++;
                    return Task.FromResult(false);
                },
                bridgeConnectAttempts: 3);

            StringAssert.Contains("3 attempts", error);
            Assert.AreEqual(3, startBridgeCount);
            Assert.AreEqual(0, stopCount);
        }

        [Test]
        public void ReconcileGate_RejectsOverlappingAttempt()
        {
            MethodInfo method = typeof(UnityMcpProjectBootstrap).GetMethod(
                "TryBeginReconcile",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(bool).MakeByRefType() },
                null);
            Assert.IsNotNull(method);

            object[] state = { false };
            Assert.IsTrue((bool)method.Invoke(null, state));
            Assert.IsTrue((bool)state[0]);
            Assert.IsFalse((bool)method.Invoke(null, state));
        }

        [Test]
        public void Bootstrap_UsesSinglePostReloadSchedulePath()
        {
            const string path = "Assets/Tactics/Scripts/Editor/MCP/UnityMcpProjectBootstrap.cs";
            string source = File.ReadAllText(path);

            StringAssert.Contains("EditorApplication.delayCall += ScheduleReconcile", source);
            StringAssert.DoesNotContain(
                "AssemblyReloadEvents.afterAssemblyReload += ScheduleReconcile",
                source);
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

        private static async Task<string> InvokeReconcileConnectionAsync(
            Func<bool> isBridgeRunning,
            Func<Task<bool>> verifyBridgeAsync,
            Action stopBridge,
            Func<bool> isServerReachable,
            Func<bool> startServer,
            Func<Task<bool>> waitForServerAsync,
            Func<Task<bool>> startBridgeAsync,
            int bridgeConnectAttempts = 5)
        {
            MethodInfo method = typeof(UnityMcpProjectBootstrap).GetMethod(
                "ReconcileConnectionAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Expected the connection reconciliation helper to exist.");

            var task = (Task<string>)method.Invoke(
                null,
                new object[]
                {
                    isBridgeRunning,
                    verifyBridgeAsync,
                    stopBridge,
                    isServerReachable,
                    startServer,
                    waitForServerAsync,
                    startBridgeAsync,
                    new Func<Task>(() => Task.CompletedTask),
                    new Func<Task>(() => Task.CompletedTask),
                    bridgeConnectAttempts,
                });
            return await task;
        }

    }
}
