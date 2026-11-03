# Building a basic AC car from scratch

This is what we learned building `acm_tutorial_basic` by hand on 2026-11-02 (Milestone 0).
Every step here worked on BRITOD-DESK. The evidence is in `docs/VERIFIED_FACTS.md`, and anything still unverified is marked.
It follows the community [first car guide](https://assettocorsamods.net/threads/your-first-car-in-assetto-corsa-basic-guide.1019/), with the fixes we found along the way.

Tools: Blender 4.4, ksEditor (`<AC>\sdk\editor\ksEditor.exe`), a text editor, and AC with Content Manager.
`<AC>` means `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa`.

## Key ideas

- **A car is a folder** named with its car ID, for example `acm_tutorial_basic`. Use lowercase letters only (rule V-01).
  AC finds the car by that folder name, and the `data.acd` key and the sound names depend on it.
- **The 3D model is a `.kn5` file**, made by ksEditor from an FBX exported by Blender. AC never reads the Blender file.
- **Physics live in `data\`**: INI, LUT, and RTO text files. A packed `data.acd` can replace that folder, and **when both exist, AC uses `data.acd`**.
- **Dummies** are empty objects with fixed names, such as `WHEEL_LF`, that tell AC where parts are.
- **Work in a workspace** such as `D:\ACModding`, and copy the car into `<AC>\content\cars\` only to test it.

## Step 1: make the folder

1. Copy `<AC>\sdk\dev\content\cars\formula_k` (Kunos's sample car) to `D:\ACModding\` and rename the copy to your car ID.
2. Delete its models: `formula_K.kn5`, the three `formula_K_LOD_*.kn5` files, and `collider.kn5`.
3. Empty `sfx\`, but keep the folder. You'll add a sound bank in step 6.

## Step 2: model the car in Blender

**AC's axes:** +Z points forward, +Y up, and +X to the car's left. With the export settings below, Blender maps them like this:

| AC | Blender |
| --- | --- |
| Forward (+Z) | **−Y**, so the nose faces you in Front view (numpad 1) |
| Left (+X) | **+X** |
| Up (+Y) | **+Z**, and the ground is Z = 0 |

1. Start a new General file and delete everything (`A`, then `X`). Keep the unit system metric, with unit scale 1.0.
2. **Wheels:** build the positions from the data files so the model matches the physics.
   - The X position is ± half of `suspensions.ini` `TRACK`.
   - The Y position is ± half of `WHEELBASE`: front wheels at −Y, rear wheels at +Y.
   - The Z position is the tyre `RADIUS` from `tyres.ini`, so the tyre touches Z = 0.

   For formula_k's data, that gives:

   | Dummy | X | Y | Z | Wheel radius | Wheel width |
   | --- | --- | --- | --- | --- | --- |
   | `WHEEL_LF` | 0.7575 | −1.325 | 0.275 | 0.275 | 0.18 |
   | `WHEEL_RF` | −0.7575 | −1.325 | 0.275 | 0.275 | 0.18 |
   | `WHEEL_LR` | 0.730 | 1.325 | 0.285 | 0.285 | 0.24 |
   | `WHEEL_RR` | −0.730 | 1.325 | 0.285 | 0.285 | 0.24 |

3. **Each dummy:** add it with `Shift+A` > Empty > Plain Axes, set its position in the `N` panel (Item tab), and name it with `F2`. **Never scale a dummy.**
4. **Each wheel:** add a cylinder (`Shift+A` > Mesh > Cylinder) at the same position with Rotation Y = 90°.
   Then press `Ctrl+A` > Rotation, so the rotation becomes part of the mesh.
   Parent it to its dummy: click the wheel, `Shift`+click the dummy, then `Ctrl+P` > Object (Keep Transform).
   Scaling a *mesh* is fine, as long as you apply it afterwards with `Ctrl+A` > Scale.
5. **Body:** a cube with Dimensions X 0.8, Y 3.8, Z 0.5 at (0, −0.1, 0.33), with its scale applied. Leave it without a parent.
6. **Materials:** every object needs one. We used `body` for the body and `tyre` for the four wheels. Textures come later, in ksEditor.

Mesh names can be anything. Only the dummy names matter.

## Step 3: export the FBX

Select all with `A`, keeping the mouse over the 3D view, then choose File > Export > FBX:
- **Limit to:** Selected Objects (Empty and Mesh must be included).
- **Apply Scalings: FBX All.** The default, "All Local", makes ksEditor say `ERROR, FOUND SCALING ON NODE : RootNode`.
- **Apply Transform:** on, so dummies export with no rotation.
- Forward `-Z` and Up `Y` (the defaults).

Save these settings as an Operator Preset, and check that the preset really stores FBX All: ours once saved All Local by mistake.
The guide's tip to "deselect the button next to Scale" (Apply Unit) changes nothing in Blender 4.4.
Blender always writes FBX 7.4 (the 2014/2015 format), which ksEditor's FBX SDK 2014.1 reads.

## Step 4: ksEditor, the model KN5

1. **Import:** File > Open FBX. A black model is normal at this point, because it has no textures yet.
   The camera starts inside the car, at Pos 0, 0, 0. Move it with the **arrow keys**; the Pos box can't be edited.
2. **Check the scale:** click `WHEEL_LF` in the Scene tree. Its Position should read in metres, such as `0.7575 0.275 1.325`.
3. **Textures:** in the **Material** tab at the top left, pick a material. The default shader `ksPerPixel` is fine.
   Click `...` next to `txDiffuse` and choose a PNG from the car's `texture\` folder (we used plain-colour `body.png` and `tyre.png`).
4. **Save:** File > Save KN5 > **Car**, as `<car_id>.kn5` in the car folder. ksEditor embeds the textures in the file.

## Step 5: the collider

AC uses `collider.kn5` as the invisible collision shape.
1. In Blender, duplicate the body (`Shift+D`, then right-click to cancel the move) and name the copy `collider`.
   Use `Shift+S` > Cursor to World Origin, then Object > Set Origin > Origin to 3D Cursor, so its pivot sits on the ground.
2. Select **only** `collider`, export it as `collider.fbx` with the same settings, then hide it with `H` so it stays out of later car exports.
3. In ksEditor, use **File > Restart Editor** first. Otherwise you save the car scene that is still loaded.
   Open `collider.fbx`, set its material's shader to **GL**, and save it with Save KN5 > Car as `collider.kn5`.
   A correct collider KN5 is small (ours was 1.4 KB) and has no textures.

## Step 6: data, sound, and UI

**`data\lods.ini`:** set `[LOD_0] FILE=<car_id>.kn5` and `OUT=2000`, and delete `[LOD_1]` to `[LOD_3]`.
**`data\car.ini`:** set `SCREEN_NAME=` to your name, and `USE_ANIMATED_SUSPENSIONS=0`, because the model has no suspension parts.

These files can stay unchanged: `aero.ini`, `brakes.ini`, `cameras.ini`, `colliders.ini`, `driver3d.ini`, `drivetrain.ini`, `engine.ini`, `setup.ini`, `suspensions.ini`, `tyres.ini`, and every `.lut` and `.rto` file they name.
Move the rest (`ai.ini`, `digital_instruments.ini`, `lights.ini`, `mirrors.ini`, `sounds.ini`, `flames.ini`, `throttle.lut`, and so on) to a folder **outside** the car.
The car loads without them. AC only logs `flames.ini not found`, and a `digital_instruments.ini` that names missing parts didn't crash it either.
Delete `proview_nodes.ini`, which AC recreates itself.

**Sound:** AC finds sounds by name through `GUIDs.txt`, so a copied bank also needs its GUIDs renamed.
1. Copy `<AC>\content\cars\shelby_cobra_427sc\sfx\shelby_cobra_427sc.bank` to `sfx\<car_id>.bank`.
2. Create `sfx\GUIDs.txt`: take the cobra's 17 lines from `<AC>\content\sfx\GUIDs.txt` and replace `shelby_cobra_427sc` with your car ID.
   Each line is `{guid} bank:/<id>` or `{guid} event:/cars/<id>/<event>`. Use ASCII with CRLF line endings.
   The guide's line "the GUIDs are already set" is wrong for a self-built car.

**`ui\ui_car.json`:** change `"name"`, and `"brand"` if you like. Kunos's own files have raw line breaks inside strings, which AC accepts.

## Step 7: install and test

1. Copy the car folder into `<AC>\content\cars\`. On this PC that needs no administrator permission.
2. Start a practice session, not the Showroom, which this simple car doesn't support. Check that:
   the body and wheels show up in the right places, the front wheels steer, the car drives forward, the engine sounds, the gears and brakes work, and the car stays visible from far away.
3. Read `%USERPROFILE%\Documents\Assetto Corsa\logs\log.txt` **right after quitting**, because the next AC start overwrites it.
   - `LOADING COLLIDER MESH FILE` and `USING PHYSICS DRIVEN SUSPENSIONS` mean the collider and `car.ini` were read.
   - `POWER CLASS INDEX ... PW:` is the car's computed power, which is handy for telling which data AC loaded.
   - `Data path:` reads the same whether AC used `data\` or `data.acd`, so it proves nothing.

## Mistakes we made, and how to spot them

| Symptom | Cause | Fix |
| --- | --- | --- |
| ksEditor: `FOUND SCALING ON NODE : RootNode` | The FBX was exported with All Local | Use Apply Scalings = FBX All |
| A 4 KB FBX with no objects | Nothing was selected (`A` was pressed over another panel) | Press `A` over the 3D view; the objects turn orange |
| `collider.kn5` contains the whole car | The car scene was still loaded in ksEditor | File > Restart Editor, then open only `collider.fbx` |
| Rear wheels float 1 cm above the ground | The front wheel was duplicated with the wrong radius | Match each wheel to its own `tyres.ini` `RADIUS` |
| An edit to `data\` does nothing | A `data.acd` is also present, and AC uses it | Remove `data.acd`, or repack it |

## Still unverified

- How AC treats `//` inside INI values. Mods use it as a comment, as in `POSITION=… /// (x,y,z)`.
- Which duplicate key AC uses when an INI repeats one.
- Whether Blender 5.2's FBX imports into ksEditor (we used 4.4 only).
