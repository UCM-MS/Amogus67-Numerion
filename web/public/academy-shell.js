import { AGE_LABELS, TUTORIAL_STEPS, tutorialStep } from './academy-journey.js';

export function createAcademyShell({ getState, petArt, onPlay, onTutorial, onCoop, onArena, onPanel, onProfile, onHelp, onPauseTutorial, onNavigate }) {
  const title = document.createElement('section'); title.id = 'title-screen'; title.className = 'title-screen'; title.setAttribute('aria-label', 'Hlavné menu LUMEN');
  title.innerHTML = `<div class="title-shade"></div><div class="title-top"><a class="title-logo" href="./"><span>✦</span> LUMEN<small>AKADÉMIA OBJAVOV</small></a><span class="title-edition">DOBRODRUŽSTVO VEDOMOSTÍ <i></i></span></div>
    <div class="title-layout"><div class="title-copy"><span class="title-kicker">TVOJ PRÍBEH SA ZAČÍNA ZVEDAVOSŤOU</span><h1>Malý objav.<br><em>Veľký svet.</em></h1><p>Vstúp do živej akadémie. Spoznaj svojho spoločníka, objav nové vedomosti a vyraz za dobrodružstvom s kamarátmi.</p>
    <div class="title-primary-actions"><button class="title-play" id="title-play"><span>Začať dobrodružstvo</span><b>↗</b></button><button class="title-tutorial" id="title-tutorial">Prvá výprava <small>Interaktívny tutoriál · približne 5 min</small></button></div>
    <div class="title-secondary-actions"><button id="title-coop"><svg class="ui-icon"><use href="#i-users"/></svg> Hrať spolu</button><button id="title-arena"><svg class="ui-icon"><use href="#i-arena"/></svg> Aréna</button><button id="title-settings"><svg class="ui-icon"><use href="#i-settings"/></svg> Nastavenia</button></div>
    <div class="title-player"><span class="title-player-icon">✦</span><div><strong id="title-player-name">Tvoj objaviteľ</strong><small id="title-player-progress">Postup sa automaticky ukladá.</small></div><button id="title-profile" aria-label="Upraviť objaviteľa">Upraviť ↗</button></div></div>
    <div class="title-world-note"><span class="world-note-line"></span><small>VITAJ DOMA</small><strong>Akadémia<br>nekonečnej zvedavosti.</strong><span>6 učebných oblastí · živý 3D svet</span></div>
    <article class="title-pet-feature"><div id="title-pet-art"></div><div><span class="title-kicker">SPOLU OBJAVÍTE VIAC</span><h2 id="title-pet-name">Tvoj prvý kamarát čaká.</h2><p id="title-pet-copy">Vajíčka, tréning a celá zbierka spoločníkov.</p></div><button id="title-pets" aria-label="Otvoriť zbierku spoločníkov">↗</button></article></div>
    <footer class="title-footer"><span><i></i> HRATEĽNÉ PRIAMO V PREHLIADAČI</span><button id="title-help">Ovládanie a pomoc</button><span>OBJAVUJ. UČ SA. RASŤ.</span></footer>`;
  document.body.append(title);
  const card = document.createElement('aside'); card.id = 'tutorial-card'; card.className = 'tutorial-card'; card.hidden = true; card.setAttribute('aria-label', 'Sprievodca prvou výpravou');
  card.innerHTML = `<div class="tutorial-heading"><span>PRVÁ VÝPRAVA</span><button id="tutorial-pause" aria-label="Odložiť tutoriál">×</button></div><div class="tutorial-dots" aria-hidden="true"></div><span id="tutorial-step-count"></span><h2 id="tutorial-title"></h2><p id="tutorial-text"></p><button id="tutorial-go" class="primary-btn"></button>`;
  document.body.append(card);
  const bind = (id, fn) => document.getElementById(id).addEventListener('click', fn);
  bind('title-play', onPlay); bind('title-tutorial', onTutorial);
  bind('title-coop', () => { hide(); onCoop(); }); bind('title-arena', () => { hide(); onArena(); });
  bind('title-settings', () => onPanel('settings')); bind('title-pets', () => onPanel('collection'));
  bind('title-profile', onProfile); bind('title-help', onHelp); bind('tutorial-pause', onPauseTutorial);
  bind('tutorial-go', () => { const s = tutorialStep(getState()); if (s?.panel) onPanel(s.panel); else if (s?.waypoint) onNavigate(s.waypoint); else document.getElementById('game-canvas').focus(); });
  let signature = '';
  function refresh(state = getState()) {
    const p = state.player, owned = state.pets.find(pet => pet.id === state.equippedPet);
    title.querySelector('#title-player-name').textContent = state.onboarding.profileReady ? p.name : 'Tvoj objaviteľ';
    title.querySelector('#title-player-progress').textContent = `Úroveň ${p.level} · ${state.progress.solved.length} poznatkov · ${AGE_LABELS[p.ageBand]}`;
    title.querySelector('#title-play span').textContent = state.onboarding.profileReady ? 'Pokračovať v dobrodružstve' : 'Začať dobrodružstvo';
    title.querySelector('#title-pet-art').innerHTML = petArt(owned || { kind: 'fox', color: '#ffc88d' });
    title.querySelector('#title-pet-name').textContent = owned ? `${owned.name} je pripravený.` : 'Tvoj prvý kamarát čaká.';
    title.querySelector('#title-pet-copy').textContent = owned ? `Úroveň ${owned.level} · ${state.pets.length} spoločníkov v tvojej partii.` : 'Vajíčka, tréning a celá zbierka spoločníkov.';
    const step = tutorialStep(state), active = state.onboarding.active && step;
    card.hidden = !active; document.body.classList.toggle('tutorial-active', Boolean(active));
    if (!active) return;
    const sig = step.id + state.onboarding.events.join(','); if (signature === sig) return; signature = sig;
    const index = TUTORIAL_STEPS.indexOf(step);
    card.querySelector('.tutorial-dots').innerHTML = TUTORIAL_STEPS.map(s => `<i class="${state.onboarding.events.includes(s.id) ? 'done' : s.id === step.id ? 'current' : ''}"></i>`).join('');
    card.querySelector('#tutorial-step-count').textContent = `KROK ${index + 1} Z ${TUTORIAL_STEPS.length}`;
    card.querySelector('#tutorial-title').textContent = step.title; card.querySelector('#tutorial-text').textContent = step.text;
    card.querySelector('#tutorial-go').textContent = step.panel === 'curriculum' ? 'Otvoriť prvú výzvu ↗' : step.panel === 'pets' ? 'Vybrať spoločníka ↗' : step.waypoint ? 'Ukázať cestu ↗' : `Vyskúšaj ${step.key}`;
  }
  function hide() { title.hidden = true; document.body.classList.remove('at-title'); document.getElementById('game-canvas').focus({ preventScroll: true }); }
  function show() { refresh(); title.hidden = false; document.body.classList.add('at-title'); title.scrollTop = 0; title.querySelector('#title-play').focus({ preventScroll: true }); }
  refresh(); show();
  return { show, hide, refresh, get open() { return !title.hidden; } };
}
