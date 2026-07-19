<p align="center">
  <img src="src/AssestBundleTools.Presentation.WinUI/Assets/AppLogo.png" width="96" alt="AssestBundleTools icon">
</p>

# AssestBundleTools

English | [简体中文](README.md)

AssestBundleTools is a Windows workspace tool for Unity AssetBundles. It identifies, browses, previews, extracts, replaces, and rebuilds game bundles, with practical support for encrypted bundles, asset relationships, recoverable edits, and export using the source encryption method.

> The project is currently in preview. Keep backups of original assets and validate rebuilt bundles in the target game.

## Features

- Workspace management with file and directory import, cached indexes, browsing-state restoration, and incremental refresh.
- Automatic bundle recognition that tries available codecs first and falls back to validated Unity assets while rejecting unrelated files.
- Built-in Arknights LZ4AK and CrossCore duplicated-header codec CLIs, including source metadata preservation for re-encrypted exports.
- Asset information, dependency inspection, and previews for common Unity assets, images, audio, meshes, text, and Spine groups.
- Single-asset, batch, and Spine-group extraction and replacement, including drag-and-drop workflows.
- AssetBundle rebuild and export with selectable compression and source-codec encryption.
- External AssetBundle codec CLI and resource-processor CLI support, with shared online component installation infrastructure.
- A modern light/dark Windows interface with Windows 10/11 icon compatibility and 125%/150% high-DPI layouts.

## Downloads

Download the latest build from [GitHub Releases](https://github.com/laoxinH/AssestBundleTools/releases).

| Architecture | Intended devices | Packages |
| --- | --- | --- |
| `win-x64` | Most 64-bit Intel and AMD PCs | Portable ZIP / Installer MSI |
| `win-x86` | PCs requiring 32-bit compatibility | Portable ZIP / Installer MSI |
| `win-arm64` | Windows on ARM devices | Portable ZIP / Installer MSI |

The MSI wizard supports per-user or all-users installation, a custom destination, and an optional desktop shortcut. Current packages are unsigned, so Windows SmartScreen may show a warning. Only download official releases from this repository.

The portable package runs after extraction. Keep workspace data outside the application directory so later updates or fresh extractions do not mix program files with user data.

## Basic Workflow

1. Create a workspace and select its data directory.
2. Choose automatic recognition or a specific AssetBundle codec.
3. Drop AssetBundle files or a directory into the workspace and wait for indexing.
4. Browse assets and dependencies, preview content, and apply extraction, replacement, or editing operations.
5. Use **Save as new bundle** to export modified bundles, then validate them in the target game.

The workspace keeps original inputs intact by default and records changes as recoverable operations. Separate backups are still recommended before replacement and export work.

## Updates

Release builds check GitHub Releases after startup, and the About page provides a manual check. When an update is available, the confirmation dialog displays the release notes for that version. Installed and portable builds use architecture-specific channels. Cross-version and multi-device validation of the automatic apply-and-restart path is ongoing.

## Development

Core technologies:

- .NET 8
- WinUI 3 / Windows App SDK
- AssetsTools.NET
- Velopack
- xUnit and repository console test suites

The repository follows this dependency direction:

```text
Presentation.WinUI -> Application -> Domain
Infrastructure     -> Application + Domain
```

Visual Studio MSBuild is recommended for the WinUI project. See the [build and debugging guide](docs/design/debug-setup.md), [architecture guidelines](docs/design/code-architecture-guidelines.md), and [design documentation index](docs/design/README.md).

## Contributing

Read [AGENTS.md](AGENTS.md) and the relevant design documents before making changes. User-facing text must be localized, and parsing, caching, or file IO should not be implemented directly in Views or ViewModels.

Useful issue reports include the application version, Windows version and architecture, workspace diagnostics, the selected codec, and a shareable minimal sample or stack trace.

## Project Information

- GitHub: [laoxinH/AssestBundleTools](https://github.com/laoxinH/AssestBundleTools)
- Website: [www.modwu.com](https://www.modwu.com)
- Icon artist: QingLei (青雷)
- License: [MIT License](LICENSE.md)

Bundled third-party components remain subject to their respective licenses and copyright notices.
