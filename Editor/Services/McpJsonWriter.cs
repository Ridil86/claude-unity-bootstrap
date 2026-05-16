using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// Maintains the project-root <c>.mcp.json</c> Claude Code expects. Adds or refreshes the
    /// <c>UnityMCP</c> server entry (HTTP transport pointing at the live bridge URL) while
    /// preserving any other entries the user has configured.
    /// </summary>
    public static class McpJsonWriter
    {
        public const string DefaultServerName = "UnityMCP";
        public const string DefaultUrl = "http://127.0.0.1:8080/mcp";

        public enum WriteResult
        {
            Unchanged,
            CreatedFile,
            UpdatedEntry,
            BackedUpAndReplaced,
            Failed
        }

        /// <summary>
        /// Ensures the .mcp.json at <paramref name="absoluteMcpJsonPath"/> contains a
        /// <c>UnityMCP</c> HTTP server entry pointing at <paramref name="mcpUrl"/>.
        /// </summary>
        public static WriteResult EnsureUnityMcpServer(
            string absoluteMcpJsonPath,
            string mcpUrl,
            out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(absoluteMcpJsonPath))
            {
                error = "mcp.json path is empty.";
                return WriteResult.Failed;
            }
            if (string.IsNullOrWhiteSpace(mcpUrl))
            {
                mcpUrl = DefaultUrl;
            }

            try
            {
                if (!File.Exists(absoluteMcpJsonPath))
                {
                    var fresh = BuildFreshConfig(mcpUrl);
                    WriteJson(absoluteMcpJsonPath, fresh);
                    return WriteResult.CreatedFile;
                }

                var raw = File.ReadAllText(absoluteMcpJsonPath);
                JObject root;
                try
                {
                    root = string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
                }
                catch (JsonReaderException)
                {
                    // Malformed — back up and replace.
                    var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Copy(absoluteMcpJsonPath, absoluteMcpJsonPath + ".bak." + stamp, overwrite: false);
                    var fresh = BuildFreshConfig(mcpUrl);
                    WriteJson(absoluteMcpJsonPath, fresh);
                    return WriteResult.BackedUpAndReplaced;
                }

                if (!(root["mcpServers"] is JObject servers))
                {
                    servers = new JObject();
                    root["mcpServers"] = servers;
                }

                var unityEntry = servers[DefaultServerName] as JObject;
                bool changed = false;
                if (unityEntry == null)
                {
                    unityEntry = new JObject
                    {
                        ["type"] = "http",
                        ["url"] = mcpUrl
                    };
                    servers[DefaultServerName] = unityEntry;
                    changed = true;
                }
                else
                {
                    if ((string)unityEntry["type"] != "http")
                    {
                        unityEntry["type"] = "http";
                        changed = true;
                    }
                    if ((string)unityEntry["url"] != mcpUrl)
                    {
                        unityEntry["url"] = mcpUrl;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    return WriteResult.Unchanged;
                }

                WriteJson(absoluteMcpJsonPath, root);
                return WriteResult.UpdatedEntry;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return WriteResult.Failed;
            }
        }

        /// <summary>
        /// True when the file exists and has an HTTP <c>UnityMCP</c> entry whose url matches
        /// <paramref name="expectedUrl"/> exactly.
        /// </summary>
        public static bool IsUnityMcpEntryCurrent(string absoluteMcpJsonPath, string expectedUrl)
        {
            if (!File.Exists(absoluteMcpJsonPath)) return false;
            try
            {
                var root = JObject.Parse(File.ReadAllText(absoluteMcpJsonPath));
                var entry = root["mcpServers"]?[DefaultServerName] as JObject;
                if (entry == null) return false;
                return (string)entry["type"] == "http"
                       && string.Equals((string)entry["url"], expectedUrl, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        static JObject BuildFreshConfig(string mcpUrl)
        {
            return new JObject
            {
                ["mcpServers"] = new JObject
                {
                    [DefaultServerName] = new JObject
                    {
                        ["type"] = "http",
                        ["url"] = mcpUrl
                    }
                }
            };
        }

        static void WriteJson(string absolutePath, JObject root)
        {
            var text = root.ToString(Formatting.Indented) + "\n";
            File.WriteAllText(absolutePath, text, new UTF8Encoding(false));
        }
    }
}
