# Claude Unity Bootstrap

A Unity 6 Editor package that wires a project to **[Claude Code](https://www.anthropic.com/claude-code)** in a single guided wizard. It installs the **[CoplayDev unity-mcp](https://github.com/CoplayDev/unity-mcp)** bridge so Claude can manipulate scenes/prefabs/SO instances/materials directly via MCP tool calls, scaffolds the `_Prompts/` queue, and ships a **Prompt Runner** window that drives generative-art prompts through Unity AI Assistant in Agent mode with two clicks per prompt.

Package ID: `com.rockrabbit.claude-unity-bootstrap` · License: [MIT](LICENSE.md) · Repo: [Ridil86/claude-unity-bootstrap](https://github.com/Ridil86/claude-unity-bootstrap)

---

## Contents

- [Why this exists](#why-this-exists)
- [What you get](#what-you-get)
- [Requirements](#requirements)
- [Installation](#installation)
- [First run — the Bootstrap wizard](#first-run--the-bootstrap-wizard)
- [The two-channel asset workflow](#the-two-channel-asset-workflow)
- [Tools reference](#tools-reference)
- [Configuration](#configuration)
- [Files the package writes/edits in your project](#files-the-package-writeseits-in-your-project)
- [Troubleshooting](#troubleshooting)
- [Package internals (for contributors)](#package-internals-for-contributors)
- [Uninstalling](#uninstalling)
- [License](#license)

---

## Why this exists

Wiring a Unity project to Claude Code from scratch requires ~7 manual steps spread across three tools (Unity Package Manager, the MCP for Unity bridge window, a text editor for `.mcp.json` and `CLAUDE.md`, plus a Python prereq install). Doing it once is tolerable; doing it for every new project is not.

This package turns those steps into a single re-runnable Editor window. It also fixes a subtler problem: the conventions for how Claude should interact with the project (MCP-first for assembly, prompts only for generative art) only work if every contributor agrees on them. The package codifies those conventions in a `CLAUDE.md` section it writes and re-syncs on demand.

---

## What you get

- **Bootstrap wizard** (`Window > Claude > Bootstrap`) — a 7-step checklist that detects what's done, fills in what isn't, and is safe to re-run on a project that's already configured.
- **Prompt Runner** (`Window > Claude > Prompt Runner`) — runs flat numbered `.md` prompts under `Assets/_Prompts/` through Unity AI Assistant via the public `AssistantApi.Run(...)` API. Two clicks per prompt: **▶ Run Next** → wait → **✓ Mark Done & Delete**.
- **`.mcp.json` writer/merger** — drops the `UnityMCP` HTTP entry at the project root so Claude Code finds the MCP bridge on launch. Preserves any other MCP servers you already have configured.
- **`CLAUDE.md` section manager** — appends a sentinel-bounded "Asset Workflow" section explaining the conventions. Edits outside the sentinels are preserved; edits inside are regenerated on the next bootstrap run.
- **Optional dependency handling** — the package compiles even when CoplayDev unity-mcp isn't installed yet. A second asmdef gated by `MCP_FOR_UNITY_PRESENT` carries the CoplayDev-specific bridge calls; if the package is absent, the relevant wizard step is disabled until step 2 installs it.

---

## Requirements

| | Required | Notes |
|---|---|---|
| Unity | **6000.0** or newer | Uses Unity 6's Package Manager git URL support and the AI Assistant 2.x API. |
| `com.unity.ai.assistant` | 2.x | Auto-installed by Unity 6 in most templates. Provides `Unity.AI.Assistant.Editor.Api.AssistantApi.Run(...)`. |
| `com.unity.nuget.newtonsoft-json` | ≥ 3.2.1 | Listed in `package.json` dependencies; auto-installed. |
| Python | 3.10+ | Required by the MCP bridge runtime (CoplayDev's Python backend). Must be on PATH. |
| `uvx` (via `uv`) | latest | [Astral's package runner](https://docs.astral.sh/uv/). Must be on PATH. Wizard detects and reports. |
| [Claude Code](https://www.anthropic.com/claude-code) | latest | The external CLI you'll talk to. Reads `.mcp.json` at the project root on launch. |
| OS | Windows, macOS, Linux | Built-in process spawn is cross-platform. Install commands in the wizard are OS-specific. |

The wizard will not auto-install Python or `uv` — those usually involve elevation and platform package managers, which we'd rather not own. The wizard surfaces the exact commands.

---

## Installation

### Option 1 — via Git URL (recommended)

In your project's `Packages/manifest.json`, add:

```json
"com.rockrabbit.claude-unity-bootstrap": "https://github.com/Ridil86/claude-unity-bootstrap.git#v0.1.0"
```

Pin to a tag so updates are intentional. Use `#main` for bleeding-edge.

### Option 2 — embedded package

Clone or download this repo and drop the contents into your project as `Packages/com.rockrabbit.claude-unity-bootstrap/`. Unity auto-detects embedded packages on next focus. Useful for forking or hacking on the package locally.

### Option 3 — local file path

Reference the repo on disk from `manifest.json`:

```json
"com.rockrabbit.claude-unity-bootstrap": "file:../some/path/claude-unity-bootstrap"
```

Good for local development — edits to the package are visible immediately in dependent projects.

---

## First run — the Bootstrap wizard

1. Open `Window > Claude > Bootstrap`.
2. The window shows a 7-row checklist with status icons (✅ done · ⬜ pending · ⏳ in progress · ⚠️ warning · ❌ failed). Either click each row's action button, or hit **Run all remaining** at the top.

The seven steps:

| # | Step | What it does |
|---|------|--------------|
| 1 | Prerequisites | Runs `python --version` and `uvx --version` as child processes. Verifies Python is 3.10+. Surfaces install commands per OS if anything is missing. **No auto-install.** |
| 2 | CoplayDev unity-mcp | Checks `UnityEditor.PackageManager.Client.List()` for `com.coplaydev.unity-mcp`. If absent, calls `Client.Add("https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main")`. After install, CoplayDev's own setup window auto-opens — let it run. |
| 3 | MCP bridge running | Calls `MCPServiceLocator.Server.StartLocalHttpServer(quiet: true)` via the optional integration asmdef. Bridge defaults to `http://127.0.0.1:8080`. |
| 4 | `Assets/_Prompts/` | Creates the prompts queue folder via `AssetDatabase.CreateFolder`. Path is overridable in the wizard UI; persisted via EditorPrefs. |
| 5 | `.mcp.json` configured | Writes/merges the project-root `.mcp.json` with a `UnityMCP` HTTP server entry pointing at the live bridge URL (`HttpEndpointUtility.GetMcpRpcUrl()`). Preserves other entries. Backs up malformed JSON. |
| 6 | `CLAUDE.md` section | Appends or refreshes a sentinel-bounded "Asset Workflow" section. Sentinels: `<!-- claude-unity-bootstrap:asset-workflow:start -->` … `<!-- claude-unity-bootstrap:asset-workflow:end -->`. If `CLAUDE.md` doesn't exist, a minimal one is created. |
| 7 | Open Prompt Runner | Opens `Window > Claude > Prompt Runner` so you can confirm it appears. |

After step 5 writes `.mcp.json`, **restart Claude Code** so it picks up the new project-scoped MCP server. Claude Code may also prompt you to approve the new server on launch — accept it.

The wizard is idempotent. Re-running on an already-configured project should report every step as ✅ and make no changes.

---

## The two-channel asset workflow

The convention the package installs into `CLAUDE.md`:

| Asset kind | Channel | Who acts |
|---|---|---|
| Scenes, prefabs, ScriptableObject instances, materials, GameObjects, components, lighting, NavMesh, scene hierarchy, asset moves/deletes | **Unity MCP tool calls** (`manage_scene`, `manage_prefabs`, `manage_scriptable_object`, `manage_material`, `manage_components`, `manage_gameobject`, …) | Claude Code calls directly. No pause, no prompt file. |
| Rigged models, painterly textures, VFX flipbooks, audio, music, UI illustrations | **Flat numbered prompts** in `Assets/_Prompts/`, run through the Prompt Runner via `AssistantApi.Run(...)` in Agent mode | Claude writes the prompt; user clicks Run Next + Mark Done & Delete. |

The MCP channel covers ~80% of typical Unity asset work. Prompts are reserved for the genuinely generative output that Unity AI Assistant can produce and MCP can't.

### Prompt convention

Prompts live **flat** under `Assets/_Prompts/`. Filenames are `NN-name.md` with zero-padded 2-digit prefixes so they sort lexicographically by execution order. Each prompt file follows this skeleton:

```markdown
# <asset name>

**Type:** <model | texture | vfx | audio | music | ui-illustration>
**Intended use:** <where the asset will be used>
**Style anchors:** <art direction summary>

## Prompt

<the prompt body to send to Unity AI Assistant>

## Acceptance criteria

- <what the result must look like>
- <integration notes — rig type, texel density, loop length, etc.>
```

Prompts are temporary. Once the user clicks **✓ Mark Done & Delete**, the runner removes the `.md` and `.meta` files together. `_Prompts/` should only ever contain pending work.

---

## Tools reference

### `Window > Claude > Bootstrap`

| UI element | What it does |
|---|---|
| **Refresh status** | Re-reads every step's current state from disk / PackageManager / EditorPrefs. |
| **Run all remaining** | Executes each step that's not ✅ in order, stopping at the first failure. |
| Per-step **action** button | Re-checks (when ✅) or performs the missing action (when ⬜). |
| Folder path field (step 4) | Override for `Assets/_Prompts/`; persists to `EditorPrefs` key `RockRabbit.ClaudeBootstrap.PromptsFolder`. |

### `Window > Claude > Prompt Runner`

| UI element | What it does |
|---|---|
| **Refresh** | Re-scans the configured prompts folder for `.md` files. |
| Queue list | All prompts sorted alphabetically. Click to select. The selected row gets a `▶` indicator. |
| Preview pane | Full content of the selected prompt (read-only). |
| **▶ Run Next** | Sends the selected prompt content to Unity AI Assistant via `AssistantApi.Run(content)` in Agent mode. Assistant window opens with prompt already submitted. |
| **✓ Mark Done & Delete** | `AssetDatabase.DeleteAsset` on the selected prompt (removes both `.md` and `.meta`). Cursor stays at the same index so the queue shifts up into it. |
| **↓ Skip** | Advance selection without deleting. Use when a prompt depends on an earlier one finishing first. |

The runner doesn't *know* when the Assistant finishes a prompt — `AssistantApi.Run` returns once the conversation is registered, not when the AI completes its work. The user judges completion and clicks Mark Done.

---

## Configuration

### EditorPrefs keys (per-machine, per-project)

| Key | Default | What it controls |
|---|---|---|
| `RockRabbit.ClaudeBootstrap.PromptsFolder` | `Assets/_Prompts` | Where the Prompt Runner scans. Must live under `Assets/` (so `AssetDatabase` queries work). Override via the wizard's step 4 path field. |

### Customizing the CLAUDE.md template

The text the wizard writes into the sentinel-bounded section lives in [`Editor/Templates/claude-md-asset-workflow.md`](Editor/Templates/claude-md-asset-workflow.md). Project-specific tweaks belong **outside** the sentinels in your `CLAUDE.md` — wizard re-runs will only rewrite the content between markers.

### Pointing at a different MCP bridge URL

The `.mcp.json` writer pulls the URL from `MCPServiceLocator.HttpEndpointUtility.GetMcpRpcUrl()`, which reads the value CoplayDev's bridge actually binds to. If you change the port in `Window > MCP for Unity`, re-run the wizard's step 5 to refresh `.mcp.json` accordingly.

---

## Files the package writes/edits in your project

When the wizard runs, these locations are touched:

| Path | Action |
|---|---|
| `<project>/.mcp.json` | Created or merged. Only the `mcpServers.UnityMCP` entry is owned by the package; other entries are preserved. Malformed files are backed up to `.mcp.json.bak.<timestamp>`. |
| `<project>/CLAUDE.md` | Sentinel-bounded section appended (if `CLAUDE.md` exists without markers), replaced (if markers exist), or written fresh (if `CLAUDE.md` doesn't exist). |
| `Assets/_Prompts/` (or your override) | Created if absent. Empty by default. |
| `Packages/manifest.json` | Modified by Unity itself when step 2 installs `com.coplaydev.unity-mcp`. No direct edits. |

Nothing else in the project is touched.

---

## Troubleshooting

### Step 1 reports `uvx` missing right after I installed `uv`

The Unity Editor process inherited the old PATH at launch. Quit Unity completely (Editor + the MCP for Unity bridge window) and reopen — the fresh process sees the new PATH. Click **Re-check**.

### Step 2 fails with a git/clone error

UPM uses your system `git`. Verify `git --version` runs in a fresh shell. On Windows, install via `winget install --id Git.Git`. If your machine sits behind a corporate proxy, set `git config --global http.proxy ...` so UPM can clone the CoplayDev repo.

### Step 5 writes `.mcp.json` but Claude Code doesn't see it

Claude Code reads project-scoped `.mcp.json` files **on launch**. After the wizard writes the file, fully restart Claude Code (quit and reopen on the project directory). On first launch with a new project-scoped MCP server, Claude Code may prompt you to approve it — accept.

### MCP tools aren't loaded in my Claude Code session

In Claude Code, ask "what MCP servers are loaded?" — if `UnityMCP` isn't listed, the connection isn't live. Common causes:
- CoplayDev's bridge isn't running (open `Window > MCP for Unity` and click Start Server, or re-run wizard step 3).
- `.mcp.json` points at the wrong URL. The wizard's step 5 fixes this by reading the live URL from CoplayDev's HttpEndpointUtility.
- Claude Code wasn't restarted after `.mcp.json` was written.

### CLAUDE.md section is wrong / I edited it and want it back

Just re-run the wizard's step 6. The content between the sentinels is regenerated from the template every time. Anything outside the sentinels in your `CLAUDE.md` is preserved.

### `AssistantApi.Run` throws "Unity provider not active" or similar

Open `Window > AI Assistant` once manually. The Assistant window initializes its provider on first display; subsequent `AssistantApi.Run` calls work after that.

---

## Package internals (for contributors)

```
claude-unity-bootstrap/                                       # repo root == package root
├── package.json                                              # UPM manifest, dependencies
├── README.md / CHANGELOG.md / LICENSE.md
└── Editor/
    ├── RockRabbit.ClaudeUnityBootstrap.Editor.asmdef        # Main asmdef. References Unity.AI.Assistant.API.Editor + Newtonsoft.Json. versionDefines sets MCP_FOR_UNITY_PRESENT when CoplayDev is installed.
    ├── McpIntegration/                                       # Optional sub-asmdef. defineConstraint = "MCP_FOR_UNITY_PRESENT" so it only compiles when CoplayDev is installed.
    │   ├── RockRabbit.ClaudeUnityBootstrap.McpIntegration.Editor.asmdef
    │   └── UnityMcpBridge.cs                                 # Façade over MCPServiceLocator.Server / HttpEndpointUtility. Registers delegates into McpIntegrationApi on InitializeOnLoad.
    ├── Services/
    │   ├── BootstrapSettings.cs                              # EditorPrefs wrapper (prompts folder path)
    │   ├── McpIntegrationApi.cs                              # Static delegate registry. The optional asmdef populates it; main asmdef invokes it without a hard reference.
    │   ├── PrereqCheck.cs                                    # Cross-platform Python / uvx detection via Process.Start
    │   ├── CoplayDevInstaller.cs                             # Wrapper for UPM Client.Add / Client.List
    │   ├── PromptsFolderProvisioner.cs                       # AssetDatabase.CreateFolder (creates intermediate folders)
    │   ├── McpJsonWriter.cs                                  # Newtonsoft.Json merge of .mcp.json; preserves other servers; backs up malformed
    │   └── ClaudeMdWriter.cs                                 # Sentinel-bounded section append/replace
    ├── Templates/
    │   └── claude-md-asset-workflow.md                       # The body content of the managed section
    └── Windows/
        ├── BootstrapWindow.cs                                # The 7-step IMGUI wizard
        └── PromptRunnerWindow.cs                             # The prompts queue runner
```

### Why two asmdefs

The package's main asmdef compiles on any Unity 6 project, regardless of whether CoplayDev unity-mcp is installed. That's important because step 2 of the wizard installs CoplayDev — before that runs, the package is already loaded and the user needs the BootstrapWindow accessible.

Code that depends on CoplayDev's types (e.g. `MCPServiceLocator.Server.StartLocalHttpServer`) lives in the secondary asmdef `RockRabbit.ClaudeUnityBootstrap.McpIntegration.Editor`. That asmdef has:

- A hard `references` entry on `MCPForUnity.Editor`.
- A `defineConstraint` on `MCP_FOR_UNITY_PRESENT`. The constraint is set via `versionDefines` keyed on `com.coplaydev.unity-mcp` so it activates automatically when the package becomes present.

When CoplayDev isn't installed, the secondary asmdef is excluded from compilation. The main asmdef still compiles; the relevant wizard steps detect this state (`McpIntegrationApi.IsAvailable == false`) and disable themselves with a "Install CoplayDev first" hint.

### The delegate registry pattern

The main asmdef can't reference the optional one directly (the optional one wouldn't be compiled when CoplayDev is absent). Instead, `McpIntegrationApi` exposes a static set of nullable delegates:

```csharp
public static class McpIntegrationApi
{
    public static bool IsAvailable => GetMcpRpcUrl != null;
    public static Func<string> GetMcpRpcUrl;
    public static Func<bool> IsBridgeRunning;
    public static Func<bool> EnsureBridgeRunning;
}
```

`UnityMcpBridge` (in the optional asmdef) populates these via `[InitializeOnLoadMethod]` whenever it's compiled. The main asmdef checks `IsAvailable` and invokes the delegates without ever taking a direct compile-time dependency on CoplayDev's types.

### Adding more wizard steps

`BootstrapWindow.cs` is a single-file IMGUI EditorWindow. Each step has:

1. A backing `StepStatus` field plus per-step state (versions, errors).
2. A `RefreshXxx()` method that reads state and sets the status field.
3. A `DrawStep_Xxx()` method that calls `BeginStep(title, description, status, detail)` and adds the step's action button(s).
4. (Optional) An `ActionXxx()` method invoked by the button. Long-running work polls async requests via `EditorApplication.update`.
5. An entry in `ActionRunAllRemaining()` so the master button drives it.

---

## Uninstalling

To remove the package from a project:

1. Remove the `com.rockrabbit.claude-unity-bootstrap` line from `Packages/manifest.json` (or delete the embedded folder).
2. Optional cleanup:
   - Delete the `<!-- claude-unity-bootstrap:asset-workflow:start --> … <!-- claude-unity-bootstrap:asset-workflow:end -->` block from `CLAUDE.md` if you no longer want the workflow notes there.
   - Remove the `mcpServers.UnityMCP` entry from `.mcp.json` if you're also removing CoplayDev's bridge.
   - Delete `Assets/_Prompts/` if you don't have pending prompts.
3. Don't forget to remove CoplayDev's package separately if you also want the bridge gone — this package adds it as a dependency but doesn't own it.

EditorPrefs are cleaned up automatically the next time you uninstall Unity. To purge them manually, search for `RockRabbit.ClaudeBootstrap.*` in the Editor preferences plist / registry.

---

## License

MIT. See [LICENSE.md](LICENSE.md).
