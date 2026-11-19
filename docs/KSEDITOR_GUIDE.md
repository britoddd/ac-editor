# ksEditor: turning your Blender model into a car KN5

A short guide to the ksEditor part of a new car, for the second car `acm_second`.
Every step here worked when we built `acm_tutorial_basic` (2026-11-02). The evidence is in `docs/VERIFIED_FACTS.md`
("Blender FBX export settings for ksEditor" and "KN5 written by ksEditor"). Anything not tested is marked **unverified**.
The full walk-through, Blender included, is `docs/FIRST_CAR_GUIDE.md`.

`<AC>` means `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa`.
The car folder means `D:\ACModding\cars\acm_second\`.

## What ksEditor does here

AC can't read Blender or FBX files. It reads only `.kn5` models.
ksEditor (`<AC>\sdk\editor\ksEditor.exe`, or **Tools > ksEditor** and the checklist's **Open ksEditor** button in the app) imports an FBX, lets you assign textures and shaders, and saves a KN5.
You make two KN5 files per car:

| File | What it is | Textures |
| --- | --- | --- |
| `acm_second.kn5` | The visible car: body, wheels and dummies | Yes, embedded in the file |
| `collider.kn5` | The invisible shape AC uses for collisions | None. Ours was 1.4 KB |

## Before you start: the FBX from Blender

ksEditor only accepts an FBX exported like this (File > Export > FBX in Blender 4.4):

- **Limit to: Selected Objects.** Press `A` with the mouse over the 3D view first, so everything turns orange.
- **Apply Scalings: FBX All.** The default, "All Local", makes ksEditor say `ERROR, FOUND SCALING ON NODE : RootNode`.
- **Apply Transform: on.**
- Forward `-Z`, Up `Y` (the defaults).

The dummies must be named `WHEEL_LF`, `WHEEL_RF`, `WHEEL_LR` and `WHEEL_RR`. Every mesh needs a material, such as `body` and `tyre`.
Save `acm_second.fbx` in the car folder. Export ZIP leaves `.fbx` and `.blend` files out, so they can stay there.

**Textures:** make a `texture\` folder in the car folder and put plain PNGs in it, such as `body.png` and `tyre.png`.
A one-colour image from Paint is enough. The app's New car (empty) doesn't create this folder.

## Part 1: the car model

1. **Start clean.** Open ksEditor. If it already has a scene loaded, use **File > Restart Editor**.
2. **Import.** Choose **File > Open FBX** and pick `acm_second.fbx`.
   - The model shows **black**. That's normal: it has no textures yet.
   - The camera starts at `Pos 0, 0, 0`, which is inside the car. Move it with the **arrow keys**; you can't type into the Pos box.
   - ksEditor creates `texture\NULL.dds` next to the FBX. Leave it.
3. **Check the tree.** The Scene tree should look like
   `SCENE_ROOT > MODEL: acm_second.fbx > FBX: acm_second.fbx > WHEEL_LF, WHEEL_LR, WHEEL_RF, WHEEL_RR, <your meshes>`.
   If a `WHEEL_` name is missing or misspelled, fix it in Blender and export again.
4. **Check the scale.** Click `WHEEL_LF`. Its Position should be in metres, such as `0.7575 0.275 1.325` for formula_k's wheel layout, with Scale `1 1 1`.
   Values 100 times too large mean the FBX was exported with the wrong Apply Scalings.
5. **Add textures.** Open the **Material** tab at the top left. It lists one entry per Blender material, under the names you gave them in Blender.
   Do this once for each material:

   | Pick this material | It covers | Shader | Texture slot | Choose |
   | --- | --- | --- | --- | --- |
   | `body` | The body mesh | `ksPerPixel` (the default, keep it) | `txDiffuse` | `texture\body.png` |
   | `tyre` | All four wheel cylinders (they share this one material) | `ksPerPixel` (the default, keep it) | `txDiffuse` | `texture\tyre.png` |

   - On an imported material, `txDiffuse` is the only texture slot and shows **NULL**. Click **`...`** next to it and pick the PNG.
   - Leave the number values (ksAmbient 0.0509, ksDiffuse 0.8, ksSpecular 0.8, ksSpecularEXP 10, and so on) at their defaults.
   - If your Blender materials have other names, use those. The rule is one PNG per material, in `txDiffuse`.
   - The model now shows its colours.
   - **Layers:** TODO, waiting for a screenshot of the Material tab. Until then, only change `txDiffuse`.
6. **Save.** Choose **File > Save KN5 > Car** and save it as **`acm_second.kn5`** in the car folder.
   ksEditor embeds the textures in the KN5.
   The menu also has *Car (No Textures)*, *Track* and *Car (Experimental)*. We haven't tested those, so use **Car**.

## Part 2: the collider

1. In Blender, make a simple copy of the body called `collider`, with its origin on the ground. See step 5 of `docs/FIRST_CAR_GUIDE.md`.
   Select **only** `collider` and export it as `collider.fbx` with the same settings. Then hide it with `H`, so it stays out of the next car export.
2. In ksEditor, choose **File > Restart Editor first**. Otherwise you save the car scene that's still loaded, and your "collider" contains the whole car.
3. **File > Open FBX** and pick `collider.fbx`.
4. In the Material tab, set the collider's material shader to **GL**. Don't add a texture.
5. **File > Save KN5 > Car**, as **`collider.kn5`** in the car folder. It should be only a few KB.

## Part 3: tell the car which KN5 to load

`data\lods.ini` names the model file. If you used **Copy data from `formula_k`**, it still names `formula_K.kn5` and three LOD files you don't have.
On the app's **Raw files** tab, edit `lods.ini` so it reads:

```ini
[LOD_0]
FILE=acm_second.kn5
IN=0
OUT=2000
```

Keep `[COCKPIT_HR]` and `[DRIVER_HR]` as they are, and delete `[LOD_1]` to `[LOD_3]`.
Also set `car.ini` `USE_ANIMATED_SUSPENSIONS=0` on the Forms or Raw files tab, because this model has no suspension parts.

Then run **Validate**:
- **V-02** finds a KN5 that `lods.ini` names but that isn't in the folder, so a typo in the file name shows up here.
- **V-04** finds a missing `collider.kn5`.
- **V-15** lists the `.fbx` and `.blend` files as information only. Export leaves them out.

Then tick the **ksEditor model** and **Collider** steps on the First-car checklist.

## When it goes wrong

| What you see | Why | Fix |
| --- | --- | --- |
| `ERROR, FOUND SCALING ON NODE : RootNode` | FBX exported with All Local | Export again with Apply Scalings = FBX All |
| ksEditor opens an empty scene, and the FBX is only about 4 KB | Nothing was selected at export (`A` was pressed over another panel) | Press `A` over the 3D view, check the objects are orange, and export again |
| `collider.kn5` is large or has the whole car in it | The car scene was still loaded | File > Restart Editor, then open only `collider.fbx` |
| The model is black after you save | No `txDiffuse` texture was assigned | Material tab, `...` next to `txDiffuse`, pick a PNG |
| A wheel floats or sinks in the game | Its height doesn't match its own `tyres.ini` `RADIUS` | Fix that wheel's Z position in Blender, and export and save again |

## Still unverified

- Whether a Blender version other than 4.4 exports an FBX ksEditor accepts. Blender 5.2 also writes FBX 7.4, but we haven't imported one.
- What *Save KN5 > Car (No Textures)* and *Car (Experimental)* change.
- Whether textures must sit in `texture\`. They worked from there, and we haven't tried anywhere else.
