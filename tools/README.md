# Seer Unity Asset Tooling

This directory now contains several independent entry points:

- `SeerAssetTool.Win`
  - A standalone Windows desktop tool under `tools/SeerAssetTool.Win`.
  - Can run as a clickable GUI or as a headless CLI.
  - Uses the same local install / synced mirror pipeline as the rest of this repo.
  - Supports two export layouts:
    - `cache`: directly usable by this Unity project (`monsters.json`, `pet_skin.json`, `1.png`, `countermark_xxx.png`)
    - `mirror`: GitHub-style mirror layout (`config/`, `newseer/assets/...`)

- `export-seer-assets.ps1`
  - Exports local Unity resources from an installed NewSeer client.
  - Works even if the main Unity project is already open.
  - Automatically syncs a temporary batch-safe project copy before running `Unity.exe -batchmode`.
- `probe-seer-remote.ps1`
  - Collects the currently known remote update signals.
  - Fetches `check-unity-ip.txt`, `online_gate`, `unity_notice`, local package versions, and live remote package metadata.
  - Auto-discovers the official CDN base from `resources.assets` when a local install is available.
- `sync-seer-remote.ps1`
  - Pulls the latest official `ConfigPackage` and `DefaultPackage` metadata directly from the Unity CDN.
  - Downloads only the bundle hashes required for config tables and the requested head/countermark assets.
  - Rebuilds a local mirror root under `Seer_Data/yoo/...` so the current Unity project can read it without launching the game client.

- `export-seer-config.py`
  - Pure Python config exporter.
  - Reads from local `rawfile/rawfile_txt` when available.
  - Falls back to reading `monsters.bytes` / `pet_skin.bytes` from `ConfigPackage` bundles via `UnityPy`.

- `export-seer-images.py`
  - Pure Python image exporter.
  - Reads heads and countermarks from `DefaultPackage` bundles via `UnityPy`.
  - Supports both `cache` and `mirror` layouts.

## Standalone Windows Tool

Project:

- `E:\Project\UnityProject\SeerUnityGradientLeaderboard\tools\SeerAssetTool.Win`

Published EXE:

- `E:\Project\UnityProject\SeerUnityGradientLeaderboard\tools\SeerAssetTool.Win\bin\Release\net8.0-windows\win-x64\publish\SeerAssetTool.Win.exe`

The published folder also contains the PowerShell/Python helper scripts that the EXE uses.

### GUI capabilities

- Choose a local install root or a synced mirror root
- Sync the latest official remote mirror
- Export to `cache` layout for the current Unity project
- Export to `mirror` layout for GitHub-style assets/config repos
- Auto-install `UnityPy` / `Pillow` when Python is already available

### CLI examples

Export from a local install into project-cache layout:

```powershell
.\tools\SeerAssetTool.Win\bin\Release\net8.0-windows\win-x64\publish\SeerAssetTool.Win.exe `
  export `
  --source-root 'D:\SeerLauncher\games\NewSeer' `
  --output-root '.\ToolCacheOut' `
  --layout cache
```

Sync the latest mirror and export from that mirror:

```powershell
.\tools\SeerAssetTool.Win\bin\Release\net8.0-windows\win-x64\publish\SeerAssetTool.Win.exe `
  sync-export `
  --install-root 'D:\SeerLauncher\games\NewSeer' `
  --mirror-root '.\StandaloneMirror' `
  --output-root '.\StandaloneCacheOut' `
  --layout cache
```

## Unity Integration

The Unity editor now has standalone-tool bridge menu items under:

- `Seer/Standalone Tool/Publish Windows Tool`
- `Seer/Standalone Tool/Launch Windows Tool`
- `Seer/Standalone Tool/Export Project Cache Via Tool`
- `Seer/Standalone Tool/Sync Latest Mirror And Export Cache Via Tool`

These menus use the standalone EXE instead of the old in-editor export chain, so the original Unity project can consume the same generated cache layout that the desktop tool outputs.

## Verified Local Export

The pipeline was verified against:

- Install root: `D:\SeerLauncher\games\NewSeer`
- Unity editor: `2022.3.11f1`
- Config version: `20260417162622`
- Default manifest version: `20260417155911`

Verified outputs:

- `6057` monsters
- `644` pet skins from local `pet_skin.txt`
- `5675` indexed monster heads
- `3719` indexed countermark icons

The current full export output is available at:

- `E:\Project\UnityProject\SeerUnityGradientLeaderboard\ExportedSeerMirror-full`

Its summary file is:

- `E:\Project\UnityProject\SeerUnityGradientLeaderboard\ExportedSeerMirror-full\seer-export-summary.json`

## Local Export Usage

Smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\export-seer-assets.ps1 `
  -InstallRoot 'D:\SeerLauncher\games\NewSeer' `
  -OutputDir '.\ExportedSeerMirror-smoke' `
  -Mode probe
```

Full export:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\export-seer-assets.ps1 `
  -InstallRoot 'D:\SeerLauncher\games\NewSeer' `
  -OutputDir '.\ExportedSeerMirror-full' `
  -Mode export
```

Useful options:

- `-UnityPath <path>`
- `-BatchProjectCopyRoot <path>`
- `-Limit <n>`
- `-SkipMonsters`
- `-SkipHeads`
- `-SkipCountermarks`

## Remote Probe Usage

Basic probe:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\probe-seer-remote.ps1 `
  -InstallRoot 'D:\SeerLauncher\games\NewSeer' `
  -OutputPath '.\remote-probe.json'
```

Probe a known remote base URL:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\probe-seer-remote.ps1 `
  -InstallRoot 'D:\SeerLauncher\games\NewSeer' `
  -RemoteBaseUrl 'https://example.com/yoo' `
  -OutputPath '.\remote-probe.json'
```

The official Seer Unity CDN base currently resolves to:

```text
https://newseer.61.com/Assets/StandaloneWindows64
```

## GitHub Actions Automation

The repo now includes a GitHub Actions workflow:

- `.github/workflows/seer-remote-sync.yml`

It is designed to run entirely from the official remote Unity CDN:

- restores the last cached mirror root if one exists
- runs `sync-seer-remote.ps1`
- exports `config/` and `newseer/assets/...` in `mirror` layout
- copies only the generated repo payload into the repository root
- commits and pushes changes automatically when generated files changed

Manual trigger options:

- `all_heads`
  - default `true`
  - exports the full `newseer/assets/art/ui/assets/pet/head/` mirror
- `all_countermarks`
  - default `false`
  - enables the heavier countermark export pass
- `head_ids`
  - optional comma-separated monster head IDs
  - used when `all_heads` is disabled
- `countermark_ids`
  - optional comma-separated countermark IDs
  - used when `all_countermarks` is disabled
- `commit_message`
  - optional override for the auto-generated commit message

The workflow also uploads summary JSON artifacts for each run:

- mirror sync summary
- combined export summary

The CI orchestration entry point is:

- `.github/scripts/export-seer-remote-to-repo.ps1`

Local example:

```powershell
powershell -ExecutionPolicy Bypass -File .\.github\scripts\export-seer-remote-to-repo.ps1 `
  -MirrorRoot .\SyncedSeerMirror-github `
  -ExportRoot .\ExportedSeerMirror-github `
  -RepoOutputRoot .\SomeOtherRepoClone `
  -AllHeads
```

Targeted local example:

```powershell
powershell -ExecutionPolicy Bypass -File .\.github\scripts\export-seer-remote-to-repo.ps1 `
  -MirrorRoot .\SyncedSeerMirror-github-targeted `
  -ExportRoot .\ExportedSeerMirror-github-targeted `
  -RepoOutputRoot .\SomeOtherRepoClone `
  -HeadIds 1,2
```

Verified full-head remote result:

- config version: `20260424150031`
- default manifest version: `20260424001633`
- monsters: `6312`
- indexed heads: `5685`
- exported heads: `5685`
- failed heads: `0`
- requested remote bundles: `21`

Important current limitation:

- remote `monsters.json` and image exports are in good shape
- remote-only `pet_skin.json` is still less authoritative than a local install export
- the GitHub flow preserves an existing repo `config/pet_skin.json` when the current remote export falls back, instead of blindly replacing it with a worse file

If you later want the built game to consume this repository directly as its mirror source, update the constants in `Assets/Scripts/SeerResources.cs` to point at the repository that receives the generated `config/` and `newseer/` output.

## Remote Sync Usage

Sync the latest config bundle plus a specific monster head:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\sync-seer-remote.ps1 `
  -MirrorRoot '.\SyncedSeerMirror-smoke' `
  -HeadIds 1
```

Sync the latest config bundle plus all monster heads:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\sync-seer-remote.ps1 `
  -MirrorRoot '.\SyncedSeerMirror-all-heads' `
  -AllHeads
```

Then export from that mirror root with the existing Unity batch exporter:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\export-seer-assets.ps1 `
  -InstallRoot '.\SyncedSeerMirror-smoke' `
  -OutputDir '.\ExportedSeerMirror-remote-smoke' `
  -Mode export `
  -Limit 1 `
  -SkipCountermarks
```

Verified remote smoke result:

- Remote mirror root: `E:\Project\UnityProject\SeerUnityGradientLeaderboard\SyncedSeerMirror-smoke`
- Remote export output: `E:\Project\UnityProject\SeerUnityGradientLeaderboard\ExportedSeerMirror-remote-smoke`
- Downloaded official bundles:
  - `ConfigPackage/30a5cf21c20d5c6fbbc82c1d14fdbf43`
  - `DefaultPackage/c1903dd080b20c234e62f0d8135b130f`

## Current Remote Signals

These are confirmed reachable today:

- `https://seer-login-ip.61.com/check-unity-ip.txt`
- `http://seerh5login.61.com/online_gate`
- `https://newseer.61.com/u_r.html`
- `http://124.222.192.41:8001/unity_notice/`

What is still not fully pinned down:

- The client-side transform that produces `ConfigPackage/rawfile_txt` from the official config bundle assets.
- Remote `monsters.bytes` and image bundle sync are working, but `pet_skin.txt` is still best treated as a fallback/mirror-data problem until that transform is fully reversed.
