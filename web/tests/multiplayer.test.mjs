import assert from 'node:assert/strict';
import { createTestD1 } from './d1-adapter.mjs';
import { AcademyEngine } from '../public/academy-engine.js';
import { createMultiplayerApi } from '../worker/multiplayer.mjs';
import { advanceRoom, createRoom, finishRoom, matchPlayer, prepareQuestions, publicRoom, QUESTION_MS, REVEAL_MS, startRoom } from '../worker/match-core.mjs';

const questions = new AcademyEngine(null).getDefinitions().questions;
const answerBank = new Map(questions.map(q => [q.id, q]));
const DB = createTestD1();
let time = 1900000000000;
const api = createMultiplayerApi({ questions, now: () => time });
const origin = 'https://academy.example';
async function call(path, { method = 'GET', data, cookie, headers = {}, requestApi = api } = {}) {
  const requestHeaders = { ...(cookie ? { Cookie: cookie } : {}), ...headers };
  if (data !== undefined) requestHeaders['Content-Type'] ??= 'application/json';
  const response = await requestApi(new Request(origin + '/api/multiplayer' + path, { method, headers: requestHeaders, ...(data !== undefined ? { body: typeof data === 'string' ? data : JSON.stringify(data) } : {}) }), { DB });
  const result = response ? await response.json() : null;
  return { response, result, status: response?.status };
}
async function session(name) {
  const created = await call('/session', { method: 'POST', data: { name, shards: 99999, rating: 99999 } });
  assert.equal(created.status, 200);
  const setCookie = created.response.headers.get('Set-Cookie');
  assert.match(setCookie, /HttpOnly/); assert.match(setCookie, /SameSite=Lax/); assert.match(setCookie, /Secure/);
  assert.equal(created.result.me.rating, 1000); assert.equal(created.result.me.shards, 0);
  assert.equal(created.result.me.rank, null);
  assert.equal(created.result.gearDefs.length, 3);
  return { id: created.result.me.id, cookie: setCookie.split(';')[0] };
}
const pet = { kind: 'fox', color: '#ffab77', name: 'Mochi' };

try {
  assert.equal(await api(new Request(origin + '/unrelated'), { DB }), null);
  assert.equal((await call('/me')).status, 401);
  assert.equal((await call('/session', { method: 'POST', data: { name: '<script>' } })).status, 400);
  assert.equal((await call('/session', { method: 'POST', data: { name: 'x'.repeat(21) } })).status, 400);
  assert.equal((await call('/session', { method: 'POST', data: '{bad-json' })).status, 400);
  assert.equal((await call('/session', { method: 'POST', data: { name: 'Valid' }, headers: { Origin: 'https://attacker.example' } })).status, 403);
  assert.equal((await call('/session', { method: 'POST', data: { name: 'x'.repeat(9000) } })).status, 413);
  const ada = await session('Ada'), lin = await session('Lin'), outsider = await session('Nela');
  const renewed = await call('/session', { method: 'POST', cookie: ada.cookie, data: { name: 'Ada' } });
  assert.equal(renewed.result.me.id, ada.id);
  assert.equal(renewed.response.headers.get('Set-Cookie').split(';')[0], ada.cookie);
  assert.match(renewed.response.headers.get('Set-Cookie'), /Max-Age=2592000/);
  assert.match(renewed.response.headers.get('Set-Cookie'), /HttpOnly/);
  assert.match(renewed.response.headers.get('Set-Cookie'), /Secure/);
  assert.deepEqual((await call('/leaderboard', { cookie: ada.cookie })).result.players, []);
  const invalidCookie = ada.cookie.slice(0, -1) + (ada.cookie.endsWith('a') ? 'b' : 'a');
  assert.equal((await call('/me', { cookie: invalidCookie })).status, 401);
  assert.equal((await call('/me', { cookie: ada.cookie, headers: { 'Sec-Fetch-Site': 'cross-site' } })).status, 403);
  assert.equal((await call('/gear', { method: 'POST', cookie: ada.cookie, data: { slot: 'shards', amount: 10000 } })).status, 400);
  assert.equal((await call('/gear', { method: 'POST', cookie: ada.cookie, data: { slot: 'power', cost: 0 } })).status, 409);
  const created = await call('/rooms', { method: 'POST', cookie: ada.cookie, data: { mode: 'quiz', pet } });
  assert.equal(created.status, 201);
  const roomCode = created.result.room.code;
  assert.match(roomCode, /^[A-Z2-9]{6}$/);
  assert.equal(created.result.room.status, 'lobby');
  assert.equal(created.result.room.question, null);
  assert.equal((await call(`/rooms/${roomCode}/start`, { method: 'POST', cookie: ada.cookie })).status, 409);
  assert.equal((await call(`/rooms/${roomCode}`, { cookie: outsider.cookie })).status, 403);
  assert.equal((await call('/rooms', { method: 'POST', cookie: ada.cookie, data: { mode: 'duel', pet } })).status, 409);
  assert.equal((await call(`/rooms/${roomCode}/join`, { method: 'POST', cookie: lin.cookie, data: { pet } })).status, 200);
  const repeatedJoin = await call(`/rooms/${roomCode}/join`, { method: 'POST', cookie: lin.cookie, data: { pet } });
  assert.equal(repeatedJoin.result.room.players.length, 2);
  assert.equal((await call(`/rooms/${roomCode}/start`, { method: 'POST', cookie: lin.cookie })).status, 403);
  const start = await call(`/rooms/${roomCode}/start`, { method: 'POST', cookie: ada.cookie });
  assert.equal(start.status, 200);
  assert.equal(start.result.room.round, 1);
  assert.equal(start.result.room.deadline, time + QUESTION_MS);
  assert.equal((await call(`/rooms/${roomCode}/join`, { method: 'POST', cookie: outsider.cookie, data: { pet } })).status, 409);
  let state = start.result;
  for (let round = 1; round <= 6; round++) {
    const question = state.room.question;
    assert.equal(Object.hasOwn(question, 'answer'), false);
    assert.equal(state.room.reveal, null);
    assert.equal(Object.hasOwn(state.room, 'questionIds'), false);
    assert.equal(JSON.stringify(state).includes('secret_hash'), false);
    assert.equal(JSON.stringify(state).includes('optionOrders'), false);
    const correct = answerBank.get(question.id).answer;
    const wrong = question.options.find(o => o.id !== correct).id;
    const answers = await Promise.all([
      call(`/rooms/${roomCode}/answer`, { method: 'POST', cookie: ada.cookie, data: { round, answerId: correct, correct: false, score: 999999 } }),
      call(`/rooms/${roomCode}/answer`, { method: 'POST', cookie: lin.cookie, data: { round, answerId: wrong, correct: true, score: 999999 } }),
    ]);
    assert.ok(answers.every(r => r.status === 200));
    assert.equal((await call(`/rooms/${roomCode}/answer`, { method: 'POST', cookie: ada.cookie, data: { round, answerId: correct } })).status, 409);
    const revealed = await call(`/rooms/${roomCode}`, { cookie: ada.cookie });
    assert.equal(revealed.result.room.status, 'reveal');
    assert.equal(revealed.result.room.deadline, time + REVEAL_MS, 'All answers reveal immediately rather than waiting twenty seconds');
    assert.equal(revealed.result.room.question.id, question.id);
    assert.equal(revealed.result.room.reveal.correctAnswerId, correct);
    assert.equal(revealed.result.room.reveal.results.find(r => r.playerId === ada.id).correct, true);
    assert.equal(revealed.result.room.reveal.results.find(r => r.playerId === lin.id).points, 0);
    assert.equal(revealed.result.room.players.find(p => p.id === ada.id).score, round * 120);
    time += REVEAL_MS;
    state = (await call(`/rooms/${roomCode}`, { cookie: ada.cookie })).result;
  }
  assert.equal(state.room.status, 'finished');
  assert.equal(state.room.winnerId, ada.id);
  assert.equal(state.me.matches, 1); assert.equal(state.me.wins, 1);
  assert.equal(state.me.shards, 98); assert.ok(state.me.rating > 1000);
  assert.equal(state.me.activeRoomCode, null);
  assert.equal(state.room.rewards.find(r => r.playerId === lin.id).shards, 0, 'No correct answers cannot farm rewards');
  const beforeRepeats = { rating: state.me.rating, shards: state.me.shards, wins: state.me.wins, matches: state.me.matches };
  const concurrentFinish = await Promise.all(Array.from({ length: 8 }, (_, i) => call(`/rooms/${roomCode}`, { cookie: i % 2 ? lin.cookie : ada.cookie })));
  assert.ok(concurrentFinish.every(r => r.status === 200));
  const finalMe = (await call('/me', { cookie: ada.cookie })).result.me;
  for (const field of Object.keys(beforeRepeats)) assert.equal(finalMe[field], beforeRepeats[field]);
  assert.equal((await DB.prepare('SELECT COUNT(*) AS count FROM mp_match_results WHERE room_code = ?').bind(roomCode).first()).count, 2);
  const bought = await call('/gear', { method: 'POST', cookie: ada.cookie, data: { slot: 'power', cost: 0 } });
  assert.equal(bought.status, 200); assert.equal(bought.result.me.shards, 18); assert.equal(bought.result.me.gear.power, 1);
  assert.equal(bought.result.gearDefs.find(d => d.slot === 'power').cost, 160);
  assert.equal((await call('/gear', { method: 'POST', cookie: ada.cookie, data: { slot: 'power' } })).status, 409);
  const board = await call('/leaderboard', { cookie: ada.cookie });
  assert.equal(board.status, 200); assert.equal(board.result.players[0].id, ada.id); assert.equal(board.result.players[0].rank, 1);
  assert.equal(board.result.players.some(p => p.id === outsider.id), false);

  // Independent worker instances read authoritative state from the same durable database.
  const restartApi = createMultiplayerApi({ questions, now: () => time });
  const durable = await call(`/rooms/${roomCode}`, { cookie: ada.cookie, requestApi: restartApi });
  assert.equal(durable.result.room.status, 'finished'); assert.equal(durable.result.me.shards, 18);

  // A missing opponent answer keeps the full clock and is rejected at the deadline.
  const timed = await call('/rooms', { method: 'POST', cookie: ada.cookie, data: { mode: 'quiz', pet } });
  const timedCode = timed.result.room.code;
  await call(`/rooms/${timedCode}/join`, { method: 'POST', cookie: lin.cookie, data: { pet } });
  const timedStart = (await call(`/rooms/${timedCode}/start`, { method: 'POST', cookie: ada.cookie })).result;
  const timedCorrect = answerBank.get(timedStart.room.question.id).answer;
  const singleAnswer = await call(`/rooms/${timedCode}/answer`, { method: 'POST', cookie: ada.cookie, data: { round: 1, answerId: timedCorrect } });
  assert.equal(singleAnswer.result.room.status, 'question'); assert.equal(singleAnswer.result.room.reveal, null);
  assert.equal(singleAnswer.result.room.deadline, time + QUESTION_MS);
  time += QUESTION_MS;
  assert.equal((await call(`/rooms/${timedCode}/answer`, { method: 'POST', cookie: lin.cookie, data: { round: 1, answerId: timedCorrect } })).status, 409);
  const timeoutReveal = (await call(`/rooms/${timedCode}`, { cookie: ada.cookie })).result.room;
  assert.equal(timeoutReveal.status, 'reveal'); assert.equal(timeoutReveal.reveal.results.find(r => r.playerId === lin.id).correct, false);
  assert.equal(timeoutReveal.reveal.results.find(r => r.playerId === lin.id).points, 0);
  await call(`/rooms/${timedCode}/leave`, { method: 'POST', cookie: lin.cookie });

  // A payout failure rolls back its ledger and wallet; a later worker poll retries once.
  const recoveryA = await session('RecoveryA'), recoveryB = await session('RecoveryB');
  const recoveryRoom = (await call('/rooms', { method: 'POST', cookie: recoveryA.cookie, data: { mode: 'quiz', pet } })).result.room.code;
  await call(`/rooms/${recoveryRoom}/join`, { method: 'POST', cookie: recoveryB.cookie, data: { pet } });
  const recoveryStart = (await call(`/rooms/${recoveryRoom}/start`, { method: 'POST', cookie: recoveryA.cookie })).result;
  const recoveryAnswer = answerBank.get(recoveryStart.room.question.id).answer;
  await Promise.all([recoveryA, recoveryB].map(p => call(`/rooms/${recoveryRoom}/answer`, { method: 'POST', cookie: p.cookie, data: { round: 1, answerId: recoveryAnswer } })));
  // Forfeit after the resolved round finishes the room, making payout happen in the same request.
  DB.database.exec("CREATE TEMP TRIGGER fail_settlement BEFORE UPDATE OF matches ON mp_players BEGIN SELECT RAISE(ABORT, 'simulated payout failure'); END");
  const realConsoleError = console.error;
  let failedPayout;
  try { console.error = () => {}; failedPayout = await call(`/rooms/${recoveryRoom}/leave`, { method: 'POST', cookie: recoveryB.cookie }); }
  finally { console.error = realConsoleError; }
  assert.equal(failedPayout.status, 503);
  assert.equal((await DB.prepare('SELECT COUNT(*) AS count FROM mp_match_results WHERE room_code = ?').bind(recoveryRoom).first()).count, 0);
  DB.database.exec('DROP TRIGGER fail_settlement');
  const retries = await Promise.all([recoveryA, recoveryB].map(p => call(`/rooms/${recoveryRoom}`, { cookie: p.cookie, requestApi: restartApi })));
  assert.ok(retries.every(r => r.status === 200));
  assert.equal(retries[0].result.me.matches, 1); assert.equal(retries[0].result.me.shards, 58);
  assert.equal(retries[1].result.me.matches, 1); assert.equal(retries[1].result.me.shards, 0);
  assert.equal((await DB.prepare('SELECT COUNT(*) AS count FROM mp_match_results WHERE room_code = ? AND applied = 1').bind(recoveryRoom).first()).count, 2);

  // Duel attacks resolve simultaneously; guarding and focus have distinct effects.
  const duelist = await session('Duelista'), defender = await session('Obranca');
  const duelCreated = await call('/rooms', { method: 'POST', cookie: duelist.cookie, data: { mode: 'duel', pet } });
  const duelCode = duelCreated.result.room.code;
  assert.equal((await call(`/rooms/${duelCode}/join`, { method: 'POST', cookie: defender.cookie, data: { pet } })).status, 200);
  assert.equal((await call(`/rooms/${duelCode}/join`, { method: 'POST', cookie: outsider.cookie, data: { pet } })).status, 409);
  let duel = (await call(`/rooms/${duelCode}/start`, { method: 'POST', cookie: duelist.cookie })).result;
  const duelCorrect = answerBank.get(duel.room.question.id).answer;
  assert.equal((await call(`/rooms/${duelCode}/answer`, { method: 'POST', cookie: duelist.cookie, data: { round: 1, answerId: duelCorrect, move: 'invalid' } })).status, 400);
  await Promise.all([
    call(`/rooms/${duelCode}/answer`, { method: 'POST', cookie: duelist.cookie, data: { round: 1, answerId: duelCorrect, move: 'strike' } }),
    call(`/rooms/${duelCode}/answer`, { method: 'POST', cookie: defender.cookie, data: { round: 1, answerId: duelCorrect, move: 'guard' } }),
  ]);
  duel = (await call(`/rooms/${duelCode}`, { cookie: duelist.cookie })).result;
  assert.equal(duel.room.reveal.results.find(r => r.playerId === duelist.id).damage, 15);
  assert.equal(duel.room.reveal.results.find(r => r.playerId === defender.id).damage, 12);
  assert.equal(duel.room.players.find(p => p.id === duelist.id).hp, 108);
  assert.equal(duel.room.players.find(p => p.id === defender.id).hp, 105);
  const forfeited = await call(`/rooms/${duelCode}/leave`, { method: 'POST', cookie: defender.cookie });
  assert.equal(forfeited.result.room.status, 'finished'); assert.equal(forfeited.result.room.winnerId, duelist.id);
  assert.equal(forfeited.result.me.shards, 0); assert.equal(forfeited.result.me.activeRoomCode, null);

  // Concurrent room creation/joining cannot enroll one identity in two active rooms.
  const racer = await session('Racer');
  const racingCreate = await Promise.all(['quiz', 'duel'].map(mode => call('/rooms', { method: 'POST', cookie: racer.cookie, data: { mode, pet } })));
  assert.equal(racingCreate.filter(r => r.status === 201).length, 1);
  assert.equal(racingCreate.filter(r => r.status === 409).length, 1);
  const raceRoom = racingCreate.find(r => r.status === 201).result.room.code;
  assert.equal((await call(`/rooms/${raceRoom}/leave`, { method: 'POST', cookie: racer.cookie })).status, 200);
  assert.equal((await call('/me', { cookie: racer.cookie })).result.me.shards, 0, 'Closing a lobby cannot mint rewards');
  const ownerA = await session('OwnerA'), ownerB = await session('OwnerB');
  const lobbyA = (await call('/rooms', { method: 'POST', cookie: ownerA.cookie, data: { mode: 'quiz', pet } })).result.room.code;
  const lobbyB = (await call('/rooms', { method: 'POST', cookie: ownerB.cookie, data: { mode: 'quiz', pet } })).result.room.code;
  const racingJoin = await Promise.all([lobbyA, lobbyB].map(c => call(`/rooms/${c}/join`, { method: 'POST', cookie: racer.cookie, data: { pet } })));
  assert.equal(racingJoin.filter(r => r.status === 200).length, 1);
  assert.equal(racingJoin.filter(r => r.status === 409).length, 1);
  const joinedCode = racingJoin.find(r => r.status === 200).result.room.code;
  const otherCode = joinedCode === lobbyA ? lobbyB : lobbyA;
  const otherOwner = otherCode === lobbyA ? ownerA : ownerB;
  assert.equal((await call(`/rooms/${otherCode}`, { cookie: otherOwner.cookie })).result.room.players.some(p => p.id === racer.id), false);

  // A departed lobby host transfers control; expiration releases membership.
  const joinedOwner = joinedCode === lobbyA ? ownerA : ownerB;
  const transferred = await call(`/rooms/${joinedCode}/leave`, { method: 'POST', cookie: joinedOwner.cookie });
  assert.equal(transferred.result.room.hostId, racer.id);
  time += 3600001;
  assert.ok([404, 410].includes((await call(`/rooms/${joinedCode}`, { cookie: racer.cookie })).status));
  assert.equal((await call('/me', { cookie: racer.cookie })).result.me.activeRoomCode, null);

  // Pure rule coverage: focus bypasses guard, charges a later strike, timeouts grant nothing.
  const bank = prepareQuestions(questions);
  const p1 = { id: 'one', name: 'One', rating: 1000, power: 0, shield: 0, focus: 0 }, p2 = { ...p1, id: 'two', name: 'Two' };
  const pure = createRoom('ABCDEF', 'duel', p1, pet, [...bank.keys()].slice(0, 6), 0);
  pure.players.push(matchPlayer(p2, pet, 0)); startRoom(pure, 0);
  pure.answers = { one: { answerId: bank.get(pure.questionIds[0]).answer, move: 'focus', at: 0 }, two: { answerId: bank.get(pure.questionIds[0]).answer, move: 'guard', at: 0 } };
  advanceRoom(pure, bank, QUESTION_MS);
  assert.equal(pure.reveal.results[0].damage, 18); assert.equal(pure.players[0].charge, 10);
  advanceRoom(pure, bank, QUESTION_MS + REVEAL_MS);
  pure.answers = { one: { answerId: bank.get(pure.questionIds[1]).answer, move: 'strike', at: 24000 } };
  advanceRoom(pure, bank, 44000);
  assert.equal(pure.reveal.results[0].damage, 40); assert.equal(pure.players[0].charge, 0);
  assert.equal(pure.reveal.results[1].correct, false);
  assert.equal(pure.reveal.results[1].points, 0);
  const afk = createRoom('HIJKLM', 'quiz', p1, pet, [...bank.keys()].slice(0, 6), 0);
  afk.players.push(matchPlayer(p2, pet, 0)); startRoom(afk, 0); advanceRoom(afk, bank, 144000);
  assert.equal(afk.status, 'finished'); assert.equal(afk.winnerId, null);
  assert.ok(afk.rewards.every(r => r.shards === 0 && r.ratingDelta <= 0 && !r.winner));
  assert.equal(publicRoom(afk, bank, 144000).question, null);
  finishRoom(afk, 144000);
  assert.ok(afk.rewards.every(r => r.shards === 0));

  console.log('Multiplayer: cookie authentication, origin/input guards, authoritative timed quizzes, duel moves, CAS races, one-room membership, exact-once D1 rewards, server wallet/ranks, worker restart, expiry and AFK reward protection passed.');
} finally { DB.close(); }
