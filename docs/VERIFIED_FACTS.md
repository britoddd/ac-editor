# Verified facts

AC facts checked against real data. Each entry says what was checked, how, on which PC, and when.
Anything not listed here is unverified. Do not write rules or code that depend on it.

## Install location (BRITOD-DESK, 2026-11-02)

- AC is installed at `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa`.
  Evidence: the folder exists, and the owner confirmed this PC is the modding PC.
  Note: it is under `Program Files (x86)`, not `Program Files`.
- AC's Steam app ID is `244210`.
  Evidence: `steamapps\appmanifest_244210.acf` contains `"name" "Assetto Corsa"` and `"installdir" "assettocorsa"`.

## Finding AC automatically (BRITOD-DESK, 2026-11-02)

Checked so the app can find AC on its first run on another PC. On this PC, these steps lead to the correct folder:

1. Registry `HKCU\Software\Valve\Steam`, value `SteamPath` = `c:/program files (x86)/steam`.
   It uses forward slashes and lower case. `HKLM\SOFTWARE\WOW6432Node\Valve\Steam`, value
   `InstallPath` = `C:\Program Files (x86)\Steam`, also exists.
2. `<SteamPath>\steamapps\libraryfolders.vdf` lists every Steam library. There are 5 here, on C:, E:, G:, O:, and D:.
   Each library has an `apps` block keyed by app ID. `244210` is listed only under `C:\Program Files (x86)\Steam`.
3. `<library>\steamapps\appmanifest_244210.acf` gives `installdir` = `assettocorsa`.
4. AC folder = `<library>\steamapps\common\<installdir>`.

- The uninstall key `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 244210`
  does **not** exist on this PC. Do not rely on it.
- Not verified: Steam in a non-default folder, AC in a secondary library, or non-Steam installs.
  Check these on the second PC.

## Contents of the AC folder (BRITOD-DESK, 2026-11-02)

These exist in the AC folder above. They are candidates for checking a folder the user picks by hand:

- `acs.exe` and `AssettoCorsa.exe`
- `content\cars` (250 entries)
- `sdk\dev\content\cars\formula_k`

## Custom Shaders Patch (BRITOD-DESK, 2026-11-02)

- `extension\` and `dwrite.dll` exist in the AC folder.
- `extension\installed.log` says the patch was installed by Content Manager from a custom ZIP file.
  It shows `version: 0` and `build: 0`, so CSP is installed but its version is unknown.
- `extension\config\cars` exists. How CSP uses it is not verified.
