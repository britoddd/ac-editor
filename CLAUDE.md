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

**Milestone 2 (ACD) is in progress.** The plan below was approved on 2026-11-03. **Start at step 1, in a fresh session, under the clean-room rules.**

Milestones must be done in order. A milestone is done only when its exit gate passes. Anything that requires the game must be checked by the owner, so ask for that result and never assume it. Small inspection scripts belong in the gitignored `scratch/` folder.

Done so far. Git history has the details: one commit per step, prefixed `M0` or `M1`.
- **M0 (2026-11-02):** the tutorial car `acm_tutorial_basic` was built and drives in AC. Facts about real AC data are in `docs/VERIFIED_FACTS.md`, the ACD format is in `docs/ACD.md`, and the V-01 to V-15 verdicts are in `docs/REQUIREMENTS.md` ("M0 verdicts"). The license is MIT, and no AcTools code is ported.
- **M1 (2026-11-03):** `src/AcMod.Core` has `Text/TextDocument` (the lossless base) and `Formats/` with `LutFile` (.lut and .rto), `IniFile`, `UiCarJson`, and `GuidsFile`. `FixtureRunnerTests` checks QS-1 and every single-value edit (QS-2) on all 168 fixture files. 223 tests pass, with 0 warnings.

## Milestone 2 plan

Exit gate: files unpacked by Core match acd.py's output, and a car repacked by Core loads in AC (checked by the owner). Make one commit per step.

1. **Key** (`AcdKey`): car ID to key string, following the "Key" table in `docs/ACD.md`. Include the "data*" rule: a file whose name starts with `data` (ignoring case) uses its parent folder's name. Test with the known keys in `docs/ACD.md`.
2. **Read:** parse the container (with or without the `-1111` header) and decrypt it. Check that the result is plausible text, and report an unreadable file instead of throwing (V-13). Container layout: `docs/ACD.md`.
3. **Write:** no header, zero padding, and the key from the target car ID.
4. **Compare with acd.py:** move `scratch/acd_test/acm_tutorial_basic/data.acd` (acd.py's packing of `fixtures/local/acm_tutorial_basic/data`, 188,756 bytes) into `fixtures/local/`.
   Check that Core unpacks it to the original 40 files, and that Core's pack of that `data` folder is byte-identical. The owner also runs one acd.py command to make a synthetic reference file.
5. **Effective data** (TU-1, V-12): read `data.acd` when it exists, otherwise `data\`, and flag a car that has both. AC ignores `data\` while `data.acd` exists.
6. **Pack and verify** (PK-1): pack, unpack again, and compare, all in memory. Writing files waits for M3's guarded file service.
7. **Game check:** repack the tutorial car with one visible change. The owner drives it, and Claude reads the log, as in the V-12 test in `docs/VERIFIED_FACTS.md`.

### Clean-room rules for step 1

The owner chose a fresh session so the key code is written only from public notes.
- Write `AcdKey` from `docs/ACD.md` alone: the "Key" table, the "data*" rule, and the "Known keys" list.
- **Do not open** `scratch/acd.py/` (GPL-2.0 source), `scratch/acd_key_from_notes.py`, or `scratch/acd_container.py`.
  An earlier session that had read acd.py wrote the last two.
- acd.py may only be run as a black box, by the owner, with the `!` prefix (see `docs/ACD.md`).

### Notes for the next agent

- **Backslashes:** the Bash tool collapses `\\` into `\`, so Windows paths inside Python string literals get mangled (`\f` and `\a` become control characters).
  Write doc text with the Edit tool or a `cat <<'EOF'` heredoc, build a backslash with `chr(92)` in Python, and afterwards scan edited docs for bytes `0x00`–`0x1F` other than tab, CR, and LF.
- **Line endings:** git has `core.autocrlf=true` system-wide. `.gitattributes` marks `fixtures/** -text`, so keep every byte-exact test file under `fixtures/`.
- **Running acd.py:** auto mode blocks Claude from running downloaded code. Give the owner a one-line `python -c` command to run with `!`.
- **Screenshots:** the owner saves them in `scratch/` with default Windows names. Read the newest file.
- **Game checks:** read `%USERPROFILE%\Documents\Assetto Corsa\logs\log.txt` right after the owner quits, because the next AC start overwrites it.
  The `POWER CLASS INDEX ... PW:` line shows which data loaded (formula in the V-12 entry of `docs/VERIFIED_FACTS.md`). Versions: AC 1.16.4, CSP 0.3.0-preview634.
- **Installed tutorial car:** `content\cars\acm_tutorial_basic` matches `D:\ACModding\acm_tutorial_basic`, plus AC's own `data\proview_nodes.ini`.
  Write there only with the owner's approval, and remove test files afterwards.
- **`fixtures/local` (gitignored):** `acm_tutorial_basic`, `formula_k`, `shelby_cobra_427sc`, and `ks_porsche_911_gt3_r_2016/data.acd` (the `data.acd` only, with a `-1111` header).
- **Core conventions:** each format has `Parse(bytes)` (never throws) and `ToBytes()`, and `With...()` edits return a new object.
  Add every new format to `FixtureRunnerTests`. Mark a known shortcut with a `ponytail:` comment that names its limit.

## Agreed scope

Version 1 covers workspace and folder management, physics tuning, validation, packaging, learning aids, and Claude Code integration through a local MCP server.

Not in version 1: KN5 creation or editing, shader setup, FBX work, sound creation, tracks, accounts, and Assetto Corsa EVO.

## Hard rules

These apply to every change:

1. **Never bypass protection.** Do not decrypt protected mods, remove protection, or add a way around either. Encrypted or unreadable content opens read-only.
2. **Never add content conversion** from other games.
3. **Keep the app local.** The app itself has no uploads, accounts, analytics, or other network calls with user content. Content Claude Code reads is sent to Anthropic for processing, so CC-10 must explain that and let the owner exclude a car.
4. **Never lose work.** Snapshot before every write. Deletes go to `trash/`. Permanent deletion needs the user to type the car ID.
5. **Stay inside the chosen workspace.** The only exception is the `WS-11` install into `content/cars`. Any other write outside it is a bug.
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

There is nothing to run yet: M1 has no app, CLI, or MCP server. `LutReader/` is the warm-up and is not part of the solution.

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
