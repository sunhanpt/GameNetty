using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    [RequireComponent(typeof(Text))]
    internal sealed class GameTextOutline : Shadow
    {
        private Text caption;

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            if (caption == null) caption = GetComponent<Text>();
            float fontSize = caption.resizeTextForBestFit
                ? caption.cachedTextGenerator.fontSizeUsedForBestFit
                : caption.fontSize;
            if (fontSize <= 0) fontSize = caption.fontSize;

            float horizontal = effectDistance.x * fontSize * .04f;
            float upper = -effectDistance.y * fontSize * .02f;
            // Match the visible lower edge in the original recording at the restored canvas scale.
            float lower = effectDistance.y * fontSize * .06f;
            float scale = caption.canvas != null ? caption.canvas.scaleFactor : 1f;
            int layers = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(lower) * scale));
            using (ListPool<UIVertex>.Get(out var vertices))
            {
                mesh.GetUIVertexStream(vertices);
                if (vertices.Count == 0) return;
                int maxLayers = (65534 / vertices.Count - 6) / 3;
                if (maxLayers < 1) return;
                layers = Mathf.Min(layers, maxLayers);
                int start = 0;
                void Copy(float x, float y)
                {
                    int end = vertices.Count;
                    ApplyShadowZeroAlloc(vertices, effectColor, start, end, x, y);
                    start = end;
                }

                Copy(0, upper);
                Copy(-horizontal, 0);
                Copy(horizontal, 0);
                Copy(horizontal, upper);
                Copy(-horizontal, upper);
                for (int layer = 1; layer <= layers; layer++)
                {
                    float bottom = lower * layer / layers;
                    Copy(0, bottom);
                    Copy(-horizontal, bottom);
                    Copy(horizontal, bottom);
                }
                mesh.Clear();
                mesh.AddUIVertexTriangleStream(vertices);
            }
        }
    }
}
