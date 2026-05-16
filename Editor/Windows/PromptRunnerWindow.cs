using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RockRabbit.ClaudeUnityBootstrap.Editor.Services;
using Unity.AI.Assistant.Editor.Api;
using UnityEditor;
using UnityEngine;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Windows
{
    /// <summary>
    /// Drives the markdown prompts under <see cref="BootstrapSettings.PromptsFolder"/> through
    /// Unity AI Assistant in Agent mode (via <see cref="AssistantApi.Run"/>), one click per prompt.
    /// </summary>
    public class PromptRunnerWindow : EditorWindow
    {
        string[] promptPaths = Array.Empty<string>();
        int selectedIndex;
        Vector2 listScroll;
        Vector2 previewScroll;
        string preview = string.Empty;
        bool isRunning;
        string runningName;
        CancellationTokenSource cancellation;

        string PromptsFolder => BootstrapSettings.PromptsFolder;

        [MenuItem("Window/Claude/Prompt Runner")]
        public static void Open()
        {
            var window = GetWindow<PromptRunnerWindow>();
            window.titleContent = new GUIContent("Prompt Runner");
            window.minSize = new Vector2(420, 360);
            window.Refresh();
            window.Show();
        }

        void OnEnable()
        {
            Refresh();
        }

        void OnDisable()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }

        void Refresh()
        {
            var folder = PromptsFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                promptPaths = Array.Empty<string>();
                selectedIndex = 0;
                preview = string.Empty;
                return;
            }

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { folder });
            promptPaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                .ToArray();

            if (promptPaths.Length == 0)
            {
                selectedIndex = 0;
                preview = string.Empty;
            }
            else
            {
                selectedIndex = Mathf.Clamp(selectedIndex, 0, promptPaths.Length - 1);
                LoadPreview();
            }
        }

        void LoadPreview()
        {
            if (selectedIndex < 0 || selectedIndex >= promptPaths.Length)
            {
                preview = string.Empty;
                return;
            }
            try
            {
                preview = File.ReadAllText(Path.GetFullPath(promptPaths[selectedIndex]));
            }
            catch (Exception ex)
            {
                preview = $"<failed to read: {ex.Message}>";
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Claude Prompt Runner", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Folder: {PromptsFolder}", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(isRunning))
            {
                if (GUILayout.Button("Refresh", GUILayout.Height(20)))
                {
                    Refresh();
                }
            }

            EditorGUILayout.Space();

            if (promptPaths.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No .md prompts in {PromptsFolder}. " +
                    "Generative-art prompts (rigged models, textures, VFX, audio, music, UI illustrations) will appear here.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Queue ({promptPaths.Length}):", EditorStyles.miniBoldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(110));
            for (int i = 0; i < promptPaths.Length; i++)
            {
                var name = Path.GetFileName(promptPaths[i]);
                var prefix = i == selectedIndex ? "▶ " : "   ";
                var label = prefix + name;
                var style = i == selectedIndex ? EditorStyles.boldLabel : EditorStyles.label;
                using (new EditorGUI.DisabledScope(isRunning))
                {
                    if (GUILayout.Button(label, style, GUILayout.Height(18)))
                    {
                        selectedIndex = i;
                        LoadPreview();
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(isRunning))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var canRun = !isRunning && selectedIndex >= 0 && selectedIndex < promptPaths.Length;
                    using (new EditorGUI.DisabledScope(!canRun))
                    {
                        if (GUILayout.Button("▶ Run Next", GUILayout.Height(28)))
                        {
                            _ = RunSelectedAsync();
                        }
                    }
                    if (GUILayout.Button("✓ Mark Done & Delete", GUILayout.Height(28)))
                    {
                        DeleteSelected();
                    }
                    if (GUILayout.Button("↓ Skip", GUILayout.Height(28)))
                    {
                        Skip();
                    }
                }
            }

            if (isRunning)
            {
                EditorGUILayout.HelpBox(
                    $"Sent to Assistant: {runningName}\nWatch the Assistant window. " +
                    "When the run is finished, click 'Mark Done & Delete' to advance.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(preview, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        async Task RunSelectedAsync()
        {
            if (selectedIndex < 0 || selectedIndex >= promptPaths.Length) return;
            if (string.IsNullOrEmpty(preview)) return;

            isRunning = true;
            runningName = Path.GetFileName(promptPaths[selectedIndex]);
            Repaint();

            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();

            try
            {
                await AssistantApi.Run(preview, cancellationToken: cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Window closed or run cancelled — nothing to surface.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PromptRunner] AssistantApi.Run failed: {ex}");
            }
            finally
            {
                isRunning = false;
                runningName = null;
                Repaint();
            }
        }

        void DeleteSelected()
        {
            if (selectedIndex < 0 || selectedIndex >= promptPaths.Length) return;
            var path = promptPaths[selectedIndex];
            if (!AssetDatabase.DeleteAsset(path))
            {
                Debug.LogWarning($"[PromptRunner] AssetDatabase.DeleteAsset failed for {path}");
                return;
            }
            Refresh();
        }

        void Skip()
        {
            if (promptPaths.Length == 0) return;
            selectedIndex = (selectedIndex + 1) % promptPaths.Length;
            LoadPreview();
        }
    }
}
