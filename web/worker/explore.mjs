/** Shared 3D presence and cooperative rewards, backed by D1 rather than a worker cache. */
const BASE = '/api/multiplayer/worlds';
const PRESENCE_MS = 15000;
const WORLD_TTL_MS = 3600000;
const REWARD = 60;
const SPAWN = { x: 0, y: 0, z: 13, yaw: 0 };
const TELEPORTS = { spawn: SPAWN, 'course-start': { x: 20, y: .35, z: 22, yaw: 0 } };
const COURSE_CHECKPOINTS = [[20, .35, 22], [24, .8, 22], [28, 1.2, 24], [32, 1.7, 24], [36, 2.2, 27], [36, 2.7, 31], [32, 3.1, 34]];
const BEACONS = [
  { id: 'beacon-1', x: 6, y: 1, z: 12 }, { id: 'beacon-2', x: -8, y: 1, z: 22 },
  { id: 'beacon-3', x: 14, y: 1, z: -3 }, { id: 'beacon-4', x: -12, y: 1, z: -18 },
  { id: 'beacon-5', x: 4, y: 1, z: -27 }, { id: 'beacon-6', x: 30, y: 1, z: 18 },
];
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const encoder = new TextEncoder();
const record = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const changed = result => Number(result?.meta?.changes || result?.changes || 0);
class ExploreError extends Error {
  constructor(status, error, message, extra = {}) { super(message); this.status = status; this.error = error; this.extra = extra; }
}
const fail = (status, error, message, extra) => { throw new ExploreError(status, error, message, extra); };
const response = (value, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store, private', 'X-Content-Type-Options': 'nosniff' } });
const hex = bytes => [...bytes].map(byte => byte.toString(16).padStart(2, '0')).join('');
const makeCode = () => [...crypto.getRandomValues(new Uint8Array(6))].map(byte => alphabet[byte % alphabet.length]).join('');
const plainPosition = p => ({ x: p.x, y: p.y, z: p.z, yaw: p.yaw });

function validateName(name) {
  if (typeof name !== 'string') fail(400, 'invalid_name', 'Napíš meno s 1 až 20 znakmi.');
  const clean = name.normalize('NFKC').trim().replace(/\s+/gu, ' ');
  if (![...clean].length || [...clean].length > 20 || !/^[\p{L}\p{M}\p{N} _.-]+$/u.test(clean)) fail(400, 'invalid_name', 'Meno môže obsahovať písmená, číslice a medzery.');
  return clean;
}
function validatePet(pet) {
  if (pet === undefined || pet === null) return { kind: 'bunny', color: '#c7a3ff', name: 'Spoločník' };
  if (!record(pet) || !['bunny', 'cat', 'fox', 'owl', 'robot', 'dragon'].includes(pet.kind) || typeof pet.color !== 'string' || !/^#[a-f0-9]{6}$/i.test(pet.color)) fail(400, 'invalid_pet', 'Vyber platného spoločníka.');
  return { kind: pet.kind, color: pet.color.toLowerCase(), name: validateName(pet.name || 'Spoločník') };
}
function validatePosition(position = SPAWN) {
  if (!record(position) || !['x', 'y', 'z', 'yaw'].every(key => typeof position[key] === 'number' && Number.isFinite(position[key])) || Math.abs(position.x) > 48 || Math.abs(position.z) > 48 || position.y < 0 || position.y > 10) fail(400, 'invalid_position', 'Poloha musí byť vo vnútri ostrova.');
  return { x: position.x, y: position.y, z: position.z, yaw: Math.atan2(Math.sin(position.yaw), Math.cos(position.yaw)) };
}
function movementState(position, time) {
  return { ...position, at: time, budget: 1.5, moving: false, onGround: position.y < .1, emote: null, emoteAt: null, left: false };
}
function nextMovement(previous, data, time, { joining = false } = {}) {
  const teleport = data.teleport;
  if (teleport !== undefined && !Object.hasOwn(TELEPORTS, teleport) && teleport !== 'course-checkpoint') fail(400, 'invalid_teleport', 'Tento návrat na kontrolný bod nepoznáme.');
  if (teleport === 'course-checkpoint' && (!Number.isInteger(data.checkpoint) || data.checkpoint < 0 || data.checkpoint >= COURSE_CHECKPOINTS.length)) fail(400, 'invalid_checkpoint', 'Vyber platný kontrolný bod dráhy.');
  const checkpoint = teleport === 'course-checkpoint' ? COURSE_CHECKPOINTS[data.checkpoint] : null;
  let position;
  try { position = checkpoint ? { x: checkpoint[0], y: checkpoint[1], z: checkpoint[2], yaw: 0 } : teleport ? { ...TELEPORTS[teleport] } : validatePosition(data.position ?? null); }
  catch (error) { if (error instanceof ExploreError) error.extra = { position: plainPosition(previous) }; throw error; }
  if (!joining && (typeof data.moving !== 'boolean' || typeof data.onGround !== 'boolean')) fail(400, 'invalid_movement', 'Stav pohybu nemá platný formát.', { position: plainPosition(previous) });
  if (data.emote !== undefined && data.emote !== null && data.emote !== '' && !['wave', 'cheer', 'dance'].includes(data.emote)) fail(400, 'invalid_emote', 'Vyber platné gesto.');
  const elapsed = Math.max(0, Math.min(6, (time - previous.at) / 1000));
  const distance = Math.hypot(position.x - previous.x, position.y - previous.y, position.z - previous.z);
  const allowance = elapsed * 24 + Math.min(1.5, previous.budget ?? 1.5);
  if (!teleport && distance > allowance + .001) fail(409, 'movement_too_fast', 'Pohyb bol príliš rýchly. Vráť sa na poslednú potvrdenú polohu.', { position: plainPosition(previous) });
  return { ...previous, ...position, at: time, budget: teleport ? 1.5 : Math.min(1.5, Math.max(0, allowance - distance)), moving: joining ? false : data.moving, onGround: joining ? position.y < .1 : data.onGround, emote: data.emote === undefined ? previous.emote : data.emote || null, emoteAt: data.emote === undefined ? previous.emoteAt : time, left: false };
}
async function readBody(request) {
  if (!request.body) return {};
  if (!request.headers.get('Content-Type')?.toLowerCase().startsWith('application/json')) fail(415, 'json_required', 'Požiadavka musí obsahovať JSON.');
  if (Number(request.headers.get('Content-Length')) > 8192) fail(413, 'body_too_large', 'Požiadavka je príliš veľká.');
  const reader = request.body.getReader(), chunks = []; let length = 0;
  for (;;) {
    const { value, done } = await reader.read(); if (done) break;
    length += value.byteLength; if (length > 8192) { await reader.cancel(); fail(413, 'body_too_large', 'Požiadavka je príliš veľká.'); }
    chunks.push(value);
  }
  const bytes = new Uint8Array(length); let offset = 0;
  chunks.forEach(chunk => { bytes.set(chunk, offset); offset += chunk.byteLength; });
  try { const data = length ? JSON.parse(new TextDecoder().decode(bytes)) : {}; if (!record(data)) fail(400, 'invalid_json', 'Požiadavka nemá platný formát.'); return data; }
  catch (error) { if (error instanceof ExploreError) throw error; fail(400, 'invalid_json', 'Súbor JSON sa nepodarilo prečítať.'); }
}

export function createExploreApi({ now = () => Date.now() } = {}) {
  let lastCleanup = -Infinity;
  const members = (db, code) => db.prepare('SELECT wp.*, p.name FROM mp_world_players wp JOIN mp_players p ON p.id = wp.player_id WHERE wp.world_code = ? ORDER BY wp.last_seen DESC, wp.player_id').bind(code).all();
  const member = (db, code, id) => db.prepare('SELECT * FROM mp_world_players WHERE world_code = ? AND player_id = ?').bind(code, id).first();
  const live = (rows, time) => rows.filter(row => row.last_seen >= time - PRESENCE_MS && !JSON.parse(row.position_json).left);
  async function authenticate(db, request, time) {
    const value = request.headers.get('Cookie')?.split(';').map(item => item.trim()).find(item => item.startsWith('lumen_mp='))?.slice(9);
    const cookie = value?.match(/^([0-9a-f-]{36})\.([0-9a-f]{64})$/);
    const secretHash = cookie ? hex(new Uint8Array(await crypto.subtle.digest('SHA-256', encoder.encode(cookie[2])))) : '';
    const player = cookie ? await db.prepare('SELECT * FROM mp_players WHERE id = ? AND secret_hash = ?').bind(cookie[1], secretHash).first() : null;
    if (!player) fail(401, 'session_required', 'Najprv si vytvor profil objaviteľa.');
    if (player.active_world) {
      const prior = await member(db, player.active_world, player.id);
      if (!prior || prior.last_seen < time - PRESENCE_MS || JSON.parse(prior.position_json).left) {
        await db.prepare('UPDATE mp_players SET active_world = NULL WHERE id = ? AND active_world = ?').bind(player.id, player.active_world).run(); player.active_world = null;
      }
    }
    return player;
  }
  async function cleanup(db, time) {
    if (time - lastCleanup < 30000) return;
    lastCleanup = time;
    const expired = 'SELECT code FROM mp_worlds WHERE updated_at < ? AND NOT EXISTS (SELECT 1 FROM mp_world_players WHERE world_code = mp_worlds.code AND last_seen >= ?) ORDER BY updated_at LIMIT 10';
    await db.batch([
      db.prepare(`UPDATE mp_players SET active_world = NULL WHERE active_world IN (${expired})`).bind(time - WORLD_TTL_MS, time - WORLD_TTL_MS),
      db.prepare(`DELETE FROM mp_worlds WHERE code IN (${expired})`).bind(time - WORLD_TTL_MS, time - WORLD_TTL_MS),
      db.prepare('UPDATE mp_players SET active_world = NULL WHERE EXISTS (SELECT 1 FROM mp_world_players WHERE player_id = mp_players.id AND world_code = active_world AND last_seen < ?)').bind(time - 60000),
      db.prepare('DELETE FROM mp_world_players WHERE rowid IN (SELECT rowid FROM mp_world_players WHERE last_seen < ? ORDER BY last_seen LIMIT 64)').bind(time - 60000),
    ]);
  }
  async function world(db, code, time) {
    const row = await db.prepare('SELECT * FROM mp_worlds WHERE code = ?').bind(code).first();
    if (!row) fail(404, 'world_not_found', 'Spoločný svet neexistuje alebo už vypršal.');
    if (row.updated_at < time - WORLD_TTL_MS) {
      const recent = await db.prepare('SELECT player_id FROM mp_world_players WHERE world_code = ? AND last_seen >= ? LIMIT 1').bind(code, time - WORLD_TTL_MS).first();
      if (!recent) fail(410, 'world_expired', 'Tento svet vypršal. Vytvor nový.');
    }
    return row;
  }
  async function activate(db, player, code) {
    if (player.active_world && player.active_world !== code) fail(409, 'already_in_world', 'Najprv odíď z aktuálneho spoločného sveta.');
    const result = await db.prepare('UPDATE mp_players SET active_world = ? WHERE id = ? AND (active_world IS NULL OR active_world = ?)').bind(code, player.id, code).run();
    if (!changed(result)) fail(409, 'already_in_world', 'Tvoj profil je už v inom spoločnom svete.');
    player.active_world = code;
  }
  async function settle(db, code, state, time) {
    if (!state.complete || state.settled) return;
    const writes = [];
    for (const id of state.eligibleIds) writes.push(
      db.prepare('INSERT OR IGNORE INTO mp_world_rewards (world_code, player_id, amount, applied, created_at) VALUES (?, ?, ?, 0, ?)').bind(code, id, REWARD, time),
      db.prepare('UPDATE mp_players SET shards = MIN(1000000000, shards + ?) WHERE id = ? AND EXISTS (SELECT 1 FROM mp_world_rewards WHERE world_code = ? AND player_id = ? AND applied = 0)').bind(REWARD, id, code, id),
      db.prepare('UPDATE mp_world_rewards SET applied = 1 WHERE world_code = ? AND player_id = ? AND applied = 0').bind(code, id),
    );
    // Mark completion in the same transaction so a finished walking party can keep
    // polling without repeatedly opening reward-write transactions at movement rate.
    writes.push(db.prepare("UPDATE mp_worlds SET state_json = json_set(state_json, '$.settled', json('true')), version = version + 1 WHERE code = ? AND json_extract(state_json, '$.settled') IS NOT 1").bind(code));
    await db.batch(writes);
  }
  async function snapshot(db, code, player, time) {
    const row = await world(db, code, time), state = JSON.parse(row.state_json);
    await settle(db, code, state, time);
    const rows = live((await members(db, code)).results, time);
    const hostId = rows.some(p => p.player_id === row.host_id) ? row.host_id : rows[0]?.player_id || row.host_id;
    const latest = await db.prepare('SELECT id, name, shards FROM mp_players WHERE id = ?').bind(player.id).first();
    const rewarded = state.complete && state.eligibleIds.includes(player.id);
    const collected = Object.keys(state.collected).length;
    const contributors = new Set(Object.values(state.collected).map(beacon => beacon.playerId));
    const activeContributors = rows.filter(row => contributors.has(row.player_id)).length;
    return {
      world: { code, hostId, serverNow: time, players: rows.map(row => {
        const p = JSON.parse(row.position_json);
        return { id: row.player_id, name: row.name, pet: JSON.parse(row.pet_json), position: plainPosition(p), moving: p.moving, onGround: p.onGround, emote: p.emoteAt !== null && time - p.emoteAt <= 3000 ? p.emote : null, lastSeen: row.last_seen };
      }), beacons: BEACONS.map(beacon => ({ ...beacon, collected: Boolean(state.collected[beacon.id]), collectedBy: state.collected[beacon.id]?.playerId || null })), goal: { collected, total: BEACONS.length, complete: state.complete, reward: REWARD, needsTeam: collected === 5 && !state.complete && activeContributors < 2 }, meId: player.id },
      meId: player.id, me: latest,
      ...(rewarded ? { reward: { shards: REWARD, id: `world-${code}-${player.id}` } } : {}),
    };
  }
  async function heartbeat(db, code, player, time) {
    const row = await member(db, code, player.id);
    if (!row || JSON.parse(row.position_json).left) fail(403, 'not_a_member', 'Do sveta sa najprv pripoj kódom.');
    await activate(db, player, code);
    await db.batch([
      db.prepare("UPDATE mp_world_players SET last_seen = ? WHERE world_code = ? AND player_id = ? AND json_extract(position_json, '$.left') = 0 AND EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_world = ?)").bind(time, code, player.id, player.id, code),
      db.prepare('UPDATE mp_worlds SET updated_at = ? WHERE code = ? AND updated_at < ?').bind(time, code, time - 20000),
    ]);
    return row;
  }

  return async function exploreApi(request, env) {
    const url = new URL(request.url);
    if (url.pathname !== BASE && !url.pathname.startsWith(`${BASE}/`)) return null;
    try {
      if (!env?.DB) fail(503, 'database_unavailable', 'Spoločný svet sa práve pripravuje.');
      if (!['GET', 'POST'].includes(request.method)) fail(405, 'method_not_allowed', 'Táto metóda nie je podporovaná.');
      const origin = request.headers.get('Origin');
      if (origin && origin !== url.origin || request.headers.get('Sec-Fetch-Site') === 'cross-site') fail(403, 'cross_origin', 'Túto požiadavku otvor z adresy hry.');
      const db = typeof env.DB.withSession === 'function' ? env.DB.withSession('first-primary') : env.DB;
      const time = now(); await cleanup(db, time);
      const player = await authenticate(db, request, time), path = url.pathname.slice(BASE.length) || '/';
      if (path === '/' && request.method === 'POST') {
        const data = await readBody(request), pet = validatePet(data.pet), position = validatePosition(data.position);
        if (data.name !== undefined) { player.name = validateName(data.name); await db.prepare('UPDATE mp_players SET name = ? WHERE id = ?').bind(player.name, player.id).run(); }
        if (player.active_world) fail(409, 'already_in_world', 'Najprv odíď z aktuálneho spoločného sveta.');
        for (let attempt = 0; attempt < 4; attempt++) {
          const code = makeCode();
          const old = await db.prepare('SELECT world_code FROM mp_world_rewards WHERE world_code = ? LIMIT 1').bind(code).first(); if (old) continue;
          const state = { collected: {}, complete: false, eligibleIds: [] };
          try {
            const results = await db.batch([
              db.prepare('INSERT INTO mp_worlds (code, state_json, version, host_id, created_at, updated_at) SELECT ?, ?, 0, ?, ?, ? WHERE EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_world IS NULL) AND (SELECT COUNT(*) FROM mp_worlds WHERE updated_at >= ?) < 200').bind(code, JSON.stringify(state), player.id, time, time, player.id, time - WORLD_TTL_MS),
              db.prepare('UPDATE mp_players SET active_world = ? WHERE id = ? AND active_world IS NULL AND EXISTS (SELECT 1 FROM mp_worlds WHERE code = ? AND host_id = ?)').bind(code, player.id, code, player.id),
              db.prepare('INSERT INTO mp_world_players (world_code, player_id, position_json, pet_json, last_seen) SELECT ?, ?, ?, ?, ? WHERE EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_world = ?)').bind(code, player.id, JSON.stringify(movementState(position, time)), JSON.stringify(pet), time, player.id, code),
            ]);
            if (changed(results[0])) return response(await snapshot(db, code, player, time), 201);
            const current = await db.prepare('SELECT active_world FROM mp_players WHERE id = ?').bind(player.id).first();
            if (current.active_world) fail(409, 'already_in_world', 'Tvoj profil je už v inom spoločnom svete.');
            fail(429, 'too_many_worlds', 'Akadémia má veľa otvorených svetov. Pripoj sa k existujúcemu.');
          } catch (error) { if (error instanceof ExploreError) throw error; if (!String(error.message).includes('UNIQUE')) throw error; }
        }
        fail(503, 'code_unavailable', 'Kód sveta sa nepodarilo vytvoriť. Skús to znova.');
      }
      const route = path.match(/^\/([A-Za-z2-9]{6})(?:\/(join|move|collect|leave))?$/);
      if (!route) fail(404, 'endpoint_not_found', 'Táto cesta neexistuje.');
      const code = route[1].toUpperCase(), operation = route[2];
      await world(db, code, time);
      if (!operation && request.method === 'GET') { await heartbeat(db, code, player, time); return response(await snapshot(db, code, player, time)); }
      if (!operation || request.method !== 'POST') fail(405, 'method_not_allowed', 'Táto metóda nie je podporovaná.');
      const data = await readBody(request);
      if (operation === 'join') {
        const pet = validatePet(data.pet), supplied = validatePosition(data.position);
        if (player.active_world && player.active_world !== code) fail(409, 'already_in_world', 'Najprv odíď z aktuálneho spoločného sveta.');
        for (let attempt = 0; attempt < 8; attempt++) {
          const row = await world(db, code, time), prior = await member(db, code, player.id);
          const active = live((await members(db, code)).results, time).filter(p => p.player_id !== player.id);
          if (active.length >= 8) fail(409, 'world_full', 'V tomto svete už je osem objaviteľov.');
          const p = prior ? nextMovement(JSON.parse(prior.position_json), { position: supplied }, time, { joining: true }) : movementState(supplied, time);
          const hostId = active.some(p => p.player_id === row.host_id) ? row.host_id : active[0]?.player_id || player.id;
          const token = crypto.randomUUID();
          const results = await db.batch([
            db.prepare('UPDATE mp_worlds SET state_json = json_set(state_json, \'$.membershipToken\', ?), version = version + 1, host_id = ?, updated_at = ? WHERE code = ? AND version = ? AND EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND (active_world IS NULL OR active_world = ?)) AND (SELECT COUNT(*) FROM mp_world_players WHERE world_code = ? AND last_seen >= ? AND player_id != ? AND json_extract(position_json, \'$.left\') = 0) < 8').bind(token, hostId, time, code, row.version, player.id, code, code, time - PRESENCE_MS, player.id),
            db.prepare('UPDATE mp_players SET active_world = ? WHERE id = ? AND (active_world IS NULL OR active_world = ?) AND EXISTS (SELECT 1 FROM mp_worlds WHERE code = ? AND version = ? AND json_extract(state_json, \'$.membershipToken\') = ?)').bind(code, player.id, code, code, row.version + 1, token),
            db.prepare('INSERT INTO mp_world_players (world_code, player_id, position_json, pet_json, last_seen) SELECT ?, ?, ?, ?, ? WHERE EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_world = ?) AND EXISTS (SELECT 1 FROM mp_worlds WHERE code = ? AND version = ? AND json_extract(state_json, \'$.membershipToken\') = ?) ON CONFLICT (world_code, player_id) DO UPDATE SET position_json = excluded.position_json, pet_json = excluded.pet_json, last_seen = excluded.last_seen').bind(code, player.id, JSON.stringify(p), JSON.stringify(pet), time, player.id, code, code, row.version + 1, token),
          ]);
          if (changed(results[0])) return response(await snapshot(db, code, player, time));
          const current = await db.prepare('SELECT active_world FROM mp_players WHERE id = ?').bind(player.id).first(); if (current.active_world && current.active_world !== code) fail(409, 'already_in_world', 'Tvoj profil je už v inom spoločnom svete.');
        }
        fail(409, 'world_busy', 'Svet sa aktualizuje. Skús pripojenie znova.');
      }
      const existing = await member(db, code, player.id);
      if (!existing || JSON.parse(existing.position_json).left) fail(403, 'not_a_member', 'Do sveta sa najprv pripoj kódom.');
      await activate(db, player, code);
      if (operation === 'move') {
        const hasPet = Object.hasOwn(data, 'pet'), petJson = hasPet ? JSON.stringify(validatePet(data.pet)) : null;
        for (let attempt = 0; attempt < 4; attempt++) {
          const row = await member(db, code, player.id), previous = JSON.parse(row.position_json);
          if (previous.left) fail(403, 'not_a_member', 'Najprv sa znova pripoj k svetu.');
          const p = nextMovement(previous, data, time);
          const results = await db.batch([
            hasPet
              ? db.prepare('UPDATE mp_world_players SET position_json = ?, pet_json = ?, last_seen = ? WHERE world_code = ? AND player_id = ? AND position_json = ? AND EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_world = ?)').bind(JSON.stringify(p), petJson, time, code, player.id, row.position_json, player.id, code)
              : db.prepare('UPDATE mp_world_players SET position_json = ?, last_seen = ? WHERE world_code = ? AND player_id = ? AND position_json = ? AND EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_world = ?)').bind(JSON.stringify(p), time, code, player.id, row.position_json, player.id, code),
            db.prepare('UPDATE mp_worlds SET updated_at = ? WHERE code = ? AND updated_at < ?').bind(time, code, time - 20000),
          ]);
          if (changed(results[0])) return response(await snapshot(db, code, player, time));
        }
        fail(409, 'movement_busy', 'Poloha sa aktualizuje. Skús pohyb znova.');
      }
      if (operation === 'collect') {
        const beacon = BEACONS.find(b => b.id === data.id); if (!beacon) fail(400, 'invalid_beacon', 'Tento tímový signál nepoznáme.');
        for (let attempt = 0; attempt < 8; attempt++) {
          const row = await world(db, code, time), state = JSON.parse(row.state_json);
          if (state.collected[beacon.id]) return response(await snapshot(db, code, player, time));
          const own = await member(db, code, player.id), p = JSON.parse(own.position_json);
          if (p.left || Math.hypot(p.x - beacon.x, p.y + 1 - beacon.y, p.z - beacon.z) > 3.2) fail(409, 'beacon_too_far', 'Príď bližšie k tímovému signálu.');
          const activeIds = new Set(live((await members(db, code)).results, time).map(p => p.player_id)); activeIds.add(player.id);
          const collectors = new Set([...Object.values(state.collected).map(b => b.playerId), player.id]);
          if (Object.keys(state.collected).length === 5 && [...collectors].filter(id => activeIds.has(id)).length < 2) fail(409, 'need_team', 'Posledný signál potrebuje dvoch prítomných objaviteľov, ktorí pomohli zbierať. Pozvi kamaráta.');
          state.collected[beacon.id] = { playerId: player.id, at: time };
          if (Object.keys(state.collected).length === BEACONS.length) { state.complete = true; state.eligibleIds = [...collectors]; }
          const result = await db.prepare('UPDATE mp_worlds SET state_json = ?, version = version + 1, updated_at = ? WHERE code = ? AND version = ? AND EXISTS (SELECT 1 FROM mp_players WHERE id = ? AND active_world = ?) AND EXISTS (SELECT 1 FROM mp_world_players WHERE world_code = ? AND player_id = ? AND json_extract(position_json, \'$.left\') = 0)').bind(JSON.stringify(state), time, code, row.version, player.id, code, code, player.id).run();
          if (changed(result)) { await heartbeat(db, code, player, time); return response(await snapshot(db, code, player, time)); }
        }
        fail(409, 'world_busy', 'Tím práve zbiera signál. Skús to znova.');
      }
      if (operation === 'leave') {
        for (let attempt = 0; attempt < 8; attempt++) {
          const row = await world(db, code, time), own = await member(db, code, player.id), position = JSON.parse(own.position_json);
          position.left = true; position.moving = false; position.emote = null;
          const active = live((await members(db, code)).results, time).filter(p => p.player_id !== player.id), hostId = row.host_id === player.id ? active[0]?.player_id || player.id : row.host_id;
          const token = crypto.randomUUID();
          const results = await db.batch([
            db.prepare('UPDATE mp_worlds SET state_json = json_set(state_json, \'$.membershipToken\', ?), version = version + 1, host_id = ?, updated_at = ? WHERE code = ? AND version = ?').bind(token, hostId, time, code, row.version),
            db.prepare('UPDATE mp_world_players SET position_json = ?, last_seen = ? WHERE world_code = ? AND player_id = ? AND EXISTS (SELECT 1 FROM mp_worlds WHERE code = ? AND version = ? AND json_extract(state_json, \'$.membershipToken\') = ?)').bind(JSON.stringify(position), time - PRESENCE_MS - 1, code, player.id, code, row.version + 1, token),
            db.prepare("UPDATE mp_players SET active_world = NULL WHERE id = ? AND active_world = ? AND EXISTS (SELECT 1 FROM mp_world_players WHERE world_code = ? AND player_id = ? AND json_extract(position_json, '$.left') = 1)").bind(player.id, code, code, player.id),
          ]);
          if (changed(results[0])) return response(await snapshot(db, code, player, time));
        }
        fail(409, 'world_busy', 'Svet sa aktualizuje. Skús odísť znova.');
      }
      fail(404, 'endpoint_not_found', 'Táto cesta neexistuje.');
    } catch (error) {
      if (error instanceof ExploreError) return response({ error: error.error, message: error.message, ...error.extra }, error.status);
      console.error('LUMEN shared world request failed:', error?.name || 'Error');
      return response({ error: 'server_error', message: 'Spoločný svet sa nepodarilo aktualizovať. Skús to o chvíľu.' }, 503);
    }
  };
}
