using System;
using System.IO;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using Newtonsoft.Json.Linq;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.MCP
{
    /// <summary>
    /// Makes the Unity MCP HTTP endpoint follow this worktree's project-level configuration.
    /// </summary>
    [InitializeOnLoad]
    public static class UnityMcpProjectBootstrap
    {
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private const string ResumeHttpAfterReloadKey = "MCPForUnity.ResumeHttpAfterReload";
        private const int ServerReadyAttempts = 240;
        private const int ServerReadyDelayMilliseconds = 250;
        private const int ServerReadyTimeoutSeconds =
            ServerReadyAttempts * ServerReadyDelayMilliseconds / 1000;
        private const int BridgeRegistrationDelayMilliseconds = 1000;
        private const int BridgeConnectAttempts = 5;
        private const int BridgeConnectRetryDelayMilliseconds = 500;

        private static bool _reconcileInProgress;

        static UnityMcpProjectBootstrap()
        {
            DisableSharedAutoStart();
            // InitializeOnLoad runs once in every new editor domain. A single delayCall is enough
            // to reconnect after reload; also subscribing to afterAssemblyReload can schedule a
            // second Bridge.StartAsync and leave competing WebSocket reconnect loops alive.
            EditorApplication.delayCall += ScheduleReconcile;
        }

        private static void ScheduleReconcile()
        {
            DisableSharedAutoStart();
            if (_reconcileInProgress)
            {
                return;
            }

            _ = ReconcileAsync();
        }

        private static void DisableSharedAutoStart()
        {
            // The package preference is shared by all Editors. This worktree always owns startup.
            EditorPrefs.SetBool(AutoStartOnLoadKey, false);
            EditorPrefs.SetBool(ResumeHttpAfterReloadKey, false);
        }

        private static async Task ReconcileAsync()
        {
            if (!TryBeginReconcile())
            {
                return;
            }
            try
            {
                if (!TryReadProjectEndpoint(out Uri endpoint, out string error))
                {
                    TLog.Error($"[UnityMCP] Project bootstrap skipped: {error}");
                    return;
                }

                ConfigureLocalHttp(endpoint);
                string connectionError = await ReconcileConnectionAsync(
                    () => MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http),
                    () => MCPServiceLocator.TransportManager.VerifyAsync(TransportMode.Http),
                    () => MCPServiceLocator.TransportManager.ForceStop(TransportMode.Http),
                    () => MCPServiceLocator.Server.IsLocalHttpServerReachable(),
                    () => MCPServiceLocator.Server.StartLocalHttpServer(quiet: true),
                    WaitForServerAsync,
                    () => MCPServiceLocator.Bridge.StartAsync(),
                    () => Task.Delay(BridgeRegistrationDelayMilliseconds),
                    () => Task.Delay(BridgeConnectRetryDelayMilliseconds),
                    BridgeConnectAttempts);
                if (!string.IsNullOrEmpty(connectionError))
                {
                    string launchLogHint = connectionError.StartsWith(
                        "Local HTTP server did not become reachable",
                        StringComparison.Ordinal)
                        ? $" Inspect {GetServerLaunchLogPath(endpoint.Port)}."
                        : string.Empty;
                    TLog.Error($"[UnityMCP] {connectionError} Endpoint: {endpoint}.{launchLogHint}");
                    return;
                }

                TLog.Info($"[UnityMCP] Project bridge is connected at {endpoint}.");
            }
            catch (Exception exception)
            {
                TLog.Error($"[UnityMCP] Project bootstrap failed: {exception.Message}");
            }
            finally
            {
                EndReconcile();
            }
        }

        private static bool TryBeginReconcile()
        {
            return TryBeginReconcile(ref _reconcileInProgress);
        }

        private static bool TryBeginReconcile(ref bool reconcileInProgress)
        {
            if (reconcileInProgress)
            {
                return false;
            }

            reconcileInProgress = true;
            return true;
        }

        private static void EndReconcile()
        {
            _reconcileInProgress = false;
        }

        private static async Task<string> ReconcileConnectionAsync(
            Func<bool> isBridgeRunning,
            Func<Task<bool>> verifyBridgeAsync,
            Action stopBridge,
            Func<bool> isServerReachable,
            Func<bool> startServer,
            Func<Task<bool>> waitForServerAsync,
            Func<Task<bool>> startBridgeAsync,
            Func<Task> waitAfterBridgeStartAsync,
            Func<Task> retryDelayAsync,
            int bridgeConnectAttempts)
        {
            if (isBridgeRunning())
            {
                if (await verifyBridgeAsync())
                {
                    return null;
                }

                stopBridge();
            }

            if (!isServerReachable())
            {
                if (!startServer())
                {
                    return "Could not start the local HTTP server.";
                }

                if (!await waitForServerAsync())
                {
                    return $"Local HTTP server did not become reachable after " +
                           $"{ServerReadyTimeoutSeconds} seconds ({ServerReadyAttempts} attempts).";
                }
            }

            int attempts = Math.Max(1, bridgeConnectAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                bool bridgeStarted = await startBridgeAsync();
                if (bridgeStarted)
                {
                    // StartAsync completes when the WebSocket opens. Registration on the server is
                    // asynchronous, so verification must wait for the session to become visible.
                    await waitAfterBridgeStartAsync();
                    if (await verifyBridgeAsync())
                    {
                        return null;
                    }
                }

                if (isBridgeRunning())
                {
                    stopBridge();
                }

                if (attempt + 1 < attempts)
                {
                    await retryDelayAsync();
                }
            }

            return $"Bridge did not reconnect after {attempts} attempts.";
        }

        private static bool TryReadProjectEndpoint(out Uri endpoint, out string error)
        {
            endpoint = null;
            error = null;
            string agentsPath = Path.Combine(GetProjectRoot(), ".agents");
            string configPath = Path.Combine(agentsPath, "mcp.json");
            if (!File.Exists(configPath))
            {
                string migrationBackupPath = Path.Combine(agentsPath, "mcp.local.json");
                if (!File.Exists(migrationBackupPath))
                {
                    error = $"Could not find {configPath} or {migrationBackupPath}. Initialize this worktree with " +
                            "Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 " +
                            "-Url http://127.0.0.1:<PORT>/mcp.";
                    return false;
                }

                configPath = migrationBackupPath;
                TLog.Warning(
                    $"[UnityMCP] Using migration backup {migrationBackupPath}. " +
                    "Run Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -RestoreMigration to restore mcp.json.");
            }

            try
            {
                var config = JObject.Parse(File.ReadAllText(configPath));
                string url = config["mcpServers"]?["unityMCP"]?["url"]?.Value<string>();
                if (!Uri.TryCreate(url, UriKind.Absolute, out endpoint))
                {
                    error = $"Invalid unityMCP URL in {configPath}.";
                    return false;
                }

                bool isExpectedEndpoint = endpoint.Scheme == Uri.UriSchemeHttp &&
                                          endpoint.Host == "127.0.0.1" &&
                                          endpoint.Port > 0 &&
                                          endpoint.AbsolutePath.TrimEnd('/') == "/mcp" &&
                                          string.IsNullOrEmpty(endpoint.Query) &&
                                          string.IsNullOrEmpty(endpoint.Fragment);
                if (!isExpectedEndpoint)
                {
                    error = $"unityMCP URL must be a loopback HTTP /mcp endpoint: {url}";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not read {configPath}: {exception.Message}";
                return false;
            }
        }

        private static void ConfigureLocalHttp(Uri endpoint)
        {
            string baseUrl = endpoint.GetLeftPart(UriPartial.Authority);
            var configuration = EditorConfigurationCache.Instance;
            configuration.SetUseHttpTransport(true);
            configuration.SetHttpTransportScope("local");
            HttpEndpointUtility.SaveLocalBaseUrl(baseUrl);
            configuration.SetHttpBaseUrl(HttpEndpointUtility.GetLocalBaseUrl());
        }

        private static async Task<bool> WaitForServerAsync()
        {
            return await WaitForServerAsync(
                () => MCPServiceLocator.Server.IsLocalHttpServerReachable(),
                () => Task.Delay(ServerReadyDelayMilliseconds));
        }

        private static async Task<bool> WaitForServerAsync(
            Func<bool> isServerReachable,
            Func<Task> waitAsync)
        {
            if (isServerReachable == null)
            {
                throw new ArgumentNullException(nameof(isServerReachable));
            }

            if (waitAsync == null)
            {
                throw new ArgumentNullException(nameof(waitAsync));
            }

            for (int attempt = 0; attempt < ServerReadyAttempts; attempt++)
            {
                if (isServerReachable())
                {
                    return true;
                }

                await waitAsync();
            }

            return false;
        }

        private static string GetServerLaunchLogPath(int port)
        {
            return Path.Combine(
                GetProjectRoot(), "Library", "MCPForUnity", "Logs", $"server-launch-{port}.log");
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }
    }
}
