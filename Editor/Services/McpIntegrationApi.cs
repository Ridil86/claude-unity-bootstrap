using System;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// Static delegate registry that the optional <c>RockRabbit.ClaudeUnityBootstrap.McpIntegration.Editor</c>
    /// asmdef populates at editor load when the CoplayDev <c>unity-mcp</c> package is installed.
    /// This lets the main asmdef query bridge state without a hard compile-time reference to the
    /// optional dependency — keeping the package compileable on projects where CoplayDev isn't
    /// installed yet (e.g. before Step 2 of the bootstrap wizard).
    /// </summary>
    public static class McpIntegrationApi
    {
        /// <summary>True when the optional McpIntegration asmdef has registered its delegates.</summary>
        public static bool IsAvailable => GetMcpRpcUrl != null;

        /// <summary>Returns the live MCP HTTP endpoint URL the bridge serves on.</summary>
        public static Func<string> GetMcpRpcUrl;

        /// <summary>Returns true when the HTTP listener is up and accepting connections.</summary>
        public static Func<bool> IsBridgeRunning;

        /// <summary>Starts the HTTP listener if it isn't already running. Returns true when running after the call.</summary>
        public static Func<bool> EnsureBridgeRunning;

        /// <summary>
        /// True when the MCP session is active (separate from the HTTP listener — the session is
        /// what actually accepts MCP client connections). CoplayDev calls this state "Bridge.IsRunning"
        /// internally; we expose it as a distinct concept because the bootstrap wizard has to start
        /// it as its own step after the HTTP server is up.
        /// </summary>
        public static Func<bool> IsSessionRunning;

        /// <summary>
        /// Fire-and-forget start of the MCP session. Idempotent — no-op when already running.
        /// Poll <see cref="IsSessionRunning"/> for completion.
        /// </summary>
        public static Action StartSession;
    }
}
