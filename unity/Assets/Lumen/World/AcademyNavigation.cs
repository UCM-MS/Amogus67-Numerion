using UnityEngine;

namespace Lumen.World
{
    /// <summary>Landmark guidance over the academy's fixed island/bridge topology. Local obstacles still need to be walked around.</summary>
    public static class AcademyNavigation
    {
        public readonly struct Step
        {
            public readonly Vector3 Position;
            public readonly string Caption;
            public readonly bool Portal;
            public Step(Vector3 position, string caption, bool portal = false)
            { Position = Flat(position); Caption = caption; Portal = portal; }
        }

        public static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0, p.z);

        // 0 commons; 1..6 teaching islands; 7..9 expeditions; 20..25 causeways; -1 outside the floor plan.
        public static int Area(Vector3 point)
        {
            point = Flat(point);
            for (int i = 0; i < AcademyWorld.ExpeditionCenters.Length; i++)
                if (Distance(point, AcademyWorld.ExpeditionCenters[i]) <= AcademyMapGeometry.ExpeditionRadius + .2f) return 7 + i;
            if (point.magnitude <= AcademyMapGeometry.CommonsRadius + .1f) return 0;
            for (int i = 0; i < AcademyWorld.SectionPositions.Length; i++)
                if (Distance(point, AcademyWorld.SectionPositions[i]) <= AcademyMapGeometry.SectionRadius + .1f) return 1 + i;
            for (int i = 0; i < AcademyWorld.SectionPositions.Length; i++)
            {
                AcademyMapGeometry.BridgeEndpoints(AcademyWorld.SectionPositions[i], out var start, out var end);
                if (Distance(point, ClosestOnSegment(point, start, end)) <= AcademyMapGeometry.BridgeWidth * .5f) return 20 + i;
            }
            return -1;
        }

        public static bool OnFloor(Vector3 point) => Area(point) >= 0;

        /// <summary>A click in empty sky selects the closest reachable floor, never a point over the void.</summary>
        public static Vector3 ClampToFloor(Vector3 requested)
        {
            requested = Flat(requested);
            if (!float.IsFinite(requested.x) || !float.IsFinite(requested.z)) return Vector3.zero;
            Vector3 best = ClampDisc(requested, Vector3.zero, AcademyMapGeometry.CommonsRadius - .9f);
            float distance = Distance(requested, best);
            for (int i = 0; i < AcademyWorld.SectionPositions.Length; i++)
            {
                Consider(requested, ClampDisc(requested, AcademyWorld.SectionPositions[i], AcademyMapGeometry.SectionRadius - .9f), ref best, ref distance);
                AcademyMapGeometry.BridgeEndpoints(AcademyWorld.SectionPositions[i], out var start, out var end);
                // Use the centre line: a free-form click cannot put the destination beside a rail.
                Consider(requested, ClosestOnSegment(requested, start, end), ref best, ref distance);
            }
            foreach (Vector3 center in AcademyWorld.ExpeditionCenters)
                Consider(requested, ClampDisc(requested, center, AcademyMapGeometry.ExpeditionRadius - .9f), ref best, ref distance);
            return best;
        }

        public static Step Next(WorldLayout layout, Vector3 player, Vector3 destination)
        {
            int from = Area(player), to = Area(destination);
            if (from >= 7 && from <= 9)
            {
                if (from == to) return new Step(destination, "K vybranému cieľu");
                return new Step(PortalPosition(layout, from - 7, true), "Návrat do akadémie · E", true);
            }
            int desiredSection = to >= 1 && to <= 6 ? to - 1 : to >= 20 && to <= 25 ? to - 20 : -1;
            if (from >= 1 && from <= 6)
            {
                int section = from - 1;
                if (to == from) return new Step(destination, "K vybranému cieľu");
                Vector3 direction = AcademyWorld.SectionPositions[section].normalized;
                float lateral = Mathf.Abs(Vector3.Cross(Flat(player), direction).y);
                Vector3 outer = OuterLanding(section);
                if (lateral > 1.15f && Distance(player, outer) > 1.15f)
                    return new Step(outer, "K vstupu na most");
                if (to == 20 + section) return new Step(destination, "Po moste k cieľu");
                return new Step(InnerLanding(section), "Po moste na námestie");
            }
            if (from >= 20 && from <= 25)
            {
                int section = from - 20;
                if (to == from) return new Step(destination, "Po moste k cieľu");
                return desiredSection == section
                    ? new Step(OuterLanding(section), "Po moste k ostrovu")
                    : new Step(InnerLanding(section), "Po moste na námestie");
            }
            if (from < 0) return new Step(ClampToFloor(player), "Vráť sa na pevnú cestu");
            if (desiredSection >= 0)
            {
                Vector3 direction = AcademyWorld.SectionPositions[desiredSection].normalized;
                float lateral = Mathf.Abs(Vector3.Cross(Flat(player), direction).y);
                float outward = Vector3.Dot(Flat(player), direction);
                if (lateral > 1.15f || outward < 17.8f)
                    return new Step(InnerLanding(desiredSection), "K vstupu na most");
                return to >= 20
                    ? new Step(destination, "Po moste k cieľu")
                    : new Step(OuterLanding(desiredSection), "Po moste k ostrovu");
            }
            if (to >= 7 && to <= 9)
                return new Step(PortalPosition(layout, to - 7, false), "Portál: " + AcademyWorld.ExpeditionNames[to - 7] + " · E", true);
            return new Step(destination, "K vybranému cieľu");
        }

        public static Vector3 InnerLanding(int section) => AcademyWorld.SectionPositions[section].normalized * 18.5f;
        public static Vector3 OuterLanding(int section)
        {
            Vector3 center = AcademyWorld.SectionPositions[section];
            return center - center.normalized * 14f;
        }

        public static Vector3 PortalPosition(WorldLayout layout, int expedition, bool returning)
        {
            string id = (returning ? "expedition-return-" : "expedition-portal-") + expedition;
            if (layout != null)
                foreach (var station in layout.Stations)
                    if (station && station.Id == id) return Flat(station.transform.position);
            return returning
                ? AcademyWorld.ExpeditionCenters[expedition] + new Vector3(5, 0, expedition == 2 ? 6 : -6)
                : new Vector3(-5 + (expedition - 1) * 3.1f, 0, -8.7f);
        }

        private static Vector3 ClampDisc(Vector3 p, Vector3 center, float radius)
        { Vector3 delta = Flat(p - center); return Flat(center) + Vector3.ClampMagnitude(delta, radius); }
        private static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
        { Vector3 d = b - a; return a + d * Mathf.Clamp01(Vector3.Dot(p - a, d) / Mathf.Max(.0001f, d.sqrMagnitude)); }
        private static void Consider(Vector3 point, Vector3 candidate, ref Vector3 best, ref float distance)
        { float next = Distance(point, candidate); if (next < distance) { best = candidate; distance = next; } }
    }
}
