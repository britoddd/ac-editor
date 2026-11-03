# AC Car Mod Dashboard

A personal Windows app in C# for tuning, validating, and packaging cars for the original Assetto Corsa (AC), with a Claude Code integration.

Full requirements: `docs/REQUIREMENTS.md`. Read it before planning any feature. Requirement IDs such as `TU-4`, `V-09`, and `QS-1` come from that file, so use them in commits and tests.

## About the owner

- The owner is a computer science student and is new to AC modding. Explain AC concepts briefly when they matter to a decision.
- The owner is new to C#, WPF, and .NET, so explain language concepts briefly when they appear.
- The owner builds this project with you. Prefer small, reviewable steps over large generated changes.
- Answer briefly first. If something is ambiguous or wrong, say so directly and ask.
- Never invent AC file fields, tools, or behavior. If a fact is unverified, label it and propose how to test it.

## Current stage

**Milestone 4 is done (2026-11-03). Next: TU-8, then plan Milestone 5** (see "Next" below).

Milestones must be done in order. A milestone is done only when its exit gate passes. Anything that requires the game must be checked by the owner, so ask for that result and never assume it. Small inspection scripts belong in the gitignored `scratch/` folder.

Done so far. Git history has the details: one commit per step, prefixed `M0` to `M4`.
- **M0 (2026-11-02):** the tutorial car `acm_tutorial_basic` was built and drives in AC. Facts about real AC data are in `docs/VERIFIED_FACTS.md`, the ACD format is in `docs/ACD.md`, and the V-01 to V-15 verdicts are in `docs/REQUIREMENTS.md` ("M0 verdicts"). The license is MIT, and no AcTools code is ported.
- **M1 (2026-11-03):** `src/AcMod.Core` has `Text/TextDocument` (the lossless base) and `Formats/` with `LutFile` (.lut and .rto), `IniFile`, `UiCarJson`, and `GuidsFile`. `FixtureRunnerTests` checks QS-1 and every single-value edit (QS-2) on all 168 fixture files. 223 tests pass, with 0 warnings.
- **M2 (2026-11-03):** `src/AcMod.Core/Acd/` has `AcdKey` (written clean-room from `docs/ACD.md`), `AcdFile` (`Parse` reports unreadable files per V-13, `Pack`, and `PackVerified` for PK-1), and `CarData` (effective data per TU-1 and V-12, and `PackFolder`). Core matches acd.py in both directions, and AC loaded a Core-packed `data.acd` (the log's `PW` matched the prediction exactly). 294 tests pass, with 0 warnings.
- **M3 (2026-11-03):** `src/AcMod.Core/Workspaces/` has `Workspace` and `AcInstall`/`Settings`. `Workspace` covers the path guard (`Resolve`), snapshots with rollback, the car list and folder manager, trash with permanent delete, and import from a folder or ZIP. `Workspace.Install.cs` is the only code that writes into AC (WS-11). `AcInstall` finds AC through Steam, and `Settings` lives in `%LOCALAPPDATA%\AcMod\settings.json`. `src/AcMod.Cli` (`acmod`) is a thin CLI over Core. The owner ran the exit gate on `D:\ACModding`: snapshot restore, delete and restore, an install refusal, a 966 MB import peaking at 30.5 MB of memory (QS-7), and an app install that AC loaded with `PW` 188,460 W, the baseline. 409 tests pass, with 0 warnings.
- **M4 (2026-11-03):** `src/AcMod.App` is a plain WPF app (no MVVM package, owner decision): a car list with Open, Compare and Install, and a car window with Forms (TU-2), Power curve (TU-4), Gears (TU-5) and Raw files (TU-3) tabs that share one `CarEdit` session (TU-1, TU-7). Compare covers TU-6. Core added `Workspaces/CarEdit` (unsaved changes, save through `WriteCarFiles`, `data.acd` repacked and verified), `Formats/DataDiff`, and `Tuning/` (`PowerEstimate`, `GearTable`, `TuningForms`). Exit gate: the owner lowered `LIMITER` 6500 → 5500 in the app, installed, and AC cut at 5500 rpm with about 80 km/h in 1st (80.8 predicted). 461 tests pass, with 0 warnings.

## Next: TU-8, then Milestone 5 (Validator)

TU-8 (P2, owner request): plan approved 2026-11-03. One commit per step, prefixed `TU-8 step N`.
Owner decisions: a drag moves every selected point by the same Nm; dragged values are rounded to 0.1 Nm (typed values never); no Ctrl+Z (Discard and the save snapshot cover mistakes).
1. **Core:** `CarEdit.SetLutValues(file, {row → value})` as one change, plus scale-by-% and add-Nm helpers, with QS-2 tests.
2. **Table multi-select:** Ctrl/Shift selection on the Power curve table, with Scale % and Add Nm buttons.
3. **Chart drag:** dots for points, click and Ctrl-click to select (kept in sync with the table), vertical drag with a live label, one change on release.
Check: the owner tries each feature and the Save preview; no drive needed.

After TU-8, plan M5.
For M5, read the M5 row and V-01 to V-15 ("M0 verdicts") in `docs/REQUIREMENTS.md`, propose small steps, and get the owner's approval before coding. Exit gate: each rule has a passing and failing fixture.

Still unverified after M4:
- Which tyre radius sets the speed on an AWD car with different front and rear radii. The M4 gate read about 80 km/h, against 80.8 predicted for the rear and 77.9 for the front.
- That `[COMPOUND_DEFAULT] INDEX` n means `[FRONT_n]`/`[REAR_n]`.
- Adding several turbos' boosts in the power estimate.
- The community estimate rated torque ≈ `power.lut` ÷ 0.85.

### Notes for the next agent

- **Workspace API:** every car write goes through `Workspace.WriteCarFiles(carId, files)`, which snapshots first, writes through temp files, and rolls back on failure (QS-3, QS-8).
  Paths must be in the snapshot scope: `data.acd`, `data\`, `ui\`, `sfx\GUIDs.txt`. Methods return `(Value, Error)` tuples. File system failures throw `IOException` or `UnauthorizedAccessException`.
- **Deleting folders:** use `Workspace.DeleteFolder`, never `Directory.Delete(path, recursive: true)`. Without admin rights the recursive version throws on a junction, and it refuses read-only files.
- **Install marker:** WS-11 writes `acmod_install.json` into each car it installs. A car imported back from `content\cars` carries it, so M6 exports (PK-3) must leave it out.
- **Owner's workspace:** `D:\ACModding`, with the imported `cars\acm_tutorial_basic`. The owner's own folders sit next to it: `acm_tutorial_basic` (loose; its `car.ini` says `SCREEN_NAME=ACM Testing` from a test edit), `acm_tutorial_basic_FOR_LATER_USE`, and `installed_backup\acm_tutorial_basic` (the old hand copy from `content\cars`). Leave those alone.
- **Running the CLI with the owner:** some of the owner's `!` lines arrive as plain messages and don't run. Check for a `<bash-input>` block before assuming a command ran.
  The owner twice edited the wrong copy of `car.ini`, so give full paths and check files on disk.

- **Branches:** work happens on `main`. The owner switched to it during M2 and pushes it themselves, so never push. The local `milestone-0` branch is stale.
- **Backslashes:** the Bash tool collapses `\\` into `\`, so Windows paths inside Python string literals get mangled (`\f` and `\a` become control characters).
  Write doc text with the Edit tool or a `cat <<'EOF'` heredoc, build a backslash with `chr(92)` in Python, and afterwards scan edited docs for bytes `0x00`–`0x1F` other than tab, CR, and LF.
  The same thing breaks C# escapes such as `"\r\n"` when a Python script edits C# source: they turn into real line breaks. Edit `.cs` files with the Edit tool.
- **Line endings:** git has `core.autocrlf=true` system-wide. `.gitattributes` marks `fixtures/** -text`, so keep every byte-exact test file under `fixtures/`.
- **Running acd.py:** auto mode blocks Claude from running downloaded code. Give the owner the one-line `python -c` command from `docs/ACD.md` ("Commands") to run with `!`.
  Never open `scratch/acd.py/` (GPL-2.0 source) or the old `scratch/acd_key_from_notes.py` and `scratch/acd_container.py`, which were written after reading it.
- **Screenshots:** the owner saves them in `scratch/` with default Windows names. Read the newest file.
- **Game checks:** read `%USERPROFILE%\Documents\Assetto Corsa\logs\log.txt` right after the owner quits, because the next AC start overwrites it.
  The `POWER CLASS INDEX ... PW:` line shows which data loaded (formula in the V-12 entry of `docs/VERIFIED_FACTS.md`). Versions: AC 1.16.4, CSP 0.3.0-preview634.
- **App builds:** the owner often has `AcMod.App` open, which locks its output folder. Build-check app code with `dotnet build src\AcMod.App\AcMod.App.csproj -o <job tmp>\appcheck`, and ask the owner to close the app before a full build.
- **Installed tutorial car:** `content\cars\acm_tutorial_basic` is an app install (it has `acmod_install.json`) of `D:\ACModding\cars\acm_tutorial_basic`, plus AC's own `data\proview_nodes.ini`. Since the M4 gate both have `LIMITER=5500`; the 6500 version is in workspace snapshots and the trash.
  `acmod install acm_tutorial_basic` replaces it and moves the old copy to the workspace trash. Install only with the owner's approval. Ask for approval explicitly and wait for a clear yes.
  `scratch/m2_game_check` is a Core-only console program that builds a test `data.acd` (see the M2 entry in `docs/VERIFIED_FACTS.md`).
- **`fixtures/local` (gitignored):** `acm_tutorial_basic`, `formula_k`, `shelby_cobra_427sc`, `ks_porsche_911_gt3_r_2016/data.acd` (the `data.acd` only, with a `-1111` header), and `acdpy_reference/acm_tutorial_basic/data.acd` (acd.py's pack of the tutorial car).
- **Synthetic ACD fixture:** `fixtures/synthetic/acd/acm_acd_synthetic/data.acd` is acd.py's pack of the `data` folder next to it.
  If any file in that folder changes, `AcdPyComparisonTests` fails until the owner repacks it with the command in `docs/ACD.md`. `FixtureRunnerTests` also scans those files.
- **Core conventions:** each format has `Parse(bytes)` (never throws) and `ToBytes()`, and `With...()` edits return a new object.
  Add every new format to `FixtureRunnerTests`. Mark a known shortcut with a `ponytail:` comment that names its limit.
- **ACD API:** `AcdEntry(Name, Data)` is the file type for both `data.acd` entries and `data\` files.
  `CarData.Load` never falls back to `data\` when `data.acd` is unreadable, because AC doesn't. Show its `Error` and open the car read-only (V-13).
  `CarData.PackFolder` and `AcdFile.PackVerified` return `(Bytes, Error)` and write nothing.
  They refuse non-ASCII or path-character file names. One real mod has `data\suspensions - 副本.ini`, so the UI must tell the user to rename or remove such a file.
- **Scratch tools** (gitignored, all read-only): `ctrl_survey.py` and `name_survey.py` survey every installed car's `data\`. `acd_names.py` lists an `.acd`'s unencrypted entry names and decrypts nothing.
  Run Python on game or mod files with `python -I`.

## Agreed scope

Version 1 covers workspace and folder management, physics tuning, validation, packaging, learning aids, and Claude Code integration through a local MCP server.

Not in version 1: KN5 creation or editing, shader setup, FBX work, sound creation, tracks, accounts, and Assetto Corsa EVO.

## Hard rules

These apply to every change:

1. **Never bypass protection.** Do not decrypt protected mods, remove protection, or add a way around either. Encrypted or unreadable content opens read-only.
2. **Never add content conversion** from other games.
3. **Keep the app local.** The app itself has no uploads, accounts, analytics, or other network calls with user content. Content Claude Code reads is sent to Anthropic for processing, so CC-10 must explain that and let the owner exclude a car.
4. **Never lose work.** Snapshot before every write. Deletes go to `trash/`. Permanent deletion needs the user to type the car ID.
5. **Stay inside the chosen workspace.** The only exceptions are the `WS-11` install into `content/cars` and the app's own settings file, `%LOCALAPPDATA%\AcMod\settings.json`, which never holds car data. Any other write outside it is a bug.
6. **Do not commit third-party game content.** Kunos cars and other people's mods stay in the gitignored `fixtures/local/` folder. Commit only synthetic fixtures.
7. **Respect licenses.** You may port AcTools code under Ms-PL. Keep its license in the repo and list ported files in `docs/THIRD_PARTY.md`. Do not copy code from `acd`, because it is GPL-2.0, but you may use it to compare output. Setup, safety notes, and commands are in `docs/ACD.md`. Check any other project's license first and tell the owner.
8. **Never use or decompile `AcManager.Internal.dll`.** AcTools gets its ACD key factory from that closed DLL, so write key code from public format notes.
9. **Do not port code that exists to handle protected content.**

## Claude Code integration

Follow `CC-1` to `CC-11` in `docs/REQUIREMENTS.md`.

- Keep all mod operations in one shared core from Milestone 1. The app, command-line tool, and MCP server must all call it.
- Change tools must use the same snapshot, validation, and workspace checks as the app.
- Change tools need the owner's approval. No tool may empty the trash or delete permanently.
- Do not edit cars in the workspace directly once the MCP tools exist. Use the tools, so changes are logged and can be restored.
- Do not expose encrypted or unreadable content through a tool.
- You cannot operate Blender, ksEditor, or the game in version 1. Give the steps, then wait for the owner's result.

## Stack

- C# on .NET 10 LTS, with nullable reference types enabled
- WPF for the UI, Windows only, targeting x64
- xUnit for tests
- `ModelContextProtocol` NuGet package for the stdio MCP server

Do not add a dependency without saying why it is needed.

Commands, run from the repo root:

```powershell
dotnet restore AcMod.slnx
dotnet build AcMod.slnx      # must finish with 0 warnings; warnings are errors in every project
dotnet test AcMod.slnx
```

The CLI (M3) runs as `src\AcMod.Cli\bin\Debug\net10.0\acmod.exe` after a build; with no arguments it lists its commands. It reads and writes the owner's real settings in `%LOCALAPPDATA%\AcMod\settings.json`, so run it only when the owner asks. The WPF app (M4) is `src\AcMod.App\bin\Debug\net10.0-windows\AcMod.App.exe`; the owner starts it, because Claude can't see its window. There is no MCP server yet. `LutReader/` is the warm-up and is not part of the solution.

## Windows constraints

- AC is under Program Files. Writing to `content\cars` works on BRITOD-DESK (M0 task 7), but other PCs may differ, so check at runtime.
- Keep work in a separate Mod Workspace such as `D:\ACModding`.
- Send every write through one guarded file service.
- Treat the AC install as read-only, except for `WS-11`.
- Use `CultureInfo.InvariantCulture` for AC numbers. Indonesian Windows settings use `,` for decimals.

## Planned structure

```text
src/
  AcMod.Core/   Formats, ACD, validation, calculations, workspace; targets net10.0, no WPF
  AcMod.Cli/    Command-line tool over Core
  AcMod.Mcp/    Stdio MCP server over Core
  AcMod.App/    WPF app over Core; targets net10.0-windows
tests/
  AcMod.Core.Tests/
fixtures/
  synthetic/    Committed test data written for this project
  local/        Gitignored real cars used for local verification
docs/
  REQUIREMENTS.md
  VERIFIED_FACTS.md
  ACD.md        data.acd notes, and how to set up and run the acd.py test tool
  THIRD_PARTY.md
```

The Cli, Mcp, and App projects must not contain mod logic of their own.

## File format rules

- Do not reuse the AcTools INI parser for saving, because comment keeping is off by default.
- Parsers must preserve comments, ordering, spacing, encoding, and line endings.
- An unchanged file must round-trip byte-for-byte. Add a test for every format.
- Editing one value must change only that value.
- Unknown fields must survive a save.
- Check files against real AC data before writing rules about them.

## Definition of done

A task is done when:

- It meets the linked requirement.
- Tests cover the normal case and at least one failure case.
- `dotnet build` has no warnings, and `dotnet test` passes.
- The owner has the exact manual check for anything game-related.
- `docs/VERIFIED_FACTS.md` is updated if the work confirmed or disproved an AC fact.

## How to start a session

1. Read this file and `docs/REQUIREMENTS.md`.
2. Find the current milestone.
3. State the next small step and any question that blocks it.
4. Do the step, test it, and report what changed and what the owner must check.
5. Commit that step on its own before starting the next. Make one commit per mini-step, never several features at once. Put the requirement ID or M0 task in the message, for example `M0 task 4: record lods.ini naming`.
