// =============================================================================
// SafeCharge - DashboardUI
// =============================================================================
using UnityEngine;
using UnityEngine.UI;

namespace SafeCharge
{
    public class DashboardUI : MonoBehaviour
    {
        [Header("Controller refs")]
        public BatteryPackController controller;
        public RangeEstimator        rangeEstimator;
        public BMSLogic              bmsLogic;

        [Header("SOC readouts")]
        public Text  socDisplayedLabel;
        public Text  socActualLabel;
        public Image socOuterRing;
        public Image socInnerRing;

        [Header("Temperature")]
        public Text  tempLabel;
        public Image tempBar;

        [Header("Cycle counter")]
        public Text cycleCountLabel;

        [Header("Cell heatmap (16 Image tiles + 16 SOH Text labels)")]
        public Image[] heatmapTiles;
        public Text[]  heatmapSohLabels;

        [Header("EV range readouts")]
        public Text rangeHealthyLabel;
        public Text rangeActualLabel;
        public Text rangeDeltaLabel;

        [Header("Warning banners")]
        public GameObject coolingBanner;
        public GameObject alarmBanner;
        public GameObject cvModeBanner;
        public GameObject chargeCompleteBanner;

        [Header("Inputs")]
        public Slider currentSlider;
        public Slider ambientSlider;
        public Text   currentValueLabel;
        public Text   ambientValueLabel;

        [Header("Scenario buttons")]
        public Button freshPackButton;
        public Button wornPackButton;

        [Header("Reset buttons")]
        public Button resetCycleButton;
        public Button fullResetButton;

        [Header("Speed buttons (1x / 30x / 50x)")]
        public Button speed1xButton;
        public Button speed30xButton;
        public Button speed50xButton;

        private static readonly Color ColActive   = new Color(0.53f, 0.09f, 0.09f);
        private static readonly Color ColInactive = new Color(0.25f, 0.25f, 0.25f);

        void Start()
        {
            if (controller != null)
            {
                controller.OnSocChanged          += OnSocChanged;
                controller.OnTemperatureChanged  += OnTempChanged;
                controller.OnSohChanged          += OnSohChanged;
                controller.OnCurrentOverridden   += OnCurrentOverridden;
                controller.OnCycleCountChanged   += OnCycleCountChanged;
                controller.OnCoolingActivated    += () => SetBanner(coolingBanner, true);
                controller.OnCoolingDeactivated  += () => SetBanner(coolingBanner, false);
                controller.OnCvModeEntered       += () => SetBanner(cvModeBanner, true);
                controller.OnCvModeExited        += () => SetBanner(cvModeBanner, false);
                controller.OnAlarmRaised         += () => SetBanner(alarmBanner, true);
                controller.OnAlarmCleared        += () => SetBanner(alarmBanner, false);
                controller.OnChargeComplete      += OnChargeComplete;
            }

            if (rangeEstimator != null)
                rangeEstimator.OnRangeChanged += OnRangeChanged;

            // Current slider — right = charge, left = discharge
            if (currentSlider != null)
            {
                currentSlider.minValue = -5f;
                currentSlider.maxValue =  5f;
                if (controller != null) currentSlider.value = controller.currentC;
                currentSlider.onValueChanged.AddListener(v =>
                {
                    // Negate: slider right (positive UI) = charging = negative C-rate in physics
                    if (controller != null) controller.SetCurrent(-v);
                    if (currentValueLabel != null)
                        currentValueLabel.text = SliderLabel(v);
                });
                if (currentValueLabel != null)
                    currentValueLabel.text = SliderLabel(currentSlider.value);
            }

            // Ambient slider
            if (ambientSlider != null)
            {
                ambientSlider.minValue = -10f;
                ambientSlider.maxValue =  50f;
                if (controller != null) ambientSlider.value = controller.ambientTemp;
                ambientSlider.onValueChanged.AddListener(v =>
                {
                    if (controller != null) controller.SetAmbient(v);
                    if (ambientValueLabel != null)
                        ambientValueLabel.text = v.ToString("F0") + " C";
                });
                if (ambientValueLabel != null)
                    ambientValueLabel.text = ambientSlider.value.ToString("F0") + " C";
            }

            // Scenario buttons
            if (freshPackButton != null) freshPackButton.onClick.AddListener(OnFreshPackClicked);
            if (wornPackButton  != null) wornPackButton.onClick.AddListener(OnWornPackClicked);

            // Reset buttons
            if (resetCycleButton != null) resetCycleButton.onClick.AddListener(OnResetCycleClicked);
            if (fullResetButton  != null) fullResetButton.onClick.AddListener(OnFullResetClicked);

            // Speed buttons
            if (speed1xButton  != null) speed1xButton.onClick.AddListener(()  => OnSpeedClicked(1f));
            if (speed30xButton != null) speed30xButton.onClick.AddListener(() => OnSpeedClicked(30f));
            if (speed50xButton != null) speed50xButton.onClick.AddListener(() => OnSpeedClicked(50f));

            RefreshSpeedButtons(controller != null ? controller.timeScale : 30f);

            SetBanner(coolingBanner,        false);
            SetBanner(alarmBanner,          false);
            SetBanner(cvModeBanner,         false);
            SetBanner(chargeCompleteBanner, false);
        }

        void OnDestroy()
        {
            if (controller != null)
            {
                controller.OnSocChanged         -= OnSocChanged;
                controller.OnTemperatureChanged -= OnTempChanged;
                controller.OnSohChanged         -= OnSohChanged;
                controller.OnCurrentOverridden  -= OnCurrentOverridden;
                controller.OnCycleCountChanged  -= OnCycleCountChanged;
            }
            if (rangeEstimator != null)
                rangeEstimator.OnRangeChanged -= OnRangeChanged;
        }

        // Button handlers

        public void OnFreshPackClicked()
        {
            if (controller == null) return;
            controller.ResetToFreshPack();
            SetBanner(coolingBanner, false);
            SetBanner(alarmBanner,   false);
        }

        public void OnWornPackClicked()
        {
            if (controller == null) return;
            controller.ApplyAgedScenario(15, 0.72f, 0.88f);
        }

        public void OnResetCycleClicked()
        {
            if (controller == null) return;
            controller.ResetCycleCount();
        }

        public void OnFullResetClicked()
        {
            if (controller == null) return;
            controller.ResetSimulation();
            if (currentSlider  != null) currentSlider.SetValueWithoutNotify(0f);
            if (ambientSlider  != null) ambientSlider.SetValueWithoutNotify(25f);
            if (currentValueLabel != null) currentValueLabel.text = SliderLabel(0f);
            if (ambientValueLabel != null) ambientValueLabel.text = "25 C";
            SetBanner(coolingBanner,        false);
            SetBanner(alarmBanner,          false);
            SetBanner(cvModeBanner,         false);
            SetBanner(chargeCompleteBanner, false);
        }

        private void OnSpeedClicked(float scale)
        {
            if (controller != null) controller.SetTimeScale(scale);
            RefreshSpeedButtons(scale);
        }

        private void RefreshSpeedButtons(float scale)
        {
            SetSpeedButtonColor(speed1xButton,  Mathf.Approximately(scale, 1f));
            SetSpeedButtonColor(speed30xButton, Mathf.Approximately(scale, 30f));
            SetSpeedButtonColor(speed50xButton, Mathf.Approximately(scale, 50f));
        }

        private static void SetSpeedButtonColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? ColActive : ColInactive;
        }

        // Event handlers

        void OnCurrentOverridden(float newCurrent)
        {
            // newCurrent from controller is in physics convention (negative = charge).
            // Slider is in UI convention (positive = charge), so negate back.
            float sliderVal = -newCurrent;
            if (currentSlider     != null) currentSlider.SetValueWithoutNotify(sliderVal);
            if (currentValueLabel != null) currentValueLabel.text = SliderLabel(sliderVal);
        }

        void OnChargeComplete()
        {
            if (currentSlider     != null) currentSlider.SetValueWithoutNotify(0f);
            if (currentValueLabel != null) currentValueLabel.text = SliderLabel(0f);
            SetBanner(chargeCompleteBanner, true);
            SetBanner(cvModeBanner,         false);
        }

        void OnSocChanged(float socActual)
        {
            if (controller == null) return;
            float disp = controller.SOC_displayed;
            if (socDisplayedLabel != null)
                socDisplayedLabel.text = (disp * 100f).ToString("F0") + "% displayed";
            if (socActualLabel != null)
                socActualLabel.text = (socActual * 100f).ToString("F0") + "% actual";
            if (socOuterRing != null) socOuterRing.fillAmount = disp;
            if (socInnerRing != null) socInnerRing.fillAmount = socActual;

            if (controller.currentC > 0f)
                SetBanner(chargeCompleteBanner, false);
        }

        void OnTempChanged(float t)
        {
            if (tempLabel != null) tempLabel.text = "T pack: " + t.ToString("F1") + " C";
            if (tempBar   != null) tempBar.fillAmount = Mathf.InverseLerp(0f, 80f, t);
        }

        void OnSohChanged(float[] soh)
        {
            if (heatmapTiles != null)
            {
                int n = Mathf.Min(heatmapTiles.Length, soh.Length);
                for (int i = 0; i < n; i++)
                    if (heatmapTiles[i] != null)
                        heatmapTiles[i].color = CellGrid.SohToColor(soh[i]);
            }

            if (heatmapSohLabels != null)
            {
                int n = Mathf.Min(heatmapSohLabels.Length, soh.Length);
                for (int i = 0; i < n; i++)
                    if (heatmapSohLabels[i] != null)
                        heatmapSohLabels[i].text = (soh[i] * 100f).ToString("F0") + "%";
            }
        }

        void OnCycleCountChanged(float cycles)
        {
            if (cycleCountLabel != null)
                cycleCountLabel.text = "Cycles: " + cycles.ToString("F1");
        }

        void OnRangeChanged(float healthy, float actual)
        {
            if (rangeHealthyLabel != null)
                rangeHealthyLabel.text = "Healthy: " + healthy.ToString("F0") + " km";
            if (rangeActualLabel  != null)
                rangeActualLabel.text  = "Actual: "  + actual.ToString("F0")  + " km";
            if (rangeDeltaLabel   != null)
                rangeDeltaLabel.text   = "-" + Mathf.Abs(healthy - actual).ToString("F0") + " km lost to SOH";
        }

        private static void SetBanner(GameObject g, bool on)
        {
            if (g != null) g.SetActive(on);
        }

        // Slider right (positive) = charge; left (negative) = discharge.
        private static string SliderLabel(float v)
        {
            if (Mathf.Approximately(v, 0f)) return "0.0 C  (idle)";
            return v > 0f
                ? "+" + v.ToString("F1") + " C  charge"
                : v.ToString("F1") + " C  discharge";
        }
    }
}
