using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// Thin wrapper around <see cref="UnityEditor.PackageManager.Client"/> for installing and
    /// detecting the CoplayDev MCP for Unity package. Returns Unity's <c>Request</c> objects
    /// directly so callers (e.g. <c>BootstrapWindow</c>) can poll them from
    /// <see cref="UnityEditor.EditorApplication.update"/> without blocking the Editor.
    /// </summary>
    public static class CoplayDevInstaller
    {
        public const string PackageName = "com.coplaydev.unity-mcp";
        public const string GitUrl = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main";

        /// <summary>Start a query for installed packages (offline mode — fast, no registry hit).</summary>
        public static ListRequest BeginQueryInstalled()
        {
            return Client.List(offlineMode: true, includeIndirectDependencies: false);
        }

        /// <summary>Start adding the CoplayDev unity-mcp package via its git URL.</summary>
        public static AddRequest BeginInstall()
        {
            return Client.Add(GitUrl);
        }

        /// <summary>
        /// Inspect a completed <see cref="ListRequest"/> for the CoplayDev package. Returns
        /// true (with the resolved version) if installed, false otherwise.
        /// </summary>
        public static bool TryGetInstalledVersion(ListRequest request, out string version)
        {
            version = null;
            if (request == null || request.Status != StatusCode.Success || request.Result == null)
                return false;
            foreach (var pkg in request.Result)
            {
                if (pkg.name == PackageName)
                {
                    version = pkg.version;
                    return true;
                }
            }
            return false;
        }
    }
}
