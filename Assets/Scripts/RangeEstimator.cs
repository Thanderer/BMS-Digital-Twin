// =============================================================================
// SafeCharge — RangeEstimator
// =============================================================================
// Computes EV driving range in km for the two reference cases the dashboard
// shows side-by-side:
//
//   E_usable (kWh) = V_nom · Q_pack_usable · SOC_displayed / 1000
//   Range    (km)  = E_usable / c_drive
//
// healthy = if SOH were 1.0;  actual = with the current weakest-cell SOH.
// =============================================================================
using System;
using UnityEngine;

namespace SafeCharge
{
    public class RangeEstimator : MonoBehaviour
    {
        public BatteryPackController controller;

        [Tooltip("Vehicle consumption in kWh/km. 0.18 is a typical mid-size EV.")]
        public float driveConsumption = 0.18f;

        public float RangeHealthyKm { get; private set; }
        public float RangeActualKm  { get; private set; }
        public float DeltaKm => RangeHealthyKm - RangeActualKm;

        /// <summary> healthy_km, actual_km. </summary>
        public event Action<float, float> OnRangeChanged;

        void Start()
        {
            if (controller == null) { enabled = false; return; }
            controller.OnSocChanged += OnAnyStateChanged;
            controller.OnSohChanged += _ => Recompute();
            Recompute();
        }

        void OnDestroy()
        {
            if (controller != null) controller.OnSocChanged -= OnAnyStateChanged;
        }

        private void OnAnyStateChanged(float _) => Recompute();

        private void Recompute()
        {
            if (controller == null || driveConsumption <= 0f) return;

            float socD = controller.SOC_displayed;
            float E_healthy = controller.V_nom * controller.Q_nom    * socD / 1000f;
            float E_actual  = controller.V_nom * controller.Q_usable * socD / 1000f;

            RangeHealthyKm = E_healthy / driveConsumption;
            RangeActualKm  = E_actual  / driveConsumption;

            OnRangeChanged?.Invoke(RangeHealthyKm, RangeActualKm);
        }
    }
}
