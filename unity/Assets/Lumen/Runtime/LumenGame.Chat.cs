using System;
using System.Linq;
using Lumen.World;
using UnityEngine;

namespace Lumen.Runtime
{
    public sealed partial class LumenGame
    {
        public bool ChatMuted { get; private set; }
        private string emoteRoom = "";
        private long emoteCursor;
        private bool emoteHistoryPrimed;

        public void SetChatMuted(bool muted)
        {
            ChatMuted = muted;
            if (!muted) return;
            if (offline) LumenEmoteBubble.Clear(offline.transform);
            foreach (var avatar in sharedAvatars.Values)
                if (avatar) LumenEmoteBubble.Clear(avatar.transform);
        }

        private void ResetRoomChatContext(OnlineRoom room)
        {
            string code = room?.code ?? "";
            if (code == emoteRoom) return;
            emoteRoom = code; emoteCursor = 0; emoteHistoryPrimed = false;
            if (offline) LumenEmoteBubble.Clear(offline.transform);
            Hud?.RefreshChat();
        }

        private void ReceiveRoomChat(OnlineRoomMessage[] messages)
        {
            ResetRoomChatContext(Online?.Room);
            Hud?.RefreshChat();
            if (!SharedConnected || !Online.ChatHistoryReady) return;
            messages ??= Array.Empty<OnlineRoomMessage>();
            long newest = messages.Length == 0 ? 0 : messages.Max(message => message.id);
            // Joining a room loads its transcript without replaying other people's old reactions.
            if (!emoteHistoryPrimed)
            {
                emoteHistoryPrimed = true; emoteCursor = newest; return;
            }
            OnlineRoomMessage notification = null;
            foreach (var message in messages.Where(message => message.id > emoteCursor).OrderBy(message => message.id))
            {
                if (ChatMuted) continue;
                if (LumenEmotes.IsValid(message.emoteId))
                {
                    Transform actor = null;
                    if (message.authorId == Online.IdentityId)
                        actor = Connected ? Session.LocalPlayer.transform : offline != null ? offline.transform : null;
                    else if (sharedAvatars.TryGetValue(message.authorId, out var avatar) && avatar)
                        actor = avatar.transform;
                    if (actor) LumenEmoteBubble.Show(actor, message.emoteId, Save.Options.ReduceMotion);
                }
                else if (!string.IsNullOrEmpty(message.text) && message.authorId != Online.IdentityId)
                    notification = message;
            }
            emoteCursor = Math.Max(emoteCursor, newest);
            if (notification != null && Hud != null && !Hud.ModalOpen)
            {
                string text = notification.text;
                if (text.Length > 80)
                {
                    int length = char.IsHighSurrogate(text[79]) ? 79 : 80;
                    text = text.Substring(0, length) + "…";
                }
                Notify(notification.authorName + ": " + text);
            }
        }
    }
}
