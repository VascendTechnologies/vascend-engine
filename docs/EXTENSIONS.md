# Vascend Engine extensions — API 1

[Italiano](EXTENSIONS.it.md)

Extensions live in `%USERPROFILE%\.config\vascend-engine\extensions\<id>`. Each folder contains an `extension.json` manifest and any required files. Settings are stored separately in `extension-settings/<id>.json`.

## Installation and updates

Use **Add → From GitHub repository** to enter a public repository URL or `owner/repository`. Engine discovers manifests at the root and in subfolders, then lets you select the plugins to install. The optional reference field accepts a branch, tag or commit; leaving it blank follows the default branch. Enter repository URLs, not `/tree/...` URLs.

Plugins start disabled. Enable them from the catalog after the conflict check. **Connected repositories** records the source, reference and installed commit. Updates are staged and applied on the next app launch, retaining settings. A pinned tag or SHA continues to point at that version.

For local installation, select **Add → From local folder** and choose the folder containing the manifest. Existing IDs are not overwritten.

GitHub installation supports public repositories on github.com without authentication. Submodules, Git LFS pointers and release-only assets are not resolved: required files must be included in the repository snapshot. Limits are 64 MB downloaded, 256 MB extracted, 10,000 archive entries and 100 manifests per repository. Symlinks, unsafe paths and duplicate archive paths are rejected.

## Manifest

```json
{
  "apiVersion": 1,
  "id": "community.my-plugin",
  "name": "My plugin",
  "version": "1.0.0",
  "author": "Your name",
  "description": "A page with persistent settings.",
  "settings": [
    { "key": "enabled", "label": "Enabled", "type": "boolean", "default": true },
    { "key": "message", "label": "Message", "type": "string", "default": "Hello Vascend" },
    { "key": "interval", "label": "Interval in seconds", "type": "number", "default": 30 }
  ],
  "conflictsWith": [],
  "exclusiveResources": []
}
```

IDs must be unique and contain lowercase letters, numbers, dots, underscores or hyphens. The built-in ID `vascend-technologies` and legacy ID `vascend-ghiaccio` are reserved. API version 1 is supported.

Settings support `string`, `boolean` and `number`, each with a unique `key`. The host generates the page and saves values using **Save settings**. Numeric values use a decimal point. Unsaved edits are discarded when navigating away.

A manifest declares UI and data; it does not implement custom behavior. Use a DLL for that.

## Native WPF page

Create an x64 class library targeting `net8.0-windows10.0.19041.0` with `UseWPF=true`. Reference the installed `VascendEngine.dll` with `Private=false`; do not distribute a second copy of the host assembly.

```csharp
using System.Windows;
using System.Windows.Controls;
using VascendEngine;

public sealed class MyExtension : IEngineExtension
{
    public FrameworkElement CreatePage(ExtensionContext context)
    {
        return new TextBlock
        {
            Text = context.Language == "it" ? "La mia pagina" : "My page",
            FontSize = 24
        };
    }
}
```

Add `"entryAssembly": "MyExtension.dll"` and `"entryType": "MyExtension"` to the manifest. Include the compiled DLL, dependencies and `.deps.json` in the extension folder. Engine downloads snapshots; it does not compile them or execute installation scripts.

`ExtensionContext` exposes:

- `ExtensionDirectory`: installed plugin folder.
- `SettingsPath`: user settings file.
- `LoadSettings()` and `SaveSettings(Dictionary<string, JsonElement>)`.
- `ActivationToken`: cancellation token cancelled when the loaded plugin is disabled.
- `Language`: host UI language, `en` or `it`.

Native code loads when an enabled extension's page is opened. The host keeps its instance during the session. Plugin authors own the lifecycle of their controls, background work and resources. Custom pages and authored metadata are not automatically translated by the host.

Disabled native plugins are not loaded to render a preview. Their manifest-declared settings remain available in read-only mode; arbitrary native pages require activation.

## Lifecycle and restoration

Implement `IEngineExtensionLifecycle` to handle `OnActivated(ExtensionContext)` and `OnDeactivated()`. Stop background work, unsubscribe events and release resources on deactivation. Use `ActivationToken` for cooperative cancellation.

Older plugins without this lifecycle contract may require an Engine restart before they can be enabled again. Blocking their page cannot forcibly stop arbitrary code that has already started.

Optionally implement `IEngineExtensionRestoration`:

```csharp
string RestorationDescription { get; }
Task RestoreSettingsAsync(CancellationToken cancellationToken);
```

The host can offer to restore settings when a loaded plugin is disabled. The plugin is responsible for retaining and restoring previous values.

## Conflict declarations

`conflictsWith` lists incompatible extension IDs. Checks work in both directions. `exclusiveResources` declares resources a plugin owns exclusively. Equal names and overlapping hierarchies conflict: `desktop.widgets` overlaps with `desktop.widgets.clock`.

The built-in Vascend Technologies extension reserves:

```text
desktop.bar
desktop.widgets
desktop.island
system.appearance
editor.vscode.theme
editor.vascend.theme
terminal.theme
```

Activation is blocked while an incompatible extension is enabled. Duplicate IDs are also blocked. Declarations must be accurate: the host cannot infer every side effect of arbitrary plugin code.

## Trust and local files

Native plugins run with the user's permissions. Install only code from authors you trust. Claude Code plugins are a different format and are not directly compatible.

The host stores activation in `extension-state.json`, GitHub provenance in `extension-sources/<id>.json`, and pending updates in `extension-updates/<id>`. Update application failures are logged in `errori.log`. The catalog refresh control rereads local extensions; it does not perform automatic GitHub update checks.
