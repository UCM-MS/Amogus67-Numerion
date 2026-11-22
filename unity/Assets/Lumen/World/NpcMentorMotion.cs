using UnityEngine;

namespace Lumen.World
{
    /// <summary>Stationary mentors reuse the same gentle idle animation as player characters.</summary>
    public sealed class NpcMentorMotion : MonoBehaviour
    {
        private AvatarVisual avatar;
        public void Initialize(AvatarVisual visual) { avatar = visual; }
        private void Update()
        {
            if (avatar && !WorldAnimation.Frozen) avatar.Tick(Time.deltaTime, false);
        }
    }
}
