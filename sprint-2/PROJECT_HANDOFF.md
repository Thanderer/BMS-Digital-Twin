# SafeCharge — Project Handoff

This document captures the full state of the SafeCharge BMS Digital Twin
project so any new Claude session (or a different LLM) can resume work
without re-deriving context.

---

## 1. Project at a glance

- **Course:** Digital Engineering Summer Semester Project, Otto-von-Guericke-University Magdeburg, 2026.
- **Team:** Group 6 — 6 students.
  - CEE: Rishi Chalamalasetty, Avinash Lobo
  - DE:  Jashkumar Dhameliya, Shashi Kumar (this is me, the user), Hari Charan
  - DKE: Anant Sharma
- **Tech stack:** Unity 6 LTS (URP, legacy uGUI), Blender 4.2 LTS (scripted with bpy), LaTeX/Beamer for slide decks (compiled in Overleaf).
- **Final deliverable:** An interactive 3D digital twin of a Li-ion battery pack + BMS, demoing how cell SOH degradation reduces real EV range. Sprint deadline 22 May 2026 for Sprint 2.

## 2. Folder map

| Location | Contents |
|---|---|
| `C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\` | All source code, decks, plans — committed to GitHub. |
| `└── blender\build_pack.py` | Headless Blender script that builds pack shell + 4×4 cell grid + fan + cross-section, exports `pack.fbx`. |
| `└── blender\SETUP_blender.md` | How to run the Blender script. |
| `└── Assets\Scripts\BatteryPackController.cs` | Runs the Sprint 2 equations (SOC, Joule heating, lumped thermal). Single source of truth. |
| `└── Assets\Scripts\CellGrid.cs` | Maps SOH array to 3D cell colours. |
| `└── Assets\Scripts\RangeEstimator.cs` | Computes EV range km from usable energy. |
| `└── Assets\Scripts\BMSLogic.cs` | Threshold checks + fan-spin logic. |
| `└── Assets\Scripts\DashboardUI.cs` | uGUI bindings, button click handlers. |
| `└── Assets\Editor\SafeChargeSceneBuilder.cs` | Editor menu `SafeCharge → Build Demo Scene`. Builds the scene + canvas + wires references. |
| `└── SafeCharge_Sprint2.tex` | Beamer deck for Sprint 2 (compiles in Overleaf with pdfLaTeX). |
| `└── SafeCharge_Sprint2.pdf` | Compiled preview of the Sprint 2 deck. |
| `└── SafeCharge_Sprint2_Plan.docx` | Full Sprint 2 planning document with equations + task assignments. |
| `└── SETUP.md` | Step-by-step: empty Unity project → running Play-mode demo. |
| `C:\Users\Shashi Kumar\Documents\DE Project_visualisation\Safe Charge\` | Unity 6 project root (separate from the workspace folder, intentionally). |

## 3. Sprint timeline

| Sprint | Date | Status |
|---|---|---|
| Kickoff | 24 Apr | done |
| Sprint 1 | 08 May | done — concept presentation accepted |
| **Sprint 2** | **22 May** | **in progress — working demo built** |
| Sprint 4 | 19 Jun | planned: per-cell coupling, ion-flow particles, scenario presets |
| Sprint 6 | 17 Jul | planned: result card, polish |
| Sprint 8 | 14 Aug | bug fixing, final app |
| Final | 11 Sep | report + demo |

User strategy: front-load ~70% of the work now, reveal incrementally each sprint. Working demo already runs end-to-end as of Sprint 2.

## 4. Professor feedback (Sprint 1) — the design driver

The Sprint 1 feedback was: SOC alone is misleading — a pack reading 80 % may actually deliver much less because the weakest cell limits usable capacity. The app must show the gap between displayed SOC and actual usable SOC, expressed as EV driving range loss in km.

Every Sprint 2 decision came from this: SOH array, weakest-cell math, dual SOC gauge, range estimator, Worn Pack scenario button.

## 5. Equations implemented (with sources)

```
SOC[k+1]   = SOC[k] - (I * dt) / (3600 * Q_nom)               (Plett 2015)
Q_gen      = I^2 * R_int * numCells                            (Bernardi et al. 1985)
m*c_p*dT/dt = Q_gen - h*A*(T - T_amb)                          (Forgez et al. 2010)
Q_usable   = min_j(Q_full_j)    where Q_full_j = SOH_j * Q_nom (Dubarry et al. 2009)
SOC_actual = SOC_displayed * SOH_pack
E_usable   = V_nom * Q_usable * SOC_displayed / 1000   (kWh)   (Larminie & Lowry 2012)
Range      = E_usable / c_drive    where c_drive = 0.18 kWh/km
```

Thresholds locked from Sprint 1: cooling at 45 °C, alarm at 60 °C.

## 6. Runtime architecture (event-driven)

`BatteryPackController` is the single source of truth. It runs §4 equations in `FixedUpdate` and raises events. Everyone else subscribes.

```
BatteryPackController
  ├─ OnSocChanged           --> DashboardUI (SOC labels, rings)
  ├─ OnTemperatureChanged   --> DashboardUI (temp label)
  ├─ OnSohChanged           --> CellGrid (3D cell colours) + DashboardUI (heatmap)
  ├─ OnCurrentOverridden    --> DashboardUI (snap slider to 0 when BMS cuts current)
  ├─ OnCoolingActivated     --> BMSLogic (fan starts) + DashboardUI (banner)
  ├─ OnCoolingDeactivated   --> BMSLogic (fan stops) + DashboardUI (banner)
  └─ OnAlarmRaised          --> DashboardUI (alarm banner)

RangeEstimator subscribes to SOC + SOH changes, recomputes range, raises OnRangeChanged.
```

## 7. UI layout (built procedurally by SafeChargeSceneBuilder)

```
Top maroon title bar
├─ SOC panel (left)          "80% displayed" / "64% actual" / "T pack: 25 C"
├─ Heatmap panel (middle)    4x4 grid of Image tiles, green->red by SOH
├─ Range panel (right)       "Healthy: 156 km" / "Actual: 112 km" / "-44 km lost to SOH"
└─ Bottom controls bar
   ├─ Current slider          -5 .. +5 C-rate
   ├─ Ambient slider          -10 .. +50 °C
   └─ Three stacked buttons:
      ├─ Fresh Pack (green)   restore SOH=1.0, clear banners
      ├─ Worn Pack (maroon)   apply weak cell scenario (cell 15 = 0.72, others = 0.88)
      └─ Reset Sim (grey)     full restart: SOC, T, current, sliders, banners all snap back

Floating banners (anchored top centre, just below title bar):
- Cooling active (yellow)     visible while T > 45 °C
- Thermal alarm (red)         visible after T > 60 °C until reset
```

## 8. Known gotchas / quirks to remember

1. **Edit tool truncates large C# files** — when modifying any of the runtime scripts via the LLM's Edit tool, the file often gets cut off mid-content and silently inserts NUL bytes. Workaround used throughout: write full files via Python via `mcp__workspace__bash`, verifying byte counts, null counts, and brace balance afterwards.
2. **UnityEvent lambdas don't survive scene save/reload.** Anywhere a Button.onClick or Slider.onValueChanged listener is registered with an inline lambda, do it in `DashboardUI.Start()` (runtime), NOT in the Editor scene builder. Lambdas added at build time are silently dropped when the scene is saved.
3. **Unity 6 default Input Handling is "Input System Package"**, but uGUI's EventSystem creates `StandaloneInputModule` which expects the old `Input` class. Fix: Edit → Project Settings → Player → Other Settings → Active Input Handling → **Both** → restart editor.
4. **URP shader colour property is `_BaseColor`**, not `_Color`. `CellGrid.colorProperty` defaults to `_BaseColor` — change in Inspector if needed.
5. **Blender Y-up vs Z-up handoff:** the FBX exporter is configured for Y-up; in Unity the imported prefab gets `rotation = Quaternion.Euler(-90, 0, 0)` to land flat.
6. **Unity Personal license** is fine for this project (educational, $0 revenue).

## 9. What's currently working end-to-end

- Play mode: SOC drops, temperature climbs, fan spins at 45 °C, alarm at 60 °C cuts current and snaps slider to 0.
- Fresh Pack / Worn Pack / Reset Sim buttons all work.
- 4×4 heatmap recolours per SOH.
- Range "Actual" diverges from "Healthy" when SOH < 1.
- All Console errors and the deprecated warning are cleared.

## 10. Pending fix (planned but not yet applied at handoff)

**Add a `timeScale` multiplier to BatteryPackController to make demos visible faster.**

Current real-world thermal mass means temperature rises ~0.005 °C/sec passively — too slow to demo. Plan was to add:

```csharp
[Header("Simulation")]
[Tooltip("Time acceleration factor. 1 = real-time; 30 = 1 real-sec = 30 sim-sec.")]
public float timeScale = 30f;

void FixedUpdate()
{
    float dt = Time.fixedDeltaTime * timeScale;   // <-- the only change
    ...
}
```

This makes SOC drop visibly during discharge and lets the ambient slider visibly affect cooling rate within seconds. Single one-line change in BatteryPackController.cs. Don't change anything else.

## 11. Next planned work (Sprint 4 and beyond)

- Per-cell coupled electro-thermal model (replace pack-lumped Q_gen with per-cell + neighbour conduction).
- Ion-flow particle system on `cross_section_*` assets.
- Result card UI (cause-and-effect summary at end of a scenario run).
- PBR materials on Blender assets, proper lighting.
- One more scenario preset: "Worn pack, abuse case" — high current + warm ambient.

## 12. How to verify state of the project

```bash
# In the workspace folder
cd "C:/Users/Shashi Kumar/Documents/Claude/Projects/DE Visulisation Project"
ls Assets/Scripts/      # should list 5 .cs files
ls Assets/Editor/       # should list SafeChargeSceneBuilder.cs
ls blender/             # should list build_pack.py and pack.fbx
```

In Unity:
1. Open `Assets/Scenes/SafeCharge_Demo.unity`.
2. Hierarchy should show: `Main Camera, Directional Light, Pack, Logic, DashboardCanvas, EventSystem`.
3. Press Play. All four widgets respond. Three scenario buttons all work.

If any of this is missing, the user should re-run `SafeCharge → Build Demo Scene` from the top menu.

---

*Last updated mid-Sprint 2 (May 2026). Working demo runs end-to-end. timeScale fix pending.*
