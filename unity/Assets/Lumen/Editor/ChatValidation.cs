#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lumen.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Lumen.Editor
{
    /// <summary>Exercises the production chat builders and transcript updates without connecting to a room.</summary>
    public static class ChatValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string LiteralMessage = "<b>Matematika & spolupráca</b> <color=red>doslovne</color>";
        private static int checks, screens;

        [MenuItem("Numerion/Validate Room Chat UI")]
        public static void Run()
        {
            checks = screens = 0;
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); };
            Application.logMessageReceived += capture;
            GameObject root = null;
            try
            {
                root = new GameObject("Numerion chat UI validation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                root.hideFlags = HideFlags.DontSave; root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var hud = root.AddComponent<LumenHud>(); Set(hud, "canvas", root.GetComponent<Canvas>());
                var hudRoot = new GameObject("HUD root", typeof(RectTransform)).GetComponent<RectTransform>();
                hudRoot.SetParent(root.transform, false); Set(hud, "hudRoot", hudRoot);
                var regular = Resources.Load<Font>("Fonts/BarlowSemiCondensed-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var strong = Resources.Load<Font>("Fonts/BarlowSemiCondensed-SemiBold") ?? regular;
                Set(hud, "font", regular); Set(hud, "strongFont", strong); Set(hud, "headingFont", strong);
                foreach (var window in new[] {new Vector2(1280, 720), new Vector2(1024, 768), new Vector2(768, 1024), new Vector2(390, 844)})
                    foreach (float scale in new[] {1f, .85f})
                        CheckScreen(hud, root, new Vector2(window.x, window.y - 48), scale);
                Require(errors.Count == 0, "Actual Unity chat UI errors: " + string.Join(" | ", errors));
                Debug.Log("NUMERION_CHAT_UI_PASSED: " + checks + " checks across " + screens
                    + " actual production chat layouts; 1280x720, 1024x768, 768x1024, 390x844 minus48 header at100/85%; six44px reactions, visible composer, plain text, last50 history, stable draft and latest/history behavior.");
            }
            finally { if (root) Object.DestroyImmediate(root); Application.logMessageReceived -= capture; }
        }

        private static void CheckScreen(LumenHud hud, GameObject root, Vector2 viewport, float scale)
        {
            screens++;
            Set(hud, "layoutPreviewSize", viewport); Set(hud, "interfaceScale", scale);
            root.GetComponent<RectTransform>().sizeDelta = viewport / scale;
            var old = (GameObject)Get(hud, "modal"); if (old) Object.DestroyImmediate(old);
            Set(hud, "modal", null); Set(hud, "chatDraft", ""); Set(hud, "chatOwnId", "qa-self");
            Call(hud, "Open", "Chat miestnosti", "NUMERION / HRAŤ SPOLU");
            int submitted = 0;
            Call(hud, "BuildChatContent", (Action)(() => submitted++), (Action<string>)(_ => {}), (Action)(() => {}));
            Rebuild(hud);
            var panel = (RectTransform)Get(hud, "modalPanel");
            var body = (RectTransform)Get(hud, "body");
            var outer = body.GetComponentInParent<ScrollRect>();
            var input = (InputField)Get(hud, "chatInput");
            var transcript = (ScrollRect)Get(hud, "chatTranscript");
            var content = (RectTransform)Get(hud, "chatTranscriptContent");
            var reactions = (RectTransform)Get(hud, "chatReactionGrid");
            var grid = reactions.GetComponent<GridLayoutGroup>();
            string context = viewport + " at " + scale;
            Require(panel.rect.width * scale <= viewport.x + 1 && panel.rect.height * scale <= viewport.y + 1,
                context + " panel fits the available browser canvas.");
            Require(!outer.vertical && !outer.horizontal, context + " only the message transcript scrolls.");
            Require(transcript.GetComponent<RectTransform>().rect.height >= 99, context + " keeps a useful transcript viewport.");
            Require(grid.constraintCount == (body.rect.width - 4 < 620 ? 3 : 6), context + " uses six wide or three narrow reaction columns.");
            var buttons = reactions.GetComponentsInChildren<Button>();
            Require(buttons.Length == 6, context + " exposes all six room reactions.");
            foreach (var reaction in buttons)
            {
                var rect = reaction.GetComponent<RectTransform>();
                Require(rect.rect.width * scale >= 43.5f && rect.rect.height * scale >= 43.5f, context + " reaction target is at least44 CSS pixels: " + reaction.name);
                AssertInside(outer.viewport, rect, context + " reaction stays inside the visible panel.");
            }
            AssertInside(outer.viewport, input.GetComponent<RectTransform>(), context + " composer input stays visible.");
            AssertInside(outer.viewport, ((Button)Get(hud, "chatSend")).GetComponent<RectTransform>(), context + " send action stays visible.");
            Require(input.GetComponent<RectTransform>().rect.height * scale >= 43.5f, context + " composer retains its44px touch height.");
            Require(input.characterLimit == 240 && input.lineType == InputField.LineType.SingleLine, context + " bounded composer contract remains intact.");

            var messages = Messages(60);
            Call(hud, "SetChatMessages", messages, true); Rebuild(hud);
            Require(content.childCount == 50, context + " history is bounded to the last50 messages.");
            Require(content.GetChild(0).name == "Room message 11" && content.GetChild(49).name == "Room message 60", context + " history keeps the newest actual message IDs.");
            Require(content.GetComponentsInChildren<Text>().Any(t => t.text == LiteralMessage), context + " tag-like message content remains literal.");
            foreach (var text in panel.GetComponentsInChildren<Text>()) Require(!text.supportRichText, context + " room text cannot enable rich-text markup.");
            Require(transcript.verticalNormalizedPosition <= .04f, context + " initial history opens at the latest message.");

            const string draft = "Rozpracovaný <b>text</b> pre spoluhráčov";
            input.text = draft;
            transcript.verticalNormalizedPosition = .65f;
            float historyOffset = content.anchoredPosition.y;
            Require(content.rect.height > transcript.viewport.rect.height, context + " history fixture actually needs scrolling.");
            Call(hud, "SetChatMessages", Messages(61), false); Rebuild(hud);
            Require(ReferenceEquals(input, Get(hud, "chatInput")) && input.text == draft,
                context + " an incoming message does not recreate the composer or erase the typed draft.");
            Require(Mathf.Abs(content.anchoredPosition.y - historyOffset) <= 1, context + " incoming messages preserve a reader's history offset.");
            Require(transcript.verticalNormalizedPosition > .04f && (int)Get(hud, "chatUnseen") > 0,
                context + " reading history does not jump to the latest message.");
            Require(((Button)Get(hud, "chatLatest")).interactable, context + " new messages leave a usable return-to-latest action.");
            Call(hud, "FollowChat");
            Call(hud, "SetChatMessages", Messages(62), false); Rebuild(hud);
            Require(transcript.verticalNormalizedPosition <= .04f && (int)Get(hud, "chatUnseen") == 0,
                context + " a reader following the bottom continues to follow incoming messages.");
            Require(input.text == draft && content.childCount == 50, context + " latest-follow updates preserve both draft and history bound.");

            input.onEndEdit.Invoke(input.text);
            Require(submitted == 0, context + " losing field focus must not submit a draft.");
            input.onSubmit.Invoke(input.text);
            Require(submitted == 1, context + " Enter/mobile Done invokes the production submit handler once.");
        }

        private static OnlineRoomMessage[] Messages(int count) => Enumerable.Range(1, count).Select(i => new OnlineRoomMessage {
            id = i, createdAt = i * 1000, authorId = i % 2 == 0 ? "qa-self" : "qa-peer",
            authorName = i == 60 ? "<color=red>Objaviteľ</color>" : "Objaviteľ " + i,
            text = i == 60 ? LiteralMessage : "Správa " + i + " · spoločne precvičujeme matematiku."
        }).ToArray();

        private static void AssertInside(RectTransform viewport, RectTransform element, string message)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, element);
            Require(bounds.min.x >= viewport.rect.xMin - 1 && bounds.max.x <= viewport.rect.xMax + 1
                && bounds.min.y >= viewport.rect.yMin - 1 && bounds.max.y <= viewport.rect.yMax + 1, message);
        }

        private static void Rebuild(LumenHud hud)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                Call(hud, "RefreshChatLayout");
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(hud, "modalPanel"));
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(hud, "body"));
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(hud, "chatTranscriptContent"));
            }
        }

        private static object Call(LumenHud hud, string method, params object[] args) => typeof(LumenHud).GetMethod(method, Private).Invoke(hud, args);
        private static object Get(LumenHud hud, string field) => typeof(LumenHud).GetField(field, Private).GetValue(hud);
        private static void Set(LumenHud hud, string field, object value) => typeof(LumenHud).GetField(field, Private).SetValue(hud, value);
        private static void Require(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException("Room chat UI validation: " + message); }
    }
}
#endif
