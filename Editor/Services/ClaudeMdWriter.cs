using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// Writes the bootstrap-managed "Asset Workflow" section into a project-root CLAUDE.md file.
    /// The section is delimited by HTML-comment sentinels so re-runs are idempotent — content
    /// between the markers is regenerated from the embedded template, content outside is preserved.
    /// </summary>
    public static class ClaudeMdWriter
    {
        public const string SectionStartMarker = "<!-- claude-unity-bootstrap:asset-workflow:start -->";
        public const string SectionEndMarker = "<!-- claude-unity-bootstrap:asset-workflow:end -->";

        const string TemplatePackagePath =
            "Packages/com.rockrabbit.claude-unity-bootstrap/Editor/Templates/claude-md-asset-workflow.md";

        public enum WriteResult
        {
            Unchanged,
            CreatedFile,
            AppendedSection,
            ReplacedSection,
            Failed
        }

        /// <summary>
        /// Ensures the project-root CLAUDE.md contains the bootstrap-managed Asset Workflow section.
        /// Creates the file if absent, appends the section if missing, or refreshes the section in
        /// place if the markers are present.
        /// </summary>
        /// <param name="claudeMdAbsolutePath">Absolute path to CLAUDE.md (typically &lt;project&gt;/CLAUDE.md).</param>
        public static WriteResult EnsureSection(string claudeMdAbsolutePath, out string error)
        {
            error = null;

            string template;
            try
            {
                var templateAbs = Path.GetFullPath(TemplatePackagePath);
                if (!File.Exists(templateAbs))
                {
                    error = $"Template not found at '{templateAbs}'.";
                    return WriteResult.Failed;
                }
                template = File.ReadAllText(templateAbs);
            }
            catch (Exception ex)
            {
                error = $"Failed to read template: {ex.Message}";
                return WriteResult.Failed;
            }

            // Sanity: template must contain both markers.
            if (!template.Contains(SectionStartMarker) || !template.Contains(SectionEndMarker))
            {
                error = "Template is missing one or both sentinel markers.";
                return WriteResult.Failed;
            }

            try
            {
                if (!File.Exists(claudeMdAbsolutePath))
                {
                    // Create minimal CLAUDE.md with just the bootstrap section.
                    var initial =
                        "# CLAUDE.md" + Environment.NewLine +
                        Environment.NewLine +
                        "Project notes for Claude Code. Edit freely. The Asset Workflow section below is " +
                        "managed by Claude Unity Bootstrap — edit outside the sentinels." +
                        Environment.NewLine +
                        Environment.NewLine +
                        template +
                        Environment.NewLine;
                    File.WriteAllText(claudeMdAbsolutePath, initial, new UTF8Encoding(false));
                    return WriteResult.CreatedFile;
                }

                var existing = File.ReadAllText(claudeMdAbsolutePath);
                int startIdx = existing.IndexOf(SectionStartMarker, StringComparison.Ordinal);
                int endIdx = existing.IndexOf(SectionEndMarker, StringComparison.Ordinal);

                if (startIdx < 0 && endIdx < 0)
                {
                    // Markers absent — append the section to the end.
                    var sb = new StringBuilder(existing);
                    if (!existing.EndsWith("\n") && !existing.EndsWith("\r\n"))
                    {
                        sb.Append(Environment.NewLine);
                    }
                    sb.Append(Environment.NewLine);
                    sb.Append(template);
                    sb.Append(Environment.NewLine);
                    File.WriteAllText(claudeMdAbsolutePath, sb.ToString(), new UTF8Encoding(false));
                    return WriteResult.AppendedSection;
                }

                if (startIdx < 0 || endIdx < 0 || endIdx < startIdx)
                {
                    error = $"CLAUDE.md sentinels are damaged (start={startIdx}, end={endIdx}). " +
                            $"Restore both markers or delete the section manually, then re-run.";
                    return WriteResult.Failed;
                }

                int endMarkerEnd = endIdx + SectionEndMarker.Length;
                var before = existing.Substring(0, startIdx);
                var after = existing.Substring(endMarkerEnd);
                var newContent = before + template + after;

                if (newContent == existing)
                {
                    return WriteResult.Unchanged;
                }

                File.WriteAllText(claudeMdAbsolutePath, newContent, new UTF8Encoding(false));
                return WriteResult.ReplacedSection;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogException(ex);
                return WriteResult.Failed;
            }
        }

        /// <summary>
        /// Returns true if the file exists and currently contains both sentinel markers.
        /// </summary>
        public static bool SectionPresent(string claudeMdAbsolutePath)
        {
            if (!File.Exists(claudeMdAbsolutePath)) return false;
            var text = File.ReadAllText(claudeMdAbsolutePath);
            return text.Contains(SectionStartMarker) && text.Contains(SectionEndMarker);
        }
    }
}
