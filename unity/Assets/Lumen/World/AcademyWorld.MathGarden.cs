using System.Collections.Generic;
using UnityEngine;

namespace Lumen.World
{
    public static partial class AcademyWorld
    {
        private sealed partial class Builder
        {
            private static Mesh fractionSliceMesh, triangularPrismMesh;
            private readonly Color gardenMint = new Color(.43f, .55f, .49f);
            private readonly Color gardenPurple = new Color(.47f, .46f, .58f);
            private readonly Color gardenLime = new Color(.72f, .68f, .41f);

            private void MathDiscoveryGarden()
            {
                AcademyMentors();
                if (headless) return;
                var garden = MathGroup("Math discovery garden / first-party art", root, Vector3.zero);
                var staticArt = MathGroup("Static garden details", garden, Vector3.zero);
                var movingArt = MathGroup("Kinetic mathematical exhibits", garden, Vector3.zero);
                var labels = MathGroup("Readable mathematical ideas", garden, Vector3.zero);
                var motion = movingArt.gameObject.AddComponent<MathExhibitMotion>();
                for (int i = 0; i < SectionPositions.Length; i++)
                {
                    Vector3 position = SectionPositions[i] + new Vector3(0, 2.7f, -1.2f);
                    Transform fixedDisplay = MathGroup(SubjectNames[i], staticArt, position);
                    Transform movingDisplay = MathGroup(SubjectNames[i], movingArt, position);
                    Transform caption = MathGroup(SubjectNames[i], labels, position);
                    switch (i)
                    {
                        case 0: FractionWheel(fixedDisplay, movingDisplay, caption, motion); break;
                        case 1: PercentMarket(fixedDisplay, caption); break;
                        case 2: EquationBalance(fixedDisplay, movingDisplay, caption, motion); break;
                        case 3: GeometryWorkshop(fixedDisplay, movingDisplay, caption, motion); break;
                        case 4: DataSculpture(fixedDisplay, caption); break;
                        case 5: CartographyTable(fixedDisplay, caption); break;
                    }
                    // Small planting islands sit behind the plinth, outside station and bridge lanes.
                    for (int side = -1; side <= 1; side += 2)
                        MathSprout(staticArt, SectionPositions[i] + new Vector3(side * 5.7f, .14f, -4.3f), i + side);
                }
                LibraryConstellation(staticArt, movingArt, labels, motion);
                // Dynamic objects and billboard labels are intentionally outside this static batch.
                StaticBatchingUtility.Combine(staticArt.gameObject);
            }

            private void AcademyMentors()
            {
                AcademyMentor("npc-mira", "Mira", "SPRIEVODKYŇA AKADÉMIE", new Vector3(-4, .05f, 4), 24,
                    new AvatarAppearance { ColorIndex = 3, SkinIndex = 1, Hair = 2, Hat = 2, Backpack = 0 });
                AcademyMentor("npc-tobi", "Tobi", "MATEMATICKÝ MENTOR", new Vector3(-7, .05f, -4), 45,
                    new AvatarAppearance { ColorIndex = 4, SkinIndex = 3, Hair = 0, Hat = 0, Backpack = 0 });
                AcademyMentor("npc-ema", "Ema", "STRATÉGKA ARÉNY", new Vector3(5, .05f, 1), -35,
                    new AvatarAppearance { ColorIndex = 0, SkinIndex = 2, Hair = 1, Hat = 1, Backpack = 2 });
            }

            private void AcademyMentor(string id, string name, string role, Vector3 position, float yaw, AvatarAppearance appearance)
            {
                Transform holder;
                if (headless) holder = MathGroup("Academy mentor / " + name, root, position);
                else
                {
                    var avatar = AvatarFactory.Create(name, appearance);
                    holder = avatar.transform; holder.SetParent(root, false); holder.localPosition = position;
                    holder.localRotation = Quaternion.Euler(0, yaw, 0);
                    holder.gameObject.AddComponent<NpcMentorMotion>().Initialize(avatar);
                    WorldLabels.Create(holder, role, new Vector3(0, 2.7f, 0), .28f, ink);
                }
                var station = holder.gameObject.AddComponent<InteractiveStation>();
                station.Id = id; station.Subject = "math"; station.Mode = "npc";
                station.Prompt = "Porozprávať sa: " + name; station.InteractionRadius = 2.6f;
                layout.Stations.Add(station);
            }

            private void FractionWheel(Transform fixedArt, Transform moving, Transform caption, MathExhibitMotion motion)
            {
                MathPart("Fraction wheel stand", PrimitiveType.Cylinder, fixedArt, new Vector3(0, -.65f, -.1f), new Vector3(.15f, .85f, .15f), brass);
                var wheel = MathGroup("Eight equal portions", moving, new Vector3(0, .12f, 0));
                if (!fractionSliceMesh) fractionSliceMesh = MathExtrudedPolygon("One eighth / reusable sector", FractionOutline(), .16f);
                for (int i = 0; i < 8; i++)
                {
                    var slice = MathMesh("Equal fraction / " + i, wheel, Vector3.zero, Vector3.one * 1.55f, fractionSliceMesh, i < 3 ? gardenPurple : cream);
                    slice.localRotation = Quaternion.Euler(0, 0, i * 45);
                }
                MathPart("Fraction wheel pin", PrimitiveType.Sphere, wheel, new Vector3(0, 0, .2f), Vector3.one * .25f, brass);
                motion.Add(wheel, Vector3.forward, 5, .05f, .65f);
                WorldLabels.Create(caption, "3 / 8", new Vector3(0, 1.55f, 1.1f), 1.05f, ink);
                WorldLabels.Create(caption, "ROVNAKÉ ČASTI · JEDEN CELOK", new Vector3(0, -1.1f, 1.3f), .35f, ink);
            }

            private void PercentMarket(Transform art, Transform caption)
            {
                MathPart("Percentage market counter", PrimitiveType.Cube, art, new Vector3(0, -.65f, 0), new Vector3(3.8f, .25f, 1.7f), cream);
                for (int side = -1; side <= 1; side += 2)
                    MathPart("Market slender post", PrimitiveType.Cylinder, art, new Vector3(side * 1.65f, .1f, -.5f), new Vector3(.1f, 1.3f, .1f), brass);
                for (int stripe = 0; stripe < 3; stripe++)
                {
                    MathPart("Striped market canopy", PrimitiveType.Cube, art, new Vector3((stripe - 1) * 1.3f, 1.5f, -.2f), new Vector3(1.3f, .16f, 2), stripe == 1 ? cream : gardenMint);
                    for (int coin = 0; coin < stripe + 1; coin++)
                        MathPart("Percentage coin stack", PrimitiveType.Cylinder, art, new Vector3((stripe - 1) * 1.16f, -.45f + coin * .15f, 0), new Vector3(.7f, .065f, .7f), brass);
                    MathPart("Market discount card", PrimitiveType.Cube, art, new Vector3((stripe - 1) * 1.16f, .45f, .25f), new Vector3(.96f, .6f, .08f), stripe == 1 ? gardenPurple : gardenMint);
                    WorldLabels.Create(caption, stripe == 0 ? "10 %" : stripe == 1 ? "25 %" : "50 %", new Vector3((stripe - 1) * 1.16f, .45f, .32f), .45f, cream, false);
                }
                WorldLabels.Create(caption, "KOĽKO NAOZAJ UŠETRÍŠ?", new Vector3(0, 1.7f, 1.1f), .5f, ink);
            }

            private void EquationBalance(Transform fixedArt, Transform moving, Transform caption, MathExhibitMotion motion)
            {
                MathPart("Equation balance foot", PrimitiveType.Cylinder, fixedArt, new Vector3(0, -1.15f, 0), new Vector3(1.6f, .1f, 1), cream);
                MathPart("Equation balance stem", PrimitiveType.Cylinder, fixedArt, new Vector3(0, -.2f, 0), new Vector3(.17f, .9f, .17f), brass);
                var beam = MathGroup("Search for equilibrium", moving, new Vector3(0, .85f, 0));
                MathPart("Balance beam", PrimitiveType.Cube, beam, Vector3.zero, new Vector3(4, .14f, .18f), gardenPurple);
                for (int side = -1; side <= 1; side += 2)
                {
                    MathPart("Balance pan", PrimitiveType.Cylinder, beam, new Vector3(side * 1.55f, -.9f, 0), new Vector3(1.3f, .06f, 1), cream);
                    for (int stringSide = -1; stringSide <= 1; stringSide += 2)
                        MathPart("Balance suspension", PrimitiveType.Cylinder, beam, new Vector3(side * 1.55f, -.42f, stringSide * .4f), new Vector3(.035f, .44f, .035f), brass);
                    for (int token = 0; token < 2; token++)
                        MathPart("Unknown and known weights", PrimitiveType.Cube, beam, new Vector3(side * 1.55f + (token - .5f) * .45f, -.6f, 0), Vector3.one * .4f, side < 0 ? gardenMint : gardenLime);
                }
                motion.Add(beam, Vector3.forward, 4, 0, .8f);
                WorldLabels.Create(beam, "2x + 3", new Vector3(-1.55f, -.35f, .65f), .35f, ink);
                WorldLabels.Create(beam, "11", new Vector3(1.55f, -.35f, .65f), .35f, ink);
                WorldLabels.Create(caption, "2x + 3 = 11", new Vector3(0, 1.55f, 1.1f), .8f, ink);
                WorldLabels.Create(caption, "OBE STRANY V ROVNOVÁHE", new Vector3(0, -1.25f, 1.2f), .35f, ink);
            }

            private void GeometryWorkshop(Transform fixedArt, Transform moving, Transform caption, MathExhibitMotion motion)
            {
                if (!triangularPrismMesh) triangularPrismMesh = MathExtrudedPolygon("Reusable triangular prism", new[] { new Vector2(-.7f, -.55f), new Vector2(.7f, -.55f), new Vector2(0, .75f) }, .8f);
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * 1.5f;
                    MathPart("Geometry specimen pedestal", PrimitiveType.Cylinder, fixedArt, new Vector3(x, -1.1f, 0), new Vector3(1.15f, .16f, 1.15f), cream);
                    var specimen = MathGroup("Rotating solid / " + i, moving, new Vector3(x, .2f, 0));
                    if (i == 1) MathMesh("Triangular prism", specimen, Vector3.zero, Vector3.one * 1.15f, triangularPrismMesh, gardenMint);
                    else MathPart(i == 0 ? "Cube specimen" : "Cylinder specimen", i == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder, specimen, Vector3.zero, i == 0 ? Vector3.one * 1.05f : new Vector3(.95f, .62f, .95f), i == 0 ? gardenPurple : gardenLime);
                    motion.Add(specimen, Vector3.up, 0, .07f, .8f, i, 13 + i * 2);
                    WorldLabels.Create(caption, i == 0 ? "a³" : i == 1 ? "S · v" : "πr² · v", new Vector3(x, -1.3f, 1), .45f, ink);
                }
                WorldLabels.Create(caption, "OD ROVINY K OBJEMU", new Vector3(0, 1.55f, 1.1f), .65f, ink);
            }

            private void DataSculpture(Transform art, Transform caption)
            {
                MathPart("Chart horizontal axis", PrimitiveType.Cube, art, new Vector3(0, -1.15f, 0), new Vector3(4.3f, .08f, .1f), brass);
                MathPart("Chart vertical axis", PrimitiveType.Cube, art, new Vector3(-2.1f, .05f, 0), new Vector3(.08f, 2.5f, .1f), brass);
                var points = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    float height = new[] { .9f, 1.65f, 1.2f, 2.1f }[i];
                    float x = (i - 1.5f) * .92f;
                    MathPart("Data bar", PrimitiveType.Cube, art, new Vector3(x, -1.1f + height * .5f, 0), new Vector3(.57f, height, .65f), i % 2 == 0 ? gardenMint : gardenPurple);
                    points[i] = new Vector3(x, height - .7f, .55f);
                    MathPart("Observation point", PrimitiveType.Sphere, art, points[i], Vector3.one * .16f, gardenLime);
                    if (i > 0) MathLink("Observed trend", art, points[i - 1], points[i], .045f, brass);
                }
                WorldLabels.Create(caption, "DÁTA ROZPRÁVAJÚ PRÍBEH", new Vector3(0, 1.55f, 1.1f), .6f, ink);
                WorldLabels.Create(caption, "POROVNAJ · ODHADNI · OVER", new Vector3(0, -1.35f, 1.1f), .35f, ink);
            }

            private void CartographyTable(Transform art, Transform caption)
            {
                var map = MathGroup("Scale map / 1 to 1000", art, new Vector3(0, -.1f, 0));
                map.localRotation = Quaternion.Euler(24, 0, 0);
                MathPart("Map field", PrimitiveType.Cube, map, Vector3.zero, new Vector3(3.8f, 2.7f, .12f), gardenMint);
                for (int i = -1; i <= 1; i++)
                {
                    MathPart("Map meridian", PrimitiveType.Cube, map, new Vector3(i * .8f, 0, .08f), new Vector3(.025f, 2.5f, .025f), cream);
                    MathPart("Map parallel", PrimitiveType.Cube, map, new Vector3(0, i * .65f, .08f), new Vector3(3.6f, .025f, .025f), cream);
                }
                Vector3[] stops = { new Vector3(-1.15f, -.65f, .16f), new Vector3(0, .6f, .16f), new Vector3(1.25f, -.25f, .16f) };
                for (int i = 0; i < stops.Length; i++)
                {
                    MathPart("Map destination", PrimitiveType.Sphere, map, stops[i], Vector3.one * .2f, gardenPurple);
                    if (i > 0) MathLink("Measured journey", map, stops[i - 1], stops[i], .09f, gardenLime);
                }
                WorldLabels.Create(caption, "1 : 1 000", new Vector3(0, 1.55f, 1.1f), .95f, ink);
                WorldLabels.Create(caption, "MALÁ MAPA · VEĽKÁ VÝPRAVA", new Vector3(0, -1.4f, 1.2f), .35f, ink);
            }

            private void LibraryConstellation(Transform fixedArt, Transform movingArt, Transform labels, MathExhibitMotion motion)
            {
                var books = MathGroup("Library / floating discoveries", movingArt, new Vector3(-13.7f, 2.4f, -2.8f));
                for (int i = 0; i < 3; i++)
                {
                    var book = MathGroup("Floating mathematical volume", books, new Vector3((i - 1) * .95f, i == 1 ? .65f : .1f, i * .18f));
                    book.localRotation = Quaternion.Euler(-15, (i - 1) * 20, (i - 1) * -16);
                    MathPart("Volume pages", PrimitiveType.Cube, book, Vector3.zero, new Vector3(.66f, .16f, .87f), cream);
                    for (int side = -1; side <= 1; side += 2)
                        MathPart("Volume cover", PrimitiveType.Cube, book, new Vector3(0, side * .1f, 0), new Vector3(.73f, .035f, .94f), i == 1 ? gardenPurple : gardenMint);
                    motion.Add(book, Vector3.forward, 8, .12f, .7f, i * 1.5f);
                }
                Vector3 previous = Vector3.zero;
                for (int i = 0; i < 5; i++)
                {
                    Vector3 point = new Vector3(-15.2f + i * .7f, 4.7f + Mathf.Sin(i * 1.8f) * .45f, -3.6f);
                    MathPart("Library constellation light", PrimitiveType.Sphere, fixedArt, point, Vector3.one * .12f, gardenLime);
                    if (i > 0) MathLink("Connected idea", fixedArt, previous, point, .025f, brass);
                    previous = point;
                }
                for (int i = 0; i < 2; i++)
                {
                    Vector3 p = new Vector3(-15.4f + i * 3.2f, .1f, -5.1f);
                    MathPart("Library lantern stem", PrimitiveType.Cylinder, fixedArt, p + Vector3.up * .7f, new Vector3(.07f, .7f, .07f), brass);
                    MathPart("Library lantern globe", PrimitiveType.Sphere, fixedArt, p + Vector3.up * 1.5f, Vector3.one * .4f, new Color(.77f,.67f,.45f), .10f);
                }
                WorldLabels.Create(labels, "KAŽDÝ OBJAV SA ZAČÍNA OTÁZKOU", new Vector3(-13.7f, 1.35f, -2.1f), .35f, ink);
            }

            private void MathSprout(Transform parent, Vector3 position, int seed)
            {
                var sprout = MathGroup("Garden / mathematical meadow", parent, position);
                MathPart("Garden leaf", PrimitiveType.Sphere, sprout, new Vector3(-.17f, .32f, 0), new Vector3(.65f, .3f, .36f), gardenMint);
                MathPart("Garden leaf", PrimitiveType.Sphere, sprout, new Vector3(.16f, .5f, .07f), new Vector3(.55f, .26f, .32f), gardenMint);
                MathPart("Meadow flower", PrimitiveType.Sphere, sprout, new Vector3(.09f, .69f, .04f), Vector3.one * .21f, seed % 2 == 0 ? gardenPurple : gardenLime);
            }

            private Transform MathPart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color tint, float glow = 0)
                => Shape(name, type, parent, position, scale, tint, false, 0, .55f, glow).transform;

            private void MathLink(string name, Transform parent, Vector3 a, Vector3 b, float width, Color tint)
            {
                var link = MathPart(name, PrimitiveType.Cube, parent, (a + b) * .5f, new Vector3(width, width, Vector3.Distance(a, b)), tint);
                link.localRotation = Quaternion.LookRotation(b - a);
            }

            private static Transform MathGroup(string name, Transform parent, Vector3 position)
            {
                var group = new GameObject(name).transform;
                group.SetParent(parent, false); group.localPosition = position;
                return group;
            }

            private Transform MathMesh(string name, Transform parent, Vector3 position, Vector3 scale, Mesh mesh, Color tint)
            {
                var item = MathGroup(name, parent, position); item.localScale = scale;
                item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = LumenMaterials.Get("math-garden-" + ColorUtility.ToHtmlStringRGB(tint), tint);
                return item;
            }

            private static Vector2[] FractionOutline()
            {
                var points = new Vector2[8]; points[0] = Vector2.zero;
                for (int i = 0; i < 7; i++)
                {
                    float angle = (.6f + i * 7.3f) * Mathf.Deg2Rad;
                    points[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
                return points;
            }

            private static Mesh MathExtrudedPolygon(string name, Vector2[] outline, float depth)
            {
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                for (int side = 0; side < 2; side++)
                {
                    int start = vertices.Count;
                    for (int i = 0; i < outline.Length; i++) vertices.Add(new Vector3(outline[i].x, outline[i].y, (side == 0 ? 1 : -1) * depth * .5f));
                    for (int i = 1; i < outline.Length - 1; i++)
                    { triangles.Add(start); triangles.Add(start + (side == 0 ? i : i + 1)); triangles.Add(start + (side == 0 ? i + 1 : i)); }
                }
                for (int i = 0; i < outline.Length; i++)
                {
                    int next = (i + 1) % outline.Length, start = vertices.Count;
                    vertices.Add(new Vector3(outline[i].x, outline[i].y, depth * .5f)); vertices.Add(new Vector3(outline[i].x, outline[i].y, -depth * .5f));
                    vertices.Add(new Vector3(outline[next].x, outline[next].y, depth * .5f)); vertices.Add(new Vector3(outline[next].x, outline[next].y, -depth * .5f));
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
                }
                var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
