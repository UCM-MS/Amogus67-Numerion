using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Lumen.Runtime
{
    [Serializable] public sealed class OnlineRoomMessage
    {
        public long id, createdAt;
        public string authorId, authorName, text, emoteId;
    }
    [Serializable] public sealed class OnlineRoomChat
    {
        public string code;
        public long cursor, serverTime;
        public OnlineRoomMessage[] messages = Array.Empty<OnlineRoomMessage>();
    }

    public sealed partial class OnlineAcademyClient
    {
        public static readonly string[] EmoteIds = { "wave", "cheer", "heart", "laugh", "think", "gg" };
        public OnlineRoomMessage[] RoomMessages { get; private set; } = Array.Empty<OnlineRoomMessage>();
        public string ChatStatus { get; private set; } = "Chat je dostupný hráčom v rovnakej miestnosti.";
        public bool ChatSending => pendingChat != null || chatSending;
        public bool ChatHistoryReady { get; private set; }
        public event Action<OnlineRoomMessage[]> RoomMessagesChanged;
        public event Action<string> ChatStatusChanged;

        [Serializable] private sealed class ChatRequest
        {
            public string code, text, emoteId, clientMessageId;
            public long afterId;
        }
        [Serializable] private sealed class ChatResponse
        {
            public bool ok;
            public string message;
            public OnlineRoomChat chat;
        }
        private string chatRoom = "";
        private int chatGeneration = -1;
        private long chatCursor;
        private float nextChatPoll;
        private bool chatInFlight, chatSending;
        private ChatRequest pendingChat;

        public void SendChat(string text)
        {
            text = (text ?? "").Trim();
            int count = 0;
            for (int i = 0; i < text.Length; i++, count++)
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            if (count == 0 || count > 240) { SetChatStatus("Napíš správu s dĺžkou 1 až 240 znakov."); return; }
            QueueChat(text, "");
        }
        public void SendEmote(string id)
        {
            if (!EmoteIds.Contains(id)) { SetChatStatus("Táto emócia nie je dostupná."); return; }
            QueueChat("", id);
        }
        private void QueueChat(string text, string emote)
        {
            if (!InRoom) { SetChatStatus("Najprv sa pripoj do spoločnej miestnosti."); return; }
            SyncChatRoom();
            if (ChatSending) { SetChatStatus("Počkaj na odoslanie predchádzajúcej správy."); return; }
            pendingChat = new ChatRequest { code = RoomCode, text = text, emoteId = emote, clientMessageId = Guid.NewGuid().ToString("N") };
            SetChatStatus("Správa čaká na odoslanie…");
        }
        private void SyncChatRoom()
        {
            if (chatRoom == RoomCode && chatGeneration == generation) return;
            chatRoom = RoomCode; chatGeneration = generation; chatCursor = 0; nextChatPoll = 0;
            pendingChat = null; chatInFlight = false; chatSending = false; ChatHistoryReady = false;
            RoomMessages = Array.Empty<OnlineRoomMessage>(); RoomMessagesChanged?.Invoke(RoomMessages);
            SetChatStatus(InRoom ? "Chat miestnosti · najviac 240 znakov." : "Chat je dostupný hráčom v rovnakej miestnosti.");
        }
        private void TickRoomChat()
        {
            SyncChatRoom();
            if (!InRoom || identityToken.Length == 0 || Busy || chatInFlight) return;
            if (pendingChat != null && ChatHistoryReady)
            {
                var next = pendingChat; pendingChat = null; chatSending = true;
                StartCoroutine(ChatTransport("rooms/chat/send", next, true));
            }
            else if (Time.unscaledTime >= nextChatPoll)
                StartCoroutine(ChatTransport("rooms/chat", new ChatRequest { code = RoomCode }, false));
        }
        private IEnumerator ChatTransport(string path, ChatRequest body, bool sending)
        {
            chatInFlight = true; int requestGeneration = generation; string room = RoomCode;
            body.afterId = chatCursor; nextChatPoll = Time.unscaledTime + 1.5f;
            if (!ValidOrigin(BaseUrl)) { chatInFlight = false; chatSending = false; SetChatStatus("Neplatná adresa servera chatu."); yield break; }
            if (sending) SetChatStatus("Odosielam…");
            using (var request = new UnityWebRequest(BaseUrl.TrimEnd('/') + "/api/unity/" + path, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 8;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + identityToken);
                yield return request.SendWebRequest();
                if (requestGeneration != generation || RoomCode != room) yield break;
                ChatResponse response = null;
                try { response = JsonUtility.FromJson<ChatResponse>(request.downloadHandler.text); } catch (ArgumentException) { }
                if (request.result == UnityWebRequest.Result.Success && response != null && response.ok && response.chat != null && response.chat.code == room)
                {
                    AcceptChat(response.chat, !sending);
                    if (sending) SetChatStatus(string.IsNullOrEmpty(response.message) ? "Doručené." : response.message);
                }
                else
                {
                    SetChatStatus(!string.IsNullOrEmpty(response?.message) ? response.message : "Chat je dočasne nedostupný. Pohyb a súboj pokračujú.");
                    nextChatPoll = Time.unscaledTime + (request.responseCode == 429 ? 3 : 5);
                }
            }
            if (requestGeneration == generation && RoomCode == room)
            {
                chatInFlight = false; chatSending = false;
                if (sending) ChatStatusChanged?.Invoke(ChatStatus);
            }
        }
        private void AcceptChat(OnlineRoomChat update, bool historyRead)
        {
            var byId = new Dictionary<long, OnlineRoomMessage>();
            foreach (var message in RoomMessages.Concat(update.messages ?? Array.Empty<OnlineRoomMessage>()))
                if (message != null && message.id > 0 && message.createdAt >= update.serverTime - 600000) byId[message.id] = message;
            var next = byId.Values.OrderBy(message => message.id).TakeLast(50).ToArray();
            bool changed = next.Length != RoomMessages.Length || next.Where((message, i) => message.id != RoomMessages[i].id).Any();
            bool firstHistory = historyRead && !ChatHistoryReady;
            if (historyRead) ChatHistoryReady = true;
            chatCursor = Math.Max(chatCursor, update.cursor); RoomMessages = next;
            if (changed || firstHistory) RoomMessagesChanged?.Invoke(RoomMessages);
        }
        private void SetChatStatus(string message)
        {
            if (ChatStatus == message) return;
            ChatStatus = message; ChatStatusChanged?.Invoke(message);
        }
    }
}
