# AddonPed+ — standalone addon ped mod

A ped mod that runs **completely independently** of the GTACoop launcher and server.
It has its own executable, its own in-game script and an `.oiv` package that can be
installed and removed with OpenIV. The server-side / launcher Addon Peds system is
not affected by it; it keeps working exactly as before.

## What's in it

| Part | File | Purpose |
| --- | --- | --- |
| Desktop tool | `AddonPed+.exe` | Add, edit and save peds, build the OIV, install / remove in game |
| In-game script | `payload\AddonPedPlus.dll` | Ped selection in game with **F10** |
| Settings | `AddonPedPlus.ini` | `MenuKey` (the key that opens the in-game menu) |
| Package | `AddonPedPlus.oiv` | Install / remove with OpenIV |
| DLC | `gtacoop_peds/dlc.rpf` | Ped models + ped metadata |

## Installation (two ways)

### 1) From the tool

1. Run `AddonPed+.exe`.
2. Add your ped files (.yft/.ydd/.ytd/.ymt) with **Add Peds...** / **Add Streamed Folder...**.
3. Untick the peds you do not want in the list.
4. **Install to Game...** → choose the GTA V folder (the one with `GTA5.exe` or `GTA5_Enhanced.exe`).
5. Press **F10** in game to pick a ped.

Removal: **Uninstall** in the same tool. It deletes only the files it installed and its own
dlclist entry (`gtacoop_peds`); other mods are untouched.

### 2) With OpenIV

1. **Build OIV...** in the tool → save `AddonPedPlus.oiv`.
2. OpenIV → **Tools → Package Installer** → select the `.oiv` file → **Install**.
3. To remove it, **Uninstall** the same package in OpenIV.

## Requirements

- ScriptHookV (matching your game build) + ScriptHookVDotNet 3 (in the `scripts/` folder)
- `mods/update/update.rpf` (a mods folder created with OpenIV)
- .NET 8 Desktop Runtime (x64) for the desktop tool

## In-game usage

The in-game menu is English by default.

- **F10**: open / close the menu (change the key in `AddonPedPlus.ini`)
- **Up / Down**: move the selection
- **Enter**: apply the selected ped
- **Default (normal character)** — first row: return to your normal character
- **Back** — last row: close the menu
- **Esc**: close the menu without changing the ped
- **Backspace** does nothing (it no longer restores the normal ped)
- Diagnostic log: `scripts/GTACoop-pedselector.log`

## Desktop language

The tool's interface is **English by default**. Use the language selector (`Language:` / `Dil:`)
in the button bar to switch to Turkish; the choice is stored in
`%LOCALAPPDATA%\GTACoop Ped Selector\settings.json`.

## Ped list

`scripts/GTACoop-peds.txt` — written by the tool. Format:

```
model|name|type|health|base model|ragdoll|meta verified
superman|Superman|human|200||auto|false
```

The script reads `GTACoop-peds.txt`, `AddonPedPlus-peds.txt` and `peds.txt`, looking in its own
folder, `scripts\`, the game root and the working directory, so a late install does not leave the
menu empty.

## Notes

- The package uses a single DLC folder (`gtacoop_peds`); every new OIV updates that folder, so only
  one dlclist entry remains.
- It can be used alongside an AddonPeds (ap_m.xml) installation; this mod never touches its files.
- The launcher's own Addon Peds tab and server-side ped delivery keep working separately.
