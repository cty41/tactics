using System;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.MCP
{
    /// <summary>
    /// Keeps project initialization free of Unity MCP lifecycle ownership.
    /// </summary>
    [InitializeOnLoad]
    public static class UnityMcpProjectBootstrap
    {
        static UnityMcpProjectBootstrap()
        {
            if (ShouldSkipInitialization(
                    Application.isBatchMode,
                    AssetDatabase.IsAssetImportWorkerProcess))
            {
                return;
            }

            // Intentionally no-op. The MCP package and explicit user actions own the connection
            // lifecycle; project code must not start, stop, connect, verify, or poll it.
        }

        private static bool ShouldSkipInitialization(
            bool isBatchMode,
            Func<bool> isAssetImportWorkerProcess)
        {
            return isBatchMode || isAssetImportWorkerProcess();
        }
    }
}
