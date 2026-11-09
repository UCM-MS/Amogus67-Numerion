import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// The app serves native ES modules; data import also works without changing its package.json.
const source = await readFile(new URL('../public/academy-engine.js', import.meta.url), 'utf8');
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
assert.equal(engine.answerQuiz(q.id, q.answer).event, 'quiz-already-answered');
assert.equal(engine.getState().player.knowledge, 1);
assert.equal(engine.getState().player.coins, beforeWrong + 50);
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
assert.equal(engine.getDefinitions().questions.length, 36);
assert.equal(engine.getDefinitions().questions.filter(item => engine.getState().progress.solved.includes(item.id)).length, 24);
assert.deepEqual(engine.getState().mastery.math, { solved: 8, total: 8 });
assert.deepEqual(engine.getState().mastery.astral, { solved: 0, total: 12 });
for (let i = 0; i < 12; i++) {
  const next = engine.getQuiz('astral');
  assert.equal(engine.answerQuiz(next.id, next.answer).firstSolve, true);
}
assert.equal(engine.getState().player.knowledge, 36);
assert.deepEqual(engine.getState().mastery.astral, { solved: 12, total: 12 });
const practice = engine.getQuiz('math');
const beforePractice = engine.getState().player.coins;
assert.equal(engine.answerQuiz(practice.id, practice.answer).firstSolve, false);
assert.equal(engine.getState().player.coins, beforePractice + 10);
assert.equal(engine.getState().player.knowledge, 36);
assert.equal(engine.answerQuiz(practice.id, practice.answer).ok, false);
assert.equal(engine.getState().player.coins, beforePractice + 10);
assert.ok(engine.getState().stats.streak > 0);
assert.equal(engine.getState().stats.bestStreak, engine.getState().stats.streak);
const streakQuestion = engine.getQuiz('nature');
const streakWrong = streakQuestion.options.find(option => option.id !== streakQuestion.answer).id;
const bestStreak = engine.getState().stats.bestStreak;
assert.equal(engine.answerQuiz(streakQuestion.id, streakWrong).correct, false);
assert.equal(engine.getState().stats.streak, 0);
assert.equal(engine.getState().stats.bestStreak, bestStreak);
assert.equal(engine.getState().player.xpInLevel, engine.getState().player.xp % 100);
assert.equal(engine.getState().player.xpToNext, 100 - engine.getState().player.xpInLevel);
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
assert.equal(engine.getState().player.knowledge, 36);
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
assert.equal(restored.getState().player.knowledge, 36);
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

// Exploration has exactly twelve persistent, single-use rewards.
const exploreStorage = new MemoryStorage();
const explore = new AcademyEngine(exploreStorage);
assert.equal(explore.getQuiz('astral'), null);
assert.equal(explore.answerQuiz('astral-01', 'a').ok, false);
assert.equal(explore.collectCrystal('crystal-00').ok, false);
assert.equal(explore.claimQuest('crystal-scout').ok, false);
assert.equal(explore.claimQuest('missing').ok, false);
assert.equal(explore.collectCrystal('crystal-01').ok, true);
assert.equal(explore.getState().player.coins, 165);
assert.equal(explore.getState().player.xp, 10);
assert.equal(explore.getState().player.knowledge, 0);
assert.equal(explore.collectCrystal('crystal-01').ok, false);
assert.equal(explore.getState().player.coins, 165);
for (let i = 2; i <= 12; i++) assert.equal(explore.collectCrystal(`crystal-${String(i).padStart(2, '0')}`).ok, true);
assert.equal(explore.getState().progress.collected.length, 12);
assert.equal(explore.getState().player.coins, 330);
assert.equal(explore.getState().player.xp, 120);
assert.equal(explore.getState().player.level, 2);
assert.equal(explore.getDefinitions().expeditions.length, 6);
assert.equal(explore.getDefinitions().expeditions.find(def => def.id === 'academy-explorer').complete, true);
for (const id of ['crystal-scout', 'academy-explorer']) {
  const def = explore.getDefinitions().expeditions.find(item => item.id === id);
  const before = explore.getState();
  assert.equal(explore.claimQuest(id).ok, true);
  assert.equal(explore.getState().player.coins, before.player.coins + def.rewardCoins);
  assert.equal(explore.getState().player.xp, before.player.xp + def.rewardXp);
  assert.equal(explore.getDefinitions().expeditions.find(item => item.id === id).claimed, true);
  const after = explore.getState().player.coins;
  assert.equal(explore.claimQuest(id).ok, false);
  assert.equal(explore.getState().player.coins, after);
}
const exploreRestored = new AcademyEngine(exploreStorage);
assert.equal(exploreRestored.collectCrystal('crystal-12').ok, false);
assert.equal(exploreRestored.claimQuest('academy-explorer').ok, false);
assert.equal(exploreRestored.getState().progress.collected.length, 12);
exploreRestored.reset();
assert.equal(exploreRestored.getState().progress.collected.length, 0);
assert.equal(exploreRestored.getState().progress.claimedQuests.length, 0);

// Training spends coins atomically, grows original income, and survives save migration.
const trainStorage = new MemoryStorage();
const train = new AcademyEngine(trainStorage);
const funded = JSON.parse(train.exportSave());
funded.player.coins = 10000;
assert.equal(train.importSave(JSON.stringify(funded)).ok, true);
assert.equal(train.buyEgg('egg-basic').ok, true);
const trainedId = train.hatchEgg().pet.id;
const originalPet = train.getState().pets[0];
assert.equal(originalPet.level, 1);
assert.equal(originalPet.baseIncome, originalPet.income);
assert.equal(train.trainPet('fake-pet').ok, false);
let previousCost = 0;
for (let level = 2; level <= 5; level++) {
  const before = train.getState();
  const pet = before.pets[0];
  assert.ok(pet.trainCost > previousCost);
  previousCost = pet.trainCost;
  assert.equal(train.trainPet(trainedId).ok, true);
  const after = train.getState();
  assert.equal(after.player.coins, before.player.coins - pet.trainCost);
  assert.equal(after.pets[0].level, level);
  assert.ok(Math.abs(after.pets[0].income - originalPet.baseIncome * (1 + (level - 1) * 0.2)) < 0.011);
  assert.ok(after.incomePerSecond > before.incomePerSecond);
}
assert.equal(train.getState().pets[0].trainCost, null);
const maxedCoins = train.getState().player.coins;
assert.equal(train.trainPet(trainedId).ok, false);
assert.equal(train.getState().player.coins, maxedCoins);
const trainRestored = new AcademyEngine(trainStorage);
assert.equal(trainRestored.getState().pets[0].level, 5);
assert.equal(trainRestored.getState().incomePerSecond, train.getState().incomePerSecond);
assert.equal(trainRestored.equipPet(null).ok, true);
assert.equal(new AcademyEngine(trainStorage).getState().equippedPet, null);
const oldSave = JSON.parse(train.exportSave());
delete oldSave.pets[0].level;
delete oldSave.progress.collected;
delete oldSave.progress.claimedQuests;
delete oldSave.settings.graphics;
delete oldSave.stats.streak;
delete oldSave.stats.parkourRuns;
oldSave.player.coins = 0;
trainStorage.setItem(key, JSON.stringify(oldSave));
const migrated = new AcademyEngine(trainStorage);
assert.equal(migrated.getState().pets[0].level, 1);
assert.equal(migrated.getState().pets[0].income, originalPet.income);
assert.equal(migrated.getState().settings.graphics, 'auto');
assert.deepEqual(migrated.getState().progress.collected, []);
assert.equal(migrated.trainPet(trainedId).ok, false);
assert.equal(migrated.getState().pets[0].level, 1);
assert.equal(migrated.getState().player.coins, 0);

// Parkour uses a real-time cooldown, including after a reload, rather than tick() time.
const parkourStorage = new MemoryStorage();
const realNow = Date.now;
let simulatedNow = 1900000000000;
Date.now = () => simulatedNow;
try {
  const parkour = new AcademyEngine(parkourStorage);
  for (const invalid of [0, -1, Number.NaN, Number.POSITIVE_INFINITY, '12', 3601]) assert.equal(parkour.finishParkour(invalid).ok, false);
  assert.equal(parkour.getState().stats.parkourRuns, 0);
  const first = parkour.finishParkour(25.555);
  assert.equal(first.ok, true);
  assert.equal(first.coins, 100);
  assert.equal(first.xp, 40);
  assert.equal(parkour.getState().stats.bestParkourTime, 25.56);
  assert.equal(parkour.getState().parkourCooldownSeconds, 30);
  assert.equal(parkour.finishParkour(10).ok, false);
  parkour.tick(3000);
  assert.equal(parkour.finishParkour(10).ok, false);
  let resumed = new AcademyEngine(parkourStorage);
  assert.equal(resumed.finishParkour(10).ok, false);
  simulatedNow += 29999;
  assert.equal(resumed.finishParkour(10).ok, false);
  simulatedNow++;
  const second = resumed.finishParkour(20);
  assert.equal(second.ok, true);
  assert.equal(second.firstRun, false);
  assert.equal(second.coins, 20);
  assert.equal(second.xp, 5);
  assert.equal(resumed.getState().stats.parkourRuns, 2);
  assert.equal(resumed.getState().stats.bestParkourTime, 20);
  assert.equal(resumed.getDefinitions().expeditions.find(def => def.id === 'sky-runner').complete, true);
  assert.equal(resumed.claimQuest('sky-runner').ok, true);
  simulatedNow += 30000;
  assert.equal(resumed.finishParkour(30).ok, true);
  assert.equal(resumed.getState().stats.bestParkourTime, 20);
  resumed = new AcademyEngine(parkourStorage);
  assert.equal(resumed.getState().stats.parkourRuns, 3);
  assert.equal(resumed.claimQuest('sky-runner').ok, false);
} finally { Date.now = realNow; }

// Backups preserve progress and reject invalid documents without resetting the game.
const backupStorage = new MemoryStorage();
const backup = new AcademyEngine(backupStorage);
assert.equal(backup.setName('Mira').ok, true);
assert.equal(backup.setSetting('graphics', 'low').ok, true);
assert.equal(backup.setSetting('graphics', 'turbo').ok, false);
assert.equal(backup.setSetting('sound', 'yes').ok, false);
assert.equal(backup.collectCrystal('crystal-03').ok, true);
const backupText = backup.exportSave();
assert.equal(typeof backupText, 'string');
const receiver = new AcademyEngine(backupStorage);
assert.equal(receiver.importSave(backupText).ok, true);
assert.equal(receiver.getState().player.name, 'Mira');
assert.equal(receiver.getState().settings.graphics, 'low');
assert.deepEqual(receiver.getState().progress.collected, ['crystal-03']);
assert.equal(receiver.save().ok, true);
const beforeBadImport = receiver.exportSave();
const badVersion = JSON.parse(backupText);
badVersion.version = 3;
const nonFinite = backupText.replace('"coins": 165', '"coins": 1e999');
for (const bad of ['{bad-json', 'null', '[]', '{}', JSON.stringify(badVersion), nonFinite, backupText.replace('"pets": []', '"pets": null')]) {
  assert.equal(receiver.importSave(bad).ok, false);
  assert.deepEqual(JSON.parse(receiver.exportSave()).player, JSON.parse(beforeBadImport).player);
  assert.deepEqual(receiver.getState().progress.collected, ['crystal-03']);
}
assert.equal(denied.save().ok, false);
assert.equal(typeof denied.exportSave(), 'string');
assert.equal(denied.importSave(backupText).ok, true);
assert.equal(denied.getState().player.name, 'Mira');

// Loaded rewards and training values are allowlisted and clamped.
const poisoned = JSON.parse(train.exportSave());
poisoned.pets[0].level = 1000;
poisoned.pets[0].income = 999999;
poisoned.pets.push({ id: 'pet-999', rarity: '__proto__', name: 'Fake', kind: 'robot' });
poisoned.progress.collected = ['crystal-01', 'crystal-01', 'invalid'];
poisoned.progress.claimedQuests = ['hatch-friend', 'hatch-friend', 'invalid'];
poisoned.stats.streak = 999999;
poisoned.stats.bestStreak = 999999;
trainStorage.setItem(key, JSON.stringify(poisoned));
const guarded = new AcademyEngine(trainStorage).getState();
assert.equal(guarded.pets.length, 1);
assert.equal(guarded.pets[0].level, 5);
assert.equal(guarded.pets[0].baseIncome, originalPet.income);
assert.deepEqual(guarded.progress.collected, ['crystal-01']);
assert.deepEqual(guarded.progress.claimedQuests, ['hatch-friend']);
assert.ok(guarded.stats.streak <= guarded.stats.correct);

// Every expedition metric is sourced from the real inventory and solved lessons.
const contractStorage = new MemoryStorage();
const contract = new AcademyEngine(contractStorage);
const contractSeed = JSON.parse(contract.exportSave());
const petDefinition = contract.getDefinitions().petPools.common[0];
contractSeed.player.coins = 2000;
contractSeed.pets = Array.from({ length: 6 }, (_, i) => ({ id: `pet-${i + 1}`, rarity: 'common', ...petDefinition, level: 1 }));
contractSeed.progress.solved = contract.getDefinitions().questions.filter(q => q.subject !== 'astral').slice(0, 12).map(q => q.id);
assert.equal(contract.importSave(JSON.stringify(contractSeed)).ok, true);
for (const id of ['hatch-friend', 'lesson-trail', 'pet-club']) {
  const def = contract.getDefinitions().expeditions.find(item => item.id === id);
  assert.equal(def.complete, true);
  const before = contract.getState();
  assert.equal(contract.claimQuest(id).ok, true);
  assert.equal(contract.getState().player.coins, before.player.coins + def.rewardCoins);
  assert.equal(contract.getState().player.xp, before.player.xp + def.rewardXp);
  assert.equal(contract.claimQuest(id).ok, false);
}
assert.equal(contract.collectCrystal('crystal-05').ok, true);
assert.equal(contract.prestige().ok, true);
assert.deepEqual(contract.getState().progress.collected, ['crystal-05']);
assert.equal(contract.getDefinitions().expeditions.find(def => def.id === 'pet-club').claimed, true);
assert.equal(contract.getDefinitions().expeditions.find(def => def.id === 'pet-club').complete, true);
assert.equal(contract.claimQuest('pet-club').ok, false);
const contractReloaded = new AcademyEngine(contractStorage);
assert.equal(contractReloaded.getState().pets.length, 0);
assert.deepEqual(contractReloaded.getState().progress.collected, ['crystal-05']);
assert.equal(contractReloaded.getState().player.knowledge, 12);
assert.equal(contractReloaded.claimQuest('hatch-friend').ok, false);

console.log('AcademyEngine: 36 quizzes, duplicate-answer guard, mastery/streak, twelve persistent crystals, six expeditions, pet training, real-time parkour cooldown, graphics settings, backup import/export, old-save migration and storage denial passed.');
