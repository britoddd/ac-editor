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

## FBX export format (BRITOD-DESK, 2026-11-02)

- Blender 4.4 and Blender 5.2 both write binary FBX version 7400 (FBX 7.4, the "2014/2015" format).
  Evidence: `FBX_VERSION = 7400` in `scripts\addons_core\io_scene_fbx\fbx_utils.py` for both versions,
  and the default cube exported from each version has the header `Kaydara FBX Binary` followed by version 7400.
  The export script is `scratch/fbx_test/export_cube.py` (not committed). The default exporter has no setting to choose another version.
- ksEditor ships with `sdk\editor\fbxsdk-2014.1.dll`, whose file version is `2014.1 RC (212161)` and whose company is Autodesk.
- ksEditor imports a Blender 4.4 FBX 7400 file. The owner imported test cubes, and all of them showed black because they have no texture.
  ksEditor created `texture\NULL.dds` (136 bytes) next to the imported FBX.

## Blender FBX export settings for ksEditor (BRITOD-DESK, 2026-11-02)

Tested with Blender 4.4 only. Each file was read with `scratch/fbx_test/fbxdump.py`, and the owner imported it in ksEditor.

| Export settings | Object scale in file | `UnitScaleFactor` | Object rotation | ksEditor import |
| --- | --- | --- | --- | --- |
| Default (Apply Scalings = All Local) | 100 | 1 | -90° on X | `ERROR, FOUND SCALING ON NODE : RootNode` |
| Apply Scalings = FBX All | 1 | 100 | -90° on X | No error |
| Apply Scalings = FBX All, Apply Transform on | 1 | 100 | none | No error |

- The guide's advice to leave the button next to Scale off (Apply Unit) does not remove the x100 in Blender 4.4.
  `export_fbx_bin.py:3453` uses 100 whether Apply Unit is on or off, unless the scene's unit scale is not 1.0.
- With FBX All and Apply Transform, an empty placed at Blender `(1, -2, 3)` is written at `(1, 3, 2)` with no rotation.
  So Blender `+X` becomes FBX `+X`, Blender `+Z` becomes FBX `+Y` (up), and Blender `-Y` becomes FBX `+Z`.
- Not verified: how Apply Transform behaves on models more complex than a cube and an empty. Blender labels it experimental.
- Kunos's own 3ds Max export settings are pictured in `sdk\editor\docs\fbx_max_export_settings.png`:
  preset "Assetto corsa", Smoothing Groups on, Triangulate on, scene units converted to Meters, scale factor 1.0, Y-up, Binary, FBX 2012.
  This matches the FBX All result above (metres, Y-up). Blender's closest options are Smoothing = Face and Triangulate Faces.
  We have not set either one, and ksEditor imported the tutorial car without them.
- The tutorial car (9 objects, FBX All, Apply Transform) imports into ksEditor with the expected tree:
  `SCENE_ROOT > MODEL: acm_tutorial_basic.fbx > FBX: acm_tutorial_basic.fbx > WHEEL_LF, WHEEL_LR, WHEEL_RF, WHEEL_RR, body`.
  In ksEditor, `WHEEL_LF` shows Position `0.7575 0.275 1.325` and Scale `1 1 1`, the same values as the FBX in metres.
- ksEditor camera: after import, the camera starts at `Pos: 0; 0; 0`, which is inside this car. The Pos box cannot be edited.
  The owner moved the camera with the keyboard arrow keys.
- ksEditor materials: an imported Blender material gets the shader `ksPerPixel` with an empty `txDiffuse` texture slot (shown as NULL).
  Default values: ksAmbient 0.0509, ksDiffuse 0.8, ksSpecular 0.8, ksSpecularEXP 10, ksEmissive 0 0 0, ksAlphaRef 0, DepthMode eDepthNormal, BlendMode eOpaque.
- The ksEditor File menu has Save KN5 > Car, Car (No Textures), Track, and Car (Experimental).

## KN5 written by ksEditor (BRITOD-DESK, 2026-11-02)

- `acm_tutorial_basic.kn5`, saved with Save KN5 > Car, starts with `sc6969` and version 6, the same as `formula_K.kn5`.
- It embeds each assigned texture byte-for-byte, under its file name (`body.png`, `tyre.png`).
  Evidence: each PNG file's bytes appear unchanged inside the KN5.
  The texture section matches this layout: int32 count, then for each texture an int32 active flag, a length-prefixed name, an int32 size, and the bytes.
  This layout comes from memory of community readers, not a spec. It is confirmed only by the sizes matching the files.
- The file contains the node names `WHEEL_LF`, `WHEEL_LR`, `WHEEL_RF`, `WHEEL_RR`, `VISUAL_WHEEL_*`, and `body`, plus the material names, `ksPerPixel`, and `txDiffuse`.

## AC model axes (BRITOD-DESK, 2026-11-02)

- In `sdk\dev\content\cars\formula_k\formula_K.kn5`, `SUSP_LR` is at `(0.719, 0.268, -1.512)` and `SUSP_RR` is at `(-0.719, 0.268, -1.512)`.
  This suggests that in AC, `+Z` points forward, `+Y` points up, and `+X` points to the car's left.
  Evidence: I searched the file for each length-prefixed node name and read the 4×4 matrix that follows it.
  This assumes a node layout I did not take from any spec, but the matrices read as clean identity rotations, which supports it.
  The file starts with `sc6969` and version 6. A full KN5 parse or the in-game test should confirm this.
- So, in Blender with the settings above, the car's nose points to `-Y` and its left side is `+X`.
