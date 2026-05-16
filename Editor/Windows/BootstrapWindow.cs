using System;
using System.IO;
using RockRabbit.ClaudeUnityBootstrap.Editor.Services;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Windows
{
    /// <summary>
    /// The bootstrap installer — a vertical checklist of the work needed to wire a Unity project
    /// to Claude Code via Unity AI Assistant + CoplayDev unity-mcp. Re-runnable; reads current
    /// state on each Refresh and only acts when a step is incomplete.
    /// </summary>
    public class BootstrapWindow : EditorWindow
    {
        enum StepStatus { Unknown, Pending, InProgress, Done, Warning, Failed }

        // --- Cached step state ------------------------------------------------------------------

        PrereqCheck.Result prereqResult;
        bool prereqChecked;

        StepStatus coplayDevStatus = StepStatus.Unknown;
        string coplayDevVersion;
        string coplayDevError;
        ListRequest activeListRequest;
        AddRequest activeAddRequest;

        StepStatus bridgeStatus = StepStatus.Unknown;
        string bridgeUrl;
        string bridgeError;

        StepStatus promptsFolderStatus = StepStatus.Unknown;
        string promptsFolderError;

        StepStatus mcpJsonStatus = StepStatus.Unknown;
        string mcpJsonError;

        StepStatus claudeMdStatus = StepStatus.Unknown;
        string claudeMdError;

        Vector2 scroll;

        // --- Lifecycle --------------------------------------------------------------------------

        [MenuItem("Window/Claude/Bootstrap")]
        public static void Open()
        {
            var window = GetWindow<BootstrapWindow>();
            window.titleContent = new GUIContent("Claude Bootstrap");
            window.minSize = new Vector2(560, 600);
            window.RefreshAll();
            window.Show();
        }

        void OnEnable()
        {
            RefreshAll();
            EditorApplication.update += PollAsyncRequests;
        }

        void OnDisable()
        {
            EditorApplication.update -= PollAsyncRequests;
        }

        // --- Status refresh ---------------------------------------------------------------------

        void RefreshAll()
        {
            RefreshPrereqs();
            RefreshCoplayDev();
            RefreshBridge();
            RefreshPromptsFolder();
            RefreshMcpJson();
            RefreshClaudeMd();
        }

        void RefreshPrereqs()
        {
            prereqResult = PrereqCheck.Check();
            prereqChecked = true;
        }

        void RefreshCoplayDev()
        {
            if (activeAddRequest != null) return; // Don't disturb an in-progress install.
            coplayDevStatus = StepStatus.InProgress;
            coplayDevError = null;
            activeListRequest = CoplayDevInstaller.BeginQueryInstalled();
        }

        void RefreshBridge()
        {
            if (!McpIntegrationApi.IsAvailable)
            {
                bridgeStatus = StepStatus.Pending;
                bridgeUrl = null;
                bridgeError = "Install CoplayDev unity-mcp first (Step 2).";
                return;
            }

            try
            {
                bridgeUrl = McpIntegrationApi.GetMcpRpcUrl?.Invoke();
                bridgeStatus = (McpIntegrationApi.IsBridgeRunning?.Invoke() ?? false)
                    ? StepStatus.Done
                    : StepStatus.Pending;
                bridgeError = null;
            }
            catch (Exception ex)
            {
                bridgeStatus = StepStatus.Failed;
                bridgeError = ex.Message;
            }
        }

        void RefreshPromptsFolder()
        {
            var path = BootstrapSettings.PromptsFolder;
            promptsFolderStatus = AssetDatabase.IsValidFolder(path)
                ? StepStatus.Done
                : StepStatus.Pending;
            promptsFolderError = null;
        }

        void RefreshMcpJson()
        {
            var path = McpJsonPath();
            string expectedUrl = McpIntegrationApi.GetMcpRpcUrl?.Invoke() ?? McpJsonWriter.DefaultUrl;

            if (!File.Exists(path))
            {
                mcpJsonStatus = StepStatus.Pending;
                mcpJsonError = null;
                return;
            }

            mcpJsonStatus = McpJsonWriter.IsUnityMcpEntryCurrent(path, expectedUrl)
                ? StepStatus.Done
                : StepStatus.Warning;
            mcpJsonError = mcpJsonStatus == StepStatus.Warning
                ? $"Exists but UnityMCP entry doesn't match {expectedUrl}."
                : null;
        }

        void RefreshClaudeMd()
        {
            var path = ClaudeMdPath();
            claudeMdStatus = ClaudeMdWriter.SectionPresent(path) ? StepStatus.Done : StepStatus.Pending;
            claudeMdError = null;
        }

        // --- Polling ----------------------------------------------------------------------------

        void PollAsyncRequests()
        {
            if (activeListRequest != null && activeListRequest.IsCompleted)
            {
                if (CoplayDevInstaller.TryGetInstalledVersion(activeListRequest, out var version))
                {
                    coplayDevStatus = StepStatus.Done;
                    coplayDevVersion = version;
                    coplayDevError = null; // List is authoritative — clear any spurious Add error.
                }
                else if (!string.IsNullOrEmpty(coplayDevError))
                {
                    // List confirms not installed AND we have a cached Add error → real failure.
                    coplayDevStatus = StepStatus.Failed;
                }
                else
                {
                    coplayDevStatus = StepStatus.Pending;
                    coplayDevVersion = null;
                }
                activeListRequest = null;
                // Bridge availability and .mcp.json URL both depend on whether CoplayDev is present.
                RefreshBridge();
                RefreshMcpJson();
                Repaint();
            }

            if (activeAddRequest != null && activeAddRequest.IsCompleted)
            {
                // UPM's AddRequest can report a non-Success status even when the package was
                // actually installed (post-install scripts, domain reload, transient state).
                // Capture the diagnostic, then verify ground truth via Client.List(...) below.
                coplayDevError = activeAddRequest.Status == StatusCode.Success
                    ? null
                    : (activeAddRequest.Error?.message ?? "Unknown UPM error.");

                activeAddRequest = null;
                coplayDevStatus = StepStatus.InProgress;
                activeListRequest = CoplayDevInstaller.BeginQueryInstalled();
                Repaint();
            }
        }

        // --- Actions ----------------------------------------------------------------------------

        void ActionInstallCoplayDev()
        {
            coplayDevStatus = StepStatus.InProgress;
            coplayDevError = null;
            activeAddRequest = CoplayDevInstaller.BeginInstall();
        }

        void ActionStartBridge()
        {
            if (!McpIntegrationApi.IsAvailable) return;
            try
            {
                var ok = McpIntegrationApi.EnsureBridgeRunning?.Invoke() ?? false;
                bridgeStatus = ok ? StepStatus.Done : StepStatus.Failed;
                bridgeError = ok ? null : "Bridge did not become reachable after start.";
                if (ok) bridgeUrl = McpIntegrationApi.GetMcpRpcUrl?.Invoke();
            }
            catch (Exception ex)
            {
                bridgeStatus = StepStatus.Failed;
                bridgeError = ex.Message;
            }
            RefreshMcpJson();
        }

        void ActionCreatePromptsFolder()
        {
            var path = BootstrapSettings.PromptsFolder;
            if (PromptsFolderProvisioner.EnsureExists(path, out var err))
            {
                promptsFolderStatus = StepStatus.Done;
                promptsFolderError = null;
            }
            else
            {
                promptsFolderStatus = StepStatus.Failed;
                promptsFolderError = err;
            }
            AssetDatabase.Refresh();
        }

        void ActionWriteMcpJson()
        {
            var path = McpJsonPath();
            string url = McpIntegrationApi.GetMcpRpcUrl?.Invoke() ?? McpJsonWriter.DefaultUrl;
            var result = McpJsonWriter.EnsureUnityMcpServer(path, url, out var err);
            switch (result)
            {
                case McpJsonWriter.WriteResult.Unchanged:
                case McpJsonWriter.WriteResult.CreatedFile:
                case McpJsonWriter.WriteResult.UpdatedEntry:
                case McpJsonWriter.WriteResult.BackedUpAndReplaced:
                    mcpJsonStatus = StepStatus.Done;
                    mcpJsonError = result == McpJsonWriter.WriteResult.BackedUpAndReplaced
                        ? "Existing .mcp.json was malformed; backed up to .mcp.json.bak.<timestamp>."
                        : null;
                    break;
                default:
                    mcpJsonStatus = StepStatus.Failed;
                    mcpJsonError = err;
                    break;
            }
        }

        void ActionWriteClaudeMd()
        {
            var path = ClaudeMdPath();
            var result = ClaudeMdWriter.EnsureSection(path, out var err);
            switch (result)
            {
                case ClaudeMdWriter.WriteResult.Unchanged:
                case ClaudeMdWriter.WriteResult.CreatedFile:
                case ClaudeMdWriter.WriteResult.AppendedSection:
                case ClaudeMdWriter.WriteResult.ReplacedSection:
                    claudeMdStatus = StepStatus.Done;
                    claudeMdError = null;
                    break;
                default:
                    claudeMdStatus = StepStatus.Failed;
                    claudeMdError = err;
                    break;
            }
        }

        void ActionOpenPromptRunner()
        {
            PromptRunnerWindow.Open();
        }

        void ActionRunAllRemaining()
        {
            if (prereqChecked && !prereqResult.AllOk)
            {
                // Can't auto-fix prereqs — bail and let the user install them.
                Repaint();
                return;
            }
            if (coplayDevStatus == StepStatus.Pending) ActionInstallCoplayDev();
            // Bridge / mcp.json / etc. wait until CoplayDev finishes installing.
            if (coplayDevStatus == StepStatus.Done && bridgeStatus == StepStatus.Pending) ActionStartBridge();
            if (promptsFolderStatus == StepStatus.Pending) ActionCreatePromptsFolder();
            if (mcpJsonStatus == StepStatus.Pending || mcpJsonStatus == StepStatus.Warning) ActionWriteMcpJson();
            if (claudeMdStatus == StepStatus.Pending) ActionWriteClaudeMd();
        }

        // --- Drawing ----------------------------------------------------------------------------

        void OnGUI()
        {
            EditorGUILayout.LabelField("Claude Unity Bootstrap", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Step through the checklist or click Run all remaining. Re-runnable.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh status", GUILayout.Height(24))) RefreshAll();
                if (GUILayout.Button("Run all remaining", GUILayout.Height(24))) ActionRunAllRemaining();
            }
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawStep_Prereqs();
            DrawStep_CoplayDev();
            DrawStep_Bridge();
            DrawStep_PromptsFolder();
            DrawStep_McpJson();
            DrawStep_ClaudeMd();
            DrawStep_PromptRunner();

            EditorGUILayout.EndScrollView();
        }

        void DrawStep_Prereqs()
        {
            StepStatus status;
            string detail;
            if (!prereqChecked)
            {
                status = StepStatus.Unknown;
                detail = "Not checked yet.";
            }
            else if (prereqResult.AllOk)
            {
                status = StepStatus.Done;
                detail = $"{prereqResult.PythonVersion}, {prereqResult.UvxVersion}";
            }
            else
            {
                status = StepStatus.Failed;
                detail = BuildPrereqDetail(prereqResult);
            }

            BeginStep("1. Prerequisites", "Python 3.10+ and uvx on PATH.", status, detail);
            if (GUILayout.Button("Re-check", GUILayout.Width(120))) RefreshPrereqs();
            if (status == StepStatus.Failed)
            {
                EditorGUILayout.HelpBox(BuildPrereqInstructions(prereqResult), MessageType.Info);
            }
            EndStep();
        }

        void DrawStep_CoplayDev()
        {
            string detail = coplayDevStatus switch
            {
                StepStatus.Done => $"Installed: {coplayDevVersion}",
                StepStatus.Pending => "Not installed.",
                StepStatus.InProgress => "Working...",
                StepStatus.Failed => coplayDevError ?? "Install failed.",
                _ => ""
            };
            BeginStep("2. CoplayDev unity-mcp", "Adds the MCP bridge package via UPM git URL.",
                coplayDevStatus, detail);

            using (new EditorGUI.DisabledScope(coplayDevStatus == StepStatus.InProgress))
            {
                if (coplayDevStatus != StepStatus.Done)
                {
                    if (GUILayout.Button("Install", GUILayout.Width(120))) ActionInstallCoplayDev();
                }
                else if (GUILayout.Button("Re-check", GUILayout.Width(120))) RefreshCoplayDev();
            }
            EndStep();
        }

        void DrawStep_Bridge()
        {
            string detail = bridgeStatus switch
            {
                StepStatus.Done => $"Running at {bridgeUrl}",
                StepStatus.Pending => bridgeError ?? "Bridge is not running.",
                StepStatus.Failed => bridgeError ?? "Bridge start failed.",
                _ => ""
            };
            BeginStep("3. MCP bridge running", "Starts CoplayDev's local HTTP MCP server.",
                bridgeStatus, detail);

            using (new EditorGUI.DisabledScope(!McpIntegrationApi.IsAvailable))
            {
                if (bridgeStatus != StepStatus.Done)
                {
                    if (GUILayout.Button("Start bridge", GUILayout.Width(120))) ActionStartBridge();
                }
                else if (GUILayout.Button("Re-check", GUILayout.Width(120))) RefreshBridge();
            }
            EndStep();
        }

        void DrawStep_PromptsFolder()
        {
            var folder = BootstrapSettings.PromptsFolder;
            string detail = promptsFolderStatus switch
            {
                StepStatus.Done => folder + " exists.",
                StepStatus.Pending => folder + " is missing.",
                StepStatus.Failed => promptsFolderError ?? "Could not create folder.",
                _ => ""
            };
            BeginStep("4. Prompts folder", "Creates the prompts queue location.",
                promptsFolderStatus, detail);

            if (promptsFolderStatus != StepStatus.Done)
            {
                if (GUILayout.Button("Create", GUILayout.Width(120))) ActionCreatePromptsFolder();
            }
            else if (GUILayout.Button("Re-check", GUILayout.Width(120))) RefreshPromptsFolder();

            // Allow override.
            EditorGUILayout.LabelField("Folder path (EditorPref):", EditorStyles.miniLabel);
            var newPath = EditorGUILayout.DelayedTextField(folder);
            if (newPath != folder)
            {
                BootstrapSettings.PromptsFolder = newPath;
                RefreshPromptsFolder();
            }
            EndStep();
        }

        void DrawStep_McpJson()
        {
            string detail = mcpJsonStatus switch
            {
                StepStatus.Done => ".mcp.json has the UnityMCP HTTP entry.",
                StepStatus.Pending => ".mcp.json missing UnityMCP entry.",
                StepStatus.Warning => mcpJsonError ?? "Entry present but URL is stale.",
                StepStatus.Failed => mcpJsonError ?? "Write failed.",
                _ => ""
            };
            BeginStep("5. .mcp.json configured", "Writes/merges the project-root Claude Code MCP config.",
                mcpJsonStatus, detail);

            if (mcpJsonStatus != StepStatus.Done)
            {
                if (GUILayout.Button("Write entry", GUILayout.Width(120))) ActionWriteMcpJson();
            }
            else if (GUILayout.Button("Re-check", GUILayout.Width(120))) RefreshMcpJson();
            EndStep();
        }

        void DrawStep_ClaudeMd()
        {
            string detail = claudeMdStatus switch
            {
                StepStatus.Done => "Asset Workflow section present in CLAUDE.md.",
                StepStatus.Pending => "CLAUDE.md missing the Asset Workflow section.",
                StepStatus.Failed => claudeMdError ?? "Write failed.",
                _ => ""
            };
            BeginStep("6. CLAUDE.md section", "Appends/refreshes the bootstrap-managed Asset Workflow section.",
                claudeMdStatus, detail);

            if (claudeMdStatus != StepStatus.Done)
            {
                if (GUILayout.Button("Write section", GUILayout.Width(120))) ActionWriteClaudeMd();
            }
            else if (GUILayout.Button("Refresh section", GUILayout.Width(120))) ActionWriteClaudeMd();
            EndStep();
        }

        void DrawStep_PromptRunner()
        {
            BeginStep("7. Prompt Runner reachable",
                "Opens Window > Claude > Prompt Runner so you can confirm the runner appears.",
                StepStatus.Unknown, "");
            if (GUILayout.Button("Open Prompt Runner", GUILayout.Width(180))) ActionOpenPromptRunner();
            EndStep();
        }

        // --- Helpers ----------------------------------------------------------------------------

        void BeginStep(string title, string description, StepStatus status, string detail)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(StatusIcon(status), GUILayout.Width(24));
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            }
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(detail))
            {
                EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedMiniLabel);
            }
        }

        void EndStep()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        static string StatusIcon(StepStatus status) => status switch
        {
            StepStatus.Done => "✅",
            StepStatus.Pending => "⬜",
            StepStatus.InProgress => "⏳",
            StepStatus.Warning => "⚠️",
            StepStatus.Failed => "❌",
            _ => "·"
        };

        static string McpJsonPath()
        {
            return Path.Combine(ProjectRootPath(), ".mcp.json");
        }

        static string ClaudeMdPath()
        {
            return Path.Combine(ProjectRootPath(), "CLAUDE.md");
        }

        static string ProjectRootPath()
        {
            // Application.dataPath is <project>/Assets ; parent is the project root.
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        static string BuildPrereqDetail(PrereqCheck.Result r)
        {
            if (!r.PythonOk && !r.UvxOk) return "Python 3.10+ and uvx are missing.";
            if (!r.PythonOk) return $"Python 3.10+ not on PATH (found: {r.PythonVersion ?? "<none>"}).";
            if (!r.UvxOk) return "uvx not on PATH.";
            return "";
        }

        static string BuildPrereqInstructions(PrereqCheck.Result r)
        {
            var lines = new System.Text.StringBuilder();
            if (!r.PythonOk)
            {
                lines.AppendLine("Install Python 3.10+:");
                lines.AppendLine("  Windows:   winget install --id Python.Python.3.12");
                lines.AppendLine("  macOS:     brew install python@3.12");
                lines.AppendLine("  Linux:     use your distro's package manager");
                lines.AppendLine();
            }
            if (!r.UvxOk)
            {
                lines.AppendLine("Install uv (provides uvx):");
                lines.AppendLine("  Windows:   winget install --id astral-sh.uv");
                lines.AppendLine("  macOS:     brew install uv");
                lines.AppendLine("  Other:     irm https://astral.sh/uv/install.ps1 | iex");
            }
            lines.AppendLine();
            lines.Append("After installing, restart Unity so the Editor picks up the new PATH, then click Re-check.");
            return lines.ToString();
        }
    }
}
