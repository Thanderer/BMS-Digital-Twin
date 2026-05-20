# SafeCharge — Full LLM Handoff Document
### BMS Digital Twin | Group 6 | OVGU Digital Engineering | Sprint 2 → Sprint 4

> **Purpose:** This document gives a new LLM complete context to continue development immediately — no re-deriving, no guesswork. Read this before touching any file.

---

## 1. Project Overview

| Field | Detail |
|-------|--------|
| **Course** | Digital Engineering, Summer Semester 2026, OVGU Magdeburg |
| **Team** | Group 6 — 6 students (CEE: Rishi, Avinash; DE: Jash, Shashi, Hari; DKE: Anant) |
| **Goal** | Interactive 3D digital twin of a Li-ion EV battery pack + BMS, showing how cell SOH degradation causes real range loss |
| **Tech stack** | Unity 6000.4.6f1 (HDRP, uGUI), Blender 4.x (scripted headless), LaTeX/Beamer (Overleaf) |
| **Repo** | https://github.com/Thanderer/BMS-Digital-Twin (private) |
| **Sprint cadence** | Every ~4 weeks; Sprint 2 delivered 22 May 2026 |

### Design Driver (Professor Feedback, Sprint 1)

> "If the app shows 80% SOC, that doesn't mean 80% usable — some cells may be weaker, limiting real capacity. Show the health of individual cells AND show what the display says vs. what's actually stored, expressed as how far the car will actually drive."

Every Sprint 2 decision flows from this. The dual SOC gauge, SOH heatmap, range estimator, and Worn Pack scenario all exist because of this feedback.

---

## 2. Repository Structure

```
BMS-Digital-Twin/
├── Assets/
│   ├── pack.fbx                          ← 3D battery pack model (import directly — already here)
│   ├── Scripts/
│   │   ├── BatteryPackController.cs      ← CORE: all physics, SOC, thermal, SOH, events
│   │   ├── BMSLogic.cs                   ← fan RPM control, alarm state relay
│   │   ├── CellGrid.cs                   ← maps SOH[] to 3D cell mesh colours
│   │   ├── DashboardUI.cs                ← all uGUI bindings, button handlers
│   │   └── RangeEstimator.cs             ← computes healthy vs actual EV range in km
│   └── Editor/
│       └── SafeChargeSceneBuilder.cs     ← Editor tool: SafeCharge → Build Demo Scene
├── Packages/
│   └── manifest.json                     ← all packages auto-resolved (HDRP 17.4.0, etc.)
├── ProjectSettings/
└── sprint-2/
    ├── blender/
    │   ├── pack.fbx                      ← same FBX (backup copy)
    │   ├── build_pack.py                 ← headless Blender script to regenerate FBX
    │   └── SETUP_blender.md
    ├── SafeCharge_Sprint2.tex            ← Beamer presentation source (17 slides)
    ├── SafeCharge_Sprint2.pdf            ← compiled PDF
    ├── INSTALL_GUIDE.md                  ← full Blender + Unity setup guide
    ├── UNITY_SETUP.md                    ← Unity-only quick setup guide
    └── LLM_HANDOFF.md                    ← this file
```

**Unity project root** (separate from workspace, on Shashi's machine):
`C:\Users\Shashi Kumar\Documents\DE Project_visualisation\Safe Charge\`

**Workspace / source root** (where Claude edits files):
`C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\`

---

## 3. Sprint Timeline

| Sprint | Date | Status | Key deliverable |
|--------|------|--------|----------------|
| Kickoff | 24 Apr 2026 | ✅ Done | Concept approved |
| Sprint 1 | 08 May 2026 | ✅ Done | Concept presentation accepted |
| **Sprint 2** | **22 May 2026** | **✅ Done** | Working Unity demo, Beamer deck |
| Sprint 4 | 19 Jun 2026 | 🔜 Planned | Per-cell thermal, particles, PBR materials |
| Sprint 6 | 17 Jul 2026 | 🔜 Planned | Result card UI, scenario presets |
| Sprint 8 | 14 Aug 2026 | 🔜 Planned | Bug fixing, final app |
| Final | 11 Sep 2026 | 🔜 | Report + demo |

**Strategy:** ~70% of work was front-loaded into Sprint 2. Each subsequent sprint reveals more of the already-working system and polishes presentation.

---

## 4. Physics & Equations (Implemented in BatteryPackController.cs)

All equations run in `FixedUpdate` with `dt = Time.fixedDeltaTime * timeScale`.

### 4.1 SOC Integration (Coulomb Counting)
```
SOC[k+1] = SOC[k] - (I · dt) / (3600 · Q_nom)
```
- `I` = effective current in Amperes (resolved through CC-CV logic, see §4.4)
- `Q_nom` = 100 Ah (nominal pack capacity)
- Source: Plett 2015

### 4.2 Thermal Model (Lumped)
```
Q_gen = I² · R_int · numCells          (Joule heating)
Q_out = h · A · (T_pack - T_ambient)   (convective cooling)
dT/dt = (Q_gen - Q_out) / (m · c_p)
T[k+1] = T[k] + dT · dt
```
- `R_int` = 0.030 Ω per cell
- `m` = 25 kg (pack mass)
- `c_p` = 800 J/kg·K (specific heat)
- `A` = 0.5 m² (surface area)
- `h` = 10 W/m²K passive, 40 W/m²K when fan active
- Source: Forgez et al. 2010; Bernardi et al. 1985

### 4.3 State of Health & Usable Capacity
```
SOH_pack    = min_j( cellSOH[j] )                    (weakest cell dominates)
Q_usable    = SOH_pack · Q_nom
SOC_actual  = SOC_displayed · SOH_pack
```
- 16 cells (4×4 grid), each with individual `cellSOH[j]`
- Source: Dubarry et al. 2009

### 4.4 CC-CV Charging Logic
```
if SOC_displayed > cvThreshold (0.80) AND charging:
    taper = (1 - SOC_displayed) / (1 - cvThreshold)
    I_effective = I_requested · clamp(taper, 0, 1)
else:
    I_effective = I_requested
```
- CC phase: full requested current until 80% SOC
- CV phase: tapers linearly to 0 at 100% SOC
- Auto-stop fires at SOC ≥ 0.999 (rounds to "100%" on display)

### 4.5 EV Range Estimation
```
E_healthy = V_nom · Q_nom    · SOC_displayed / 1000   (kWh)
E_actual  = V_nom · Q_usable · SOC_displayed / 1000   (kWh)
Range_healthy = E_healthy / c_drive
Range_actual  = E_actual  / c_drive
Delta_km      = Range_healthy - Range_actual
```
- `V_nom` = 350 V
- `c_drive` = 0.18 kWh/km (typical mid-size EV)
- Source: Larminie & Lowry 2012

### 4.6 Cycle-Life Degradation
```
_ahThroughput += |I| · dt / 3600
CycleCount = _ahThroughput / Q_nom

cellSOH[i] = max(0.50, 1 - _cellFadeRate[i] · CycleCount)

where _cellFadeRate[i] = kFadeBase + random_spread   (for i < 15)
      _cellFadeRate[15] = kFadeBase + kFadeSpread + kFadeWeak   (weakest cell)

kFadeBase = 0.0004  → SOH=80% at ~500 cycles
kFadeWeak = 0.0002  → weak cell hits 80% at ~333 cycles
```
- RNG seed is fixed (42) so degradation pattern is deterministic and reproducible

### 4.7 Thermal Thresholds & Hysteresis
```
Cooling ON  : T_pack > 45°C
Cooling OFF : T_pack < 42°C   (3°C hysteresis band — prevents oscillation)
Alarm ON    : T_pack > 60°C   (latches, cuts current to 0)
Alarm OFF   : T_pack < 45°C   (releases latch — pack cooled to safe level)
```

### 4.8 Fan RPM Model
```
t       = InverseLerp(45°C, 60°C, T_pack)
RPM_target = Lerp(0.25 · maxRPM, maxRPM, t)       (25%–100% range)
RPM_current = MoveTowards(RPM_current, RPM_target, maxRPM · 2 · deltaTime)  (smooth spool)
fan_rotor.Rotate(axis, RPM · 6° · deltaTime)       (6°/s per RPM = 360°/60s)
```
- `maxRPM` = 1500 (Inspector field on BMSLogic)

---

## 5. Runtime Architecture

`BatteryPackController` is the **single source of truth**. All state lives there. Everything else subscribes to its events — nothing polls.

```
BatteryPackController  (FixedUpdate — physics at 50Hz × timeScale)
  │
  ├─ OnSocChanged(float SOC_actual)
  │     └─ DashboardUI  → updates SOC labels, ring fills
  │     └─ RangeEstimator → triggers Recompute()
  │
  ├─ OnTemperatureChanged(float T)
  │     └─ DashboardUI  → temp label + temp bar fill
  │
  ├─ OnSohChanged(float[] cellSOH)
  │     └─ CellGrid     → recolours 16 cell meshes via MaterialPropertyBlock
  │     └─ DashboardUI  → recolours 16 heatmap Image tiles + SOH% labels
  │     └─ RangeEstimator → triggers Recompute()
  │
  ├─ OnCurrentOverridden(float newC)
  │     └─ DashboardUI  → snaps slider + label (BMS cut current due to alarm/charge-stop)
  │
  ├─ OnCycleCountChanged(float cycles)
  │     └─ DashboardUI  → cycle counter label
  │
  ├─ OnCoolingActivated / OnCoolingDeactivated
  │     └─ BMSLogic     → sets CoolingOn flag (fan RPM target changes in Update)
  │     └─ DashboardUI  → shows/hides yellow "▲ Cooling active" banner
  │
  ├─ OnCvModeEntered / OnCvModeExited
  │     └─ DashboardUI  → shows/hides blue "CV phase" banner
  │
  ├─ OnAlarmRaised / OnAlarmCleared
  │     └─ DashboardUI  → shows/hides red "⚠ Thermal runaway" banner
  │
  └─ OnChargeComplete
        └─ DashboardUI  → snaps slider to 0, shows green "✓ Charged" banner

RangeEstimator
  └─ OnRangeChanged(float healthy_km, float actual_km)
        └─ DashboardUI  → healthy/actual/delta range labels
```

---

## 6. Inspector Parameters (All Tunable at Runtime)

### BatteryPackController
| Field | Default | Effect |
|-------|---------|--------|
| `Q_nom` | 100 Ah | Nominal pack capacity |
| `V_nom` | 350 V | Nominal voltage |
| `R_int` | 0.030 Ω | Internal resistance per cell |
| `packMass` | 25 kg | Thermal mass |
| `specificHeat` | 800 J/kg·K | Heat capacity |
| `surfaceArea` | 0.5 m² | Cooling surface |
| `convPassive` | 10 W/m²K | Passive convection coefficient |
| `convActive` | 40 W/m²K | Active (fan) convection coefficient |
| `numCells` | 16 | Number of cells in pack |
| `timeScale` | 30 | Simulation speed (30 = 30 sim-seconds per real second) |
| `initialSOC` | 0.80 | Starting SOC |
| `initialTemp` | 25°C | Starting temperature |
| `cvThreshold` | 0.80 | SOC above which CV phase begins |
| `kFadeBase` | 0.0004 | Base SOH fade rate per cycle |
| `kFadeWeak` | 0.0002 | Extra fade rate for weakest cell |

### BMSLogic
| Field | Default | Effect |
|-------|---------|--------|
| `fanRotor` | (assigned) | Transform of `fan_rotor` in the FBX hierarchy |
| `maxRpm` | 1500 | Max fan RPM at 60°C |
| `rotorAxis` | Vector3.up | Local axis of rotation |

### RangeEstimator
| Field | Default | Effect |
|-------|---------|--------|
| `driveConsumption` | 0.18 kWh/km | Energy use per km (change for different vehicle) |

### CellGrid
| Field | Default | Effect |
|-------|---------|--------|
| `colorProperty` | `_BaseColor` | Shader property — use `_Color` for Built-in pipeline |

---

## 7. UI Layout (Built Procedurally by SafeChargeSceneBuilder)

```
┌─────────────── maroon title bar: "SafeCharge — BMS Digital Twin" ──────────────┐
│                                                                                  │
│  ┌─ SOC Panel ──────┐  ┌─ Cell SOH Heatmap ──┐  ┌─ EV Range ───────────────┐  │
│  │ 80% displayed    │  │  [4×4 grid of tiles] │  │  Healthy:  222 km        │  │
│  │ 80% actual       │  │  green=healthy        │  │  Actual:   178 km        │  │
│  │ T pack: 25.0 C   │  │  orange/red=degraded  │  │  -44 km lost to SOH     │  │
│  │ Cycles: 0.0      │  │  SOH% on each tile    │  │                          │  │
│  └──────────────────┘  └────────────────────── ┘  └──────────────────────────┘  │
│                                                                                  │
│  [yellow banner: ▲ Cooling active]  [red banner: ⚠ Thermal runaway]            │
│  [blue banner: ↗ CV charging]       [green banner: ✓ Charge complete]           │
│                                                                                  │
│  Current (C-rate) [slider: -5 .. +5]      Ambient (°C) [slider: -10 .. +50]   │
│                                                                                  │
│  Speed: [1x]  [30x]  [50x]     [Fresh Pack]  [Worn Pack]                       │
│                                  [Reset Cycle] [Full Reset]                     │
└──────────────────────────────────────────────────────────────────────────────────┘
              (HDRP 3D scene: battery pack model in background)
```

**SOH → colour mapping (CellGrid.SohToColor):**
```
SOH ≥ 0.90  →  green       (0.36, 0.66, 0.36)
SOH ≥ 0.85  →  light green (0.71, 0.83, 0.42)
SOH ≥ 0.80  →  yellow      (0.91, 0.77, 0.28)
SOH ≥ 0.75  →  orange      (0.85, 0.48, 0.25)
SOH  < 0.75 →  red         (0.72, 0.24, 0.18)
```

---

## 8. Button Behaviours

| Button | Method called | Effect |
|--------|--------------|--------|
| **Fresh Pack** | `ResetToFreshPack()` | All cellSOH → 1.0, clears alarm/cooling, releases `_scenarioLocked` |
| **Worn Pack** | `ApplyAgedScenario(15, 0.72f, 0.88f)` | Cell 15 → 0.72, rest → 0.88, sets `_scenarioLocked = true` |
| **Reset Cycle** | `ResetCycleCount()` | Zeroes CycleCount + Ah throughput, releases `_scenarioLocked` |
| **Full Reset** | `ResetSimulation()` | Everything back to initial state, sliders snap to 0/25, all banners hide |
| **1x / 30x / 50x** | `SetTimeScale(n)` | Changes simulation speed multiplier |

---

## 9. Bugs Fixed This Session (Do Not Reintroduce)

### Bug 1: WornPack Blinking
**Symptom:** Clicking "Worn Pack" caused the heatmap to blink rapidly between worn and fresh states.

**Root cause:** `ApplyAgedScenario` set `cellSOH` manually, but `FixedUpdate`'s degradation loop ran every physics frame (50Hz × 30 timeScale) and recalculated `SOH = 1 - kFadeRate × CycleCount`. With `CycleCount = 0`, this gave `SOH = 1.0` for all cells, instantly overwriting the worn values. Net effect: worn→fresh every frame = visible blink.

**Fix applied:** Added `private bool _scenarioLocked` to `BatteryPackController`. `ApplyAgedScenario` sets it `true`, which skips the degradation loop in `FixedUpdate`. `ResetToFreshPack`, `ResetCycleCount`, and `ResetSimulation` all set it `false`.

```csharp
// In FixedUpdate — degradation is now guarded:
if (!_scenarioLocked)
{
    for (int i = 0; i < cellSOH.Length; i++)
    {
        float degraded = Mathf.Max(0.50f, 1f - _cellFadeRate[i] * CycleCount);
        if (!Mathf.Approximately(cellSOH[i], degraded)) { cellSOH[i] = degraded; sohDirty = true; }
    }
    if (sohDirty) OnSohChanged?.Invoke((float[])cellSOH.Clone());
}
```

### Bug 2: LaTeX Beamer Compilation Errors
Three classes of errors fixed in `SafeCharge_Sprint2.tex`:

**a) PGF Math `cm` operator error:**
- Wrong: `yshift={-0.52 - \i*0.73}cm` — PGF tries to evaluate `cm` as an operator
- Fixed: `\pgfmathparse{-0.52-\i*0.73}` then `yshift=\pgfmathresult cm`

**b) Undefined color `warn ` (with trailing space):**
- Wrong: `foreach` last item `warn` without trailing `%` — LaTeX picks up the newline after `warn` as part of the color name
- Fixed: `3/0.72/warn%` (trailing `%` suppresses the newline)

**c) `\mathrm` outside math mode in table cell:**
- Wrong: `\mathrm{SOC_{cv}}` in a tabular cell
- Fixed: `$\mathrm{SOC_{cv}}$`

### Bug 3: NUL Bytes in C# Files
**Symptom:** Unity fails to compile scripts with invisible NUL byte characters embedded in the file.

**Root cause:** The LLM's `Edit` and `Write` tools truncate large files and inject `\x00` NUL bytes silently.

**Permanent workaround:** Always write large C# files via Python in bash:
```bash
python3 - << 'PYEOF'
path = "/path/to/file.cs"
with open(path, 'rb') as f:
    src = f.read().replace(b'\x00', b'').decode('utf-8')
# ... make string replacements ...
with open(path, 'wb') as f:
    f.write(src.encode('utf-8'))
PYEOF
```

### Bug 4: pack.fbx Not Found on Fresh Clone
**Symptom:** `SafeCharge → Build Demo Scene` showed "pack.fbx not found" dialog.

**Root cause:** `pack.fbx` was only committed to `sprint-2/blender/` but `SafeChargeSceneBuilder` searches `Assets/`.

**Fix:** Committed `pack.fbx` directly to `Assets/pack.fbx` in the repo. No manual copy needed after cloning.

---

## 10. Critical Gotchas — Read Before Editing

1. **Never use the Edit/Write tool for large C# files.** They inject NUL bytes and truncate content silently. Always use Python via bash (`mcp__workspace__bash`). Verify after writing: check byte count and brace balance.

2. **UnityEvent lambdas added in the Editor script are dropped on scene save.** All `Button.onClick.AddListener` and `Slider.onValueChanged.AddListener` calls must be in `DashboardUI.Start()` (runtime), NOT in `SafeChargeSceneBuilder`. This is why `SafeChargeSceneBuilder` only creates and positions GameObjects — it never wires runtime events.

3. **HDRP shader colour property is `_BaseColor`, not `_Color`.** Built-in pipeline uses `_Color`. If cells don't recolour on a new machine, check `CellGrid.colorProperty` in the Inspector.

4. **Unity 6 Input System:** Project Settings → Player → Active Input Handling must be set to **Both** (not just "Input System Package") or uGUI's `StandaloneInputModule` will fail silently.

5. **Blender Z-up → Unity Y-up:** `build_pack.py` exports FBX with Z-up. Unity's FBX importer converts to Y-up automatically. `SafeChargeSceneBuilder` compensates with `Quaternion.Euler(-90, 0, 0)` on the Pack root — do not remove this.

6. **`_scenarioLocked` must be cleared before cycle-based degradation can resume.** If a user clicks Worn Pack and then runs the simulation for a while, CycleCount accumulates but SOH doesn't update. This is intentional — the scenario is a demonstration snapshot. Any reset button clears the lock.

7. **ChargeStopSoc is 0.999, not 1.0.** CV taper means SOC asymptotically approaches 1.0 and may never reach exactly 1.0 in a demo timeframe. The stop fires when display rounds to "100%".

8. **`timeScale` is the simulation speed multiplier.** Default is 30 (30 simulated seconds per real second). At `timeScale = 1`, thermal changes take hours — useless for demo. Don't set below 10.

9. **Fan rotor reference must point to `Pack/fan_rotor`.** SafeChargeSceneBuilder wires this via `pack.transform.Find("fan_rotor")`. If FBX is re-imported with different internal names, this will be null and the fan won't spin.

10. **HDRP Wizard + TextMeshPro import are required on every new machine.** Without HDRP Wizard → Fix All, the scene is black. Without TMP Essential Resources, all text is invisible. Both are one-click steps but easy to forget.

---

## 11. Blender Asset Pipeline

`build_pack.py` runs headless (no GUI) and produces `pack.fbx` containing 22 mesh objects:

```
pack_shell              — hollow prismatic casing
cell_00 … cell_15       — 16 prismatic Li-ion cells (4×4 grid)
fan_housing             — outer fan housing
fan_rotor               — 5-blade rotor (separate so Unity can spin it)
cross_section_anode     — flat strip (educational cross-section view)
cross_section_separator — flat strip
cross_section_cathode   — flat strip
```

**Run command:**
```bash
blender --background --python build_pack.py -- --rows 4 --cols 4 --out pack.fbx
```

**Key conventions baked into the script:**
- Z-up, 1 unit = 1 metre, all transforms applied
- Cell names are exactly `cell_00` through `cell_15` — `CellGrid.cs` searches for these exact names
- `fan_rotor` is the exact name `BMSLogic` and `SafeChargeSceneBuilder` look for
- Single material slot per object (Unity replaces materials on import)

---

## 12. What's Working End-to-End (Sprint 2)

- ✅ SOC drops/rises correctly when current slider is moved
- ✅ CC-CV charging: taper visible above 80% SOC, auto-stop at 100%
- ✅ Temperature rises with I²R heating, falls with cooling
- ✅ Fan spins at 45°C (speed scales with temperature up to 60°C), stops at 42°C
- ✅ Thermal alarm at 60°C: cuts current, shows red banner, locks until cooled to 45°C
- ✅ Fresh Pack button: restores SOH=1.0, clears banners
- ✅ Worn Pack button: sets cell 15 to 0.72, rest to 0.88, heatmap recolours — FIXED (no blink)
- ✅ Reset Cycle button: zeroes cycle count
- ✅ Full Reset: everything back to initial state
- ✅ 4×4 heatmap recolours per-cell SOH in real time
- ✅ Range "Actual" drops below "Healthy" when SOH < 1.0
- ✅ Speed buttons (1x / 30x / 50x) work
- ✅ All 16 cell meshes in 3D model recolour via MaterialPropertyBlock (no material instancing)
- ✅ Cycle degradation model: cells gradually degrade over simulated cycles
- ✅ 17-slide Beamer LaTeX presentation compiles clean in Overleaf (pdfLaTeX)

---

## 13. Planned Work — Sprint 4 and Beyond

### Sprint 4 (June 2026)
- **Per-cell coupled electro-thermal model:** replace pack-lumped `Q_gen` with individual cell heat + neighbour conduction matrix. Each cell gets its own temperature.
- **Ion-flow particle system:** particles flowing through `cross_section_*` assets to visualise Li-ion movement during charge/discharge.
- **PBR materials:** replace placeholder materials in Blender with proper PBR (metallic casing, translucent separator, etc.).
- **Lighting setup:** HDRP area lights + emissive cells when in alarm state.

### Sprint 6 (July 2026)
- **Result card UI:** after a scenario run completes, show a summary card (cause, effect, predicted degradation timeline).
- **Scenario presets:** "Worn pack abuse case" — high current + warm ambient pre-loaded.
- **Per-cell temperature display:** extend heatmap to show temperature per cell, not just SOH.

### Sprint 8 (August 2026)
- Polish, bug fixing, performance profiling.
- Build for Windows standalone.

### Final (September 2026)
- Written report + live demo.

---

## 14. How to Verify Everything Is Working

**In Unity, after opening `Assets/Scenes/SafeCharge_Demo.unity`:**

Hierarchy should contain exactly:
```
Main Camera
Directional Light
Pack              ← FBX root; children include cell_00..cell_15, fan_rotor
Logic             ← has BatteryPackController, CellGrid, RangeEstimator, BMSLogic
DashboardCanvas   ← all UI lives here
EventSystem
```

**Smoke test sequence:**
1. Press ▶ Play — dashboard appears, all 16 tiles green, range shows ~222 km
2. Drag Current to +2.0 — SOC starts dropping slowly (timeScale=30, so visible in seconds)
3. Drag Ambient to 50°C — temperature climbs faster; fan banner appears at 45°C
4. Click **Worn Pack** — cell 15 tile turns red/orange, "Actual" range drops, stays stable (no blink)
5. Click **Full Reset** — everything snaps back to initial state, all tiles green
6. Click **Fresh Pack** then drag Current to -2.0 (charge) — SOC rises, CV banner appears above 80%

If all 6 steps work without Console errors, Sprint 2 is fully functional.

---

## 15. File Write Rules for This Project

When writing or modifying C# files, **always use Python via bash** — never the Edit or Write tools directly on `.cs` files:

```bash
python3 - << 'PYEOF'
path = "/sessions/magical-pensive-rubin/mnt/DE Visulisation Project/Assets/Scripts/FILENAME.cs"
with open(path, 'rb') as f:
    src = f.read().replace(b'\x00', b'').decode('utf-8')

# Make targeted string replacements:
src = src.replace('OLD_STRING', 'NEW_STRING')

with open(path, 'wb') as f:
    f.write(src.encode('utf-8'))

# Verify:
print(f"Bytes: {len(src.encode('utf-8'))}")
print(f"NUL count: {src.count(chr(0))}")
print(f"Open braces: {src.count('{')}, Close braces: {src.count('}')}")
PYEOF
```

After any script change, copy to the repo and push:
```bash
cp "/sessions/magical-pensive-rubin/mnt/DE Visulisation Project/Assets/Scripts/FILENAME.cs" \
   /tmp/BMS-Digital-Twin/Assets/Scripts/FILENAME.cs
cd /tmp/BMS-Digital-Twin && git add -A && git commit -m "fix: ..." && git push origin main
```

---

## 16. Path Reference (Bash Sandbox)

| Windows path | Bash sandbox path |
|---|---|
| `C:\Users\Shashi Kumar\Documents\Claude\Projects\DE Visulisation Project\` | `/sessions/magical-pensive-rubin/mnt/DE Visulisation Project/` |
| `...\Assets\Scripts\` | `/sessions/magical-pensive-rubin/mnt/DE Visulisation Project/Assets/Scripts/` |
| Outputs / temp work | `/sessions/magical-pensive-rubin/mnt/outputs/` |
| Skills folder | `/sessions/magical-pensive-rubin/mnt/.claude/skills/` |
| Cloned repo | `/tmp/BMS-Digital-Twin/` |

GitHub token for push: ask Shashi — do not store in any file.

---

*Last updated: Sprint 2, 21 May 2026. All Sprint 2 deliverables complete. Next: Sprint 4 per-cell thermal model.*
