#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lumen.Runtime;
using Lumen.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumen.Editor
{
    /// <summary>Checks navigation geometry against the real academy, including translated and letterboxed plots.</summary>
    public static class MapValidation
    {
        private static int checks;

        [MenuItem("Numerion/Validate Map Geometry")]
        public static void Run()
        {
            WorldLayout layout = null;
            try { layout = AcademyWorld.Build(true); Validate(layout); }
            finally { if (layout?.Root) UnityEngine.Object.DestroyImmediate(layout.Root); }
        }

        public static void Validate(WorldLayout layout)
        {
            if (layout == null || !layout.Root) throw new ArgumentNullException(nameof(layout));
            checks = 0;
            Require(layout.Root.GetComponentsInChildren<Renderer>(true).Length == 0, "Geometry verification must not allocate rendered scenery.");
            Require(AcademyMapGeometry.Extent >= Mathf.Max(layout.PlayableBounds.extents.x, layout.PlayableBounds.extents.z) + 4,
                "Overview must include the full playable bounds and a visible outer margin.");
            Require(layout.Stations.Select(s => s.Id).Distinct().Count() == layout.Stations.Count, "Actual station identifiers must remain unique.");

            CheckIsland(layout, Vector3.zero, AcademyMapGeometry.CommonsRadius, "Meadow");
            foreach (var center in AcademyWorld.SectionPositions) CheckIsland(layout, center, AcademyMapGeometry.SectionRadius, "Meadow");
            foreach (var center in AcademyWorld.ExpeditionCenters) CheckIsland(layout, center, AcademyMapGeometry.ExpeditionRadius, "Expedition walkable limestone island");
            CheckBridgeFootprints(layout);

            var points = new List<Vector3> {layout.SpawnPosition};
            points.AddRange(layout.Stations.Select(s => s.transform.position));
            points.AddRange(layout.Collectibles.Select(c => c.transform.position));
            points.AddRange(layout.ParkourPlatforms);
            points.AddRange(AcademyWorld.ExpeditionAnchors);
            // Tablet/Chromebook-shaped panels and a translated Rect catch both aspect stretching and click-origin errors.
            Rect[] plots = {
                new Rect(-464, -190, 928, 380), new Rect(-336, -340, 672, 680),
                new Rect(93, -240, 620, 420), new Rect(-470, -40, 940, 330)
            };
            foreach (var plot in plots)
            {
                CheckProjectionAxes(plot);
                foreach (var point in points)
                {
                    var projected = AcademyMapGeometry.Project(point, plot);
                    Require(AcademyMapGeometry.TryUnproject(projected, plot, out var restored), "A real world landmark falls outside the clickable plot: " + point);
                    Require(FlatDistance(point, restored) < .0005f, "Landmark click does not return its actual world X/Z: " + point);
                }
                CheckIslandRim(plot, Vector3.zero, AcademyMapGeometry.CommonsRadius);
                foreach (var center in AcademyWorld.SectionPositions) CheckIslandRim(plot, center, AcademyMapGeometry.SectionRadius);
                foreach (var center in AcademyWorld.ExpeditionCenters) CheckIslandRim(plot, center, AcademyMapGeometry.ExpeditionRadius);
            }
            Require(!AcademyMapGeometry.TryUnproject(Vector2.zero, new Rect(0, 0, 0, 100), out _), "Collapsed maps must reject clicks.");
            Require(!AcademyMapGeometry.TryUnproject(new Vector2(float.NaN, 0), plots[0], out _), "Non-finite click coordinates must be rejected.");
            Require(!AcademyMapGeometry.TryUnproject(new Vector2(0, float.PositiveInfinity), plots[0], out _), "Infinite click coordinates must be rejected.");
            CheckActualGraphicInput(layout);
            Debug.Log("NUMERION_MAP_GEOMETRY_PASSED: " + checks + " checks; actual ten islands, six bridge footprints, " + points.Count
                + " spawn/station/collectible/course/arrival coordinates across four plot shapes; north +Z, uniform aspect, offset click round-trips and letterbox rejection.");
        }

        private static void CheckProjectionAxes(Rect plot)
        {
            Rect square = AcademyMapGeometry.SquarePlot(plot, 0);
            Require((square.center - plot.center).sqrMagnitude < .0001f, "Square plot shifted away from its supplied Rect center.");
            Require(Mathf.Abs(square.width - square.height) < .0001f, "Plot stretches world circles into ellipses.");
            Require(Mathf.Abs(square.width - Mathf.Min(plot.width, plot.height)) < .0001f, "Plot must fit the available short dimension.");
            Rect inset = AcademyMapGeometry.SquarePlot(plot, 18);
            Require(Mathf.Abs(inset.width - Mathf.Max(0, Mathf.Min(plot.width, plot.height) - 36)) < .0001f
                && (inset.center - plot.center).sqrMagnitude < .0001f, "Visual plot padding must not change the coordinate origin.");
            Vector2 origin = AcademyMapGeometry.Project(Vector3.zero, plot);
            Require((origin - plot.center).sqrMagnitude < .0001f, "World origin does not map to the actual panel center.");
            var north = AcademyMapGeometry.Project(new Vector3(0, 0, 10), plot) - origin;
            var east = AcademyMapGeometry.Project(new Vector3(10, 0, 0), plot) - origin;
            var south = AcademyMapGeometry.Project(new Vector3(0, 0, -10), plot) - origin;
            var west = AcademyMapGeometry.Project(new Vector3(-10, 0, 0), plot) - origin;
            Require(north.y > 0 && Mathf.Abs(north.x) < .0001f && south.y < 0, "North must mean world +Z and screen-up.");
            Require(east.x > 0 && Mathf.Abs(east.y) < .0001f && west.x < 0, "East must mean world +X and screen-right.");
            Require(Mathf.Abs(east.magnitude - north.magnitude) < .0001f, "Map axes do not share one world-to-pixel scale.");
            Require((AcademyMapGeometry.Project(new Vector3(0, 80, 0), plot) - origin).sqrMagnitude < .0001f, "Height must not displace top-down markers.");
            Require(AcademyMapGeometry.TryUnproject(plot.center, plot, out var center) && FlatDistance(center, Vector3.zero) < .0001f,
                "Clicking an offset panel center must select world origin.");
            Vector2 outside = plot.width > plot.height ? new Vector2(square.xMin - 1, square.center.y) : new Vector2(square.center.x, square.yMin - 1);
            Require(!AcademyMapGeometry.TryUnproject(outside, plot, out _), "Letterbox/outside pixels must not produce a false world waypoint.");
        }

        private static void CheckIsland(WorldLayout layout, Vector3 center, float radius, string name)
        {
            var floor = layout.Root.GetComponentsInChildren<MeshCollider>().FirstOrDefault(c => c.name == name && FlatDistance(c.transform.position, center) < .001f);
            Require(floor != null && floor.sharedMesh != null, "A plotted island has no matching walkable ground: " + center);
            Vector3 size = floor.sharedMesh.bounds.size;
            Require(Mathf.Abs(size.x * .5f - radius) < .001f && Mathf.Abs(size.z * .5f - radius) < .001f,
                "Plotted island radius differs from its physical footprint at " + center);
        }

        private static void CheckIslandRim(Rect plot, Vector3 center, float radius)
        {
            Rect square = AcademyMapGeometry.SquarePlot(plot, 0);
            foreach (Vector3 direction in new[] {Vector3.forward, Vector3.back, Vector3.right, Vector3.left})
                Require(square.Contains(AcademyMapGeometry.Project(center + direction * radius, plot)), "An actual island rim is clipped out of the map.");
        }

        private static void CheckBridgeFootprints(WorldLayout layout)
        {
            for (int i = 0; i < AcademyWorld.SectionPositions.Length; i++)
            {
                var bridge = layout.Root.transform.Find("Learning bridge " + i + "/Walkable stone causeway");
                Require(bridge != null && bridge.GetComponent<BoxCollider>() != null, "A plotted bridge has no actual stone deck.");
                AcademyMapGeometry.BridgeEndpoints(AcademyWorld.SectionPositions[i], out var start, out var end);
                Vector3 actualStart = bridge.TransformPoint(Vector3.back * .5f), actualEnd = bridge.TransformPoint(Vector3.forward * .5f);
                Require(FlatDistance(start, actualStart) < .001f && FlatDistance(end, actualEnd) < .001f,
                    "Bridge endpoints differ from the real causeway footprint for section " + i);
                Require(Mathf.Abs(bridge.TransformVector(Vector3.right).magnitude - AcademyMapGeometry.BridgeWidth) < .001f,
                    "Plotted bridge width differs from its walkable deck.");
            }
        }

        private static void CheckActualGraphicInput(WorldLayout layout)
        {
            var root = new GameObject("Map pointer and mesh regression", typeof(RectTransform), typeof(Canvas));
            Mesh mesh = null;
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var holder = new GameObject("Translated scaled map", typeof(RectTransform), typeof(LumenMapMarker));
                holder.transform.SetParent(root.transform, false);
                var marker = holder.GetComponent<LumenMapMarker>(); var rect = marker.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.2f, .8f);
                rect.sizeDelta = new Vector2(620, 420); rect.anchoredPosition = new Vector2(137, -83);
                rect.localScale = new Vector3(1.25f, .85f, 1);
                Rect plot = AcademyMapGeometry.SquarePlot(rect.rect, 18);
                Vector3 requested = AcademyWorld.ExpeditionAnchors[0], selected = Vector3.zero; int selections = 0;
                marker.Selected = point => { selected = point; selections++; };
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(AcademyMapGeometry.Project(requested, plot)));
                marker.OnPointerClick(new PointerEventData(null) {position = screen, button = PointerEventData.InputButton.Left});
                Require(selections == 1 && FlatDistance(selected, requested) < .001f,
                    "Actual map click handler loses its waypoint through translated/scaled or non-centered-pivot transforms.");
                marker.OnPointerClick(new PointerEventData(null) {position = screen, button = PointerEventData.InputButton.Right});
                Require(selections == 1, "Right-click must not replace a navigation waypoint.");
                Vector2 blank = new Vector2(plot.xMin - 3, plot.center.y);
                screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(blank));
                marker.OnPointerClick(new PointerEventData(null) {position = screen, button = PointerEventData.InputButton.Left});
                Require(selections == 1, "The actual click handler selects points in the square plot's letterbox.");

                mesh = new Mesh();
                marker.SetState(Vector3.zero, 90, 0, null);
                Populate(marker, mesh);
                var white = mesh.colors32.Select((color, index) => new {color, index}).Where(c => c.color.r == 255 && c.color.g == 255 && c.color.b == 255 && c.color.a == 255)
                    .Select(c => (Vector2)mesh.vertices[c.index]).ToArray();
                Vector2 origin = AcademyMapGeometry.Project(Vector3.zero, plot);
                Require(white.Length == 4 && Mathf.Abs(white.Max(p => p.x) - origin.x - 8) < .001f,
                    "A world heading of +90 degrees must turn the player arrow east/right, not west.");
                Require(white.All(p => Mathf.Abs(p.y - origin.y) <= 5.01f), "East-facing player arrow incorrectly points north/south.");

                var terrain = new GameObject("Actual map terrain mesh", typeof(RectTransform), typeof(LumenMapTerrain)).GetComponent<LumenMapTerrain>();
                terrain.transform.SetParent(root.transform, false); terrain.rectTransform.sizeDelta = new Vector2(620, 420); terrain.Initialize(layout);
                Populate(terrain, mesh);
                Require(mesh.vertexCount > 100 && mesh.vertexCount < 20000, "Map terrain must produce a bounded, nonempty chart mesh.");
                foreach (Vector3 vertex in mesh.vertices)
                    Require(!float.IsNaN(vertex.x) && !float.IsNaN(vertex.y) && !float.IsInfinity(vertex.x) && !float.IsInfinity(vertex.y)
                        && terrain.rectTransform.rect.Contains(vertex), "Map terrain emits non-finite or out-of-frame vertices.");
                Require(!terrain.raycastTarget, "Static chart terrain must not intercept live map clicks.");
            }
            finally { if (mesh) UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Populate(Graphic graphic, Mesh mesh)
        {
            mesh.Clear();
            using (var helper = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] {typeof(VertexHelper)}, null).Invoke(graphic, new object[] {helper});
                helper.FillMesh(mesh);
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        private static void Require(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException("Numerion map geometry: " + message); }
    }
}
#endif
