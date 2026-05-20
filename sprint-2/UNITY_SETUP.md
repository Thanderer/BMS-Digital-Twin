# SafeCharge — Unity Setup Guide

**Project:** BMS Digital Twin (Group 6, SafeCharge)  
**Unity Version:** 6000.4.6f1 (Unity 6)  
**Render Pipeline:** High Definition Render Pipeline (HDRP)

---

## Requirements

| Tool | Version | Notes |
|------|---------|-------|
| Unity Editor | **6000.4.6f1** | Must match exactly — HDRP assets are version-locked |
| Unity Hub | Any recent | Used to install the editor |
| Git | 2.x+ | To clone the repo |
| OS | Windows 10/11 or macOS 13+ | Linux not tested |
| GPU | DX12 / Metal capable | HDRP requires a modern GPU |

> **Why Unity 6000.4.6f1?** The project uses HDRP 17.4.0 which ships with Unity 6. Opening it in Unity 2022/2023 will break the render pipeline and all lighting.

---

## Step 1 — Install Unity Hub & Editor

1. Download **Unity Hub** from [unity.com/download](https://unity.com/download) and install it.
2. In Unity Hub → **Installs** → **Install Editor**.
3. Search for `6000.4.6f1` (use the Archive tab if it doesn't appear under recommended).
4. During install, tick these modules:
   - **Windows Build Support (IL2CPP)** — needed to build standalone
   - **Visual Studio** (or VS Code) — for script editing
5. Click **Install** and wait.

---

## Step 2 — Clone the Repository

Open a terminal and run:

```bash
git clone https://github.com/Thanderer/BMS-Digital-Twin.git
cd BMS-Digital-Twin
```

The repo structure you'll see:

```
BMS-Digital-Twin/
├── Assets/
│   ├── Editor/
│   │   └── SafeChargeSceneBuilder.cs   ← auto-builds the demo scene
│   └── Scripts/
│       ├── BMSLogic.cs
│       ├── BatteryPackController.cs
│       ├── CellGrid.cs
│       ├── DashboardUI.cs
│       └── RangeEstimator.cs
├── Packages/
├── ProjectSettings/
└── sprint-2/
    ├── blender/
    │   └── pack.fbx                    ← 3D battery pack model
    └── SafeCharge_Sprint2.pdf          ← presentation
```

---

## Step 3 — Open the Project in Unity

1. In **Unity Hub** → **Projects** → **Open** → navigate to the cloned `BMS-Digital-Twin/` folder and click **Open**.
2. Unity will detect the HDRP package and may ask to import default HDRP assets — click **Yes / Import**.
3. Wait for the first import to complete (can take 3–5 minutes on first open).
4. If Unity warns about a Safe Mode or compile errors, click **Ignore** and let scripts finish compiling.

---

## Step 4 — Import the FBX (Battery Pack Model)

The 3D battery pack model (`pack.fbx`) lives in `sprint-2/blender/` and needs to be manually placed into Unity's Assets folder:

1. In your file explorer, go to `BMS-Digital-Twin/sprint-2/blender/`.
2. Copy `pack.fbx` into `BMS-Digital-Twin/Assets/` (drag it directly into the **Project** panel in Unity, or copy via file explorer).
3. Unity will auto-import it. When done, you'll see `pack` appear in the **Project** panel under `Assets/`.

> The scene builder script looks for any `.fbx` file with `pack` in the name anywhere under `Assets/` — the exact subfolder doesn't matter.

---

## Step 5 — Build the Demo Scene

This is a one-click step using the custom editor tool:

1. In Unity, go to the top menu bar → **SafeCharge** → **Build Demo Scene**.
2. Unity will:
   - Create `Assets/Scenes/SafeCharge_Demo.unity`
   - Instantiate the battery pack 3D model
   - Set up the camera, lighting, and Logic GameObject
   - Build the full dark-theme UI dashboard (heatmap, sliders, alarm banners, range estimator)
3. The scene opens automatically in the editor.

> If the menu item doesn't appear, check the **Console** panel for compile errors — fix them before re-trying.

---

## Step 6 — Run & Explore

1. Press **Play** (▶) in the Unity editor.
2. Use the dashboard sliders to:
   - Adjust **SOC** (State of Charge)
   - Set individual **cell temperatures**
   - Trigger **thermal runaway alarm** (above 60 °C)
3. The fan rotor on the 3D model will spin when cooling activates (above 45 °C).
4. The **range estimator** updates in real time based on SOC and cell health.

---

## Installed Unity Packages (auto-resolved via Packages/manifest.json)

You do **not** need to install these manually — Unity Package Manager handles them on first open:

| Package | Version | Purpose |
|---------|---------|---------|
| `com.unity.render-pipelines.high-definition` | 17.4.0 | HDRP renderer |
| `com.unity.inputsystem` | 1.19.0 | Input handling |
| `com.unity.ugui` | 2.0.0 | UI / TextMeshPro |
| `com.unity.timeline` | 1.8.12 | Animation timeline |
| `com.unity.visualscripting` | 1.9.11 | Visual scripting nodes |

---

## Common Issues

**"HDRP wizard" opens on first launch**  
→ Click **Fix All** in the HDRP Wizard, then close it. This configures graphics settings correctly.

**Scene looks completely black / pink**  
→ The HDRP default resources weren't imported. Go to **Edit → Render Pipeline → HD Render Pipeline → Wizard** → click **Fix All**.

**`pack.fbx` not found — Build Demo Scene fails**  
→ Make sure `pack.fbx` is inside the `Assets/` folder (not just `sprint-2/blender/`). See Step 4.

**Compile errors on first open**  
→ Wait for Unity to finish importing all packages. If errors persist, check that you're using exactly Unity `6000.4.6f1`.

**Fan rotor doesn't spin**  
→ In the `Logic` GameObject in the Hierarchy, check that **BMS Logic → Fan Rotor** is assigned. It should point to the `fan_rotor` child transform of the Pack object.

---

## Project Scripts — Quick Reference

| Script | Responsibility |
|--------|---------------|
| `BatteryPackController.cs` | Core BMS simulation — SOC, temperature thresholds, events |
| `BMSLogic.cs` | Fan RPM control, alarm state, event relay |
| `CellGrid.cs` | Heatmap visualisation of individual cell health |
| `DashboardUI.cs` | UI bindings — sliders, banners, readouts |
| `RangeEstimator.cs` | Estimates remaining driving range from SOC + cell health |
| `SafeChargeSceneBuilder.cs` | Editor-only — builds the full scene in one click |

---

## Contacts

**Group 6 — SafeCharge**  
University: Otto-von-Guericke-Universität Magdeburg (OVGU)  
Module: Digital Engineering — Visualisation Project  
Repo: [github.com/Thanderer/BMS-Digital-Twin](https://github.com/Thanderer/BMS-Digital-Twin)
