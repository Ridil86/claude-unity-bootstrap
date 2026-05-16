using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using RockRabbit.ClaudeUnityBootstrap.Editor.Services;
using UnityEditor;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.McpIntegration
{
    /// <summary>
    /// Thin façade over CoplayDev's MCP for Unity public services. Only compiled when the
    /// <c>com.coplaydev.unity-mcp</c> package is installed (gated by the
    /// <c>MCP_FOR_UNITY_PRESENT</c> define constraint on this asmdef). Registers itself with
    /// <see cref="McpIntegrationApi"/> on load so the main asmdef can reach these methods
    /// without a direct compile-time reference.
    /// </summary>
    public static class UnityMcpBridge
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            McpIntegrationApi.GetMcpRpcUrl = GetMcpRpcUrl;
            McpIntegrationApi.IsBridgeRunning = IsBridgeRunning;
            McpIntegrationApi.EnsureBridgeRunning = EnsureBridgeRunning;
        }

        /// <summary>The live JSON-RPC endpoint URL the bridge serves on (base + "/mcp").</summary>
        public static string GetMcpRpcUrl() => HttpEndpointUtility.GetMcpRpcUrl();

        /// <summary>The base URL the bridge binds to locally (default http://127.0.0.1:8080).</summary>
        public static string GetLocalBaseUrl() => HttpEndpointUtility.GetLocalBaseUrl();

        /// <summary>True when the bridge HTTP server has been started and accepts connections.</summary>
        public static bool IsBridgeRunning()
        {
            var server = MCPServiceLocator.Server;
            return server != null && server.IsLocalHttpServerReachable();
        }

        /// <summary>
        /// Idempotent start of the local HTTP bridge. Returns true if running after the call.
        /// </summary>
        public static bool EnsureBridgeRunning()
        {
            var server = MCPServiceLocator.Server;
            if (server == null) return false;
            if (server.IsLocalHttpServerReachable()) return true;
            server.StartLocalHttpServer(quiet: true);
            return server.IsLocalHttpServerReachable();
        }
    }
}
