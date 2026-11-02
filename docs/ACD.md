# data.acd and the acd.py test tool

This file covers what we know about `data.acd`, and how to set up and run `acd.py`, the GPL-2.0 tool we use only to check our own output.
Read it before touching ACD work (M0 task 5, M2).

## What data.acd is

These facts are verified unless marked otherwise. The evidence is in `docs/VERIFIED_FACTS.md`.

- `data.acd` is a packed and encrypted copy of a car's `data\` folder: the INI, LUT, and RTO files.
- It sits in the **car root** (`content\cars\<car_id>\data.acd`). All 177 Kunos cars and all 52 mods on BRITOD-DESK that have one keep it there.
- AC loads an unpacked `data\` folder when a car has no `data.acd`. The tutorial car's log says `Data path: content/cars/acm_tutorial_basic/data/`.
- Some mods ship both `data.acd` and `data\` (4 on BRITOD-DESK). Which one AC uses then is **not verified** (rule V-12).
- The key depends on the file's location. acd.py's README says the key comes from the file's base name or its folder name, and AcTools uses the parent folder name for `data` files.
  So a `data.acd` only opens inside a folder named after the car ID it was packed for. If the car ID changes, the file must be repacked.
- Public format notes for the key: **not collected yet** (M0 task 5). Add them here, with links.

## acd.py: what it is and the rules

| Item | Value |
| --- | --- |
| Repo | https://github.com/philippkosarev/acd.py (renamed from `philippkosarev/acd` in 2026; the old URL redirects) |
| Pinned commit | `a575c54fbd51b984fb690081f60903546f93a01e` (v1.1.0, 2026-10-09) |
| License | GPL-2.0 |
| Local copy | `scratch\acd.py\` (gitignored, never committed) |
| Python | 3.8 or newer; BRITOD-DESK has 3.13.4. No dependencies. |

Rules (from `CLAUDE.md`, hard rules 1, 7, and 9):

- acd.py is a **test oracle only**: it produces reference output that we compare our own output against. Never copy its code into this repo, and never ship it or call it from the app.
- Write the project's key code (M2, `AcMod.Core`) from **public format notes** listed in this file, then check it against acd.py's output.
  Claude read acd.py's source on 2026-11-02 for the safety scan. For a stricter clean room, have a fresh session write the key code from the public notes alone.
- Run it only on the owner's own cars, such as `acm_tutorial_basic`. Never use it to open protected or other people's mods.
- Work only in `scratch\` or `fixtures\local\`, never in the AC install or the Mod Workspace.

## Safety scan (2026-11-02, commit a575c54)

Every tracked file was read before download: `acd/acd.py`, `acd/cli.py`, `acd/__init__.py`, `acd/__main__.py`, `test_library.py`, `pyproject.toml`, `.github/workflows/docs.yaml`, and the README.

- `acd/acd.py` imports only `os` and `io`. `cli.py` adds `sys`, `math`, `shutil`, `argparse`, and an optional `readline`.
  There are no network calls, no `subprocess`, no `eval` or `exec`, and no install hooks. `pyproject.toml` lists no runtime dependencies.
- The GitHub workflow only builds the project's documentation site. It does not run on this PC.
- The repo has one contributor, 3 stars, and was created on 2025-12-29. It is small and young, so re-scan it before moving to a newer commit.

Cautions, from reading `cli.py`:

- `unpack` joins file names taken from inside the `.acd` onto the output folder without checking them, so a crafted `.acd` could write outside that folder.
  Only unpack files we packed ourselves, and only into a new, empty folder under `scratch\`.
- `unpack` overwrites existing files without asking.
- `pack` includes only the top-level files of the folder and skips subfolders.
- `view` without an item name asks you to pick one interactively, so pass the item name when a script calls it.

## Setup

Re-create the local copy (for example on a new PC):

```powershell
cd D:\personal\ac-editor\scratch
git clone https://github.com/philippkosarev/acd.py.git acd.py
git -C acd.py checkout a575c54fbd51b984fb690081f60903546f93a01e
```

Self-test, which runs the repo's own tests. The owner ran it on BRITOD-DESK on 2026-11-02 and it printed `acd.py self-test passed`.
Claude Code's auto-mode check blocks Claude from running downloaded code, so the owner runs it with the `!` prefix or adds a permission rule.

```powershell
cd D:\personal\ac-editor\scratch\acd.py
python -c "import test_library as t; [getattr(t, n)() for n in dir(t) if n.startswith('test_')]; print('acd.py self-test passed')"
```

## Commands

Point Python at the local copy rather than installing it with pip. In PowerShell:

```powershell
$env:PYTHONPATH = "D:\personal\ac-editor\scratch\acd.py"
python -m acd view  <car_id>\data.acd car.ini        # print one file from the archive
python -m acd pack   <data folder> <car_id>\data.acd  # folder -> data.acd
python -m acd unpack <car_id>\data.acd <new empty folder>
```

The same in Git Bash: `PYTHONPATH=/d/personal/ac-editor/scratch/acd.py python -m acd ...`

Because the key comes from the folder name, the `data.acd` path must sit inside a folder named exactly after the car ID, for example `scratch\acd_test\acm_tutorial_basic\data.acd`.

## How this project uses it

- **M0 task 5:** collect public format notes, then pack `fixtures\local\acm_tutorial_basic\data` with acd.py and check that our understanding of the notes matches its output.
- **M2 exit gate:** files unpacked by `AcMod.Core` must match acd.py's unpacked output, and a car repacked by `AcMod.Core` must load in AC (checked by the owner).
