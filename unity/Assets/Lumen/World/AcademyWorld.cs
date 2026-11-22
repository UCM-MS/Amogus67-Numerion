using System.Collections.Generic;
using UnityEngine;

namespace Lumen.World
{
    /// <summary>Deterministic native academy. The same geometry builds server colliders without render resources.</summary>
    public static partial class AcademyWorld
    {
        public static readonly string[] SubjectIds = { "math", "science", "nature", "history", "language", "technology" };
        public static readonly string[] SubjectNames = { "ČÍSLA A ZLOMKY", "PERCENTÁ A POMER", "VÝRAZY A ROVNICE", "GEOMETRIA", "DÁTA A NÁHODA", "MATEMATIKA V PRAXI" };
        public static readonly Vector3[] SectionPositions = {
            new Vector3(0, 0, 48), new Vector3(42, 0, 24), new Vector3(42, 0, -24),
            new Vector3(0, 0, -48), new Vector3(-42, 0, -24), new Vector3(-42, 0, 24)
        };

        public static WorldLayout Build(bool headless = false) => new Builder(headless).Build();

        private sealed partial class Builder
        {
            private readonly bool headless;
            private readonly WorldLayout layout = new WorldLayout();
            private readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
            private readonly Color cream = new Color(.89f, .87f, .79f);
            private readonly Color path = new Color(.68f, .67f, .59f);
            private readonly Color stone = new Color(.25f, .31f, .34f);
            private readonly Color grass = new Color(.30f, .45f, .33f);
            private readonly Color trunk = new Color(.32f, .26f, .21f);
            private Transform root;

            public Builder(bool headless) { this.headless = headless; }

            public WorldLayout Build()
            {
                layout.Root = new GameObject(headless ? "Academy / Server physics" : "LUMEN / Academy of discovery");
                root = layout.Root.transform;
                Island("Academy commons", Vector3.zero, 20, grass);
                Disc("Central plaza", root, new Vector3(0, 0.04f, 0), 12, 0.12f, path, true);
                Disc("Compass medallion", root, new Vector3(0, 0.18f, 0), 3.6f, 0.16f, cream, true);
                CompassRose();
                Fountain(new Vector3(0, 0.35f, -3.5f));
                HubKiosks();
                Station("pet-arena", "math", "arena", "Aréna spoločníkov", new Vector3(9, 0, -1), new Color(.66f, .5f, .92f));
                Station("study-library", "math", "study", "Matematická knižnica", new Vector3(-10, 0, -2), new Color(.38f, .73f, .7f));
                Station("artifact-workshop", "math", "artifacts", "Dielňa artefaktov", new Vector3(3, 0, -9), new Color(.9f, .67f, .31f));
                PetArenaExhibit();
                MathDiscoveryGarden();
                AcademyLandmark();
                CommonsGardens();
                CampusWayfinding();

                for (int i = 0; i < SectionPositions.Length; i++)
                {
                    Vector3 position = SectionPositions[i];
                    Color accent = LumenMaterials.OutfitColors[i];
                    Island(SubjectNames[i], position, 16.5f, Color.Lerp(grass, accent, 0.18f));
                    Bridge(Vector3.zero, position, accent, i);
                    BuildSection(i, position, accent);
                    for (int tree = 0; tree < 5; tree++)
                    {
                        float angle = tree * 1.1f + i * 0.4f;
                        Vector3 treePosition = position + new Vector3(Mathf.Cos(angle) * 12, 0, Mathf.Sin(angle) * 12);
                        if ((treePosition - Vector3.zero).magnitude > position.magnitude - 4)
                            Tree(treePosition, 0.9f + (tree % 3) * 0.15f, i);
                    }
                    Station("section-" + SubjectIds[i], SubjectIds[i], "quiz", "Preskúmať: " + SubjectNames[i], position + new Vector3(-3.4f, 0, 4), accent);
                    Station("challenge-" + SubjectIds[i], SubjectIds[i], "challenge", "Majstrovská skúška", position + new Vector3(3.4f, 0, 4), accent);
                    Collectible("beacon-" + i, position + new Vector3(0, 0, -8), true, i, accent);
                    for (int crystal = 0; crystal < 3; crystal++)
                    {
                        float angle = crystal * 2.1f + i;
                        Collectible("crystal-" + i + "-" + crystal, position + new Vector3(Mathf.Cos(angle) * 9, 0, Mathf.Sin(angle) * 9), false, i * 3 + crystal, accent);
                    }
                }

                Parkour();
                Tree(new Vector3(-11, 0, 9), 1.3f, 1);
                Tree(new Vector3(11, 0, 9), 1.15f, 3);
                Tree(new Vector3(-13, 0, -7), 1.05f, 0);
                Tree(new Vector3(13, 0, -7), 1.1f, 5);
                Bench(new Vector3(-8, 0, 4), 90);
                Bench(new Vector3(8, 0, 4), -90);
                if (!headless)
                {
                    DistantScenery();
                    AcademyAtmosphere();
                }
                SyntyAcademyDecor.Attach(root, headless);
                return layout;
            }

            private void Island(string name, Vector3 center, float radius, Color tint)
            {
                var island = new GameObject(name);
                island.transform.SetParent(root, false);
                island.transform.localPosition = center;
                Disc("Meadow", island.transform, Vector3.zero, radius, 1.4f, tint, true);
                if (headless) return;
                Cone("Weathered cliff", island.transform, new Vector3(0, -1.35f, 0), radius * 0.98f, radius * 0.7f, 6.5f, stone);
                Cone("Floating foundation", island.transform, new Vector3(0, -7.5f, 0), radius * 0.71f, radius * 0.24f, 8, Color.Lerp(stone, new Color(0.61f, 0.58f, 0.67f), 0.3f));
                for (int i = 0; i < 9; i++)
                {
                    float a = i * Mathf.PI * 2 / 9;
                    var rock = Shape("Cliff facets", PrimitiveType.Cube, island.transform, new Vector3(Mathf.Cos(a) * radius * 0.84f, -3, Mathf.Sin(a) * radius * 0.84f), new Vector3(3.3f, 4.7f, 4), Color.Lerp(stone, tint, 0.1f), false);
                    rock.transform.localRotation = Quaternion.Euler(14, -a * Mathf.Rad2Deg, 8);
                }
            }

            private void Bridge(Vector3 from, Vector3 to, Color accent, int index)
            {
                Vector3 direction = (to - from).normalized;
                float distance = Vector3.Distance(from, to);
                float bridgeLength = distance - 20 - 16.5f + 2;
                Vector3 midpoint = from + direction * ((20 + distance - 16.5f) * 0.5f);
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var group = new GameObject("Learning bridge " + index);
                group.transform.SetParent(root, false);
                group.transform.position = midpoint;
                group.transform.rotation = Quaternion.Euler(0, angle, 0);
                Shape("Walkable stone causeway", PrimitiveType.Cube, group.transform, new Vector3(0, -0.24f, 0), new Vector3(5.4f, 0.48f, bridgeLength), path, true);
                for (int side = -1; side <= 1; side += 2)
                {
                    Shape("Bridge safety rail", PrimitiveType.Cube, group.transform, new Vector3(side * 2.8f, 0.65f, 0), new Vector3(0.24f, 1.3f, bridgeLength - 1), cream, true);
                    if (!headless)
                    {
                        Shape("Rail trim", PrimitiveType.Cube, group.transform, new Vector3(side * 2.8f, 1.33f, 0), new Vector3(0.34f, 0.15f, bridgeLength - 1), accent, false);
                        for (int post = -1; post <= 1; post++)
                            Shape("Bridge pier", PrimitiveType.Cube, group.transform, new Vector3(side * 2.8f, 0.9f, post * bridgeLength * 0.42f), new Vector3(0.6f, 1.8f, 0.6f), cream, false);
                    }
                }
                if (!headless)
                {
                    for (int paving = 0; paving < 4; paving++)
                        Shape("Path inlay", PrimitiveType.Cube, group.transform, new Vector3(0, 0.015f, (paving - 1.5f) * bridgeLength * 0.24f), new Vector3(0.7f, 0.025f, 1.8f), accent, false);
                }
            }

            private void HubKiosks()
            {
                Station("expedition-hub", "math", "coop", "Spoločná výprava", new Vector3(-5, 0, -8), LumenMaterials.OutfitColors[1]);
                Station("companion-garden", "nature", "pets", "Záhrada spoločníkov", new Vector3(7, 0, -7), LumenMaterials.OutfitColors[3]);
                Station("style-studio", "language", "customize", "Tvoj objaviteľ", new Vector3(-7, 0, 7), LumenMaterials.OutfitColors[4]);
            }

            private void BuildSection(int index, Vector3 center, Color accent)
            {
                var group = new GameObject(SubjectNames[index] + " / Pavilion");
                group.transform.SetParent(root, false);
                group.transform.localPosition = center;
                Disc("Learning plaza", group.transform, new Vector3(0, 0.04f, 0), 9.5f, 0.12f, path, true);
                // Open arcades are approachable from every bridge; no wall hides the subject exhibit.
                PavilionArchitecture(group.transform, index, accent);
                if (headless) return;
                WorldLabels.Create(group.transform, SubjectNames[index], new Vector3(0, 6.35f, 2.2f), 2.4f, cream);
                WorldLabels.Create(group.transform, "OBJAVUJ  ·  PREMÝŠĽAJ  ·  VYSVETLI", new Vector3(0, 0.85f, 7.5f), 0.55f, stone);
                Shape("Exhibit plinth", PrimitiveType.Cylinder, group.transform, new Vector3(0, 0.65f, -1.2f), new Vector3(2.8f, 0.65f, 2.8f), cream, false);
                Bench(center + new Vector3(-8, 0, 0), 90);
                Bench(center + new Vector3(8, 0, 0), -90);
                SubjectGarden(group.transform, index, accent);
            }

            private void Station(string id, string subject, string mode, string prompt, Vector3 position, Color accent)
            {
                var station = new GameObject("Interactive / " + id);
                station.transform.SetParent(root, false);
                station.transform.position = position;
                var component = station.AddComponent<InteractiveStation>();
                component.Id = id; component.Subject = subject; component.Mode = mode; component.Prompt = prompt;
                layout.Stations.Add(component);
                Shape("Kiosk pedestal", PrimitiveType.Cube, station.transform, new Vector3(0, 0.6f, 0), new Vector3(1.1f, 1.2f, 0.8f), cream, true);
                if (headless) return;
                var screen = Shape("Interactive crystal", PrimitiveType.Cube, station.transform, new Vector3(0, 1.5f, 0), new Vector3(1.3f, 0.9f, 0.12f), accent, false, 0.25f, 0.7f, 0.15f);
                screen.transform.localRotation = Quaternion.Euler(15, 0, 0);
                WorldLabels.Create(station.transform, mode == "challenge" ? "VÝZVA" : mode == "quiz" ? "OBJAV" : prompt, new Vector3(0, 2.35f, 0), 0.5f, stone);
                Disc("Interaction marker", station.transform, new Vector3(0, 0.025f, 1.25f), 0.65f, 0.02f, accent, false);
            }

            private void Collectible(string id, Vector3 position, bool beacon, int seed, Color tint)
            {
                var group = new GameObject((beacon ? "Team beacon / " : "Knowledge crystal / ") + id);
                group.transform.SetParent(root, false);
                group.transform.position = position;
                var collectible = group.AddComponent<WorldCollectible>();
                collectible.Id = id; collectible.IsBeacon = beacon; collectible.Value = beacon ? 5 : 1;
                layout.Collectibles.Add(collectible);
                if (beacon) Disc("Beacon footing", group.transform, new Vector3(0, 0.15f, 0), 1.4f, 0.3f, cream, true);
                if (headless) return;
                var crystal = Shape("Crystal", PrimitiveType.Cube, group.transform, new Vector3(0, beacon ? 1.6f : 0.9f, 0), Vector3.one * (beacon ? 0.95f : 0.55f), tint, false, 0.3f, 0.8f, 1.5f);
                crystal.transform.localRotation = Quaternion.Euler(35, 45, 20);
                collectible.Initialize(crystal.transform, seed);
                if (beacon)
                {
                    Ring(group.transform, new Vector3(0, 1.65f, 0), 1.15f, tint, new Vector3(75, 0, 0));
                    WorldLabels.Create(group.transform, "TÍMOVÝ MAJÁK", new Vector3(0, 3.1f, 0), 0.7f, cream);
                }
            }

            private void Parkour()
            {
                Vector3 start = new Vector3(14, 0, 14);
                Station("sky-course", "science", "parkour", "Nebeská dráha", start, LumenMaterials.OutfitColors[6]);
                // Short gaps and <=0.45m rises are compatible with the standard 1.8m character jump.
                for (int i = 0; i < 9; i++)
                {
                    Vector3 center = new Vector3(20 + (i % 3) * 2.7f, 0.3f + i * 0.4f, 22 + (i / 3) * 3.3f);
                    if (i / 3 == 1) center.x = 25.4f - (i % 3) * 2.7f;
                    Shape("Parkour checkpoint " + (i + 1), PrimitiveType.Cube, root, new Vector3(center.x, center.y * 0.5f, center.z), new Vector3(2.2f, center.y, 2.5f), i % 2 == 0 ? cream : LumenMaterials.OutfitColors[6], true);
                    layout.ParkourPlatforms.Add(center + Vector3.up * 0.1f);
                    if (!headless) WorldLabels.Create(root, (i + 1).ToString(), center + Vector3.up * 1.1f, 0.8f, stone);
                }
                // A narrow connecting ledge provides a consistent start above the void.
                var approach = Shape("Course approach", PrimitiveType.Cube, root, new Vector3(17, -0.2f, 18), new Vector3(3.2f, 0.4f, 10), path, true);
                approach.transform.rotation = Quaternion.Euler(0, 36.87f, 0);
                if (!headless) WorldLabels.Create(root, "NEBESKÁ DRÁHA", new Vector3(23, 6, 29), 1.1f, cream);
            }

            private void Fountain(Vector3 position)
            {
                Disc("Fountain basin", root, position, 2.6f, 0.35f, cream, true);
                if (headless) return;
                Disc("Still reflective water", root, position + Vector3.up * 0.04f, 2.25f, 0.03f, new Color(0.28f, 0.73f, 0.79f), false, 0.55f, 0.97f);
                Torus("Fountain limestone rim", root, position + Vector3.up * 0.12f, 2.48f, 0.16f, cream, Vector3.zero);
                Shape("Fountain stone pedestal", PrimitiveType.Cylinder, root, position + Vector3.up * 0.5f, new Vector3(0.65f, 0.5f, 0.65f), cream, false);
                Disc("Upper fountain bowl", root, position + Vector3.up * 1.1f, 1, 0.18f, cream, false);
                Disc("Upper fountain water", root, position + Vector3.up * 1.12f, 0.86f, 0.025f, new Color(0.28f, 0.73f, 0.79f), false, 0.4f, 0.92f);
                Shape("Lumen heart", PrimitiveType.Sphere, root, position + Vector3.up * 1.5f, Vector3.one * 0.9f, LumenMaterials.OutfitColors[0], false, 0.3f, 0.85f, 0.4f);
                Ring(root, position + Vector3.up * 1.5f, 1.15f, LumenMaterials.OutfitColors[6], new Vector3(75, 20, 0));
            }

            private void CompassRose()
            {
                if (headless) return;
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 60 * Mathf.Deg2Rad;
                    var tile = Shape("Compass ray", PrimitiveType.Cube, root, new Vector3(Mathf.Sin(angle) * 2, 0.19f, Mathf.Cos(angle) * 2), new Vector3(0.24f, 0.02f, 1.9f), LumenMaterials.OutfitColors[i], false);
                    tile.transform.localRotation = Quaternion.Euler(0, i * 60, 0);
                }
            }

            private void Tree(Vector3 position, float scale, int style)
            {
                var group = new GameObject("Wind garden / Tree");
                group.transform.SetParent(root, false); group.transform.position = position; group.transform.localScale = Vector3.one * scale;
                Shape("Tree trunk", PrimitiveType.Cylinder, group.transform, new Vector3(0, 1.1f, 0), new Vector3(0.55f, 1.1f, 0.55f), trunk, true);
                if (headless) return;
                Color foliage = style % 3 == 0 ? new Color(0.57f, 0.7f, 0.9f) : style % 3 == 1 ? new Color(0.42f, 0.7f, 0.55f) : new Color(0.78f, 0.63f, 0.77f);
                Shape("Faceted canopy", PrimitiveType.Sphere, group.transform, new Vector3(0, 3.2f, 0), new Vector3(3.2f, 3.5f, 3.1f), foliage, false);
                Shape("Canopy highlight", PrimitiveType.Sphere, group.transform, new Vector3(0.9f, 3, 0.3f), new Vector3(2.1f, 2.4f, 2.2f), Color.Lerp(foliage, cream, 0.16f), false);
            }

            private void Bench(Vector3 position, float yaw)
            {
                var group = new GameObject("Rest / Bench"); group.transform.SetParent(root, false); group.transform.position = position; group.transform.rotation = Quaternion.Euler(0, yaw, 0);
                Shape("Bench seat", PrimitiveType.Cube, group.transform, new Vector3(0, 0.55f, 0), new Vector3(2.4f, 0.2f, 0.85f), cream, true);
                Shape("Bench back", PrimitiveType.Cube, group.transform, new Vector3(0, 1, -0.4f), new Vector3(2.4f, 0.8f, 0.14f), cream, true);
                for (int side = -1; side <= 1; side += 2) Shape("Bench foot", PrimitiveType.Cube, group.transform, new Vector3(side * 0.8f, 0.2f, 0), new Vector3(0.18f, 0.4f, 0.7f), stone, false);
            }

            private void Ring(Transform parent, Vector3 center, float radius, Color tint, Vector3 rotation)
            {
                if (headless) return;
                Torus("Orbit ring", parent, center, radius, 0.045f, tint, rotation);
            }

            private void DistantScenery()
            {
                Disc("Cloud sea", root, new Vector3(0, -23, 0), 360, 0.1f, new Color(0.57f, 0.77f, 0.86f), false, 0, 0.9f);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 2.39996f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * (95 + i * 8), -14 - i % 3 * 3, Mathf.Sin(a) * (95 + i * 8));
                    Cone("Distant floating isle", root, p, 10 + i % 3 * 4, 1, 18, new Color(0.61f, 0.7f, 0.77f));
                    for (int cloud = 0; cloud < 3; cloud++)
                        Shape("Cloudbank", PrimitiveType.Sphere, root, p + new Vector3(cloud * 8 - 8, 5, 0), new Vector3(24, 5, 13), cream, false);
                }
            }

            private GameObject Shape(string name, PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Color tint, bool solid, float metal = 0, float smoothness = 0.4f, float glow = 0)
            {
                var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
                if (!headless)
                {
                    if (!meshes.TryGetValue(primitive, out var mesh))
                    {
                        var source = GameObject.CreatePrimitive(primitive); source.SetActive(false); mesh = source.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(source); meshes[primitive] = mesh;
                    }
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = obj.AddComponent<MeshRenderer>();
                    string key = ColorUtility.ToHtmlStringRGB(tint) + "/" + metal + "/" + smoothness + "/" + glow;
                    if (name == "Tree trunk" || name.StartsWith("Bench"))
                        renderer.sharedMaterial = LumenMaterials.GetSurface("wood", tint, Mathf.Max(scale.y, scale.x) * 0.5f);
                    else if (name.Contains("wall") || name.Contains("stone") || name.Contains("causeway") || name.Contains("column") || name == "Floating canopy" || name.Contains("checkpoint"))
                        renderer.sharedMaterial = LumenMaterials.GetSurface("stone", tint, Mathf.Max(scale.x, scale.z) * 0.4f);
                    else if (metal > 0 && glow == 0)
                        renderer.sharedMaterial = LumenMaterials.GetSurface("metal", tint, 1, metal, smoothness);
                    else renderer.sharedMaterial = LumenMaterials.Get(key, tint, metal, smoothness, glow);
                }
                if (solid)
                {
                    // Primitive cylinder has unit diameter and height 2. Box bounds are deterministic on headless servers.
                    var collider = obj.AddComponent<BoxCollider>(); collider.size = primitive == PrimitiveType.Cylinder ? new Vector3(1, 2, 1) : Vector3.one;
                }
                return obj;
            }

            private GameObject Disc(string name, Transform parent, Vector3 position, float radius, float height, Color tint, bool solid, float metal = 0, float smoothness = 0.4f)
            {
                var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
                if (headless && !solid) return obj;
                Mesh mesh = CylinderMesh(radius, radius, height);
                if (solid) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
                if (!headless)
                {
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = obj.AddComponent<MeshRenderer>();
                    if (name == "Meadow") renderer.sharedMaterial = LumenMaterials.GetSurface("grass", tint, radius * 0.5f);
                    else if (solid) renderer.sharedMaterial = LumenMaterials.GetSurface("stone", tint, radius * 0.4f, metal, smoothness);
                    else renderer.sharedMaterial = LumenMaterials.Get(ColorUtility.ToHtmlStringRGB(tint) + "/" + metal + "/" + smoothness, tint, metal, smoothness);
                }
                return obj;
            }

            private void Cone(string name, Transform parent, Vector3 position, float top, float bottom, float height, Color tint)
            {
                if (headless) return;
                var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
                obj.AddComponent<MeshFilter>().sharedMesh = CylinderMesh(top, bottom, height);
                obj.AddComponent<MeshRenderer>().sharedMaterial = LumenMaterials.Get(ColorUtility.ToHtmlStringRGB(tint), tint);
            }

            private static Mesh CylinderMesh(float topRadius, float bottomRadius, float height)
            {
                const int sides = 32;
                var vertices = new Vector3[(sides + 1) * 2 + 2];
                var triangles = new int[sides * 12];
                var uv = new Vector2[vertices.Length];
                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    vertices[i * 2] = new Vector3(Mathf.Cos(angle) * topRadius, 0, Mathf.Sin(angle) * topRadius);
                    vertices[i * 2 + 1] = new Vector3(Mathf.Cos(angle) * bottomRadius, -height, Mathf.Sin(angle) * bottomRadius);
                    uv[i * 2] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f + Vector2.one * 0.5f;
                    uv[i * 2 + 1] = uv[i * 2];
                }
                int topCenter = vertices.Length - 2, bottomCenter = vertices.Length - 1;
                vertices[topCenter] = Vector3.zero; vertices[bottomCenter] = Vector3.down * height;
                uv[topCenter] = uv[bottomCenter] = Vector2.one * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    int a = i * 2, b = a + 1, c = a + 2, d = a + 3, t = i * 12;
                    triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
                    triangles[t + 3] = c; triangles[t + 4] = d; triangles[t + 5] = b;
                    triangles[t + 6] = topCenter; triangles[t + 7] = c; triangles[t + 8] = a;
                    triangles[t + 9] = bottomCenter; triangles[t + 10] = b; triangles[t + 11] = d;
                }
                var mesh = new Mesh { name = "Lumen deterministic island mesh", vertices = vertices, triangles = triangles, uv = uv };
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
