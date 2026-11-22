using Lumen.World;
using UnityEngine;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private RectTransform navigationHud;
        private LumenNavigationArrow navigationArrow;
        private Text navigationTitle, navigationStep;
        private Button navigationClear;

        private void BuildNavigationHud()
        {
            var surface = ImageNode("Selected destination guidance", hudRoot, SoftInk);
            surface.raycastTarget = false; navigationHud = surface.rectTransform;
            var arrow = new GameObject("Direction to next landmark", typeof(RectTransform), typeof(LumenNavigationArrow));
            arrow.transform.SetParent(navigationHud, false); navigationArrow = arrow.GetComponent<LumenNavigationArrow>();
            navigationArrow.color = Brass; navigationArrow.raycastTarget = false;
            navigationTitle = CompactLabel(navigationHud, "", 17, Chalk);
            navigationTitle.raycastTarget = false; navigationTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            navigationTitle.verticalOverflow = VerticalWrapMode.Truncate;
            navigationStep = CompactLabel(navigationHud, "", 14, Muted);
            navigationStep.raycastTarget = false; navigationStep.horizontalOverflow = HorizontalWrapMode.Wrap;
            navigationStep.verticalOverflow = VerticalWrapMode.Truncate;
            navigationClear = CompactButton(navigationHud, "×", ClearMapDestination, Lavender, 22);
            navigationHud.gameObject.SetActive(false);
        }

        private void RefreshNavigation()
        {
            if (!navigationHud || game == null) return;
            bool visible = mapDestination.HasValue && !titleOpen && !ModalOpen && !game.PhotoMode
                && !(game.VR != null && game.VR.OwnsCamera);
            if (!visible) { navigationHud.gameObject.SetActive(false); return; }
            Vector3 player = game.PlayerPosition, destination = mapDestination.Value;
            if (AcademyNavigation.Distance(player, destination) <= 2.1f && Mathf.Abs(player.y - destination.y) < 3f)
            {
                ClearMapDestination(); navigationHud.gameObject.SetActive(false); game.Notify("Dorazil si k vybranému cieľu."); return;
            }
            // Nearby interaction has priority over a navigation card in the same lower HUD area.
            if (game.Target != null) { navigationHud.gameObject.SetActive(false); return; }
            navigationHud.gameObject.SetActive(true);
            float width = Mathf.Clamp(UiViewportSize.x / interfaceScale - 32, 240, 380);
            float bottom = TouchInterface && UiViewportSize.y > UiViewportSize.x ? 164 : 80;
            Anchor(navigationHud, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, bottom), new Vector2(width, 78));
            Anchor(navigationArrow.rectTransform, new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(32, 0), new Vector2(48, 48));
            Anchor(navigationClear.GetComponent<RectTransform>(), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-8, 0), new Vector2(MinimumTargetSize, MinimumTargetSize));
            float textWidth = Mathf.Max(110, width - 72 - MinimumTargetSize);
            Anchor(navigationTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -10), new Vector2(textWidth, 22));
            Anchor(navigationStep.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -34), new Vector2(textWidth, 36));
            string title = mapDestinationName ?? "Vybraný cieľ";
            int maxTitle = Mathf.Max(10, Mathf.FloorToInt(textWidth / 8f));
            Put(navigationTitle, title.Length > maxTitle ? title.Substring(0, maxTitle - 1) + "…" : title);
            var step = AcademyNavigation.Next(game.World, player, destination);
            int metres = Mathf.CeilToInt(AcademyNavigation.Distance(player, step.Position));
            Put(navigationStep, step.Caption + (step.Portal && metres <= 3 ? "" : " · " + metres + " m k bodu"));
            Vector3 direction = step.Position - player; direction.y = 0;
            Vector3 forward = game.ViewCamera ? game.ViewCamera.transform.forward : Quaternion.Euler(0, game.PlayerYaw, 0) * Vector3.forward;
            forward.y = 0;
            float angle = direction.sqrMagnitude < .001f ? 0 : Vector3.SignedAngle(forward, direction, Vector3.up);
            navigationArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LumenNavigationArrow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect; float s = Mathf.Min(r.width, r.height) * .41f;
            Vector2 c = r.center;
            mesh.AddVert(c + new Vector2(0, s), color, Vector2.zero);
            mesh.AddVert(c + new Vector2(-s * .8f, -s * .65f), color, Vector2.zero);
            mesh.AddVert(c + new Vector2(0, -s * .24f), color, Vector2.zero);
            mesh.AddVert(c + new Vector2(s * .8f, -s * .65f), color, Vector2.zero);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 3);
        }
    }
}
