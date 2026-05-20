// =============================================================================
// SafeCharge — CellGrid
// =============================================================================
// Finds the 16 cell GameObjects under packRoot (named cell_00 .. cell_15) and
// drives their material colour from the BatteryPackController's SOH array.
//
// Uses MaterialPropertyBlock so we don't have to instance materials per cell.
// =============================================================================
using UnityEngine;

namespace SafeCharge
{
    public class CellGrid : MonoBehaviour
    {
        [Tooltip("Controller emitting SOH changes.")]
        public BatteryPackController controller;
        [Tooltip("Parent transform containing cell_00 .. cell_15.")]
        public Transform packRoot;
        [Tooltip("Number of cells to wire (must match controller.numCells).")]
        public int cellCount = 16;

        // Shader property name. URP uses _BaseColor; Built-in uses _Color.
        [Tooltip("Material colour property name. URP: _BaseColor; Built-in: _Color.")]
        public string colorProperty = "_BaseColor";

        private Renderer[] cellRenderers;
        private MaterialPropertyBlock mpb;
        private int colorId;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            colorId = Shader.PropertyToID(colorProperty);
            cellRenderers = new Renderer[cellCount];

            if (packRoot == null)
            {
                Debug.LogWarning("[CellGrid] packRoot not assigned — cell colours won't update.");
                return;
            }

            for (int i = 0; i < cellCount; i++)
            {
                string name = $"cell_{i:D2}";
                var t = FindRecursive(packRoot, name);
                if (t != null) cellRenderers[i] = t.GetComponentInChildren<Renderer>();
                else Debug.LogWarning($"[CellGrid] Could not find {name} under {packRoot.name}.");
            }

            if (controller != null)
            {
                controller.OnSohChanged += ApplyColors;
                ApplyColors(controller.cellSOH);
            }
        }

        void OnDestroy()
        {
            if (controller != null) controller.OnSohChanged -= ApplyColors;
        }

        private void ApplyColors(float[] soh)
        {
            if (cellRenderers == null) return;
            int n = Mathf.Min(cellRenderers.Length, soh.Length);
            for (int i = 0; i < n; i++)
            {
                var r = cellRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(colorId, SohToColor(soh[i]));
                r.SetPropertyBlock(mpb);
            }
        }

        public static Color SohToColor(float soh)
        {
            if (soh >= 0.90f) return new Color(0.36f, 0.66f, 0.36f);   // green
            if (soh >= 0.85f) return new Color(0.71f, 0.83f, 0.42f);   // light green
            if (soh >= 0.80f) return new Color(0.91f, 0.77f, 0.28f);   // yellow
            if (soh >= 0.75f) return new Color(0.85f, 0.48f, 0.25f);   // orange
            return new Color(0.72f, 0.24f, 0.18f);                     // red
        }

        private static Transform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var r = FindRecursive(child, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
