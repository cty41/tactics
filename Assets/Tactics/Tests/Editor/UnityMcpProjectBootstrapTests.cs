using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tactics.Editor.MCP;

namespace Tactics.Tests.Editor
{
    public class UnityMcpProjectBootstrapTests
    {
        [TestCase(false, false, false, 1)]
        [TestCase(true, false, true, 0)]
        [TestCase(false, true, true, 1)]
        [TestCase(true, true, true, 0)]
        public void ShouldSkipInitialization_UsesBatchAndImportWorkerGuards(
            bool isBatchMode,
            bool isAssetImportWorkerProcess,
            bool expected,
            int expectedProbeCount)
        {
            int probeCount = 0;

            bool actual = InvokeShouldSkipInitialization(
                isBatchMode,
                () =>
                {
                    probeCount++;
                    return isAssetImportWorkerProcess;
                });

            Assert.AreEqual(expected, actual);
            Assert.AreEqual(expectedProbeCount, probeCount);
        }

        [Test]
        public void Bootstrap_DoesNotOwnMcpLifecycle()
        {
            const string path = "Assets/Tactics/Scripts/Editor/MCP/UnityMcpProjectBootstrap.cs";
            string source = File.ReadAllText(path);

            StringAssert.Contains("Application.isBatchMode", source);
            StringAssert.Contains("AssetDatabase.IsAssetImportWorkerProcess", source);

            string[] forbiddenTokens =
            {
                "MCPServiceLocator",
                "EditorApplication.update",
                "EditorApplication.delayCall",
                "EditorPrefs",
                "SessionState",
                "ForceStop",
                "StartLocalHttpServer",
                "StartAsync",
                "VerifyAsync",
                "Task.Delay",
                "Recovery",
                "Retry",
                "Pending",
            };

            foreach (string token in forbiddenTokens)
            {
                StringAssert.DoesNotContain(token, source);
            }
        }

        private static bool InvokeShouldSkipInitialization(
            bool isBatchMode,
            Func<bool> isAssetImportWorkerProcess)
        {
            MethodInfo method = typeof(UnityMcpProjectBootstrap).GetMethod(
                "ShouldSkipInitialization",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Expected the process guard helper to exist.");

            return (bool)method.Invoke(
                null,
                new object[] { isBatchMode, isAssetImportWorkerProcess });
        }
    }
}
