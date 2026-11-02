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

Start at Milestone 0 in `docs/REQUIREMENTS.md`. Do not write app code until Milestone 0 is complete. Small inspection scripts are fine in a gitignored `scratch/` folder.

Milestones must be done in order. A milestone is done only when its exit gate passes. Anything that requires the game must be checked by the owner, so ask for that result and never assume it.

## Milestone 0 tasks

Work through these in order:

1. Confirm the owner finished the `LutReader` warm-up. Ask which PC will be used for modding and confirm the AC path. AC is in Steam's default folder under Program Files. **Done 2026-11-02:** the modding PC is BRITOD-DESK, and AC is under `Program Files (x86)`. See `docs/VERIFIED_FACTS.md`.
2. Create `.gitignore`, `fixtures/synthetic/`, `fixtures/local/`, `scratch/`, and `docs/VERIFIED_FACTS.md`. Do not scaffold the app yet. **Done 2026-11-02:** the repo root is `D:\personal\ac-editor`, and `LutReader/` is a subfolder.
3. Coach the owner through building `acm_tutorial_basic` from the [first car guide](https://assettocorsamods.net/threads/your-first-car-in-assetto-corsa-basic-guide.1019/). One step at a time, tell the owner what to do in Blender and ksEditor, then wait for the result. **Done 2026-11-02:** the car loads and drives in AC; the working folder is `D:\ACModdingcm_tutorial_basic`. See `docs/VERIFIED_FACTS.md`.
4. Inspect the tutorial car, `sdk/dev/content/cars/formula_k`, and one Kunos car on the owner's PC. Record what really exists in `docs/VERIFIED_FACTS.md`, including where `data.acd` sits, how `lods.ini` names models, and how `sfx` and `ui` files are structured.
5. Find public format notes for the `.acd` key, then confirm your understanding by comparing against `acd.py` on the owner's own car.
6. Find a way to identify model encryption without bypassing it. If no reliable method exists, record that and propose a conservative warning.
7. Test whether a normal user process can write to AC's `content/cars`, using one small test folder that the owner approves. Remove it afterward.
8. Check which AcTools files port cleanly to .NET 10 by trying them in `scratch/`.
9. Mark every validator rule V-01 to V-15 as confirmed, changed, or rejected, with the evidence.
10. Close each open question in `docs/REQUIREMENTS.md`, then propose the Milestone 1 plan and wait for approval.

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
7. **Respect licenses.** You may port AcTools code under Ms-PL. Keep its license in the repo and list ported files in `docs/THIRD_PARTY.md`. Do not copy code from `acd`, because it is GPL-2.0, but you may use it to compare output. Check any other project's license first and tell the owner.
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

After the project is scaffolded, add the real restore, build, test, and run commands here.

## Windows constraints

- AC is under Program Files, and write access there is unverified. Never assume it works.
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
