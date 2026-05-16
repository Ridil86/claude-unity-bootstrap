using System;
using UnityEditor;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// Ensures the configured prompts folder (e.g. <c>Assets/_Prompts</c>) exists. Creates intermediate
    /// folders as needed using <see cref="AssetDatabase"/> so .meta files are minted correctly.
    /// </summary>
    public static class PromptsFolderProvisioner
    {
        /// <summary>
        /// Returns true if the folder exists or was successfully created.
        /// </summary>
        public static bool EnsureExists(string assetPath, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "Prompts folder path is empty.";
                return false;
            }

            // Normalize to forward slashes (Unity convention).
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');

            if (!assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) &&
                !assetPath.StartsWith("Packages", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Path must be inside Assets/ or Packages/. Got '{assetPath}'.";
                return false;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return true;
            }

            // Walk parents and create missing ones in order.
            var segments = assetPath.Split('/');
            var current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, segments[i]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        error = $"AssetDatabase.CreateFolder failed at '{current}' / '{segments[i]}'.";
                        return false;
                    }
                }
                current = next;
            }

            AssetDatabase.Refresh();
            return AssetDatabase.IsValidFolder(assetPath);
        }
    }
}
