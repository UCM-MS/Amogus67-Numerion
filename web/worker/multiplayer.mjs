import { advanceRoom, createRoom, finishRoom, gearDefinitions, GEAR_MAX_LEVEL, GEAR_SLOTS, matchPlayer, MOVES, prepareQuestions, PRESENCE_MS, publicRoom, resolveRound, ROOM_TTL_MS, ROUND_COUNT, startRoom } from './match-core.mjs';

const BASE = '/api/multiplayer';
const COOKIE = 'lumen_mp';
const CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const encoder = new TextEncoder();
const MAX_ROOMS = 200;
const MAX_BODY_BYTES = 8192;

class ApiError extends Error {
  constructor(status, error, message) { super(message); this.status = status; this.error = error; }
}
const fail = (status, error, message) => { throw new ApiError(status, error, message); };
const changed = result => Number(result?.meta?.changes || result?.changes || 0);
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const json = (value, status = 200, extra = {}) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store, private', 'X-Content-Type-Options': 'nosniff', ...extra } });
const readCookie = request => request.headers.get('Cookie')?.split(';').map(s => s.trim()).find(s => s.startsWith(`${COOKIE}=`))?.slice(COOKIE.length + 1);
const cookieHeader = (value, url) => `${COOKIE}=${value}; Path=/api/multiplayer; HttpOnly; SameSite=Lax; Max-Age=2592000${url.protocol === 'https:' ? '; Secure' : ''}`;

function cleanName(value) {
  if (typeof value !== 'string') fail(400, 'invalid_name', 'Napíš meno s 1 až 20 znakmi.');
  const name = value.normalize('NFKC').trim().replace(/\s+/gu, ' ');
  if (![...name].length || [...name].length > 20 || !/^[\p{L}\p{M}\p{N} _.-]+$/u.test(name)) fail(400, 'invalid_name', 'Meno môže obsahovať písmená, číslice, medzery, bodku, pomlčku a podčiarkovník.');
  return name;
}
function cleanPet(value) {
  if (value === undefined || value === null) return { kind: 'bunny', color: '#c7a3ff', name: 'Spoločník' };
  if (!object(value) || !['bunny', 'cat', 'fox', 'owl', 'robot', 'dragon'].includes(value.kind) || typeof value.color !== 'string' || !/^#[0-9a-fA-F]{6}$/.test(value.color)) fail(400, 'invalid_pet', 'Vyber platného spoločníka.');
  return { kind: value.kind, color: value.color.toLowerCase(), name: cleanName(value.name || 'Spoločník') };
}
function randomBytes(length) { return crypto.getRandomValues(new Uint8Array(length)); }
function hex(bytes) { return [...bytes].map(b => b.toString(16).padStart(2, '0')).join(''); }
async function hash(secret) { return hex(new Uint8Array(await crypto.subtle.digest('SHA-256', encoder.encode(secret)))); }
function shuffled(values) {
  const result = [...values];
  for (let i = result.length - 1; i > 0; i--) {
    const random = new Uint32Array(1); crypto.getRandomValues(random);
    const j = Math.floor(random[0] / 4294967296 * (i + 1));
    [result[i], result[j]] = [result[j], result[i]];
  }
  return result;
}
function code() { return [...randomBytes(6)].map(b => CODE_ALPHABET[b % CODE_ALPHABET.length]).join(''); }
function checkOrigin(request, url) {
  const origin = request.headers.get('Origin');
  if ((origin && origin !== url.origin) || request.headers.get('Sec-Fetch-Site') === 'cross-site') fail(403, 'cross_origin', 'Túto požiadavku otvor z adresy hry.');
}
async function body(request) {
  const declared = Number(request.headers.get('Content-Length'));
  if (Number.isFinite(declared) && declared > MAX_BODY_BYTES) fail(413, 'body_too_large', 'Požiadavka je príliš veľká.');
  if (!request.body) return {};
  if (!request.headers.get('Content-Type')?.toLowerCase().startsWith('application/json')) fail(415, 'json_required', 'Požiadavka musí obsahovať JSON.');
  const reader = request.body.getReader(); const chunks = []; let length = 0;
  for (;;) {
    const { done, value } = await reader.read(); if (done) break;
    length += value.byteLength;
    if (length > MAX_BODY_BYTES) { await reader.cancel(); fail(413, 'body_too_large', 'Požiadavka je príliš veľká.'); }
    chunks.push(value);
  }
  const combined = new Uint8Array(length); let offset = 0;
  for (const chunk of chunks) { combined.set(chunk, offset); offset += chunk.byteLength; }
  try {
    const parsed = length ? JSON.parse(new TextDecoder().decode(combined)) : {};
    if (!object(parsed)) fail(400, 'invalid_json', 'Požiadavka nemá platný formát.');
    return parsed;
  } catch (error) { if (error instanceof ApiError) throw error; fail(400, 'invalid_json', 'JSON sa nepodarilo prečítať.'); }
}

export function createMultiplayerApi({ questions, now = () => Date.now() }) {
  const bank = prepareQuestions(questions);
  let lastCleanup = -Infinity;

  const getPlayer = (db, id) => db.prepare('SELECT * FROM mp_players WHERE id = ?').bind(id).first();
  async function authenticated(request, db, time, required = true) {
    const cookie = readCookie(request);
    const match = cookie?.match(/^([0-9a-f-]{36})\.([0-9a-f]{64})$/);
    let player = match ? await db.prepare('SELECT * FROM mp_players WHERE id = ? AND secret_hash = ?').bind(match[1], await hash(match[2])).first() : null;
    if (!player) { if (required) fail(401, 'session_required', 'Najprv si vytvor profil objaviteľa.'); return null; }
    if (player.active_room) {
      const room = await db.prepare('SELECT code, updated_at FROM mp_rooms WHERE code = ?').bind(player.active_room).first();
      if (!room || room.updated_at < time - ROOM_TTL_MS) {
        await db.prepare('UPDATE mp_players SET active_room = NULL WHERE id = ? AND active_room = ?').bind(player.id, player.active_room).run();
        player.active_room = null;
      }
    }
    if (time - player.last_seen > 30000) await db.prepare('UPDATE mp_players SET last_seen = ? WHERE id = ?').bind(time, player.id).run();
    return player;
  }
  async function me(db, id) {
    const player = await getPlayer(db, id);
    const rank = player.matches > 0 ? await db.prepare('SELECT 1 + COUNT(*) AS rank FROM mp_players WHERE matches > 0 AND (rating > ? OR (rating = ? AND wins > ?) OR (rating = ? AND wins = ? AND id < ?))').bind(player.rating, player.rating, player.wins, player.rating, player.wins, id).first() : null;
    const gearDefs = gearDefinitions(player);
    return { me: { id: player.id, name: player.name, rank: rank?.rank ?? null, rating: player.rating, wins: player.wins, matches: player.matches, shards: player.shards, gear: { power: player.power, shield: player.shield, focus: player.focus }, gearCosts: Object.fromEntries(gearDefs.map(g => [g.slot, g.cost])), activeRoomCode: player.active_room || null }, gearDefs };
  }
  async function cleanup(db, time) {
    if (time - lastCleanup < 30000) return;
    lastCleanup = time;
    const stale = 'SELECT code FROM mp_rooms WHERE updated_at < ? ORDER BY updated_at, code LIMIT 20';
    await db.batch([
      db.prepare(`UPDATE mp_players SET active_room = NULL WHERE active_room IN (${stale})`).bind(time - ROOM_TTL_MS),
      db.prepare(`DELETE FROM mp_rooms WHERE code IN (${stale})`).bind(time - ROOM_TTL_MS),
    ]);
  }
  async function settle(db, room, time) {
    if (room.status !== 'finished') return;
    const statements = [];
    for (const reward of room.rewards) {
      statements.push(
        db.prepare('INSERT OR IGNORE INTO mp_match_results (room_code, player_id, rating_delta, shards, winner, applied, created_at) VALUES (?, ?, ?, ?, ?, 0, ?)').bind(room.code, reward.playerId, reward.ratingDelta, reward.shards, reward.winner ? 1 : 0, time),
        db.prepare('UPDATE mp_players SET rating = MAX(100, MIN(5000, rating + ?)), shards = MIN(1000000000, shards + ?), matches = matches + 1, wins = wins + ? WHERE id = ? AND EXISTS (SELECT 1 FROM mp_match_results WHERE room_code = ? AND player_id = ? AND applied = 0)').bind(reward.ratingDelta, reward.shards, reward.winner ? 1 : 0, reward.playerId, room.code, reward.playerId),
        db.prepare('UPDATE mp_match_results SET applied = 1 WHERE room_code = ? AND player_id = ? AND applied = 0').bind(room.code, reward.playerId),
      );
    }
    statements.push(db.prepare('UPDATE mp_players SET active_room = NULL WHERE active_room = ?').bind(room.code));
    // D1 batch is a transaction: the ledger flag and wallet update cannot diverge.
    await db.batch(statements);
  }
  async function roomAction(db, roomCode, player, time, mutate = null, { joining = false, membership = null } = {}) {
    for (let attempt = 0; attempt < 8; attempt++) {
      const row = await db.prepare('SELECT * FROM mp_rooms WHERE code = ?').bind(roomCode).first();
      if (!row) fail(404, 'room_not_found', 'Miestnosť neexistuje alebo jej platnosť skončila.');
      if (row.updated_at < time - ROOM_TTL_MS) fail(410, 'room_expired', 'Táto miestnosť už vypršala. Vytvor novú.');
      const state = JSON.parse(row.state_json);
      const member = state.players.find(p => p.id === player.id);
      if (!joining && !member) fail(403, 'not_a_member', 'Do tejto miestnosti sa najprv pripoj kódom.');
      const before = JSON.stringify(state);
      advanceRoom(state, bank, time);
      if (member && !member.forfeited && time - member.seenAt >= 8000) member.seenAt = time;
      let actionError = null;
      try { if (mutate) await mutate(state, member); } catch (error) { if (!(error instanceof ApiError)) throw error; actionError = error; }
      const next = JSON.stringify(state);
      if (next !== before) {
        let sql = 'UPDATE mp_rooms SET state_json = ?, revision = revision + 1, updated_at = ? WHERE code = ? AND revision = ?';
        const params = [next, time, roomCode, row.revision];
        if (joining && !actionError) { sql += ' AND EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND (active_room IS NULL OR active_room = ?))'; params.push(player.id, roomCode); }
        const writes = [db.prepare(sql).bind(...params)];
        if (membership && !actionError) {
          const flag = membership === 'join' ? '' : " AND json_extract(value, '$.forfeited') = 1";
          const inside = `SELECT 1 FROM mp_rooms, json_each(mp_rooms.state_json, '$.players') WHERE mp_rooms.code = ? AND mp_rooms.revision = ? AND json_extract(value, '$.id') = ?${flag}`;
          if (membership === 'join') writes.push(db.prepare(`UPDATE mp_players SET active_room = ? WHERE id = ? AND (active_room IS NULL OR active_room = ?) AND EXISTS (${inside})`).bind(roomCode, player.id, roomCode, roomCode, row.revision + 1, player.id));
          else writes.push(db.prepare(`UPDATE mp_players SET active_room = NULL WHERE id = ? AND active_room = ? AND (NOT EXISTS (SELECT 1 FROM mp_rooms, json_each(mp_rooms.state_json, '$.players') WHERE mp_rooms.code = ? AND json_extract(value, '$.id') = ?) OR EXISTS (${inside}))`).bind(player.id, roomCode, roomCode, player.id, roomCode, row.revision + 1, player.id));
        }
        const result = await db.batch(writes);
        if (!changed(result[0])) {
          if (joining) { const latest = await getPlayer(db, player.id); if (latest.active_room && latest.active_room !== roomCode) fail(409, 'already_in_room', 'Najprv odíď z aktuálnej miestnosti.'); }
          continue;
        }
      }
      await settle(db, state, time);
      if (actionError) throw actionError;
      return { room: publicRoom(state, bank, time), ...(await me(db, player.id)) };
    }
    fail(409, 'room_busy', 'Miestnosť sa práve aktualizuje. Skús to o chvíľu znova.');
  }

  return async function multiplayerApi(request, env) {
    const url = new URL(request.url);
    if (url.pathname !== BASE && !url.pathname.startsWith(`${BASE}/`)) return null;
    try {
      if (!env?.DB) fail(503, 'database_unavailable', 'Spoločný svet sa práve pripravuje. Skús to o chvíľu.');
      if (!['GET', 'POST'].includes(request.method)) fail(405, 'method_not_allowed', 'Táto metóda nie je podporovaná.');
      checkOrigin(request, url);
      const db = env.DB, time = now();
      await cleanup(db, time);
      const path = url.pathname.slice(BASE.length) || '/';
      if (path === '/session' && request.method === 'POST') {
        const data = await body(request), name = cleanName(data.name);
        let player = await authenticated(request, db, time, false);
        let headers = {};
        if (player) {
          await db.prepare('UPDATE mp_players SET name = ?, last_seen = ? WHERE id = ?').bind(name, time, player.id).run();
          // Renew the validated credential, keeping the same anonymous identity and wallet.
          headers = { 'Set-Cookie': cookieHeader(readCookie(request), url) };
        }
        else {
          const recent = await db.prepare('SELECT COUNT(*) AS count FROM mp_players WHERE created_at > ?').bind(time - 60000).first();
          if (recent.count >= 500) fail(429, 'too_many_sessions', 'Vzniká veľa nových profilov. Počkaj chvíľu.');
          const id = crypto.randomUUID(), secret = hex(randomBytes(32));
          await db.prepare('INSERT INTO mp_players (id, secret_hash, name, created_at, last_seen) VALUES (?, ?, ?, ?, ?)').bind(id, await hash(secret), name, time, time).run();
          player = await getPlayer(db, id);
          headers = { 'Set-Cookie': cookieHeader(`${id}.${secret}`, url) };
        }
        return json(await me(db, player.id), 200, headers);
      }
      const player = await authenticated(request, db, time);
      if (path === '/me' && request.method === 'GET') {
        if (player.active_room) await roomAction(db, player.active_room, player, time);
        return json(await me(db, player.id));
      }
      if (path === '/leaderboard' && request.method === 'GET') {
        const result = await db.prepare('SELECT id, name, rating, wins, matches FROM mp_players WHERE matches > 0 ORDER BY rating DESC, wins DESC, id ASC LIMIT 50').all();
        return json({ players: result.results.map((p, i) => ({ ...p, rank: i + 1 })), ...(await me(db, player.id)) });
      }
      if (path === '/gear' && request.method === 'POST') {
        const { slot } = await body(request);
        if (!GEAR_SLOTS.includes(slot)) fail(400, 'invalid_gear', 'Vyber platné vybavenie.');
        for (let attempt = 0; attempt < 3; attempt++) {
          const current = await getPlayer(db, player.id), def = gearDefinitions(current).find(g => g.slot === slot);
          if (current[slot] >= GEAR_MAX_LEVEL) fail(409, 'gear_maxed', 'Vybavenie už má najvyšší stupeň.');
          if (current.shards < def.cost) fail(409, 'insufficient_shards', `Na ďalší stupeň potrebuješ ${def.cost} kryštálových úlomkov.`);
          const result = await db.prepare(`UPDATE mp_players SET shards = shards - ?, ${slot} = ${slot} + 1 WHERE id = ? AND ${slot} = ? AND shards >= ?`).bind(def.cost, player.id, current[slot], def.cost).run();
          if (changed(result)) return json(await me(db, player.id));
        }
        fail(409, 'wallet_busy', 'Peňaženka sa aktualizuje. Skús nákup znova.');
      }
      if (path === '/rooms' && request.method === 'POST') {
        const data = await body(request);
        if (!['quiz', 'duel'].includes(data.mode)) fail(400, 'invalid_mode', 'Vyber vedomostné preteky alebo duel.');
        const pet = cleanPet(data.pet);
        if (player.active_room) await roomAction(db, player.active_room, player, time);
        const latest = await getPlayer(db, player.id);
        if (latest.active_room) fail(409, 'already_in_room', 'Najprv odíď z aktuálnej miestnosti.');
        for (let attempt = 0; attempt < 4; attempt++) {
          const roomCode = code();
          const prior = await db.prepare('SELECT room_code FROM mp_match_results WHERE room_code = ? LIMIT 1').bind(roomCode).first();
          if (prior) continue;
          const questionIds = shuffled(bank.keys()).slice(0, ROUND_COUNT);
          const optionOrders = Object.fromEntries(questionIds.map(id => [id, shuffled(bank.get(id).options.map(o => o.id))]));
          const state = createRoom(roomCode, data.mode, latest, pet, questionIds, time, optionOrders);
          try {
            const writes = await db.batch([
              db.prepare("INSERT INTO mp_rooms (code, state_json, revision, created_at, updated_at) SELECT ?, ?, 0, ?, ? WHERE EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_room IS NULL) AND (SELECT COUNT(*) FROM mp_rooms WHERE updated_at >= ? AND json_extract(state_json, '$.status') != 'finished') < ?").bind(roomCode, JSON.stringify(state), time, time, latest.id, time - ROOM_TTL_MS, MAX_ROOMS),
              db.prepare('UPDATE mp_players SET active_room = ? WHERE id = ? AND active_room IS NULL AND EXISTS (SELECT 1 FROM mp_rooms WHERE code = ?)').bind(roomCode, latest.id, roomCode),
            ]);
            if (changed(writes[0])) return json({ room: publicRoom(state, bank, time), ...(await me(db, player.id)) }, 201);
            const current = await getPlayer(db, player.id);
            if (current.active_room) fail(409, 'already_in_room', 'Najprv odíď z aktuálnej miestnosti.');
            fail(429, 'too_many_rooms', 'Akadémia má veľa otvorených miestností. Skús sa pripojiť do existujúcej.');
          } catch (error) { if (error instanceof ApiError) throw error; if (!String(error.message).includes('UNIQUE')) throw error; }
        }
        fail(503, 'code_unavailable', 'Kód miestnosti sa nepodarilo vytvoriť. Skús to znova.');
      }
      const route = path.match(/^\/rooms\/([A-Za-z2-9]{6})(?:\/(join|start|answer|leave))?$/);
      if (!route) fail(404, 'endpoint_not_found', 'Táto cesta neexistuje.');
      const roomCode = route[1].toUpperCase(), operation = route[2];
      if (!operation && request.method === 'GET') return json(await roomAction(db, roomCode, player, time));
      if (!operation || request.method !== 'POST') fail(405, 'method_not_allowed', 'Táto metóda nie je podporovaná.');
      const data = await body(request);
      if (operation === 'join') {
        const pet = cleanPet(data.pet);
        if (player.active_room && player.active_room !== roomCode) {
          await roomAction(db, player.active_room, player, time);
          if ((await getPlayer(db, player.id)).active_room) fail(409, 'already_in_room', 'Najprv odíď z aktuálnej miestnosti.');
        }
        return json(await roomAction(db, roomCode, player, time, (state, member) => {
          if (member && !member.forfeited) { member.seenAt = time; return; }
          if (state.status !== 'lobby') fail(409, 'already_started', 'Zápas už začal. Pripoj sa do novej miestnosti.');
          if (state.players.length >= (state.mode === 'duel' ? 2 : 8)) fail(409, 'room_full', 'Táto miestnosť je plná.');
          state.players.push(matchPlayer(player, pet, time));
        }, { joining: true, membership: 'join' }));
      }
      if (operation === 'start') return json(await roomAction(db, roomCode, player, time, async state => {
        if (state.hostId !== player.id) fail(403, 'host_required', 'Zápas môže začať zakladateľ miestnosti.');
        if (state.status !== 'lobby') fail(409, 'already_started', 'Zápas už začal.');
        if (state.players.filter(p => !p.forfeited && time - p.seenAt < PRESENCE_MS).length < 2) fail(409, 'need_players', 'Na zápas potrebujete aspoň dvoch prítomných hráčov.');
        const ids = state.players.map(p => p.id);
        const rows = await db.prepare(`SELECT * FROM mp_players WHERE id IN (${ids.map(() => '?').join(',')})`).bind(...ids).all();
        for (const member of state.players) { const current = rows.results.find(p => p.id === member.id); member.rating = current.rating; member.gear = { power: current.power, shield: current.shield, focus: current.focus }; }
        startRoom(state, time);
      }));
      if (operation === 'answer') return json(await roomAction(db, roomCode, player, time, (state, member) => {
        if (member.forfeited) fail(403, 'left_match', 'Z tohto zápasu už si odišiel.');
        if (state.status !== 'question' || !Number.isInteger(data.round) || data.round !== state.round) fail(409, 'round_closed', 'Táto otázka už nie je otvorená.');
        const q = bank.get(state.questionIds[state.round - 1]);
        if (typeof data.answerId !== 'string' || !q.options.some(o => o.id === data.answerId)) fail(400, 'invalid_answer', 'Vyber jednu z ponúknutých odpovedí.');
        const move = data.move ?? 'strike';
        if (!MOVES.includes(move)) fail(400, 'invalid_move', 'Vyber úder, obranu alebo sústredenie.');
        if (state.answers[player.id]) fail(409, 'already_answered', 'Odpoveď už je uložená. Počkaj na výsledok kola.');
        state.answers[player.id] = { answerId: data.answerId, move, at: time }; member.seenAt = time;
        if (state.players.filter(p => !p.forfeited).every(p => state.answers[p.id])) resolveRound(state, bank, time);
      }));
      if (operation === 'leave') return json(await roomAction(db, roomCode, player, time, (state, member) => {
        if (state.status === 'lobby') {
          state.players = state.players.filter(p => p.id !== player.id);
          if (state.hostId === player.id) state.hostId = state.players[0]?.id || null;
          if (!state.players.length) { state.status = 'finished'; state.deadline = null; state.finishedAt = time; state.rewards = []; }
        } else if (state.status !== 'finished' && !member.forfeited) {
          member.forfeited = true;
          if (state.players.filter(p => !p.forfeited).length < 2) finishRoom(state, time);
          else if (state.status === 'question' && state.players.filter(p => !p.forfeited).every(p => state.answers[p.id])) resolveRound(state, bank, time);
        }
      }, { membership: 'leave' }));
      fail(404, 'endpoint_not_found', 'Táto cesta neexistuje.');
    } catch (error) {
      if (error instanceof ApiError) return json({ error: error.error, message: error.message }, error.status);
      console.error('LUMEN multiplayer request failed:', error?.name || 'Error');
      return json({ error: 'server_error', message: 'Spoločný svet sa nepodarilo aktualizovať. Skús to o chvíľu.' }, 503);
    }
  };
}
