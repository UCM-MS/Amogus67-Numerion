import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// The app serves native ES modules; data import also works without changing its package.json.
const source = await readFile(new URL('../dist/academy-engine.js', import.meta.url), 'utf8');
const { AcademyEngine } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
class MemoryStorage {
  constructor() { this.data = new Map(); }
  getItem(key) { return this.data.get(key) || null; }
  setItem(key, value) { this.data.set(key, value); }
}
const storage = new MemoryStorage();
const engine = new AcademyEngine(storage);
assert.equal(engine.getState().player.coins, 150);
assert.equal(engine.getState().capacity, 3);
assert.equal(engine.getQuiz('unknown'), null);
assert.equal(engine.buyEgg('egg-science').ok, false);
assert.equal(engine.buyEgg('egg-basic').ok, true);
assert.equal(engine.getState().player.coins, 110);
assert.equal(engine.buyEgg('egg-basic').ok, true);
assert.equal(engine.buyEgg('egg-basic').ok, true);
assert.equal(engine.getState().eggs.length, 3);
const blockedState = engine.getState();
assert.equal(engine.buyEgg('egg-basic').ok, false);
assert.equal(engine.getState().player.coins, blockedState.player.coins);
const hatch = engine.hatchEgg();
assert.equal(hatch.ok, true);
assert.equal(engine.getState().eggs.length, 2);
assert.equal(engine.getState().pets.length, 1);
assert.equal(engine.getState().stats.hatched, 1);
assert.equal(engine.getState().equippedPet, hatch.pet.id);
assert.ok(engine.getState().incomePerSecond > 0.5);

const q = engine.getQuiz('math');
const wrong = q.options.find(option => option.id !== q.answer).id;
const beforeWrong = engine.getState().player.coins;
assert.equal(engine.answerQuiz(q.id, wrong).correct, false);
assert.equal(engine.getState().player.coins, beforeWrong);
assert.equal(engine.getState().stats.wrong, 1);
assert.equal(engine.getQuiz('math').id, q.id);
assert.equal(engine.answerQuiz(q.id, q.answer).firstSolve, true);
assert.equal(engine.getState().player.knowledge, 1);
assert.equal(engine.getState().player.coins, beforeWrong + 50);
assert.equal(engine.answerQuiz(q.id, q.answer).firstSolve, false);
assert.equal(engine.getState().player.knowledge, 1);
assert.equal(engine.getState().player.coins, beforeWrong + 60);
assert.equal(engine.answerQuiz(q.id, 'invalid').ok, false);

for (const subject of ['math', 'science', 'nature']) {
  for (let i = 0; i < 8; i++) {
    const next = engine.getQuiz(subject);
    assert.equal(engine.answerQuiz(next.id, next.answer).ok, true);
  }
}
assert.equal(engine.getState().player.knowledge, 24);
assert.deepEqual(engine.getState().progress.unlocked, ['meadow', 'science', 'astral']);
assert.equal(engine.getDefinitions().eggDefs.find(def => def.id === 'egg-cosmic').available, true);
assert.equal(engine.getDefinitions().quests.find(def => def.id === 'academy-master').complete, true);
assert.equal(engine.getDefinitions().questions.length, 24);
assert.equal(engine.getDefinitions().questions.filter(item => engine.getState().progress.solved.includes(item.id)).length, 24);
const speed = engine.getState().movementMultiplier;
assert.equal(engine.upgrade('speed').ok, true);
assert.ok(engine.getState().movementMultiplier > speed);
assert.equal(engine.upgrade('capacity').ok, true);
assert.equal(engine.getState().capacity, 5);
const income = engine.getState().incomePerSecond;
assert.equal(engine.upgrade('income').ok, true);
assert.ok(engine.getState().incomePerSecond > income);
assert.equal(engine.equipPet('nonexistent').ok, false);
assert.equal(engine.equipPet(null).ok, true);
assert.equal(engine.equipPet(hatch.pet.id).ok, true);

let tickEvents = 0;
const unsubscribe = engine.subscribe((state, result) => { if (result.event === 'tick') tickEvents++; });
const beforeTicks = engine.getState().player.coins;
for (let i = 0; i < 60; i++) engine.tick(1 / 60);
assert.ok(tickEvents <= 1);
assert.ok(engine.getState().player.coins > beforeTicks);
const beforeHuge = engine.getState().player.coins;
const tick = engine.tick(100000);
assert.ok(tick.earned <= engine.getState().incomePerSecond * 5 + 0.1);
assert.ok(engine.getState().player.coins < beforeHuge + 100);
engine.tick(Number.NaN);
assert.ok(Number.isFinite(engine.getState().player.coins));
unsubscribe();

while (engine.getState().eggs.length) engine.hatchEgg();
while (engine.getState().pets.length < 6) { assert.equal(engine.buyEgg('egg-basic').ok, true); assert.equal(engine.hatchEgg().ok, true); }
assert.equal(engine.prestige().ok, true);
assert.equal(engine.getState().pets.length, 0);
assert.equal(engine.getState().eggs.length, 0);
assert.equal(engine.getState().player.knowledge, 24);
assert.equal(engine.getState().prestigeCount, 1);
assert.equal(engine.getState().player.coins, 150);
assert.equal(engine.getState().incomePerSecond, 0.6);
assert.equal(engine.prestige().ok, false);
assert.equal(engine.setName('  Ada\u0000  ').ok, true);
assert.equal(engine.getState().player.name, 'Ada');
assert.equal(engine.setSetting('sound', true).ok, true);
const restored = new AcademyEngine(storage);
assert.equal(restored.getState().player.name, 'Ada');
assert.equal(restored.getState().settings.sound, true);
assert.equal(restored.getState().prestigeCount, 1);
assert.equal(restored.getState().player.knowledge, 24);
const snapshot = restored.getState();
snapshot.player.coins = 9e99;
assert.equal(restored.getState().player.coins, 150);
restored.reset();
assert.equal(restored.getState().player.knowledge, 0);
assert.equal(restored.getState().prestigeCount, 0);
assert.equal(restored.getState().player.coins, 150);

const key = engine.getDefinitions().saveKey;
storage.setItem(key, '{broken-json');
assert.equal(new AcademyEngine(storage).getState().player.coins, 150);
storage.setItem(key, JSON.stringify({ version: 2, player: { coins: -100, xp: 'oops', knowledge: 99 }, progress: { solved: ['fake', 'math-01', 'math-01'] }, eggs: [{ id: 'egg-1', type: 'fake' }], upgrades: { capacity: 999 } }));
const sanitized = new AcademyEngine(storage).getState();
assert.equal(sanitized.player.coins, 0);
assert.equal(sanitized.player.knowledge, 1);
assert.equal(sanitized.player.level, 1);
assert.equal(sanitized.eggs.length, 0);
assert.equal(sanitized.upgrades.capacity, 5);
const denied = new AcademyEngine({ getItem() { throw Error('denied'); }, setItem() { throw Error('denied'); } });
assert.equal(denied.buyEgg('egg-basic').ok, true);
assert.equal(denied.hatchEgg().ok, true);
assert.equal(denied.getState().pets.length, 1);
assert.equal(new AcademyEngine(null).buyEgg('egg-basic').ok, true);
console.log('AcademyEngine: purchases, inventory, hatching, 24 quizzes, repeat protection, upgrades, passive income, prestige, save recovery and storage denial passed.');
