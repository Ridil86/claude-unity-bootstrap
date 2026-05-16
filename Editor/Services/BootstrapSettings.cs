using UnityEditor;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// EditorPrefs-backed settings for the Claude Unity Bootstrap package.
    /// Per-project values: the location of the prompts folder (default <see cref="DefaultPromptsFolder"/>).
    /// </summary>
    public static class BootstrapSettings
    {
        const string PromptsFolderKey = "RockRabbit.ClaudeBootstrap.PromptsFolder";

        /// <summary>Default location for the prompts queue. Project-relative.</summary>
        public const string DefaultPromptsFolder = "Assets/_Prompts";

        /// <summary>
        /// Where the Prompt Runner scans for .md prompts. Persisted in EditorPrefs.
        /// Falls back to <see cref="DefaultPromptsFolder"/> if unset or blank.
        /// </summary>
        public static string PromptsFolder
        {
            get
            {
                var stored = EditorPrefs.GetString(PromptsFolderKey, DefaultPromptsFolder);
                return string.IsNullOrWhiteSpace(stored) ? DefaultPromptsFolder : stored;
            }
            set
            {
                EditorPrefs.SetString(PromptsFolderKey,
                    string.IsNullOrWhiteSpace(value) ? DefaultPromptsFolder : value);
            }
        }
    }
}
