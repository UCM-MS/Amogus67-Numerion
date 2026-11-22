#if UNITY_EDITOR
using System;
using Lumen.World;
using UnityEngine;

namespace Lumen.Editor
{
    /// <summary>Checks the actual rebuilt colliders, including new architecture, before shipping.</summary>
    public static class AcademyRouteValidation
    {
        public static void Validate(WorldLayout layout)
        {
            if (layout == null || !layout.Root) throw new ArgumentNullException(nameof(layout));
            if (layout.Root.GetComponentsInChildren<Renderer>(true).Length != 0)
                throw new InvalidOperationException("Academy server geometry unexpectedly allocated renderers.");
            if (layout.Stations.Count != 22 || layout.Collectibles.Count != 24 || layout.ParkourPlatforms.Count != 9)
                throw new InvalidOperationException("Academy route validation: gameplay anchors changed.");
            Physics.SyncTransforms();
            int checks = 0;
            CheckStandingRoom("spawn", layout.SpawnPosition); checks++;
            foreach (var station in layout.Stations)
            {
                // Every station must be approachable within its interaction radius without a jump.
                CheckStandingRoom(station.Id, station.transform.position + Vector3.forward * 2.25f);
                checks++;
            }
            foreach (Vector3 destination in AcademyWorld.SectionPositions)
            {
                float distance = destination.magnitude;
                Vector3 direction = destination.normalized;
                for (int sample = 0; sample < 3; sample++)
                {
                    float progress = Mathf.Lerp(20, distance - 16.5f, sample * 0.5f);
                    CheckStandingRoom("bridge to " + destination, direction * progress);
                    checks++;
                }
            }
            foreach (Vector3 checkpoint in layout.ParkourPlatforms)
            {
                CheckStandingRoom("parkour checkpoint", checkpoint);
                checks++;
            }
            Debug.Log("LUMEN_WORLD_ROUTES_PASSED: " + checks + " standing positions; 22 reachable stations, six bridges and nine checkpoint floors; zero server renderers.");
        }

        private static void CheckStandingRoom(string name, Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up * 3, Vector3.down, out var hit, 6, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Academy route has no floor: " + name + " at " + position);
            Vector3 feet = hit.point + Vector3.up * 0.06f;
            if (Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.45f, 0.28f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Academy architecture blocks standing room: " + name + " at " + position);
        }
    }
}
#endif
