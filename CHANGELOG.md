# Changelog

All notable changes to **Claude Unity Bootstrap** are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_Nothing yet._

## [0.1.2] - 2026-05-16

### Fixed
- `PrereqCheck` now falls back to well-known install locations when `uvx` isn't reachable via the inherited `PATH`. This handles the common case where the user installs `uv` while Unity Hub is already running (Hub captures `PATH` at launch, so the Editor inherits a stale env). When `uvx` is found via the fallback, the Bootstrap step also augments the current Editor process's `PATH` so CoplayDev's bridge (which spawns `uvx` by name) can resolve it without requiring a Unity Hub restart.

### Added
- `PrereqCheck.Result.UvxPath` — absolute path of the fallback-discovered binary, null when found via `PATH`.

## [0.1.1] - 2026-05-16

### Fixed
- Added `com.unity.ai.assistant` as a hard dependency in `package.json`. Without it the package failed to compile on Unity 6 projects whose template doesn't auto-include AI Assistant (compile error `CS0234: 'AI' does not exist in the namespace 'Unity'` from `PromptRunnerWindow.cs`). UPM now auto-installs the dependency.

## [0.1.0] - 2026-05-16

First public release. Lifted from embedded development inside Spiritbound to a standalone Git repo.

### Added
- Package skeleton (`package.json` with publishing metadata, asmdefs, license, changelog).
- `BootstrapWindow` step-by-step installer (`Window > Claude > Bootstrap`) — 7-step idempotent checklist covering prereqs, CoplayDev install, bridge start, `_Prompts/` folder, `.mcp.json`, `CLAUDE.md` section, and Prompt Runner discovery.
- `PromptRunnerWindow` (`Window > Claude > Prompt Runner`) — drives `_Prompts/*.md` through `AssistantApi.Run` in Agent mode. Two clicks per prompt: ▶ Run Next + ✓ Mark Done & Delete.
- Services: `PrereqCheck` (cross-platform Python / uvx detection), `CoplayDevInstaller` (UPM `Client.Add` wrapper), `PromptsFolderProvisioner` (`AssetDatabase.CreateFolder`), `McpJsonWriter` (Newtonsoft.Json merge of `.mcp.json` preserving other servers), `ClaudeMdWriter` (sentinel-bounded section append/replace), `BootstrapSettings` (EditorPrefs wrapper for the prompts folder path).
- `McpIntegrationApi` delegate registry + `UnityMcpBridge` façade — the asmdef split pattern that lets the package compile on projects without CoplayDev `unity-mcp` installed (gated by `MCP_FOR_UNITY_PRESENT` via `versionDefines`).
- `Editor/Templates/claude-md-asset-workflow.md` — the canonical "Asset Workflow" section text the bootstrap writes into consumer projects' `CLAUDE.md`.
