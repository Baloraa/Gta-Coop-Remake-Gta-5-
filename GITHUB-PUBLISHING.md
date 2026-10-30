# Publishing GTACoop Remake on GitHub

This file is a practical checklist for making the repository public. It is not part of the user-facing README.

## Repository description

Use this in the GitHub **About** field:

> Windows launcher and server manager for GTA Coop: mod delivery, short codes, relay fallback, live server status, and session cleanup for GTA V story-mode co-op.

Suggested topics:

`gta-v` `gtacoop` `gta-coop` `multiplayer` `coop` `launcher` `winforms` `dotnet` `csharp` `mod-manager` `relay` `story-mode`

## Release 2.4.1 description

Use this as the GitHub Release body:

> **GTACoop Remake 2.4.1 — in-game F9 server browser fix**
>
> The in-game F9 server browser now works when GTA V is launched normally, without the launcher. The default master (`https://master.gtacoop.com`) currently answers HTTP 526, and the old client blocked on it while the menu froze. This release makes discovery asynchronous and resilient.
>
> **Working list first.** When the configured primary master is the known dead host, the client tries the working backup list first and only then the primary, with a 5-second timeout per request. A "Searching..." row shows progress, and servers appear as discovery responses arrive instead of freezing the game.
>
> **Shared server cache.** Found servers are written to `%LOCALAPPDATA%\cgmp\gtacoop\servers.json` and shown immediately the next time F9 is opened. The launcher writes the same cache after a successful scan, so the list stays populated even when in-game UDP discovery is restricted.
>
> **Better local discovery.** Local peers are probed on ports 4499, 4498, and 44499, and every master endpoint is queried with its own error handling.
>
> **Settings migration.** Existing `ClientSettings.xml` files that still use the dead `https://master.gtacoop.com` default are moved to the backup list. Custom master addresses are never changed.
>
> **Upgrade:** replace the application folder with the new build, or run the installer as usual. Your `%LOCALAPPDATA%\GTACoopRemake` settings, server list, and logs are kept in place. The share ZIP still excludes GTA V, ScriptHookV, ScriptHookVDotNet, and RageOpenV. The bundled `OpenIV.asi` is user-supplied.

Attach `GTACoop-Remake-Share-2.4.1.zip`, `GTACoop.Launcher.exe`, and `GTACoop.Sync.dll` as release assets. Do not commit them to the repository.

## Before you make the repository public

### 1. Check the license and attribution

- The project is GPL-3.0. Keep `LICENSE.md` in the repository and keep the upstream attribution in `README.md` and the About tab.
- The upstream GTA Coop code is GPL-3.0. The original code is credited to Guad, with modifications by Bluscream and wolfmitchell, maintained by oldnapalm and contributors.
- If you modified upstream files, keep their copyright headers and note the changes. GPL-3.0 requires preserving the license and source availability.

### 2. Check every binary under `vendor/`

The source code being GPL-3.0 does not automatically grant permission to republish third-party binaries. Before publishing, review:

- `vendor/Distribution/Components/OpenIV.asi` — user-supplied; confirm you have the right to redistribute it. If not, remove it from the repository and keep it only in the local build input or ask users to supply it.
- `vendor/Distribution/Scripts/*` — runtime DLLs used by the client. Check the licenses of LemonUI, Lidgren.Network, NAudio, Newtonsoft.Json, protobuf-net, MaxMind GeoIP2, and the others.
- `vendor/Distribution/Server/*` — upstream server binaries and their dependencies. Check the upstream distribution terms and the third-party licenses.
- `vendor/Distribution/Launcher/*` — prebuilt launcher files. If these are stale, remove them or document why they are there. Do not publish a prebuilt binary that you cannot rebuild.
- `vendor/Build/` — already ignored by `.gitignore`. Keep it that way.

The share ZIP intentionally excludes ScriptHookV, ScriptHookVDotNet, and RageOpenV. ScriptHookV redistribution is not allowed; RageOpenV asks users not to redistribute built binaries. Keep those downloads on the user's machine.

### 3. Keep generated output out of git

`.gitignore` already excludes:

```
dist/
bin/
obj/
publish/
vendor/Build/
```

Verify with:

```powershell
git status --short
git ls-files | Select-String -Pattern '^(dist|bin|obj|publish|vendor/Build)/'
```

The second command should return nothing.

### 4. README layout (already applied)

The repository uses English as the default landing page:

- `README.md` — English, detailed, with sources.
- `README.tr.md` — Turkish, detailed.
- Both files have a language link at the top.

Do not delete the Turkish documentation; it is the primary guide for the existing users and the friend-facing ZIP. If you later prefer Turkish as the default, swap the two files and update the language links in both.

### 5. Prepare the release asset

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
powershell -ExecutionPolicy Bypass -File kur.ps1
powershell -ExecutionPolicy Bypass -File paylas.ps1
```

The share ZIP is written to `dist\GTACoop-Remake-Share-<version>.zip`. Upload that file to the GitHub Release. Do not add it to the repository.

**Every release must also run `kur.ps1`.** It copies the fresh `dist\GTACoopRemake` build over the locally installed app at `%LOCALAPPDATA%\Programs\GTACoop Remake` and refreshes the desktop/Start-menu shortcuts. Do not skip this step: building `dist` and the ZIP alone leaves the user's installed EXE on the old version.

Before uploading, check:

- The ZIP contains the launcher, `Distribution\SyncServer`, the docs, the GPL notice, and `Distribution\Components\OpenIV.asi`.
- The ZIP does **not** contain `GTA5.exe`, `PlayGTAV.exe`, `ScriptHookV.dll`, `ScriptHookVDotNet*.dll/.asi`, `RageOpenV.asi`, or `.rpf` files.
- The launcher and DLL versions match the release tag.
- The GTA V protected files and `update.rpf` hashes were not changed by the build.

### 6. Repository settings

- Enable **Issues**. A bug report template should ask for the diagnostic text, launcher version, Windows version, GTA V edition/build, ScriptHookV/SHVDN channel/version, and whether the connection was direct, UPnP, or relay.
- Add a short **SECURITY.md** or a pinned issue: do not post passwords, sync tokens, short codes that are still active, or full `settings.json` files.
- If you enable **Discussions**, use it for setup questions and keep Issues for reproducible bugs.
- Add the GPL-3.0 license in the GitHub repository settings so the license badge is correct.
- Pin a "Read this before reporting a connection problem" issue with the direct/UPnP/relay decision tree and the log locations.

### 7. What not to publish

- GTA V game files or assets.
- ScriptHookV, ScriptHookVDotNet, or RageOpenV binaries.
- Copyrighted vehicle/ped/map packs that you do not have permission to redistribute.
- User logs or diagnostics containing passwords, sync tokens, IP addresses, or Discord handles without consent.
- The `dist/` folder, build output, or the share ZIP in the git history.

## A note on the current repository state

The working tree has uncommitted changes for 2.4.1, including the asynchronous F9 server browser, the shared server cache, and the dead-master settings migration. Review `git status`, commit them in logical chunks, and tag the release only after the build and ZIP verification pass.

The GitHub remote is configured. Push `main`, then create the release from the tag. The English README is already the default; keep the language links intact when you make further edits.
