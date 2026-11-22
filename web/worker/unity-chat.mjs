// Room-scoped, short-lived communication. The caller authenticates and checks live membership.
export const CHAT_TTL_MS = 10 * 60 * 1000;
export const CHAT_BODY_LIMIT = 4096;
export const CHAT_EMOTES = Object.freeze(['wave', 'cheer', 'heart', 'laugh', 'think', 'gg']);
const HISTORY_LIMIT = 50, SEND_INTERVAL_MS = 1000, WINDOW_MS = 60000, WINDOW_LIMIT = 12;
const PRIVATE_CODE = /\b(?:Bearer\s+)?[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.[0-9a-f]{64}\b/i;

export function createRoomChatHandler({fail, cleanName}) {
  function messageInput(data) {
    if (typeof data.text !== 'string' && data.text != null || typeof data.emoteId !== 'string' && data.emoteId != null)
      fail(400, 'invalid_chat', 'Správa musí byť text alebo emócia.');
    const text = (data.text || '').normalize('NFC').trim();
    const emoteId = data.emoteId || '';
    if (!!text === !!emoteId) fail(400, 'invalid_chat', 'Pošli jednu správu alebo jednu emóciu.');
    if (emoteId && !CHAT_EMOTES.includes(emoteId)) fail(400, 'invalid_emote', 'Táto emócia nie je dostupná.');
    if ([...text].length > 240 || /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f\u202a-\u202e\u2066-\u2069]/u.test(text))
      fail(400, 'invalid_chat', 'Použi najviac 240 znakov bez skrytých riadiacich znakov.');
    if (PRIVATE_CODE.test(text)) fail(400, 'private_code', 'Súkromný kód profilu neposielaj do chatu.');
    if (typeof data.clientMessageId !== 'string' || !/^[A-Za-z0-9_-]{16,64}$/.test(data.clientMessageId))
      fail(400, 'invalid_chat_id', 'Chýba identifikátor správy. Skús ju odoslať znova.');
    return {text, emoteId, clientId: data.clientMessageId};
  }
  async function history(db, code, time, afterId = 0) {
    if (!Number.isSafeInteger(afterId) || afterId < 0) fail(400, 'invalid_cursor', 'Neplatná pozícia histórie chatu.');
    const rows = await db.prepare('SELECT id,author_id,author_name,text,emote_id,created_at FROM unity_room_messages WHERE room_code=? AND created_at>=? ORDER BY id DESC LIMIT 50').bind(code, time - CHAT_TTL_MS).all();
    const ordered = rows.results.reverse();
    return {code, serverTime: time, cursor: ordered.at(-1)?.id || 0,
      messages: ordered.filter(row => row.id > afterId).map(row => ({id: row.id, authorId: row.author_id,
        authorName: row.author_name, text: row.text, emoteId: row.emote_id, createdAt: row.created_at}))};
  }
  return async function roomChat(path, data, db, player, own, code, time) {
    if (path !== 'rooms/chat' && path !== 'rooms/chat/send') return null;
    if (data.afterId != null && (!Number.isSafeInteger(data.afterId) || data.afterId < 0)) fail(400, 'invalid_cursor', 'Neplatná pozícia histórie chatu.');
    if (path === 'rooms/chat') return {chat: await history(db, code, time, data.afterId ?? 0)};
    const input = messageInput(data);
    const existing = await db.prepare('SELECT text,emote_id FROM unity_room_messages WHERE room_code=? AND author_id=? AND client_id=?').bind(code, player.id, input.clientId).first();
    if (existing) {
      if (existing.text !== input.text || existing.emote_id !== input.emoteId) fail(409, 'chat_id_conflict', 'Táto správa už bola odoslaná s iným obsahom.');
      return {chat: await history(db, code, time, data.afterId ?? 0), message: 'Správa už bola doručená.'};
    }
    const nonce = crypto.randomUUID(), authorName = cleanName(JSON.parse(own.presence_json).name);
    // A single D1 transaction reserves the rate slot, inserts only the reservation owner's
    // message, and prunes history. Counts remain separate from history, so pruning or
    // leaving/rejoining cannot reset the per-player spam limit.
    const result = await db.batch([
      db.prepare(`INSERT INTO unity_chat_limits (room_code,player_id,last_at,window_at,window_count,nonce)
        VALUES (?,?,?,?,1,?) ON CONFLICT(room_code,player_id) DO UPDATE SET
        last_at=excluded.last_at,window_at=CASE WHEN window_at<=? THEN excluded.window_at ELSE window_at END,
        window_count=CASE WHEN window_at<=? THEN 1 ELSE window_count+1 END,nonce=excluded.nonce
        WHERE last_at<=? AND (window_at<=? OR window_count<?)`)
        .bind(code, player.id, time, time, nonce, time-WINDOW_MS, time-WINDOW_MS, time-SEND_INTERVAL_MS, time-WINDOW_MS, WINDOW_LIMIT),
      db.prepare(`INSERT OR IGNORE INTO unity_room_messages (room_code,author_id,author_name,text,emote_id,client_id,created_at)
        SELECT ?,?,?,?,?,?,? WHERE EXISTS (SELECT 1 FROM unity_chat_limits WHERE room_code=? AND player_id=? AND nonce=?)
        AND EXISTS (SELECT 1 FROM unity_room_members m JOIN unity_rooms r ON r.code=m.room_code
          WHERE m.room_code=? AND m.player_id=? AND m.last_seen>=? AND r.updated_at>=?)`)
        .bind(code, player.id, authorName, input.text, input.emoteId, input.clientId, time, code, player.id, nonce, code, player.id, time-15000, time-30*60*1000),
      db.prepare('DELETE FROM unity_room_messages WHERE room_code=? AND (created_at<? OR id NOT IN (SELECT id FROM unity_room_messages WHERE room_code=? ORDER BY id DESC LIMIT 50))')
        .bind(code, time-CHAT_TTL_MS, code),
    ]);
    if (!Number(result[1]?.meta?.changes || 0)) {
      const duplicate = await db.prepare('SELECT text,emote_id FROM unity_room_messages WHERE room_code=? AND author_id=? AND client_id=?').bind(code, player.id, input.clientId).first();
      if (!duplicate) fail(429, 'chat_rate_limited', 'Počkaj chvíľu. Chat povoľuje jednu správu za sekundu a 12 správ za minútu.');
      if (duplicate.text !== input.text || duplicate.emote_id !== input.emoteId) fail(409, 'chat_id_conflict', 'Táto správa už bola odoslaná s iným obsahom.');
    }
    return {chat: await history(db, code, time, data.afterId ?? 0), message: input.emoteId ? 'Emócia odoslaná.' : 'Správa odoslaná.'};
  };
}
