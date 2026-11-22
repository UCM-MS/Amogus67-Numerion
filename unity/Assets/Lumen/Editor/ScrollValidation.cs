#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Lumen.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Lumen.Editor
{
    /// <summary>Actual uGUI wheel events, fractional trackpad values, clamping and drag interruption.</summary>
    public static class ScrollValidation
    {
        private static int checks;

        [MenuItem("Numerion/Validate Panel Scrolling")]
        public static void Run()
        {
            checks = 0;
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message); };
            Application.logMessageReceived += capture;
            GameObject root = null, events = null;
            try
            {
                root = new GameObject("Numerion scrolling validation", typeof(RectTransform), typeof(Canvas));
                root.hideFlags = HideFlags.DontSave;
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(600, 500);
                events = new GameObject("Scroll validation events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.hideFlags = HideFlags.DontSave;
                var system = events.GetComponent<EventSystem>();
                var module = events.GetComponent<InputSystemUIInputModule>();
                module.scrollDeltaPerTick = 6;
                var current = typeof(EventSystem).GetField("m_CurrentInputModule", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(current != null, "Pinned uGUI exposes its current input module for actual event validation.");
                current.SetValue(system, module);
                var surface = new GameObject("Scrollable page", typeof(RectTransform), typeof(LumenScrollRect));
                surface.transform.SetParent(root.transform, false);
                var rect = (RectTransform)surface.transform;
                rect.sizeDelta = new Vector2(400, 300);
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                viewport.SetParent(rect, false); Stretch(viewport);
                var content = new GameObject("Long page", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 1400);
                var scroll = surface.GetComponent<LumenScrollRect>();
                scroll.viewport = viewport; scroll.content = content; scroll.vertical = true; scroll.horizontal = false;
                scroll.scrollSensitivity = 44; scroll.movementType = ScrollRect.MovementType.Clamped;
                Canvas.ForceUpdateCanvases(); scroll.Rebuild(CanvasUpdate.PostLayout);
                var data = new PointerEventData(system);
                Require(ReferenceEquals(data.currentInputModule, module), "Actual pointer events use the configured Input System module.");
                CheckNear(module.ConvertPointerEventScrollDeltaToTicks(new Vector2(0, -6)).y, -1, "Six event units represent one physical wheel notch.");

                content.anchoredPosition = Vector2.zero;
                Wheel(scroll, data, -6); CheckNear(content.anchoredPosition.y, 44, "One ordinary notch moves 44px, not 264px.");
                Require(data.scrollDelta == new Vector2(0, -6), "Event delta is restored for other listeners.");
                Wheel(scroll, data, 6); CheckNear(content.anchoredPosition.y, 0, "Opposite notch returns to the top.");
                Wheel(scroll, data, .06f); CheckNear(content.anchoredPosition.y, 0, "Upward fractional wheel input clamps at the top.");
                Wheel(scroll, data, -.06f); CheckNear(content.anchoredPosition.y, .44f, "One hundredth of a trackpad tick remains fractional.");
                content.anchoredPosition = Vector2.zero;
                for (int i = 0; i < 100; i++) Wheel(scroll, data, -.06f);
                CheckNear(content.anchoredPosition.y, 44, "One tick split across 100 trackpad events covers the same distance.");
                float before = content.anchoredPosition.y;
                Wheel(scroll, data, -120); CheckNear(content.anchoredPosition.y - before, 225, "A large wheel event is bounded to 75% of the 300px viewport.");
                before = content.anchoredPosition.y;
                Wheel(scroll, data, 0); CheckNear(content.anchoredPosition.y, before, "Zero input does not move content.");
                data.scrollDelta = new Vector2(12, 0); scroll.OnScroll(data);
                CheckNear(content.anchoredPosition.y, before, "Horizontal trackpad motion cannot unexpectedly scroll a vertical page.");
                scroll.velocity = new Vector2(0, 1000); Wheel(scroll, data, -6);
                Require(scroll.velocity == Vector2.zero, "Wheel input cancels the previous touch-flick velocity.");

                content.anchoredPosition = new Vector2(0, content.rect.height - viewport.rect.height - 10);
                Wheel(scroll, data, -6);
                CheckNear(content.anchoredPosition.y, content.rect.height - viewport.rect.height, "Long content clamps exactly at the bottom.");
                Wheel(scroll, data, -6);
                CheckNear(content.anchoredPosition.y, content.rect.height - viewport.rect.height, "Further bottom input cannot overshoot.");
                content.sizeDelta = new Vector2(0, 100);
                scroll.Rebuild(CanvasUpdate.PostLayout); Wheel(scroll, data, -6);
                CheckNear(content.anchoredPosition.y, 0, "Shrinking to a short page removes the old out-of-range offset.");

                content.sizeDelta = new Vector2(0, 1400); content.anchoredPosition = new Vector2(0, 300);
                scroll.Rebuild(CanvasUpdate.PostLayout);
                data.button = PointerEventData.InputButton.Left; data.position = new Vector2(100, 100);
                scroll.OnInitializePotentialDrag(data); scroll.OnBeginDrag(data);
                data.position = new Vector2(100, 150); scroll.OnDrag(data); scroll.OnEndDrag(data);
                CheckNear(content.anchoredPosition.y, 350, "Dragging retains the normal direct 1:1 uGUI content movement.");

                foreach (float value in new[] {float.NaN, float.PositiveInfinity, float.NegativeInfinity})
                    Require(LumenScrollMath.BoundedTicks(new Vector2(value, value), 44, new Vector2(400, 300), true, true) == Vector2.zero,
                        "Invalid input cannot poison content geometry.");
                Require(LumenScrollMath.BoundedTicks(Vector2.one, 0, Vector2.one, true, true) == Vector2.zero,
                    "Zero sensitivity is stable.");
                Require(LumenScrollMath.BoundedTicks(Vector2.one, 44, Vector2.zero, true, true) == Vector2.zero,
                    "A collapsed viewport ignores scrolling.");
                Require(errors.Count == 0, "Actual scrolling errors: " + string.Join(" | ", errors));
                Debug.Log("NUMERION_SCROLL_PASSED: " + checks + " actual Input System/uGUI checks; 6x multiplier normalization, fractional trackpad, event restoration, bounded wheel gestures, top/bottom, resized content and touch drag.");
            }
            finally
            {
                if (root) Object.DestroyImmediate(root);
                if (events) Object.DestroyImmediate(events);
                Application.logMessageReceived -= capture;
            }
        }

        private static void Wheel(LumenScrollRect scroll, PointerEventData data, float y)
        { data.scrollDelta = new Vector2(0, y); scroll.OnScroll(data); }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private static void CheckNear(float actual, float expected, string message)
        { Require(!float.IsNaN(actual) && !float.IsInfinity(actual) && Mathf.Abs(actual - expected) < .1f, message + " Actual " + actual + ", expected " + expected); }

        private static void Require(bool value, string message)
        { checks++; if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
