#if UNITY_EDITOR
using System;
using System.Linq;
using Lumen.Networking;
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
            if (layout.Stations.Count != 34 || layout.Collectibles.Count != 24 || layout.ParkourPlatforms.Count != 9)
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
                CheckSweptRoute("bridge to " + destination, direction * 19.3f, direction * (distance - 15.8f));
                checks++;
            }
            foreach (Vector3 checkpoint in layout.ParkourPlatforms)
            {
                CheckStandingRoom("parkour checkpoint", checkpoint);
                checks++;
            }
            foreach (Vector3 arrival in AcademyWorld.ExpeditionAnchors)
            {
                CheckStandingRoom("expedition portal arrival", arrival); checks++;
            }
            for (int island = 0; island < AcademyWorld.ExpeditionCenters.Length; island++)
                foreach (var station in layout.Stations.Where(s => s.Id == "expedition-lesson-" + island || s.Id == "expedition-test-" + island))
                {
                    CheckSweptRoute(station.Id, AcademyWorld.ExpeditionAnchors[island], station.transform.position + Vector3.forward * 2.25f);
                    checks++;
                }
            RequireSolidCount(layout, "Exhibit plinth", 6); checks++;
            RequireSolidCount(layout, "Fountain stone pedestal", 1); checks++;
            RequireSolidCount(layout, "Upper fountain bowl", 1); checks++;
            CheckCylinderSilhouette(layout); checks++;
            int movementChecks = ValidateControllerContacts();
            Debug.Log("LUMEN_WORLD_ROUTES_PASSED: 66 standing positions plus 12 swept routes, three major-solid checks and a cylinder silhouette probe; 34 stations, four portal arrivals, six bridges and nine checkpoint floors; zero server renderers. Total " + checks + " world checks.");
            Debug.Log("NUMERION_CONTROLLER_CONTACTS_PASSED: " + movementChecks + " real CharacterController checks for floor, walls, low steps and ceilings.");
        }

        private static void CheckStandingRoom(string name, Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up * 3, Vector3.down, out var hit, 6, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Academy route has no floor: " + name + " at " + position);
            Vector3 feet = hit.point + Vector3.up * 0.06f;
            if (Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.45f, 0.28f, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Academy architecture blocks standing room: " + name + " at " + position);
        }

        private static void CheckSweptRoute(string name, Vector3 start, Vector3 end)
        {
            if (!Physics.Raycast(start + Vector3.up * 3, Vector3.down, out var floor, 6, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Swept route has no starting floor: " + name);
            Vector3 feet = new Vector3(start.x, floor.point.y + .06f, start.z);
            Vector3 motion = new Vector3(end.x - start.x, 0, end.z - start.z);
            if (Physics.CapsuleCast(feet + Vector3.up * .35f, feet + Vector3.up * 1.45f, .28f,
                motion.normalized, out var obstruction, motion.magnitude, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Academy swept walking route blocked by " + obstruction.collider.name + ": " + name);
        }

        private static void RequireSolidCount(WorldLayout layout, string name, int expected)
        {
            int count = layout.Root.GetComponentsInChildren<Collider>(true).Count(c => c.name == name && c.enabled && !c.isTrigger);
            if (count != expected) throw new InvalidOperationException("Missing matching solid collider for " + name + ": " + count + "/" + expected);
        }

        private static void CheckCylinderSilhouette(WorldLayout layout)
        {
            var column = layout.Root.GetComponentsInChildren<MeshCollider>().FirstOrDefault(c => c.name == "Exhibit plinth");
            if (!column) throw new InvalidOperationException("Exhibit plinth does not have its circular solid collision surface.");
            var mesh = column.sharedMesh;
            if ((mesh.bounds.center - Vector3.zero).sqrMagnitude > .0001f || (mesh.bounds.size - new Vector3(1, 2, 1)).sqrMagnitude > .0001f)
                throw new InvalidOperationException("Unit-cylinder collider dimensions do not match the rendered primitive.");
            Vector3 start = column.transform.TransformPoint(new Vector3(.48f, 0, 2));
            Vector3 direction = column.transform.TransformDirection(Vector3.back);
            if (!column.Raycast(new Ray(start, direction), out var hit, 20) || column.transform.InverseTransformPoint(hit.point).z > .24f)
                throw new InvalidOperationException("Cylinder hitbox still contains invisible square corners.");
        }

        private static int ValidateControllerContacts()
        {
            // Isolated test geometry is far outside the academy and removed even when an assertion fails.
            var probe = new GameObject("Numerion / isolated contact regression");
            probe.transform.position = new Vector3(1000, 0, 1000);
            int checks = 0;
            void Require(bool condition, string message)
            {
                checks++;
                if (!condition) throw new InvalidOperationException("Explorer contact regression: " + message);
            }
            void Box(string name, Vector3 center, Vector3 size)
            {
                var item = new GameObject(name); item.transform.SetParent(probe.transform, false); item.transform.localPosition = center;
                item.AddComponent<BoxCollider>().size = size;
            }
            try
            {
                Box("Contact test floor", new Vector3(0, -.25f, 0), new Vector3(24, .5f, 24));
                var player = new GameObject("Contact test explorer"); player.transform.SetParent(probe.transform, false);
                var controller = player.AddComponent<CharacterController>(); ExplorerPhysics.Configure(controller);
                void Place(Vector3 point)
                {
                    controller.enabled = false; player.transform.localPosition = point; controller.enabled = true; Physics.SyncTransforms();
                }
                Require(Mathf.Approximately(controller.height, 1.8f) && Mathf.Approximately(controller.radius, .3f), "capsule dimensions differ");
                Require(Mathf.Approximately(controller.skinWidth, .05f) && Mathf.Approximately(controller.stepOffset, .3f), "capsule contact settings differ");
                Place(new Vector3(0, .12f, 0));
                CollisionFlags contacts = controller.Move(Vector3.down * .3f);
                Require((contacts & CollisionFlags.Below) != 0 && controller.isGrounded, "capsule does not settle on the floor");
                Require(Mathf.Abs(player.transform.localPosition.y) < .08f, "grounded feet do not match the visible floor");

                Box("Contact test wall", new Vector3(0, 1.5f, 3), new Vector3(3, 3, .4f)); Physics.SyncTransforms();
                for (int step = 0; step < 50; step++) contacts |= controller.Move(new Vector3(0, -.03f, .1f));
                Require((contacts & CollisionFlags.Sides) != 0, "wall did not produce a side contact");
                Require(player.transform.localPosition.z < 2.6f, "capsule penetrated the wall");
                Require(player.transform.localPosition.z > 2.35f, "wall hitbox blocks too far from its visible face");

                Box("Contact test low step", new Vector3(6, .1f, 2.5f), new Vector3(3, .2f, 2));
                Place(new Vector3(6, .12f, 0)); controller.Move(Vector3.down * .3f);
                for (int step = 0; step < 25; step++) controller.Move(new Vector3(0, -.03f, .1f));
                Require(player.transform.localPosition.z > 2.2f && player.transform.localPosition.y > .12f,
                    "a 20cm step requires jumping despite 30cm step offset");

                Box("Contact test ceiling", new Vector3(-6, 2.3f, 0), new Vector3(3, .2f, 3));
                Place(new Vector3(-6, .12f, 0)); controller.Move(Vector3.down * .3f);
                bool touchedCeiling = false; float vertical = 7.2f;
                for (int step = 0; step < 30; step++)
                {
                    vertical = Mathf.Max(ExplorerPhysics.TerminalSpeed, vertical - 22f / 60f);
                    contacts = controller.Move(Vector3.up * (vertical / 60f));
                    if ((contacts & CollisionFlags.Above) != 0)
                    {
                        touchedCeiling = true;
                        vertical = ExplorerPhysics.ResolveVerticalSpeed(vertical, contacts, controller.isGrounded);
                        Require(vertical == 0, "ceiling contact kept upward momentum");
                        break;
                    }
                    vertical = ExplorerPhysics.ResolveVerticalSpeed(vertical, contacts, controller.isGrounded);
                }
                Require(touchedCeiling, "the jumping capsule passed through the ceiling");
                float ceilingFeet = player.transform.localPosition.y;
                for (int step = 0; step < 10; step++)
                {
                    vertical -= 22f / 60f;
                    contacts = controller.Move(Vector3.up * (vertical / 60f));
                    vertical = ExplorerPhysics.ResolveVerticalSpeed(vertical, contacts, controller.isGrounded);
                }
                Require(player.transform.localPosition.y < ceilingFeet - .1f, "the capsule sticks under the ceiling");
                Require(ExplorerPhysics.ResolveVerticalSpeed(-100, CollisionFlags.None, false) == ExplorerPhysics.TerminalSpeed, "unbounded fall speed");
                return checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); Physics.SyncTransforms(); }
        }
    }
}
#endif
