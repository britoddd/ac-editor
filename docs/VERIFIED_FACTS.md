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

## Car sound files (BRITOD-DESK, 2026-11-02)

- AC has a shared `content\sfx\GUIDs.txt` with 3237 lines. Each line is `{guid} bank:/<name>` or `{guid} event:/cars/<car_id>/<event>`.
  It contains 17 entries for `shelby_cobra_427sc`: one `bank:/` line and 16 events
  (`backfire_ext`, `bodywork`, `door`, `engine_ext`, `engine_int`, `gear_ext`, `gear_grind`, `gear_int`, `horn`, `limiter`,
  `skid_ext`, `skid_int`, `tractioncontrol_ext`, `tractioncontrol_int`, `wheel`, `wind`).
- Kunos's `shelby_cobra_427sc\sfx\` holds only `shelby_cobra_427sc.bank`, with no `GUIDs.txt`.
- The mod cars `an_nextgen_chevy` and `ac_friends_ferrari_499p_lmh` each have `sfx\<car_id>.bank` and `sfx\GUIDs.txt`.
  Their `GUIDs.txt` is ASCII with CRLF line endings and includes a `bank:/<car_id>` line. The Ferrari also lists `bank:/showroom` and `bank:/common`.
- `formula_k` does not appear in the shared `GUIDs.txt`. Its SDK `sfx\` folder has `formula_k.bank` and the source `.wav` files, but no `GUIDs.txt`.
- The cobra bank contains no readable `event:/` strings, so the event names come only from `GUIDs.txt`.
- The tutorial car uses the cobra bank, copied as `sfx\acm_tutorial_basic.bank`, plus a `GUIDs.txt` holding the cobra's 17 lines with the car ID changed.
  Not verified until the in-game test: whether AC plays it.

## ui_car.json (BRITOD-DESK, 2026-11-02)

- Kunos's own `sdk\dev\content\cars\formula_k\ui\ui_car.json` is **not** strict JSON. Its `description` string contains raw line breaks.
  Python `json.loads` fails with `Invalid control character`, but it succeeds with `strict=False`.
  Consequence for V-10: a strict "valid JSON" check would reject a Kunos file, so the parser must allow control characters inside strings.
- `ui\badge.png` is 128×128 RGBA. The same folder also has `old_badge.png` (86×55) and `preview_small.png` (240×120).

## Tutorial car in game (BRITOD-DESK, 2026-11-02)

The owner copied `D:\ACModding\acm_tutorial_basic` into `content\cars` and drove it on `algarve_portimao_2023`.
They reported that all checks passed: the car loads, the model and textures are right, the front wheels steer, the car drives forward,
the engine sound plays, the gears and brakes work, and the car stays visible from a far camera.
These lines from `Documents\Assetto Corsa\logs\log.txt` (22:21) support that:

- `Data path: content/cars/acm_tutorial_basic/data/`: AC loads an unpacked `data\` folder when the car has no `data.acd`.
- `USING PHYSICS DRIVEN SUSPENSIONS`, with `USE_ANIMATED_SUSPENSIONS=0` in `car.ini`.
- `INIT LOD:0`, then `LOADING COLLIDER MESH FILE: content/cars/acm_tutorial_basic/collider.kn5`.
- `CREATING proview_nodes.ini`: AC wrote `data\proview_nodes.ini` into the car folder in `content\cars` (201 bytes).
  It went to the real folder, not the UAC VirtualStore.
- `WARNING: flames.ini not found` is only a warning. The car still loads when `flames.ini`, `digital_instruments.ini`, `lights.ini`, `mirrors.ini`,
  `sounds.ini`, `ai.ini`, `damage.ini`, `electronics.ini`, and the other files in `acm_tutorial_basic_FOR_LATER_USE` are missing.
- `FOUND 0 BRAKE DISCS MESHES FOR TYRE:0..3` and `RADIUS FRONT, P:0.275000 , G:0.000000` (graphics radius 0) are expected for a model with no disc or tyre meshes.
- `ERROR: Variable type cannot be found` appears 4 times before the car's animations load. The cause is unknown.
- Leftover working files in the car folder (`.blend`, `.blend1`, `.fbx`, `texture\NULL.dds`) did not stop the car loading.

So the working car keeps these files in `data\`: `aero.ini`, `brakes.ini`, `cameras.ini`, `car.ini`, `colliders.ini`, `driver3d.ini`, `drivetrain.ini`,
`engine.ini`, `lods.ini`, `setup.ini`, `suspensions.ini`, `tyres.ini`, and every `.lut` and `.rto` file these refer to.

## Write access to content\cars (BRITOD-DESK, 2026-11-02)

- `icacls` on `...\assettocorsa\content\cars` shows `BUILTIN\Users:(I)(F)`, which gives normal users inherited full control.
  The game also created a file there. This strongly suggests a normal user process can write to `content\cars`.
  M0 task 7 still needs the agreed test folder to confirm it.
- The owner copied `acm_tutorial_basic` into `content\cars` with Windows Explorer, and Windows did **not** ask for administrator permission (2026-11-02).

## Car folder survey (BRITOD-DESK, 2026-11-02, M0 task 4)

Method: `scratch/survey_cars.py` (not committed) read all 250 entries in `content\cars`, and a second scan read every unpacked `data\lods.ini`.
A car counts as "Kunos" when `content\sfx\GUIDs.txt` has a `bank:/<car_id>` line (189 such lines).
That test misses `ks_ferrari_sf15t` and `ks_mercedes_amg_gt3_2020`, so both are counted as mods below.
Local copies of `acm_tutorial_basic`, `formula_k`, and `shelby_cobra_427sc` are in `fixtures/local/`, which git ignores.

### Where data.acd sits

- All 177 Kunos cars have `data.acd` in the car root. None has a `data\` folder or a `data\data.acd`.
- Of the 73 mod folders that have files: 48 have only a root `data.acd`, 21 have only a `data\` folder, and 4 have both
  (`Fardier_Cugnot`, `gp_2023_rb19`, `jw_porsche_991.2_gt2_rs_us`, `traffic_hino4`). None has `data\data.acd`.
- Two folders are empty: `ks_ferrari_488_challenge_evo` and `ks_ferrari_488_gt3_2020`.
- `shelby_cobra_427sc\data.acd` is 160,844 bytes. A Kunos car's `lods.ini` is inside its `data.acd`, so it cannot be read until we have ACD code (task 5 and M2).
- Answer: `data.acd` sits in the car root. The claim that it lives in `data/data.acd` matched no car here.

### KN5 and lods.ini naming

- The cobra's folder is `shelby_cobra_427sc`, but its main model is `shelby_cobra.kn5`, with LODs `shelby_cobra_LOD_B.kn5`, `_LOD_C`, and `_LOD_D`.
- 137 of 177 Kunos cars and 48 of 73 mods have no `<car_id>.kn5`, even ignoring case. A main KN5 named differently from the folder is normal.
- Every one of the 26 unpacked `lods.ini` files (25 mods and formula_k) has `[COCKPIT_HR]` and `[LOD_0]` with `FILE=`.
  `[LOD_1]` and later appear only on cars with several LODs. LOD files use a `_lod_b` or `_LOD_B` suffix.
  Ten folders share one model name, `az3d_l300.kn5`.
- In two cars, `FILE=` differs in case from the file on disk: formula_k (`formula_K_LOD_D.kn5` versus `formula_k_LOD_D.kn5`) and `indy_scarab_f1` (`Scarab.kn5`).
  Windows ignores case in file names.
- All 177 Kunos cars and all 73 mods with files have `collider.kn5`.

### sfx

- Every car folder with files has a `.bank` named after its ID when case is ignored.
  In 11 Kunos cars and 3 mods only the case differs, for example `alfa_romeo_giulietta_qv` has `alfa_romeo_giulietta_QV.bank`.
- Kunos cars have no `GUIDs.txt` in the car folder, because the shared `content\sfx\GUIDs.txt` lists them.
  72 of the 73 mods have `sfx\GUIDs.txt`. The exception is `ks_ferrari_sf15t`, probably a Kunos car the "Kunos" test missed.

### ui

- `ui_car.json`: 171 of 177 Kunos files parse only with lenient JSON (raw control characters inside strings), and 6 parse strictly.
  Among mods, 70 parse strictly and 3 only leniently. One file (`ks_mercedes_amg_gt3_2020`) starts with a UTF-8 BOM.
- `badge.png` sizes for Kunos cars: 163 are 128×128, 6 are 128×117, 2 are 124×124, 5 are 256×256, and 1 is 1575×1348 (`ks_nissan_skyline_r34`).
  Mods range from 128×128 (56 cars) up to 1705×585. Two mods (`f1_rb_2025`, `f1_redbull_2025`) have a `badge.png` that does not start with the PNG signature.

### What this means for the validator rules (input for task 9)

- V-02 must compare file names without regard to case.
- V-03 would warn on most Kunos cars.
- V-09 must ignore case and accept the shared `content\sfx\GUIDs.txt` for Kunos cars.
- V-10 needs lenient JSON parsing, and "badge must be 128×128" would fire on 14 Kunos cars.

## data.acd format (BRITOD-DESK, 2026-11-02, M0 task 5)

Details, sources, and the full key recipe are in `docs/ACD.md`.

- A key built from public notes (AssettoTools README formulas, with one corrected loop bound) matches acd.py for 14 folder names.
- A `data.acd` packed from `fixtures/local/acm_tutorial_basic/data` by our scratch script is byte-identical to acd.py's (188,756 bytes),
  and unpacking acd.py's file gives back all 40 files byte-for-byte.
- The container layout from AcTools (optional `-1111` header, then name, length, and data entries at 4 bytes per data byte) parses Kunos's `shelby_cobra_427sc/data.acd`.
  File names inside are plain ASCII: 52 entries, including `lods.ini` and `proview_nodes.ini`. Nothing was decrypted.
- 91 Kunos `ks_*` cars and 2 mods start `data.acd` with the `-1111` header. 86 Kunos cars and 50 mods do not.
  Whether the same key works after the header is not tested.
- With the owner's approval, `ks_porsche_911_gt3_r_2016/data.acd` (header `-1111`, then `591346`) was decrypted in memory.
  After the 8 header bytes are skipped, the same key and cipher give 70 entries of clean text.
  Its `lods.ini` has `[COCKPIT_HR]` and `[LOD_0]` to `[LOD_3]`, naming `porsche_911_gt3_r.kn5` and the `_lod_B`, `_lod_C`, `_lod_D` versions.
  None of them matches the folder name, and all exist in the car folder.
