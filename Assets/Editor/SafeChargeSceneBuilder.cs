// =============================================================================
// SafeCharge - SafeChargeSceneBuilder (Editor-only)
// Premium dark-theme UI with anti-aliased rounded panels
// =============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SafeCharge
{
    public static class SafeChargeSceneBuilder
    {
        const string SCENE_DIR  = "Assets/Scenes";
        const string SCENE_PATH = "Assets/Scenes/SafeCharge_Demo.unity";
        const string UI_DIR     = "Assets/SafeChargeUI";

        // Rounded sprites generated once per scene build and stored as assets
        static Sprite _spritePanel;  // 64x64 r=12 — panels, buttons, banners, slider handle
        static Sprite _spriteTile;   // 40x40 r=7  — heatmap cell tiles
        static Sprite _spriteTrack;  // 32x16 r=7  — slider track + fill

        // ---- Entry point --------------------------------------------------------

        [MenuItem("SafeCharge/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            _spritePanel = null;
            _spriteTile  = null;
            _spriteTrack = null;

            GameObject packPrefab = LocatePackFbx();
            if (packPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "SafeCharge - pack.fbx not found",
                    "Drag pack.fbx into the Assets folder first, " +
                    "then run SafeCharge -> Build Demo Scene again.", "OK");
                return;
            }

            if (!Directory.Exists(SCENE_DIR)) Directory.CreateDirectory(SCENE_DIR);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var pack = (GameObject)PrefabUtility.InstantiatePrefab(packPrefab);
            pack.name = "Pack";
            pack.transform.position   = Vector3.zero;
            pack.transform.rotation   = Quaternion.Euler(-90f, 0f, 0f);
            pack.transform.localScale = Vector3.one;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position  = new Vector3(0.7f, 0.5f, -0.9f);
                cam.transform.LookAt(new Vector3(0f, 0.05f, 0f));
                cam.fieldOfView     = 35f;
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.22f, 0.24f, 0.30f);
            }

            var logic          = new GameObject("Logic");
            var controller     = logic.AddComponent<BatteryPackController>();
            var cellGrid       = logic.AddComponent<CellGrid>();
            var rangeEstimator = logic.AddComponent<RangeEstimator>();
            var bmsLogic       = logic.AddComponent<BMSLogic>();

            cellGrid.controller       = controller;
            cellGrid.packRoot         = pack.transform;
            rangeEstimator.controller = controller;
            bmsLogic.controller       = controller;
            bmsLogic.fanRotor         = pack.transform.Find("fan_rotor");

            EnsureSprites();
            BuildDashboardCanvas(controller, rangeEstimator, bmsLogic);

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SafeCharge",
                "Demo scene built.\nOpen Assets/Scenes/SafeCharge_Demo.unity and press Play.", "OK");
        }

        // ---- Rounded sprite generation ------------------------------------------

        static void EnsureSprites()
        {
            if (!Directory.Exists(UI_DIR)) Directory.CreateDirectory(UI_DIR);
            _spritePanel = BuildRoundedSprite("rounded_panel", 64, 64, 12);
            _spriteTile  = BuildRoundedSprite("rounded_tile",  40, 40,  7);
            _spriteTrack = BuildRoundedSprite("rounded_track", 32, 16,  7);
        }

        /// <summary>
        /// Generates a white rounded-rectangle PNG with anti-aliased corners,
        /// imports it as a 9-slice sprite, and returns the loaded Sprite asset.
        /// </summary>
        static Sprite BuildRoundedSprite(string assetName, int w, int h, int r)
        {
            string path = UI_DIR + "/" + assetName + ".png";

            var tex    = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float alpha = SampleRoundedRect(x, y, w, h, r);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType       = TextureImporterType.Sprite;
            imp.spriteImportMode  = SpriteImportMode.Single;
            imp.spriteBorder      = new Vector4(r, r, r, r);
            imp.filterMode        = FilterMode.Bilinear;
            imp.maxTextureSize    = 128;
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// Returns 0–1 coverage for pixel (x,y) inside a rounded rect.
        /// Corners use distance-field anti-aliasing (1-pixel soft edge).
        static float SampleRoundedRect(int x, int y, int w, int h, int r)
        {
            bool inLeft  = x < r;
            bool inRight = x >= w - r;
            bool inBot   = y < r;
            bool inTop   = y >= h - r;

            if ((inLeft || inRight) && (inBot || inTop))
            {
                // Corner region: test distance from corner-circle centre
                float cx = inLeft  ? r       : w - r - 1;
                float cy = inBot   ? r       : h - r - 1;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // Smooth step: fully inside at dist<=r-1, fully outside at dist>=r
                return Mathf.Clamp01((float)r - dist);
            }
            return 1f;
        }

        // ---- Helpers -----------------------------------------------------------

        static void ApplyRounded(Image img, Sprite spr)
        {
            if (spr == null) return;
            img.sprite = spr;
            img.type   = Image.Type.Sliced;
        }

        static GameObject LocatePackFbx()
        {
            string[] guids = AssetDatabase.FindAssets("pack t:Model");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.ToLower().EndsWith(".fbx")) continue;
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj != null) return obj;
            }
            return null;
        }

        // ---- Canvas builder ----------------------------------------------------

        static DashboardUI BuildDashboardCanvas(BatteryPackController controller,
                                                RangeEstimator rangeEstimator,
                                                BMSLogic bmsLogic)
        {
            var canvasGO = new GameObject("DashboardCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            var ui = canvasGO.AddComponent<DashboardUI>();
            ui.controller     = controller;
            ui.rangeEstimator = rangeEstimator;
            ui.bmsLogic       = bmsLogic;

            // ── Colour palette ─────────────────────────────────────────────────
            var maroon      = new Color(0.72f, 0.08f, 0.16f);
            var maroonDark  = new Color(0.55f, 0.05f, 0.12f);
            var maroonLight = new Color(1.00f, 0.55f, 0.60f);
            var green       = new Color(0.20f, 0.60f, 0.26f);
            var greenLight  = new Color(0.45f, 0.90f, 0.52f);
            var blue        = new Color(0.22f, 0.42f, 0.82f);
            var blueLight   = new Color(0.55f, 0.78f, 1.00f);
            var slate       = new Color(0.22f, 0.24f, 0.35f);
            var panelBg     = new Color(0.13f, 0.14f, 0.20f, 0.97f);
            var controlsBg  = new Color(0.09f, 0.10f, 0.14f, 1.00f);
            var trackCol    = new Color(0.18f, 0.19f, 0.27f);
            var textPrimary = Color.white;
            var textMuted   = new Color(0.65f, 0.67f, 0.75f);

            // ── Top title bar ──────────────────────────────────────────────────
            var topBar = MakePanel(canvasGO.transform, "TopBar",
                new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1f),
                new Vector2(0,62), Vector2.zero, maroon, null);
            MakeText(topBar.transform, "TopBarTitle",
                "SafeCharge  -  BMS Digital Twin",
                new Vector2(0,0), new Vector2(1,1),
                textPrimary, 23, TextAnchor.MiddleLeft, paddingLeft: 24);

            // ── SOC panel (left) ───────────────────────────────────────────────
            var socPanel = MakePanel(canvasGO.transform, "SOCPanel",
                new Vector2(0,1), new Vector2(0,1), new Vector2(0,1),
                new Vector2(430,340), new Vector2(18,-80), panelBg, _spritePanel);
            MakeText(socPanel.transform, "SOCTitle", "State of Charge",
                new Vector2(0,1), new Vector2(1,1), maroonLight, 18, TextAnchor.UpperCenter,
                pivot: new Vector2(0.5f,1), sizeDelta: new Vector2(0,30), pos: new Vector2(0,-8));
            ui.socDisplayedLabel = MakeText(socPanel.transform, "SOCDisplayed", "--",
                new Vector2(0,0.5f), new Vector2(1,0.5f),
                textPrimary, 38, TextAnchor.MiddleCenter,
                sizeDelta: new Vector2(0,58), pos: new Vector2(0,28));
            ui.socActualLabel = MakeText(socPanel.transform, "SOCActual", "--",
                new Vector2(0,0.5f), new Vector2(1,0.5f),
                maroonLight, 26, TextAnchor.MiddleCenter,
                sizeDelta: new Vector2(0,38), pos: new Vector2(0,-10));
            ui.tempLabel = MakeText(socPanel.transform, "TempLabel", "T pack: --",
                new Vector2(0,0), new Vector2(1,0),
                blueLight, 20, TextAnchor.LowerCenter,
                sizeDelta: new Vector2(0,36), pos: new Vector2(0,44));
            ui.cycleCountLabel = MakeText(socPanel.transform, "CycleCountLabel", "Cycles: 0.0",
                new Vector2(0,0), new Vector2(1,0),
                greenLight, 20, TextAnchor.LowerCenter,
                sizeDelta: new Vector2(0,36), pos: new Vector2(0,10));

            // ── Heatmap panel (middle) ─────────────────────────────────────────
            var heatPanel = MakePanel(canvasGO.transform, "HeatmapPanel",
                new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,1),
                new Vector2(390,340), new Vector2(0,-80), panelBg, _spritePanel);
            MakeText(heatPanel.transform, "HeatTitle", "Cell SOH Heatmap",
                new Vector2(0,1), new Vector2(1,1), maroonLight, 18, TextAnchor.UpperCenter,
                pivot: new Vector2(0.5f,1), sizeDelta: new Vector2(0,30), pos: new Vector2(0,-8));

            ui.heatmapTiles     = new Image[16];
            ui.heatmapSohLabels = new Text[16];
            const int   gridSize = 4;
            const float tileSize = 58f, gap = 5f;
            float gridW  = gridSize * tileSize + (gridSize - 1) * gap;
            float startX = -gridW / 2f + tileSize / 2f;
            float startY =  gridW / 2f - tileSize / 2f - 22f;
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    int   idx = row * gridSize + col;
                    float tx  = startX + col * (tileSize + gap);
                    float ty  = startY - row * (tileSize + gap);
                    Text  lbl;
                    ui.heatmapTiles[idx] = MakeTileWithLabel(heatPanel.transform,
                        "Tile_" + idx.ToString("D2"),
                        new Vector2(tx, ty), new Vector2(tileSize, tileSize),
                        CellGrid.SohToColor(1f), out lbl);
                    ui.heatmapSohLabels[idx] = lbl;
                }
            }

            // ── Range panel (right) ────────────────────────────────────────────
            var rangePanel = MakePanel(canvasGO.transform, "RangePanel",
                new Vector2(1,1), new Vector2(1,1), new Vector2(1,1),
                new Vector2(430,340), new Vector2(-18,-80), panelBg, _spritePanel);
            MakeText(rangePanel.transform, "RangeTitle", "EV Range",
                new Vector2(0,1), new Vector2(1,1), maroonLight, 18, TextAnchor.UpperCenter,
                pivot: new Vector2(0.5f,1), sizeDelta: new Vector2(0,30), pos: new Vector2(0,-8));
            ui.rangeHealthyLabel = MakeText(rangePanel.transform, "RangeHealthy", "--",
                new Vector2(0,0.5f), new Vector2(1,0.5f),
                textMuted, 22, TextAnchor.MiddleCenter,
                sizeDelta: new Vector2(0,30), pos: new Vector2(0,40));
            ui.rangeActualLabel = MakeText(rangePanel.transform, "RangeActual", "--",
                new Vector2(0,0.5f), new Vector2(1,0.5f),
                textPrimary, 38, TextAnchor.MiddleCenter,
                sizeDelta: new Vector2(0,58), pos: new Vector2(0,0));
            ui.rangeDeltaLabel = MakeText(rangePanel.transform, "RangeDelta", "--",
                new Vector2(0,0.5f), new Vector2(1,0.5f),
                maroonLight, 18, TextAnchor.MiddleCenter,
                sizeDelta: new Vector2(0,28), pos: new Vector2(0,-42));

            // ── Bottom controls bar ────────────────────────────────────────────
            var bottom = MakePanel(canvasGO.transform, "Controls",
                new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0),
                new Vector2(0,135), new Vector2(0,78), controlsBg, null);

            ui.currentSlider = MakeSlider(bottom.transform, "CurrentSlider",
                new Vector2(0.14f,0.5f), new Vector2(0.14f,0.5f),
                new Vector2(290,14), Vector2.zero, -5f, 5f, 0f, trackCol, maroon);
            MakeText(bottom.transform, "CurrentLabel", "Current  (C-rate)",
                new Vector2(0.14f,0.5f), new Vector2(0.14f,0.5f),
                textMuted, 15, TextAnchor.MiddleCenter,
                pivot: new Vector2(0.5f,0), sizeDelta: new Vector2(290,22), pos: new Vector2(0,20));
            ui.currentValueLabel = MakeText(bottom.transform, "CurrentValue", "0.0 C  (idle)",
                new Vector2(0.14f,0.5f), new Vector2(0.14f,0.5f),
                textPrimary, 15, TextAnchor.MiddleCenter,
                pivot: new Vector2(0.5f,1), sizeDelta: new Vector2(290,22), pos: new Vector2(0,-20));

            ui.ambientSlider = MakeSlider(bottom.transform, "AmbientSlider",
                new Vector2(0.42f,0.5f), new Vector2(0.42f,0.5f),
                new Vector2(290,14), Vector2.zero, -10f, 50f, 25f, trackCol, maroon);
            MakeText(bottom.transform, "AmbientLabel", "Ambient  (C)",
                new Vector2(0.42f,0.5f), new Vector2(0.42f,0.5f),
                textMuted, 15, TextAnchor.MiddleCenter,
                pivot: new Vector2(0.5f,0), sizeDelta: new Vector2(290,22), pos: new Vector2(0,20));
            ui.ambientValueLabel = MakeText(bottom.transform, "AmbientValue", "25 C",
                new Vector2(0.42f,0.5f), new Vector2(0.42f,0.5f),
                textPrimary, 15, TextAnchor.MiddleCenter,
                pivot: new Vector2(0.5f,1), sizeDelta: new Vector2(290,22), pos: new Vector2(0,-20));

            MakeText(bottom.transform, "SpeedLabel", "Speed",
                new Vector2(0.66f,0.5f), new Vector2(0.66f,0.5f),
                textMuted, 14, TextAnchor.MiddleCenter,
                pivot: new Vector2(0.5f,0), sizeDelta: new Vector2(84,24), pos: new Vector2(0,50));
            ui.speed1xButton  = MakeBtn(bottom.transform, "Speed1xButton",  "1x",
                new Vector2(0.66f,0.5f), new Vector2(0, 34), new Vector2(76,30), slate);
            ui.speed30xButton = MakeBtn(bottom.transform, "Speed30xButton", "30x",
                new Vector2(0.66f,0.5f), new Vector2(0,  0), new Vector2(76,30), maroon);
            ui.speed50xButton = MakeBtn(bottom.transform, "Speed50xButton", "50x",
                new Vector2(0.66f,0.5f), new Vector2(0,-34), new Vector2(76,30), slate);

            ui.freshPackButton  = MakeBtn(bottom.transform, "FreshPackButton",  "Fresh Pack",
                new Vector2(0.84f,0.5f), new Vector2(0, 48), new Vector2(156,30), green);
            ui.wornPackButton   = MakeBtn(bottom.transform, "WornPackButton",   "Worn Pack",
                new Vector2(0.84f,0.5f), new Vector2(0, 16), new Vector2(156,30), maroon);
            ui.resetCycleButton = MakeBtn(bottom.transform, "ResetCycleButton", "Reset Cycle",
                new Vector2(0.84f,0.5f), new Vector2(0,-16), new Vector2(156,30), blue);
            ui.fullResetButton  = MakeBtn(bottom.transform, "FullResetButton",  "Full Reset",
                new Vector2(0.84f,0.5f), new Vector2(0,-48), new Vector2(156,30), slate);

            // ── Banners ────────────────────────────────────────────────────────
            ui.coolingBanner = MakeBanner(canvasGO.transform, "CoolingBanner",
                "Cooling active",
                new Color(0.72f, 0.56f, 0.04f, 0.97f), textPrimary, new Vector2(0,-76));
            ui.alarmBanner = MakeBanner(canvasGO.transform, "AlarmBanner",
                "Thermal alarm  -  current cut",
                new Color(0.72f, 0.10f, 0.10f, 0.97f), textPrimary, new Vector2(0,-116));
            ui.cvModeBanner = MakeBanner(canvasGO.transform, "CVModeBanner",
                "CV Mode  -  tapering current",
                new Color(0.18f, 0.36f, 0.78f, 0.97f), textPrimary, new Vector2(0,-156));
            ui.chargeCompleteBanner = MakeBanner(canvasGO.transform, "ChargeCompleteBanner",
                "Charge complete  -  100%",
                new Color(0.10f, 0.52f, 0.18f, 0.97f), textPrimary, new Vector2(0,-196));

            ui.coolingBanner.SetActive(false);
            ui.alarmBanner.SetActive(false);
            ui.cvModeBanner.SetActive(false);
            ui.chargeCompleteBanner.SetActive(false);

            return ui;
        }

        // ---- UI primitive helpers -----------------------------------------------

        static GameObject MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 sizeDelta, Vector2 pos, Color bg, Sprite sprite)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = pivot; rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = bg;
            ApplyRounded(img, sprite);
            return go;
        }

        static Image MakeTileWithLabel(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color, out Text label)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = color;
            ApplyRounded(img, _spriteTile);

            var tGO = new GameObject("SOHLabel", typeof(Text));
            tGO.transform.SetParent(go.transform, false);
            var tRT = tGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;
            label = tGO.GetComponent<Text>();
            label.text      = "100%";
            label.color     = new Color(0.92f, 0.92f, 0.92f);
            label.fontSize  = 14;
            label.alignment = TextAnchor.MiddleCenter;
            label.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow   = VerticalWrapMode.Overflow;
            return img;
        }

        static Text MakeText(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax,
            Color color, int fontSize, TextAnchor alignment,
            Vector2? pivot = null, Vector2? sizeDelta = null, Vector2? pos = null,
            float paddingLeft = 0f)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            if (sizeDelta.HasValue) rt.sizeDelta        = sizeDelta.Value;
            if (pos.HasValue)       rt.anchoredPosition = pos.Value;
            if (anchorMin != anchorMax)
            {
                rt.offsetMin = new Vector2(paddingLeft,  rt.offsetMin.y);
                rt.offsetMax = new Vector2(-paddingLeft, rt.offsetMax.y);
            }
            var t = go.GetComponent<Text>();
            t.text = content; t.color = color;
            t.fontSize = fontSize; t.alignment = alignment;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            return t;
        }

        static Slider MakeSlider(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 sizeDelta, Vector2 pos,
            float min, float max, float value,
            Color trackColor, Color fillColor)
        {
            var go = new GameObject(name, typeof(Slider), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
            bgRT.sizeDelta = new Vector2(0, 14); bgRT.anchoredPosition = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = trackColor;
            ApplyRounded(bgImg, _spriteTrack);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0.5f); faRT.anchorMax = new Vector2(1, 0.5f);
            faRT.offsetMin = new Vector2(8, -6); faRT.offsetMax = new Vector2(-8, 6);

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fRT = fill.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
            fRT.offsetMin = fRT.offsetMax = Vector2.zero;
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = fillColor;
            ApplyRounded(fillImg, _spriteTrack);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRT = handleArea.GetComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(8, 0); haRT.offsetMax = new Vector2(-8, 0);

            var handle = new GameObject("Handle", typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var hRT = handle.GetComponent<RectTransform>();
            hRT.anchorMin = hRT.anchorMax = new Vector2(0, 0.5f);
            hRT.sizeDelta = new Vector2(18, 34);
            var hImg = handle.GetComponent<Image>();
            hImg.color = fillColor;
            ApplyRounded(hImg, _spritePanel);

            var slider = go.GetComponent<Slider>();
            slider.fillRect      = fRT;
            slider.handleRect    = hRT;
            slider.targetGraphic = hImg;
            slider.direction     = Slider.Direction.LeftToRight;
            slider.minValue      = min;
            slider.maxValue      = max;
            slider.value         = value;
            return slider;
        }

        static Button MakeBtn(Transform parent, string name, string label,
            Vector2 anchor, Vector2 pos, Vector2 size, Color bg)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = bg;
            ApplyRounded(img, _spritePanel);
            var lrt = MakeText(go.transform, "Label", label,
                Vector2.zero, Vector2.one,
                Color.white, 15, TextAnchor.MiddleCenter).rectTransform;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static GameObject MakeBanner(Transform parent, string name, string text,
            Color bg, Color fg, Vector2 pos)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(480, 36); rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = bg;
            ApplyRounded(img, _spritePanel);
            MakeText(go.transform, "Label", text,
                Vector2.zero, Vector2.one, fg, 16, TextAnchor.MiddleCenter);
            return go;
        }
    }
}
#endif
