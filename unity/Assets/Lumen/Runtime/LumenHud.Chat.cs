using System;
using System.Collections.Generic;
using Lumen.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        private InputField chatInput;
        private LumenChatImeGuard chatIme;
        private ScrollRect chatTranscript;
        private RectTransform chatTranscriptContent, chatReactionGrid;
        private Text chatState, chatCounter;
        private Button chatSend, chatMute, chatLatest;
        private readonly List<Button> chatReactions = new List<Button>();
        private readonly List<LumenEmoteGraphic> chatIcons = new List<LumenEmoteGraphic>();
        private string chatDraft = "", chatOwnId = "", chatPendingText, chatRoomCode;
        private long chatPendingAfter;
        private int chatFingerprint = int.MinValue, chatLastSubmitFrame = -1, chatUnseen;
        private bool chatHadRoom, chatWasSending;
        private static readonly string[] ChatEmoteIds = { "wave", "cheer", "heart", "laugh", "think", "gg" };
        private static readonly string[] ChatEmoteLabels = { "Ahoj", "Super", "Srdce", "Smiech", "Myslím", "GG" };

        public void ToggleChat()
        {
            if (ModalOpen && FeaturePanel == "CHAT") CloseModal();
            else if (!ModalOpen) ShowChat();
        }

        public void ShowChat()
        {
            if (!game.Save.HasCreatedProfile) { ShowProfile(true); return; }
            Open("Chat miestnosti", "NUMERION / HRAŤ SPOLU"); FeaturePanel = "CHAT";
            var online = game.Online;
            chatHadRoom = online != null && online.InRoom;
            chatRoomCode = online?.RoomCode;
            if (!chatHadRoom)
            {
                CompactLabel(body, "Chat je dostupný hráčom v rovnakej spoločnej miestnosti.", 18, Ink);
                CompactButton(body, "Hrať spolu", ShowMultiplayer, Lime);
                return;
            }
            chatOwnId = online.IdentityId ?? "";
            BuildChatContent(SendChatDraft, SendChatReaction, () => { game.SetChatMuted(!game.ChatMuted); RefreshChat(); });
            SetChatMessages(online.RoomMessages, true);
            RefreshChat();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(modalPanel);
            RefreshChatLayout(); FollowChat();
            if (!TouchInterface) { chatInput.Select(); chatInput.ActivateInputField(); }
        }

        /// <summary>Production layout builder also used by the real uGUI regression checks.</summary>
        private void BuildChatContent(Action send, Action<string> emote, Action mute)
        {
            chatFingerprint = int.MinValue; chatUnseen = 0;
            chatReactions.Clear(); chatIcons.Clear();
            var outer = body.GetComponentInParent<ScrollRect>();
            DisablePanelScrolling(outer);
            var fitter = body.GetComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            Stretch(body, Vector2.zero, Vector2.one); body.pivot = new Vector2(.5f, 1);

            var toolbar = CSSRow(body, MinimumTargetSize, true);
            CompactLabel(toolbar, "MIESTNOSŤ", 12, Muted);
            chatLatest = CompactButton(toolbar, "Najnovšie ↓", FollowChat, Lavender, 14);
            SetWidth(chatLatest.GetComponent<RectTransform>(), 112);
            chatMute = CompactButton(toolbar, "Stíšiť", mute, Lavender, 14);
            SetWidth(chatMute.GetComponent<RectTransform>(), 90);

            var surface = ImageNode("Room transcript", body, new Color(.045f, .065f, .085f));
            var sizing = surface.gameObject.AddComponent<LayoutElement>(); sizing.minHeight = 100; sizing.flexibleHeight = 1;
            chatTranscript = surface.gameObject.AddComponent<LumenScrollRect>(); chatTranscript.horizontal = false;
            chatTranscript.vertical = true; chatTranscript.movementType = ScrollRect.MovementType.Clamped; chatTranscript.scrollSensitivity = 32;
            surface.gameObject.AddComponent<LumenChatScrollState>();
            var viewport = new GameObject("Transcript viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(surface.transform, false); Stretch(viewport, Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(6, 6); viewport.offsetMax = new Vector2(-12, -6);
            chatTranscriptContent = new GameObject("Last 50 room messages", typeof(RectTransform)).GetComponent<RectTransform>();
            chatTranscriptContent.SetParent(viewport, false);
            chatTranscriptContent.anchorMin = new Vector2(0, 1); chatTranscriptContent.anchorMax = Vector2.one;
            chatTranscriptContent.pivot = new Vector2(.5f, 1); chatTranscriptContent.sizeDelta = Vector2.zero;
            Stack(chatTranscriptContent, 2, 6);
            chatTranscriptContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            chatTranscript.viewport = viewport; chatTranscript.content = chatTranscriptContent;
            AddScrollIndicator(chatTranscript, surface.transform, viewport);
            chatTranscript.onValueChanged.AddListener(_ => UpdateChatLatest());

            var composer = CSSRow(body, MinimumTargetSize, true); composer.name = "Chat composer";
            var inputSurface = ImageNode("Chat message input", composer, new Color(.055f, .075f, .095f));
            var inputSize = inputSurface.gameObject.AddComponent<LayoutElement>(); inputSize.flexibleWidth = 1; inputSize.minWidth = 80; inputSize.minHeight = MinimumTargetSize;
            chatInput = inputSurface.gameObject.AddComponent<InputField>(); chatInput.targetGraphic = inputSurface;
            var inputText = CompactLabel(inputSurface.transform, "", 17, Ink);
            Stretch(inputText.rectTransform, Vector2.zero, Vector2.one); inputText.rectTransform.offsetMin = new Vector2(10, 6); inputText.rectTransform.offsetMax = new Vector2(-10, -6);
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var placeholder = CompactLabel(inputSurface.transform, "Napíš správu…", 17, Muted);
            Stretch(placeholder.rectTransform, Vector2.zero, Vector2.one); placeholder.rectTransform.offsetMin = new Vector2(10, 6); placeholder.rectTransform.offsetMax = new Vector2(-10, -6);
            chatInput.textComponent = inputText; chatInput.placeholder = placeholder;
            chatInput.characterLimit = 240; chatInput.lineType = InputField.LineType.SingleLine;
            chatInput.contentType = InputField.ContentType.Standard;
            chatInput.selectionColor = new Color(.91f, .67f, .30f, .35f);
            chatInput.SetTextWithoutNotify(chatDraft);
            chatInput.onValueChanged.AddListener(value => { chatDraft = value; UpdateChatComposer(); });
            // onSubmit fires for Enter / mobile Done, while losing focus never sends a message.
            chatInput.onSubmit.AddListener(_ => { send(); if (!TouchInterface) chatIme.FocusNextFrame(chatInput); });
            chatIme = inputSurface.gameObject.AddComponent<LumenChatImeGuard>();
            chatSend = CompactButton(composer, "Odoslať", send, Lime, 16); SetWidth(chatSend.GetComponent<RectTransform>(), 86);
            chatCounter = CompactLabel(body, "0/240 · Enter odošle správu.", 12, Muted);

            chatReactionGrid = new GameObject("Six room reactions", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            chatReactionGrid.SetParent(body, false);
            var grid = chatReactionGrid.GetComponent<GridLayoutGroup>(); grid.spacing = new Vector2(6, 6); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            for (int i = 0; i < ChatEmoteIds.Length; i++)
            {
                string id = ChatEmoteIds[i];
                var reaction = CompactButton(chatReactionGrid, ChatEmoteLabels[i], () => emote(id), Lavender, 14);
                reaction.gameObject.name = "Reaction " + id;
                var caption = ButtonText(reaction); caption.alignment = TextAnchor.MiddleCenter;
                caption.rectTransform.offsetMin = new Vector2(30, 4); caption.rectTransform.offsetMax = new Vector2(-5, -4);
                var icon = new GameObject("Animated " + id, typeof(RectTransform), typeof(LumenEmoteGraphic)).GetComponent<LumenEmoteGraphic>();
                icon.transform.SetParent(reaction.transform, false); icon.EmoteId = id; icon.raycastTarget = false; icon.color = Ink;
                Anchor(icon.rectTransform, new Vector2(0, .5f), new Vector2(.5f, .5f), new Vector2(17, 0), new Vector2(24, 24));
                chatReactions.Add(reaction); chatIcons.Add(icon);
            }
            chatState = CompactLabel(body, "Správy vidia iba hráči tejto miestnosti.", 13, Muted);
            UpdateChatComposer(); RefreshChatLayout();
        }

        public void RefreshChat()
        {
            if (FeaturePanel != "CHAT" || !ModalOpen || game == null) return;
            var online = game.Online;
            bool inRoom = online != null && online.InRoom;
            if (inRoom != chatHadRoom || inRoom && online.RoomCode != chatRoomCode) { ShowChat(); return; }
            if (!inRoom || !chatInput) return;
            SetChatMessages(online.RoomMessages, false);
            bool sending = online.ChatSending;
            if (chatPendingText != null && !sending)
            {
                bool accepted = false;
                foreach (var message in online.RoomMessages ?? Array.Empty<OnlineRoomMessage>())
                    if (message != null && message.id > chatPendingAfter && message.authorId == chatOwnId && message.text == chatPendingText) { accepted = true; break; }
                if (accepted)
                {
                    if (chatInput.text.Trim() == chatPendingText) { chatInput.SetTextWithoutNotify(""); chatDraft = ""; }
                    chatPendingText = null;
                }
                else if (chatWasSending) chatPendingText = null; // Failure leaves the draft available for retry.
            }
            chatWasSending = sending;
            Put(chatState, string.IsNullOrWhiteSpace(online.ChatStatus) ? "Správy vidia iba hráči tejto miestnosti." : online.ChatStatus);
            Put(ButtonText(chatMute), game.ChatMuted ? "Zapnúť" : "Stíšiť");
            foreach (var button in chatReactions) button.interactable = !sending;
            foreach (var icon in chatIcons) { icon.ReduceMotion = game.Save.Options.ReduceMotion || game.ChatMuted; icon.Animate = !game.ChatMuted; }
            UpdateChatComposer(); RefreshChatLayout();
        }

        private void UpdateChatComposer()
        {
            if (!chatInput) return;
            Put(chatCounter, chatInput.text.Length + "/240 · Enter odošle správu.");
            if (chatSend) chatSend.interactable = !string.IsNullOrWhiteSpace(chatInput.text) && (game == null || game.Online != null && game.Online.InRoom && !game.Online.ChatSending);
        }

        private void SendChatDraft()
        {
            if (!chatInput || game?.Online == null || !game.Online.InRoom || game.Online.ChatSending ||
                chatLastSubmitFrame == Time.frameCount || chatIme && chatIme.Composing) return;
            string text = chatInput.text.Trim(); if (text.Length == 0) return;
            chatLastSubmitFrame = Time.frameCount;
            chatPendingText = text; chatPendingAfter = 0;
            foreach (var message in game.Online.RoomMessages ?? Array.Empty<OnlineRoomMessage>()) if (message != null) chatPendingAfter = Math.Max(chatPendingAfter, message.id);
            game.Online.SendChat(text); chatWasSending = game.Online.ChatSending;
            UpdateChatComposer();
        }

        private void SendChatReaction(string id)
        {
            if (game?.Online == null || !game.Online.InRoom || game.Online.ChatSending || chatLastSubmitFrame == Time.frameCount) return;
            chatLastSubmitFrame = Time.frameCount; game.Online.SendEmote(id); RefreshChat();
        }

        private void RefreshChatLayout()
        {
            if (!modalPanel || !chatReactionGrid) return;
            Vector2 safe = modalSafeRoot && modalSafeRoot.rect.width > 1 ? modalSafeRoot.rect.size : UiViewportSize / interfaceScale;
            Vector2 desired = new Vector2(Mathf.Min(760, safe.x - 24), Mathf.Min(680, safe.y - 24));
            if ((modalPanel.sizeDelta - desired).sqrMagnitude > .5f)
            { Anchor(modalPanel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, desired); LayoutRebuilder.MarkLayoutForRebuild(modalPanel); }
            var grid = chatReactionGrid.GetComponent<GridLayoutGroup>();
            float width = Mathf.Max(220, body.rect.width - 4); int columns = width < 620 ? 3 : 6;
            grid.constraintCount = columns; grid.cellSize = new Vector2((width - grid.spacing.x * (columns - 1)) / columns, MinimumTargetSize);
            float height = MinimumTargetSize * (6 / columns) + grid.spacing.y * (6 / columns - 1);
            SetHeight(chatReactionGrid, height);
        }

        private void SetChatMessages(OnlineRoomMessage[] messages, bool jumpToLatest)
        {
            if (!chatTranscriptContent) return;
            messages = messages ?? Array.Empty<OnlineRoomMessage>();
            int start = Mathf.Max(0, messages.Length - 50), fingerprint = 17;
            unchecked { for (int i = start; i < messages.Length; i++) if (messages[i] != null) fingerprint = fingerprint * 31 + messages[i].id.GetHashCode(); }
            if (fingerprint == chatFingerprint && !jumpToLatest) return;
            bool following = jumpToLatest || chatTranscript.verticalNormalizedPosition <= .04f && !chatTranscript.GetComponent<LumenChatScrollState>().Dragging;
            float offset = chatTranscriptContent.anchoredPosition.y;
            for (int i = chatTranscriptContent.childCount - 1; i >= 0; i--)
            {
                var child = chatTranscriptContent.GetChild(i).gameObject; child.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying) { DestroyImmediate(child); continue; }
#endif
                Destroy(child);
            }
            chatFingerprint = fingerprint;
            if (messages.Length == 0) CompactLabel(chatTranscriptContent, "Zatiaľ tu nie sú správy.\nPozdrav spoluhráčov správou alebo reakciou.", 16, Muted);
            for (int i = start; i < messages.Length; i++)
            {
                var message = messages[i]; if (message == null) continue;
                bool self = message.authorId == chatOwnId;
                var entry = CompactCard(chatTranscriptContent, self ? Lavender : Cream); entry.name = "Room message " + message.id;
                CompactLabel(entry, (message.authorName ?? "Objaviteľ") + (self ? " · ty" : ""), 13, self ? Lime : Muted);
                if (!string.IsNullOrEmpty(message.emoteId))
                {
                    int index = Array.IndexOf(ChatEmoteIds, message.emoteId);
                    CompactLabel(entry, "Reakcia: " + (index >= 0 ? ChatEmoteLabels[index] : "reakcia"), 16, Ink);
                }
                if (!string.IsNullOrEmpty(message.text)) CompactLabel(entry, message.text, 16, Ink);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatTranscriptContent);
            if (following) FollowChat();
            else
            {
                chatTranscriptContent.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, chatTranscriptContent.rect.height - chatTranscript.viewport.rect.height)));
                chatUnseen++; UpdateChatLatest();
            }
        }

        private void FollowChat()
        {
            if (!chatTranscript) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatTranscriptContent);
            chatTranscript.verticalNormalizedPosition = 0; chatUnseen = 0; UpdateChatLatest();
        }

        private void UpdateChatLatest()
        {
            if (!chatLatest || !chatTranscript) return;
            if (chatTranscript.verticalNormalizedPosition <= .04f) chatUnseen = 0;
            chatLatest.interactable = chatTranscript.verticalNormalizedPosition > .04f;
            Put(ButtonText(chatLatest), chatUnseen > 0 ? "Nové správy ↓" : "Najnovšie ↓");
        }
    }

    public sealed class LumenChatScrollState : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public bool Dragging { get; private set; }
        public void OnBeginDrag(PointerEventData eventData) { Dragging = true; }
        public void OnEndDrag(PointerEventData eventData) { Dragging = false; }
        private void OnDisable() { Dragging = false; }
    }

    /// <summary>IME confirmation must not also submit the chat message in the same input update.</summary>
    public sealed class LumenChatImeGuard : MonoBehaviour
    {
        private Keyboard keyboard;
        private bool composing;
        private int changedFrame = -10;
        private InputField focusAfterSubmit;
        private int focusFrame;
        public bool Composing => composing || Time.frameCount <= changedFrame + 1;
        private void OnEnable() { Bind(); }
        private void Update()
        {
            if (keyboard != Keyboard.current) Bind();
            if (focusAfterSubmit && Time.frameCount > focusFrame)
            { focusAfterSubmit.Select(); focusAfterSubmit.ActivateInputField(); focusAfterSubmit = null; }
        }
        public void FocusNextFrame(InputField field) { focusAfterSubmit = field; focusFrame = Time.frameCount; }
        private void Bind()
        {
            if (keyboard != null) keyboard.onIMECompositionChange -= Composition;
            keyboard = Keyboard.current;
            if (keyboard != null) keyboard.onIMECompositionChange += Composition;
        }
        private void Composition(IMECompositionString value) { composing = value.Count > 0; changedFrame = Time.frameCount; }
        private void OnDisable() { if (keyboard != null) keyboard.onIMECompositionChange -= Composition; keyboard = null; composing = false; focusAfterSubmit = null; }
    }
}
