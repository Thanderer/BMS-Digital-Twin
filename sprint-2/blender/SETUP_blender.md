# SafeCharge — Blender step

## What this does

Runs `build_pack.py` in **headless Blender** (no GUI) and produces a single
`pack.fbx` file ready to drop into Unity. The script generates:

| Object name(s)                                              | What it is                          |
|-------------------------------------------------------------|-------------------------------------|
| `pack_shell`                                                | hollow outer casing of the module   |
| `cell_00` … `cell_15`                                       | 16 prismatic cells in a 4×4 grid    |
| `fan_housing`, `fan_rotor`                                  | cooling fan, rotor is separable     |
| `cross_section_anode`, `..._separator`, `..._cathode`       | didactic cell-internals view        |

Conventions: Z-up, 1 unit = 1 m, transforms applied, materials are placeholders
(Unity will replace them).

---

## How to run it

1. Save `build_pack.py` somewhere convenient, e.g.
   `C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\blender\build_pack.py`.

2. Open **PowerShell** (Start → type "PowerShell" → Enter).

3. `cd` into that folder:

   ```powershell
   cd "C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\blender"
   ```

4. Run the build:

   ```powershell
   blender --background --python build_pack.py -- --rows 4 --cols 4 --out pack.fbx
   ```

5. Wait ~10–20 seconds. You should see output ending in:

   ```
   [OK] Wrote C:\...\pack.fbx
        pack_shell + 16 cells + fan_housing + fan_rotor + cross_section_*
   ```

6. Confirm `pack.fbx` exists next to the script (`dir` or check Explorer).

---

## Optional flags

| Flag                  | Default | Meaning                              |
|-----------------------|---------|--------------------------------------|
| `--rows N`            | 4       | rows of cells                        |
| `--cols N`            | 4       | columns of cells                     |
| `--cell-w X`          | 0.034   | cell width  (m)                      |
| `--cell-h X`          | 0.090   | cell height (m)                      |
| `--cell-d X`          | 0.140   | cell depth  (m)                      |
| `--gap X`             | 0.004   | gap between cells (m)                |
| `--shell-thickness X` | 0.006   | casing wall thickness (m)            |
| `--no-fan`            | –       | skip the cooling fan                 |
| `--no-cross-section`  | –       | skip the didactic asset              |
| `--out PATH`          | pack.fbx| where to write the .fbx              |

---

## Quick sanity check inside Blender (optional)

If you want to *see* what the script produced before importing into Unity:

```powershell
blender pack.fbx
```

That opens Blender with the .fbx loaded. You should see:
- A grey hollow box (the shell).
- 16 blue cells inside it in a 4×4 grid.
- A fan unit poking out one end.
- A small striped slab (anode / separator / cathode) off to one side.

Press `1`, `3`, `7` on the numpad to flip between front/side/top views.

---

## What "good" looks like

When this step works, you have:
- `pack.fbx` on disk, ~50–200 KB.
- 16 + 1 + 2 + 3 = **22 mesh objects** in the file (16 cells + shell + 2 fan + 3 cross-section).
- All objects with applied transforms (location/rotation/scale baked into geometry).
- Z-up orientation that Unity will swap to Y-up at import (handled by the exporter).

---

## If something fails

| Symptom                                              | Likely cause                                              |
|------------------------------------------------------|-----------------------------------------------------------|
| `blender` not recognised                             | PATH didn't take — re-run terminal or fix install step 4. |
| `ImportError: No module named bpy`                    | You ran with system Python instead of `blender --python`. |
| Script runs but `pack.fbx` not produced               | Check working directory; the path is relative to CWD.     |
| `Error: invalid axis` during FBX export               | Unsupported Blender version — script targets 4.x.         |

Send me the full PowerShell output if you hit anything else and I'll patch it.
