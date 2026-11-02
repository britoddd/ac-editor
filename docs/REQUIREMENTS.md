# AC Car Mod Dashboard Requirements

Nov 2, 2026 · @Brian

Build a personal Windows app in C# that tunes, validates, and packages original Assetto Corsa cars, with Claude Code able to help, while Blender and ksEditor keep handling the 3D work.

## Decisions

Version 1 is a single-user WPF app on .NET 10 that keeps work in a dedicated Mod Workspace folder.

| Topic | Decision | Reason |
| --- | --- | --- |
| Game | Original Assetto Corsa only | EVO has a different toolchain |
| User | Brian only, with possible open-sourcing later | He is new to AC modding, so the tool also teaches |
| Platform | Windows only | AC runs on Windows here, and WPF only runs on Windows |
| Runtime | Native Windows desktop app | It can reach the default Steam folder and run a local Claude Code server |
| Version 1 focus | Physics tuning, validation, packaging, and folder management | Blender and ksEditor already handle the 3D work |
| 3D model work | Stays in Blender and ksEditor | The dashboard does not replace either tool |
| Encrypted content | Never bypassed | Agreed limit |
| Content conversion | Not included | Agreed limit |
| Builder | Brian, working with Claude Code | Personal project |
| Stack | C#, .NET 10 LTS, WPF, xUnit | Best Windows fit, with reusable KN5 and format code in AcTools |
| Claude Code | Connects through a local MCP server | It can help build mods using the app's safe operations |

**Audience note:** every requirement assumes the user has no prior AC modding experience.

## At a glance

```text
Blender (model, dummies, hierarchy; exports FBX)
  -> ksEditor (shaders and materials; exports KN5 files)
  -> Dashboard in the Mod Workspace (workspace, tuning, validator, packaging)
  -> ZIP -> Content Manager (installs the car)
  -> Assetto Corsa (test drive and logs)
  -> back to the dashboard to adjust and test again
```

The dashboard takes over after ksEditor produces the KN5 files, and the test drive sends changes back to it. Model problems go back to Blender or ksEditor.

## Constraints

C# removes the browser limits, but AcTools is not a drop-in library, and it does not include the code that makes ACD keys.

| Constraint | Effect on the design |
| --- | --- |
| AC is under Program Files, and write access there is unverified | Keep work in a Mod Workspace such as `D:\ACModding`. Test direct install in Milestone 0, with a Content Manager ZIP as the fallback |
| [Each `.acd` file is tied to a name](https://github.com/gro-ove/actools/blob/master/AcTools/AcdFile/AcdEncryption.cs): for `data` files, AcTools uses the parent folder name | Always repack for the target car ID |
| [The AcTools key factory comes from `AcManager.Internal`](https://github.com/gro-ove/actools/blob/master/AcManager/App.xaml.cs), a compiled DLL with no source in the repo | Do not use or decompile that DLL. Write key code from public format notes and verify it against `acd.py` |
| [AcTools targets .NET Framework 4.5.2](https://raw.githubusercontent.com/gro-ove/actools/master/AcTools/AcTools.csproj), and no NuGet package was found | Port only the files needed to .NET 10, and keep the [Ms-PL license](https://raw.githubusercontent.com/gro-ove/actools/master/LICENSE.txt) with them |
| [The AcTools INI parser has comment keeping off by default](https://github.com/gro-ove/actools/blob/master/AcTools/DataFile/IniFile.cs) | Write lossless INI, LUT, and RTO parsers for QS-1 and QS-2 |
| A native app has no browser sandbox | Send all file writes through one guarded file service |
| Windows number settings vary, and Indonesian settings use `,` for decimals | Read and write AC numbers with the invariant culture |
| The app cannot control Blender or ksEditor | The user works in those tools, and the app shows the next step |
| Dummies, shaders, and meshes live inside the KN5 model | Version 1 validates files around the KN5 but does not edit it |

## Functional requirements

Version 1 has six areas, and priority P1 means required for version 1.

### Workspace and folder manager

The workspace layout is `cars/<car_id>/`, `snapshots/`, `trash/`, and `exports/`.

| ID | Requirement | Priority |
| --- | --- | --- |
| WS-1 | Choose a workspace folder, detect the AC install, and remember both | P1 |
| WS-2 | Treat the AC install as read-only, except for WS-11 | P1 |
| WS-3 | List cars with display name, folder ID, size, last changed, and validation status | P1 |
| WS-4 | Import a car from a ZIP or a user-selected folder into `cars/` | P1 |
| WS-5 | Show unused folders: cars not changed for a chosen period, plus snapshots and trash, with their disk size | P1 |
| WS-6 | Delete moves a car to `trash/`; **Empty trash** deletes permanently after the user types the car ID | P1 |
| WS-7 | **Save as new car** copies to a new ID and rewrites the folder, KN5, `lods.ini`, sound bank, GUIDs, and `.acd` references | P1 |
| WS-8 | **Replace previous** runs after a new copy passes validation, then moves the old folder to `trash/` | P1 |
| WS-9 | Editing a car keeps the same folder and creates a snapshot, because a new folder is only needed for a new car ID | P1 |
| WS-10 | Snapshots store text data and UI files only, not KN5 models | P1 |
| WS-11 | Install to AC copies a validated car into `content/cars`, and may only replace a car the app installed; use ZIP export if testing shows this is unreliable | P1 |
| WS-12 | Launch AC or Content Manager from the app | P2 |

### Physics tuning

| ID | Requirement | Priority |
| --- | --- | --- |
| TU-1 | Open data from an unpacked `data/` folder or a readable `data.acd` | P1 |
| TU-2 | Provide guided forms for car, engine, drivetrain, brakes, suspension, tyres, and aero values | P1 |
| TU-3 | Provide a raw editor for every INI and LUT file, so unsupported fields stay reachable | P1 |
| TU-4 | Edit `power.lut` as a table and curve, with estimated power and torque | P1 |
| TU-5 | Calculate speed per gear from redline, gear ratios, final drive, and tyre radius | P1 |
| TU-6 | Compare two snapshots, or two cars, field by field | P1 |
| TU-7 | Mark changes as unsaved and show exactly which files will change before writing | P1 |

### Validator

| ID | Requirement | Priority |
| --- | --- | --- |
| VA-1 | Run the rules below on demand, before packing, and after import | P1 |
| VA-2 | Show each result with severity, file, explanation, and suggested fix | P1 |
| VA-3 | Label each rule as verified, community-reported, or unverified | P1 |

### Packaging

| ID | Requirement | Priority |
| --- | --- | --- |
| PK-1 | Pack `data/` into `data.acd` for the current car ID, then unpack it again and compare before keeping it | P1 |
| PK-2 | Export a ZIP that uses the car ID as its top-level folder, then show the Content Manager install step | P1 |
| PK-3 | Exclude development files such as `.blend`, `.fbx`, and editor persistence files from exports | P1 |
| PK-4 | Block export when validation has errors, with an explicit override | P1 |

### Learning aids

| ID | Requirement | Priority |
| --- | --- | --- |
| LE-1 | Explain fields inline, labelled with where the explanation came from | P1 |
| LE-2 | Provide a first-car checklist covering folders, dummies, FBX export, ksEditor, collider, data, and sound | P1 |
| LE-3 | Show what must happen in Blender or ksEditor when a problem cannot be fixed in the dashboard | P1 |
| LE-4 | Read the latest AC log and highlight likely errors | P2 |

### Claude Code integration

Claude Code connects to one local [MCP](https://code.claude.com/docs/en/mcp) tool server, so it uses the same safe operations as the app. Build CC-1 from Milestone 1; the rest comes in Milestone 8.

| ID | Requirement | Priority |
| --- | --- | --- |
| CC-1 | Keep all mod operations in one shared core used by the app, a command-line tool, and the MCP server | P1 |
| CC-2 | Provide a local stdio MCP server, registered for the project in `.mcp.json` | P1 |
| CC-3 | Read tools: list cars, read car data, run the validator, compare snapshots, calculate gears, and read AC logs | P1 |
| CC-4 | Change tools: preview a tuning change as a diff, apply it, restore a snapshot, pack data, and export a ZIP | P1 |
| CC-5 | Every change tool runs the same snapshot, validation, and workspace checks as the app | P1 |
| CC-6 | Change tools need Brian's approval in Claude Code, and no tool can empty the trash or delete permanently | P1 |
| CC-7 | Project permission rules block Claude Code from directly editing workspace cars, so its changes go through the tools | P1 |
| CC-8 | The app shows Claude Code's changes in an activity log, refreshes open cars, and offers one-click restore | P1 |
| CC-9 | Encrypted or unreadable content stays blocked, and tools return only its status | P1 |
| CC-10 | Explain that content Claude Code reads is sent to Anthropic for processing, and let Brian exclude a car | P1 |
| CC-11 | Include a project guide for the mod loop: read logs, validate, preview, apply, then give Brian the exact in-game check | P2 |

**Limit:** Claude Code does not operate Blender, ksEditor, or the game in version 1. It gives steps, and Brian reports the result.

## Validator rules

The first rule set comes from one community tutorial, so every rule must be tested against real cars in Milestone 0.

| ID | Check | Severity | Evidence |
| --- | --- | --- | --- |
| V-01 | Folder name is 32 characters or fewer, lowercase, and Latin | Warning | [Tutorial](https://assettocorsamods.net/threads/your-first-car-in-assetto-corsa-basic-guide.1019/) |
| V-02 | `lods.ini` points to KN5 files that exist | Error | Tutorial |
| V-03 | Main KN5 name differs from the folder name | Warning | Tutorial says identical, but a [Kunos example differs](https://www.overtake.gg/threads/skinning-assetto-corsa-cars-from-nothing-using-mudbox-and-photoshop.147080/) |
| V-04 | `collider.kn5` exists | Error | Tutorial |
| V-05 | Mandatory data exists: `aero.ini`, `cameras.ini`, `car.ini`, `driver3d.ini`, `engine.ini`, `lods.ini`, `suspensions.ini`, `tyres.ini`, `power.lut` | Error | Tutorial |
| V-06 | Behaviour data exists: `brakes.ini`, `colliders.ini`, `drivetrain.ini`, `setup.ini` | Warning | Tutorial |
| V-07 | LUT and RTO files referenced by data files exist, including wing and tyre tables | Error | Tutorial |
| V-08 | `digital_instruments.ini` refers to objects that may not exist on a new model | Info | Tutorial thread, crash reports |
| V-09 | `sfx/` contains a bank for the car ID, and `GUIDs.txt` refers to the same ID | Error | Tutorial |
| V-10 | `ui/ui_car.json` is valid JSON and the badge is a 128×128 PNG | Warning | Tutorial |
| V-11 | At least one skin folder exists | Warning | Tutorial |
| V-12 | Unpacked data and `data.acd` are both present and differ | Warning | [Forum advice](https://www.gtplanet.net/forum/threads/assetto-corsa-pc-mods-general-discussion.307899/post-13288251) |
| V-13 | `data.acd` cannot be read with the standard method | Error, read-only | Community library behaviour |
| V-14 | The car may use model encryption | Warning, read-only | [Steam post](https://steamcommunity.com/app/244210/discussions/0/3759977302532019084); detection method is an open question |
| V-15 | Development files remain in the car folder | Info | Tutorial |

### M0 verdicts (2026-11-02, BRITOD-DESK)

Each rule was checked against 250 installed cars, the tutorial car in game, and the owner's reports. Evidence is in `docs/VERIFIED_FACTS.md` and `docs/ACD.md`.
"Effective data" means `data.acd` when it exists and `data\` otherwise, because AC ignores `data\` while `data.acd` exists (V-12).

| ID | Verdict | Rule after M0 | Evidence |
| --- | --- | --- | --- |
| V-01 | Changed | Warning unless the folder name is all lowercase, meaning no capital letters, spaces, or non-ASCII characters. The 32-character limit is dropped (owner decision, 2026-11-02) | Owner confirmed the lowercase rule. The Kunos car `ks_lamborghini_huracan_performante` has 35 characters. 9 mod folders use capitals, and 1 has a space. The CSP wiki says encrypted cars need lowercase names. Tools disagree on the ACD key for non-ASCII names. |
| V-02 | Changed | Error. Compare names ignoring case, and read `lods.ini` from the effective data | formula_k's `lods.ini` names `formula_K_LOD_D.kn5`, but the file is `formula_k_LOD_D.kn5`. All 26 readable `lods.ini` files point to existing files when case is ignored. A missing file was not tested in game. |
| V-03 | Rejected | Drop the rule | 137 of 177 Kunos cars and 48 of 73 mods have no `<id>.kn5`. Kunos's own `lods.ini` names `porsche_911_gt3_r.kn5` for `ks_porsche_911_gt3_r_2016`. |
| V-04 | Confirmed | Error | All 177 Kunos cars and 73 mods have `collider.kn5`. The AC log loads it. A missing file was not tested in game. |
| V-05 | Changed | Error. Check the effective data | The tutorial car loads with these files. Kunos `data.acd` files contain them. A missing file was not tested in game. |
| V-06 | Changed | Warning. Check the effective data | Owner confirmed the rule. |
| V-07 | Changed | Error. Ignore case, check the effective data, and skip `engine.ini` `[COAST_CURVE] FILENAME` | The AC log loads every referenced LUT. `coast.lut` is referenced in that obsolete section but missing, and the car still loads. |
| V-08 | Kept, not tested | Info, only when `digital_instruments.ini` exists | The tutorial car loads without the file. The crash claim was not tested. |
| V-09 | Changed | Error. Match the bank name ignoring case. Accept GUIDs from `sfx\GUIDs.txt` or the shared `content\sfx\GUIDs.txt` | Every car's bank matches its ID when case is ignored. Kunos cars have no `GUIDs.txt` of their own. Bank plus `GUIDs.txt` gave the tutorial car sound. Severity was not tested. |
| V-10 | Changed | Warning if `ui_car.json` won't parse leniently (raw line breaks and a BOM allowed) or the badge is missing or not a PNG. Info for a badge that isn't 128×128 | 171 of 177 Kunos `ui_car.json` files fail strict JSON. 14 Kunos badges are not 128×128. 2 mod badges are not PNG. |
| V-11 | Confirmed | Warning | All 250 cars have at least one skin folder. |
| V-12 | Confirmed | Warning: "`data\` is ignored while `data.acd` exists" | In-game test: with both present, the logged power came from `data.acd`. Without it, the power came from `data\`. |
| V-13 | Confirmed | Error, read-only. "Can't read" means the container doesn't parse or the decrypted entries aren't plausible text | A key built from public notes reads the tutorial car and a Kunos `-1111` header file. |
| V-14 | Changed | Warning, read-only. Flag `*_encrypted*.kn5`, or a KN5 whose header is valid but whose texture or material table doesn't parse. The message says "may be encrypted" | 36 KN5 files in 10 mod folders fail the table parse. Every Kunos file passes. Encryption is not proven. |
| V-15 | Confirmed | Info for `*.blend`, `*.blend1`, `*.fbx`, `*.psd`, `*.tmp`, `*.bak`, and `texture\NULL.dds` | The tutorial car loads with `.blend`, `.fbx`, and `NULL.dds` present. Installed cars hold 26 `.psd`, 12 `.fbx`, 22 `NULL.dds`, 17 `.tmp`, and 2 `.bak` files. |

## Quality and safety requirements

The tool must never lose a user's work or change a file it could not read safely.

| ID | Requirement | Test |
| --- | --- | --- |
| QS-1 | Opening and saving an unchanged INI, LUT, or RTO file leaves it byte-identical | Round-trip test on every fixture |
| QS-2 | Editing one value changes only that value, keeping comments, order, spacing, and line endings | Diff test |
| QS-3 | Every write is preceded by a snapshot | Integration test |
| QS-4 | The app itself has no uploads, accounts, or analytics; Claude Code use is covered by CC-10 | Network check during a full workflow |
| QS-5 | Encrypted or unreadable content opens read-only, and the tool offers no bypass | Fixture test |
| QS-6 | The app only writes inside the workspace, except WS-11 installs | Code review and test |
| QS-7 | Large binary files stay out of app memory unless needed | Import a car of at least 500 MB without running out of memory |
| QS-8 | Failed operations leave the original car intact | Interrupt copy and pack tests |
| QS-9 | Fixtures from Kunos cars or other people's mods are never committed | `.gitignore` plus a pre-commit check |
| QS-10 | The core library has no WPF references, so it can be tested without the UI | Core targets net10.0, not net10.0-windows |
| QS-11 | AC numbers are read and written with the invariant culture | Format tests pass under the id-ID culture |

## Build order

Brian builds one car by hand before the app work starts, because that car becomes the first test fixture and teaches the workflow.

| Milestone | Deliverable | Exit gate |
| --- | --- | --- |
| M0: Learn and verify | C# warm-up, hand-built tutorial car, local fixtures, answers to the open questions | The car loads in AC, and each validator rule is marked confirmed or rejected |
| M1: Core formats | Parsers and writers for INI, LUT, RTO, `ui_car.json`, and `GUIDs.txt`; no UI | QS-1 and QS-2 pass on every fixture |
| M2: ACD | Read and write `data.acd` in C#, with key code built from public format notes | Files match `acd.py` output when unpacked, and a repacked car loads in AC |
| M3: Workspace | Workspace setup, car list, import, snapshots, trash, and folder manager | Install to AC refuses unsafe targets, and delete and restore work end to end |
| M4: Tuning | Guided forms, raw editor, power curve, gear calculator, and diff | A tuned car loads in AC with the intended change visible in-game |
| M5: Validator | V-01 to V-15, with fixes and explanations | Each rule has a passing and failing fixture |
| M6: Packaging | New car IDs, repacking, and ZIP export | The ZIP installs through Content Manager and drives in AC |
| M7: Learning aids | First-car checklist and sourced field explanations | Brian can build a second car using only the dashboard and its links |
| M8: Claude Code integration | MCP server, permission rules, activity log, and project guide | Claude Code finds a problem on a test car, previews a fix, applies it after approval, and Brian confirms it in AC; restore also works |

Each milestone is finished only when its gate passes. Anything that needs the game is a manual check by Brian.

## Out of scope and later ideas

Version 1 excludes 3D editing, conversion, and bypassing encryption.

**Not in version 1:** KN5 creation or editing, shader setup, FBX work, sound creation, tracks, multi-user accounts, and anything for Assetto Corsa EVO.

**Never:** decrypting protected mods, removing protection, or converting content from other games.

**Later ideas:** a read-only 3D preview of the KN5, checks for dummy names inside the KN5, and CSP config editing.

## Facts and confidence

The tool and code facts are confirmed from primary sources, but most AC facts are community knowledge and need testing.

| Fact | Confidence | Source |
| --- | --- | --- |
| .NET 10 is the latest LTS, supported until November 14, 2028 | Verified in official policy | [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| WPF runs on modern .NET, on Windows only | Verified in official documentation | [Microsoft WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) |
| The official C# MCP SDK provides a stdio server transport | Verified in SDK documentation | [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/v1/concepts/getting-started.html) |
| Claude Code supports local stdio MCP servers and project `.mcp.json` files | Verified in official documentation | [Claude Code MCP docs](https://code.claude.com/docs/en/mcp) |
| AcTools has open ACD container and KN5 code, targets .NET Framework 4.5.2, and uses Ms-PL | Verified in its files. A compile trial on .NET 10 (M0 task 8) found nothing that ports cleanly; see `docs/VERIFIED_FACTS.md` | [AcTools](https://github.com/gro-ove/actools) |
| The AcTools ACD key factory comes from a closed `AcManager.Internal` DLL | Verified by searching the public source | [App.xaml.cs](https://github.com/gro-ove/actools/blob/master/AcManager/App.xaml.cs) |
| For `data` files, AcTools takes the ACD key ID from the parent folder name | Verified in source; the key derivation is not there | [AcdEncryption.cs](https://github.com/gro-ove/actools/blob/master/AcTools/AcdFile/AcdEncryption.cs) |
| Blender places dummies and hierarchy; ksEditor sets shaders and exports KN5 | Community, widely used | [First car guide](https://assettocorsamods.net/threads/your-first-car-in-assetto-corsa-basic-guide.1019/) |
| FBX must be 2014/2015 format or older | Community, from 2017, may be outdated | First car guide |
| `data.acd` takes priority over unpacked data | Verified in game on BRITOD-DESK (V-12 test, 2026-11-02; see `docs/VERIFIED_FACTS.md`) | [GTPlanet](https://www.gtplanet.net/forum/threads/assetto-corsa-pc-mods-general-discussion.307899/post-13288251) |
| `data.acd` sits in the car root | Verified on BRITOD-DESK: all 177 Kunos cars and all 52 mods that have one keep it in the root; none uses `data/data.acd` | `docs/VERIFIED_FACTS.md` |
| `power.lut` is `rpm\|torque`, with torque in Nm at the wheels | Community | [GTPlanet](https://www.gtplanet.net/forum/threads/assetto-corsa-pc-mods-general-discussion.307899/post-12788964) |
| Rated torque is about `power.lut` torque ÷ 0.85 without a turbo | Community estimate, show as an estimate | [GTPlanet](https://www.gtplanet.net/forum/threads/assetto-corsa-pc-mods-general-discussion.307899/post-12788964) |
| Content Manager accepts a dropped ZIP | Community tutorial | [DEV Community](https://dev.to/kazuslu/how-to-download-and-install-assetto-corsa-car-mods-1hcd) |
| Changing `data.acd` can break heavily encrypted models | Single forum post | [Steam](https://steamcommunity.com/app/244210/discussions/0/3759977302532019084) |

**Rule for building:** a community fact can guide a feature, but only a test against a real car can confirm it.

## Open questions

These questions must be answered in Milestone 0, before Milestone 1 starts.

- [x] Can the app write to AC's Steam folder under Program Files, or is a Content Manager ZIP the only reliable install route? **Answered 2026-11-02 for BRITOD-DESK:** yes. Steam gives `BUILTIN\Users` full control of `content\cars`, and a non-elevated test process created, wrote, and deleted a folder there with no VirtualStore redirect. Other PCs may differ, so `WS-11` should still check write access at runtime and offer the Content Manager ZIP as the fallback.
- [x] Which public format notes fully describe the `.acd` key, and does a key built from them match `acd.py`? **Answered 2026-11-02:** the AssettoTools README formulas, with aluigi's notes and the AcTools layout. One loop bound in the notes needed correcting. The key matches acd.py for 14 names, and our pack of the tutorial car is byte-identical. A Kunos file with the `-1111` header (`ks_porsche_911_gt3_r_2016`) also decrypts with the same key once the header is skipped (see `docs/ACD.md`).
- [x] Where does `data.acd` sit in a real car? **Answered 2026-11-02:** in the car root (see `docs/VERIFIED_FACTS.md`, car folder survey).
- [x] How can model encryption be detected without trying to bypass it? **Answered 2026-11-02:** not with certainty. The conservative signal flags a KN5 named `*_encrypted*`, or one whose texture or material table doesn't parse with the standard layout (36 files in 10 mod folders on BRITOD-DESK, none from Kunos). The warning says "may be encrypted". See `docs/VERIFIED_FACTS.md`.
- [x] Is FBX 2014/2015 still the required export format with current Blender and ksEditor? **Answered 2026-11-02:** Blender's exporter always writes FBX 7.4 (the 2014/2015 format), and ksEditor (FBX SDK 2014.1) imports it from Blender 4.4. Use Apply Scalings = FBX All and Apply Transform. The default export triggers `FOUND SCALING ON NODE`. Blender 5.2 also writes 7.4, but its import into ksEditor was not tested. The owner uses 4.4 only. See `docs/VERIFIED_FACTS.md`.
- [x] Which AcTools files port cleanly to .NET 10, and are they worth keeping? **Answered 2026-11-02:** none port cleanly as-is, because the ACD, data-file, and path files all pull in a tangle of `Utils/Helpers` code. They aren't worth keeping either: the data-file writers drop comments (failing QS-1 and QS-2), `Acd.cs` bypasses our guarded file service, and `AcRootFinder` misses secondary Steam libraries. Write these parts fresh from `docs/ACD.md` and the verified facts. See `docs/VERIFIED_FACTS.md`.
- [x] If the project is open-sourced, which license should it use? **Answered 2026-11-02:** MIT (`LICENSE`), chosen by the owner. No AcTools code is ported, and acd.py stays a test tool that is never distributed. Ported AcTools code keeps its Ms-PL terms. Keep [`acd`, which is GPL-2.0](https://github.com/philippkosarev/acd), as a test tool only. This is not legal advice.

## Sources

- [Your FIRST car in Assetto Corsa: Basic Guide](https://assettocorsamods.net/threads/your-first-car-in-assetto-corsa-basic-guide.1019/)
- [acd library](https://github.com/philippkosarev/acd)
- [AcTools mirror with license note](https://pkg.go.dev/github.com/nmkr/assetto-server-manager/pkg/acd)
- [Steam: encrypted data discussion](https://steamcommunity.com/app/244210/discussions/0/3759977302532019084)
- [GTPlanet: data and power.lut discussions](https://www.gtplanet.net/forum/threads/assetto-corsa-pc-mods-general-discussion.307899/post-13288251)
- [Overtake: Kunos car file example](https://www.overtake.gg/threads/skinning-assetto-corsa-cars-from-nothing-using-mudbox-and-photoshop.147080/)
- [DEV Community: installing car mods](https://dev.to/kazuslu/how-to-download-and-install-assetto-corsa-car-mods-1hcd)
- [XGamingServer: installing car mods](https://xgamingserver.com/docs/assetto-corsa/install-mods)
- [AcTools repository](https://github.com/gro-ove/actools), with its [project file](https://raw.githubusercontent.com/gro-ove/actools/master/AcTools/AcTools.csproj) and [license](https://raw.githubusercontent.com/gro-ove/actools/master/LICENSE.txt)
- [Claude Code: MCP](https://code.claude.com/docs/en/mcp)
- [MCP C# SDK: getting started](https://csharp.sdk.modelcontextprotocol.io/v1/concepts/getting-started.html)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Microsoft: WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
