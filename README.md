# WsVersionControl

A [Flax Engine](https://flaxengine.com/) plugin that provides professional Git-based source control integration directly inside the Flax Editor.

## Features

- **Editor toolbar integration** — Source Control button in the ToolStrip and Window menu
- **Changes tab** — Staged/unstaged file trees with color-coded change types, right-click context menus (stage, unstage, discard, open in explorer), inline diff viewer, and commit with amend support
- **History tab** — Commit log browser with search/filter by hash, author, or message, and a detail panel showing full commit info and changed files
- **Branches tab** — Local and remote branch listing, checkout, delete, create new branch with auto-checkout, ahead/behind counter
- **Sync tab** — Fetch, Pull, Push, Stash, Stash Pop, stash list with pop/apply/drop actions, merge conflict detection and file listing
- **Git initialization** — One-click Git repo initialization if the project isn't already a Git repository
- **Async git operations** — Background thread execution with main-thread callback marshalling for non-blocking UI

## Installation

1. Copy the `WsVersionControl` folder into your Flax project's `Plugins/` directory
2. Regenerate project files and rebuild

## Requirements

- Flax Engine 1.12+
- Git installed and available in PATH

## License

Copyright © Wavestorm Software. All rights reserved.
