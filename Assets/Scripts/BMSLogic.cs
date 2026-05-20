// =============================================================================
// SafeCharge — BMSLogic
// =============================================================================
// Listens to BatteryPackController threshold events (45 °C cooling activation,
// 60 °C runaway alarm) and turns them into visible behaviour:
//   - spins the fan rotor when cooling is active
//   - exposes events for the UI banner system
// =============================================================================
using System;
using UnityEngine;

namespace SafeCharge
{
    public class BMSLogic : MonoBehaviour
    {
        public BatteryPackController controller;

        [Tooltip("Reference to the fan_rotor transform from the imported FBX.")]
        public Transform fanRotor;

        [Tooltip("Maximum rotor RPM when at full cooling load.")]
        public float maxRpm = 1500f;

        [Tooltip("Axis the rotor spins around in its local space.")]
        public Vector3 rotorAxis = Vector3.up;

        public bool CoolingOn   { get; private set; }
        public bool AlarmRaised { get; private set; }

        public event Action OnCoolingStarted;
        public event Action OnCoolingStopped;
        public event Action OnAlarmRaised;

        private float currentRpm = 0f;

        void Start()
        {
            if (controller == null) { enabled = false; return; }
            controller.OnCoolingActivated   += HandleCoolingOn;
            controller.OnCoolingDeactivated += HandleCoolingOff;
            controller.OnAlarmRaised        += HandleAlarm;
        }

        void OnDestroy()
        {
            if (controller == null) return;
            controller.OnCoolingActivated   -= HandleCoolingOn;
            controller.OnCoolingDeactivated -= HandleCoolingOff;
            controller.OnAlarmRaised        -= HandleAlarm;
        }

        void Update()
        {
            // Target RPM scales linearly between 45 °C and 60 °C while cooling.
            float target = 0f;
            if (CoolingOn && controller != null)
            {
                float t = Mathf.InverseLerp(
                    BatteryPackController.CoolingThresholdC,
                    BatteryPackController.AlarmThresholdC,
                    controller.T_pack);
                target = Mathf.Lerp(0.25f * maxRpm, maxRpm, t);
            }
            // Spool up / down smoothly so the fan doesn't jump.
            currentRpm = Mathf.MoveTowards(currentRpm, target, maxRpm * Time.deltaTime * 2f);

            if (fanRotor != null && currentRpm > 0.5f)
            {
                // 6 degrees per second per RPM (360 / 60)
                fanRotor.Rotate(rotorAxis, currentRpm * 6f * Time.deltaTime, Space.Self);
            }
        }

        private void HandleCoolingOn()
        {
            CoolingOn = true;
            OnCoolingStarted?.Invoke();
        }

        private void HandleCoolingOff()
        {
            CoolingOn = false;
            OnCoolingStopped?.Invoke();
        }

        private void HandleAlarm()
        {
            AlarmRaised = true;
            OnAlarmRaised?.Invoke();
        }
    }
}
