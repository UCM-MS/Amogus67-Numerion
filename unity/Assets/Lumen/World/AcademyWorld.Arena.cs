using UnityEngine;

namespace Lumen.World
{
    public static partial class AcademyWorld
    {
        private sealed partial class Builder
        {
            private void PetArenaExhibit()
            {
                if (headless) return;
                var exhibit = new GameObject("Arena / Knowledge companions").transform;
                exhibit.SetParent(root, false);
                exhibit.localPosition = new Vector3(12, .12f, 4);
                Disc("Duel dais", exhibit, Vector3.zero, 3.8f, .14f, cream, false);
                Disc("Lavender inlay", exhibit, Vector3.up * .16f, 3.5f, .035f, new Color(.76f,.7f,.9f), false);
                for (int i = 0; i < 2; i++)
                {
                    var podium = new GameObject("Companion showcase").transform; podium.SetParent(exhibit, false);
                    podium.localPosition = new Vector3(i == 0 ? -1.55f : 1.55f, .3f, 0);
                    podium.localRotation = Quaternion.Euler(0, i == 0 ? 55 : -55, 0);
                    Disc("Companion podium", podium, Vector3.zero, 1.12f, .12f, cream, false);
                    var pet = CompanionModels.Build(podium, i == 0 ? 4 : 7, i == 0 ? new Color(.4f,.8f,.72f) : new Color(.7f,.55f,.9f));
                    if (pet) { pet.localPosition += Vector3.up * .17f; pet.localScale *= 1.5f; }
                    WorldLabels.Create(podium, i == 0 ? "ZLOMKO" : "GEORA", new Vector3(0, 1.9f, 0), .4f, stone);
                }
                WorldLabels.Create(exhibit, "ARÉNA VEDOMOSTÍ", new Vector3(0, 3.35f, 0), .72f, stone);
                WorldLabels.Create(exhibit, "VYRIEŠ  ·  ZVOĽ ŤAH  ·  OBJAV", new Vector3(0, .6f, 3), .3f, stone);
            }
        }
    }
}
