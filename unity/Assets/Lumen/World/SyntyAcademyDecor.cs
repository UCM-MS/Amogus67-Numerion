using UnityEngine;

namespace Lumen.World
{
    /// <summary>Optional licensed POLYGON visuals. All gameplay collision stays in AcademyWorld.</summary>
    public static class SyntyAcademyDecor
    {
        public static void Attach(Transform worldRoot, bool headless)
        {
            if (headless || !worldRoot) return;
            var prefab = Resources.Load<GameObject>("Generated/SyntyDecor");
            if (!prefab) return; // The educational game also opens without the separately licensed pack.
            var decor = Object.Instantiate(prefab, worldRoot, false);
            decor.name = "POLYGON / Academy collection";
        }
    }
}
