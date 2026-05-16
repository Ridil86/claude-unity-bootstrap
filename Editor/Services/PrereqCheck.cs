using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// Cross-platform check for the external tools the MCP bridge depends on: Python 3.10+ and
    /// the <c>uvx</c> Astral runner. Runs each tool with <c>--version</c> and parses the output.
    /// Read-only: never installs anything.
    /// </summary>
    public static class PrereqCheck
    {
        public struct Result
        {
            public bool PythonOk;
            public string PythonVersion;     // raw version output, e.g. "Python 3.12.10"
            public string PythonExecutable;  // "python" or "python3"

            public bool UvxOk;
            public string UvxVersion;        // raw "uvx 0.11.13 (...)"

            public bool AllOk => PythonOk && UvxOk;
        }

        const int PythonMinMajor = 3;
        const int PythonMinMinor = 10;

        public static Result Check()
        {
            var r = new Result();

            // Try `python` first, then `python3` (common on macOS / Linux).
            foreach (var exe in new[] { "python", "python3" })
            {
                var (ok, output) = TryRun(exe, "--version");
                if (ok && IsPython310OrNewer(output))
                {
                    r.PythonOk = true;
                    r.PythonExecutable = exe;
                    r.PythonVersion = output;
                    break;
                }
                if (ok && !string.IsNullOrEmpty(output))
                {
                    // Found Python but too old — record version for the UI message.
                    r.PythonExecutable = exe;
                    r.PythonVersion = output;
                }
            }

            var (uvxOk, uvxVer) = TryRun("uvx", "--version");
            r.UvxOk = uvxOk;
            r.UvxVersion = uvxVer;

            return r;
        }

        static bool IsPython310OrNewer(string versionLine)
        {
            if (string.IsNullOrEmpty(versionLine)) return false;
            var m = Regex.Match(versionLine, @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups[1].Value, out int major)) return false;
            if (!int.TryParse(m.Groups[2].Value, out int minor)) return false;
            if (major > PythonMinMajor) return true;
            if (major < PythonMinMajor) return false;
            return minor >= PythonMinMinor;
        }

        static (bool ok, string output) TryRun(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return (false, null);

                // Some tools (CPython on older Windows builds) print --version to stderr.
                var stdout = p.StandardOutput.ReadToEnd().Trim();
                var stderr = p.StandardError.ReadToEnd().Trim();

                if (!p.WaitForExit(5000))
                {
                    try { p.Kill(); } catch { /* ignore */ }
                    return (false, "<timeout>");
                }

                var combined = !string.IsNullOrEmpty(stdout) ? stdout : stderr;
                return (p.ExitCode == 0, combined);
            }
            catch (Exception)
            {
                // FileNotFoundException, Win32Exception, etc. — tool missing or unreachable.
                return (false, null);
            }
        }
    }
}
