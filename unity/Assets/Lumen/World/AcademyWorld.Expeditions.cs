using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lumen.World
{
    public static partial class AcademyWorld
    {
        // These four arrival points are also allow-listed by the shared-room server.
        public static readonly Vector3[] ExpeditionAnchors = {
            new Vector3(-70, .15f, 58), new Vector3(70, .15f, 58),
            new Vector3(-70, .15f, -58), new Vector3(0, .15f, 12)
        };
        public static readonly Vector3[] ExpeditionCenters = {
            new Vector3(-70, 0, 64), new Vector3(70, 0, 64), new Vector3(-70, 0, -64)
        };
        public static readonly string[] ExpeditionIds = { "fraction-tide", "geometry-ruins", "algebra-observatory" };
        public static readonly string[] ExpeditionNames = { "Príliv zlomkov", "Geometrické ruiny", "Observatórium rovníc" };

        private sealed partial class Builder
        {
            private static Mesh expeditionWedge, expeditionBird;
            private readonly Color tideBlue = new Color(.34f, .48f, .53f);
            private readonly Color agedCopper = new Color(.61f, .43f, .27f);

            private void ExpeditionPortalHub()
            {
                var hub = MathGroup("Numerion / Expedition portal exchange", root, new Vector3(-5, 0, -8));
                ExpeditionNode("expedition-hub", "math", "Portály expedícií", hub, new Vector3(0, 0, 1.6f), 2.15f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = new Vector3((i - 1) * 3.1f, 0, -.7f);
                    ExpeditionNode("expedition-portal-" + i, "math", ExpeditionNames[i], hub, p, 1.5f);
                    if (!headless) ExpeditionPortal(hub, p, CampusAccents[i], 0.74f, (i + 1).ToString());
                }
                if (headless) return;
                WorldLabels.Create(hub, "EXPEDÍCIE", new Vector3(0, 3.65f, -.7f), 4f, cream);
                for (int i = 0; i < 3; i++)
                    Shape("Portal approach ground inlay", PrimitiveType.Cube, hub,
                        new Vector3((i - 1) * 3.1f, .045f, 1), new Vector3(.12f, .025f, 2.2f), brass, false);
            }

            private void ExpeditionArchipelago()
            {
                layout.PlayableBounds = new Bounds(Vector3.zero, new Vector3(168, 40, 168));
                for (int i = 0; i < 3; i++)
                {
                    var island = MathGroup("Expedition / " + ExpeditionIds[i], root, ExpeditionCenters[i]);
                    Disc("Expedition walkable limestone island", island, Vector3.zero, 11, 1.3f,
                        i == 0 ? new Color(.51f, .53f, .43f) : i == 1 ? new Color(.51f, .49f, .40f) : new Color(.31f, .39f, .37f), true);
                    float facing = i == 2 ? -1 : 1;
                    // Keep the return gateway beside arrival, outside the camera-to-player sight line.
                    Vector3 returnPoint = new Vector3(5, 0, -6 * facing);
                    ExpeditionNode("expedition-return-" + i, "math", "Návrat do akadémie", island, returnPoint, 2.25f);
                    string subject = i == 0 ? "math" : i == 1 ? "history" : "nature";
                    ExpeditionNode("expedition-lesson-" + i, subject, "Učebnica: " + ExpeditionNames[i], island, new Vector3(-4 * facing, 0, 0), 2.35f);
                    ExpeditionNode("expedition-test-" + i, subject, "Tematický test · 6 otázok", island, new Vector3(4 * facing, 0, 0), 2.35f);
                    ExpeditionTerminal(island, new Vector3(-4 * facing, 0, 0), "UČEBNICA", CampusAccents[i]);
                    ExpeditionTerminal(island, new Vector3(4 * facing, 0, 0), "6 OTÁZOK", brass);
                    ExpeditionLandmarkPhysics(island, i);
                    // Ground colliders and interactive nodes above are identical on headless hosts.
                    if (headless) continue;
                    Cone("Expedition fractured limestone cliff", island, new Vector3(0, -.4f, 0), 10.8f, 7.3f, 5.2f, stone);
                    Cone("Expedition suspended mineral keel", island, new Vector3(0, -5.5f, 0), 7.4f, 1.1f, 7.1f, Color.Lerp(stone, ink, .25f));
                    ExpeditionPortal(island, returnPoint, brass, .9f, "AKADÉMIA");
                    ExpeditionGroundRoute(island, facing);
                    ExpeditionRegionPlaque(island, i, facing);
                    if (i == 0) FractionTideLandmark(island);
                    else if (i == 1) GeometryRuinLandmark(island);
                    else AlgebraObservatoryLandmark(island);
                    for (int rock = 0; rock < 6; rock++)
                    {
                        float a = (rock * 51 + 24) * Mathf.Deg2Rad;
                        var boulder = Shape("Island shoulder weathered rock", PrimitiveType.Cube, island,
                            new Vector3(Mathf.Cos(a) * 9.5f, .15f, Mathf.Sin(a) * 9.5f), new Vector3(1.4f, .9f, 1.1f), stone, false);
                        boulder.transform.localRotation = Quaternion.Euler(12, rock * 37, 8);
                    }
                    // No custom per-prop scripts or realtime lights: static geometry is combined once.
                    PolishPrebatchGeometry(island);
                    var staticGeometry = new List<GameObject>();
                    foreach (var renderer in island.GetComponentsInChildren<MeshRenderer>())
                        if (!renderer.GetComponent<TextMesh>() && !renderer.GetComponentInParent<WorldBillboard>())
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                            staticGeometry.Add(renderer.gameObject);
                        }
                    // Billboards must remain outside static batches so camera-facing labels can rotate.
                    StaticBatchingUtility.Combine(staticGeometry.ToArray(), island.gameObject);
                }
                if (!headless) LivelyNumerionEnvironment();
            }

            private void ExpeditionNode(string id, string subject, string prompt, Transform parent, Vector3 position, float radius)
            {
                var holder = MathGroup(prompt, parent, position);
                var station = holder.gameObject.AddComponent<InteractiveStation>();
                station.Id = id; station.Subject = subject; station.Mode = "expedition";
                station.Prompt = prompt; station.InteractionRadius = radius;
                layout.Stations.Add(station);
            }

            private void ExpeditionLandmarkPhysics(Transform island, int index)
            {
                // Only substantial, already-visible rear structures receive collision. These boxes
                // match the existing visual dimensions and are identical in clients and headless hosts.
                for (int side = -1; side <= 1; side += 2)
                {
                    if (index == 0)
                        ExpeditionSolidBox(island, "Tide archive column collision", new Vector3(-6 + side * 1.2f, 1.5f, 4), new Vector3(.35f, 3, .45f));
                    else if (index == 1)
                        ExpeditionSolidBox(island, "Geometry ruin column collision", new Vector3(side * 3.4f, 2.7f, 4), new Vector3(1.2f, 5.4f, 1.4f));
                    else
                    {
                        ExpeditionSolidBox(island, "Algebra archive pier collision", new Vector3(side * 3.2f, 2.5f, -4), new Vector3(1, 5, 1.5f));
                        ExpeditionSolidBox(island, "Algebra cabinet collision", new Vector3(side * 6, .9f, -4), new Vector3(1.6f, 1.8f, 1));
                    }
                }
            }

            private static void ExpeditionSolidBox(Transform parent, string name, Vector3 position, Vector3 size)
            {
                Transform holder = MathGroup(name, parent, position);
                holder.localScale = size;
                holder.gameObject.AddComponent<BoxCollider>().size = Vector3.one;
            }

            private void ExpeditionPortal(Transform parent, Vector3 position, Color accent, float scale, string label)
            {
                if (headless) return;
                var arch = MathGroup("Working expedition portal / " + label, parent, position);
                arch.localScale = Vector3.one * scale;
                // Two slim piers leave the opening completely walkable; no invisible blocking plane.
                for (int side = -1; side <= 1; side += 2)
                {
                    Shape("Portal limestone pier", PrimitiveType.Cube, arch, new Vector3(side * 1.05f, 1.5f, 0), new Vector3(.36f, 3, .55f), cream, false);
                    Shape("Portal brass track", PrimitiveType.Cube, arch, new Vector3(side * .83f, 1.5f, .30f), new Vector3(.05f, 2.6f, .04f), brass, false, .45f, .65f);
                }
                Shape("Portal lintel", PrimitiveType.Cube, arch, new Vector3(0, 3.02f, 0), new Vector3(2.5f, .36f, .65f), cream, false);
                Torus("Portal inner field rim", arch, new Vector3(0, 1.65f, 0), 1.04f, .045f, accent, new Vector3(90, 0, 0));
                Disc("Portal arrival medallion", arch, new Vector3(0, .055f, 0), 1.15f, .035f, ink, false);
                Shape("Portal warm locator", PrimitiveType.Sphere, arch, new Vector3(0, 3.03f, .37f), Vector3.one * .17f, brass, false, .15f, .65f, .35f);
                ExpeditionSign(arch, label, new Vector3(0, 3.55f, .1f), label.Length > 2 ? .65f : .9f);
            }

            private void ExpeditionGroundRoute(Transform island, float facing)
            {
                // Warm, low-contrast floor arrows lead from the arrival point to the teaching stations.
                for (int i = 0; i < 5; i++)
                {
                    float z = (-5.8f + i * 1.3f) * facing;
                    Shape("Expedition route paving", PrimitiveType.Cube, island, new Vector3(0, .025f, z), new Vector3(2.1f, .025f, .92f), path, false);
                    if (i % 2 == 0)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            var arrow = Shape("Ground direction chevron", PrimitiveType.Cube, island,
                                new Vector3(side * .14f, .047f, z), new Vector3(.06f, .018f, .4f), brass, false);
                            arrow.transform.localRotation = Quaternion.Euler(0, side * 42 * facing, 0);
                        }
                }
                for (int side = -1; side <= 1; side += 2)
                    Shape("Teaching station branch paving", PrimitiveType.Cube, island, new Vector3(side * 2.3f, .03f, 0), new Vector3(3.4f, .025f, 1.8f), path, false);
                Shape("Return gateway side approach", PrimitiveType.Cube, island,
                    new Vector3(2.55f, .03f, -6 * facing), new Vector3(4.3f, .025f, 1.15f), path, false);
            }

            private void ExpeditionTerminal(Transform parent, Vector3 position, string label, Color accent)
            {
                // This cheap box is also built on headless hosts; players cannot walk through the desk.
                Shape("Lesson terminal limestone plinth", PrimitiveType.Cube, parent, position + new Vector3(0, .55f, -.65f), new Vector3(1.5f, 1.1f, .8f), cream, true);
                if (headless) return;
                var folio = Shape("Open field study folio", PrimitiveType.Cube, parent, position + new Vector3(0, 1.18f, -.56f), new Vector3(1.45f, .07f, .68f), accent, false);
                folio.transform.localRotation = Quaternion.Euler(18, 0, 0);
                ExpeditionSign(parent, label, position + new Vector3(0, 2, -.5f), .55f);
                Disc("Lesson interaction floor medallion", parent, position + Vector3.up * .055f, .65f, .025f, accent, false);
            }

            private void ExpeditionSign(Transform parent, string text, Vector3 position, float size)
            {
                if (headless) return;
                var label = WorldLabels.Create(parent, text, position, size, cream);
                // TextMesh uses tenths of a world unit; terminal signs need readable glyphs at walking distance.
                label.characterSize *= 5.5f;
                // Plate follows the text billboard. Positive local Z places it behind the glyphs.
                float width = Mathf.Max(size * 1.5f, text.Length * size * .66f);
                var backing = Shape("Expedition sign / dark slate backing", PrimitiveType.Cube, label.transform,
                    new Vector3(0, 0, .055f), new Vector3(width, size * 1.5f, .06f), ink, false);
                var renderer = backing.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }

            private void ExpeditionRegionPlaque(Transform island, int index, float facing)
            {
                // The name belongs to an existing forecourt desk, rather than floating through a lintel.
                // Its thin plate stays flush with the desk's existing collision envelope.
                var plaque = MathGroup("Field archive region name plaque", island, new Vector3(-4*facing,.58f,-.65f-.42f*facing));
                plaque.localRotation = Quaternion.Euler(0,facing > 0 ? 0 : 180,0);
                Shape("Region plaque / dark slate", PrimitiveType.Cube, plaque, Vector3.zero, new Vector3(1.42f,.72f,.035f), ink, false);
                string text = index == 0 ? "PRÍLIV\nZLOMKOV" : index == 1 ? "GEOMETRICKÉ\nRUINY" : "OBSERVATÓRIUM\nROVNÍC";
                var label = WorldLabels.Create(plaque,text,new Vector3(0,0,-.027f),index == 2 ? 2.15f : index == 1 ? 2.55f : 3f,cream,false);
                label.transform.localRotation = Quaternion.identity;
            }

            private void FractionTideLandmark(Transform island)
            {
                var tidal = MathGroup("Fraction tide / divided tidal pool and gauge house", island, new Vector3(0, 0, 4));
                Disc("Tidal basin stone rim", tidal, Vector3.up * .06f, 3.15f, .12f, cream, false);
                Disc("Tidal pool still mineral water", tidal, Vector3.up * .09f, 2.85f, .025f, tideBlue, false, .05f, .88f);
                if (!expeditionWedge) expeditionWedge = MathExtrudedPolygon("Numerion / reusable eighth of a whole", FractionOutline(), .04f);
                for (int part = 0; part < 8; part++)
                {
                    var slice = MathMesh("Tidal fraction / one eighth", tidal, new Vector3(0, .13f, 0), Vector3.one * 2.65f, expeditionWedge, part < 3 ? agedCopper : new Color(.66f, .71f, .64f));
                    slice.localRotation = Quaternion.Euler(90, part * 45, 0);
                }
                WorldLabels.Create(tidal, "3/8", new Vector3(0, 1.15f, 0), 4.5f, cream);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = new Vector3(5.5f, 0, -2.4f + i * 1.3f);
                    Shape("Tide calibrated measuring post", PrimitiveType.Cube, island, p + Vector3.up * (1 + i * .15f), new Vector3(.16f, 2 + i * .3f, .16f), cream, false);
                    Shape("Tide brass gauge mark", PrimitiveType.Cube, island, p + new Vector3(0, .6f + i * .3f, -.12f), new Vector3(.43f, .06f, .08f), brass, false);
                }
                Shape("Tide archive roof", PrimitiveType.Cube, island, new Vector3(-6, 3, 4), new Vector3(3.2f, .25f, 2.7f), ink, false);
                for (int side = -1; side <= 1; side += 2)
                    Shape("Tide archive stone column", PrimitiveType.Cube, island, new Vector3(-6 + side * 1.2f, 1.5f, 4), new Vector3(.35f, 3, .45f), cream, false);
                WorldLabels.Create(island, "1/4 = 2/8", new Vector3(-6, 2, 4), 3.8f, cream);
            }

            private void GeometryRuinLandmark(Transform island)
            {
                var ruin = MathGroup("Geometry ruins / surveyed archive", island, new Vector3(0, 0, 4));
                for (int side = -1; side <= 1; side += 2)
                {
                    Shape("Surveyed ruin stone column", PrimitiveType.Cube, ruin, new Vector3(side * 3.4f, 2.7f, 0), new Vector3(1.2f, 5.4f, 1.4f), cream, false);
                    Shape("Ruin capital", PrimitiveType.Cube, ruin, new Vector3(side * 3.4f, 5.4f, 0), new Vector3(1.65f, .28f, 1.8f), path, false);
                }
                Shape("Broken geometrical arch lintel", PrimitiveType.Cube, ruin, new Vector3(-1.8f, 5.7f, 0), new Vector3(4.7f, .65f, 1.6f), cream, false);
                var fallen = Shape("Fallen arch fragment", PrimitiveType.Cube, ruin, new Vector3(4.5f, .65f, 1.7f), new Vector3(2.6f, 1.1f, 1.4f), path, false);
                fallen.transform.localRotation = Quaternion.Euler(0, 24, 15);
                // The survey diagram is deliberately behind the walking route.
                MathLink("Three-unit survey side", ruin, new Vector3(-1.5f, .1f, -1), new Vector3(1.5f, .1f, -1), .10f, brass);
                MathLink("Four-unit survey side", ruin, new Vector3(-1.5f, .1f, -1), new Vector3(-1.5f, .1f, 3), .10f, brass);
                MathLink("Five-unit survey hypotenuse", ruin, new Vector3(-1.5f, .1f, 3), new Vector3(1.5f, .1f, -1), .10f, brass);
                WorldLabels.Create(ruin, "3 : 4 : 5", new Vector3(0, 1.3f, 1), 3.8f, cream);
                for (int i = 0; i < 5; i++)
                    Shape("Surveyor ground grid", PrimitiveType.Cube, island, new Vector3(i - 2, .045f, 1.4f), new Vector3(.025f, .02f, 2.4f), new Color(.63f, .61f, .48f), false);
                Banner(island, new Vector3(-7.4f, 0, 3.3f), agedCopper);
            }

            private void AlgebraObservatoryLandmark(Transform island)
            {
                var archive = MathGroup("Algebra observatory / equilibrium instrument", island, new Vector3(0, 0, -4));
                for (int side = -1; side <= 1; side += 2)
                {
                    Shape("Algebra archive stone pier", PrimitiveType.Cube, archive, new Vector3(side * 3.2f, 2.5f, 0), new Vector3(1, 5, 1.5f), cream, false);
                    Shape("Algebra instrument cabinet", PrimitiveType.Cube, archive, new Vector3(side * 6, .9f, 0), new Vector3(1.6f, 1.8f, 1), ink, false);
                }
                Shape("Observatory roof lintel", PrimitiveType.Cube, archive, new Vector3(0, 5.1f, 0), new Vector3(7.5f, .45f, 2.8f), cream, false);
                Torus("Equatorial telescope ring", archive, new Vector3(0, 5.7f, 0), 2.2f, .08f, brass, new Vector3(65, 25, 0));
                Torus("Meridian telescope ring", archive, new Vector3(0, 5.7f, 0), 2.2f, .07f, tideBlue, new Vector3(20, -40, 60));
                Shape("Equilibrium instrument mast", PrimitiveType.Cylinder, archive, new Vector3(0, 1.3f, 1.2f), new Vector3(.17f, 1.3f, .17f), brass, false);
                Shape("Equilibrium instrument balanced beam", PrimitiveType.Cube, archive, new Vector3(0, 2.55f, 1.2f), new Vector3(4.6f, .12f, .14f), brass, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    Shape("Balance pan suspension", PrimitiveType.Cube, archive, new Vector3(side * 1.7f, 2.1f, 1.2f), new Vector3(.04f, .9f, .04f), cream, false);
                    Disc("Balance specimen pan", archive, new Vector3(side * 1.7f, 1.65f, 1.2f), .65f, .06f, brass, false);
                    Shape("Equation unknown specimen", PrimitiveType.Cube, archive, new Vector3(side * 1.7f, 1.95f, 1.2f), Vector3.one * .5f, side < 0 ? tideBlue : cream, false);
                }
                WorldLabels.Create(archive, "2x + 3 = 11", new Vector3(0, 3.4f, 1.2f), 4.2f, cream);
                WorldLabels.Create(archive, "x = 4", new Vector3(0, 2, 1.4f), 3.4f, cream);
            }

            private void LivelyNumerionEnvironment()
            {
                var environment = MathGroup("Numerion / bounded living atmosphere", root, Vector3.zero);
                bool mobile = Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer;
                var motion = environment.gameObject.AddComponent<NumerionEnvironmentMotion>();
                if (!atlasFern) atlasFern = AtlasFernMesh();
                if (!expeditionBird) expeditionBird = AtlasFacetedMesh("Numerion / low polygon swallow",
                    new[] { new Vector3(-.9f, 0, -.15f), new Vector3(-.15f, .1f, .05f), new Vector3(0, 0, .45f), new Vector3(.15f, .1f, .05f), new Vector3(.9f, 0, -.15f), new Vector3(0, 0, -.2f) },
                    new[] { 0, 1, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 5, 1, 0, 5, 2, 1, 5, 3, 2, 5, 4, 3 });
                for (int i = 0; i < (mobile ? 3 : 6); i++)
                {
                    var bird = MathMesh("Distant swallow silhouette", environment, new Vector3(0, 16 + i, 0), Vector3.one * (.32f + i * .03f), expeditionBird, ink);
                    motion.AddBird(bird, 30 + i * 7, 15 + i * 1.1f, i * .9f);
                }
                for (int i = 0; i < (mobile ? 12 : 24); i++)
                {
                    int region = i % 6; float a = i * 2.4f;
                    Vector3 basePosition = region < 3 ? ExpeditionCenters[region] : SectionPositions[region];
                    Vector3 p = basePosition + new Vector3(Mathf.Cos(a) * 8.6f, .06f, Mathf.Sin(a) * 8.6f);
                    var fern = MathMesh("Wind bent meadow fern", environment, p, Vector3.one * (.8f + i % 3 * .22f), atlasFern, AtlasGreens[i % AtlasGreens.Length]);
                    motion.AddFoliage(fern, i * .7f);
                }
                for (int i = 0; i < (mobile ? 2 : 4); i++)
                {
                    int region = i % 4;
                    Vector3 p = (region < 3 ? ExpeditionCenters[region] : Vector3.zero) + new Vector3((i % 5 - 2) * 2.6f, 1.1f + i % 4 * .5f, (i % 3 - 1) * 2.8f);
                    var mote = Shape("Sunlit dust mote", PrimitiveType.Cube, environment, p, Vector3.one * .035f, new Color(.83f, .77f, .58f), false);
                    motion.AddMote(mote.transform, i * .6f);
                }
                for (int i = 0; i < (mobile ? 2 : 3); i++)
                {
                    var cloud = Shape("Slow lower cloud wisp", PrimitiveType.Sphere, environment,
                        new Vector3((i - 1) * 51, -8.5f - i, 35 - i * 28), new Vector3(17, 1.4f, 8), new Color(.65f, .71f, .69f), false);
                    motion.AddCloud(cloud.transform, i * 1.4f);
                }
                for (int i = 0; i < (mobile ? 2 : 3); i++)
                {
                    var ripple = Torus("Tidal pool quiet ripple", environment,
                        ExpeditionCenters[0] + new Vector3(0, .21f, 4), 1.1f + i * .62f, .014f,
                        new Color(.65f, .73f, .67f), Vector3.zero);
                    motion.AddRipple(ripple.transform, i * 1.6f);
                }
                // Warm fixtures emit through material only: no extra realtime light sources.
                foreach (Vector3 center in ExpeditionCenters)
                    for (int side = -1; side <= 1; side += 2) Lantern(environment, center + new Vector3(side * 7, 0, 0));
                foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    /// <summary>One capped, allocation-free animation loop. Reduced motion and photo mode freeze its phase.</summary>
    public sealed class NumerionEnvironmentMotion : MonoBehaviour
    {
        private struct Element { public Transform Item; public Vector3 Rest; public Quaternion Rotation; public float Phase, Radius, Height; }
        private readonly List<Element> foliage = new List<Element>(24), motes = new List<Element>(16), clouds = new List<Element>(3), birds = new List<Element>(6), ripples = new List<Element>(3);
        private float phase;
        public void AddFoliage(Transform item, float seed) => foliage.Add(new Element { Item = item, Rest = item.localPosition, Rotation = item.localRotation, Phase = seed });
        public void AddMote(Transform item, float seed) => motes.Add(new Element { Item = item, Rest = item.localPosition, Phase = seed });
        public void AddCloud(Transform item, float seed) => clouds.Add(new Element { Item = item, Rest = item.localPosition, Phase = seed });
        public void AddRipple(Transform item, float seed) => ripples.Add(new Element { Item = item, Rest = item.localScale, Phase = seed });
        public void AddBird(Transform item, float radius, float height, float seed)
        {
            item.localPosition = new Vector3(Mathf.Cos(seed) * radius, height, Mathf.Sin(seed) * radius);
            item.localRotation = Quaternion.Euler(0, -seed * Mathf.Rad2Deg, 0);
            birds.Add(new Element { Item = item, Radius = radius, Height = height, Phase = seed });
        }
        private void Update()
        {
            if (WorldAnimation.Frozen) return;
            phase += Mathf.Min(Time.deltaTime, .05f);
            for (int i = 0; i < foliage.Count; i++)
            {
                Element e = foliage[i]; if (!e.Item) continue;
                e.Item.localRotation = e.Rotation * Quaternion.Euler(Mathf.Sin(phase * .9f + e.Phase) * 4.5f, 0, Mathf.Sin(phase * .65f + e.Phase) * 3);
            }
            for (int i = 0; i < motes.Count; i++)
            {
                Element e = motes[i]; if (!e.Item) continue;
                e.Item.localPosition = e.Rest + new Vector3(Mathf.Sin(phase * .23f + e.Phase) * .5f, Mathf.Sin(phase * .4f + e.Phase) * .45f, Mathf.Cos(phase * .2f + e.Phase) * .4f);
            }
            for (int i = 0; i < clouds.Count; i++)
            {
                Element e = clouds[i]; if (e.Item) e.Item.localPosition = e.Rest + new Vector3(Mathf.Sin(phase * .025f + e.Phase) * 7, 0, Mathf.Cos(phase * .03f + e.Phase) * 3);
            }
            for (int i = 0; i < ripples.Count; i++)
            {
                Element e = ripples[i]; if (!e.Item) continue;
                float size = 1 + Mathf.Sin(phase * .38f + e.Phase) * .025f;
                e.Item.localScale = new Vector3(e.Rest.x * size, e.Rest.y, e.Rest.z * size);
            }
            for (int i = 0; i < birds.Count; i++)
            {
                Element e = birds[i]; if (!e.Item) continue;
                float a = phase * .055f + e.Phase;
                e.Item.localPosition = new Vector3(Mathf.Cos(a) * e.Radius, e.Height + Mathf.Sin(phase * .3f + e.Phase) * .45f, Mathf.Sin(a) * e.Radius);
                e.Item.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2 + e.Phase) * 5, -a * Mathf.Rad2Deg, -Mathf.Cos(a) * 12);
            }
        }
    }
}
