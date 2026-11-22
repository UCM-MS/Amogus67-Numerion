import * as THREE from './vendor/three.module.js';
import { AcademyEngine } from './academy-engine.js';
import { createAcademyWorld } from './scene3d.js';

const $ = (selector,parent=document) => parent.querySelector(selector);
const esc = text => String(text ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const icons = {coin:'<circle cx="12" cy="12" r="9"/><path d="M14.5 7h-4a2 2 0 0 0 0 4h3a2 2 0 0 1 0 4h-4M12 5v14"/>',book:'<path d="M12 5c-3-2-7-2-10-1v15c3-1 7-1 10 1 3-2 7-2 10-1V4c-3-1-7-1-10 1v15Z"/>',egg:'<path d="M20 15c0 5-4 7-8 7s-8-2-8-7C4 9 9 2 12 2s8 7 8 13Z"/><path d="m7 12 3 2 3-3 4 2"/>',star:'<path d="m12 2 3 7 7 3-7 3-3 7-3-7-7-3 7-3 3-7Z"/>',arrow:'<path d="M4 12h16m-6-6 6 6-6 6"/>',check:'<path d="m5 12 4 4L19 6"/>',close:'<path d="m6 6 12 12M6 18 18 6"/>',lock:'<rect x="5" y="10" width="14" height="11" rx="3"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/>'};
const icon=(name)=>`<svg class="ui-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[name]||icons.star}</svg>`;
const rarityLabels={common:'Bežný',uncommon:'Neobyčajný',rare:'Vzácny',epic:'Epický',legendary:'Legendárny'};
const subjectLabels={math:'Matematika',science:'Veda',nature:'Príroda'};
const compact = n => new Intl.NumberFormat('sk-SK',{notation:n>=10000?'compact':'standard',maximumFractionDigits:1}).format(n);
let engine, renderer, scene, camera, world, player, limbs, follower, followerId, currentTarget, modalPanel, activeQuiz, modalOpen=false;
let yaw=0, pitch=.59, cameraDistance=21, clockTime=0, lastTime=0, verticalVelocity=0, onGround=true;
const keys = new Set(), joystick={x:0,y:0};
const position = new THREE.Vector3(0,0,13), velocity=new THREE.Vector3();
const desiredCamera = new THREE.Vector3(), cameraLook=new THREE.Vector3(), smoothLook=new THREE.Vector3();
let pointer, audio, stateCache, lastQuestSignature='', interactionId='', previousFocus;
const materialCache=new Map(), geometries=new Map();
function material(color){if(!materialCache.has(color))materialCache.set(color,new THREE.MeshStandardMaterial({color,roughness:.7}));return materialCache.get(color);}
function box(w,h,d,color,x=0,y=0,z=0){const key=[w,h,d].join(',');if(!geometries.has(key))geometries.set(key,new THREE.BoxGeometry(w,h,d));const mesh=new THREE.Mesh(geometries.get(key),material(color));mesh.position.set(x,y,z);mesh.castShadow=true;mesh.receiveShadow=true;return mesh;}
function notify(message,kind='info'){if(!message)return;const div=document.createElement('div');div.className=`game-toast ${kind}`;div.setAttribute('role','status');div.textContent=message;$('#toast-root').append(div);setTimeout(()=>div.remove(),4200);}
function chime(good=true){if(!stateCache?.settings.sound)return;try{audio ||=new (window.AudioContext||window.webkitAudioContext)();audio.resume();[good?523:196,good?784:165].forEach((freq,i)=>{const oscillator=audio.createOscillator(),gain=audio.createGain();const t=audio.currentTime+i*.12;oscillator.frequency.value=freq;gain.gain.setValueAtTime(.0001,t);gain.gain.exponentialRampToValueAtTime(.045,t+.01);gain.gain.exponentialRampToValueAtTime(.0001,t+.25);oscillator.connect(gain).connect(audio.destination);oscillator.start(t);oscillator.stop(t+.26);});}catch{}}
function action(result){notify(result.message,result.ok?'success':'warning');chime(result.ok);return result;}
function createCharacter(){
  const group=new THREE.Group();
  const torso=box(1.2,1.25,.65,'#f9df70',0,1.65,0); group.add(torso);
  group.add(box(.65,.66,.18,'#b7c8fc',0,1.65,.4));
  const head=box(.86,.84,.82,'#ffdab4',0,2.7,0);group.add(head);
  group.add(box(.9,.21,.86,'#333548',0,3.15,0));group.add(box(.26,.45,.87,'#333548',-.35,2.95,0));
  // A readable smile and eyes on the avatar's forward (-Z) face.
  group.add(box(.09,.1,.025,'#263248',-.19,2.75,-.425));group.add(box(.09,.1,.025,'#263248',.19,2.75,-.425));group.add(box(.26,.055,.028,'#a95b58',0,2.52,-.427));
  const arms=[];[-1,1].forEach(side=>{const pivot=new THREE.Group();pivot.position.set(side*.86,2.18,0);pivot.add(box(.42,.92,.47,'#ffdab4',0,-.45,0));pivot.add(box(.45,.38,.5,'#f9df70',0,-.17,0));group.add(pivot);arms.push(pivot);});
  const legs=[];[-1,1].forEach(side=>{const pivot=new THREE.Group();pivot.position.set(side*.32,1.05,0);pivot.add(box(.47,.91,.53,'#7083e8',0,-.46,0));pivot.add(box(.5,.22,.72,'#fff7e7',0,-.93,-.05));group.add(pivot);legs.push(pivot);});
  scene.add(group);return {group,arms,legs};
}
function makePetModel(pet){
  const g=new THREE.Group(),color=pet.color||'#c8aff5';g.add(box(.95,.8,.8,color,0,.58,0));g.add(box(1,.76,.9,color,0,1.15,-.1));
  [-1,1].forEach(side=>{g.add(box(.16,.22,.04,'#29334b',side*.24,1.23,-.56));g.add(box(.3,.24,.32,color,side*.3,.17,-.1));});
  g.add(box(.14,.1,.05,'#f080a0',0,1.05,-.58));
  if(pet.kind==='bunny'){[-1,1].forEach(s=>{g.add(box(.23,.72,.26,color,s*.27,1.78,0));g.add(box(.1,.5,.03,'#ffbdd1',s*.27,1.8,-.14));});}
  else if(pet.kind==='robot'){g.add(box(.07,.4,.07,'#91a4ac',0,1.7,0));g.add(box(.2,.16,.2,'#f9df70',0,1.96,0));}
  else {[-1,1].forEach(s=>g.add(box(.29,.34,.31,color,s*.35,1.65,0)));}
  if(pet.kind==='dragon'||pet.kind==='owl'){[-1,1].forEach(s=>{const wing=box(.7,.14,.7,color,s*.75,.8,.1);wing.rotation.z=s*.4;g.add(wing);});}
  return g;
}
function petArt(pet){return `<svg class="pet-illustration" viewBox="0 0 120 110" aria-hidden="true"><ellipse cx="60" cy="95" rx="37" ry="7" fill="#242942" opacity=".1"/><rect x="30" y="41" width="60" height="48" rx="15" fill="${esc(pet.color||'#c8aff5')}"/><path d="M34 45V21q10-13 18 9v16M68 44V27q9-19 18-7v24" fill="${esc(pet.color||'#c8aff5')}"/><rect x="39" y="55" width="9" height="13" rx="4" fill="#253047"/><rect x="72" y="55" width="9" height="13" rx="4" fill="#253047"/><path d="m54 72 6 5 6-5" stroke="#253047" stroke-width="3" fill="none" stroke-linecap="round"/><circle cx="38" cy="73" r="5" fill="#ffbdd1"/><circle cx="82" cy="73" r="5" fill="#ffbdd1"/><rect x="35" y="85" width="19" height="10" rx="4" fill="${esc(pet.color||'#c8aff5')}"/><rect x="67" y="85" width="19" height="10" rx="4" fill="${esc(pet.color||'#c8aff5')}"/></svg>`;}
function syncState(state,result={}){
  const previousState=stateCache;
  stateCache=state;
  $('#coins-count').textContent=compact(state.player.coins);$('#knowledge-count').textContent=state.player.knowledge;
  $('#income-count').textContent=`+${compact(state.incomePerSecond||0)}/s`;
  if($('#player-name'))$('#player-name').textContent=state.player.name;
  $('#sound-btn')?.setAttribute('aria-pressed',String(state.settings.sound));
  const quests=[{title:'Prvý spoločník',text:'Vyliahni vajíčko na základni',value:Math.min(1,state.stats.hatched),target:1},{title:'Mladý vedec',text:'Objav tri nové poznatky',value:Math.min(3,state.progress.solved.length),target:3},{title:'Živá základňa',text:'Získaj štyroch spoločníkov',value:Math.min(4,state.pets.length),target:4}];
  if($('#quest-count'))$('#quest-count').textContent=`${quests.filter(q=>q.value>=q.target).length}/3`;
  const signature=quests.map(q=>q.value).join(',');
  if(signature!==lastQuestSignature){lastQuestSignature=signature;$('#quest-list').innerHTML=quests.map(q=>`<div class="quest-item ${q.value>=q.target?'completed':''}"><span class="quest-icon">${icon(q.value>=q.target?'check':'star')}</span><div class="quest-copy"><strong class="quest-title">${q.title}</strong><span class="quest-meta">${q.text}</span><div class="quest-progress"><i style="width:${q.value/q.target*100}%"></i></div></div><span class="quest-count">${q.value}/${q.target}</span></div>`).join('');}
  world?.setState(state);
  if(modalOpen){
    const definitions=engine.getDefinitions();
    document.querySelectorAll('[data-buy]').forEach(b=>{const d=definitions.eggDefs.find(e=>e.id===b.dataset.buy);if(d)b.disabled=state.player.knowledge<d.knowledge||state.player.coins<d.price||state.eggs.length>=state.capacity;});
    document.querySelectorAll('[data-upgrade]').forEach(b=>{const d=definitions.upgradeDefs.find(u=>u.id===b.dataset.upgrade);if(d)b.disabled=state.upgrades[d.id]>=d.maxLevel||state.player.coins<d.cost;});
  }
  if(state.equippedPet!==followerId){
    if(follower){scene.remove(follower);follower=null;}followerId=state.equippedPet;
    const pet=state.pets.find(p=>p.id===followerId);if(pet){follower=makePetModel(pet);follower.position.copy(position);scene.add(follower);}
  }
  if(previousState&&state.progress.unlocked.length>previousState.progress.unlocked.length)notify(state.progress.unlocked.includes('astral')?'Kozmické vajíčko a astrálna brána sú otvorené!':'Vedecké vajíčko je teraz odomknuté!','success');
}
function openModal(content,panel=null){closeModal(false);modalOpen=true;modalPanel=panel;previousFocus=document.activeElement;keys.clear();joystick.x=joystick.y=0;
  $('#modal-root').innerHTML=`<div class="modal-backdrop"><section class="modal-card" role="dialog" aria-modal="true" aria-labelledby="modal-title"><button class="modal-close" data-action="close" aria-label="Zavrieť">${icon('close')}</button>${content}</section></div>`;
  const heading=$('#modal-root h2');if(heading)heading.id='modal-title';requestAnimationFrame(()=>($('#modal-root button:not(.modal-close),#modal-root input')||$('#modal-root .modal-close'))?.focus());
}
function closeModal(focus=true){$('#modal-root').innerHTML='';modalOpen=false;modalPanel=null;activeQuiz=null;if(focus)$('#game-canvas')?.focus({preventScroll:true});}
function modalHeading(label,title,description){return `<span class="modal-eyebrow">${label}</span><h2>${title}</h2><p class="modal-description">${description}</p>`;}
function showPanel(panel){
  const state=engine.getState(),defs=engine.getDefinitions();
  if(panel==='pets'){
    openModal(modalHeading('TVOJI SPOLOČNÍCI',`Malá partia. Veľké objavy.`,`Všetci spoločníci vyrábajú mince na základni. Jeden z nich ťa môže sprevádzať svetom.`)+`<div class="panel-summary">${icon('coin')} Príjem základne <strong>+${compact(state.incomePerSecond)}/s</strong><span>${state.pets.length} spoločníkov</span></div><div class="panel-grid">${state.pets.length?state.pets.map(p=>`<article class="item-card pet-card rarity-${p.rarity}">${petArt(p)}<span class="tag">${rarityLabels[p.rarity]||p.rarity}</span><h3>${esc(p.name)}</h3><p>${icon('coin')} +${p.income}/s</p><button class="${p.id===state.equippedPet?'secondary-btn':'primary-btn'}" data-equip="${esc(p.id)}">${p.id===state.equippedPet?'Sprevádza ťa':'Vziať so sebou'}</button></article>`).join(''):`<div class="empty-state">${icon('egg')}<h3>Prvý kamarát už čaká.</h3><p>Kúp vajíčko na centrálnom námestí a vyliahni ho v tvojej základni.</p><button class="primary-btn" data-waypoint="egg-basic">Ukázať cestu k vajíčkam</button></div>`}</div>`,'pets');
  }
  if(panel==='eggs'){
    const nearHatch=world.interactables.some(t=>t.type==='hatch'&&distance(t)<t.radius+1);
    openModal(modalHeading('BATOH S VAJÍČKAMI','Objav sa ukrýva vo vnútri.',`Miesto v batohu: ${state.eggs.length} / ${state.capacity||3}. Vajíčka liahneš v inkubátore na svojej základni.`)+`<div class="panel-grid">${state.eggs.length?state.eggs.map((e,i)=>{const def=defs.eggDefs.find(d=>d.id===e.type);return `<article class="item-card"><div class="egg-preview" style="--egg-color:${esc(def?.color||'#bceaaa')}">${icon('egg')}</div><span class="tag">VAJÍČKO 0${i+1}</span><h3>${esc(def?.name||'Vajíčko objavu')}</h3><p>Ďalší príbeh sa začína v inkubátore.</p></article>`;}).join(''):`<div class="empty-state">${icon('egg')}<h3>Batoh je zatiaľ prázdny.</h3><p>Vajíčka nájdeš na centrálnom námestí.</p></div>`}</div><div class="modal-actions"><button class="secondary-btn" data-waypoint="hatch">Cesta k inkubátoru</button><button class="primary-btn" data-action="hatch" ${!state.eggs.length||!nearHatch?'disabled':''}>Vyliahnuť prvé vajíčko</button></div>`,'eggs');
  }
  if(panel==='upgrades'){
    openModal(modalHeading('TVOJA ZÁKLADŇA','Pripravený na ďalší level?',`Vylepšenia ti pomôžu rýchlejšie objavovať svet a budovať zbierku.`)+`<div class="panel-grid">${defs.upgradeDefs.map(u=>`<article class="item-card"><div class="upgrade-symbol">${icon(u.id==='capacity'?'egg':u.id==='income'?'coin':'arrow')}</div><span class="tag">ÚROVEŇ ${state.upgrades[u.id]||0} / ${u.maxLevel}</span><h3>${esc(u.name)}</h3><p>${esc(u.description)}</p><button class="primary-btn" data-upgrade="${u.id}" ${(state.upgrades[u.id]>=u.maxLevel||state.player.coins<u.cost)?'disabled':''}>${state.upgrades[u.id]>=u.maxLevel?'Maximálna úroveň':icon('coin')+' '+compact(u.cost)+' · Vylepšiť'}</button></article>`).join('')}</div><div class="prestige-panel"><div><h3>Nová éra akadémie</h3><p>Po získaní 6 spoločníkov a 12 poznatkov môžeš začať znovu s trvalým bonusom k príjmu.</p></div><button class="secondary-btn" data-action="prestige-prompt" ${state.pets.length<6||state.player.knowledge<12?'disabled':''}>Nová éra</button></div>`,'upgrades');
  }
  if(panel==='journal'){
    const questions=defs.questions||defs.quizQuestions||[];
    const entries=(state.journal||[]).filter(e=>e.type==='lesson'||e.explanation);
    let learned=state.progress.solved.map(id=>questions.find(q=>q.id===id)).filter(Boolean);
    if(!learned.length&&entries.length)learned=entries;
    openModal(modalHeading('DENNÍK OBJAVOV','Vedomosti si vezmeš so sebou.',`Objavené poznatky: ${state.progress.solved.length}. Každá správna odpoveď má vysvetlenie.`)+`<div class="journal-list">${learned.length?learned.map(q=>`<article class="journal-card"><span class="tag">${esc(subjectLabels[q.subject]||q.subject||'Objav')}</span><h3>${esc(q.title||q.concept)}</h3><p>${esc(q.explanation||q.text)}</p></article>`).join(''):`<div class="empty-state">${icon('book')}<h3>Tvoj prvý poznatok čaká.</h3><p>Navštív matematický pavilón, laboratórium alebo prírodný archív.</p><button class="primary-btn" data-waypoint="quiz-math">Cesta k matematike</button></div>`}</div>`,'journal');
  }
}
function distance(t){return Math.hypot(position.x-t.position.x,position.z-t.position.z);}
let waypoint=null;
function showWaypoint(id){const t=world.interactables.find(x=>x.id===id);if(!t)return;waypoint=t;closeModal();notify(`Sleduj modrú šípku: ${t.name}`);}
function interact(){
  if(modalOpen||!currentTarget)return;
  const target=currentTarget;
  if(target.type==='egg'){
    const def=engine.getDefinitions().eggDefs.find(e=>e.id===(target.eggId||target.id));
    if(!def){notify('Toto vajíčko ešte nie je pripravené.','warning');return;}
    const state=engine.getState(),locked=state.player.knowledge<def.knowledge;
    openModal(modalHeading('TRH OBJAVOV',esc(def.name),esc(def.description))+`<div class="egg-shop-preview"><div class="egg-preview" style="--egg-color:${esc(def.color)}">${icon('egg')}</div><div><span class="tag">${locked?'ZATIAĽ ZAMKNUTÉ':'PRIPRAVENÉ NA OBJAV'}</span><p>${def.knowledge} poznatkov na odomknutie</p><strong>${icon('coin')} ${compact(def.price)} mincí</strong></div></div><p class="small-note">Získaš jedného náhodného spoločníka. Mince sú herné a získavaš ich hraním.</p><div class="modal-actions"><button class="secondary-btn" data-action="close">Ešte sa rozhliadnem</button><button class="primary-btn" data-buy="${def.id}" ${locked||state.player.coins<def.price||state.eggs.length>=state.capacity?'disabled':''}>${locked?icon('lock')+' Získaj viac poznatkov':'Kúpiť vajíčko '+icon('arrow')}</button></div>`,'shop');
  }
  if(target.type==='hatch')showPanel('eggs');
  if(target.type==='upgrade')showPanel('upgrades');
  if(target.type==='quiz')showQuiz(target.subject||target.id.replace('quiz-',''));
  if(target.type==='portal'){
    if(!stateCache.progress.unlocked.includes('astral')){notify('Astrálna brána sa otvorí po 8 nových poznatkoch.','warning');return;}
    showQuiz('science');notify('Astrálny výskum: čas na ďalší vedecký objav.');
  }
}
function showQuiz(subject){
  const q=engine.getQuiz(subject);if(!q){notify('Tento archív sa ešte pripravuje.');return;}
  openModal(modalHeading('TERMINÁL POZNANIA',esc(q.title),`Nový objav = 50 mincí + 1 poznatok. Správna odpoveď ti otvorí ďalšie možnosti.`)+`<p class="question">${esc(q.question)}</p><div class="quiz-choices">${q.options.map((o,i)=>`<button class="choice" data-answer="${o.id}"><span class="choice-letter">${String.fromCharCode(65+i)}</span><span>${esc(o.label)}</span></button>`).join('')}</div><div id="quiz-feedback" class="answer-feedback" aria-live="polite"></div><div class="quiz-footer"><button class="secondary-btn" data-action="hint">${icon('book')} Nápoveda</button><span>Učenie je súčasť dobrodružstva.</span></div><p id="quiz-hint" class="hint-box" hidden></p>`,'quiz');activeQuiz=q;
}
function answer(value){if(!activeQuiz)return;const q=activeQuiz,r=engine.answerQuiz(q.id,value);chime(r.ok);const feedback=$('#quiz-feedback');
  if(r.ok){feedback.className='answer-feedback correct';feedback.innerHTML=`<strong>${icon('check')} Objav potvrdený!</strong><p>${esc(q.explanation)}</p><div class="modal-actions"><button class="secondary-btn" data-action="close">Späť do sveta</button><button class="primary-btn" data-next-quiz="${q.subject}">Ďalší objav ${icon('arrow')}</button></div>`;document.querySelectorAll('.choice').forEach(b=>b.disabled=true);$('button',feedback)?.focus();}
  else{feedback.className='answer-feedback incorrect';feedback.innerHTML=`<strong>Premysli to ešte raz.</strong><p>${esc(r.message||'Pomôže ti nápoveda. Môžeš skúsiť znova.')}</p>`;}
}
function hatch(){const near=world.interactables.some(t=>t.type==='hatch'&&distance(t)<t.radius+1);if(!near){notify('Vajíčko vyliahneš pri inkubátore na základni.','warning');return;}const r=engine.hatchEgg();if(!r.ok){action(r);return;}chime();const p=r.pet;openModal(`<div class="hatch-celebration">${petArt(p)}</div><span class="modal-eyebrow">NOVÝ SPOLOČNÍK</span><h2>${esc(p.name)} sa pridáva k tebe!</h2><div class="hatch-rarity rarity-${p.rarity}">${rarityLabels[p.rarity]} · +${p.income} mincí / s</div><p class="modal-description">Na základni ti pomôže vyrábať mince. Vezmi ho na ďalší objav.</p><div class="modal-actions"><button class="secondary-btn" data-action="close">Pokračovať</button><button class="primary-btn" data-equip="${esc(p.id)}">Vziať so sebou ${icon('arrow')}</button></div>`,'hatch');spawnBurst();}
function help(){openModal(modalHeading('VITAJ V AKADÉMII','Tvoj svet. Tvoje pravidlá.',`Farebný 3D svet, v ktorom každý objav posúva tvoju základňu ďalej.`)+`<ol class="help-steps"><li><strong>Objavuj svet.</strong> WASD alebo šípky = pohyb. Medzerník = skok. Ťahanie myšou = kamera. Koliesko = priblíženie.</li><li><strong>Zbieraj vajíčka.</strong> Pri centrálnom trhu stlač E a kúp vajíčko za herné mince.</li><li><strong>Vyliahni spoločníka.</strong> Zájdi do svojej základne a aktivuj inkubátor. Spoločníci vyrábajú mince automaticky.</li><li><strong>Uč sa a odomykaj.</strong> Tri pavilóny ponúkajú otázky z matematiky, prírody a vedy. Nové poznatky odomknú vzácnejšie vajíčka.</li><li><strong>Rozvíjaj základňu.</strong> Vylepši pohyb, kapacitu batoha a príjem.</li></ol><div class="save-note">Sólo svet · Postup sa automaticky ukladá v tomto prehliadači.</div><button class="primary-btn" data-action="close">Poďme objavovať ${icon('arrow')}</button>`);}
function profile(){openModal(modalHeading('PROFIL OBJAVITEĽA','Ako sa voláš?',`Tvoje meno bude súčasťou akadémie.`)+`<form id="profile-form"><label for="profile-name">Meno objaviteľa</label><input id="profile-name" maxlength="24" value="${esc(stateCache.player.name)}" required><button class="primary-btn" type="submit">Uložiť meno</button></form><div class="save-note">Postup je uložený lokálne. Úroveň ${stateCache.player.level} · ${stateCache.player.xp} XP</div><button class="text-btn" data-action="reset-prompt">Začať nový svet</button>`);$('#profile-form').onsubmit=e=>{e.preventDefault();action(engine.setName($('#profile-name').value));closeModal();};}
function bindUI(){
  document.addEventListener('click',e=>{const b=e.target.closest('button');if(!b)return;
    if(b.dataset.panel){showPanel(b.dataset.panel);return;}
    if(b.dataset.buy){const r=action(engine.buyEgg(b.dataset.buy));if(r.ok){closeModal();showWaypoint('hatch');}return;}
    if(b.dataset.equip){action(engine.equipPet(b.dataset.equip));if(modalPanel==='pets')showPanel('pets');else closeModal();return;}
    if(b.dataset.upgrade){action(engine.upgrade(b.dataset.upgrade));showPanel('upgrades');return;}
    if(b.dataset.answer){answer(b.dataset.answer);return;}
    if(b.dataset.nextQuiz){showQuiz(b.dataset.nextQuiz);return;}
    if(b.dataset.waypoint){showWaypoint(b.dataset.waypoint);return;}
    switch(b.dataset.action){case 'close':closeModal();break;case 'start':$('#welcome-card').hidden=true;$('#game-canvas').focus();showWaypoint('egg-basic');break;case 'hatch':hatch();break;case 'hint':if(activeQuiz){$('#quiz-hint').hidden=false;$('#quiz-hint').textContent=activeQuiz.hint;}break;
      case 'reset-prompt':openModal(modalHeading('NOVÝ SVET','Začať úplne od začiatku?',`Vymažeš svoje mince, vajíčka, spoločníkov aj poznatky v tomto prehliadači.`)+`<div class="modal-actions"><button class="secondary-btn" data-action="close">Zachovať svet</button><button class="primary-btn" data-action="reset">Vymazať a začať</button></div>`);break;
      case 'reset':engine.reset();position.set(0,0,13);closeModal();notify('Nový príbeh začína.');break;
      case 'prestige-prompt':openModal(modalHeading('NOVÁ ÉRA','Prebudovať akadémiu?',`Vajíčka, spoločníci, mince a vylepšenia sa obnovia. Vedomosti si ponecháš a príjem získa trvalý bonus.`)+`<div class="modal-actions"><button class="secondary-btn" data-action="close">Ešte objavovať</button><button class="primary-btn" data-action="prestige">Začať novú éru</button></div>`);break;
      case 'prestige':action(engine.prestige());closeModal();break;
    }
  });
  $('#help-btn')?.addEventListener('click',help);$('#profile-btn')?.addEventListener('click',profile);$('#interact-btn')?.addEventListener('click',interact);$('#jump-btn')?.addEventListener('click',jump);
  $('#sound-btn')?.addEventListener('click',()=>{engine.setSetting('sound',!stateCache.settings.sound);notify(stateCache.settings.sound?'Zvuk zapnutý':'Zvuk vypnutý');chime();});
  window.addEventListener('keydown',e=>{
    if(modalOpen){if(e.key==='Escape')closeModal();if(e.key==='Tab'){const nodes=[...document.querySelectorAll('#modal-root button:not([disabled]),#modal-root input')],a=nodes[0],b=nodes.at(-1);if(e.shiftKey&&document.activeElement===a){e.preventDefault();b?.focus();}else if(!e.shiftKey&&document.activeElement===b){e.preventDefault();a?.focus();}}return;}
    if(/^(INPUT|TEXTAREA|BUTTON|A|SELECT)$/.test(e.target.tagName))return;
    const k=e.key.toLowerCase();if(['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright',' '].includes(k)){e.preventDefault();keys.add(k);}if(k===' '&&!e.repeat)jump();if(k==='e'&&!e.repeat)interact();if(k==='escape')help();if(['1','2','3','4'].includes(k)&&!e.repeat)showPanel(['pets','eggs','upgrades','journal'][Number(k)-1]);
  });
  window.addEventListener('keyup',e=>keys.delete(e.key.toLowerCase()));window.addEventListener('blur',()=>{keys.clear();joystick.x=joystick.y=0;pointer=null;});
  document.addEventListener('visibilitychange',()=>{keys.clear();joystick.x=joystick.y=0;lastTime=performance.now();});
  const canvas=$('#game-canvas');canvas.addEventListener('contextmenu',e=>e.preventDefault());
  canvas.addEventListener('pointerdown',e=>{if(modalOpen)return;canvas.focus();pointer={id:e.pointerId,x:e.clientX,y:e.clientY};canvas.setPointerCapture(e.pointerId);});
  canvas.addEventListener('pointermove',e=>{if(!pointer||pointer.id!==e.pointerId)return;yaw-=(e.clientX-pointer.x)*.006;pitch=THREE.MathUtils.clamp(pitch+(e.clientY-pointer.y)*.004,.22,1.1);pointer.x=e.clientX;pointer.y=e.clientY;});
  const stop=()=>pointer=null;canvas.addEventListener('pointerup',stop);canvas.addEventListener('pointercancel',stop);
  canvas.addEventListener('wheel',e=>{e.preventDefault();cameraDistance=THREE.MathUtils.clamp(cameraDistance+e.deltaY*.018,12,32);},{passive:false});
  const stick=$('#joystick'),knob=$('#joystick-stick');let joyId=null;
  const updateJoy=e=>{const rect=stick.getBoundingClientRect(),dx=e.clientX-rect.left-rect.width/2,dy=e.clientY-rect.top-rect.height/2,limit=rect.width*.32,len=Math.hypot(dx,dy),factor=len>limit?limit/len:1;joystick.x=dx*factor/limit;joystick.y=dy*factor/limit;knob.style.transform=`translate(${dx*factor}px,${dy*factor}px)`;};
  stick?.addEventListener('pointerdown',e=>{joyId=e.pointerId;stick.setPointerCapture(joyId);updateJoy(e);});stick?.addEventListener('pointermove',e=>{if(e.pointerId===joyId)updateJoy(e);});
  const stopJoy=()=>{joyId=null;joystick.x=joystick.y=0;knob.style.transform='translate(0,0)';};stick?.addEventListener('pointerup',stopJoy);stick?.addEventListener('pointercancel',stopJoy);
}
function jump(){if(onGround&&!modalOpen){verticalVelocity=8.2;onGround=false;}}
function blocked(x,z){const r=.6;return world.colliders.some(c=>x+r>c.x-c.hx&&x-r<c.x+c.hx&&z+r>c.z-c.hz&&z-r<c.z+c.hz);}
function updatePlayer(dt){
  let x=(keys.has('d')||keys.has('arrowright')?1:0)-(keys.has('a')||keys.has('arrowleft')?1:0)+joystick.x;
  let z=(keys.has('s')||keys.has('arrowdown')?1:0)-(keys.has('w')||keys.has('arrowup')?1:0)+joystick.y;
  if(modalOpen||document.hidden){x=0;z=0;}
  const length=Math.hypot(x,z);if(length>1){x/=length;z/=length;}
  const speed=7.2*(stateCache.movementMultiplier||1);
  const dx=(Math.cos(yaw)*x+Math.sin(yaw)*z)*speed*dt,dz=(-Math.sin(yaw)*x+Math.cos(yaw)*z)*speed*dt;
  if(!blocked(position.x+dx,position.z))position.x=THREE.MathUtils.clamp(position.x+dx,world.bounds.minX+1,world.bounds.maxX-1);
  if(!blocked(position.x,position.z+dz))position.z=THREE.MathUtils.clamp(position.z+dz,world.bounds.minZ+1,world.bounds.maxZ-1);
  verticalVelocity-=22*dt;position.y+=verticalVelocity*dt;if(position.y<=0){position.y=0;verticalVelocity=0;onGround=true;}
  player.position.copy(position);
  if(length>.1){const angle=Math.atan2(-dx,-dz);let delta=angle-player.rotation.y;delta=Math.atan2(Math.sin(delta),Math.cos(delta));player.rotation.y+=delta*Math.min(1,dt*12);}
  const stride=length>.1?Math.sin(clockTime*12)*.5:0;limbs.arms[0].rotation.x=stride;limbs.arms[1].rotation.x=-stride;limbs.legs[0].rotation.x=-stride;limbs.legs[1].rotation.x=stride;
  if(!onGround){limbs.arms[0].rotation.z=.4;limbs.arms[1].rotation.z=-.4;}else limbs.arms.forEach(a=>a.rotation.z=0);
  if(follower){const behind=new THREE.Vector3(position.x+Math.sin(player.rotation.y)*2.8,0,position.z+Math.cos(player.rotation.y)*2.8);follower.position.lerp(behind,1-Math.exp(-dt*4));follower.position.y=.2+Math.sin(clockTime*5)*.13;follower.rotation.y=player.rotation.y;}
}
function updateCamera(dt){cameraLook.set(position.x,position.y+1.9,position.z);smoothLook.lerp(cameraLook,1-Math.exp(-dt*10));const horizontal=Math.cos(pitch)*cameraDistance;desiredCamera.set(smoothLook.x+Math.sin(yaw)*horizontal,smoothLook.y+Math.sin(pitch)*cameraDistance,smoothLook.z+Math.cos(yaw)*horizontal);camera.position.lerp(desiredCamera,1-Math.exp(-dt*9));camera.lookAt(smoothLook);}
let targetRing,waypointArrow;
function updateInteraction(){let nearest=null,min=Infinity;for(const t of world.interactables){const d=distance(t);if(d<t.radius&&d<min){nearest=t;min=d;}}currentTarget=nearest;const id=nearest?.id||'';if(id!==interactionId){interactionId=id;$('#interaction-prompt').hidden=!nearest;if(nearest){$('#interaction-name').textContent=nearest.name;$('#interact-btn').setAttribute('aria-label',`Preskúmať: ${nearest.name}`);} }
  targetRing.visible=!!nearest;if(nearest){targetRing.position.set(nearest.position.x,.27,nearest.position.z);targetRing.rotation.z=clockTime*.3;}
  const zone=position.x<-8&&position.z>2?'Tvoja základňa':position.z<-24?'Astrálna brána':position.x>8&&position.z<-6?'Vedecké laboratórium':position.x<-8&&position.z<-6?'Prírodný archív':position.x>8?'Matematický pavilón':'Námestie objavov';$('#zone-label').textContent=zone;
  if(waypoint){const d=distance(waypoint);if(d<waypoint.radius){waypoint=null;waypointArrow.visible=false;notify('Si na mieste. Stlač E alebo tlačidlo Preskúmať.');}else{waypointArrow.visible=true;waypointArrow.position.set(position.x,4+Math.sin(clockTime*3)*.1,position.z);waypointArrow.rotation.y=Math.atan2(waypoint.position.x-position.x,waypoint.position.z-position.z);}}
}
function drawMinimap(){const c=$('#minimap-canvas');if(!c)return;const ctx=c.getContext('2d'),w=c.width,h=c.height;ctx.clearRect(0,0,w,h);ctx.fillStyle='#edf7df';ctx.fillRect(0,0,w,h);const to=(x,z)=>[w/2+x*1.7,h/2+z*1.7];ctx.strokeStyle='#e4d1b2';ctx.lineWidth=12;ctx.beginPath();ctx.moveTo(w/2,8);ctx.lineTo(w/2,h-8);ctx.moveTo(8,h/2);ctx.lineTo(w-8,h/2);ctx.stroke();for(const t of world.interactables){const [x,y]=to(t.position.x,t.position.z);ctx.fillStyle=t.type==='egg'?'#e7b952':t.type==='quiz'?'#9b90dc':t.type==='hatch'?'#53b7bc':'#bba7cc';ctx.beginPath();ctx.arc(x,y,t.type==='quiz'?4:3,0,Math.PI*2);ctx.fill();}const [x,y]=to(position.x,position.z);ctx.fillStyle='#2b3551';ctx.beginPath();ctx.arc(x,y,4,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#2b3551';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x-Math.sin(yaw)*8,y-Math.cos(yaw)*8);ctx.stroke();}
const particles=[];
function spawnBurst(){for(let i=0;i<28;i++){const mesh=new THREE.Mesh(new THREE.IcosahedronGeometry(.11,0),material(['#ffd968','#af94ee','#69d1c4'][i%3]));mesh.position.copy(position).add(new THREE.Vector3(0,2,0));scene.add(mesh);particles.push({mesh,v:new THREE.Vector3((Math.random()-.5)*8,Math.random()*7+2,(Math.random()-.5)*8),life:1.5});}}
function updateParticles(dt){for(let i=particles.length-1;i>=0;i--){const p=particles[i];p.life-=dt;p.v.y-=8*dt;p.mesh.position.addScaledVector(p.v,dt);p.mesh.rotation.x+=dt*4;if(p.life<=0){scene.remove(p.mesh);p.mesh.geometry.dispose();particles.splice(i,1);}}}
let minimapTime=0;
function frame(time){requestAnimationFrame(frame);if(document.hidden){lastTime=time;return;}const elapsed=Math.max(0,(time-lastTime)/1000),dt=Math.min(.05,elapsed);lastTime=time;clockTime+=dt;engine.tick(elapsed);updatePlayer(dt);updateCamera(dt);world.update(dt,clockTime,stateCache);updateInteraction();updateParticles(dt);minimapTime+=dt;if(minimapTime>.15){drawMinimap();minimapTime=0;}renderer.render(scene,camera);}
function init(){
  const canvas=$('#game-canvas');engine=new AcademyEngine();stateCache=engine.getState();
  renderer=new THREE.WebGLRenderer({canvas,antialias:true,powerPreference:'high-performance'});renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFShadowMap;renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.05;
  scene=new THREE.Scene();scene.background=new THREE.Color('#a8d9f3');scene.fog=new THREE.Fog('#b5e2f6',50,130);
  scene.add(new THREE.HemisphereLight('#e7f4ff','#a2b972',1.8));const sun=new THREE.DirectionalLight('#fff4dd',2.6);sun.position.set(-25,45,22);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);sun.shadow.camera.left=-52;sun.shadow.camera.right=52;sun.shadow.camera.top=52;sun.shadow.camera.bottom=-52;sun.shadow.camera.near=1;sun.shadow.camera.far=120;sun.shadow.bias=-.0005;sun.shadow.normalBias=.025;scene.add(sun);sun.target.position.set(0,0,-5);scene.add(sun.target);
  camera=new THREE.PerspectiveCamera(50,1,.1,220);world=createAcademyWorld(scene);position.set(world.spawn.x,0,world.spawn.z);const character=createCharacter();player=character.group;limbs=character;
  const ringGeo=new THREE.RingGeometry(1.75,1.9,48);targetRing=new THREE.Mesh(ringGeo,new THREE.MeshBasicMaterial({color:'#fff2ab',transparent:true,opacity:.85,side:THREE.DoubleSide}));targetRing.rotation.x=-Math.PI/2;targetRing.visible=false;scene.add(targetRing);
  waypointArrow=new THREE.Group();const arrow=box(.25,.15,1.2,'#538bee',0,0,0),tip=new THREE.Mesh(new THREE.ConeGeometry(.45,.75,3),material('#538bee'));tip.rotation.x=Math.PI/2;tip.position.z=.75;waypointArrow.add(arrow,tip);waypointArrow.visible=false;scene.add(waypointArrow);
  engine.subscribe(syncState);syncState(stateCache);bindUI();
  if(stateCache.player.xp>0||stateCache.pets.length>0||stateCache.eggs.length>0)$('#welcome-card').hidden=true;
  const resize=()=>{renderer.setSize(window.innerWidth,window.innerHeight,false);camera.aspect=window.innerWidth/window.innerHeight;camera.updateProjectionMatrix();};window.addEventListener('resize',resize);resize();smoothLook.set(position.x,1.9,position.z);camera.position.set(position.x,14,position.z+19);updateCamera(1);renderer.render(scene,camera);$('#loading-overlay').hidden=true;lastTime=performance.now();requestAnimationFrame(frame);
  canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();$('#loading-overlay').hidden=false;if($('#load-error'))$('#load-error').textContent='Grafický kontext sa prerušil. Obnov stránku; postup je uložený.';});
}
try{init();}catch(error){console.error('LUMEN startup:',error);const node=$('#load-error');if(node){node.hidden=false;node.innerHTML='3D svet sa nepodarilo spustiť. Skontroluj podporu WebGL a obnov stránku.<br><button class="primary-btn" id="retry-load">Skúsiť znovu</button>';$('#retry-load').onclick=()=>location.reload();} }
