using System;
using System.Collections.Generic;
using Lumen.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private RectTransform mapWindow, mapFrame, mapKey, mapCompactKey;
        private LumenMapTerrain mapTerrain;
        private LumenMapMarker mapMarker;
        private readonly List<Text> mapPlaceLabels = new List<Text>();
        private readonly List<Vector3> mapPlacePositions = new List<Vector3>();
        private Text mapPosition, mapWaypoint, mapCompactWaypoint, mapCompactLegend;
        private Button mapClear, mapCompactClear;
        private Vector3? mapDestination;
        private string mapDestinationName;
        private Vector2 mapLastSize;

        public void ToggleMap()
        {
            if (ModalOpen && FeaturePanel == "MAP") CloseModal();
            else if (!ModalOpen) ShowMap();
        }

        public void ShowMap()
        {
            if (!game.Save.HasCreatedProfile) { ShowProfile(true); return; }
            Open("Mapa akadémie", "NUMERION / SEVER JE HORE"); FeaturePanel = "MAP";
            mapLastSize = Vector2.zero;
            mapPlaceLabels.Clear(); mapPlacePositions.Clear();
            mapWindow = new GameObject("Spatial map", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            mapWindow.SetParent(body, false);
            mapWindow.GetComponent<LayoutElement>().minHeight = 260;
            mapWindow.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(300, UiViewportSize.y / interfaceScale - 156);
            mapFrame = ImageNode("North-up map", mapWindow, new Color(.045f, .075f, .095f)).rectTransform;
            var terrainObject = new GameObject("Islands bridges and stations", typeof(RectTransform), typeof(LumenMapTerrain));
            terrainObject.transform.SetParent(mapFrame, false);
            mapTerrain = terrainObject.GetComponent<LumenMapTerrain>(); Stretch(mapTerrain.rectTransform, Vector2.zero, Vector2.one);
            mapTerrain.Initialize(game.World);
            AddMapPlace("NÁMESTIE", new Vector3(0, 0, -7));
            string[] sectionNames = { "1  Čísla a\nzlomky", "2  Percentá\na pomer", "3  Výrazy a\nrovnice", "4  Geometria", "5  Dáta a\nnáhoda", "6  Matematika\nv praxi" };
            for (int i = 0; i < AcademyWorld.SectionPositions.Length; i++)
                AddMapPlace(sectionNames[i], AcademyWorld.SectionPositions[i] + new Vector3(0, 0, -6));
            string[] expeditionNames = { "I  Príliv\nzlomkov", "II  Geometrické\nruiny", "III  Observatórium\nrovníc" };
            for (int i = 0; i < AcademyWorld.ExpeditionCenters.Length; i++)
                AddMapPlace(expeditionNames[i], AcademyWorld.ExpeditionCenters[i] + new Vector3(0, 0, 1));
            AddMapPlace("N / SEVER", new Vector3(0, 0, 84));
            AddMapPlace("40 m", new Vector3(61, 0, -82));
            var markerObject = new GameObject("Your position and waypoint", typeof(RectTransform), typeof(LumenMapMarker));
            markerObject.transform.SetParent(mapFrame, false);
            mapMarker = markerObject.GetComponent<LumenMapMarker>(); Stretch(mapMarker.rectTransform, Vector2.zero, Vector2.one);
            mapMarker.Selected = SelectMapDestination;

            mapKey = CompactCard(mapWindow, Cream); mapKey.name = "Map legend";
            CompactLabel(mapKey, "TVOJA POLOHA", 12, Muted);
            mapPosition = CompactLabel(mapKey, "Námestie", 18, Ink);
            CompactLabel(mapKey, "Šípka = ty · lúč = pohľad kamery.\nKlepni na mapu a označ si cieľ.", 14, Muted);
            Rule(mapKey, Frame);
            CompactLabel(mapKey, "ZNAČKY NA MAPE", 12, Muted);
            CompactLabel(mapKey, "Kruh  ·  cvičenie / test\nŠtvorec  ·  učebnica\nKosoštvorec  ·  skúška / aréna\nDvojitý kruh  ·  portál\nMalé bloky  ·  nebeská dráha", 14, Ink);
            mapWaypoint = CompactLabel(mapKey, "Cieľ zatiaľ nie je označený.", 15, Ink);
            var keyActions = CSSRow(mapKey, MinimumTargetSize, true);
            mapClear = CompactButton(keyActions, "Zrušiť cieľ", ClearMapDestination, Lavender, 15);
            CompactButton(keyActions, "Učivo", ShowCurriculum, Lavender, 15);
            CompactLabel(mapKey, "Zavri mapu: šípka ťa povedie.\nObíď miestne prekážky. Výpravy: cez portály.\nM · zatvoriť mapu", 13, Muted);

            mapCompactKey = CompactCard(mapWindow, Cream); mapCompactKey.name = "Compact map legend";
            mapCompactLegend = CompactLabel(mapCompactKey, "", 13, Muted);
            mapCompactWaypoint = CompactLabel(mapCompactKey, "Klepni na mapu a označ si cieľ.", 14, Ink);
            var footer = CSSRow(mapCompactKey, MinimumTargetSize, true);
            mapCompactClear = CompactButton(footer, "Zrušiť cieľ", ClearMapDestination, Lavender);
            CompactButton(footer, "Učivo", ShowCurriculum, Lavender);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(modalPanel);
            RefreshMap();
        }

        private void AddMapPlace(string title, Vector3 position)
        {
            var text = CompactLabel(mapFrame, title, 13, Ink);
            text.gameObject.name = "Map label " + title.Replace('\n', ' ');
            text.alignment = TextAnchor.MiddleCenter; text.lineSpacing = 1;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            mapPlaceLabels.Add(text); mapPlacePositions.Add(position);
        }

        public void RefreshMap()
        {
            if (FeaturePanel != "MAP" || !ModalOpen || !mapWindow || !mapTerrain || !mapMarker || game == null) return;
            var scroll = body.GetComponentInParent<ScrollRect>();
            float available = scroll && scroll.viewport.rect.height > 10 ? scroll.viewport.rect.height - 4 : UiViewportSize.y / interfaceScale - 156;
            float width = Mathf.Max(200, mapWindow.rect.width);
            // Very short windows scroll the complete map/key instead of overlapping them.
            float height = Mathf.Max(width >= 780 ? 420 : width < 480 ? 402 : 322, available);
            var sizing = mapWindow.GetComponent<LayoutElement>();
            if (Mathf.Abs(sizing.preferredHeight - height) > .5f) { sizing.preferredHeight = height; LayoutRebuilder.MarkLayoutForRebuild(body); }
            var size = new Vector2(width, height);
            if ((size - mapLastSize).sqrMagnitude > .5f)
            {
                mapLastSize = size;
                bool wide = width >= 780;
                bool numbered = width < 480;
                mapKey.gameObject.SetActive(wide); mapCompactKey.gameObject.SetActive(!wide);
                if (wide)
                {
                    MapRect(mapFrame, 0, 0, width - 244, height);
                    MapRect(mapKey, width - 236, 0, 236, height);
                }
                else
                {
                    float keyHeight = numbered ? 226 : 146;
                    MapRect(mapFrame, 0, 0, width, Mathf.Max(170, height - keyHeight - 6));
                    MapRect(mapCompactKey, 0, height - keyHeight, width, keyHeight);
                }
                Put(mapCompactLegend, numbered
                    ? "1 Zlomky · 2 Percentá · 3 Rovnice\n4 Geometria · 5 Dáta · 6 Prax\nI Príliv zlomkov · II Geometrické ruiny\nIII Observatórium rovníc\nKruh: úlohy · štvorec: učebnica\nKosoštvorec: skúška · dvojkruh: portál\nNa výpravy vedú portály, nie mosty."
                    : "Kruh: úlohy · štvorec: učebnica · kosoštvorec: skúška\nDvojkruh: portál · malé bloky: nebeská dráha\nPo zatvorení mapy ťa vedie šípka. Výpravy: cez portály.");
                LayoutRebuilder.ForceRebuildLayoutImmediate(mapWindow);
                Rect plot = AcademyMapGeometry.SquarePlot(mapTerrain.rectTransform.rect, 18);
                for (int i = 0; i < mapPlaceLabels.Count; i++)
                {
                    var text = mapPlaceLabels[i];
                    bool section = i >= 1 && i <= 6, expedition = i >= 7 && i <= 9;
                    text.fontSize = plot.width < 360 ? 12 : 13;
                    if (section) text.text = numbered ? i.ToString() : new[] { "1  Čísla a\nzlomky", "2  Percentá\na pomer", "3  Výrazy a\nrovnice", "4  Geometria", "5  Dáta a\nnáhoda", "6  Matematika\nv praxi" }[i - 1];
                    if (expedition) text.text = numbered ? new[] { "I", "II", "III" }[i - 7] : new[] { "I  Príliv\nzlomkov", "II  Geometrické\nruiny", "III  Observatórium\nrovníc" }[i - 7];
                    float labelWidth = section ? Mathf.Max(40, plot.width * .19f) : expedition ? Mathf.Max(50, plot.width * .25f) : 100;
                    Anchor(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), AcademyMapGeometry.Project(mapPlacePositions[i], plot), new Vector2(labelWidth, 38));
                }
                mapTerrain.SetVerticesDirty(); mapMarker.SetVerticesDirty();
            }
            Vector3 player = game.PlayerPosition;
            Vector3 cameraForward = game.ViewCamera ? game.ViewCamera.transform.forward : Vector3.forward;
            mapMarker.SetState(player, game.PlayerYaw, Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg, mapDestination);
            Put(mapPosition, MapAreaName(player));
            string destinationLabel = mapDestinationName ?? "Orientačný bod";
            if (destinationLabel.Length > 42) destinationLabel = destinationLabel.Substring(0, 39) + "…";
            string waypoint = mapDestination.HasValue
                ? destinationLabel + "\n" + Mathf.RoundToInt(Vector2.Distance(new Vector2(player.x, player.z), new Vector2(mapDestination.Value.x, mapDestination.Value.z))) + " m vzdušnou čiarou"
                : "Klepni na mapu a označ si cieľ.";
            Put(mapWaypoint, waypoint); Put(mapCompactWaypoint, waypoint);
            mapClear.interactable = mapCompactClear.interactable = mapDestination.HasValue;
        }

        private static void MapRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top); rect.sizeDelta = new Vector2(width, height);
        }

        private void SelectMapDestination(Vector3 world)
        {
            mapDestinationName = "Orientačný bod";
            float closest = 5f;
            Vector3 requested = world;
            if (game.World != null)
                foreach (var station in game.World.Stations)
                {
                    if (!station) continue;
                    Vector3 p = station.transform.position; float d = Vector2.Distance(new Vector2(requested.x, requested.z), new Vector2(p.x, p.z));
                    if (d < closest) { closest = d; world = new Vector3(p.x, 0, p.z); mapDestinationName = station.Prompt; }
                }
            mapDestination = AcademyNavigation.ClampToFloor(world); RefreshMap();
        }

        private void ClearMapDestination() { mapDestination = null; mapDestinationName = null; RefreshMap(); RefreshNavigation(); }

        private static string MapAreaName(Vector3 position)
        {
            Vector2 flat = new Vector2(position.x, position.z);
            for (int i = 0; i < AcademyWorld.ExpeditionCenters.Length; i++)
            {
                var p = AcademyWorld.ExpeditionCenters[i];
                if (Vector2.Distance(flat, new Vector2(p.x, p.z)) < 13) return AcademyWorld.ExpeditionNames[i];
            }
            for (int i = 0; i < AcademyWorld.SectionPositions.Length; i++)
            {
                var p = AcademyWorld.SectionPositions[i];
                if (Vector2.Distance(flat, new Vector2(p.x, p.z)) < 17) return AcademyWorld.SubjectNames[i];
            }
            return flat.magnitude <= 20 ? "Námestie akadémie" : "Mosty a nebeská dráha";
        }
    }

    /// <summary>Small flat UI meshes, shared by the static chart and its live marker.</summary>
    public abstract class LumenMapGraphic : MaskableGraphic
    {
        protected Rect Plot => AcademyMapGeometry.SquarePlot(rectTransform.rect, 18);
        protected Vector2 Point(Vector3 world) => AcademyMapGeometry.Project(world, Plot);
        protected float Units(float metres) => Plot.width * metres / (AcademyMapGeometry.Extent * 2);

        protected static void Polygon(VertexHelper mesh, Color color, params Vector2[] points)
        {
            if (points.Length < 3) return;
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++) mesh.AddVert(points[i], color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }

        protected static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
            Polygon(mesh, color, from - normal, from + normal, to + normal, to - normal);
        }

        protected static void Disc(VertexHelper mesh, Vector2 center, float radius, Color color, int sides = 32)
        {
            int first = mesh.currentVertCount; mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= sides; i++) { float a = i * Mathf.PI * 2 / sides; mesh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero); }
            for (int i = 0; i < sides; i++) mesh.AddTriangle(first, first + i + 1, first + i + 2);
        }

        protected static void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color color, int sides = 24)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Polygon(mesh, color, center + p * radius, center + p * (radius - width), center + q * (radius - width), center + q * radius);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LumenMapTerrain : LumenMapGraphic
    {
        private WorldLayout world;
        public void Initialize(WorldLayout layout) { world = layout; raycastTarget = false; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (Plot.width < 1) return;
            var grid = new Color(.18f, .25f, .29f, .38f);
            for (int i = -80; i <= 80; i += 20)
            {
                Line(mesh, Point(new Vector3(i, 0, -88)), Point(new Vector3(i, 0, 88)), .65f, grid);
                Line(mesh, Point(new Vector3(-88, 0, i)), Point(new Vector3(88, 0, i)), .65f, grid);
            }
            foreach (Vector3 section in AcademyWorld.SectionPositions)
            {
                AcademyMapGeometry.BridgeEndpoints(section, out var start, out var end);
                Line(mesh, Point(start), Point(end), Units(AcademyMapGeometry.BridgeWidth), new Color(.54f, .55f, .47f));
                Line(mesh, Point(start), Point(end), .7f, new Color(.79f, .75f, .58f, .65f));
            }
            MapIsland(mesh, Vector3.zero, AcademyMapGeometry.CommonsRadius, new Color(.23f, .34f, .29f));
            foreach (Vector3 section in AcademyWorld.SectionPositions)
                MapIsland(mesh, section, AcademyMapGeometry.SectionRadius, new Color(.27f, .35f, .31f));
            foreach (Vector3 expedition in AcademyWorld.ExpeditionCenters)
                MapIsland(mesh, expedition, AcademyMapGeometry.ExpeditionRadius, new Color(.35f, .34f, .27f));
            Ring(mesh, Point(Vector3.zero), Units(12), .8f, new Color(.64f, .65f, .52f, .5f));
            if (world != null)
            {
                foreach (Vector3 platform in world.ParkourPlatforms)
                {
                    Vector2 p = Point(platform); float x = Mathf.Max(1, Units(1.1f)), y = Mathf.Max(1, Units(1.25f));
                    Polygon(mesh, new Color(.75f, .69f, .48f), p + new Vector2(-x, -y), p + new Vector2(-x, y), p + new Vector2(x, y), p + new Vector2(x, -y));
                }
                foreach (var station in world.Stations) if (station) Station(mesh, station);
            }
            // Arrival points are separate from return portals and use a small, open amber square.
            for (int i = 0; i < AcademyWorld.ExpeditionCenters.Length; i++)
                Ring(mesh, Point(AcademyWorld.ExpeditionAnchors[i]), 3.7f, 1, new Color(.91f, .67f, .30f), 4);
            Vector2 scaleStart = Point(new Vector3(41, 0, -76)), scaleEnd = Point(new Vector3(81, 0, -76));
            Line(mesh, scaleStart, scaleEnd, 1.5f, Color.white);
            Line(mesh, scaleStart + Vector2.down * 3, scaleStart + Vector2.up * 3, 1.5f, Color.white);
            Line(mesh, scaleEnd + Vector2.down * 3, scaleEnd + Vector2.up * 3, 1.5f, Color.white);
        }

        private void MapIsland(VertexHelper mesh, Vector3 position, float radius, Color fill)
        {
            Disc(mesh, Point(position), Units(radius) + 1, new Color(.53f, .60f, .53f));
            Disc(mesh, Point(position), Units(radius), fill);
            Ring(mesh, Point(position), Units(radius - 2), .6f, new Color(.52f, .58f, .49f, .5f));
        }

        private void Station(VertexHelper mesh, InteractiveStation station)
        {
            Vector2 p = Point(station.transform.position);
            var chalk = new Color(.88f, .91f, .85f);
            if (station.Mode == "expedition" && !station.Id.Contains("lesson") && !station.Id.Contains("test"))
            { Ring(mesh, p, 4.2f, 1.2f, new Color(.96f, .72f, .35f)); Ring(mesh, p, 2.1f, .8f, new Color(.96f, .72f, .35f)); }
            else if (station.Mode == "study" || station.Id.Contains("lesson"))
            { Polygon(mesh, chalk, p + new Vector2(-3, -3), p + new Vector2(-3, 3), p + new Vector2(3, 3), p + new Vector2(3, -3)); Line(mesh, p + Vector2.down * 2, p + Vector2.up * 2, 1, new Color(.13f, .20f, .21f)); }
            else if (station.Mode == "challenge" || station.Mode == "arena")
            { Polygon(mesh, new Color(.93f, .73f, .42f), p + Vector2.up * 4, p + Vector2.right * 4, p + Vector2.down * 4, p + Vector2.left * 4); }
            else { Disc(mesh, p, 2.6f, chalk, 10); }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LumenMapMarker : LumenMapGraphic, IPointerClickHandler
    {
        public Action<Vector3> Selected;
        private Vector3 player;
        private float yaw, viewYaw;
        private Vector3? destination;

        public void SetState(Vector3 position, float heading, float cameraHeading, Vector3? waypoint)
        {
            if ((player - position).sqrMagnitude < .0001f && Mathf.Abs(Mathf.DeltaAngle(yaw, heading)) < .1f &&
                Mathf.Abs(Mathf.DeltaAngle(viewYaw, cameraHeading)) < .1f && destination == waypoint) return;
            player = position; yaw = heading; viewYaw = cameraHeading; destination = waypoint; SetVerticesDirty();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out var local) &&
                AcademyMapGeometry.TryUnproject(local, Plot, out var world)) Selected?.Invoke(world);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (Plot.width < 1) return;
            Vector2 p = Point(player);
            float cameraAngle = viewYaw * Mathf.Deg2Rad;
            Vector2 cameraDirection = new Vector2(Mathf.Sin(cameraAngle), Mathf.Cos(cameraAngle));
            Line(mesh, p, p + cameraDirection * 25, 1.5f, new Color(.9f, .94f, 1, .5f));
            if (destination.HasValue)
            {
                Vector2 target = Point(destination.Value);
                Ring(mesh, target, 7, 1.5f, new Color(1, .72f, .30f));
                Line(mesh, target + Vector2.left * 11, target + Vector2.right * 11, 1, new Color(1, .72f, .30f));
                Line(mesh, target + Vector2.down * 11, target + Vector2.up * 11, 1, new Color(1, .72f, .30f));
            }
            float angle = yaw * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)), right = new Vector2(forward.y, -forward.x);
            Disc(mesh, p, 8, new Color(.035f, .055f, .07f, .9f), 16);
            Polygon(mesh, Color.white, p + forward * 8, p - forward * 5 - right * 5, p - forward * 2, p - forward * 5 + right * 5);
        }
    }
}
