using UnityEngine;

namespace Lumen.World
{
    public static partial class AcademyWorld
    {
        private sealed partial class Builder
        {
            private readonly Color ink = new Color(.10f, .17f, .22f);
            private readonly Color teal = new Color(.15f, .35f, .36f);
            private readonly Color brass = new Color(.74f, .55f, .29f);

            private void AcademyLandmark()
            {
                var hall = new GameObject("Academy landmark / Observatory of discovery").transform;
                hall.SetParent(root, false);
                hall.localPosition = new Vector3(0, 0, -13.4f);

                // Two tall piers frame an open walk-through arch. The southern bridge remains clear.
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 3.4f;
                    Shape("Observatory foundation", PrimitiveType.Cube, hall, new Vector3(x, 0.22f, 0), new Vector3(2.1f, 0.44f, 3.6f), ink, true);
                    Shape("Observatory stone column", PrimitiveType.Cube, hall, new Vector3(x, 3.5f, 0), new Vector3(1.65f, 7, 2.8f), cream, true);
                    Shape("Observatory inset", PrimitiveType.Cube, hall, new Vector3(x, 3.8f, 1.43f), new Vector3(0.82f, 4.6f, 0.08f), teal, false);
                    Shape("Observatory window", PrimitiveType.Cube, hall, new Vector3(x, 4.3f, 1.49f), new Vector3(0.5f, 2.5f, 0.055f), new Color(0.69f, 0.89f, 0.81f), false, 0.45f, 0.85f);
                    Shape("Observatory gold sill", PrimitiveType.Cube, hall, new Vector3(x, 2.8f, 1.55f), new Vector3(1, 0.14f, 0.25f), brass, false, 0.55f, 0.7f);
                    Shape("Observatory capital", PrimitiveType.Cube, hall, new Vector3(x, 7.1f, 0), new Vector3(2.1f, 0.42f, 3.4f), path, true);
                }
                Shape("Observatory stone lintel", PrimitiveType.Cube, hall, new Vector3(0, 7.15f, 0), new Vector3(8.8f, 1.1f, 3.25f), cream, true);
                Shape("Observatory teal frieze", PrimitiveType.Cube, hall, new Vector3(0, 7.65f, 1.69f), new Vector3(8.3f, 0.34f, 0.18f), teal, false);
                Shape("Observatory upper drum", PrimitiveType.Cylinder, hall, new Vector3(0, 9.15f, 0), new Vector3(7.5f, 1.2f, 5.4f), cream, false);
                if (headless) return;

                Shape("Observatory glazed dome", PrimitiveType.Sphere, hall, new Vector3(0, 10.15f, 0), new Vector3(8.2f, 5.3f, 6.4f), teal, false, 0.4f, 0.78f);
                Torus("Observatory dome base", hall, new Vector3(0, 9.9f, 0), 4.05f, 0.12f, brass, Vector3.zero);
                for (int rib = 0; rib < 4; rib++)
                    Torus("Dome brass meridian", hall, new Vector3(0, 10.1f, 0), 3.35f, 0.055f, brass, new Vector3(90, rib * 45, 0));
                Shape("Observatory finial", PrimitiveType.Cylinder, hall, new Vector3(0, 13.3f, 0), new Vector3(0.18f, 0.8f, 0.18f), brass, false, 0.65f, 0.7f);
                Shape("North star", PrimitiveType.Sphere, hall, new Vector3(0, 14.25f, 0), Vector3.one * 0.65f, brass, false, 0.5f, 0.8f, 0.25f);

                // A legible facade sign, with a contrasting field instead of floating text in the sky.
                Shape("Academy nameplate", PrimitiveType.Cube, hall, new Vector3(0, 6.65f, 1.76f), new Vector3(5.5f, 1.04f, 0.13f), ink, false);
                WorldLabels.Create(hall, "L U M E N", new Vector3(0, 6.77f, 1.85f), 5.8f, cream, false);
                WorldLabels.Create(hall, "AKADÉMIA OBJAVOV", new Vector3(0, 6.38f, 1.86f), 1.55f, new Color(0.84f, 0.86f, 0.7f), false);

                Torus("Astronomical clock bezel", hall, new Vector3(0, 9.2f, 3.45f), 1.18f, 0.09f, brass, new Vector3(90, 0, 0));
                var face = Shape("Astronomical clock face", PrimitiveType.Cylinder, hall, new Vector3(0, 9.2f, 3.42f), new Vector3(2.3f, 0.06f, 2.3f), ink, false);
                face.transform.localRotation = Quaternion.Euler(90, 0, 0);
                for (int hour = 0; hour < 12; hour++)
                {
                    float a = hour * Mathf.PI / 6;
                    var tick = Shape("Clock hour", PrimitiveType.Cube, hall, new Vector3(Mathf.Sin(a) * 0.94f, 9.2f + Mathf.Cos(a) * 0.94f, 3.51f), new Vector3(0.055f, 0.16f, 0.035f), brass, false);
                    tick.transform.localRotation = Quaternion.Euler(0, 0, -hour * 30);
                }
                var hand = Shape("Clock minute hand", PrimitiveType.Cube, hall, new Vector3(0.22f, 9.48f, 3.55f), new Vector3(0.06f, 0.8f, 0.045f), cream, false);
                hand.transform.localRotation = Quaternion.Euler(0, 0, -38);
                var hourHand = Shape("Clock hour hand", PrimitiveType.Cube, hall, new Vector3(-0.2f, 9.22f, 3.56f), new Vector3(0.5f, 0.09f, 0.05f), cream, false);
                hourHand.transform.localRotation = Quaternion.Euler(0, 0, 12);
                Shape("Clock pivot", PrimitiveType.Sphere, hall, new Vector3(0, 9.2f, 3.58f), Vector3.one * 0.14f, brass, false, 0.6f, 0.75f);

                // Facade books and banners make this a place for learning rather than an empty monument.
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int book = 0; book < 5; book++)
                        Shape("Facade library volume", PrimitiveType.Cube, hall, new Vector3(side * 3.4f + (book - 2) * 0.18f, 1.2f + (book % 2) * 0.1f, 1.5f), new Vector3(0.13f, 0.7f + (book % 3) * 0.13f, 0.27f), LumenMaterials.OutfitColors[(book + 1) % 6], false);
                    Banner(hall, new Vector3(side * 5.1f, 0, 0.9f), side < 0 ? LumenMaterials.OutfitColors[0] : teal);
                }
                Torus("Gateway arch gold inner curve", hall, new Vector3(0, 4.95f, 1.65f), 2.5f, 0.11f, brass, new Vector3(90, 0, 0));
            }

            private void CommonsGardens()
            {
                // A circular promenade and radial paths visually connect the six learning islands.
                if (!headless)
                {
                    Torus("Commons promenade border", root, new Vector3(0, 0.065f, 0), 11.6f, 0.15f, cream, Vector3.zero);
                    for (int i = 0; i < SectionPositions.Length; i++)
                    {
                        Vector3 direction = SectionPositions[i].normalized;
                        var paving = Shape("Academy radial stone path", PrimitiveType.Cube, root, direction * 15.5f + Vector3.up * 0.025f, new Vector3(4.9f, 0.035f, 9), path, false);
                        paving.transform.rotation = Quaternion.LookRotation(direction);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector3 cross = Vector3.Cross(Vector3.up, direction) * side;
                            Lantern(root, direction * 17.4f + cross * 3.3f);
                        }
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                        Vector3 p = new Vector3(Mathf.Cos(a) * 10.4f, 0, Mathf.Sin(a) * 10.4f);
                        // Keep the four activity kiosks, spawn lane and observatory doorway open.
                        if (p.z < -5 || p.z > 8 || p.x < -8 && p.z > 3) continue;
                        Planter(root, p, 1.65f, LumenMaterials.OutfitColors[(i + 2) % 6]);
                    }
                }
                CompanionGarden();
                StyleStudio();
            }

            private void CompanionGarden()
            {
                var garden = new GameObject("Companion garden / Nursery and training lawn").transform;
                garden.SetParent(root, false);
                garden.localPosition = new Vector3(9.6f, 0, -8.4f);
                // The actual pet station remains at (7,-7); all shelter collision is behind it.
                Shape("Pet shelter back wall", PrimitiveType.Cube, garden, new Vector3(0, 1.35f, -1.8f), new Vector3(3.8f, 2.7f, 0.25f), cream, true);
                Shape("Pet shelter column", PrimitiveType.Cube, garden, new Vector3(-1.8f, 1.3f, -0.1f), new Vector3(0.2f, 2.6f, 0.2f), cream, true);
                Shape("Pet shelter column", PrimitiveType.Cube, garden, new Vector3(1.8f, 1.3f, -0.1f), new Vector3(0.2f, 2.6f, 0.2f), cream, true);
                Shape("Pet nursery roof", PrimitiveType.Cube, garden, new Vector3(0, 2.85f, -1), new Vector3(4.3f, 0.28f, 3), teal, true);
                if (headless) return;
                Shape("Pet roof trim", PrimitiveType.Cube, garden, new Vector3(0, 3.05f, -1), new Vector3(4.5f, 0.13f, 3.15f), brass, false, 0.3f, 0.6f);
                WorldLabels.Create(garden, "ZÁHRADA SPOLOČNÍKOV", new Vector3(0, 3.5f, 0.2f), 2.2f, ink);
                for (int bed = 0; bed < 3; bed++)
                {
                    Vector3 p = new Vector3((bed - 1) * 1.04f, 0.18f, -1);
                    Shape("Companion cushion", PrimitiveType.Sphere, garden, p, new Vector3(0.9f, 0.24f, 0.8f), LumenMaterials.OutfitColors[bed], false);
                    Shape("Nursery egg sculpture", PrimitiveType.Sphere, garden, p + Vector3.up * 0.49f, new Vector3(0.5f, 0.72f, 0.5f), cream, false, 0, 0.65f);
                    Torus("Egg species band", garden, p + Vector3.up * 0.42f, 0.25f, 0.035f, LumenMaterials.OutfitColors[bed], Vector3.zero);
                }
                Torus("Companion training hoop", garden, new Vector3(3.7f, 0.85f, 0.8f), 0.78f, 0.12f, LumenMaterials.OutfitColors[0], new Vector3(90, 0, 0));
                Shape("Training hoop foot", PrimitiveType.Cube, garden, new Vector3(3.7f, 0.11f, 0.8f), new Vector3(1.9f, 0.16f, 0.7f), cream, false);
                for (int toy = 0; toy < 3; toy++)
                    Shape("Training stepping stone", PrimitiveType.Cylinder, garden, new Vector3(2.5f + toy * 0.65f, 0.1f, 2.4f), new Vector3(0.42f, 0.1f, 0.42f), LumenMaterials.OutfitColors[(toy + 2) % 6], false);
                Shape("Companion feeding bowl", PrimitiveType.Cylinder, garden, new Vector3(-0.9f, 0.17f, 0.85f), new Vector3(0.62f, 0.13f, 0.62f), brass, false, 0.45f, 0.65f);
                Shape("Bowl inset", PrimitiveType.Cylinder, garden, new Vector3(-0.9f, 0.3f, 0.85f), new Vector3(0.48f, 0.025f, 0.48f), ink, false);
                Planter(garden, new Vector3(2.6f, 0, -2), 0.8f, LumenMaterials.OutfitColors[4]);
                Lantern(garden, new Vector3(-2.4f, 0, -2));
            }

            private void StyleStudio()
            {
                var studio = new GameObject("Explorer studio / Wardrobe pergola").transform;
                studio.SetParent(root, false);
                studio.localPosition = new Vector3(-8.6f, 0, 7.8f);
                // Place the dressing display beside the existing customization kiosk.
                for (int side = -1; side <= 1; side += 2)
                    Shape("Studio timber column", PrimitiveType.Cube, studio, new Vector3(side * 2.4f, 1.7f, -1.3f), new Vector3(0.22f, 3.4f, 0.22f), trunk, true);
                if (headless) return;
                for (int beam = 0; beam < 5; beam++)
                    Shape("Studio timber pergola", PrimitiveType.Cube, studio, new Vector3((beam - 2) * 1.18f, 3.5f, -1.3f), new Vector3(0.18f, 0.18f, 3), cream, false);
                Shape("Studio canopy ribbon", PrimitiveType.Cube, studio, new Vector3(0, 3.43f, -1.3f), new Vector3(5.3f, 0.07f, 2.8f), LumenMaterials.OutfitColors[4], false);
                WorldLabels.Create(studio, "TVOJ OBJAVITEĽ", new Vector3(0, 4.05f, -0.6f), 2.15f, ink);
                for (int fabric = 0; fabric < 4; fabric++)
                    Shape("Studio hanging fabric", PrimitiveType.Cube, studio, new Vector3(-1.75f + fabric * 0.46f, 1.85f, -2.3f), new Vector3(0.35f, 1.3f, 0.13f), LumenMaterials.OutfitColors[fabric], false);
                Planter(studio, new Vector3(-3.15f, 0, -1.7f), 0.85f, LumenMaterials.OutfitColors[0]);
            }

            private void PavilionArchitecture(Transform group, int index, Color accent)
            {
                Color roof = Color.Lerp(ink, teal, .48f + index * .055f);
                for (int side = -1; side <= 1; side += 2)
                    for (int depth = 0; depth < 2; depth++)
                    {
                        Vector3 p = new Vector3(side * 4.7f, 0, -4.1f + depth * 5.5f);
                        Shape("Pavilion stone column", PrimitiveType.Cylinder, group, p + Vector3.up * 2.4f, new Vector3(0.48f, 2.4f, 0.48f), cream, true);
                        Shape("Pavilion stone column base", PrimitiveType.Cube, group, p + Vector3.up * 0.18f, new Vector3(0.85f, 0.36f, 0.85f), path, true);
                        Shape("Pavilion capital", PrimitiveType.Cube, group, p + Vector3.up * 4.7f, new Vector3(0.85f, 0.3f, 0.85f), cream, false);
                    }
                Shape("Pavilion roof stone edge", PrimitiveType.Cube, group, new Vector3(0, 5.05f, -1.4f), new Vector3(11, 0.45f, 8.6f), cream, true);
                if (headless) return;
                Shape("Pavilion colored roof", PrimitiveType.Cube, group, new Vector3(0, 5.4f, -1.4f), new Vector3(10.5f, 0.28f, 8.15f), roof, false);
                Shape("Pavilion subject frieze", PrimitiveType.Cube, group, new Vector3(0, 5.93f, 2.55f), new Vector3(10.2f, 0.95f, 0.22f), roof, false);
                Shape("Pavilion gold cornice", PrimitiveType.Cube, group, new Vector3(0, 6.45f, 2.55f), new Vector3(10.4f, 0.12f, 0.3f), brass, false, 0.45f, 0.65f);
                // Individual skylight crowns distinguish the six departments at a distance.
                Shape("Pavilion skylight drum", PrimitiveType.Cylinder, group, new Vector3(0, 5.8f, -1.5f), new Vector3(3.4f, 0.35f, 3.4f), cream, false);
                Cone("Department skylight crown", group, new Vector3(0, 8.6f, -1.5f), 0.12f, 2.2f, 2.4f, roof);
                Shape("Department golden finial", PrimitiveType.Sphere, group, new Vector3(0, 8.75f, -1.5f), Vector3.one * 0.32f, brass, false, 0.65f, 0.75f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Banner(group, new Vector3(side * 6.1f, 0, 0), Color.Lerp(teal, accent, .15f));
                    Lantern(group, new Vector3(side * 5.6f, 0, 5.2f));
                }
            }

            private void SubjectGarden(Transform group, int index, Color accent)
            {
                if (headless) return;
                Planter(group, new Vector3(-6.8f, 0, -5.4f), 1.25f, accent);
                Planter(group, new Vector3(6.8f, 0, -5.4f), 1.25f, accent);
                for (int i = 0; i < 4; i++)
                    Shape("Learning plaza accent tile", PrimitiveType.Cube, group, new Vector3((i - 1.5f) * 0.55f, 0.055f, 6.6f), new Vector3(0.32f, 0.025f, 0.32f), accent, false);
                if (index == 2)
                {
                    for (int plant = 0; plant < 4; plant++)
                    {
                        Vector3 p = new Vector3(-5 + plant * 3.3f, 0, -9.5f);
                        Shape("Botanical raised bed", PrimitiveType.Cube, group, p + Vector3.up * 0.22f, new Vector3(2.3f, 0.44f, 1.6f), trunk, false);
                        for (int shoot = 0; shoot < 3; shoot++)
                        {
                            Vector3 stem = p + new Vector3((shoot - 1) * 0.65f, 0.75f, 0);
                            Shape("Garden leaf shoot", PrimitiveType.Sphere, group, stem, new Vector3(0.45f, 0.8f + plant * 0.12f, 0.55f), Color.Lerp(grass, accent, 0.5f), false);
                        }
                    }
                }
                else if (index == 3)
                {
                    for (int pillar = 0; pillar < 3; pillar++)
                    {
                        Vector3 p = new Vector3((pillar - 1) * 3.3f, 1.5f + pillar * 0.4f, -9.8f);
                        Shape("History ruin stone column", PrimitiveType.Cylinder, group, p, new Vector3(0.85f, 1.5f + pillar * 0.4f, 0.85f), path, false);
                        Shape("History ruin capital", PrimitiveType.Cube, group, p + Vector3.up * (1.55f + pillar * 0.4f), new Vector3(1.3f, 0.25f, 1.3f), cream, false);
                    }
                }
                else if (index == 4)
                {
                    for (int shelf = 0; shelf < 2; shelf++)
                        for (int book = 0; book < 7; book++)
                            Shape("Outdoor library book", PrimitiveType.Cube, group, new Vector3(-7.6f + book * 0.27f, 0.75f + shelf * 1.05f, 2.6f), new Vector3(0.2f, 0.8f + book % 2 * 0.15f, 0.6f), LumenMaterials.OutfitColors[book % 6], false);
                }
            }

            private void CampusWayfinding()
            {
                if (headless) return;
                var sign = new GameObject("Commons / Campus directory").transform;
                sign.SetParent(root, false);
                sign.localPosition = new Vector3(4, 0, 9.3f);
                Shape("Directory wooden post", PrimitiveType.Cylinder, sign, new Vector3(0, 1.3f, 0), new Vector3(0.16f, 1.3f, 0.16f), trunk, false);
                for (int row = 0; row < 3; row++)
                {
                    Shape("Directory board", PrimitiveType.Cube, sign, new Vector3(0, 2.5f - row * 0.43f, 0), new Vector3(3.7f, 0.37f, 0.16f), row == 0 ? teal : ink, false);
                    WorldLabels.Create(sign, row == 0 ? "ŠESŤ OSTROVOV POZNANIA" : row == 1 ? "ZÁHRADA SPOLOČNÍKOV  →" : "←  SPOLOČNÁ VÝPRAVA", new Vector3(0, 2.5f - row * 0.43f, 0.095f), 1.05f, cream, false);
                }
            }

            private void Banner(Transform parent, Vector3 position, Color accent)
            {
                if (headless) return;
                Shape("Banner brass pole", PrimitiveType.Cylinder, parent, position + Vector3.up * 2.6f, new Vector3(0.09f, 2.6f, 0.09f), brass, false, 0.55f, 0.6f);
                var fabric = Shape("Academy fabric pennant", PrimitiveType.Cube, parent, position + new Vector3(0.55f, 4.1f, 0), new Vector3(1, 1.8f, 0.07f), accent, false);
                fabric.GetComponent<MeshFilter>().sharedMesh = AtlasClothMesh();
                Shape("Pennant gold embroidery", PrimitiveType.Cube, parent, position + new Vector3(0.55f, 3.35f, 0.045f), new Vector3(0.85f, 0.07f, 0.02f), brass, false, 0.4f, 0.55f);
                var emblem = Shape("Pennant discovery emblem", PrimitiveType.Cube, parent, position + new Vector3(0.55f, 4.15f, 0.055f), new Vector3(0.28f, 0.28f, 0.02f), cream, false);
                emblem.transform.localRotation = Quaternion.Euler(0, 0, 45);
                Shape("Banner finial", PrimitiveType.Sphere, parent, position + Vector3.up * 5.23f, Vector3.one * 0.22f, brass, false, 0.65f, 0.7f);
            }

            private void Lantern(Transform parent, Vector3 position)
            {
                if (headless) return;
                Shape("Lantern stone foot", PrimitiveType.Cube, parent, position + Vector3.up * 0.15f, new Vector3(0.6f, 0.3f, 0.6f), cream, false);
                Shape("Lantern metal post", PrimitiveType.Cylinder, parent, position + Vector3.up * 1.5f, new Vector3(0.11f, 1.4f, 0.11f), ink, false, 0.45f, 0.55f);
                Shape("Lantern warm glass", PrimitiveType.Cube, parent, position + Vector3.up * 3, new Vector3(0.42f, 0.65f, 0.42f), new Color(1, 0.85f, 0.5f), false, 0.05f, 0.75f, 0.4f);
                Shape("Lantern canopy", PrimitiveType.Cube, parent, position + Vector3.up * 3.38f, new Vector3(0.68f, 0.12f, 0.68f), ink, false, 0.45f, 0.55f);
            }

            private void Planter(Transform parent, Vector3 position, float radius, Color flowers)
            {
                if (headless) return;
                Disc("Garden stone planter", parent, position + Vector3.up * 0.3f, radius, 0.3f, cream, false);
                Disc("Garden rich soil", parent, position + Vector3.up * 0.32f, radius * 0.87f, 0.035f, trunk, false);
                for (int plant = 0; plant < 5; plant++)
                {
                    float a = plant * Mathf.PI * 2 / 5;
                    Vector3 stem = position + new Vector3(Mathf.Cos(a) * radius * 0.58f, 0.6f, Mathf.Sin(a) * radius * 0.58f);
                    Shape("Garden foliage", PrimitiveType.Sphere, parent, stem, new Vector3(radius * 0.52f, 0.75f, radius * 0.52f), grass * 0.83f, false);
                    Shape("Garden flower cluster", PrimitiveType.Sphere, parent, stem + Vector3.up * 0.38f, Vector3.one * radius * 0.4f, Color.Lerp(flowers, cream, plant % 2 * 0.25f), false);
                }
            }

            private GameObject Torus(string name, Transform parent, Vector3 center, float radius, float tube, Color tint, Vector3 rotation)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = center;
                obj.transform.localRotation = Quaternion.Euler(rotation);
                if (headless) return obj;
                const int segments = 48, sides = 6;
                var vertices = new Vector3[(segments + 1) * (sides + 1)];
                var normals = new Vector3[vertices.Length];
                var uv = new Vector2[vertices.Length];
                var indices = new int[segments * sides * 6];
                for (int segment = 0; segment <= segments; segment++)
                    for (int side = 0; side <= sides; side++)
                    {
                        float a = segment * Mathf.PI * 2 / segments, b = side * Mathf.PI * 2 / sides;
                        int v = segment * (sides + 1) + side;
                        Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        normals[v] = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        vertices[v] = radial * radius + normals[v] * tube;
                        uv[v] = new Vector2((float)segment / segments, (float)side / sides);
                        if (segment == segments || side == sides) continue;
                        int t = (segment * sides + side) * 6;
                        indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + sides + 1;
                        indices[t + 3] = v + 1; indices[t + 4] = v + sides + 2; indices[t + 5] = v + sides + 1;
                    }
                var mesh = new Mesh { name = "Lumen / Continuous architectural ring", vertices = vertices, normals = normals, uv = uv, triangles = indices };
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterial = LumenMaterials.Get("brass-ring-" + ColorUtility.ToHtmlStringRGB(tint), tint, 0.55f, 0.72f);
                return obj;
            }
        }
    }
}
