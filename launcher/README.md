# The Table Launcher (prototype 0.1)

A small, independent Windows desktop app. It does not require Godot or DropDeploy on a friend's PC.

## Download a test build
Open the GitHub repository → **Actions** → **Build Windows Launcher** → latest successful run → **Artifacts** → **TheTableLauncher-Windows-x64**. Extract the artifact and run `TheTableLauncher.exe`. Do not distribute as a production build until tested.

## What happens today
- Looks up the repository's latest **published, non-prerelease** GitHub Release.
- The release tag must be `game-vX.Y.Z` (for example `game-v0.1.0`).
- That release must contain:
  - `TheTable-Windows.zip` with `TheTableGame.exe` at the ZIP root (Godot may also need a matching `.pck` file).
  - `TheTable-Windows.zip.sha256` containing the SHA-256 hash of the exact ZIP file.
- Downloads the ZIP, validates the hash, stages extraction, then swaps the existing game into `game.previous`.
- If the release API/download fails but a local game is installed, the **Play** button still opens the installed version.
- The app installs game files inside `%LOCALAPPDATA%\TheTable\game` and leaves itself elsewhere.

## First-run expectations
Until we publish the first game release, the launcher correctly displays **GAME NOT AVAILABLE YET**. This is expected, not a failure.

## Test and publish commands
On a Windows PC with the .NET 10 SDK:

```powershell
dotnet publish launcher/TheTableLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o output
```

Make a ZIP for a *real* Godot export (not a Godot source folder); name its executable `TheTableGame.exe`. If the export has an accompanying `.pck`, include it at the ZIP root. From PowerShell:

```powershell
Get-FileHash .\TheTable-Windows.zip -Algorithm SHA256 | Select-Object -ExpandProperty Hash | Set-Content .\TheTable-Windows.zip.sha256
```

Create a **GitHub Release** tagged `game-v0.1.0`, attach both assets, and publish. The launcher should detect it on next run.

## Important boundaries
- This is an **unsigned prototype**. Windows SmartScreen may warn. A signed installer/distribution strategy is still necessary for a polished release; don't tell friends to disable security features.
- This does not yet include a Windows installer, game-server compatibility negotiation, release signing, or a dedicated launcher self-update mechanism.
- The checksum is published alongside the release; this catches transfer errors or mismatches, but is not a standalone proof the publisher is trusted. Use official GitHub Releases only.
- Automatic rollback after a game launches and then crashes is not yet implemented; `game.previous` is retained for recovery.
- The launcher is separate from the public GitHub Pages site; do not directly expose unpublished builds on the download page.
