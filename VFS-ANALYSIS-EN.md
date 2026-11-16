# GTA V VFS / file safety — technical analysis and current implementation

## What this release actually does

GTACoop uses a narrow **NTFS junction mount** for DLC packs:

- A server add-on is first placed in GTACoop's server-specific download cache.
- For a `DlcPack`, the mount point `mods/update/x64/dlcpacks/gcs_<id>` is pointed at the cached DLC folder through a junction.
- If the junction is created, DLC files are not copied into the game folder again. If Windows/NTFS cannot create or verify the junction, the existing compatibility behaviour copies the files into the mount staging folder.
- The mount target is stored in `InstalledAddon.LinkTarget` and the mount point in `InstalledAddon.InstalledPaths` in the sync state, which acts as the DLC mount manifest.
- On removal only a junction that exactly matches the recorded target is removed. Cache files behind the junction target are never deleted.
- A link or folder whose target does not match, or whose ownership cannot be verified, is left alone.
- During an update only a mount whose previous state record has a junction target that still points at the same target may be replaced. Old copy-based installations are not overwritten automatically.

The junction is created with the Windows `DeviceIoControl` reparse-point API instead of running `cmd.exe` / `mklink`, which avoids shell-escaping problems and lets the mount target be verified separately.

## What this is not

This is **not a general-purpose native VFS hook inside the game process**. It only redirects a DLC folder at the Windows file-system level. OpenIV.asi's `mods` overlay and GTA's own loader consume that file layout.

The following types are not virtualised by this junction mount:

- `OivPackage`: OIV operations are still applied physically to supported `mods` paths.
- `ClientScript`: installed physically into the `scripts/` folder ScriptHookVDotNet expects.
- `AsiPlugin` and `ClientFiles`: installed physically into the locations the loader/game expects.

Virtualising these would require a native ASI file-system hook tested against GTA V versions and loader combinations, or a compatible separate loader. No unverified hook was added to the launcher.

## Security boundaries

1. Server content is first kept under `%LOCALAPPDATA%\GTACoop Remake\Cache\<server>`.
2. The junction target is a separate cache folder; removing the mount never recursively deletes the target.
3. A link is not removed unless the ownership record and the resolved real target match.
4. An existing DLC folder whose ownership cannot be verified is never overwritten.
5. `update.rpf` / `dlclist.xml` editing is independent of this mount; the game only recognises the DLC through the existing OpenIV `mods/update/update.rpf` flow.
6. A Windows smoke test covers the junction behaviour. It is skipped on non-Windows platforms.

## Known limitations and test requirements

- Junction support needs Windows and suitable NTFS/permission conditions. If it fails, the copy fallback takes over.
- Real in-game testing with OpenIV.asi / OpenRPF / RageOpenV on both Legacy and Enhanced GTA V is still required.
- This source package contains no general-purpose native VFS hook; that is a deliberate safety boundary.

## Next stage: virtual/overlay management for all mod types

The target scope is DLC, OIV, ASI and scripts, but these types load differently. OIV is an installation package, not a file loaded at runtime. ASI and script files follow the discovery rules of their own loaders. A single folder-junction approach is therefore not enough for all of them.

The continuation brief is in `DEEPSEEK-SONRAKI-ADIMLAR-VFS-UI-TR.txt` (Turkish). Until a native hook compatible with GTA V's real Rage VFS layer is verified, general-purpose VFS must not be presented as complete. A safe partial overlay is better than an unverified in-game hook.
