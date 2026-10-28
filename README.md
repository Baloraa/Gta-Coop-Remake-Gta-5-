# GTACoop Remake

**English** | [Türkçe](README.tr.md)

A Windows launcher and server manager for **GTA Coop**, the story-mode multiplayer mod for Grand Theft Auto V.

GTACoop Remake is not a new multiplayer engine. It is the desktop layer around GTA Coop: it installs the in-game client, starts and configures a GTACoop server, publishes a join code, delivers the server's mods to the people who join, and removes those mods when the session ends. The network protocol and the in-game script come from the GPL-3.0 GTA Coop project; the launcher, the sync server, the relay bridge, and the session tooling are this project's work.

GTACoop Remake is for private Story Mode co-op. It does not touch GTA Online, does not bypass anti-cheat, and does not inject into online sessions.

**Current release:** 2.3.5  
**Platform:** Windows 10/11, x64  
**UI:** Turkish and English  
**License:** GNU GPL v3

> Not affiliated with Rockstar Games or Take-Two Interactive. This is not an official product. It requires a legitimate copy of GTA V.

---

## Why this exists

Setting up GTA Coop by hand is possible, but it is easy to get wrong:

- ScriptHookV and ScriptHookVDotNet have to match the installed GTA V build.
- The GTACoOp client DLL and its dependencies have to land in the game's `scripts/` folder.
- The server has its own settings, ports, and mod folder.
- Every player needs the same add-on packs, scripts, and ASI plugins.
- Those files have to be removed afterward so single-player is left clean.
- If the host is behind a router, the guest needs a working path to the host.

Each step is manageable. Together, they are enough to stop a casual session before it starts. GTACoop Remake turns them into one repeatable workflow: choose the game folder, start a server, send a short code, and let the launcher handle the rest.

---

## What it does

### Hosting

- Starts and stops a GTACoop server from the launcher.
- Writes the server configuration and launches `gtaserver.core`.
- Uses the default ports **4499 UDP** for game traffic and **4599 TCP** for synchronization.
- Attempts UPnP port mapping for both ports when the router supports it. If UPnP is disabled or unavailable, the launcher does not fail; it continues with the relay or a direct connection.
- Can open a **bore-compatible TCP relay** for the sync port and the game's UDP bridge, so the host does not have to configure port forwarding or Radmin VPN.
- Supports a custom relay server with `host` or `host:port`. If a custom relay is selected and fails, the launcher does not silently fall back to `bore.pub`; it keeps retrying the selected server.
- Publishes a **short code** such as `Gel.com` through `ntfy.sh`. The code is a handle, not a password; the actual connection data is encrypted.
- Lets the host enable, disable, add, and remove server mods without editing folders by hand.

### Joining

- Adds servers by IP address, short code, or a long `GTC1...` code.
- Shows a **live status column** on the Servers tab: online, offline, or unknown, with latency in milliseconds. The row is green for online, red for offline, and gray while the state is unknown.
- Probes the sync port every five seconds while the Servers tab is visible. Each probe has a 1.4-second timeout. It tries the direct address first and the relay endpoint second.
- Does not run background probes while the tab is hidden.
- Refreshes stale short-code relay information at most every 30 seconds, so a restarted server does not leave a dead relay port in the list.
- Includes a server browser for the public GTACoop master list, LAN discovery, and direct `IP:port` entries.
- Shows server name, player count, mod, password flag, ping, and protocol version. Connecting from the browser does not automatically add the server to the saved list; the **Add to Servers** button does that.

### Mod delivery

- Downloads the server's manifest and verifies each file with SHA-256 before installing it.
- Uses HTTP Range requests for large files: two parallel connections above 4 MB, four above 12 MB. Each part resumes from its own offset.
- Falls back to a single connection when the server does not support Range requests.
- Detects stalled downloads after 45 seconds and retries instead of leaving the launcher hanging.
- Caches downloaded files per server, so joining the same server again does not download everything twice.
- Installs DLC packs with directory junctions instead of copying gigabytes of data.
- Copies scripts into `scripts/` and ASI plugins into the game root.
- Leaves an existing file with the same name alone, even if the server has a different version. The local file wins.
- In **session mode**, removes the server's mods when the game exits. A separate watcher process handles Alt+F4, a game crash, or a reboot; if cleanup is missed, the next launcher start finishes it.
- Moves removed files to `%LOCALAPPDATA%\GTACoopRemake\removed\<date>` instead of deleting them. The three most recent sessions are kept.

### Safety around the game folder

- Edits only `mods/update/update.rpf` when adding DLC packs. The original `update/update.rpf` is never modified.
- Backs up the dlclist before each edit and keeps the last three backups.
- If `mods/update/update.rpf` is close to 4 GB or structurally unhealthy, DLC installation is skipped rather than risking corruption. Scripts and ASI plugins can still be installed.
- Never installs `NativeTrainer.asi`, even though it ships inside the official ScriptHookV archive.
- Does not scan, delete, or overwrite the user's own `scripts/`, `mods/`, or component files.

### Components

The Components tab installs and updates the files the GTA Coop client needs:

- **ScriptHookV** from the official `dev-c.com` release.
- **ScriptHookVDotNet** stable, nightly, or the Enhanced build that supports both Legacy and Enhanced.
- **OpenIV.asi** from the user-supplied file in the share package.
- **RageOpenV** as an open-source alternative to OpenIV.asi.

It backs up every file it replaces under `mods/GTACoop/backups/components/<component>/<date>/`, offers a restore action, and caches the version list for six hours. ScriptHookV and RageOpenV are downloaded on the user's machine and are not placed in the share ZIP.

### In-game features

- **Voice chat** with push-to-talk. The default key is `N`; the in-game menu is under `F9 > Settings > Voice Chat`. A `[MIC]` marker appears next to the speaking player when the server bridge plugin is installed.
- **Host-led story mission co-op.** The host selects a mission and starts it through `Pause > Game > Replay Mission`; clients receive a notification and stay in the same world. Mission objectives and cutscenes belong to the host. No save files are written and clients do not need matching save games.
- **Low-latency profile.** Disables traffic and pedestrians and reduces streamed NPCs to four. It is a mitigation for crowded areas, not a rewrite of the synchronization engine; it does not eliminate every desync.

### Diagnostics and language

- **Copy Diagnostics** collects the launcher version, Windows version, .NET version, GTA folder, server and relay settings, local addresses, and the last 80 log lines. Passwords and sync tokens are excluded.
- The UI and logs are available in Turkish and English. The language can be changed from the top bar and is applied on the next launch.

---

## How it works

The repository is split into small projects with separate responsibilities:

| Project | Target | Role |
|---|---|---|
| `GTACoop.Launcher` | `net8.0-windows` | WinForms UI, server lifecycle, connection selection, relay, components, diagnostics |
| `GTACoOp.Client` | `net48` | In-game ScriptHookVDotNet 3 client; derived from GTA Coop |
| `GTACoop.Sync` | `net8.0` | Shared sync protocol, manifest, add-on and RPF handling, short codes, relay tunnels |
| `GTACoop.SyncServer` | `net8.0` | HTTP file server for the server's mod manifest and files |
| `GTACoop.ServerPlugin` | `net10.0` | Server-side bridge for voice and host-led mission state |
| `GTACoop.Rpf` | `net8.0` | RPF archive and dlclist reader/writer |
| `GTACoop.RpfTool` | `net8.0` | Command-line RPF helper |
| `GTACoop.SyncTool` | `net8.0` | Command-line sync helper and the detached session watcher |

### The join path

1. The host starts the server. The launcher writes the server settings, starts `gtaserver.core`, tries UPnP, opens relay tunnels if needed, and publishes the short code with the current relay endpoints.
2. The guest adds the server by short code, long code, or IP. A short code is resolved through `ntfy.sh`; a long code carries the connection data itself.
3. The guest's launcher chooses a path. On the same machine or the same LAN, it connects directly. Otherwise it tries the relay endpoints; if the sync port is directly reachable, large downloads can go direct while game traffic stays on the relay. If the relay cannot be established, it falls back to a direct address.
4. The guest's launcher downloads the manifest from the sync server, compares hashes with the local cache, downloads what is missing, and applies the add-ons.
5. The launcher starts `PlayGTAV.exe` by default. `GTA5_Enhanced.exe` and `GTA5.exe` can be selected if the installed edition and ScriptHookV/SHVDN build require them.
6. The GTACoOp client joins the server over Lidgren UDP. The server bridge plugin carries voice and mission state.
7. When the game exits, the watcher removes the session's add-ons and restores the dlclist. The download cache stays in place for the next session.

### Direct, UPnP, and relay

The launcher prefers the shortest path:

- Same machine or same LAN: direct connection, no relay.
- Internet host with a reachable sync port: direct downloads, relay only for game traffic if a relay was opened.
- Internet host without a reachable port: relay for sync and game traffic.
- Relay failure: direct address fallback; if neither works, the launcher reports the failure instead of hanging.

UPnP is attempted because a direct connection is faster. It is not treated as a requirement. Many routers ship with UPnP disabled, and some ISPs use carrier-grade NAT; the relay exists for those cases.

The relay is a transport tunnel, not a VPN. It does not add end-to-end encryption. `bore.pub` is a public service; if that is not acceptable, point the launcher at a relay you host yourself.

### Short codes

A short code is normalized to lowercase and limited to 3–64 ASCII letters, digits, dots, dashes, and underscores. The code itself is never sent to `ntfy.sh`:

- The topic is `gtacoop-v1-` plus a base64url-encoded SHA-256 hash of the code and a fixed domain separator.
- The payload is encrypted with AES-GCM using a key derived from the code with SHA-256.
- The payload contains the server name, direct IPv4 addresses, game and sync ports, optional password and sync token, relay endpoints, and a timestamp.
- The host publishes a heartbeat every 60 seconds. A lookup rejects an offer older than five minutes, and the host publishes an offline marker when the server stops.

This keeps the code out of the topic name and makes the payload unreadable without the code. It is not a replacement for trusting the person you send the code to; anyone with the code can decrypt the offer.

### Session cleanup

Session cleanup is deliberately boring:

- A detached `GTACoop.SyncTool session-watch` process watches the game process.
- If the launcher is closed while the game is running, the watcher keeps going.
- If the game crashes or the machine reboots, the next launcher start notices the leftover session and finishes the cleanup.
- If the game never starts, the session expires after 12 minutes.
- Removed files are moved, not deleted, so a mistaken cleanup can be inspected or recovered manually.

---

## Requirements

| Requirement | Notes |
|---|---|
| Windows 10/11 x64 | The launcher is WinForms; the game mods are Windows-only. |
| A legitimate GTA V installation | Legacy and Enhanced layouts are detected. Use the ScriptHookV/SHVDN build that matches your game version. |
| .NET 8 Desktop Runtime | Required by the launcher. Guests need this. |
| .NET 10 runtime | Required by the server core and the server bridge plugin. Hosts need this. |
| ScriptHookV | The official release from `dev-c.com`. The Components tab can install it. |
| ScriptHookVDotNet | Stable, nightly, or Enhanced, depending on the game edition. The Components tab can install it. |
| An ASI loader | ScriptHookV ships with `dinput8.dll`; use a compatible loader. |
| OpenIV.asi or RageOpenV | Needed for the `mods/` folder and DLC packs. The Components tab can install either. |
| Microsoft Visual C++ 2015–2022 x64 | Required by ScriptHookV/SHVDN. |
| Firewall access | Allow the launcher/server on private networks. Direct hosting uses 4499 UDP and 4599 TCP. |

The share ZIP contains the launcher, the sync server, the documentation, the GPL notice, and a user-supplied `OpenIV.asi`. It does **not** contain GTA V, ScriptHookV, ScriptHookVDotNet, RageOpenV, or any copyrighted mod pack.

---

## Installation

### From a release

1. Download `GTACoop-Remake-Share-<version>.zip` from the Releases page.
2. Extract it to a writable folder. Avoid `Program Files`; the launcher prefers to keep per-server data next to the executable and falls back to `%LOCALAPPDATA%` when that folder is read-only.
3. Run `GTACoop.Launcher.exe`.
4. On the **Game** tab, select the folder that contains `GTA5.exe` (or `PlayGTAV.exe` / `GTA5_Enhanced.exe`).
5. Press **Install / Update Client**. This copies the GTACoOp client into `scripts/`; it does not touch the game's original files.
6. Open **Components** and install ScriptHookV and the ScriptHookVDotNet channel that matches your game. Install OpenIV.asi or RageOpenV if you plan to use DLC/mod packs.
7. To host: open **My Server**, start the server, and send the short code to your friend. To join: open **Servers**, add the server or use **Join with Code**, then press **Connect and Play**.

The ZIP includes Turkish and English guides: `OKUBENI-ARKADASLAR.txt`, `READ-ME-EN.txt`, `KODLA-KATIL.txt`, and `JOIN-WITH-CODE-EN.txt`.

### From source

You need Windows, the .NET 8 SDK, the .NET 10 SDK, and PowerShell.

```powershell
git clone <repository-url>
cd GTACoopRemakeV2

# The client build needs the SHVDN3 reference assembly.
# It is intentionally not committed; copy it from a ScriptHookVDotNet release.
# Place it at: vendor\Build\ScriptHookVDotNet3.dll

dotnet restore GTACoopRemake.sln
dotnet build GTACoopRemake.sln -c Release

powershell -ExecutionPolicy Bypass -File build.ps1
```

`build.ps1` produces `dist\GTACoopRemake\`. It builds the GTACoOp client, the server bridge plugin, the launcher, the sync server, and the SyncTool, then copies the vendored runtime files and the license notices.

Useful scripts:

```powershell
# Install/update to %LOCALAPPDATA%\Programs\GTACoop Remake and create shortcuts
powershell -ExecutionPolicy Bypass -File kur.ps1

# Build the friend/share ZIP under dist\
powershell -ExecutionPolicy Bypass -File paylas.ps1
```

Notes for builders:

- `vendor\Build\ScriptHookVDotNet3.dll` is excluded by `.gitignore` because it is a third-party binary. A fresh clone will not build without it.
- `vendor\Distribution` contains the runtime DLLs used by the client and server. `build.ps1` copies them; it does not rebuild them.
- `Directory.Build.props` disables NuGet's vulnerability audit and the build scripts opt out of .NET telemetry, so those steps do not make network calls. Restore still needs access to the configured package sources.
- `dist\`, `bin\`, `obj\`, and `publish\` are ignored. Release ZIPs belong in GitHub Releases, not in the git history.

---

## Data and configuration

| Path | Contents |
|---|---|
| `%LOCALAPPDATA%\GTACoopRemake\settings.json` | Launcher settings, server list, language, game folder |
| `%LOCALAPPDATA%\GTACoopRemake\logs\launcher-YYYYMMDD.log` | Launcher log |
| `%LOCALAPPDATA%\GTACoopRemake\logs\session-watch.log` | Detached cleanup watcher log |
| `%LOCALAPPDATA%\GTACoopRemake\cache` | Shared download/cache data |
| `%LOCALAPPDATA%\GTACoopRemake\removed\<date>` | Files removed after a session; last three sessions kept |
| `Servers\<server name>__<id>` | Per-server mod cache, next to the launcher when writable |
| `<GTA V>\mods\GTACoop\backups` | dlclist and component backups |
| `<GTA V>\scripts` | GTACoOp client and session scripts; cleaned after the session |
| `<GTA V>\mods\update\x64\dlcpacks\gcs_<name>` | DLC junctions created for a session |

The launcher does not store passwords or sync tokens in the diagnostic text. They can still appear in the server configuration files on disk; treat those files as private.

---

## Security and privacy

This is a tool for playing with friends, not a hardened anti-cheat or privacy system. The boundaries matter:

- **No launcher telemetry.** The launcher does not include analytics or crash reporting. The upstream server's public listing and error reporting are disabled by default.
- **External services are feature-specific.** Depending on what you use, the launcher may contact `ntfy.sh` for short codes, `bore.pub` (or your relay) for tunnels, `api.ipify.org` for the public IP, `master.gtacoop.com` and its backup for the server browser, and GitHub/`dev-c.com` for component downloads.
- **Short-code payloads are encrypted.** The topic is a SHA-256 hash of the code, and the payload uses AES-GCM with a key derived from the code. The code itself is not sent to `ntfy.sh`. The service can still see topic metadata and message timing.
- **The relay does not add end-to-end encryption.** It forwards traffic between the host and the guest. Use a relay you trust, or use a direct connection/Radmin/Tailscale if you need a private path.
- **SHA-256 proves integrity, not safety.** A server's mods are executable code. The launcher verifies that a file matches the host's manifest; it cannot verify that the code is harmless. Join servers you trust.
- **GTA Online is out of scope.** ScriptHookV itself does not work in GTA Online and closes the game when entering multiplayer. GTACoop Remake does not attempt to bypass that behavior.
- **Diagnostics exclude secrets.** **Copy Diagnostics** omits passwords and sync tokens, but review the text before posting it publicly; it includes local addresses and log lines.

---

## Known limitations

- Windows only. There is no Linux or macOS build.
- The host's PC and the launcher must stay online for the session. If the relay is used, the guest's launcher must stay open as well.
- Relay latency depends on the relay server and the route between both players. It is usually slower than a direct connection.
- UPnP is not guaranteed. If the router has it disabled, use the relay or forward 4499 UDP and 4599 TCP manually.
- GTA V updates can break ScriptHookV or ScriptHookVDotNet. Install a matching build before blaming the launcher.
- Host-led story missions are not a full mission synchronization engine. Objectives and cutscenes belong to the host; clients help in the shared world.
- The low-latency profile reduces traffic and NPC density. It does not fix every desync.
- The server browser depends on the public GTACoop master list and its backup. If those are unavailable, direct IP and short-code joins still work.
- There is no automated test suite or CI pipeline yet. Testing is manual on Windows, so a bug report with the diagnostic text and the relevant log lines is much easier to act on.
- Server owners are responsible for the mods they distribute. The launcher automates delivery; it does not grant permission to redistribute someone else's work.

---

## What is new in 2.3.5

The main addition is the **Status** column on the Servers tab:

- A short TCP probe of each server's sync port every five seconds while the tab is visible, with a 1.4-second timeout per probe.
- Direct address first, relay endpoint second.
- `Online (ms)`, `Offline`, or `Unknown`, with green, red, and gray rows.
- No background probing when the tab is hidden.
- Short-code records with stale relay data are refreshed quietly at most every 30 seconds.

The release also updates the About tab to credit the first tester, `zaza specialist`.

The 2.3.4 release added UPnP mapping, custom relay servers, and the diagnostic copy. The 2.3.3 release added parallel Range downloads and the resume/stall handling described above. Those features are all present in 2.3.5.

---

## Contributing

Bug reports are most useful when they include:

- Launcher version and Windows version.
- GTA V edition and game build.
- ScriptHookV and ScriptHookVDotNet channel/version.
- Whether the connection was direct, UPnP, or relay.
- The **Copy Diagnostics** text and the relevant lines from `launcher-YYYYMMDD.log`.

Remove passwords, sync tokens, and anything else you do not want public.

For pull requests:

- Keep the project under GPL-3.0 and preserve the upstream attribution.
- Do not add GTA V files, ScriptHookV/SHVDN/RageOpenV binaries, or copyrighted mod packs.
- Test on Windows and describe the manual test path in the PR.
- Follow the existing C#/WinForms style; avoid adding NuGet packages when a vendored dependency already covers the job.
- Update the README and the Turkish/English guides when behavior changes.

The repository has no CI workflow yet. A reproducible test harness for the sync server and addon applier, plus a Windows build workflow, would be the most useful infrastructure contribution.

---

## Credits

**GTACoop Remake:** Turkish Mods  
- YouTube: [@Reelone123](https://www.youtube.com/@Reelone123)
- X: [@imp_loyal38310](https://x.com/imp_loyal38310)
- gta5-mods: [Turkish Mods](https://www.gta5-mods.com/users/Turkish%20Mods)

**First tester:** zaza specialist

**Upstream GTA Coop:** original code © 2016 Guad; modifications © 2016 Bluscream and wolfmitchell; maintained by [oldnapalm](https://github.com/oldnapalm) and contributors. The upstream project is licensed under GPL-3.0.

**Third-party components:** ScriptHookV by Alexander Blade, ScriptHookVDotNet and its nightly/Enhanced builds, RageOpenV, OpenIV, LemonUI, Lidgren.Network, NAudio, protobuf-net, Newtonsoft.Json, MaxMind GeoIP2, BCrypt.Net-Next, .NET, bore by Eric Zhang, and ntfy. Each project keeps its own license; see the references below.

If a credit is missing or wrong, open an issue with the correction.

---

## License

GTACoop Remake is free software under the **GNU General Public License v3.0**. The full text is in [LICENSE.md](LICENSE.md). The upstream GTA Coop code is also GPL-3.0; the launcher and tooling are released under the same license.

Third-party binaries keep their own terms. In particular:

- **ScriptHookV** is proprietary freeware; redistribution is not allowed. The launcher downloads it from the official `dev-c.com` release.
- **RageOpenV** is GPL-3.0, but its upstream asks users not to redistribute built binaries; link to the official release instead.
- **OpenIV.asi** is user-supplied. Check that you have the right to redistribute it before publishing a package or repository that contains it.
- **ScriptHookVDotNet** is zlib-licensed. Other vendored libraries have their own licenses; review them before republishing binaries.

This software comes with no warranty. Use it at your own risk, and keep backups of your game folder.

---

## References

### Upstream and platform

1. [GTA Coop upstream repository](https://github.com/oldnapalm/GTACoop) — original client, server, and game scripts; GPL-3.0.
2. [GTA Coop releases](https://github.com/oldnapalm/GTACoop/releases) and [wiki](https://github.com/oldnapalm/GTACoop/wiki) — upstream builds and setup notes.
3. [ScriptHookV](https://www.dev-c.com/gtav/scripthookv/) — Alexander Blade's official release; the page also documents that it does not work in GTA Online.
4. [ScriptHookVDotNet](https://github.com/scripthookvdotnet/scripthookvdotnet) — stable SHVDN.
5. [ScriptHookVDotNet nightly](https://github.com/scripthookvdotnet/scripthookvdotnet-nightly) — daily builds for newer game versions.
6. [ScriptHookVDotNet Enhanced](https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced) — Legacy + Enhanced support.
7. [RageOpenV](https://github.com/Chiheb-Bacha/RageOpenV) — open-source OpenIV.asi/OpenRPF.asi alternative.
8. [OpenIV](https://openiv.com/) — mods folder and RPF tooling.

### Runtime and libraries

9. [.NET 8 download](https://dotnet.microsoft.com/download/dotnet/8.0) — launcher runtime.
10. [.NET 10 download](https://dotnet.microsoft.com/download/dotnet/10.0) — server core and plugin runtime.
11. [bore](https://github.com/ekzhang/bore) — the TCP tunnel protocol used by the relay.
12. [ntfy](https://ntfy.sh/) — the short-code transport.
13. [Lidgren.Network](https://github.com/lidgren/lidgren-network-gen3) — game networking library used by GTA Coop.
14. [LemonUI](https://github.com/justalemon/LemonUI) — in-game menu library.
15. [NAudio](https://github.com/naudio/NAudio) — voice capture/playback.
16. [protobuf-net](https://github.com/protobuf-net/protobuf-net) — message serialization.
17. [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) — JSON handling.
18. [MaxMind GeoIP2](https://github.com/maxmind/GeoIP2-dotnet) — GeoIP lookups used by the upstream server.
19. [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net) — password hashing used by the upstream server.
20. [GNU GPL v3](https://www.gnu.org/licenses/gpl-3.0.html) — the project license.

### Code references

| Area | File |
|---|---|
| Launcher UI, live status, connection selection | `src/GTACoop.Launcher/ServersTab.cs` |
| Server lifecycle, UPnP, relay orchestration | `src/GTACoop.Launcher/MyServerTab.cs` |
| Relay host/client and UDP bridge | `src/GTACoop.Launcher/RelayService.cs` |
| Short-code publication, lookup, AES-GCM payload | `src/GTACoop.Sync/ShortCodeDirectory.cs` |
| Add-on install, RPF/dlclist handling, cleanup | `src/GTACoop.Sync/AddonApplier.cs` |
| Component downloads and backups | `src/GTACoop.Sync/ComponentManager.cs` |
| HTTP sync server | `src/GTACoop.SyncServer/Program.cs` |
| Voice/mission server bridge | `src/GTACoop.ServerPlugin/Plugin.cs` |
| In-game client and story mission coordination | `src/GTACoOp.Client/Main.cs`, `src/GTACoOp.Client/StoryMissionCoop.cs` |
| Build, install, and share scripts | `build.ps1`, `kur.ps1`, `paylas.ps1` |
