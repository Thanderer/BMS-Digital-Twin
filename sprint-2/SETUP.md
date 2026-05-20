# SafeCharge — Unity setup

End-to-end: empty Unity project → running demo. ~10 minutes.

---

## What you should have already

- `pack.fbx` produced by `build_pack.py` (Blender step, already done).
  Path: `C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\blender\pack.fbx`
- A Unity project at `C:\Users\Shashi Kumar\Documents\DE Project_visualisation\Safe Charge` opened in the Editor.
- The six C# scripts in this delivery, sitting in
  `C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\Assets\`
  inside two subfolders: `Scripts/` (five files) and `Editor/` (one file).

---

## Step 1 — Import the scripts (~30 sec)

Open **two windows side by side**:
- File Explorer at `C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\Assets\`
- Unity Editor with your `Safe Charge` project open.

In Unity, look at the **Project** panel (usually bottom). You'll see an `Assets` folder.

Drag the **`Scripts`** folder from File Explorer **into Unity's Assets folder**. Then drag **`Editor`** the same way. After ~2-3 seconds you should see:
```
Assets/
  Scripts/
    BatteryPackController.cs
    CellGrid.cs
    RangeEstimator.cs
    BMSLogic.cs
    DashboardUI.cs
  Editor/
    SafeChargeSceneBuilder.cs
```

Watch the bottom-right status bar — "Compiling scripts" briefly, then nothing. Open the Console (`Window → General → Console`) and confirm there are no red error lines. If you see red errors, screenshot them.

---

## Step 2 — Import the FBX (~30 sec)

Same drag move with `pack.fbx`. Drag it from File Explorer into Unity's `Assets` folder. After a few seconds it appears as a model asset (an icon with a small triangle/cube).

You can preview it: click `pack.fbx` once → look at the bottom of the Inspector panel — there's a small 3D preview window showing the pack.

---

## Step 3 — Build the scene (~5 sec)

At the top of the Unity Editor, look at the menu bar:
`File · Edit · Assets · GameObject · Component · Window · Help · SafeCharge · ...`

The **SafeCharge** menu is new (added by the Editor script). Click it:

**SafeCharge → Build Demo Scene**

A dialog appears: "Demo scene built. Open Assets/Scenes/SafeCharge_Demo.unity and press Play." Click **OK**.

In the Project panel, navigate to `Assets/Scenes/SafeCharge_Demo.unity`. Double-click it. The 3D viewport now shows your battery pack + dashboard UI overlay.

---

## Step 4 — Press Play (~10 sec)

Hit the **▶ Play** button at the top-center.

You should see:
- **Top:** maroon title bar "SafeCharge — BMS Digital Twin"
- **Left panel:** "State of Charge" with `80% displayed` and `80% actual` (all cells start at SOH=1.0)
- **Middle panel:** 4×4 grid of green tiles (Cell SOH Heatmap)
- **Right panel:** "EV Range" with Healthy / Actual numbers
- **Bottom:** two sliders (Current and Ambient) + a maroon Reset button
- **3D viewport background:** the battery pack model

Drag the **Current** slider to ~+2.0 C. Watch:
- SOC starts dropping.
- Temperature climbs.
- After a few seconds, around 45 °C, the fan rotor starts spinning.
- A yellow "▲ Cooling active" banner appears at the top.

Click the **Reset** button. The 16 cells become uneven — one corner cell drops to 0.72 SOH. The heatmap recolours, and the Range "Actual" number drops below "Healthy" — that's the professor's-feedback demo working live.

Click ▶ again to stop Play mode.

---

## Step 5 — Sanity inspection (optional but recommended)

In the **Hierarchy** panel on the left, you should see:
```
Main Camera
Directional Light
Pack            ← from pack.fbx, has cell_00..cell_15 as children
Logic           ← holds BatteryPackController, CellGrid, RangeEstimator, BMSLogic
DashboardCanvas ← all UI lives here
EventSystem
```

Click on `Logic`. In the Inspector you'll see four components — each with its references already filled in by the Editor script. If any of those references show "None (Missing)", flag it.

---

## Troubleshooting

| Symptom                                                              | Fix                                                                                                   |
|----------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|
| Console: `CS0246: namespace SafeCharge could not be found`            | One of the scripts didn't import. Confirm all five .cs files are inside `Assets/Scripts/`.            |
| Menu `SafeCharge` doesn't appear at the top                          | `SafeChargeSceneBuilder.cs` is not under an `Editor/` folder. Move it there.                          |
| Dialog says "pack.fbx not found"                                     | The .fbx isn't in the Assets folder yet — drag it in first.                                           |
| Cells stay white / don't change colour                               | `pack.fbx` imported without separate cell objects. Rerun build_pack.py and re-import. Or your URP shader uses `_Color` instead of `_BaseColor` — change `CellGrid.colorProperty` in the Inspector. |
| Temperature never climbs                                             | Current slider is at 0. Drag it to a positive C-rate.                                                 |
| Fan doesn't spin                                                     | In the Logic GameObject's Inspector, BMSLogic's `Fan Rotor` field is `None`. Drag `Pack/fan_rotor` from the Hierarchy into that field.                              |
| Console: `MissingMethodException FindFirstObjectByType`              | Unity 6 API only. If using older Unity, change the line in SafeChargeSceneBuilder.cs to `FindObjectOfType<EventSystem>()`. |

---

## What's intentionally not in this sprint

- **Visual polish:** no PBR materials, no lighting setup, no fancy SOC ring graphics. Sprint 4.
- **Ion-flow particles** on the cross-section asset. Sprint 4.
- **Per-cell thermal coupling.** Currently pack-level lumped model. Sprint 4.
- **Scenario preset buttons** (Fresh / Worn / Abuse). Sprint 6.
- **Result card** with cause-and-effect summary. Sprint 6.

If Play mode works and the SOH ↔ range link is visible, Sprint 2 is functionally done — what's left is presentation polish and the slide deck for the 22 May review.

---

## File checklist after setup

Inside your Unity project (`C:\Users\Shashi Kumar\Documents\DE Project_visualisation\Safe Charge\`):

```
Assets/
  Scenes/
    SafeCharge_Demo.unity      ← built by the menu
    SampleScene.unity          ← the default; ignore
  Scripts/
    BatteryPackController.cs
    CellGrid.cs
    RangeEstimator.cs
    BMSLogic.cs
    DashboardUI.cs
  Editor/
    SafeChargeSceneBuilder.cs
  pack.fbx
```

That's everything Sprint 2 needs.
