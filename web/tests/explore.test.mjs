import assert from 'node:assert/strict';
import { createTestD1 } from './d1-adapter.mjs';
import { AcademyEngine } from '../public/academy-engine.js';
import { createMultiplayerApi } from '../worker/multiplayer.mjs';
import { createExploreApi } from '../worker/explore.mjs';

const DB = createTestD1();
let time = 1900000000000, primarySessions = 0;
const env = { DB: { ...DB, withSession(mode) { assert.equal(mode, 'first-primary'); primarySessions++; return DB; } } };
const mpApi = createMultiplayerApi({ questions: new AcademyEngine(null).getDefinitions().questions, now: () => time });
const api = createExploreApi({ now: () => time });
const origin = 'https://academy.example';
const pet = { kind: 'fox', color: '#ffab77', name: 'Koko' };
const spawn = { x: 0, y: 0, z: 13, yaw: 0 };
async function call(path, { method = 'GET', data, cookie, headers = {}, requestApi = api } = {}) {
  const h = { ...(cookie ? { Cookie: cookie } : {}), ...headers };
  if (data !== undefined) h['Content-Type'] ??= 'application/json';
  const response = await requestApi(new Request(origin + path, { method, headers: h, ...(data !== undefined ? { body: typeof data === 'string' ? data : JSON.stringify(data) } : {}) }), env);
  return { status: response?.status, response, result: response ? await response.json() : null };
}
const worldCall = (path, options) => call('/api/multiplayer/worlds' + path, options);
async function session(name) {
  const result = await call('/api/multiplayer/session', { method: 'POST', data: { name }, requestApi: mpApi });
  assert.equal(result.status, 200);
  return { id: result.result.me.id, cookie: result.response.headers.get('Set-Cookie').split(';')[0] };
}
const move = (code, player, position, extra = {}) => worldCall(`/${code}/move`, { method: 'POST', cookie: player.cookie, data: { position, moving: true, onGround: true, ...extra } });
const join = (code, player, position = spawn) => worldCall(`/${code}/join`, { method: 'POST', cookie: player.cookie, data: { position, pet } });
const collect = (code, player, id) => worldCall(`/${code}/collect`, { method: 'POST', cookie: player.cookie, data: { id } });

try {
  assert.equal(await api(new Request(origin + '/api/multiplayer/rooms'), env), null);
  assert.equal((await worldCall('', { method: 'POST', data: { pet } })).status, 401);
  const ada = await session('Ada'), lin = await session('Lin'), outsider = await session('Nela');
  assert.equal((await worldCall('', { method: 'POST', cookie: ada.cookie, data: { pet }, headers: { Origin: 'https://attacker.example' } })).status, 403);
  assert.equal((await worldCall('', { method: 'POST', cookie: ada.cookie, data: { pet, position: { ...spawn, x: 49 } } })).status, 400);
  assert.equal((await worldCall('', { method: 'POST', cookie: ada.cookie, data: { pet: { ...pet, color: 'red<script>' } } })).status, 400);
  assert.equal((await worldCall('', { method: 'POST', cookie: ada.cookie, data: '{broken-json' })).status, 400);
  const created = await worldCall('', { method: 'POST', cookie: ada.cookie, data: { pet, position: spawn, name: 'Ada' } });
  assert.equal(created.status, 201);
  const code = created.result.world.code;
  assert.match(code, /^[A-Z2-9]{6}$/);
  assert.equal(created.result.world.hostId, ada.id);
  assert.equal(created.result.world.players.length, 1);
  assert.equal(created.result.world.beacons.length, 6);
  assert.equal(created.result.world.goal.collected, 0);
  assert.equal(created.result.world.goal.reward, 60);
  assert.equal(created.result.meId, ada.id);
  assert.equal(created.result.reward, undefined);
  assert.equal(JSON.stringify(created.result).includes('secret_hash'), false);
  assert.equal(JSON.stringify(created.result).includes('membershipToken'), false);
  assert.equal((await worldCall(`/${code}`, { cookie: outsider.cookie })).status, 403);
  assert.equal((await move(code, outsider, spawn, { playerId: ada.id })).status, 403);
  const forgedCookie = ada.cookie.slice(0, -1) + (ada.cookie.endsWith('a') ? 'b' : 'a');
  assert.equal((await worldCall(`/${code}`, { cookie: forgedCookie })).status, 401);
  assert.equal((await join(code, lin)).status, 200);
  assert.equal((await join(code, lin)).result.world.players.length, 2);
  assert.equal((await worldCall('', { method: 'POST', cookie: lin.cookie, data: { pet } })).status, 409);

  // Motion updates touch independent player rows and never increment the world goal revision.
  const versionBefore = (await DB.prepare('SELECT version FROM mp_worlds WHERE code = ?').bind(code).first()).version;
  time += 250;
  const independent = await Promise.all([move(code, ada, { ...spawn, x: 4 }), move(code, lin, { ...spawn, x: -4 })]);
  assert.ok(independent.every(r => r.status === 200));
  assert.equal((await DB.prepare('SELECT version FROM mp_worlds WHERE code = ?').bind(code).first()).version, versionBefore);
  const players = (await worldCall(`/${code}`, { cookie: ada.cookie })).result.world.players;
  assert.equal(players.find(p => p.id === ada.id).position.x, 4);
  assert.equal(players.find(p => p.id === lin.id).position.x, -4);
  // Equipped companions update through the same movement CAS, without rejoining.
  const newPet = { kind: 'owl', color: '#FFDDAA', name: 'Mia' }, currentPosition = { ...spawn, x: 4 };
  const equipped = await move(code, ada, currentPosition, { pet: newPet });
  assert.equal(equipped.status, 200);
  assert.deepEqual(equipped.result.world.players.find(p => p.id === ada.id).pet, { ...newPet, color: '#ffddaa' });
  const remoteEquipment = (await worldCall(`/${code}`, { cookie: lin.cookie })).result.world.players.find(p => p.id === ada.id).pet;
  assert.deepEqual(remoteEquipment, { ...newPet, color: '#ffddaa' });
  const ordinaryMove = await move(code, ada, currentPosition);
  assert.deepEqual(ordinaryMove.result.world.players.find(p => p.id === ada.id).pet, remoteEquipment, 'Ordinary movement preserves equipped companion');
  const badEquipment = await move(code, ada, { ...currentPosition, x: 4.25 }, { pet: { ...newPet, kind: 'script' } });
  assert.equal(badEquipment.status, 400); assert.equal(badEquipment.result.error, 'invalid_pet');
  const unchangedEquipment = (await worldCall(`/${code}`, { cookie: lin.cookie })).result.world.players.find(p => p.id === ada.id);
  assert.deepEqual(unchangedEquipment.pet, remoteEquipment); assert.deepEqual(unchangedEquipment.position, currentPosition, 'Invalid equipment never commits accompanying movement');
  assert.equal((await DB.prepare('SELECT version FROM mp_worlds WHERE code = ?').bind(code).first()).version, versionBefore, 'Equipment sync does not mutate the shared goal revision');
  const fast = await move(code, ada, { ...spawn, x: 30 });
  assert.equal(fast.status, 409); assert.equal(fast.result.error, 'movement_too_fast'); assert.deepEqual(fast.result.position, { ...spawn, x: 4 });
  assert.equal((await move(code, ada, { ...spawn, x: 4 }, { onGround: 'true' })).status, 400);
  assert.equal((await move(code, ada, { ...spawn, x: Number.NaN })).status, 400);
  assert.equal((await move(code, ada, { ...spawn, yaw: Number.POSITIVE_INFINITY })).status, 400);
  assert.equal((await worldCall(`/${code}/move`, { method: 'POST', cookie: ada.cookie, data: { moving: true, onGround: true } })).status, 400);
  // The small tolerance is a consumed budget; repeated zero-time requests cannot move endlessly.
  assert.equal((await move(code, ada, { ...spawn, x: 5.5 })).status, 200);
  assert.equal((await move(code, ada, { ...spawn, x: 6.5 })).status, 409);
  assert.equal((await move(code, ada, { x: 200, y: 99, z: 200, yaw: 0 }, { teleport: 'spawn' })).status, 200);
  const course = await move(code, ada, spawn, { teleport: 'course-start' });
  assert.deepEqual(course.result.world.players.find(p => p.id === ada.id).position, { x: 20, y: .35, z: 22, yaw: 0 });
  const checkpoint = await move(code, ada, spawn, { teleport: 'course-checkpoint', checkpoint: 6 });
  assert.deepEqual(checkpoint.result.world.players.find(p => p.id === ada.id).position, { x: 32, y: 3.1, z: 34, yaw: 0 });
  assert.equal((await move(code, ada, spawn, { teleport: 'course-checkpoint', checkpoint: 7 })).status, 400);
  assert.equal((await move(code, ada, spawn, { teleport: 'anywhere' })).status, 400);
  const wave = await move(code, ada, spawn, { teleport: 'spawn', emote: 'wave' });
  assert.equal(wave.result.world.players.find(p => p.id === ada.id).emote, 'wave');
  assert.equal((await move(code, ada, spawn, { emote: '<script>' })).status, 400);
  time += 3001;
  assert.equal((await worldCall(`/${code}`, { cookie: ada.cookie })).result.world.players.find(p => p.id === ada.id).emote, null);

  // Collecting is proximity-checked and CAS-idempotent. Five signals alone leave a team opportunity.
  assert.equal((await collect(code, ada, 'beacon-1')).status, 409);
  assert.equal((await collect(code, ada, 'fake-beacon')).status, 400);
  const beacons = created.result.world.beacons;
  for (const beacon of beacons.slice(0, 5)) {
    time += 2500;
    assert.equal((await move(code, ada, { x: beacon.x, y: 0, z: beacon.z, yaw: 0 })).status, 200);
    const picked = await collect(code, ada, beacon.id);
    assert.equal(picked.status, 200);
    assert.equal(picked.result.world.beacons.find(b => b.id === beacon.id).collectedBy, ada.id);
    const repeated = await collect(code, ada, beacon.id);
    assert.equal(repeated.status, 200);
    assert.equal(repeated.result.world.goal.collected, picked.result.world.goal.collected);
    assert.equal(picked.result.me.shards, 0);
    await worldCall(`/${code}`, { cookie: lin.cookie });
  }
  time += 2500;
  assert.equal((await move(code, ada, { x: beacons[5].x, y: 0, z: beacons[5].z, yaw: 0 })).status, 200);
  const soloLast = await collect(code, ada, 'beacon-6');
  assert.equal(soloLast.status, 409); assert.equal(soloLast.result.error, 'need_team');
  const beforeTeam = (await worldCall(`/${code}`, { cookie: ada.cookie })).result;
  assert.equal(beforeTeam.world.goal.collected, 5); assert.equal(beforeTeam.world.goal.complete, false);
  time += 2000;
  assert.equal((await move(code, lin, { x: beacons[5].x, y: 0, z: beacons[5].z, yaw: 0 })).status, 200);
  const raced = await Promise.all([collect(code, ada, 'beacon-6'), collect(code, lin, 'beacon-6')]);
  assert.equal(raced[1].status, 200); assert.ok([200, 409].includes(raced[0].status));
  const complete = (await worldCall(`/${code}`, { cookie: ada.cookie })).result;
  assert.equal(complete.world.goal.complete, true); assert.equal(complete.world.goal.collected, 6);
  assert.equal(complete.me.shards, 60); assert.equal(complete.reward.shards, 60);
  assert.equal(complete.reward.id, `world-${code}-${ada.id}`);
  const completedLin = (await worldCall(`/${code}`, { cookie: lin.cookie })).result;
  assert.equal(completedLin.me.shards, 60);
  // A later join has zero contribution and receives neither wallet money nor a reward callback.
  const lateJoin = await join(code, outsider, { x: 30, y: 0, z: 18, yaw: 0 });
  assert.equal(lateJoin.status, 200); assert.equal(lateJoin.result.reward, undefined); assert.equal(lateJoin.result.me.shards, 0);
  await Promise.all(Array.from({ length: 8 }, (_, i) => worldCall(`/${code}`, { cookie: i % 2 ? ada.cookie : lin.cookie })));
  assert.equal((await collect(code, lin, 'beacon-6')).result.me.shards, 60);
  assert.equal((await DB.prepare('SELECT COUNT(*) AS n FROM mp_world_rewards WHERE world_code = ? AND applied = 1').bind(code).first()).n, 2);
  assert.equal(JSON.parse((await DB.prepare('SELECT state_json FROM mp_worlds WHERE code = ?').bind(code).first()).state_json).settled, true);
  const career = await DB.prepare('SELECT rating, wins, matches FROM mp_players WHERE id = ?').bind(ada.id).first();
  assert.deepEqual({ ...career }, { rating: 1000, wins: 0, matches: 0 });
  const restart = createExploreApi({ now: () => time });
  assert.equal((await worldCall(`/${code}`, { cookie: ada.cookie, requestApi: restart })).result.me.shards, 60);

  // An interrupted payout commits the goal first, but rolls back the entire wallet ledger.
  const recoveryA = await session('RecoveryA'), recoveryB = await session('RecoveryB');
  const recoveryPosition = { x: 30, y: 0, z: 18, yaw: 0 };
  const recoveryCreated = await worldCall('', { method: 'POST', cookie: recoveryA.cookie, data: { pet, position: recoveryPosition } });
  const recoveryCode = recoveryCreated.result.world.code;
  assert.equal((await join(recoveryCode, recoveryB, recoveryPosition)).status, 200);
  // Use a deterministic already-collected fixture to isolate the final transaction failure.
  const recoveryState = { collected: Object.fromEntries(beacons.slice(0, 5).map((b, i) => [b.id, { playerId: i % 2 ? recoveryB.id : recoveryA.id, at: time }])), complete: false, eligibleIds: [] };
  await DB.prepare('UPDATE mp_worlds SET state_json = ? WHERE code = ?').bind(JSON.stringify(recoveryState), recoveryCode).run();
  DB.database.exec("CREATE TEMP TRIGGER fail_shared_reward BEFORE UPDATE OF shards ON mp_players WHEN NEW.shards > OLD.shards BEGIN SELECT RAISE(ABORT, 'simulated world payout failure'); END");
  const originalConsoleError = console.error; let failedReward;
  try { console.error = () => {}; failedReward = await collect(recoveryCode, recoveryA, 'beacon-6'); }
  finally { console.error = originalConsoleError; }
  assert.equal(failedReward.status, 503);
  assert.equal(JSON.parse((await DB.prepare('SELECT state_json FROM mp_worlds WHERE code = ?').bind(recoveryCode).first()).state_json).complete, true);
  assert.equal(JSON.parse((await DB.prepare('SELECT state_json FROM mp_worlds WHERE code = ?').bind(recoveryCode).first()).state_json).settled, undefined);
  assert.equal((await DB.prepare('SELECT COUNT(*) AS n FROM mp_world_rewards WHERE world_code = ?').bind(recoveryCode).first()).n, 0);
  assert.equal((await DB.prepare('SELECT shards FROM mp_players WHERE id = ?').bind(recoveryA.id).first()).shards, 0);
  assert.equal((await DB.prepare('SELECT shards FROM mp_players WHERE id = ?').bind(recoveryB.id).first()).shards, 0);
  DB.database.exec('DROP TRIGGER fail_shared_reward');
  const recovered = await Promise.all([recoveryA, recoveryB].map(p => worldCall(`/${recoveryCode}`, { cookie: p.cookie, requestApi: restart })));
  assert.ok(recovered.every(r => r.status === 200 && r.result.me.shards === 60 && r.result.reward.shards === 60));
  await Promise.all([recoveryA, recoveryB].map(p => collect(recoveryCode, p, 'beacon-6')));
  assert.equal((await DB.prepare('SELECT COUNT(*) AS n FROM mp_world_rewards WHERE world_code = ? AND applied = 1').bind(recoveryCode).first()).n, 2);
  assert.equal(JSON.parse((await DB.prepare('SELECT state_json FROM mp_worlds WHERE code = ?').bind(recoveryCode).first()).state_json).settled, true);
  assert.equal((await DB.prepare('SELECT shards FROM mp_players WHERE id = ?').bind(recoveryA.id).first()).shards, 60);

  // Room capacity and profile membership stay valid under concurrent joins/creates.
  const filler = [];
  for (let i = 0; i < 4; i++) { const p = await session(`Filler${i}`); filler.push(p); assert.equal((await join(code, p)).status, 200); }
  const racerA = await session('RacerA'), racerB = await session('RacerB');
  const joinRace = await Promise.all([join(code, racerA), join(code, racerB)]);
  assert.equal(joinRace.filter(r => r.status === 200).length, 1);
  assert.equal(joinRace.filter(r => r.status === 409).length, 1);
  assert.equal((await worldCall(`/${code}`, { cookie: ada.cookie })).result.world.players.length, 8);
  const creator = joinRace[0].status === 409 ? racerA : racerB;
  const createRace = await Promise.all([0, 1].map(() => worldCall('', { method: 'POST', cookie: creator.cookie, data: { pet, position: spawn } })));
  assert.equal(createRace.filter(r => r.status === 201).length, 1);
  assert.equal(createRace.filter(r => r.status === 409).length, 1);
  const secondCode = createRace.find(r => r.status === 201).result.world.code;
  assert.equal((await join(code, creator)).status, 409);
  assert.equal((await worldCall(`/${secondCode}/leave`, { method: 'POST', cookie: creator.cookie, data: {} })).status, 200);

  const ownerX = await session('OwnerX'), ownerY = await session('OwnerY'), crossRacer = await session('CrossRacer');
  const worldX = (await worldCall('', { method: 'POST', cookie: ownerX.cookie, data: { pet } })).result.world.code;
  const worldY = (await worldCall('', { method: 'POST', cookie: ownerY.cookie, data: { pet } })).result.world.code;
  const crossRace = await Promise.all([join(worldX, crossRacer), join(worldY, crossRacer)]);
  assert.equal(crossRace.filter(r => r.status === 200).length, 1);
  assert.equal(crossRace.filter(r => r.status === 409).length, 1);
  const losingCode = crossRace[0].status === 409 ? worldX : worldY, losingOwner = losingCode === worldX ? ownerX : ownerY;
  assert.equal((await worldCall(`/${losingCode}`, { cookie: losingOwner.cookie })).result.world.players.some(p => p.id === crossRacer.id), false);

  // Leaving preserves a brief motion history, so instant rejoin cannot bypass speed checks.
  assert.equal((await move(code, ada, { x: 30, y: 0, z: 18, yaw: 0 })).status, 200);
  const left = await worldCall(`/${code}/leave`, { method: 'POST', cookie: ada.cookie, data: {} });
  assert.equal(left.status, 200); assert.equal(left.result.world.players.some(p => p.id === ada.id), false);
  assert.notEqual(left.result.world.hostId, ada.id);
  assert.equal((await move(code, ada, spawn)).status, 403);
  assert.equal((await join(code, ada, spawn)).status, 409);
  assert.equal((await join(code, ada, { x: 30, y: 0, z: 18, yaw: 0 })).status, 200);

  // Peers disappear after the presence grace period; a heartbeat can resume before cleanup.
  time += 15001;
  const resumed = await worldCall(`/${code}`, { cookie: ada.cookie });
  assert.equal(resumed.status, 200); assert.deepEqual(resumed.result.world.players.map(p => p.id), [ada.id]);
  const resumedLin = await worldCall(`/${code}`, { cookie: lin.cookie });
  assert.equal(resumedLin.status, 200); assert.equal(resumedLin.result.world.players.length, 2);
  time += 3600001;
  assert.ok([404, 410].includes((await worldCall(`/${code}`, { cookie: ada.cookie })).status));
  assert.equal((await DB.prepare('SELECT active_world FROM mp_players WHERE id = ?').bind(ada.id).first()).active_world, null);
  assert.ok(primarySessions > 0);
  console.log('Explore: shared cookie identity, primary D1 sessions, independent motion CAS, speed/burst/bounds guards, fixed checkpoints, emotes, eight-player races, presence/expiry, proximity-checked team signals and exact-once contribution rewards passed.');
} finally { DB.close(); }
