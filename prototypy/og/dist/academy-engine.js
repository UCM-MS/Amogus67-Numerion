/** Persistent, dependency-free rules for the educational 3D academy. */
const SAVE_KEY = 'lumen-academy-3d-v2';
const clone = value => JSON.parse(JSON.stringify(value));
const number = (value, fallback = 0, max = 1e12) => Number.isFinite(value) ? Math.max(0, Math.min(max, value)) : fallback;
const integer = (value, fallback = 0, max = 1e9) => Math.floor(number(value, fallback, max));

const EGGS = [
  { id: 'egg-basic', name: 'Lúčne vajce', price: 40, knowledge: 0, color: '#81e69e', subject: 'math', description: 'Malý priateľ pre prvé dobrodružstvo. Bežné, nezvyčajné a zriedkavé zvieratká.' },
  { id: 'egg-science', name: 'Vedecké vajce', price: 180, knowledge: 3, color: '#68ccff', subject: 'science', description: 'Odomkni 3 nové poznatky. Vyššia šanca na zriedkavého a epického priateľa.' },
  { id: 'egg-cosmic', name: 'Kozmické vajce', price: 600, knowledge: 8, color: '#ca9eff', subject: 'nature', description: 'Odomkni 8 nových poznatkov. Zriedkaví, epickí a legendárni priatelia.' }
];
const UPGRADES = [
  { id: 'speed', name: 'Rýchle tenisky', description: 'Každý stupeň zvýši rýchlosť pohybu o 12 %.', baseCost: 100, maxLevel: 5, effect: 0.12 },
  { id: 'capacity', name: 'Väčší batoh', description: 'Každý stupeň pridá dve miesta na vajíčka.', baseCost: 120, maxLevel: 5, effect: 2 },
  { id: 'income', name: 'Šťastná základňa', description: 'Každý stupeň zvýši celý príjem o 25 %.', baseCost: 150, maxLevel: 5, effect: 0.25 }
];
const PETS = {
  common: [
    { name: 'Mochi', kind: 'bunny', color: '#fff3c4', income: 1 },
    { name: 'Pip', kind: 'cat', color: '#a4edbd', income: 1.2 },
    { name: 'Koko', kind: 'fox', color: '#ffc88d', income: 1.4 }
  ],
  uncommon: [
    { name: 'Luna', kind: 'cat', color: '#a8dcff', income: 2 },
    { name: 'Kiwi', kind: 'bunny', color: '#bafa84', income: 2.2 },
    { name: 'Oli', kind: 'owl', color: '#b6a8ff', income: 2.5 }
  ],
  rare: [
    { name: 'Spark', kind: 'fox', color: '#ffac77', income: 3.5 },
    { name: 'Pixel', kind: 'robot', color: '#75f2ec', income: 4 },
    { name: 'Aurora', kind: 'owl', color: '#eca1f3', income: 4.5 }
  ],
  epic: [
    { name: 'Orion', kind: 'dragon', color: '#b397ff', income: 7 },
    { name: 'Comet', kind: 'fox', color: '#ffc75a', income: 8 },
    { name: 'Cosmo', kind: 'robot', color: '#81a5ff', income: 9 }
  ],
  legendary: [
    { name: 'Nova', kind: 'dragon', color: '#ffda72', income: 12 },
    { name: 'Stella', kind: 'owl', color: '#ff9bce', income: 11 }
  ]
};
const POOLS = {
  'egg-basic': [{ rarity: 'common', chance: 0.75 }, { rarity: 'uncommon', chance: 0.22 }, { rarity: 'rare', chance: 0.03 }],
  'egg-science': [{ rarity: 'common', chance: 0.2 }, { rarity: 'uncommon', chance: 0.45 }, { rarity: 'rare', chance: 0.3 }, { rarity: 'epic', chance: 0.05 }],
  'egg-cosmic': [{ rarity: 'rare', chance: 0.35 }, { rarity: 'epic', chance: 0.5 }, { rarity: 'legendary', chance: 0.15 }]
};

function question(id, subject, title, prompt, labels, answer, explanation, hint) {
  return { id, subject, title, question: prompt, options: labels.map((label, i) => ({ id: String.fromCharCode(97 + i), label })), answer, explanation, hint };
}
const QUESTIONS = [
  question('math-01', 'math', 'Sčítanie', 'Koľko je 7 + 8?', ['15', '13', '16'], 'a', '7 + 8 = 15. K siedmim pridaj tri a potom zvyšných päť.', 'Doplň 7 do desiatky a pripočítaj zvyšok.'),
  question('math-02', 'math', 'Násobenie', 'Koľko je 6 × 7?', ['36', '42', '49'], 'b', 'Šesť skupín po sedem je 42: 7 + 7 + 7 + 7 + 7 + 7.', 'Vieš, že 5 × 7 = 35. Pridaj ešte jednu sedmičku.'),
  question('math-03', 'math', 'Zlomky', 'Koľko sú tri štvrtiny z 20?', ['5', '10', '15'], 'c', 'Jedna štvrtina z 20 je 5. Tri štvrtiny sú 3 × 5 = 15.', 'Najprv rozdeľ 20 na štyri rovnaké časti.'),
  question('math-04', 'math', 'Percentá', 'Koľko je 25 % z 80?', ['20', '25', '40'], 'a', '25 % je jedna štvrtina. 80 ÷ 4 = 20.', '25 zo 100 je jedna zo štyroch rovnakých častí.'),
  question('math-05', 'math', 'Geometria', 'Koľko strán má každý trojuholník?', ['4', '3', '5'], 'b', 'Trojuholník má tri strany a tri vrcholy.', 'Pomôže ti jeho názov: troj-uholník.'),
  question('math-06', 'math', 'Delenie', '24 jabĺk rozdelíš rovnako medzi 6 kamarátov. Koľko dostane každý?', ['6', '3', '4'], 'c', '24 ÷ 6 = 4. Šesť kamarátov po štyri jablká dostane spolu 24.', 'Hľadaj číslo, ktoré po vynásobení šiestimi dá 24.'),
  question('math-07', 'math', 'Desatinné čísla', 'Ktorý zlomok sa rovná číslu 0,5?', ['1/2', '1/5', '5/2'], 'a', '0,5 predstavuje polovicu celku, teda 1/2.', 'Dve takéto časti spolu vytvoria 1.'),
  question('math-08', 'math', 'Obvod', 'Štvorec má stranu dlhú 5 cm. Aký je jeho obvod?', ['10 cm', '20 cm', '25 cm'], 'b', 'Štvorec má štyri rovnaké strany. Obvod je 4 × 5 = 20 cm.', 'Obvod získame sčítaním dĺžok všetkých strán.'),
  question('science-01', 'science', 'Sila', 'V akej jednotke meriame silu?', ['Kilogram (kg)', 'Meter (m)', 'Newton (N)'], 'c', 'Jednotkou sily je newton, označený N. Kilogram je jednotkou hmotnosti.', 'Jednotka je pomenovaná po vedcovi Isaacovi Newtonovi.'),
  question('science-02', 'science', 'Energia', 'Odkiaľ prichádza väčšina energie, ktorá poháňa počasie na Zemi?', ['Zo Slnka', 'Z Mesiaca', 'Zo satelitov'], 'a', 'Slnko zohrieva zemský povrch a poháňa kolobeh vody aj prúdenie vzduchu.', 'Hľadaj zdroj, ktorý nám poskytuje svetlo a teplo.'),
  question('science-03', 'science', 'Voda', 'Pri akej teplote vrie čistá voda pri bežnom atmosférickom tlaku?', ['0 °C', '100 °C', '50 °C'], 'b', 'Pri bežnom atmosférickom tlaku čistá voda vrie pri 100 °C. Pri inom tlaku sa teplota varu mení.', '0 °C je bod tuhnutia; var nastáva pri vyššej teplote.'),
  question('science-04', 'science', 'Elektrický obvod', 'Aký obvod potrebuje žiarovka pripojená k funkčnej batérii, aby svietila?', ['Prerušený', 'Bez vodičov', 'Uzavretý'], 'c', 'Elektrický prúd môže tiecť v uzavretom obvode od jedného pólu batérie k druhému.', 'Prúd potrebuje súvislú cestu.'),
  question('science-05', 'science', 'Gravitácia', 'Prečo pustená lopta padá smerom k Zemi?', ['Pôsobí na ňu gravitácia', 'Tlačí ju mesačné svetlo', 'Prestane mať hmotnosť'], 'a', 'Zem priťahuje predmety gravitačnou silou. Tá spôsobí, že pustená lopta padá.', 'Tá istá sila udržiava aj Mesiac na obežnej dráhe.'),
  question('science-06', 'science', 'Svetlo', 'Kde sa svetlo šíri najrýchlejšie?', ['Vo vode', 'Vo vákuu', 'V skle'], 'b', 'Svetlo sa najrýchlejšie šíri vo vákuu. Vo vode alebo v skle je jeho rýchlosť menšia.', 'Vákuum je priestor bez hmotného prostredia.'),
  question('science-07', 'science', 'Obnoviteľná energia', 'Ktorý z týchto zdrojov energie je obnoviteľný?', ['Uhlie', 'Ropa', 'Slnečné žiarenie'], 'c', 'Slnečné žiarenie je obnoviteľný zdroj. Uhlie a ropa sa tvoria veľmi pomaly a ich zásoby sú obmedzené.', 'Hľadaj zdroj, ktorý sa prirodzene neustále dopĺňa.'),
  question('science-08', 'science', 'Skupenstvá', 'Čo sa stane s ľadom, keď sa za bežného tlaku zohreje nad 0 °C?', ['Roztopí sa na vodu', 'Zmení sa na kameň', 'Zmizne bez stopy'], 'a', 'Ľad sa topí: tuhá voda prechádza na kvapalnú. Látka zostáva vodou.', 'Ide o zmenu skupenstva, nie o vznik novej látky.'),
  question('nature-01', 'nature', 'Opeľovanie', 'Ako včely pomáhajú mnohým kvitnúcim rastlinám?', ['Farbením listov', 'Prenášaním peľu', 'Vyrábaním koreňov'], 'b', 'Včely prenášajú peľ medzi kvetmi. Opeľovanie pomáha mnohým rastlinám vytvárať semená a plody.', 'Pri návšteve kvetov sa im na telo zachytáva jemný prášok.'),
  question('nature-02', 'nature', 'Rozkladače', 'Ktoré organizmy pomáhajú rozkladať odumreté listy a vracať živiny do pôdy?', ['Iba orly', 'Iba ryby', 'Huby a baktérie'], 'c', 'Huby a baktérie sú dôležité rozkladače. Z organických zvyškov uvoľňujú živiny.', 'Často sú malé alebo skryté v pôde.'),
  question('nature-03', 'nature', 'Fotosyntéza', 'Ktorý plyn zelené rastliny využívajú pri fotosyntéze?', ['Oxid uhličitý', 'Hélium', 'Vodík'], 'a', 'Pri fotosyntéze rastliny využívajú oxid uhličitý, vodu a svetelnú energiu na tvorbu cukrov a uvoľňujú kyslík.', 'Je to plyn, ktorý okrem iného vydychujeme.'),
  question('nature-04', 'nature', 'Potravový reťazec', 'Ktorý potravový reťazec je správny? Šípka ukazuje od potravy k tomu, kto ju je.', ['Líška → tráva → zajac', 'Tráva → zajac → líška', 'Zajac → líška → tráva'], 'b', 'Zajac môže jesť trávu a líška môže jesť zajaca. Energia sa tak prenáša od rastliny k bylinožravcovi a potom k predátorovi.', 'Začni rastlinou, ktorá získava energiu zo Slnka.'),
  question('nature-05', 'nature', 'Biodiverzita', 'Čo znamená biodiverzita?', ['Vek jedného stromu', 'Počet ciest v lese', 'Rozmanitosť živých organizmov'], 'c', 'Biodiverzita je rozmanitosť života; zahŕňa napríklad rôzne druhy, genetickú rozmanitosť aj ekosystémy.', 'Bio znamená život a diverzita rozmanitosť.'),
  question('nature-06', 'nature', 'Korene', 'Čo prijímajú korene rastliny z pôdy?', ['Vodu a minerálne látky', 'Slnečné lúče', 'Hotové kvety'], 'a', 'Korene upevňujú rastlinu a z pôdy prijímajú vodu a minerálne látky.', 'Pomysli na to, čo dodáš pôde pri polievaní.'),
  question('nature-07', 'nature', 'Biotop', 'Čo je biotop?', ['Jedno zvieracie mláďa', 'Prostredie, v ktorom organizmus žije', 'Druh baterky'], 'b', 'Biotop je životné prostredie organizmu alebo spoločenstva. Pre žabu to môže byť jazierko a jeho okolie.', 'Napríklad les, lúka alebo jazierko.'),
  question('nature-08', 'nature', 'Recyklácia', 'Čo umožňuje správne triedenie použitého papiera?', ['Premenu na kov', 'Neobmedzené používanie vody', 'Využitie jeho vlákien na výrobu nového papiera'], 'c', 'Pri recyklácii sa papierové vlákna získajú a využijú znova. Vlákna sa však nedajú recyklovať nekonečne.', 'Materiál sa spracuje a použije v novom výrobku.')
];
const QUESTS = [
  { id: 'first-hatch', name: 'Prvý kamarát', description: 'Vyliahni svoje prvé vajíčko.', target: 1, metric: 'hatched' },
  { id: 'first-knowledge', name: 'Mladý objaviteľ', description: 'Správne vyrieš tri rôzne otázky.', target: 3, metric: 'knowledge' },
  { id: 'pet-club', name: 'Zvierací klub', description: 'Nazbieraj štyroch zvieracích kamarátov.', target: 4, metric: 'pets' },
  { id: 'cosmic-scholar', name: 'Hviezdny vedec', description: 'Získaj osem rôznych poznatkov a odomkni kozmické vajce.', target: 8, metric: 'knowledge' },
  { id: 'academy-master', name: 'Majster akadémie', description: 'Vyrieš osemnásť rôznych otázok.', target: 18, metric: 'knowledge' }
];
const QUESTIONS_BY_ID = new Map(QUESTIONS.map(q => [q.id, q]));

function freshSeed() {
  try { const seed = new Uint32Array(1); globalThis.crypto.getRandomValues(seed); return seed[0] || 123456789; }
  catch { return ((Date.now() ^ Math.floor(Math.random() * 0xffffffff)) >>> 0) || 123456789; }
}
function freshState() {
  return {
    version: 2,
    player: { name: 'Objaviteľ', coins: 150, knowledge: 0, level: 1, xp: 0 },
    eggs: [], pets: [], equippedPet: null,
    upgrades: { speed: 0, capacity: 0, income: 0 },
    stats: { correct: 0, wrong: 0, hatched: 0, totalEarned: 0 },
    progress: { solved: [], unlocked: ['meadow'] },
    settings: { sound: false },
    prestigeCount: 0, rngState: freshSeed(), serial: 1, lastSave: null
  };
}
function browserStorage() {
  try { return globalThis.localStorage || null; } catch { return null; }
}

export class AcademyEngine {
  constructor(storage = undefined) {
    this.storage = storage === undefined ? browserStorage() : storage;
    this.listeners = new Set();
    this._saveTimer = 0;
    this._emitTimer = 0;
    this._quizCursor = { math: 0, science: 0, nature: 0 };
    this._state = this._load();
  }

  _load() {
    try {
      const raw = this.storage?.getItem(SAVE_KEY);
      if (!raw) return freshState();
      const saved = JSON.parse(raw);
      if (!saved || saved.version !== 2 || typeof saved.player !== 'object') return freshState();
      const state = freshState();
      state.player.name = this._cleanName(saved.player.name) || state.player.name;
      state.player.coins = number(saved.player.coins, 150);
      state.player.xp = integer(saved.player.xp);
      state.player.level = 1 + Math.floor(state.player.xp / 100);
      state.progress.solved = Array.isArray(saved.progress?.solved) ? [...new Set(saved.progress.solved.filter(id => QUESTIONS_BY_ID.has(id)))] : [];
      state.player.knowledge = state.progress.solved.length;
      for (const def of UPGRADES) state.upgrades[def.id] = integer(saved.upgrades?.[def.id], 0, def.maxLevel);
      const eggIds = new Set();
      state.eggs = (Array.isArray(saved.eggs) ? saved.eggs : []).filter(egg => {
        if (!egg || typeof egg.id !== 'string' || egg.id.length > 80 || eggIds.has(egg.id) || !EGGS.some(def => def.id === egg.type)) return false;
        eggIds.add(egg.id); return true;
      }).slice(0, 3 + 2 * state.upgrades.capacity).map(({ id, type }) => ({ id, type }));
      const petIds = new Set();
      state.pets = (Array.isArray(saved.pets) ? saved.pets : []).filter(pet => {
        if (!pet || typeof pet.id !== 'string' || pet.id.length > 80 || petIds.has(pet.id) || !PETS[pet.rarity]) return false;
        if (!PETS[pet.rarity].some(def => def.name === pet.name && def.kind === pet.kind)) return false;
        petIds.add(pet.id); return true;
      }).slice(0, 10000).map(pet => ({ id: pet.id, rarity: pet.rarity, ...PETS[pet.rarity].find(def => def.name === pet.name && def.kind === pet.kind) }));
      state.equippedPet = petIds.has(saved.equippedPet) ? saved.equippedPet : state.pets[0]?.id || null;
      state.stats.correct = Math.max(integer(saved.stats?.correct), state.player.knowledge);
      state.stats.wrong = integer(saved.stats?.wrong);
      state.stats.hatched = Math.max(integer(saved.stats?.hatched), state.pets.length);
      state.stats.totalEarned = number(saved.stats?.totalEarned);
      state.settings.sound = saved.settings?.sound === true;
      state.prestigeCount = integer(saved.prestigeCount, 0, 10000);
      state.rngState = integer(saved.rngState, state.rngState, 0xffffffff) || state.rngState;
      state.serial = integer(saved.serial, 1);
      for (const item of [...state.eggs, ...state.pets]) {
        const suffix = Number(item.id.split('-').at(-1));
        if (Number.isFinite(suffix)) state.serial = Math.max(state.serial, suffix + 1);
      }
      state.lastSave = typeof saved.lastSave === 'string' ? saved.lastSave.slice(0, 40) : null;
      this._unlock(state);
      return state;
    } catch { return freshState(); }
  }

  _cleanName(name) { return typeof name === 'string' ? name.replace(/[\u0000-\u001f\u007f]/g, '').trim().slice(0, 24) : ''; }
  _unlock(state = this._state) {
    state.progress.unlocked = ['meadow'];
    if (state.player.knowledge >= 3) state.progress.unlocked.push('science');
    if (state.player.knowledge >= 8) state.progress.unlocked.push('astral');
  }
  _save() {
    try {
      if (!this.storage?.setItem) return false;
      this._state.lastSave = new Date().toISOString();
      this.storage.setItem(SAVE_KEY, JSON.stringify(this._state));
      this._saveTimer = 0;
      return true;
    } catch { return false; }
  }
  _notify(result) {
    for (const fn of this.listeners) {
      try { fn(this.getState(), clone(result)); } catch { /* One UI listener must not stop gameplay. */ }
    }
  }
  _commit(result) { this._save(); this._notify(result); return result; }
  _fail(message, extra = {}) { return { ok: false, message, event: 'error', ...extra }; }
  _credit(amount) {
    this._state.player.coins = Math.min(1e12, this._state.player.coins + amount);
    this._state.stats.totalEarned = Math.min(1e12, this._state.stats.totalEarned + amount);
  }
  _random() {
    let value = this._state.rngState >>> 0;
    value ^= value << 13; value ^= value >>> 17; value ^= value << 5;
    this._state.rngState = value >>> 0;
    return this._state.rngState / 4294967296;
  }
  _income() {
    const pets = this._state.pets;
    const companion = pets.find(p => p.id === this._state.equippedPet);
    const base = 0.5 + pets.reduce((sum, p) => sum + p.income, 0) + (companion?.income || 0) * 0.1;
    return base * (1 + this._state.upgrades.income * 0.25) * (1 + this._state.prestigeCount * 0.2);
  }
  _upgradeCost(def) { return Math.round(def.baseCost * Math.pow(1.7, this._state.upgrades[def.id])); }

  getState() {
    const state = clone(this._state);
    state.player.coins = Math.round(state.player.coins * 100) / 100;
    state.stats.totalEarned = Math.round(state.stats.totalEarned * 100) / 100;
    state.capacity = 3 + state.upgrades.capacity * 2;
    state.movementMultiplier = 1 + state.upgrades.speed * 0.12;
    state.incomePerSecond = Math.round(this._income() * 100) / 100;
    state.prestigeBonus = 1 + state.prestigeCount * 0.2;
    return state;
  }
  subscribe(fn) {
    if (typeof fn !== 'function') return () => {};
    this.listeners.add(fn);
    try { fn(this.getState(), { ok: true, message: '', event: 'ready' }); } catch { /* UI is isolated. */ }
    return () => this.listeners.delete(fn);
  }
  tick(dtSeconds) {
    const dt = typeof dtSeconds === 'number' && Number.isFinite(dtSeconds) ? Math.max(0, Math.min(dtSeconds, 5)) : 0;
    if (!dt) return { ok: true, message: '', event: 'tick', earned: 0 };
    const earned = this._income() * dt;
    this._credit(earned);
    this._saveTimer += dt;
    this._emitTimer += dt;
    if (this._saveTimer >= 5) this._save();
    const result = { ok: true, message: '', event: 'tick', earned };
    if (this._emitTimer >= 1) { this._emitTimer %= 1; this._notify(result); }
    return result;
  }
  buyEgg(eggId) {
    const def = EGGS.find(egg => egg.id === eggId);
    if (!def) return this._fail('Také vajíčko nepoznáme.');
    if (this._state.player.knowledge < def.knowledge) return this._fail(`Najprv vyrieš ${def.knowledge} rôznych otázok.`, { requiredKnowledge: def.knowledge });
    if (this._state.eggs.length >= 3 + this._state.upgrades.capacity * 2) return this._fail('Batoh je plný. Vyliahni vajíčko alebo zväčši batoh.');
    if (this._state.player.coins < def.price) return this._fail(`Potrebuješ ${def.price} mincí. Otázky a zvieratká ti pomôžu zarobiť.`);
    this._state.player.coins -= def.price;
    const egg = { id: `egg-${this._state.serial++}`, type: def.id };
    this._state.eggs.push(egg);
    return this._commit({ ok: true, event: 'egg-bought', message: `${def.name} je v tvojom batohu!`, egg: clone(egg), cost: def.price });
  }
  hatchEgg() {
    const egg = this._state.eggs[0];
    if (!egg) return this._fail('Najprv si kúp vajíčko pri predajnom páse.');
    const pool = POOLS[egg.type];
    const roll = this._random();
    let cumulative = 0;
    let rarity = pool.at(-1).rarity;
    for (const entry of pool) { cumulative += entry.chance; if (roll < cumulative) { rarity = entry.rarity; break; } }
    const choices = PETS[rarity];
    const def = choices[Math.floor(this._random() * choices.length)];
    const pet = { id: `pet-${this._state.serial++}`, rarity, ...def };
    this._state.eggs.shift();
    this._state.pets.push(pet);
    this._state.stats.hatched++;
    if (!this._state.equippedPet) this._state.equippedPet = pet.id;
    return this._commit({ ok: true, event: 'hatched', message: `${pet.name} sa vyliahol! Tvoja základňa teraz zarába viac mincí.`, pet: clone(pet), egg: clone(egg) });
  }
  getQuiz(subject = 'math') {
    const list = QUESTIONS.filter(q => q.subject === subject);
    if (!list.length) return null;
    const unsolved = list.find(q => !this._state.progress.solved.includes(q.id));
    return clone(unsolved || list[this._quizCursor[subject] % list.length]);
  }
  answerQuiz(questionId, input) {
    const q = QUESTIONS_BY_ID.get(questionId);
    if (!q) return this._fail('Táto otázka neexistuje.');
    const option = typeof input === 'string' ? input.trim().toLowerCase() : '';
    if (!q.options.some(item => item.id === option)) return this._fail('Vyber jednu z ponúkaných odpovedí.');
    if (option !== q.answer) {
      this._state.stats.wrong++;
      return this._commit({ ok: false, event: 'quiz-wrong', message: `Skús to ešte raz. ${q.hint}`, correct: false, questionId, hint: q.hint });
    }
    const firstSolve = !this._state.progress.solved.includes(questionId);
    const coins = firstSolve ? 50 : 10;
    const xp = firstSolve ? 25 : 5;
    const oldLevel = this._state.player.level;
    this._credit(coins);
    this._state.player.xp += xp;
    this._state.player.level = 1 + Math.floor(this._state.player.xp / 100);
    this._state.stats.correct++;
    if (firstSolve) {
      this._state.progress.solved.push(questionId);
      this._state.player.knowledge = this._state.progress.solved.length;
      this._unlock();
    }
    this._quizCursor[q.subject]++;
    return this._commit({ ok: true, event: 'quiz-correct', message: `${q.explanation} +${coins} mincí${firstSolve ? ' a nový poznatok!' : ' za precvičenie.'}`, correct: true, questionId, firstSolve, coins, xp, knowledge: firstSolve ? 1 : 0, explanation: q.explanation, levelUp: this._state.player.level > oldLevel });
  }
  upgrade(id) {
    const def = UPGRADES.find(item => item.id === id);
    if (!def) return this._fail('Také vylepšenie nepoznáme.');
    if (this._state.upgrades[id] >= def.maxLevel) return this._fail('Toto vylepšenie už má najvyšší stupeň.');
    const cost = this._upgradeCost(def);
    if (this._state.player.coins < cost) return this._fail(`Na vylepšenie potrebuješ ${cost} mincí.`);
    this._state.player.coins -= cost;
    this._state.upgrades[id]++;
    return this._commit({ ok: true, event: 'upgraded', message: `${def.name}: stupeň ${this._state.upgrades[id]}. ${def.description}`, upgrade: id, level: this._state.upgrades[id], cost });
  }
  equipPet(petId) {
    if (petId !== null && !this._state.pets.some(pet => pet.id === petId)) return this._fail('Toto zvieratko ešte nevlastníš.');
    this._state.equippedPet = petId;
    const pet = this._state.pets.find(item => item.id === petId);
    return this._commit({ ok: true, event: 'pet-equipped', message: pet ? `${pet.name} ťa bude sprevádzať a pridá 10 % svojho príjmu navyše.` : 'Zvieratká na teba počkajú v základni.', petId });
  }
  setName(name) {
    const clean = this._cleanName(name);
    if (!clean) return this._fail('Zadaj meno s aspoň jedným znakom.');
    this._state.player.name = clean;
    return this._commit({ ok: true, event: 'name-changed', message: `Vitaj, ${clean}!` });
  }
  setSetting(key, value) {
    if (key !== 'sound' || typeof value !== 'boolean') return this._fail('Toto nastavenie nie je dostupné.');
    this._state.settings.sound = value;
    return this._commit({ ok: true, event: 'setting-changed', message: value ? 'Zvuk zapnutý.' : 'Zvuk vypnutý.', key, value });
  }
  prestige() {
    if (this._state.pets.length < 6 || this._state.player.knowledge < 12) return this._fail('Na nové dobrodružstvo potrebuješ 6 zvieratiek a 12 rôznych poznatkov.');
    this._state.prestigeCount++;
    this._state.player.coins = 150;
    this._state.eggs = [];
    this._state.pets = [];
    this._state.equippedPet = null;
    this._state.upgrades = { speed: 0, capacity: 0, income: 0 };
    return this._commit({ ok: true, event: 'prestige', message: 'Nové dobrodružstvo začína! Poznatky a úroveň zostali, trvalý bonus k príjmu vzrástol o 20 %.', prestigeCount: this._state.prestigeCount });
  }
  reset() {
    this._state = freshState();
    this._saveTimer = 0;
    this._emitTimer = 0;
    this._quizCursor = { math: 0, science: 0, nature: 0 };
    return this._commit({ ok: true, event: 'reset', message: 'Začínaš nové dobrodružstvo so 150 mincami.' });
  }
  getDefinitions() {
    const state = this._state;
    return clone({
      eggDefs: EGGS.map(def => ({ ...def, available: state.player.knowledge >= def.knowledge, chances: POOLS[def.id] })),
      upgradeDefs: UPGRADES.map(def => ({ ...def, cost: this._upgradeCost(def), level: state.upgrades[def.id], maxed: state.upgrades[def.id] >= def.maxLevel })),
      quests: QUESTS.map(def => {
        const current = def.metric === 'pets' ? state.pets.length : def.metric === 'hatched' ? state.stats.hatched : state.player.knowledge;
        return { ...def, current, complete: current >= def.target };
      }),
      petPools: PETS,
      hatchPools: POOLS,
      questions: QUESTIONS,
      subjects: [{ id: 'math', name: 'Matematika', color: '#ffcb6a' }, { id: 'science', name: 'Veda', color: '#6dd6ff' }, { id: 'nature', name: 'Príroda', color: '#8ae2a4' }],
      prestige: { pets: 6, knowledge: 12, bonusPerLevel: 0.2 },
      saveKey: SAVE_KEY
    });
  }
}

export default AcademyEngine;
