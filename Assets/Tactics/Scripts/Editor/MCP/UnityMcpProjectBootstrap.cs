using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
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
        private const int ServerReadyAttempts = 20;
        private const int ServerReadyDelayMilliseconds = 250;
        private const int BridgeRegistrationDelayMilliseconds = 1000;

        private static bool _reconcileInProgress;

        static UnityMcpProjectBootstrap()
        {
            DisableSharedAutoStart();
            AssemblyReloadEvents.afterAssemblyReload += ScheduleReconcile;
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
            if (_reconcileInProgress)
            {
                return;
            }

            _reconcileInProgress = true;
            try
            {
                if (!TryReadProjectEndpoint(out Uri endpoint, out string error))
                {
                    TLog.Error($"[UnityMCP] Project bootstrap skipped: {error}");
                    return;
                }

                ConfigureLocalHttp(endpoint);
                MCPServiceLocator.TransportManager.ForceStop(TransportMode.Http);

                bool serverReachable = await IsPortReachableAsync(endpoint.Port);
                if (serverReachable && !IsExpectedProjectServerRunning(endpoint.Port))
                {
                    TLog.Error(
                        $"[UnityMCP] Port {endpoint.Port} is occupied by an unknown process. " +
                        "The bootstrap will not stop it; close the owner or use this project's configured port.");
                    return;
                }

                if (!serverReachable)
                {
                    bool serverStarted = MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
                    if (!serverStarted)
                    {
                        TLog.Error($"[UnityMCP] Could not start the local server on {endpoint}.");
                        return;
                    }

                    if (!await WaitForServerAsync())
                    {
                        TLog.Error($"[UnityMCP] Local server did not become reachable on {endpoint}.");
                        return;
                    }
                }

                bool bridgeStarted = await MCPServiceLocator.Bridge.StartAsync();
                // StartAsync returns once the WebSocket is open. Give the server time to assign the
                // plugin session before VerifyAsync sends its session-bound pong.
                await Task.Delay(BridgeRegistrationDelayMilliseconds);
                bool verified = bridgeStarted && await MCPServiceLocator.TransportManager.VerifyAsync(TransportMode.Http);
                if (!verified)
                {
                    TLog.Error($"[UnityMCP] Bridge verification failed for {endpoint}.");
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
                _reconcileInProgress = false;
            }
        }

        private static bool TryReadProjectEndpoint(out Uri endpoint, out string error)
        {
            endpoint = null;
            error = null;
            string configPath = Path.Combine(GetProjectRoot(), ".agents", "mcp.json");

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
            for (int attempt = 0; attempt < ServerReadyAttempts; attempt++)
            {
                if (MCPServiceLocator.Server.IsLocalHttpServerReachable())
                {
                    return true;
                }

                await Task.Delay(ServerReadyDelayMilliseconds);
            }

            return false;
        }

        private static async Task<bool> IsPortReachableAsync(int port)
        {
            using var client = new TcpClient();
            Task connectTask = client.ConnectAsync("127.0.0.1", port);
            Task completedTask = await Task.WhenAny(connectTask, Task.Delay(500));
            if (completedTask != connectTask)
            {
                return false;
            }

            try
            {
                await connectTask;
                return client.Connected;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static bool IsExpectedProjectServerRunning(int port)
        {
            string pidFile = Path.Combine(
                GetProjectRoot(), "Library", "MCPForUnity", "RunState", $"mcp_http_{port}.pid");
            if (!File.Exists(pidFile) || !int.TryParse(File.ReadAllText(pidFile).Trim(), out int processId))
            {
                return false;
            }

            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }
    }
}
