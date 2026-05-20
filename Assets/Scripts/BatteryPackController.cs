// =============================================================================
// SafeCharge - BatteryPackController
// =============================================================================
using System;
using UnityEngine;

namespace SafeCharge
{
    public enum BmsMode { Normal, AggressiveCool, EcoCharge }

    public class BatteryPackController : MonoBehaviour
    {
        [Header("Pack configuration")]
        public float Q_nom       = 100f;
        public float V_nom       = 350f;
        public float R_int       = 0.030f;
        public float packMass    = 25f;
        public float specificHeat = 800f;
        public float surfaceArea = 0.5f;
        public float convPassive = 10f;
        public float convActive  = 40f;
        public int   numCells    = 16;

        [Header("Simulation speed")]
        [Tooltip("Time acceleration factor. 1 = real-time; 30 = 30x; 50 = 50x.")]
        public float timeScale = 30f;

        [Header("Initial state")]
        [Range(0f, 1f)] public float initialSOC   = 0.80f;
        public float initialTemp  = 25f;
        [Range(-10f, 50f)] public float ambientTemp = 25f;
        [Range(-5f,   5f)] public float currentC    = 0f;
        public BmsMode bmsMode = BmsMode.Normal;

        [Header("CC-CV charging")]
        [Tooltip("SOC threshold above which current tapers (CV phase). Typical: 0.80.")]
        [Range(0.5f, 0.95f)] public float cvThreshold = 0.80f;

        [Header("State of Health (per cell)")]
        public float[] cellSOH;

        [Header("Cycle-life degradation")]
        [Tooltip("Base capacity-fade per equivalent full cycle. 0.0004 => 80% SOH at 500 cycles.")]
        public float kFadeBase = 0.0004f;
        [Tooltip("Extra fade rate added to the single weakest cell to model non-uniformity.")]
        public float kFadeWeak = 0.0002f;

        // Thresholds
        public const float CoolingThresholdC    = 45f;  // fan turns ON above this
        public const float CoolingHysteresisC  = 42f;  // fan turns OFF below this (3 C band)
        public const float AlarmThresholdC     = 60f;

        // Auto-stop fires when SOC rounds to 100% on the display (F0 format).
        // Using 0.999 avoids waiting for the float to reach exactly 1.0 in CV taper.
        private const float ChargeStopSoc = 0.999f;

        // Public state (read-only)
        public float SOC_displayed  { get; private set; }
        public float T_pack         { get; private set; }
        public bool  CoolingActive  { get; private set; }
        public bool  AlarmActive    { get; private set; }
        public bool  CvModeActive   { get; private set; }

        /// <summary>Cumulative equivalent full cycles (throughput-based).</summary>
        public float CycleCount { get; private set; }

        private float   _requestedCurrentC;
        private float[] _cellFadeRate;
        private float   _ahThroughput;
        private bool    _scenarioLocked;   // true while a manual scenario (WornPack) is active

        // Derived properties
        public float SOH_pack
        {
            get
            {
                if (cellSOH == null || cellSOH.Length == 0) return 1f;
                float m = 1f;
                foreach (var v in cellSOH) if (v < m) m = v;
                return Mathf.Clamp01(m);
            }
        }
        public float Q_usable   => SOH_pack * Q_nom;
        public float SOC_actual => SOC_displayed * SOH_pack;

        // Events
        public event Action<float>   OnSocChanged;
        public event Action<float>   OnTemperatureChanged;
        public event Action<float[]> OnSohChanged;
        public event Action<float>   OnCurrentOverridden;
        public event Action<float>   OnCycleCountChanged;
        public event Action OnCoolingActivated;
        public event Action OnCoolingDeactivated;
        public event Action OnCvModeEntered;
        public event Action OnCvModeExited;
        public event Action OnAlarmRaised;
        public event Action OnAlarmCleared;
        public event Action OnChargeComplete;

        // Unity lifecycle
        void Awake()
        {
            if (cellSOH == null || cellSOH.Length != numCells)
            {
                cellSOH = new float[numCells];
                for (int i = 0; i < numCells; i++) cellSOH[i] = 1f;
            }
            RebuildFadeRates();
        }

        // Builds (or rebuilds) _cellFadeRate to match cellSOH.Length.
        // Called from Awake AND FixedUpdate guard to survive Unity hot-reload.
        void RebuildFadeRates()
        {
            int n = cellSOH != null ? cellSOH.Length : numCells;
            _cellFadeRate = new float[n];
            var rng = new System.Random(42);
            for (int i = 0; i < n; i++)
            {
                float spread = (float)(rng.NextDouble() * 0.2 - 0.1) * kFadeBase;
                _cellFadeRate[i] = kFadeBase + spread;
            }
            if (n > 0) _cellFadeRate[n - 1] += kFadeWeak;
        }

        void Start()
        {
            SOC_displayed      = Mathf.Clamp01(initialSOC);
            T_pack             = initialTemp;
            CoolingActive      = false;
            AlarmActive        = false;
            CvModeActive       = false;
            _requestedCurrentC = currentC;
            _ahThroughput      = 0f;
            CycleCount         = 0f;

            OnSohChanged?.Invoke((float[])cellSOH.Clone());
            OnSocChanged?.Invoke(SOC_actual);
            OnTemperatureChanged?.Invoke(T_pack);
            OnCycleCountChanged?.Invoke(CycleCount);
        }

        void FixedUpdate()
        {
            // Guard: Unity hot-reload resets private fields; rebuild if needed.
            if (_cellFadeRate == null || _cellFadeRate.Length != cellSOH.Length)
                RebuildFadeRates();

            float dt = Time.fixedDeltaTime * timeScale;

            // Resolve effective current (CC-CV + alarm cutoff)
            float I = ResolveEffectiveCurrent();

            // SOC integration
            float dSOC = -(I * dt) / (3600f * Q_nom);
            SOC_displayed = Mathf.Clamp01(SOC_displayed + dSOC);

            // Throughput-based cycle counting
            _ahThroughput += Mathf.Abs(I) * dt / 3600f;
            float newCycles = _ahThroughput / Q_nom;
            if (!Mathf.Approximately(newCycles, CycleCount))
            {
                CycleCount = newCycles;
                OnCycleCountChanged?.Invoke(CycleCount);
            }

            // Per-cell SOH degradation (skipped when a manual scenario is locked in)
            bool sohDirty = false;
            if (!_scenarioLocked)
            {
                for (int i = 0; i < cellSOH.Length; i++)
                {
                    float degraded = Mathf.Max(0.50f, 1f - _cellFadeRate[i] * CycleCount);
                    if (!Mathf.Approximately(cellSOH[i], degraded))
                    {
                        cellSOH[i] = degraded;
                        sohDirty   = true;
                    }
                }
                if (sohDirty) OnSohChanged?.Invoke((float[])cellSOH.Clone());
            }

            // Charge-complete auto-stop.
            // Use ChargeStopSoc (0.999) instead of 1.0 so the stop fires when
            // the display rounds to "100%" — CC-CV taper means the float may
            // never reach exactly 1.0 in a reasonable demo timeframe.
            if (SOC_displayed >= ChargeStopSoc && _requestedCurrentC < 0f)
            {
                _requestedCurrentC = 0f;
                currentC           = 0f;
                OnCurrentOverridden?.Invoke(0f);
                OnChargeComplete?.Invoke();
            }

            // Thermal model
            float Q_gen = I * I * R_int * numCells;
            float h     = CoolingActive ? convActive : convPassive;
            float Q_out = h * surfaceArea * (T_pack - ambientTemp);
            float dT    = (Q_gen - Q_out) * dt / (packMass * specificHeat);
            T_pack += dT;

            // Cooling threshold
            // Hysteresis band: ON above 45 C, OFF only below 42 C.
            // Prevents bang-bang oscillation at the threshold.
            if (!CoolingActive && T_pack > CoolingThresholdC)
            {
                CoolingActive = true;
                OnCoolingActivated?.Invoke();
            }
            else if (CoolingActive && T_pack < CoolingHysteresisC)
            {
                CoolingActive = false;
                OnCoolingDeactivated?.Invoke();
            }

            // Thermal alarm: latches on above 60 C.
            // Auto-clears when temperature recovers below CoolingThresholdC (45 C),
            // so the user can charge again after the pack has cooled down.
            if (T_pack > AlarmThresholdC && !AlarmActive)
            {
                AlarmActive        = true;
                currentC           = 0f;
                _requestedCurrentC = 0f;
                OnCurrentOverridden?.Invoke(0f);
                OnAlarmRaised?.Invoke();
            }
            else if (AlarmActive && T_pack < CoolingThresholdC)
            {
                // Temperature has recovered to safe level — release the latch.
                AlarmActive = false;
                OnAlarmCleared?.Invoke();
            }

            OnSocChanged?.Invoke(SOC_actual);
            OnTemperatureChanged?.Invoke(T_pack);
        }

        // CC-CV current resolver
        private float ResolveEffectiveCurrent()
        {
            if (AlarmActive) return 0f;

            float I = _requestedCurrentC * Q_nom;

            // CV phase: charging (I < 0) above cvThreshold
            bool inCv = (I < 0f) && (SOC_displayed > cvThreshold);
            if (inCv)
            {
                float taper = (1f - SOC_displayed) / (1f - cvThreshold);
                I *= Mathf.Clamp01(taper);
            }

            if (inCv && !CvModeActive)
            {
                CvModeActive = true;
                OnCvModeEntered?.Invoke();
            }
            else if (!inCv && CvModeActive)
            {
                CvModeActive = false;
                OnCvModeExited?.Invoke();
            }

            return I;
        }

        // Public setters
        public void SetCurrent(float c)
        {
            _requestedCurrentC = Mathf.Clamp(c, -5f, 5f);
            currentC           = _requestedCurrentC;
        }
        public void SetAmbient(float t)   { ambientTemp = Mathf.Clamp(t, -10f, 50f); }
        public void SetTimeScale(float s) { timeScale   = Mathf.Clamp(s, 1f, 200f); }

        public void SetCellSOH(int idx, float value)
        {
            if (cellSOH == null || idx < 0 || idx >= cellSOH.Length) return;
            cellSOH[idx] = Mathf.Clamp01(value);
            OnSohChanged?.Invoke((float[])cellSOH.Clone());
            OnSocChanged?.Invoke(SOC_actual);
        }

        public void SetAllCellSOH(float[] values)
        {
            if (values == null) return;
            int n = Mathf.Min(values.Length, cellSOH.Length);
            for (int i = 0; i < n; i++) cellSOH[i] = Mathf.Clamp01(values[i]);
            OnSohChanged?.Invoke((float[])cellSOH.Clone());
            OnSocChanged?.Invoke(SOC_actual);
        }

        public void ApplyAgedScenario(int weakIdx, float weakSoh, float averageSoh)
        {
            _scenarioLocked = true;   // freeze degradation loop so it doesn't overwrite these values
            for (int i = 0; i < cellSOH.Length; i++)
                cellSOH[i] = (i == weakIdx) ? weakSoh : averageSoh;
            OnSohChanged?.Invoke((float[])cellSOH.Clone());
            OnSocChanged?.Invoke(SOC_actual);
        }

        // Reset methods

        /// <summary>Restore all cells to SOH = 1.0. Does NOT touch SOC, temperature,
        /// current, or cycle count.</summary>
        public void ResetToFreshPack()
        {
            _scenarioLocked = false;
            if (cellSOH == null) return;
            for (int i = 0; i < cellSOH.Length; i++) cellSOH[i] = 1f;
            bool wasCooling = CoolingActive;
            AlarmActive   = false;
            CoolingActive = false;
            if (wasCooling) OnCoolingDeactivated?.Invoke();
            OnSohChanged?.Invoke((float[])cellSOH.Clone());
            OnSocChanged?.Invoke(SOC_actual);
        }

        /// <summary>Zero the cycle counter and Ah throughput only.
        /// SOH, SOC, temperature, and slider are all untouched.</summary>
        public void ResetCycleCount()
        {
            _scenarioLocked = false;
            _ahThroughput = 0f;
            CycleCount    = 0f;
            OnCycleCountChanged?.Invoke(CycleCount);
        }

        /// <summary>Full factory reset: SOC, temperature, current, SOH, cycle count —
        /// everything back to initial values.</summary>
        public void ResetSimulation()
        {
            _scenarioLocked = false;
            bool wasCooling = CoolingActive;

            SOC_displayed      = Mathf.Clamp01(initialSOC);
            T_pack             = initialTemp;
            currentC           = 0f;
            _requestedCurrentC = 0f;
            CoolingActive      = false;
            AlarmActive        = false;
            _ahThroughput      = 0f;
            CycleCount         = 0f;

            if (cellSOH != null)
                for (int i = 0; i < cellSOH.Length; i++) cellSOH[i] = 1f;

            if (wasCooling)   OnCoolingDeactivated?.Invoke();
            if (CvModeActive) { CvModeActive = false; OnCvModeExited?.Invoke(); }
            OnCurrentOverridden?.Invoke(0f);
            OnCycleCountChanged?.Invoke(CycleCount);
            OnSohChanged?.Invoke((float[])cellSOH.Clone());
            OnSocChanged?.Invoke(SOC_actual);
            OnTemperatureChanged?.Invoke(T_pack);
        }
    }
}
