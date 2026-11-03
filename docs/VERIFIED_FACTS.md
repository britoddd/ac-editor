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
- `AcInstall.Find()` (M3 step 6) follows this route and returns `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa`
  on BRITOD-DESK (2026-11-03). The registry's `SteamPath` is lower case, so the code prefers the `libraryfolders.vdf` spelling.

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
- **Write test (M0 task 7, 2026-11-02, approved by the owner):** a normal process created `content\cars\zz_acmod_write_test\write_test.txt`, read it back, and removed the folder.
  The process was not elevated (admin role False, `Medium Mandatory Level`) and was 64-bit (PowerShell 5.1).
  The file was in the real folder, and the UAC VirtualStore copy did not exist. Deleting the folder also worked.
  Conclusion for BRITOD-DESK: a normal user process can create, write, and delete in `content\cars`.
  Not verified: other PCs, Steam libraries on other drives, or non-Steam installs, where folder permissions may differ.

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

## Model encryption detection (BRITOD-DESK, 2026-11-02, M0 task 6)

Sources:
- The [CSP wiki on KN5 encryption](https://github.com/ac-custom-shaders-patch/acc-extension-config/wiki/Misc-%E2%80%93-KN5-Encryption) says encrypted models work only with CSP.
  It says the encrypter writes `<name>_encrypted.kn5` next to the original, and that an optional mode makes the model stop working if `data.acd` changes
  ("This mode protects data from changes, but doesn't encrypt it"). It gives no file signature for encrypted KN5s.
- Searches found no public statement that any specific mod on this PC is encrypted.
  One report says [VRC Formula Alpha 2026](https://traxion.gg/vrc-releases-2026-f1-car-inspired-mod-for-assetto-corsa/) needs CSP.

Survey: `scratch/kn5_survey.py` (not committed) read only the KN5 header, the texture table (names and sizes, seeking past the data),
and the first material's name and shader in every `.kn5` in `content\cars`. Nothing was decrypted or decoded beyond that.

- 1,109 KN5 files. All start with `sc6969`. 86 are version 5 and 1,023 are version 6.
- Version 6 files have one extra int32 after the version: 455 non-zero and 568 zero. The meaning is unknown.
- 1,073 files parse cleanly with the standard layout (AcTools `Kn5Reader`), including every Kunos file.
- 36 files in 10 car folders do **not**. Each has a valid `sc6969` header, version 6, extra value 0, and a texture table that does not make sense
  (the first texture's size runs past the end of the file).
  The folders are: `toyota_fortuner_2021_legender`, `toyota_hilux_revo_2021_mt`, and `vrc_formula_alpha_2023`, `_2024`, `_2025`, and `_2026`, each also in a `_csp` version.
  All 10 have an `extension\` folder. Their `collider.kn5` parses, except in the two 2026 folders.
- No KN5 file name contains `encrypt`.
- Not verified: whether those 36 files are encrypted or are a KN5 variant we don't know.
  An optional owner check: drive `vrc_formula_alpha_2023` with CSP turned off. The CSP wiki says an encrypted model then looks broken.

Proposed V-14 detection, which reads only the header and tables and never decodes model data:
1. Any KN5 whose name contains `_encrypted` gets the flag.
2. Any KN5 with a valid `sc6969` header whose texture or material table doesn't parse with the standard layout gets the flag.
Message: "Model can't be read with the standard layout. It may be encrypted." Never say it *is* encrypted.
Effects: the KN5 is never opened beyond this check. Physics edits need an extra confirmation saying CSP's data-protection mode may stop the model working after `data.acd` changes.
MCP tools return only the status (CC-9).

## AcTools port trial on .NET 10 (BRITOD-DESK, 2026-11-02, M0 task 8)

Source: [gro-ove/actools](https://github.com/gro-ove/actools), Ms-PL, commit `e5e5c94b8dee7511def0b670bfd5dbb233831c11`. Only the needed folders were sparse-cloned into `scratch/actools`.
Each group was compiled as a `net10.0` class library in `scratch/actools_port/` (SDK 10.0.403), with the JetBrains.Annotations NuGet package added only for the trial.
The builds were compile-only. No AcTools code was run, and nothing from AcTools is committed.

| Files | Result on .NET 10 | Verdict |
| --- | --- | --- |
| `AcdFile/AcdReader.cs`, `AcdWriter.cs`, `AcdEntry.cs`, `AcdEncryption.cs` | They do not compile alone. They need `ReadAheadBinaryReader.cs`, which needs `Numerics/*` and a growing set of `Utils/Helpers` files (`StringExtension` pulls in `AlphanumComparatorFast`, `Utf8Checker`, `ArrayExtension` with a `msvcrt.dll` call, and `ObjectExtension`). | Do not port. Write the ~100 lines from `docs/ACD.md` on top of `BinaryReader`. Note that AcTools' `ReadString` is an int32 length plus ASCII, unlike `BinaryReader.ReadString`. |
| `ReadAheadBinaryReader.cs` | A real .NET 5+ conflict: AcTools' `SystemHalf.Half` clashes with the built-in `System.Half`. A two-line fix works (drop the using, call `BitConverter.UInt16BitsToHalf`). It is built for KN5 reading. | Not needed for v1. |
| `AcdFile/Acd.cs` | Uses `FileUtils` (Recycle Bin, `shell32.dll`, its own delete and rename logic). | Do not port. It conflicts with the single guarded file service. |
| `DataFile/IniFile.cs`, `IniFileSection.cs`, `LutDataFile.cs`, `RtoDataFile.cs`, `DataFileBase.cs` | Need `Utils/Helpers`, `AcTools.Windows`, and the `IDataReadWrapper` types. `IniFile.OptionKeepComments` is `false` by default, and its comment mode stores comments per key, not the exact text. `LutDataFile.Stringify()` rewrites every line as `x|y` plus LF and drops comments. | Do not port. They fail QS-1 and QS-2. Use them only as a reference for parsing quirks: AcTools treats `//` as a comment as well as `;` (not verified against AC). |
| `Utils/AcRootFinder.cs` | One error (`AcToolsLogging`). It finds AC from `SourceModInstallPath`'s parent and `config\config.vdf` `BaseInstallFolder_N` entries. This PC has **0** such entries, so it would miss AC in a secondary library. | Do not port. Use the verified `libraryfolders.vdf` method above. |
| `Utils/AcPaths.cs` | Needs the `DataFile` group. | Do not port. |
| `Kn5File/Kn5Basic.cs` | Not compiled. It starts an external FBX converter process, and KN5 and FBX work are out of scope for v1. | Do not port. |
| `Utils/AesUtils.cs`, `Utils/AssemblyResolver.cs` | Not compiled. They are unrelated: Content Manager's string encryption and runtime DLL loading. | Do not port. |

No downloaded file references `AcManager.Internal` or handles KN5 encryption. `Acd.cs` and `AcdEncryption.cs` only expose an empty factory hook for the key.
Conclusion: nothing ports cleanly as-is, because the files share a tangle of helpers. Everything v1 needs is small enough to write fresh from our own notes, so port nothing.
`docs/THIRD_PARTY.md` is therefore not needed yet. Create it the first time any AcTools code is copied.

## Validator rule checks by the owner (2026-11-02, M0 task 9)

- V-01 (folder name of 32 characters or fewer, lowercase, Latin; Warning): the owner reported "confirmed". The evidence they used is not recorded.
  Related: the survey found mod folders with capital letters, for example `F1_mclaren_2024`, `Smarty_Red_Bull_RB16`, and `Fardier_Cugnot`.
  The CSP wiki says encrypted cars need lowercase folder names.
- V-06 (behaviour files `brakes.ini`, `colliders.ini`, `drivetrain.ini`, `setup.ini`; Warning): the owner reported "confirmed". The evidence they used is not recorded.
- V-12 (`data.acd` and `data\` both present): test set up at 23:21. `content\cars\acm_tutorial_basic\data.acd` was added and `data\` was left untouched.
  The `data.acd` holds the installed `data\` with `SCREEN_NAME=ACM Tutorial ACD` and every `power.lut` torque value halved.
  If AC uses `data.acd`, the log's `POWER CLASS INDEX` power should be about half of the `data\` value.
  **Result: when both are present, AC uses `data.acd` and ignores `data\`.** The owner drove three times:

  | Car folder | Log `PW` | Prediction |
  | --- | --- | --- |
  | `data.acd` (halved) + `data\`, 23:24 | 2,245,815 W | 2,246,238 W from the halved torque |
  | `data.acd` (halved) + `data\`, 23:27 | 2,245,815 W | same |
  | `data\` only (test `data.acd` removed), 23:29 | 4,491,630 W | 4,492,477 W from the full torque |

  How the prediction works: power = peak (torque × rpm) × 2π/60 × 3, where `MAX_BOOST=2.0` triples the torque, as `engine.ini`'s own comment says.
  The same formula gives 188,496 W for the original curve, and the 22:21 log said 188,460 W.
  The log line `Data path: content/cars/acm_tutorial_basic/data/` was identical in every run, so it does not show which source was used.
  The owner could not tell the difference by feel (about 3,000 HP versus about 6,000 HP), so check the log.
- AC loaded a `data.acd` packed by `scratch/acd_container.py` (no header, key from public notes) without errors. That is early evidence for the M2 exit gate.
- The test `data.acd` was removed at 23:28, and `data\` was confirmed unchanged against the backup.
- Owner tuning found in the installed car (22:43–22:44): `engine.ini` `LIMITER` 6500 → 17000, and `power.lut` torque about 10× higher, extended to 12000 rpm.
  These edits are only in `content\cars`, not in `D:\ACModding`. A backup is in `scratch/v12_test/installed_data_backup/`.

## Rule-check survey (BRITOD-DESK, 2026-11-02, M0 task 9)

These come from the 250 non-empty folders in `content\cars`.
- Folder names: `ks_lamborghini_huracan_performante` (a Kunos car) is 35 characters, the only one over 32. 9 mod folders use capital letters.
  1 folder has a space (`traffic_jetbus 3_sdd`). The rest use only `[a-z0-9_.-]`.
- Every car has at least one folder under `skins\`.
- Working-file types found inside car folders: 26 `.psd`, 12 `.fbx`, 22 `NULL.dds` (made by ksEditor), 17 `.tmp`, 2 `.bak`, plus the tutorial car's `.blend` and `.blend1`.
- 2026-11-02, at the owner's request: the experimental tune was removed from the installed `acm_tutorial_basic`.
  `engine.ini` and `power.lut` were copied back from `D:\ACModding\acm_tutorial_basic\data`. The installed `data\` now matches the workspace, apart from AC's own `proview_nodes.ini`.
  The tuned files are kept in `scratch/v12_test/installed_data_backup/` (not committed).
- The owner uses Blender 4.4 only for this project. Blender 5.2 was not used to build the tutorial car.
- V-08 test (2026-11-02, approved by the owner): formula_k's `digital_instruments.ini` (104 lines) was copied into the installed tutorial car's `data\`.
  It refers to `STEER_HR`, `Geometry37_SUB2`, and `LED_0`–`LED_4`, none of which exist in the model.
  The owner reports the car **loaded and drove normally**. The cockpit area is just the body block, so nothing could be seen there.
  The drive's `log.txt` was overwritten by a later, short AC start (23:40), which left no crash dump, so there is no log evidence.
  The file was removed afterwards, and the installed `data\` again matches the workspace, apart from `proview_nodes.ini`.
  Conclusion: a `digital_instruments.ini` that refers to missing objects did not crash AC 1.16.4 with CSP 0.3.0-preview634.
- Versions seen in the AC log: AC `1.16.4 (x64)`, Custom Shaders Patch `v0.3.0-preview634`.

## Text file conventions (BRITOD-DESK, 2026-11-02, M1 step 2)

Method: `scratch/encoding_survey.py` (not committed) read 2,616 `.ini`, `.lut`, `.rto`, `.json`, and `.txt` files. Nothing was written. The sources were:
- `fixtures/local`: the tutorial car, formula_k, and the cobra's `ui` folder.
- Every installed car's unpacked `data\`, `ui\`, and `sfx\` (`.bak` files skipped).
- The 70 files inside `ks_porsche_911_gt3_r_2016/data.acd`, decrypted in memory.
- The shared `content\sfx\GUIDs.txt`.

- **Encoding:** every file is ASCII or valid UTF-8. No UTF-16 and no invalid UTF-8 (legacy code page) files were found.
  Non-ASCII UTF-8 appears in 90 Kunos and 25 mod JSON files, and in 5 mod `.ini` files (for example `Fardier_Cugnot/data/ai.ini`).
- **BOM:** a UTF-8 BOM appears in 10 Kunos `dlc_ui_car.json` files, 1 mod `ui_car.json` (`ks_mercedes_amg_gt3_2020`), and 2 mod `.ini` files
  (`top_fuel_dragster/data/aero.ini`, `toyota_fortuner_2021_legender/data/aero.ini`). No `.lut`, `.rto`, or `GUIDs.txt` has one.
- **Line endings:** all of CRLF, LF, mixed, and none (no line break at all) occur.
  - The formula_k SDK files are CRLF, except `escmode.ini`, which is mixed.
  - The Kunos Porsche's files inside `data.acd` are **LF**.
  - Mods use both. Mixed endings appear in 27 mod `.ini`, 4 mod `.lut`, and 13 mod `GUIDs.txt` files.
  - 39 mod `.ini`, 12 `.lut`, and 2 `.rto` files have no line break, and some are empty (for example formula_k `drs.ini` is 0 bytes).
- **Final newline:** files with and without one are both common, even within one car.
- **Tabs** are common in `.ini` (aligned `;` comments) and `ui_car.json`, and occur in 7 mod `.lut` files.
- Extra files seen in `ui\` folders: `dlc_ui_car.json`, `cm_paintshop.json`, `camera_trajectory.json`, and `cm_lods_generation.json`.
  Kunos `ks_abarth500_assetto_corse/ui/` also has `ui_car (Stefano Casillo's conflicted copy 2020-01-17).json`.
- Consequence for M1: the text base must keep the BOM, the line ending of **each** line, and whether there is a final newline.
  It can assume UTF-8, with a byte-preserving fallback for anything that isn't valid UTF-8.

## LUT and RTO line shapes (BRITOD-DESK, 2026-11-02, M1 step 4)

Method: a read-only scan of 1,058 `.lut` and `.rto` files: fixtures, every installed car's `data\`, and the Kunos Porsche's `data.acd` contents.
- 14,946 lines are `key|value`, and 225 more have spaces or tabs around `|` (for example `3.900 | 3.9`).
  There are 12,819 blank lines and 25 `;` comment lines. No line has an inline `;` comment after a value.
- **`.rto` keys are labels, not numbers:** 258 lines look like `10//31|3.10` (gear teeth), and others are `1ST|1.00` and `REVERSE|-5.00`.
  The `//` in these labels is **not** a comment. Treating `//` as a comment, as AcTools does in some parsers, would corrupt them.
- No `//` comment lines, no comma decimals, and no number in a `key|value` line that isn't invariant-culture.
- Rare oddities in mods, all kept as they are by the parser: `0/1.00` (a `/` instead of `|`, in `gp_2023_rb19/data/tct_diff.lut`),
  INI-style content inside a `.lut` (`[VALUES]` and `C=1.27` in `gp_2023_rb19/data/modern_F1.lut`),
  and text after a value: `4000|-900     (engagement point)` in `cky_koenigsegg_jesko/data/kers_torque.lut`.

## INI line shapes (BRITOD-DESK, 2026-11-02, M1 step 5)

Method: a read-only scan of 855 `.ini` files in `data\`: fixtures, every installed car, and the Kunos Porsche's `data.acd` contents.
- 30,422 lines are `KEY=VALUE`, and 9,587 are `KEY=VALUE` followed by a tab-aligned `; comment`.
  5,392 lines are `[SECTION]`, and 49 more have a trailing `; comment` after the `]`. There are 11,311 blank lines and 333 `;` comment lines.
- 315 `//` comment lines exist (for example `/////////` separators), and 17 lines have a `//` comment after a value, such as
  `POSITION=-0.012, -0.01, -0.001  /// (x,y,z) x-/rightt;  z+/forward` (`az3d_traffic_*` `digital_instruments.ini`). These cars are installed and in use.
  How AC itself treats `//` is not verified. The parser ends a value at `;` or `//`, which matches the authors' intent.
- No `#` comments.
- Keys can contain `.` and `:`, for example `Plane.003=0` and `Plate_A:Plate_LODA=0` in `proview_nodes.ini`.
- There are 21 duplicate sections and 229 duplicate keys within one section, for example in the `az3d_traffic_*` `analog_instruments.ini` files.
  Which duplicate AC uses is **not verified**.
- 21 lines are neither a section nor an entry. Examples: prose pasted into `cky_koenigsegg_jesko/data/car.ini`, a lone `3` in `redbull_x1_s2/data/aero.ini`,
  and `[_EXTENSION] RAIN_DEV-1` in `faz_mercedes_g63/data/car.ini`.

## ui_car.json shapes (BRITOD-DESK, 2026-11-02, M1 step 7)

- All 250 installed `ui_car.json` files parse with lenient JSON (raw control characters inside strings allowed).
  None has trailing commas or comments. The only escape sequence used is `\"` (52 times).
- Curve values come in both styles. 6,532 pairs are strings (`["0","56"]`, Kunos style), and the rest are numbers:
  4,823 float/float, 142 int/float, and 58 int/int. `specs` values are mostly strings (1,496), with 100 ints and 3 nulls.
- `AcMod.Core.Formats.UiCarJson` parses all 250 with no errors, round-trips each one byte-for-byte, and reads `/name` from every file
  (a one-off check with `scratch/check_ui`, not committed).
- Not verified: whether AC or Content Manager decode `\n`, `\t`, or `\u` escapes in `ui_car.json`. New strings use them only for control characters.

## GUIDs.txt shapes (BRITOD-DESK, 2026-11-02, M1 step 8)

Method: a read-only scan of 74 GUID files: the shared `content\sfx\GUIDs.txt`, 72 installed cars' `sfx\GUIDs.txt`, and the tutorial car's.
- Every non-blank line is `{guid} kind:/path`, with a lowercase GUID. There are 66 blank lines and no other line shapes.
- Kinds: 8,366 `event`, 1,658 `bus`, 616 `bank`, 336 `vca`, and 1 `snapshot`.
- Car IDs appear only in `bank:/<id>` and `event:/cars/<id>/<event>`. `bus` and `vca` names are generic FMOD mixer groups such as `bus:/grp_turbo` and `bus:/grp_limiter`.
- Some mods list **other cars'** events in their own `GUIDs.txt`. For example, `bugatti_chiron_sport/sfx/GUIDs.txt` has 19 events each for
  `bugatti_bolide`, `aston_db5_vantage_s`, and others. Renaming a car must change only its own ID's `bank` and `event` lines (`GuidsFile.WithCarId`).

## data.acd read checks (BRITOD-DESK, 2026-11-03, M2 step 2)

Method: `scratch/ctrl_survey.py` and `scratch/name_survey.py` (not committed). They read only unpacked files and the unencrypted entry names. Nothing was decrypted or written.

- **Control characters:** none of 1,839 files in every installed car's unpacked `data\` folder and in `fixtures/local` contains a byte 0x00–0x1F other than tab, LF, and CR, or 0x7F.
  All of them are valid UTF-8. `AcdFile` uses this as its "decrypted text looks valid" check (V-13), because a wrong key shifts bytes into that range.
- **Entry names:** the names inside `shelby_cobra_427sc/data.acd` (52) and `ks_porsche_911_gt3_r_2016/data.acd` (70) are plain `[a-z0-9_.-]`, at most 25 characters, with no folders.
- **Unpacked `data\` file names:** no subfolders. 19 files have unusual names, all stray copies: spaces (`tyres - Copy.ini`, `lods - Kopie.ini`, `kers_torque 1.lut`)
  and one non-ASCII name (`bugatti_chiron_sport/data/suspensions - 副本.ini`). So `AcdFile` allows spaces but not non-ASCII or path characters in entry names.
- **Wrong-key detection (M2 step 5):** a wrong key shifts each byte by only -12 to +12, because key strings use only digits and `-`.
  Letters stay printable, so the check relies on line breaks and spaces turning into control bytes. A 3-byte one-line entry (`acd`) passed with a wrong key.
  Real files are multi-line: for acd.py's tutorial pack and the synthetic pack, all 4,000 wrong car IDs tried (2,000 each) failed on the first entry. `AcdPyComparisonTests` keeps that check.

## Core-packed data.acd in game (BRITOD-DESK, 2026-11-03, M2 step 7)

This is the M2 exit gate. `scratch/m2_game_check` is a throwaway console program that uses only `AcMod.Core`. It read the installed car's `data\` (41 files, including AC's `proview_nodes.ini`),
set `SCREEN_NAME=ACM Tutorial M2` with `IniFile`, scaled every `power.lut` torque by 0.75 with `LutFile`, and packed the result with `AcdFile.PackVerified` (189,665 bytes).
With the owner's approval it was copied to `content\cars\acm_tutorial_basic\data.acd` at 00:52, next to the untouched `data\`.

| Run | Log `PW` | Prediction |
| --- | --- | --- |
| `data\` only, 00:50 (baseline) | 188,460 W | 188,496 W from the original curve |
| Core's `data.acd` + `data\`, 00:53 | 141,345 W | 141,372 W from the formula, or 141,345 W after M0's 0.02% offset |

- **Result: AC loads a `data.acd` packed by `AcMod.Core`** (no header, key from public notes, entries in ordinal order). The owner said the car felt weaker than usual.
- The log has no new car-data errors, only the 4 known `ERROR: Variable type cannot be found` lines. `Data path: content/cars/acm_tutorial_basic/data/` appeared even though `data.acd` was used, as in M0.
- The owner did not see "ACM Tutorial M2" anywhere in game. Where `car.ini` `SCREEN_NAME` appears is not verified, so it is not a usable visible check.
- The test `data.acd` was removed afterwards, and a copy is in `scratch/m2_game_check/out/`. The installed `data\` still matches `D:\ACModding\acm_tutorial_basic\data`, plus `proview_nodes.ini`.

## App install in game (BRITOD-DESK, 2026-11-03, M3 exit gate)

The owner moved the hand-copied `content\cars\acm_tutorial_basic` to `D:\ACModding\installed_backup\`, then ran `acmod install acm_tutorial_basic`.
The installed folder held the workspace car's 90 files, byte-identical, plus the app's marker `acmod_install.json` in the car root.

- **Result: AC loads a car installed by the app, and the extra marker file does no harm.** The log has `PW:188460.000000`, the same as the M2 baseline, so the data loaded unchanged.
- The log loads every LUT and `collider.kn5` and ends with `CLEAN EXIT`. No line mentions `acmod_install.json`.
- Car-data errors: only the 4 known `ERROR: Variable type cannot be found` lines. The other errors (force feedback, missing Python apps, `NO STAT FOR` the car and the track, an FMOD collision event) are not about car data.
- AC wrote `CREATING proview_nodes.ini` and created `data\proview_nodes.ini` in the installed car again, as in M0. That file exists only in the installed copy, not in the workspace.
- Before the install, the app refused to replace the hand copy because it had no marker, and nothing in `content\cars` changed.
- Not checked: whether Content Manager shows or treats the marker file in any way.

## Gear, limiter, turbo and tyre fields (BRITOD-DESK, 2026-11-03, M4 step 5)

Method: `scratch/m4_field_survey` (not committed) loaded each of the 252 folders in `content\cars` with `CarData.Load` and read only these fields.
230 cars had readable data (209 from `data.acd`, 21 from `data\`), and 22 had unreadable data. Nothing was written.

- `drivetrain.ini`: all 230 have a numeric `[GEARS] COUNT`, every `GEAR_1` to `GEAR_<COUNT>`, and `FINAL`. `TYPE` is always in `[TRACTION]`.
  Values: `RWD` 193, `AWD2` 16, `FWD` 11, `AWD` 10. What `AWD2` changes compared with `AWD` is not verified.
- `engine.ini`: all 230 have `[ENGINE_DATA] LIMITER` above 0. `[HEADER] POWER_CURVE` is `power.lut` in 226 cars and names another file in 4, so read the name.
- Turbo sections (`[TURBO_n]`) per car: 0 in 120, 1 in 44, 2 in 53, 6 in 1, 8 in 11, and 10 in 1. Every turbo section has `MAX_BOOST`.
  The power formula (V-12 entry) is verified in game for one turbo only. Adding several turbos' boosts is unverified.
- `tyres.ini` `[COMPOUND_DEFAULT] INDEX`: 0 in 109, 1 in 78, 2 in 39, 3 in 4. Reading index 0 as `[FRONT]`/`[REAR]` and n as `[FRONT_n]`/`[REAR_n]` finds a `RADIUS` in 229 of 230 cars.
  That this is the compound AC uses is not verified in game. The radius differs between compounds in 13 cars, and front differs from rear in 133.
- AC's log converts power with 745.7 W per HP: `PW:188460.000000 (252.728622 HP)` (M3 install entry).

## Tuning file fields and comments (BRITOD-DESK, 2026-11-03, M4 step 8)

Method: `scratch/m4_form_survey` (not committed) read `car.ini`, `engine.ini`, `drivetrain.ini`, `brakes.ini`, `suspensions.ini`, `tyres.ini`, and `aero.ini`
in the 230 installed cars with readable data, counting each `[SECTION] KEY` (numbered sections such as `TURBO_0` counted as `TURBO_n`) and whether its line has a `;` or `//` comment.

- Nearly every field appears in 90% or more of the cars that have the file. `tyres.ini` has about 100 fields per compound. So the TU-2 forms list every field rather than a chosen subset.
- Comments usually sit on the `[FRONT]` copy of a field only. `[FRONT] SPRING_RATE` has one in 212 of 229 cars, `[REAR] SPRING_RATE` in 9. The forms reuse the same key's comment from another section as the hint, and say where it came from.
- Some common fields have no comment in any car, for example `tyres.ini` `[FRONT] WIDTH`, `DX0`, and `DY0`. Their hints stay empty until LE-1 (M7) adds sourced explanations.
- Vector values use `,` between parts, for example `car.ini` `[BASIC] GRAPHICS_OFFSET=0,-0.65,0.05`. So `,` is read as a decimal mark only in a field that holds a single number.

## Tuned limiter in game (BRITOD-DESK, 2026-11-03, M4 exit gate)

The owner set `engine.ini` `[ENGINE_DATA] LIMITER` from 6500 to 5500 on the app's Forms tab, saved, installed with the app, and drove the tutorial car.

- **Result: the rev limiter cut at 5500 rpm in game**, and 1st gear at the limiter read about 80 km/h on the speedometer.
- The app's Gears tab predicted 80.8 km/h with the rear radius (0.285 m) and 77.9 km/h with the front (0.275 m). The reading leans towards the rear radius,
  but "about 80" is not precise enough to settle which radius sets the speed on this AWD car. Still unverified.
- The log's `POWER CLASS INDEX` stayed `PW:188460.000000`, the same as with `LIMITER=6500`, so the logged power does not depend on `LIMITER` here.
- The log ends with `CLEAN EXIT`, with only the 4 known `ERROR: Variable type cannot be found` lines.

## Install blocked by Content Manager (BRITOD-DESK, 2026-11-03, TU-9)

- With Content Manager running (started 13:07), the app's install of `acm_tutorial_basic` failed with "access denied". A session from it had just written
  `content\cars\acm_tutorial_basic\data\proview_nodes.ini` (13:07:20), and the install's rename of that folder was refused.
- The failed install left `content\cars` unchanged: no `.acmod-installing` or `.acmod-old` folder and no extra trash copy. The fix is to close Content Manager (and AC) before installing.
- Not verified: whether Content Manager holds the folder only after a session, or whenever it is open.
- Later the same day, installs still failed with AC and Content Manager closed, and no file in the car was held open. **The cause was an Explorer tab showing the car's folder.** Closing it fixed the install (13:11). So any program with the folder open blocks the install's rename.

## Added gear and off-list ratios in game (BRITOD-DESK, 2026-11-03, TU-9 game check)

The app installed the tutorial car with `COUNT=7`, `GEAR_1=2.26`, a new `GEAR_7=0.77`, a `setup.ini` `[GEAR_7]` copied from `[GEAR_6]`, `LIMITER=8500`, and `power.lut` extended to 9500 rpm.
`ratios.rto` (the garage's gear list) offers 2.64, 2.36, 1.94, 1.73, 1.56, 1.50, 1.40, 1.29, 1.23, 1.17, 1.10, 1.05, 1.00, 0.96, 0.92, 0.88.
Evidence: the owner's screenshot of the garage gear page (`scratch/Screenshot 2026-11-03 131644.png`) and `log.txt`.

- **AC accepts an added gear.** The log lists `ADDING GEAR:1` to `ADDING GEAR:7`, and the garage shows seven gear sliders.
- **A ratio that is not in `ratios.rto` is not used as written.** The garage puts it on the list's first option: gear 1 (2.26) and gear 7 (0.77) both show `14/37` (2.64), not the nearest option.
  Gears whose ratio is in the list (1.73, 1.40, 1.17, 1.00, 0.88) show that ratio, and the final drive shows `10/31` (3.10).
  The owner then drove 1st gear to the limiter and reported it reached it, as the garage's 111 km/h for 2.64 predicted (no exact speedometer reading).
- The garage's top speeds match the rear tyre radius (0.285 m) at the 8500 rpm limiter: 111 km/h for 2.64 (rear predicts 111.6, front 107.7), and 332 for 0.88 (rear 334.8).
  Every gear reads about 0.5 to 0.8% below the rear-radius prediction. On this AWD car with different radii, the garage uses the rear radius.
- The log's `PW:325407.593750 (436.378082 HP)` matched the app's estimate from the tuned `power.lut` (325,469 W, peak torque × rpm at 8000 rpm × 3) within 0.02%.
- The log ends with `CLEAN EXIT`.

## Garage ratio lists in setup.ini (BRITOD-DESK, 2026-11-03, TU-9)

Method: `scratch/tu9_setup_survey` (not committed) read `setup.ini` in the 230 installed cars with readable data.

- 78 cars offer a final drive list (`[FINAL_GEAR_RATIO] RATIOS=<file>.rto`), and 55 offer gear lists (`[GEAR_n] RATIOS=`). The rest offer no ratio lists.
- `[GEARS] USE_GEARSET` is missing in 110 cars, 0 in 108, and 1 in 12. With 1 the garage offers preset gearboxes; how that works is not verified.
- One car names a `RATIOS` file it doesn't have.

## Added garage ratio in game (BRITOD-DESK, 2026-11-03, TU-10)

- The app added `19//43|2.26` to the tutorial car's `ratios.rto` and set `GEAR_1=2.26`. The owner reported the garage gear page showed it as expected (gear 1 at 19/43, top speed about 130 km/h at the 8500 rpm limiter).
  So a ratio added to the list is offered and used, and a label of our own (`driver//driven`) is accepted. That AC reads only the number after `|` is consistent with this but not proven separately.
- `GEAR_7=0.77` was still installed, which is not in the list, so the garage keeps replacing it with the first option (TU-9 entry).

## File references in data files (BRITOD-DESK, 2026-11-03, M5 step 2)

Method: `scratch/m5_ref_survey` (not committed) read every `.ini` in the effective data of the 230 installed cars with readable data, and checked whether each value ending in `.lut`, `.rto`, or `.kn5` names a file the car has (ignoring case).

- `lods.ini` names models only in `[LOD_n] FILE`. All 781 such references exist.
- Most missing references are in places AC doesn't read: Content Manager's backup sections (`[__CM_FRONT_ORIGINAL]` and similar), and stray copies such as `tyres - copy.ini`, `tyres1.ini`, and `tyres (… conflicted copy …).ini`.
  The validator skips both.
- In files AC does read, a few dozen references in about 20 cars name missing files, against 7,751 that exist. Examples: `tyres.ini` `WEAR_CURVE`, `DCAMBER_LUT`, and `PERFORMANCE_CURVE`; `aero.ini` `LUT_GH_CL = 1.lut` (2025–26 F1 mods);
  `engine.ini` `[THROTTLE_RESPONSE] LUT`; `electronics.ini` `[ABS] CURVE`; `setup.ini` `[GEAR_n] RATIOS`.
  Whether those cars load or behave normally is not verified. V-07 reports them as errors, per its M0 verdict.
- `engine.ini` `[COAST_CURVE] FILENAME = coast.lut` is missing in many cars, including Kunos cars, and the car still loads (M0), so V-07 skips it.

## KN5 probe in C# (BRITOD-DESK, 2026-11-03, M5 step 4)

- `Kn5Probe.Check` (header, texture table, first material only) over every top-level `.kn5` in `content\cars` gives 1,109 files: 1,073 standard in 248 folders and 36 with unknown tables in 10 folders.
  That is exactly M0's Python survey (`scratch/kn5_survey.py`), so V-14 flags the same files. Run with `scratch/m5_kn5_check` (not committed).

## Validator over the installed cars (BRITOD-DESK, 2026-11-03, M5 exit gate)

Method: `scratch/m5_gate` (not committed) ran `Validator.Run` with the AC folder on all 252 folders in `content\cars`. Read-only. No car made it throw.

| Rule | Findings | Cars | Compared with M0 |
| --- | --- | --- | --- |
| V-01 Warning | 10 | 10 | M0: 9 folders with capitals and 1 with a space |
| V-04 Error | 2 | 2 | `ks_ferrari_488_challenge_evo` and `ks_ferrari_488_gt3_2020`, new since M0 (250 → 252 folders). They also lack skins (V-11) and a sound bank (V-09), so they look like incomplete installs |
| V-05 Error | 0 | 0 | After the fix below |
| V-07 Error | 123 | 22 | As the step 2 survey found |
| V-08 Info | 213 | 213 | Most cars have `digital_instruments.ini` |
| V-09 Error | 4 | 2 | The two Ferrari folders above |
| V-10 Warning / Info | 6 / 29 | 4 / 29 | M0: 2 mod badges not PNG, 14 Kunos badges not 128×128 |
| V-12 Warning | 4 | 4 | `Fardier_Cugnot`, `gp_2023_rb19`, `jw_porsche_991.2_gt2_rs_us`, `traffic_hino4` |
| V-13 Error | 22 | 22 | Same 22 as the step 5 and M5 surveys |
| V-14 Warning | 36 | 10 | Exactly M0 |
| V-15 Info | 77 | 31 | 26 `.psd`, 12 `.fbx`, 17 `.tmp`, 2 `.bak` as in M0; 18 `texture\NULL.dds` (M0's 22 includes 4 deeper `NULL.dds` files) |

- **V-05 correction:** the M0 list names `power.lut`, but 4 installed cars (the VRC Formula Alpha cars among them) name another curve in `engine.ini` `[HEADER] POWER_CURVE`.
  V-05 now requires the file `POWER_CURVE` names, with `power.lut` as the default. With that, no installed car fails V-05.
- 44 cars have at least one error, 22 of them only because their data can't be read (V-13).

## Where a car's ID appears, and file types in car folders (BRITOD-DESK, 2026-11-03, M6 step 1)

Method: `scratch/m6_id_survey` (not committed) searched the text files (`.ini`, `.json`, `.txt`, `.lut`, `.rto`, `.cfg` under 2 MB) of all 252 folders in `content\cars`, and the entries of readable `data.acd` files, for the folder's own name (ignoring case). It also counted file extensions. Read-only.

- The car's own ID appears in `sfx\GUIDs.txt` (72 cars) and `lods.ini` (69 cars, because model file names often contain the ID).
  Rarer: `ui\ui_car.json` (7), Kunos `ui\dlc_ui_car.json` (4), Content Manager's `skins\*\cm_skin_for.json` (4), CSP `ext_config.ini` files in the car and skin folders (12, one F1 mod), `ui\cm_paintshop.json` (1), and single cases in `damage.ini`, `digital_instruments.ini`, `proview_nodes.ini`, and `tyres.ini`.
  What those rarer mentions do is not verified, so Save as new car lists them instead of rewriting them.
- `.knh` files (250) are `driver_base_pos.knh`, the driver's position, which every car has. Keep them.
- Leftover types also found in car folders: `.pdn` (Paint.NET, 6), `.xcf` (GIMP, 3), `.zip` (28) and `.rar` (1), `.backup` (4), and one each of `.old`, `.log`, and `.lnk`. Unclear: `.tuned` (2), `.candidate` (1), `.org` (1).
- The tutorial car, built with ksEditor, holds no editor files besides `.blend`, `.blend1`, and `.fbx`.

## New car ID through a Content Manager ZIP (BRITOD-DESK, 2026-11-03, M6 exit gate)

The owner used the app's Save as new car on `acm_tutorial_basic` (new ID `acm_tutorial_basic_v2`, name "ACM Tutorial Basic v2"), exported it with Pack data into data.acd
(`exports\acm_tutorial_basic_v2_20261103-073914-772.zip`, 20.6 MB), dragged the ZIP onto Content Manager, and drove the car. The owner reported that everything worked.

- **Result: Content Manager installs the app's ZIP, and AC loads the new car ID from the packed `data.acd`.** The log shows `Creating car: acm_tutorial_basic_v2` and
  `PW:325407.593750 (436.378082 HP)`, within 0.02% of the app's estimate (325,469 W). So the `data.acd` packed with the new ID's key loaded.
- Content Manager's install holds `data.acd` and no `data\` folder, the renamed `sfx\acm_tutorial_basic_v2.bank`, and no `acmod_install.json` (only the app's own installs get the marker).
  So a later app install of this car ID is refused until that folder is moved, as WS-11 intends.
- The log ends with `CLEAN EXIT`, with only the 4 known `Variable type cannot be found` lines plus Python app and stats errors that aren't about car data.
- The Content Manager step (drag the ZIP onto its window) from the community tutorial is now confirmed on BRITOD-DESK.
