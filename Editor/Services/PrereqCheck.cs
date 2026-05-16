using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace RockRabbit.ClaudeUnityBootstrap.Editor.Services
{
    /// <summary>
    /// Cross-platform check for the external tools the MCP bridge depends on: Python 3.10+ and
    /// the <c>uvx</c> Astral runner. Runs each tool with <c>--version</c> and parses the output.
    ///
    /// <para>
    /// When <c>uvx</c> isn't found via the inherited <c>PATH</c> (a common pitfall — Unity Hub
    /// captures <c>PATH</c> at launch, so installing <c>uv</c> while Unity is running leaves it
    /// invisible), the check falls back to a list of well-known install locations per OS. If
    /// <c>uvx</c> is found in one of those, this class also augments the current process's
    /// <c>PATH</c> so subsequent child processes (e.g. CoplayDev's bridge) can resolve it.
    /// </para>
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
            public string UvxPath;           // absolute path when discovered via the fallback; null when found via PATH

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

            // First try uvx on the inherited PATH.
            var (uvxOk, uvxVer) = TryRun("uvx", "--version");
            if (uvxOk)
            {
                r.UvxOk = true;
                r.UvxVersion = uvxVer;
                return r;
            }

            // PATH lookup failed — probe known install locations. This covers the case where
            // `uv` was installed via winget / brew / Astral's installer *after* Unity Hub
            // launched; the Editor process inherited a stale PATH and won't see uvx until
            // Hub itself is restarted.
            foreach (var candidate in GetCommonUvxPaths())
            {
                if (!File.Exists(candidate)) continue;

                var (ok2, ver2) = TryRun(candidate, "--version");
                if (!ok2) continue;

                r.UvxOk = true;
                r.UvxVersion = ver2;
                r.UvxPath = candidate;
                AugmentProcessPath(Path.GetDirectoryName(candidate));
                return r;
            }

            // Truly not found.
            r.UvxOk = false;
            r.UvxVersion = uvxVer; // empty string or "<timeout>" — leave the diagnostic as-is.
            return r;
        }

        /// <summary>
        /// Well-known absolute paths where <c>uvx</c> commonly lands per OS. Ordered so the
        /// most-likely-correct location is checked first.
        /// </summary>
        static string[] GetCommonUvxPaths()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    return new[]
                    {
                        // winget shim layer — installed by `winget install astral-sh.uv`.
                        Path.Combine(local, "Microsoft", "WinGet", "Links", "uvx.exe"),
                        // Astral's official installer default (https://astral.sh/uv/install.ps1).
                        Path.Combine(home, ".local", "bin", "uvx.exe"),
                        // Alternative install root some users pick.
                        Path.Combine(local, "uv", "bin", "uvx.exe"),
                    };

                case PlatformID.Unix:
                case PlatformID.MacOSX:
                default:
                    return new[]
                    {
                        // macOS Apple-silicon Homebrew.
                        "/opt/homebrew/bin/uvx",
                        // macOS Intel Homebrew + Linux system installs.
                        "/usr/local/bin/uvx",
                        // Astral's official installer default on Unix.
                        Path.Combine(home, ".local", "bin", "uvx"),
                        "/usr/bin/uvx",
                    };
            }
        }

        /// <summary>
        /// Prepend <paramref name="dir"/> to the current process's <c>PATH</c>, idempotent.
        /// Lets CoplayDev's bridge — which spawns <c>uvx</c> by name via <c>Process.Start</c> —
        /// find it without requiring a Unity Hub restart.
        /// </summary>
        static void AugmentProcessPath(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;

            var sep = Path.PathSeparator;
            var current = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
            var normalized = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var part in current.Split(sep))
            {
                var trimmed = part.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(trimmed, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return; // already present
                }
            }

            Environment.SetEnvironmentVariable(
                "PATH",
                normalized + sep + current,
                EnvironmentVariableTarget.Process);
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
