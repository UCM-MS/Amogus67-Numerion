using UnityEngine;

namespace Lumen.World
{
    /// <summary>North-up navigation projection. It never changes world state or chooses a route.</summary>
    public static class AcademyMapGeometry
    {
        public const float Extent = 88f;
        public const float CommonsRadius = 20f;
        public const float SectionRadius = 16.5f;
        public const float ExpeditionRadius = 11f;
        public const float BridgeWidth = 5.4f;

        public static Rect SquarePlot(Rect bounds, float inset = 14f)
        {
            float side = Mathf.Max(0, Mathf.Min(bounds.width, bounds.height) - Mathf.Max(0, inset) * 2);
            return new Rect(bounds.center - Vector2.one * side * .5f, Vector2.one * side);
        }

        public static Vector2 Project(Vector3 world, Rect plot)
        {
            Rect square = SquarePlot(plot, 0);
            return square.center + new Vector2(world.x, world.z) * (square.width / (Extent * 2));
        }

        public static Vector3 Unproject(Vector2 point, Rect plot)
        {
            Rect square = SquarePlot(plot, 0);
            if (square.width <= .001f) return Vector3.zero;
            Vector2 p = (point - square.center) * (Extent * 2 / square.width);
            return new Vector3(p.x, 0, p.y);
        }

        public static bool TryUnproject(Vector2 point, Rect plot, out Vector3 world)
        {
            Rect square = SquarePlot(plot, 0);
            world = Vector3.zero;
            if (square.width <= .001f || float.IsNaN(point.x) || float.IsNaN(point.y) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y) || !square.Contains(point)) return false;
            world = Unproject(point, square);
            return true;
        }

        /// <summary>Matches the actual stone causeway, including its overlap into both islands.</summary>
        public static void BridgeEndpoints(Vector3 section, out Vector3 start, out Vector3 end)
        {
            Vector3 flat = new Vector3(section.x, 0, section.z);
            Vector3 direction = flat.normalized;
            start = direction * 19f;
            end = direction * Mathf.Max(19f, flat.magnitude - 15.5f);
        }
    }
}
