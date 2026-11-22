/** Pure authoritative match rules. Private answers stay on the server. */
export const ROUND_COUNT = 6;
export const QUESTION_MS = 20000;
export const REVEAL_MS = 4000;
export const START_HP = 120;
export const PRESENCE_MS = 45000;
export const ROOM_TTL_MS = 60 * 60 * 1000;
export const GEAR_MAX_LEVEL = 3;
export const GEAR_SLOTS = ['power', 'shield', 'focus'];
export const MOVES = ['strike', 'guard', 'focus'];

export function gearDefinitions(player) {
  const names = { power: 'Silný úder', shield: 'Pevný štít', focus: 'Jasná myseľ' };
  const descriptions = {
    power: 'Každý stupeň pridá 2 body k zásahu pri správnej odpovedi v dueli.',
    shield: 'Každý stupeň zníži prijaté poškodenie o 2 body.',
    focus: 'Každý stupeň pridá 2 body k skóre a 2 body k nabitiu sústredenia.',
  };
  return GEAR_SLOTS.map(slot => ({ slot, name: names[slot], description: descriptions[slot], level: player[slot] || 0, maxLevel: GEAR_MAX_LEVEL, cost: player[slot] >= GEAR_MAX_LEVEL ? null : 80 * 2 ** (player[slot] || 0) }));
}

export function prepareQuestions(questions) {
  if (!Array.isArray(questions)) throw new TypeError('Multiplayer requires a question array.');
  const seen = new Set();
  const normalized = questions.map(q => ({
    id: q.id, subject: q.subject, prompt: q.question || q.prompt,
    options: (q.options || []).map(o => ({ id: o.id, text: o.label || o.text })),
    answer: q.answer, explanation: q.explanation,
  })).filter(q => {
    if (typeof q.id !== 'string' || seen.has(q.id) || typeof q.prompt !== 'string' || typeof q.explanation !== 'string' || typeof q.subject !== 'string' || q.options.length < 2 || q.options.length > 8 || !q.options.every(o => typeof o.id === 'string' && typeof o.text === 'string') || new Set(q.options.map(o => o.id)).size !== q.options.length || !q.options.some(o => o.id === q.answer)) return false;
    seen.add(q.id); return true;
  });
  if (normalized.length < ROUND_COUNT) throw new TypeError('Multiplayer requires six valid, unique questions.');
  return new Map(normalized.map(q => [q.id, q]));
}

export function createRoom(code, mode, host, pet, questionIds, now, optionOrders = {}) {
  return {
    code, mode, status: 'lobby', hostId: host.id, players: [matchPlayer(host, pet, now)],
    round: 0, totalRounds: ROUND_COUNT, questionIds, optionOrders, deadline: null, reveal: null,
    answers: {}, winnerId: null, rewards: [], createdAt: now, finishedAt: null,
  };
}

export function matchPlayer(player, pet, now) {
  return { id: player.id, name: player.name, pet, score: 0, hp: START_HP, correctCount: 0, charge: 0, rating: player.rating, gear: { power: player.power || 0, shield: player.shield || 0, focus: player.focus || 0 }, seenAt: now, forfeited: false };
}

export function startRoom(room, now) {
  room.status = 'question'; room.round = 1; room.deadline = now + QUESTION_MS; room.answers = {}; room.reveal = null;
}

export function finishRoom(room, at) {
  room.status = 'finished'; room.deadline = null; room.finishedAt = at;
  const eligible = room.players.filter(p => !p.forfeited && p.correctCount > 0);
  const sort = room.mode === 'duel' ? (a, b) => b.hp - a.hp || b.score - a.score : (a, b) => b.score - a.score;
  const ordered = [...eligible].sort(sort);
  room.winnerId = ordered.length && (ordered.length === 1 || sort(ordered[0], ordered[1]) !== 0) ? ordered[0].id : null;
  room.rewards = room.players.map(p => {
    const opponents = room.players.filter(other => other.id !== p.id);
    let outcome = 0, expected = 0;
    for (const other of opponents) {
      const result = p.forfeited ? -1 : other.forfeited ? 1 : room.mode === 'duel' ? p.hp - other.hp || p.score - other.score : p.score - other.score;
      outcome += result > 0 ? 1 : result === 0 ? .5 : 0;
      expected += 1 / (1 + 10 ** ((other.rating - p.rating) / 400));
    }
    const computedDelta = opponents.length ? Math.round(24 * (outcome - expected) / opponents.length) : 0;
    const ratingDelta = p.correctCount > 0 ? computedDelta : Math.min(0, computedDelta);
    const winner = room.winnerId === p.id;
    return { playerId: p.id, ratingDelta, shards: p.forfeited || p.correctCount === 0 ? 0 : 15 + p.correctCount * 8 + (winner ? 35 : 0), winner };
  });
}

export function resolveRound(room, questions, resolvedAt = null) {
  const q = questions.get(room.questionIds[room.round - 1]);
  const openedAt = room.deadline - QUESTION_MS;
  const results = room.players.map(player => {
    const answer = room.answers[player.id];
    const correct = !player.forfeited && answer?.answerId === q.answer;
    const move = answer?.move || 'strike';
    const points = correct ? 100 + Math.max(0, Math.min(20, 20 - Math.floor((answer.at - openedAt) / 1000))) + player.gear.focus * 2 : 0;
    player.score += points;
    if (correct) player.correctCount++;
    let rawDamage = 0;
    if (room.mode === 'duel' && correct) {
      rawDamage = (move === 'strike' ? 30 + player.charge : move === 'guard' ? 12 : 18) + player.gear.power * 2;
      if (move === 'focus') player.charge = Math.min(24, player.charge + 10 + player.gear.focus * 2);
      else if (move === 'strike') player.charge = 0;
    }
    return { playerId: player.id, correct, points, damage: 0, move, rawDamage };
  });
  // Compute both attacks before changing either health pool.
  if (room.mode === 'duel') {
    for (const result of results) {
      const defender = room.players.find(p => p.id !== result.playerId);
      const defense = results.find(r => r.playerId === defender?.id);
      if (!defender) continue;
      const guarded = defense?.correct && defense.move === 'guard';
      const reduced = guarded && result.move !== 'focus' ? Math.round(result.rawDamage * .5) : result.rawDamage;
      result.damage = result.correct ? Math.max(0, reduced - defender.gear.shield * 2) : 0;
    }
    for (const result of results) {
      const defender = room.players.find(p => p.id !== result.playerId);
      if (defender) defender.hp = Math.max(0, defender.hp - result.damage);
    }
  }
  room.reveal = { correctAnswerId: q.answer, explanation: q.explanation, results: results.map(({ rawDamage, ...result }) => result) };
  room.status = 'reveal'; room.deadline = (resolvedAt ?? room.deadline) + REVEAL_MS;
}

export function advanceRoom(room, questions, now) {
  // A delayed poll catches up along the original timeline; it cannot grant extra time.
  for (let transitions = 0; transitions < ROUND_COUNT * 2 + 2 && room.deadline !== null && now >= room.deadline; transitions++) {
    if (room.status === 'question') resolveRound(room, questions);
    else if (room.status === 'reveal') {
      if (room.round >= ROUND_COUNT || (room.mode === 'duel' && room.players.some(p => p.hp <= 0))) { finishRoom(room, room.deadline); break; }
      room.round++; room.status = 'question'; room.deadline += QUESTION_MS; room.answers = {}; room.reveal = null;
    } else break;
  }
  return room;
}

export function publicRoom(room, questions, now) {
  const q = room.status === 'question' || room.status === 'reveal' ? questions.get(room.questionIds[room.round - 1]) : null;
  return {
    code: room.code, mode: room.mode, status: room.status, hostId: room.hostId,
    players: room.players.map(p => ({ id: p.id, name: p.name, pet: p.pet, score: p.score, hp: p.hp, answered: Boolean(room.answers[p.id]), connected: !p.forfeited && now - p.seenAt < PRESENCE_MS })),
    round: room.round, totalRounds: ROUND_COUNT,
    question: q ? { id: q.id, prompt: q.prompt, options: (room.optionOrders?.[q.id] || q.options.map(o => o.id)).map(id => q.options.find(o => o.id === id)), subject: q.subject } : null,
    deadline: room.deadline, serverNow: now, reveal: room.reveal, winnerId: room.winnerId,
    rewards: room.status === 'finished' ? room.rewards : [],
  };
}
