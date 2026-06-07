<div align="center">
  <img src="Media/WSCBanner.png" alt="WsSourceControl Banner" width="100%" />
</div>

# WsSourceControl

WsSourceControl is a Flax Engine 1.12+ editor plugin that adds a dockable Git source-control panel to the Flax Editor.

Version `0.2.0` rewrites the core Git integration around LibGit2Sharp instead of ad-hoc shell command strings. The plugin is still beta, but the main workflows are now structured around typed Git operations, background remote sync, native confirmation dialogs for risky actions, and Flax-aware repository setup.

## Requirements

- Flax Engine 1.12 or newer
- Windows x64 or Linux x64 editor
- Existing Git credentials configured outside the plugin for private remotes

## Included Dependencies

The plugin vendors the runtime files needed by Flax plugin projects:

- `ThirdParty/LibGit2Sharp/net8.0/LibGit2Sharp.dll`
- `ThirdParty/LibGit2Sharp/net8.0/LibGit2Sharp.dll.config`
- `ThirdParty/LibGit2Sharp/runtimes/linux-x64/native/libgit2-3f4182d.so`
- `ThirdParty/LibGit2Sharp/runtimes/win-x64/native/git2-3f4182d.dll`
- LibGit2Sharp and libgit2 license files under `ThirdParty/LibGit2Sharp/licenses/`

Do not rely on generated `.csproj` NuGet restore for distribution. Flax uses `WsSourceControl.Build.cs` to reference and copy these files.

## Features

- Editor ToolStrip and Window menu entry with `F8` shortcut
- Dockable Source Control window
- Repository status snapshot with branch, upstream, ahead/behind, remote, conflict, and LFS indicators
- Changes tab with staged and unstaged trees, directory grouping, diff viewer, stage/unstage, commit, amend, discard selected, and discard all
- Guarded risky operations for discard, reset hard, amend, branch delete, stash pop, and stash drop
- History tab with search, commit detail, changed files, copy hash, and create-branch-from-commit action
- Branches tab with local/remote branches, current branch, upstream, ahead/behind, checkout, create, and guarded delete
- Sync tab with background fetch, pull, and push
- Stash list with save, apply, guarded pop, and guarded drop
- Merge conflict list with open-in-explorer actions
- No-repo flow with repository initialization and Flax `.gitignore` defaults
- Git LFS detection and guidance for LFS-tracked files and pointer files

## Git LFS

WsSourceControl detects `.gitattributes` entries using `filter=lfs` and detects Git LFS pointer files. It does not install Git LFS or manage `git lfs track` rules in this pass.

For large binary assets, configure Git LFS with the normal Git LFS tooling outside the plugin.

## Authentication

The plugin does not store tokens, passwords, PATs, or SSH key paths. Remote operations use credentials already configured for Git/libgit2 on the machine. Authentication failures are surfaced as operation errors in the editor.

## Installation

### Clone Through Flax

1. Open `Tools > Plugins`.
2. Use `Clone Project`.
3. Enter `https://github.com/WavestormSoftware/WsSourceControl.git`.
4. Restart the editor.

### Manual

1. Close Flax Editor.
2. Clone or copy this repository into `<your-game-project>/Plugins/WsSourceControl/`.
3. Add the plugin reference to your game `.flaxproj`:

```json
"References": [
  {
    "Name": "$(EnginePath)/Flax.flaxproj"
  },
  {
    "Name": "$(ProjectPath)/Plugins/WsSourceControl/WsSourceControl.flaxproj"
  }
]
```

4. Restart Flax Editor.

## Limitations

- macOS packaging is not included yet.
- Rebase, cherry-pick, revert, merge conflict editing, remote branch deletion, and force push are intentionally out of scope for this pass.
- The plugin does not replace Flax content browser or asset import workflows.
- Remote authentication depends on system/user Git credential configuration.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).

LibGit2Sharp and libgit2 licenses are included under `ThirdParty/LibGit2Sharp/licenses/`.
