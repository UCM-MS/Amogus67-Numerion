#if UNITY_EDITOR
using System;
using Lumen.World;
using UnityEditor;
using UnityEngine;

namespace Lumen.Editor
{
    /// <summary>Checks fixed-topology navigation across real bridge/portal coordinates. This is not obstacle pathfinding.</summary>
    public static class NavigationValidation
    {
        private static int checks;
        [MenuItem("Numerion/Validate Waypoint Guidance")]
        public static void Run()
        {
            WorldLayout world = null;
            try { world = AcademyWorld.Build(true); Validate(world); }
            finally { if (world?.Root) UnityEngine.Object.DestroyImmediate(world.Root); }
        }

        public static void Validate(WorldLayout world)
        {
            if (world == null || !world.Root) throw new ArgumentNullException(nameof(world));
            checks = 0;
            Require(world.Root.GetComponentsInChildren<Renderer>(true).Length == 0, "Guidance geometry must not create scenery renderers.");
            for (int x = -88; x <= 88; x += 11)
                for (int z = -88; z <= 88; z += 11)
                {
                    Vector3 result = AcademyNavigation.ClampToFloor(new Vector3(x, 0, z));
                    Require(AcademyNavigation.OnFloor(result), "A map click must not select the empty sky.");
                    Require(result.y == 0 && float.IsFinite(result.x) && float.IsFinite(result.z), "A map waypoint must be finite and flat.");
                }
            Require(AcademyNavigation.ClampToFloor(new Vector3(float.NaN, 0, 0)) == Vector3.zero, "Invalid map coordinates must fail closed.");
            Require(AcademyNavigation.ClampToFloor(new Vector3(0, 0, float.PositiveInfinity)) == Vector3.zero, "Infinite map coordinates must fail closed.");
            for (int i = 0; i < 3; i++)
                foreach (bool returning in new[] {false, true})
                {
                    string id = (returning ? "expedition-return-" : "expedition-portal-") + i;
                    var station = world.Stations.Find(s => s && s.Id == id);
                    Require(station != null, "Actual expedition portal missing: " + id);
                    Require(AcademyNavigation.Distance(AcademyNavigation.PortalPosition(world, i, returning), station.transform.position) < .0001f,
                        "Guidance must point to the real interactive portal.");
                    Require(AcademyNavigation.Distance(AcademyNavigation.PortalPosition(null, i, returning), station.transform.position) < .0001f,
                        "Fallback topology must match the real portal coordinates.");
                }
            for (int source = 0; source < 10; source++)
                for (int target = 0; target < 10; target++)
                    for (int side = 0; side < 4; side++)
                    {
                        Vector3 start = RegionCenter(source) + Quaternion.Euler(0, side * 90 + 23, 0) * Vector3.forward * (source >= 7 ? 5 : 10);
                        Vector3 goal = RegionCenter(target) + new Vector3(2, 0, -2);
                        Simulate(world, start, goal);
                    }
            for (int i = 0; i < 6; i++)
            {
                Vector3 bridgeGoal = Vector3.Lerp(AcademyNavigation.InnerLanding(i), AcademyNavigation.OuterLanding(i), .5f);
                Simulate(world, world.SpawnPosition, bridgeGoal);
                Simulate(world, AcademyWorld.SectionPositions[i] + new Vector3(7, 0, 4), bridgeGoal);
            }
            Debug.Log("NUMERION_NAVIGATION_PASSED: " + checks + " checks; real portal coordinates, finite reachable map clicks and fixed-topology routes between ten islands through six bridges/three portal pairs. Local obstacle avoidance remains player-controlled.");
        }

        private static Vector3 RegionCenter(int area) => area == 0 ? Vector3.zero
            : area <= 6 ? AcademyWorld.SectionPositions[area - 1] : AcademyWorld.ExpeditionCenters[area - 7];

        private static void Simulate(WorldLayout world, Vector3 player, Vector3 goal)
        {
            for (int tick = 0; tick < 300; tick++)
            {
                if (AcademyNavigation.Distance(player, goal) < 1.8f) { Require(true, "Arrived"); return; }
                var step = AcademyNavigation.Next(world, player, goal);
                Require(AcademyNavigation.OnFloor(step.Position), "A next-leg marker must be on a real island or bridge footprint.");
                if (step.Portal && AcademyNavigation.Distance(player, step.Position) <= 1.5f)
                {
                    int from = AcademyNavigation.Area(player), to = AcademyNavigation.Area(goal);
                    player = from >= 7 && from <= 9 ? AcademyWorld.ExpeditionAnchors[3] : AcademyWorld.ExpeditionAnchors[to - 7];
                    continue;
                }
                Vector3 next = Vector3.MoveTowards(player, step.Position, 1.2f);
                for (int sample = 0; sample <= 4; sample++)
                    Require(AcademyNavigation.OnFloor(Vector3.Lerp(player, next, sample * .25f)), "Guidance leg would cross empty sky.");
                Require(AcademyNavigation.Distance(next, player) > .001f, "A bridge gate must not trap the player in a zero-length leg.");
                player = next;
            }
            Require(false, "Fixed topology guidance did not reach its target within bounded simulated steps.");
        }

        private static void Require(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException("Numerion navigation validation: " + message); }
    }
}
#endif
