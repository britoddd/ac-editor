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
- Format notes for the container, the key, and the cipher are below. They were checked against acd.py on 2026-11-02.

## Format notes

### Sources

| Source | License | What it gives |
| --- | --- | --- |
| [aluigi on ZenHAX, 2014](http://zenhax.com/viewtopic.php@t=90.html) | Forum posts | An `.acd` is a sequence of files. Each byte is stored in a 32-bit field. The key comes from the folder name through "8 small algorithms", printed as `%d-%d-%d-%d-%d-%d-%d-%d`. The cipher is "a simple rot". The file must stay in its folder. |
| [AssettoTools README](https://github.com/0danny/AssettoTools) | None stated (archived). We used only its text and formula images, not its code. | Formulas for all 8 key values, plus a worked example: `ks_audi_sport_quattro` gives a first value of 230 |
| [bovis/acd_extractor README](https://github.com/bovis/acd_extractor/blob/main/README.md) | None visible | The key comes from the car's directory name, and unpacking from a renamed folder gives garbage |
| [AcTools `AcdFile`](https://github.com/gro-ove/actools/tree/master/AcTools/AcdFile) | Ms-PL (allowed to port) | The container layout. The key source is the parent folder name for files whose name starts with `data`, otherwise the file's own name. Its key factory lives in the closed `AcManager.Internal`, which we never use. |

Excluded: [Kn5Decrypt](https://github.com/SeizureSaladd/Kn5Decrypt), a tool for protected KN5 files (hard rules 1 and 9).

### Container layout

All integers are 32-bit little-endian.

1. Optional header: `-1111`, then one more int32. The cobra's file has no header. See "Header survey" below.
2. Then entries, repeated until the end of the file:
   - an int32 name length, then the name in ASCII. Names are **not encrypted**, so the file list can be read without the key.
   - an int32 data length `N`.
   - `N × 4` bytes: each data byte is followed by 3 bytes of padding. The padding is zero in every file checked.

### Key

The input is the folder name for files whose name starts with `data`, otherwise the file's own name. It is **lowercased** first.
Let `b` be its character codes and `a` its length. Compute eight values `t`. Upper bounds are exclusive, and division truncates toward zero.

| Value | Start | Loop | Step |
| --- | --- | --- | --- |
| 1 | sum of all `b` | none | none |
| 2 | 0 | `i = 0, 2, 4, …` while `i < a − 1` | `t = t·b[i] − b[i+1]` |
| 3 | 0 | `i = 1, 4, 7, …` while `i < a − 3` | `t = (t·b[i]) / (b[i+1] + 27) − 27 − b[i−1]` |
| 4 | 5763 (equal to 131 mod 256) | `i = 1, 2, …` while `i < a` | `t = t − b[i]` |
| 5 | 66 | `i = 1, 5, 9, …` while `i < a − 4` | `t = (b[i] + 15)·t·(b[i−1] + 15) + 22` |
| 6 | 101 | `i = 0, 2, …` while `i < a − 2` | `t = t − b[i]` |
| 7 | 171 | `i = 0, 2, …` while `i < a − 2` | `t = t mod b[i]` |
| 8 | 171 | `i = 0, 1, …` while `i < a − 1` | `t = t / b[i] + b[i+1]` |

Reduce each value to 0–255 with `((t mod 256) + 256) mod 256`, write it in decimal, and join the eight with `-`.
The key is that ASCII string, for example `acm_tutorial_basic` gives `101-99-107-127-42-28-74-100`.

- **Correction to the notes:** the AssettoTools image for value 3 shows the loop running up to `a`. Six known keys fit only `i < a − 3`, and no other bound fits them all.
- **Implementing it:** values 2 and 5 can overflow 32-bit integers on long names.
  Wrap-around does not change the result mod 256, because 2³² is a multiple of 256. But C# must run this `unchecked` (the default) or use `long`, and `%` must be made non-negative.

### Cipher

Stored byte = `(data[i] + key[i mod keyLength]) mod 256`, where `key[]` is the ASCII codes of the key string.
`i` restarts at 0 for each entry. Unpacking subtracts instead.
aluigi says only "a simple rot". This exact form is confirmed by matching acd.py's output byte-for-byte.

### Verification (BRITOD-DESK, 2026-11-02)

The reference scripts are `scratch/acd_key_from_notes.py` (the key from the table above) and `scratch/acd_container.py` (list, pack, and unpack using the layout above).
Both are in `scratch/` and not committed. Re-create them from this section if they are lost.

- **Keys:** the script matches acd.py for 14 names.
  Six come from acd.py's test file: `testing`, `test`, `t`, `klmno`, `pqrstu`, `vwxyz`.
  Eight were printed by the owner running acd.py: `acm_tutorial_basic`, `zz1x`, `bmw_m3_e30`, `abarth500`, `ks_audi_sport_quattro`, `ks_porsche_911_gt3_r_2016`, `a1b2c3d4e5`, `shelby_cobra_427sc`.
  `zz1x` and `bmw_m3_e30` were chosen to tell the two readings of value 7's bound apart, and the predictions were written down before acd.py ran.
- **Pack:** packing `fixtures/local/acm_tutorial_basic/data` (40 files) gives a `data.acd` byte-identical to acd.py's (188,756 bytes).
- **Unpack:** unpacking acd.py's file gives back all 40 original files byte-for-byte.
- **Kunos container:** `shelby_cobra_427sc/data.acd` parses with this layout: no header, 52 entries, all padding zero. It was not decrypted.

### Header survey (content\cars, 2026-11-02)

Only the first 8 bytes of each `data.acd` were read. Nothing was decrypted.

- No header: 86 Kunos cars and 50 mods. All 59 Kunos cars without the `ks_` prefix are here, plus 27 that have it.
- `-1111` header: 91 Kunos cars, all with the `ks_` prefix, and 2 mods. The second int32 takes 11 values:
  243591, 313233, 517311, 591346, 673425, 743221, 781233, 835647, 912125, 912346, and 919969. Its meaning is unknown.

### Open

- Do the same key and cipher apply after a `-1111` header? Testing it means decrypting a file that is not the owner's, so ask the owner first.
  Until then, treat header files as "cannot read with the standard method" (V-13, read-only).
- Non-ASCII folder names: AssettoTools sums UTF-16 characters, acd.py sums UTF-8 bytes, and AC's behaviour is unknown. Keep car IDs ASCII (V-01).
- Does AC load a `data.acd` that we packed? This is the M2 exit gate, checked in game by the owner.

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
