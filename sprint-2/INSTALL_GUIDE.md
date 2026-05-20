# SafeCharge — Full Installation & Setup Guide
### BMS Digital Twin | Group 6 | OVGU Digital Engineering

> **Who is this for?** Any teammate setting up the project from scratch on their own PC.  
> **Time needed:** ~30–45 minutes total (most of it is download/install waiting).  
> **Repo:** [github.com/Thanderer/BMS-Digital-Twin](https://github.com/Thanderer/BMS-Digital-Twin)

---

## Table of Contents

1. [System Requirements](#1-system-requirements)
2. [Clone the Repository](#2-clone-the-repository)
3. [Part A — Blender Setup](#part-a--blender-setup)
   - [Install Blender](#a1-install-blender)
   - [Add Blender to PATH](#a2-add-blender-to-path)
   - [Generate the FBX (pack.fbx)](#a3-generate-the-fbx-packfbx)
4. [Part B — Unity Setup](#part-b--unity-setup)
   - [Install Unity Hub](#b1-install-unity-hub)
   - [Install Unity Editor 6000.4.6f1](#b2-install-unity-editor-600046f1)
   - [Open the Project](#b3-open-the-project)
   - [Fix the HDRP Wizard](#b4-fix-the-hdrp-wizard--required)
   - [Import TextMeshPro Essentials](#b5-import-textmeshpro-essentials--required)
   - [Import the FBX](#b6-import-the-fbx-packfbx)
   - [Build the Demo Scene](#b7-build-the-demo-scene)
   - [Press Play](#b8-press-play--verify)
5. [What the Simulation Does](#5-what-the-simulation-does)
6. [Script Reference](#6-script-reference)
7. [Troubleshooting](#7-troubleshooting)

---

## 1. System Requirements

### Minimum Hardware

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 4-core, 2.5 GHz | 6-core, 3.5 GHz+ |
| RAM | 8 GB | 16 GB |
| GPU | DirectX 12 / Metal capable | NVIDIA GTX 1060 / AMD RX 580 or better |
| Storage | 10 GB free | 20 GB free |
| Display | 1080p | 1440p |

> **HDRP (the render pipeline this project uses) requires DirectX 12 on Windows or Metal on macOS.** Very old or integrated-only GPUs (e.g. Intel HD 4000) will not work. Unity will warn you on startup if your GPU is unsupported.

### Operating System

| OS | Status |
|----|--------|
| Windows 10 / 11 (64-bit) | ✅ Fully tested |
| macOS 13 Ventura or later | ✅ Should work |
| Linux (Ubuntu 22.04+) | ⚠️ Untested — may work |

### Software to Install

- **Git** — to clone the repo
- **Blender 4.x** — to generate the 3D battery pack model
- **Unity Hub** — to manage Unity installations
- **Unity Editor 6000.4.6f1** — exact version required

---

## 2. Clone the Repository

If you don't have Git installed, download it from [git-scm.com](https://git-scm.com) and install with defaults.

Open a terminal (PowerShell on Windows / Terminal on macOS) and run:

```bash
git clone https://github.com/Thanderer/BMS-Digital-Twin.git
cd BMS-Digital-Twin
```

After cloning, your folder structure looks like:

```
BMS-Digital-Twin/
├── Assets/
│   ├── Editor/
│   │   └── SafeChargeSceneBuilder.cs
│   └── Scripts/
│       ├── BMSLogic.cs
│       ├── BatteryPackController.cs
│       ├── CellGrid.cs
│       ├── DashboardUI.cs
│       └── RangeEstimator.cs
├── Packages/
│   └── manifest.json          ← Unity auto-resolves all packages from here
├── ProjectSettings/
└── sprint-2/
    ├── blender/
    │   ├── pack.fbx            ← pre-built FBX (use this if Blender gives trouble)
    │   ├── build_pack.py       ← script to rebuild the FBX yourself
    │   └── SETUP_blender.md
    ├── SafeCharge_Sprint2.pdf
    ├── UNITY_SETUP.md
    └── INSTALL_GUIDE.md        ← this file
```

> **Shortcut:** `sprint-2/blender/pack.fbx` is already in the repo — if you just want to run the Unity project and don't need to modify the 3D model, you can **skip Part A entirely** and go straight to [Part B](#part-b--unity-setup).

---

## Part A — Blender Setup

> Only needed if you want to **regenerate or modify the 3D battery pack model**.  
> If you're just running the Unity demo, skip to [Part B](#part-b--unity-setup).

### A1. Install Blender

1. Go to [blender.org/download](https://www.blender.org/download/).
2. Download **Blender 4.x** (the latest stable 4.x release — e.g. 4.3 or 4.4).
   - Windows: download the `.msi` installer.
   - macOS: download the `.dmg`.
3. Run the installer with default settings.

> **Why 4.x specifically?** The `build_pack.py` script uses the HDRP FBX exporter API introduced in Blender 4. Blender 3.x will throw an `Error: invalid axis` on export.

### A2. Add Blender to PATH

The script is run from the terminal using the `blender` command. You need Blender's install folder on your system PATH.

**Windows:**

1. Find where Blender installed — typically `C:\Program Files\Blender Foundation\Blender 4.x\`.
2. Open **Start** → search **"Edit the system environment variables"** → click it.
3. Click **Environment Variables** (bottom right).
4. Under **System variables**, find `Path` → click **Edit**.
5. Click **New** → paste the Blender folder path (e.g. `C:\Program Files\Blender Foundation\Blender 4.3\`).
6. Click **OK** on all dialogs.
7. **Close and reopen PowerShell** (existing terminals won't see the new PATH).
8. Verify: `blender --version` — should print `Blender 4.x.x`.

**macOS:**

```bash
# Add to ~/.zshrc (or ~/.bash_profile)
echo 'export PATH="/Applications/Blender.app/Contents/MacOS:$PATH"' >> ~/.zshrc
source ~/.zshrc
blender --version   # should print Blender 4.x.x
```

### A3. Generate the FBX (pack.fbx)

1. Open PowerShell / Terminal.
2. Navigate to the blender folder inside the repo:

```powershell
# Windows
cd "C:\path\to\BMS-Digital-Twin\sprint-2\blender"

# macOS / Linux
cd /path/to/BMS-Digital-Twin/sprint-2/blender
```

3. Run the build script:

```powershell
blender --background --python build_pack.py -- --rows 4 --cols 4 --out pack.fbx
```

4. Wait 10–20 seconds. You should see output ending with:

```
[OK] Wrote .../pack.fbx
     pack_shell + 16 cells + fan_housing + fan_rotor + cross_section_*
```

5. Confirm `pack.fbx` now exists in the folder.

**What gets generated inside pack.fbx:**

| Object | What it is |
|--------|------------|
| `pack_shell` | Hollow outer casing of the battery module |
| `cell_00` … `cell_15` | 16 prismatic cells in a 4×4 grid |
| `fan_housing` | Cooling fan outer housing |
| `fan_rotor` | Rotor — spins in Unity when temperature > 45 °C |
| `cross_section_anode/separator/cathode` | Didactic cell-internals cross-section |

**Optional flags for build_pack.py:**

| Flag | Default | Meaning |
|------|---------|---------|
| `--rows N` | 4 | Rows of cells |
| `--cols N` | 4 | Columns of cells |
| `--cell-w X` | 0.034 | Cell width in metres |
| `--cell-h X` | 0.090 | Cell height in metres |
| `--cell-d X` | 0.140 | Cell depth in metres |
| `--gap X` | 0.004 | Gap between cells |
| `--no-fan` | — | Skip the cooling fan |
| `--out PATH` | pack.fbx | Output file path |

---

## Part B — Unity Setup

### B1. Install Unity Hub

1. Go to [unity.com/download](https://unity.com/download).
2. Download **Unity Hub** and install it.
3. Create a free Unity account if you don't have one (needed to activate the free Personal licence).
4. In Unity Hub, sign in → **Preferences → Licences → Add** → activate a **Personal** licence (free).

### B2. Install Unity Editor 6000.4.6f1

> **This exact version is required.** The project uses HDRP 17.4.0 which ships with Unity 6. Opening in Unity 2022 or 2023 will break the entire render pipeline.

1. In Unity Hub → **Installs** tab → **Install Editor**.
2. The version `6000.4.6f1` may not appear in the default list. If not:
   - Click **Archive** (or go to [unity.com/releases/editor/archive](https://unity.com/releases/editor/archive))
   - Find **Unity 6** → `6000.4.6f1` → click **Install with Unity Hub**.
3. In the module selection screen, tick:
   - ✅ **Windows Build Support (IL2CPP)** — to build standalone executables
   - ✅ **Microsoft Visual Studio Community** (Windows) or leave IDE selection for later
   - Everything else is optional for now
4. Click **Install** and wait (~5–15 minutes depending on your internet).

### B3. Open the Project

1. In Unity Hub → **Projects** tab → **Open** (top right).
2. Navigate to the cloned `BMS-Digital-Twin/` folder.
3. Select the folder and click **Open**.
4. Unity will detect it as a Unity 6 project and open it.
5. **First-time import takes 3–10 minutes** — Unity is downloading HDRP and all packages from the `manifest.json`. Let it finish. You'll see a progress bar at the bottom.

> If Unity asks "Do you want to enter Safe Mode?" due to compile errors — click **Ignore**. The errors resolve once all packages are imported.

### B4. Fix the HDRP Wizard ← REQUIRED

This step is **mandatory**. Without it, the scene will appear completely black or show pink/magenta error colours.

When the project first opens, an **HDRP Wizard** window usually appears automatically. If it doesn't:

- Go to **Edit → Render Pipeline → HD Render Pipeline → Wizard**

In the Wizard window:

1. Look at the checklist — items with a red ✗ need fixing.
2. Click **Fix All** at the bottom of each section:
   - **Global Settings** → Fix All
   - **HDRP** → Fix All  
   - **VR** → you can skip this section
3. Close the Wizard.
4. Unity may recompile shaders — wait for the progress bar to finish.

**What this fixes:** HDRP requires specific Graphics API settings, HDR output configuration, and a default HDRP asset assigned in Project Settings. The wizard configures all of this automatically.

### B5. Import TextMeshPro Essentials ← REQUIRED

The dashboard UI uses TextMeshPro for all text rendering. You need to import its built-in font and shader assets once.

1. Go to **Window → TextMeshPro → Import TMP Essential Resources**.
2. A dialog appears — click **Import**.
3. A second dialog may appear for "TMP Examples & Extras" — this is optional, click **Skip**.

> If this step is skipped, all text in the dashboard will appear as pink/white rectangles. The project will still run but nothing will be readable.

### B6. Import the FBX (pack.fbx)

The 3D battery pack model needs to be placed inside Unity's `Assets/` folder so the scene builder can find it.

**Option 1 — Use the pre-built FBX from the repo (recommended):**

1. Open File Explorer and navigate to `BMS-Digital-Twin/sprint-2/blender/`.
2. Copy `pack.fbx`.
3. Paste it into `BMS-Digital-Twin/Assets/` (the root Assets folder).

**Option 2 — Drag into Unity directly:**

1. In the Unity Editor, look at the **Project** panel (bottom of screen).
2. Click on the `Assets` folder.
3. Open File Explorer alongside it and drag `pack.fbx` from `sprint-2/blender/` directly into the Project panel.

Either way, Unity will auto-import the FBX after a few seconds. You'll see it appear as a model asset (cube icon) in the Project panel.

**Verify the import:** Click `pack` in the Project panel → look at the bottom of the **Inspector** panel — a small 3D preview should show the battery housing with cells inside.

**Expected FBX import settings** (Unity sets these automatically, just verify):
- Scale Factor: `1`
- Convert Units: ✅ enabled (Blender is Z-up, Unity is Y-up — Unity corrects this)
- Import Cameras: ✗ off
- Import Lights: ✗ off
- Read/Write: ✅ enabled

### B7. Build the Demo Scene

With the FBX imported and the scripts already in `Assets/Scripts/` and `Assets/Editor/`, you can now build the full scene in one click.

Look at Unity's top menu bar:

```
File  Edit  Assets  GameObject  Component  Window  Help  SafeCharge
```

The **SafeCharge** menu was added by `SafeChargeSceneBuilder.cs`. Click it:

**SafeCharge → Build Demo Scene**

Unity will:
1. Create `Assets/Scenes/SafeCharge_Demo.unity`
2. Instantiate the battery pack 3D model in the viewport
3. Set up the camera with a close-up view of the pack
4. Create a `Logic` GameObject with all 4 simulation components attached
5. Build the full dark-theme dashboard UI (heatmap, sliders, alarm banners, range readout)
6. Wire all script references automatically

A dialog will confirm completion. Click **OK**.

In the **Project** panel, navigate to `Assets/Scenes/` and double-click `SafeCharge_Demo.unity` to open it. You should see the battery pack and UI overlay in the viewport.

> **If the SafeCharge menu doesn't appear:** The `SafeChargeSceneBuilder.cs` file must be inside a folder named exactly `Editor` under `Assets`. Check `Assets/Editor/SafeChargeSceneBuilder.cs` exists. If not, the script didn't clone correctly — re-pull from the repo.

### B8. Press Play & Verify

Click the **▶ Play** button at the top centre of Unity.

You should see:

```
┌─────────────────────────────────────────────┐
│  SafeCharge — BMS Digital Twin              │  ← maroon title bar
├────────────────┬──────────┬─────────────────┤
│ State of Charge│ Cell SOH │ EV Range        │
│  Displayed: 80%│ Heatmap  │ Healthy: 222 km │
│  Actual:    80%│ (4×4 grid│ Actual:  222 km │
│  ████████░░    │ of green │ Delta:     0 km │
│                │  tiles)  │                 │
├────────────────┴──────────┴─────────────────┤
│  Current [slider]    Ambient Temp [slider]  │
│                   [ Reset ]                 │
└─────────────────────────────────────────────┘
         (3D battery pack in background)
```

**Test the simulation:**

1. Drag the **Current** slider to `+2.0 C` — SOC starts dropping, temperature climbs.
2. When temperature crosses **45 °C**: yellow "▲ Cooling active" banner appears, fan rotor on the 3D model starts spinning.
3. If temperature hits **60 °C**: red "⚠ Thermal runaway" alarm fires.
4. Click **Reset** — one corner cell drops to SOH 0.72. The heatmap recolours that cell orange/red. The "Actual" range drops below "Healthy" — this is the professor's battery health feedback working live.
5. Click **▶** again to stop.

---

## 5. What the Simulation Does

This is a Battery Management System (BMS) digital twin for a 16-cell EV battery pack. It models:

**State of Charge (SOC):**
- Tracks charge from 0–100% using coulomb counting.
- CC/CV charging: constant current until 80% SOC, then tapers (CV phase) to prevent overcharge.

**Thermal Model:**
- Lumped thermal model — pack temperature rises with I²R losses and drops with cooling.
- Passive cooling always active. Active cooling (fan) kicks in above 45 °C.

**Cell Health (SOH):**
- 16 cells each have their own State of Health (0.0–1.0).
- Weakest cell degrades faster than the rest (models real non-uniform ageing).
- The displayed SOC is based on the weakest cell — this is what your phone/car shows you.
- The actual usable capacity is lower — the delta is what the heatmap and range readout visualise.

**Range Estimator:**
- `Healthy range` = what the range would be if all cells were at 100% SOH.
- `Actual range` = range with current weakest-cell SOH applied.
- Delta = the "hidden" range you're losing due to cell degradation.

---

## 6. Script Reference

| File | Role |
|------|------|
| `BatteryPackController.cs` | Core BMS — SOC, temperature, cell SOH, CC/CV charging, degradation model, all threshold events |
| `BMSLogic.cs` | Fan RPM control (scales 0–1500 RPM between 45–60 °C), alarm state management |
| `CellGrid.cs` | Drives material colour of each of the 16 cell meshes from their SOH value (green → orange → red) |
| `DashboardUI.cs` | All UI bindings — sliders, SOC text, SOH heatmap tiles, range readout, alarm banners |
| `RangeEstimator.cs` | Computes healthy vs actual driving range in km from SOC and SOH |
| `SafeChargeSceneBuilder.cs` | **Editor only** — builds the entire scene in one click from the SafeCharge menu |

---

## 7. Troubleshooting

### Blender

| Problem | Fix |
|---------|-----|
| `'blender' is not recognised as a command` | PATH wasn't set or terminal wasn't restarted — close and reopen PowerShell, then retry |
| `ImportError: No module named bpy` | You ran `python build_pack.py` directly instead of `blender --background --python build_pack.py` |
| `Error: invalid axis` during FBX export | Wrong Blender version — the script requires Blender 4.x, not 3.x |
| Script runs but no `pack.fbx` appears | Check your working directory — the output path is relative to where you ran the command |
| Blender opens a GUI window instead of running headless | `--background` flag is missing from your command |

### Unity — Installation

| Problem | Fix |
|---------|-----|
| Unity Hub says licence required | Sign in → Preferences → Licences → Add → Personal (free) |
| Can't find version 6000.4.6f1 | Use the Archive tab in Unity Hub or go to unity.com/releases/editor/archive |
| Install fails / times out | Try again on a stable connection; Unity packages are ~5 GB total |

### Unity — Project Setup

| Problem | Fix |
|---------|-----|
| Entire scene is black | Run HDRP Wizard (Edit → Render Pipeline → HD Render Pipeline → Wizard) → Fix All |
| Everything is pink/magenta | Missing HDRP shaders — Fix All in the HDRP Wizard, then reimport |
| All text appears as pink rectangles | TextMeshPro essentials not imported — Window → TextMeshPro → Import TMP Essential Resources |
| `SafeCharge` menu doesn't appear in menu bar | `SafeChargeSceneBuilder.cs` isn't in an `Editor/` folder — move it to `Assets/Editor/` |
| "Build Demo Scene" says "pack.fbx not found" | FBX not in Assets folder — copy `sprint-2/blender/pack.fbx` into `Assets/` |
| Compile errors on first open | Wait for all packages to finish importing (watch bottom status bar); don't click into the scene yet |
| Console: `CS0246 namespace SafeCharge not found` | Scripts didn't import — confirm all `.cs` files are in `Assets/Scripts/` and `Assets/Editor/` |

### Unity — Runtime

| Problem | Fix |
|---------|-----|
| Fan rotor doesn't spin | In Hierarchy → Logic → BMSLogic component → drag `Pack/fan_rotor` into the Fan Rotor field |
| Cells don't change colour | In `CellGrid` component, set Color Property to `_BaseColor` (HDRP/URP) or `_Color` (Built-in) |
| SOC doesn't change when dragging slider | Check Console for errors; ensure BatteryPackController is enabled (checkbox in Inspector) |
| Range shows 0 km | `RangeEstimator.controller` reference is null — check the Logic GameObject Inspector |
| Very slow in Play mode | HDRP is GPU-heavy — close other applications; or lower resolution in Game view |

---

## Final Checklist

Before hitting Play, confirm:

- [x] Unity version is exactly `6000.4.6f1`
- [x] HDRP Wizard → Fix All completed
- [x] TextMeshPro Essential Resources imported
- [x] `pack.fbx` is in `Assets/` (not just `sprint-2/blender/`)
- [x] `SafeCharge_Demo.unity` scene is open (double-clicked in Project panel)
- [x] No red errors in the Console window
- [x] Press ▶ Play — dashboard appears and sliders respond

If all boxes are checked and something still doesn't work, drop the Console error output in the group chat and we'll fix it.

---

*Last updated: Sprint 2 | Group 6 — SafeCharge | OVGU Digital Engineering*
