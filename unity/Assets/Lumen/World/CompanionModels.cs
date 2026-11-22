using UnityEngine;

namespace Lumen.World
{
    /// <summary>Ten original, code-native companion silhouettes shared by desktop and WebGL.</summary>
    public static class CompanionModels
    {
        private static Mesh crystalMesh;
        private static readonly Color Ink = new Color(.10f, .16f, .25f);
        private static readonly Color Cream = new Color(1f, .96f, .83f);
        private static readonly Color Gold = new Color(1f, .77f, .25f);
        private static readonly Color Cyan = new Color(.22f, .93f, .98f);
        private static readonly string[] Names = { "", "Iskra", "Volt", "Astra", "Zlomko", "Percenta", "Rovnik", "Geora", "Prizma", "Datax", "Ratio" };

        public static Transform Build(Transform parent, int style, Color accent, int gear = 0)
        {
            if (style <= 0) return null;
            style = Mathf.Clamp(style, 1, 10);
            var root = new GameObject("Companion / " + Names[style]).transform;
            root.SetParent(parent, false);
            var motion = root.gameObject.AddComponent<CompanionModelMotion>();
            switch (style)
            {
                case 1: Fox(root, motion); break;
                case 2: Robot(root, motion); break;
                case 3: Owl(root, motion); break;
                case 4: Slime(root, motion); break;
                case 5: Dragon(root, motion); break;
                case 6: Guardian(root, motion); break;
                case 7: Turtle(root, motion); break;
                case 8: Manta(root, motion); break;
                case 9: Beetle(root, motion); break;
                case 10: Rabbit(root, motion); break;
            }
            Gear(root, style, accent, Mathf.Clamp(gear, 0, 2));
            return root;
        }

        private static void Fox(Transform r, CompanionModelMotion motion)
        {
            Color fur = new Color(.96f, .49f, .24f), shadow = new Color(.67f, .30f, .20f);
            P("Fox oval body", PrimitiveType.Sphere, r, 0, -.02f, -.04f, .56f, .52f, .74f, fur);
            P("Fox cream bib", PrimitiveType.Sphere, r, 0, -.04f, .25f, .38f, .39f, .15f, Cream);
            P("Fox head", PrimitiveType.Sphere, r, 0, .22f, .27f, .64f, .48f, .49f, fur);
            P("Fox muzzle", PrimitiveType.Sphere, r, 0, .10f, .48f, .38f, .20f, .22f, Cream);
            P("Fox nose", PrimitiveType.Sphere, r, 0, .16f, .60f, .08f, .07f, .07f, Ink);
            Eyes(r, .14f, .28f, .505f, .080f, Ink);
            for (int s = -1; s <= 1; s += 2)
            {
                var ear = D("Fox pointed ear", r, s * .23f, .52f, .23f, .21f, .38f, .16f, fur);
                ear.localRotation = Quaternion.Euler(0, 0, -s * 12);
                D("Fox ear lining", ear, 0, .03f, .33f, .58f, .66f, .16f, Cream);
                motion.Sway(ear, Vector3.forward, 4, 1.3f, s);
                for (int fore = -1; fore <= 1; fore += 2)
                    P("Fox dark paw", PrimitiveType.Capsule, r, s * .18f, -.30f, fore * .21f, .15f, .18f, .18f, shadow);
            }
            var tail = Pivot("Fox tail pivot", r, 0, .0f, -.34f);
            P("Fox fluffy tail", PrimitiveType.Sphere, tail, 0, .10f, -.25f, .31f, .36f, .65f, fur);
            P("Fox cream tail tip", PrimitiveType.Sphere, tail, 0, .16f, -.51f, .25f, .27f, .26f, Cream);
            motion.Sway(tail, Vector3.up, 18, 1.7f, 0);
            P("Explorer pouch", PrimitiveType.Cube, r, .29f, -.03f, -.10f, .12f, .26f, .28f, new Color(.19f, .51f, .48f));
            Plus(r, .355f, -.02f, -.09f, .12f, Gold, true);
        }

        private static void Robot(Transform r, CompanionModelMotion motion)
        {
            Color metal = new Color(.51f, .75f, .84f), trim = new Color(.23f, .39f, .49f);
            P("Robot torso", PrimitiveType.Cube, r, 0, -.05f, 0, .53f, .45f, .48f, metal, .65f);
            P("Robot head", PrimitiveType.Cube, r, 0, .28f, .15f, .67f, .41f, .43f, metal, .65f);
            P("Robot visor", PrimitiveType.Cube, r, 0, .29f, .373f, .55f, .25f, .035f, Ink, .45f);
            Eyes(r, .15f, .30f, .398f, .09f, Cyan, 1.3f);
            Equals(r, 0, -.05f, .254f, .19f, Cyan);
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = Pivot("Robot arm pivot", r, s * .34f, .04f, .02f);
                P("Robot upper arm", PrimitiveType.Capsule, arm, 0, -.10f, 0, .13f, .17f, .13f, trim, .65f);
                P("Robot round hand", PrimitiveType.Sphere, arm, 0, -.24f, .03f, .17f, .18f, .17f, metal, .55f);
                motion.Sway(arm, Vector3.forward, 8, 1.6f, s * 1.8f);
                P("Robot track foot", PrimitiveType.Cube, r, s * .17f, -.36f, .02f, .23f, .18f, .40f, trim, .45f);
                P("Robot copper wheel", PrimitiveType.Cylinder, r, s * .25f, -.35f, .05f, .19f, .045f, .19f, Gold, .7f).localRotation = Quaternion.Euler(0, 0, 90);
            }
            P("Robot antenna", PrimitiveType.Cylinder, r, 0, .58f, .13f, .045f, .11f, .045f, trim, .7f);
            P("Robot signal orb", PrimitiveType.Sphere, r, 0, .72f, .13f, .14f, .14f, .14f, Cyan, .35f, 1.5f);
            P("Robot battery", PrimitiveType.Cylinder, r, 0, -.03f, -.30f, .28f, .24f, .22f, trim, .65f).localRotation = Quaternion.Euler(90, 0, 0);
        }

        private static void Owl(Transform r, CompanionModelMotion motion)
        {
            Color plum = new Color(.58f, .42f, .76f), dark = new Color(.35f, .25f, .56f);
            P("Owl round body", PrimitiveType.Sphere, r, 0, -.01f, 0, .66f, .75f, .59f, plum);
            P("Owl pale bib", PrimitiveType.Sphere, r, 0, -.09f, .27f, .40f, .43f, .10f, Cream);
            for (int s = -1; s <= 1; s += 2)
            {
                P("Owl facial disc", PrimitiveType.Sphere, r, s * .16f, .23f, .26f, .31f, .35f, .14f, Cream);
                P("Owl pupil", PrimitiveType.Sphere, r, s * .16f, .25f, .337f, .13f, .16f, .045f, Ink);
                P("Owl eye glint", PrimitiveType.Sphere, r, s * .16f - .019f, .29f, .36f, .035f, .038f, .015f, Color.white);
                D("Owl ear plume", r, s * .22f, .48f, -.02f, .16f, .32f, .15f, dark).localRotation = Quaternion.Euler(0, 0, -s * 18);
                var wing = Pivot("Owl wing pivot", r, s * .29f, .13f, -.05f);
                P("Owl wing", PrimitiveType.Sphere, wing, s * .045f, -.17f, -.02f, .18f, .51f, .40f, dark);
                for (int feather = 0; feather < 3; feather++)
                    P("Owl feather", PrimitiveType.Sphere, wing, s * .07f, -.31f, -.14f + feather * .12f, .13f, .23f, .12f, plum);
                motion.Sway(wing, Vector3.forward, 12, 1.8f, s);
                P("Owl foot", PrimitiveType.Sphere, r, s * .15f, -.39f, .10f, .20f, .09f, .24f, Gold);
            }
            D("Owl beak", r, 0, .11f, .37f, .14f, .17f, .16f, Gold).localRotation = Quaternion.Euler(35, 0, 0);
            D("Owl star medallion", r, 0, -.09f, .335f, .11f, .15f, .04f, Gold);
        }

        private static void Slime(Transform r, CompanionModelMotion motion)
        {
            Color mint = new Color(.25f, .84f, .65f), pale = new Color(.62f, 1f, .81f);
            P("Slime rounded body", PrimitiveType.Sphere, r, 0, -.10f, 0, .78f, .60f, .72f, mint, .10f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2 / 5;
                P("Slime puddle lobe", PrimitiveType.Sphere, r, Mathf.Cos(a) * .27f, -.34f, Mathf.Sin(a) * .25f, .31f, .13f, .29f, mint, .10f);
            }
            Eyes(r, .16f, -.02f, .326f, .09f, Ink);
            P("Slime smile", PrimitiveType.Capsule, r, 0, -.14f, .361f, .12f, .025f, .025f, Ink).localRotation = Quaternion.Euler(0, 0, 90);
            P("Fraction bar", PrimitiveType.Cube, r, -.23f, .17f, .22f, .22f, .025f, .035f, Cream);
            P("Fraction numerator", PrimitiveType.Sphere, r, -.23f, .24f, .22f, .055f, .055f, .04f, Cream);
            for (int s = -1; s <= 1; s += 2) P("Fraction denominator", PrimitiveType.Sphere, r, -.23f + s * .05f, .10f, .22f, .047f, .047f, .04f, Cream);
            var gem = Pivot("Slime crystal pivot", r, .06f, .27f, -.07f);
            D("Slime upper crystal", gem, 0, .11f, 0, .20f, .31f, .19f, pale);
            D("Slime satellite crystal", gem, .16f, 0, -.06f, .11f, .18f, .11f, Gold);
            motion.Sway(gem, Vector3.forward, 9, 1.5f, 0);
            P("Slime shiny spot", PrimitiveType.Sphere, r, .23f, .05f, .27f, .12f, .20f, .035f, pale, .05f);
        }

        private static void Dragon(Transform r, CompanionModelMotion motion)
        {
            Color coral = new Color(.91f, .34f, .48f), rose = new Color(.66f, .20f, .40f);
            P("Dragon body", PrimitiveType.Sphere, r, 0, -.04f, -.08f, .59f, .55f, .73f, coral);
            P("Dragon neck", PrimitiveType.Capsule, r, 0, .16f, .21f, .30f, .25f, .34f, coral);
            P("Dragon head", PrimitiveType.Sphere, r, 0, .35f, .30f, .51f, .39f, .48f, coral);
            P("Dragon snout", PrimitiveType.Sphere, r, 0, .25f, .54f, .36f, .21f, .28f, Cream);
            Eyes(r, .145f, .39f, .50f, .075f, Ink);
            for (int s = -1; s <= 1; s += 2)
            {
                D("Dragon horn", r, s * .17f, .60f, .17f, .11f, .29f, .12f, Gold).localRotation = Quaternion.Euler(-15, 0, -s * 12);
                var wing = Pivot("Dragon wing pivot", r, s * .22f, .15f, -.14f);
                D("Dragon wing membrane", wing, s * .19f, .10f, -.02f, .50f, .39f, .11f, rose).localRotation = Quaternion.Euler(0, -s * 18, s * 30);
                P("Dragon wing leading edge", PrimitiveType.Capsule, wing, s * .15f, .21f, -.02f, .06f, .23f, .06f, Gold).localRotation = Quaternion.Euler(0, 0, -s * 40);
                motion.Sway(wing, Vector3.forward, 11, 2.3f, s);
                for (int f = -1; f <= 1; f += 2) P("Dragon paw", PrimitiveType.Sphere, r, s * .22f, -.31f, f * .22f, .20f, .16f, .25f, rose);
            }
            var tail = Pivot("Dragon tail pivot", r, 0, -.05f, -.33f);
            for (int i = 0; i < 3; i++) P("Dragon tail link", PrimitiveType.Sphere, tail, i * .07f, .01f, -.10f - i * .17f, .23f - i * .045f, .21f - i * .04f, .29f, coral);
            D("Dragon tail gem", tail, .20f, .03f, -.60f, .18f, .21f, .23f, Gold);
            motion.Sway(tail, Vector3.up, 15, 1.4f, 0);
            Percent(r, .29f, -.05f, .18f, .17f, Gold);
        }

        private static void Guardian(Transform r, CompanionModelMotion motion)
        {
            Color teal = new Color(.24f, .57f, .57f), sand = new Color(.79f, .67f, .46f);
            P("Guardian broad body", PrimitiveType.Cube, r, 0, -.02f, -.08f, .69f, .54f, .67f, teal);
            P("Guardian head", PrimitiveType.Sphere, r, 0, .20f, .31f, .65f, .47f, .46f, sand);
            P("Guardian muzzle", PrimitiveType.Sphere, r, 0, .08f, .53f, .46f, .25f, .23f, Cream);
            Eyes(r, .17f, .27f, .50f, .08f, Ink);
            for (int s = -1; s <= 1; s += 2)
            {
                var horn = Pivot("Guardian horn pivot", r, s * .26f, .42f, .24f);
                D("Guardian swept horn", horn, s * .07f, .05f, -.04f, .18f, .39f, .17f, Cream).localRotation = Quaternion.Euler(-23, 0, -s * 32);
                motion.Sway(horn, Vector3.forward, 2, 1.1f, s);
                for (int f = -1; f <= 1; f += 2) P("Guardian hoof", PrimitiveType.Cube, r, s * .23f, -.34f, f * .20f, .21f, .19f, .23f, Ink);
            }
            Equals(r, 0, -.04f, .272f, .24f, Gold);
            var balance = Pivot("Guardian balanced scales", r, 0, .43f, -.18f);
            P("Balance upright", PrimitiveType.Cylinder, balance, 0, .11f, 0, .035f, .16f, .035f, Gold, .55f);
            P("Balance beam", PrimitiveType.Cube, balance, 0, .27f, 0, .54f, .035f, .04f, Gold, .55f);
            for (int s = -1; s <= 1; s += 2)
            {
                P("Scale suspension", PrimitiveType.Cylinder, balance, s * .22f, .17f, 0, .018f, .10f, .018f, Gold, .5f);
                P("Scale pan", PrimitiveType.Cylinder, balance, s * .22f, .07f, 0, .19f, .025f, .19f, Cream, .2f);
            }
            motion.Sway(balance, Vector3.forward, 4, 1.4f, 0);
        }

        private static void Turtle(Transform r, CompanionModelMotion motion)
        {
            Color green = new Color(.34f, .64f, .38f), emerald = new Color(.16f, .43f, .36f);
            P("Turtle low body", PrimitiveType.Sphere, r, 0, -.12f, 0, .81f, .35f, .83f, green);
            P("Turtle domed shell", PrimitiveType.Sphere, r, 0, .02f, -.06f, .76f, .54f, .78f, emerald, .10f);
            D("Turtle centre shell plate", r, 0, .29f, -.06f, .28f, .11f, .32f, new Color(.53f, .82f, .51f));
            for (int s = -1; s <= 1; s += 2)
            {
                D("Turtle geometric plate", r, s * .23f, .19f, -.08f, .22f, .13f, .29f, new Color(.42f, .72f, .44f));
                for (int f = -1; f <= 1; f += 2) P("Turtle broad foot", PrimitiveType.Sphere, r, s * .33f, -.28f, f * .25f, .25f, .14f, .28f, green);
            }
            var head = Pivot("Turtle head pivot", r, 0, -.01f, .43f);
            P("Turtle head", PrimitiveType.Sphere, head, 0, .03f, .05f, .39f, .31f, .42f, green);
            Eyes(head, .105f, .09f, .224f, .068f, Ink);
            motion.Sway(head, Vector3.up, 5, 1.1f, 0);
            P("Right triangle vertical", PrimitiveType.Cube, r, -.12f, .305f, .11f, .035f, .025f, .23f, Cream);
            P("Right triangle base", PrimitiveType.Cube, r, -.015f, .305f, .225f, .24f, .025f, .035f, Cream);
            P("Right triangle diagonal", PrimitiveType.Cube, r, -.015f, .31f, .11f, .035f, .025f, .32f, Gold).localRotation = Quaternion.Euler(0, -45, 0);
            D("Turtle pointed tail", r, 0, -.12f, -.53f, .11f, .11f, .31f, green).localRotation = Quaternion.Euler(0, 0, 0);
        }

        private static void Manta(Transform r, CompanionModelMotion motion)
        {
            Color ice = new Color(.53f, .77f, .97f), lilac = new Color(.67f, .54f, .91f);
            D("Manta faceted body", r, 0, .03f, .01f, .62f, .43f, .82f, ice);
            D("Manta back prism", r, 0, .26f, -.10f, .25f, .31f, .38f, lilac);
            for (int s = -1; s <= 1; s += 2)
            {
                var wing = Pivot("Manta wing pivot", r, s * .20f, .03f, -.05f);
                D("Manta swept wing", wing, s * .30f, .02f, -.10f, .73f, .14f, .62f, lilac).localRotation = Quaternion.Euler(0, -s * 23, 0);
                D("Manta luminous wingtip", wing, s * .60f, .03f, -.23f, .18f, .09f, .25f, Cyan);
                motion.Sway(wing, Vector3.forward, 12, 1.7f, s);
            }
            Eyes(r, .14f, .09f, .363f, .085f, Ink);
            var tail = Pivot("Manta tail pivot", r, 0, .0f, -.37f);
            P("Manta trailing tail", PrimitiveType.Capsule, tail, 0, -.02f, -.28f, .055f, .29f, .055f, ice).localRotation = Quaternion.Euler(90, 0, 0);
            D("Manta tail prism", tail, 0, .01f, -.59f, .12f, .19f, .17f, Gold);
            motion.Sway(tail, Vector3.up, 14, 1.3f, 0);
            P("Manta front volume glyph", PrimitiveType.Cube, r, 0, -.10f, .28f, .12f, .12f, .12f, Gold, .45f).localRotation = Quaternion.Euler(20, 30, 0);
        }

        private static void Beetle(Transform r, CompanionModelMotion motion)
        {
            Color shell = new Color(.18f, .30f, .48f), jade = new Color(.29f, .79f, .72f);
            P("Beetle body", PrimitiveType.Sphere, r, 0, -.04f, -.02f, .66f, .44f, .78f, shell, .65f);
            P("Beetle head", PrimitiveType.Sphere, r, 0, .05f, .39f, .48f, .34f, .38f, jade, .45f);
            Eyes(r, .14f, .11f, .556f, .09f, Cyan, 1.1f);
            for (int s = -1; s <= 1; s += 2)
            {
                P("Beetle wing casing", PrimitiveType.Sphere, r, s * .17f, .13f, -.10f, .30f, .30f, .63f, jade, .6f);
                var antenna = Pivot("Beetle antenna pivot", r, s * .13f, .24f, .40f);
                P("Beetle antenna stalk", PrimitiveType.Capsule, antenna, s * .05f, .14f, .04f, .035f, .18f, .035f, Gold, .65f).localRotation = Quaternion.Euler(0, 0, -s * 25);
                P("Beetle antenna data orb", PrimitiveType.Sphere, antenna, s * .11f, .29f, .04f, .10f, .10f, .10f, Cyan, .3f, .8f);
                motion.Sway(antenna, Vector3.up, 7, 1.9f, s);
                for (int leg = 0; leg < 3; leg++)
                {
                    float z = -.28f + leg * .23f;
                    P("Beetle six upper legs", PrimitiveType.Capsule, r, s * .32f, -.19f, z, .075f, .19f, .075f, Ink, .5f).localRotation = Quaternion.Euler(0, 0, s * 52);
                    P("Beetle six foot pads", PrimitiveType.Sphere, r, s * .45f, -.30f, z, .13f, .075f, .17f, Gold, .5f);
                }
            }
            for (int bar = 0; bar < 3; bar++)
                P("Beetle statistics bars", PrimitiveType.Cube, r, -.11f + bar * .11f, .25f + bar * .035f, -.14f, .06f, .08f + bar * .07f, .055f, Gold, .45f);
        }

        private static void Rabbit(Transform r, CompanionModelMotion motion)
        {
            Color blue = new Color(.43f, .75f, .83f), lining = new Color(.96f, .69f, .70f);
            P("Rabbit pear body", PrimitiveType.Sphere, r, 0, -.08f, -.08f, .58f, .59f, .58f, blue);
            P("Rabbit cream belly", PrimitiveType.Sphere, r, 0, -.11f, .18f, .36f, .40f, .11f, Cream);
            P("Rabbit head", PrimitiveType.Sphere, r, 0, .28f, .17f, .53f, .44f, .47f, blue);
            P("Rabbit muzzle", PrimitiveType.Sphere, r, 0, .17f, .39f, .31f, .18f, .15f, Cream);
            D("Rabbit nose", r, 0, .23f, .47f, .07f, .06f, .04f, lining);
            Eyes(r, .13f, .31f, .375f, .078f, Ink);
            for (int s = -1; s <= 1; s += 2)
            {
                var ear = Pivot("Rabbit long ear pivot", r, s * .14f, .47f, .07f);
                P("Rabbit tall ear", PrimitiveType.Capsule, ear, s * .035f, .23f, 0, .16f, .30f, .15f, blue).localRotation = Quaternion.Euler(0, 0, -s * 9);
                P("Rabbit inner ear", PrimitiveType.Capsule, ear, s * .035f, .23f, .068f, .09f, .22f, .025f, lining).localRotation = Quaternion.Euler(0, 0, -s * 9);
                motion.Sway(ear, Vector3.forward, 6, 1.4f, s);
                P("Rabbit hind paw", PrimitiveType.Sphere, r, s * .23f, -.33f, .02f, .28f, .18f, .44f, Cream);
                P("Rabbit front paw", PrimitiveType.Capsule, r, s * .22f, -.06f, .20f, .12f, .18f, .14f, blue).localRotation = Quaternion.Euler(-25, 0, -s * 10);
            }
            P("Rabbit cotton tail", PrimitiveType.Sphere, r, 0, -.04f, -.41f, .27f, .27f, .27f, Cream);
            for (int dot = -1; dot <= 1; dot += 2) P("Rabbit ratio dots", PrimitiveType.Sphere, r, 0, -.10f + dot * .055f, .246f, .05f, .05f, .025f, Gold);
            P("Rabbit cartographer pack", PrimitiveType.Cube, r, 0, .06f, -.34f, .40f, .32f, .17f, new Color(.32f, .42f, .54f));
            D("Rabbit map compass", r, 0, .10f, -.44f, .18f, .20f, .05f, Gold);
        }

        private static void Gear(Transform root, int style, Color accent, int gear)
        {
            float neck = style == 4 ? -.05f : style == 7 ? -.10f : .07f;
            if (gear == 1)
            {
                P("Companion expedition scarf", PrimitiveType.Cube, root, 0, neck, .22f, .59f, .10f, .41f, accent);
                P("Companion scarf pennant", PrimitiveType.Cube, root, .22f, neck - .15f, .40f, .13f, .30f, .055f, accent);
            }
            else if (gear == 2)
            {
                float height = style == 2 ? .53f : style == 4 ? .35f : style == 7 ? .36f : style == 8 ? .44f : style == 10 ? .56f : .50f;
                P("Companion earned crown", PrimitiveType.Cylinder, root, 0, height, .18f, .43f, .035f, .35f, Gold, .7f);
                for (int tooth = -1; tooth <= 1; tooth++)
                    D("Companion crown jewel", root, tooth * .14f, height + .10f, .28f, .08f, .20f, .07f, Gold);
            }
        }

        private static void Eyes(Transform parent, float spacing, float y, float z, float size, Color color, float glow = 0)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                P("Companion eye", PrimitiveType.Sphere, parent, s * spacing, y, z, size, size * 1.18f, size * .50f, color, 0, glow);
                P("Companion eye glint", PrimitiveType.Sphere, parent, s * spacing - size * .17f, y + size * .25f, z + size * .26f, size * .25f, size * .27f, size * .14f, Color.white);
            }
        }

        private static void Equals(Transform parent, float x, float y, float z, float width, Color color)
        {
            for (int s = -1; s <= 1; s += 2)
                P("Equation equals bar", PrimitiveType.Cube, parent, x, y + s * width * .19f, z, width, width * .14f, .025f, color, .2f, .15f);
        }

        private static void Plus(Transform parent, float x, float y, float z, float size, Color color, bool side)
        {
            P("Plus vertical", PrimitiveType.Cube, parent, x, y, z, side ? .025f : size * .17f, size, side ? size * .17f : .025f, color);
            P("Plus horizontal", PrimitiveType.Cube, parent, x, y, z, side ? .025f : size, size * .17f, side ? size : .025f, color);
        }

        private static void Percent(Transform parent, float x, float y, float z, float size, Color color)
        {
            P("Percent diagonal", PrimitiveType.Cube, parent, x, y, z, .025f, size * 1.6f, .035f, color).localRotation = Quaternion.Euler(0, 0, -32);
            P("Percent upper dot", PrimitiveType.Sphere, parent, x - size * .38f, y + size * .42f, z, size * .43f, size * .43f, .045f, color);
            P("Percent lower dot", PrimitiveType.Sphere, parent, x + size * .38f, y - size * .42f, z, size * .43f, size * .43f, .045f, color);
        }

        private static Transform Pivot(string name, Transform parent, float x, float y, float z)
        {
            var transform = new GameObject(name).transform; transform.SetParent(parent, false); transform.localPosition = new Vector3(x, y, z); return transform;
        }

        private static Transform P(string name, PrimitiveType type, Transform parent, float x, float y, float z, float sx, float sy, float sz, Color color, float metallic = 0, float glow = 0)
        {
            var part = AvatarFactory.Part(name, type, parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), color, metallic);
            if (glow > 0) part.GetComponent<MeshRenderer>().sharedMaterial = LumenMaterials.Get("companion-glow-" + ColorUtility.ToHtmlStringRGB(color) + "-" + glow, color, metallic, .5f, glow);
            return part;
        }

        private static Transform D(string name, Transform parent, float x, float y, float z, float sx, float sy, float sz, Color color)
        {
            if (!crystalMesh)
            {
                var points = new[] { Vector3.up * .5f, Vector3.down * .5f, Vector3.left * .5f, Vector3.right * .5f, Vector3.forward * .5f, Vector3.back * .5f };
                int[] faces = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
                var vertices = new Vector3[faces.Length]; var triangles = new int[faces.Length];
                for (int i = 0; i < faces.Length; i++) { vertices[i] = points[faces[i]]; triangles[i] = i; }
                crystalMesh = new Mesh { name = "Lumen original faceted companion crystal", vertices = vertices, triangles = triangles };
                crystalMesh.RecalculateNormals(); crystalMesh.RecalculateBounds();
            }
            var transform = Pivot(name, parent, x, y, z); transform.localScale = new Vector3(sx, sy, sz);
            transform.gameObject.AddComponent<MeshFilter>().sharedMesh = crystalMesh;
            transform.gameObject.AddComponent<MeshRenderer>().sharedMaterial = LumenMaterials.Get("companion-crystal-" + ColorUtility.ToHtmlStringRGB(color), color, .22f, .58f);
            return transform;
        }
    }

}
