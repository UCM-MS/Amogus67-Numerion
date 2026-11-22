using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    /// <summary>
    /// Scroll sensitivity is measured in UI pixels per physical wheel tick.
    /// The Input System's configurable event multiplier must not multiply it again.
    /// Fractional ticks from precision trackpads remain fractional; touch dragging
    /// and unscaled-time inertia continue to use Unity's regular ScrollRect.
    /// </summary>
    [AddComponentMenu("Numerion/Scroll Rect")]
    public sealed class LumenScrollRect : ScrollRect
    {
        public override void OnScroll(PointerEventData data)
        {
            if (data == null || !IsActive() || !horizontal && !vertical) return;
            Vector2 original = data.scrollDelta;
            var module = data.currentInputModule;
            Vector2 ticks = module ? module.ConvertPointerEventScrollDeltaToTicks(original) : original;
            Vector2 size = viewport ? viewport.rect.size : ((RectTransform)transform).rect.size;
            Vector2 normalized = LumenScrollMath.BoundedTicks(ticks, scrollSensitivity, size, horizontal, vertical);
            if (normalized.sqrMagnitude == 0) return;
            // A new wheel gesture takes over from an earlier touch flick rather
            // than adding a second independent velocity to the same content.
            StopMovement();
            data.scrollDelta = normalized;
            try { base.OnScroll(data); }
            finally { data.scrollDelta = original; }
        }
    }

    public static class LumenScrollMath
    {
        /// <summary>One event cannot move more than 75% of a page or 240 UI pixels.</summary>
        public static Vector2 BoundedTicks(Vector2 ticks, float pixelsPerTick, Vector2 viewportSize, bool horizontal, bool vertical)
        {
            if (!Finite(pixelsPerTick) || pixelsPerTick <= 0) return Vector2.zero;
            return new Vector2(horizontal ? Axis(ticks.x, pixelsPerTick, viewportSize.x) : 0,
                vertical ? Axis(ticks.y, pixelsPerTick, viewportSize.y) : 0);
        }

        private static float Axis(float ticks, float step, float viewport)
        {
            if (!Finite(ticks) || !Finite(viewport) || viewport <= 0) return 0;
            float maxPixels = Mathf.Min(240f, viewport * .75f);
            return Mathf.Clamp(ticks, -maxPixels / step, maxPixels / step);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
