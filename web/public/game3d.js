import * as THREE from './vendor/three.module.js';
import { AcademyEngine } from './academy-engine.js';
import { createAcademyWorld } from './scene3d.js';
import { stepMotion, findWalkingPath, clipCameraLine } from './physics3d.js';
import { adventurePanel, masteryCards, rarityOdds } from './adventure-ui.js';
import { createMultiplayer } from './multiplayer.js';
import { buildArena } from './arena3d.js';
import { createExplorer, createCompanion } from './avatar3d.js';
import { createCoop } from './co-op.js';
import { createPartyWorld } from './party3d.js';
import { createVRMode } from './vr-mode.js';
import { createVRPanel } from './vr-panel.js';
import { AGE_QUESTIONS } from './curriculum.js';
import { AGE_LABELS, OUTFITS, SKINS, tutorialStep, collectionEntries } from './academy-journey.js';
import { createAcademyShell } from './academy-shell.js';

const $ = (selector,parent=document) => parent.querySelector(selector);
const esc = text => String(text ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const icons = {coin:'<circle cx="12" cy="12" r="9"/><path d="M14.5 7h-4a2 2 0 0 0 0 4h3a2 2 0 0 1 0 4h-4M12 5v14"/>',book:'<path d="M12 5c-3-2-7-2-10-1v15c3-1 7-1 10 1 3-2 7-2 10-1V4c-3-1-7-1-10 1v15Z"/>',egg:'<path d="M20 15c0 5-4 7-8 7s-8-2-8-7C4 9 9 2 12 2s8 7 8 13Z"/><path d="m7 12 3 2 3-3 4 2"/>',star:'<path d="m12 2 3 7 7 3-7 3-3 7-3-7-7-3 7-3 3-7Z"/>',arrow:'<path d="M4 12h16m-6-6 6 6-6 6"/>',check:'<path d="m5 12 4 4L19 6"/>',close:'<path d="m6 6 12 12M6 18 18 6"/>',lock:'<rect x="5" y="10" width="14" height="11" rx="3"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/>'};
const icon=(name)=>`<svg class="ui-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[name]||icons.star}</svg>`;
const rarityLabels={common:'Bežný',uncommon:'Neobyčajný',rare:'Vzácny',epic:'Epický',legendary:'Legendárny'};
const subjectLabels={math:'Matematika',science:'Veda a vesmír',nature:'Príroda',history:'Dejiny a spoločnosť',language:'Jazyky a logika',technology:'Digitálny svet',astral:'Astrálny výskum'};
const beaconFacts={
  'beacon-1':['Svetlo','Svetlo zo Slnka letí k Zemi približne 8 minút.'],
  'beacon-2':['Voda','Voda môže byť pevná, kvapalná aj plynná.'],
  'beacon-3':['Čísla','Obvod štvorca je štyrikrát dĺžka jeho strany.'],
  'beacon-4':['Les','Rastliny pri fotosyntéze využívajú svetlo a uvoľňujú kyslík.'],
  'beacon-5':['Vesmír','Zem obehne Slnko približne za 365 dní.'],
  'beacon-6':['Energia','Energia sa môže meniť medzi rôznymi formami.']
};
const compact = n => new Intl.NumberFormat('sk-SK',{notation:n>=10000?'compact':'standard',maximumFractionDigits:1}).format(n);
let engine, renderer, scene, camera, world, player, limbs, follower, followerId, currentTarget, modalPanel, activeQuiz, multiplayer, coop, party, vr, vrPanel, modalOpen=false;
let yaw=0, pitch=.39, cameraDistance=20, clockTime=0, lastTime=0, verticalVelocity=0, onGround=true;
const keys = new Set(), joystick={x:0,y:0};
const position = new THREE.Vector3(0,0,13), velocity=new THREE.Vector3();
const desiredCamera = new THREE.Vector3(), cameraLook=new THREE.Vector3(), smoothLook=new THREE.Vector3();
let pointer, audio, stateCache, lastQuestSignature='', interactionId='', previousFocus, graphicsLost=false, sun, ambient, menuOpen=false, relocating=false, localEmoteUntil=0;
let journalFilter='all', courseRun=null, lastPlatform=null, qualityApplied='', effectiveQuality='auto', autoFrameTime=0, autoFrames=0, shadowTimer=0;
let collectedIds=new Set();
const followerDestination=new THREE.Vector3();
let vrInput={x:0,z:0,yaw:0}, vrStatus={supported:false,checking:true,message:'Overujeme podporu VR headsetu…'};
const materialCache=new Map(), geometries=new Map();
let shell, tutorialTravel=0, companionTravel=0, petTab='owned';
function material(color){if(!materialCache.has(color))materialCache.set(color,new THREE.MeshStandardMaterial({color,roughness:.7}));return materialCache.get(color);}
function box(w,h,d,color,x=0,y=0,z=0){const key=[w,h,d].join(',');if(!geometries.has(key))geometries.set(key,new THREE.BoxGeometry(w,h,d));const mesh=new THREE.Mesh(geometries.get(key),material(color));mesh.position.set(x,y,z);mesh.castShadow=true;mesh.receiveShadow=true;return mesh;}
function notify(message,kind='info'){if(!message)return;const div=document.createElement('div');div.className=`game-toast ${kind}`;div.setAttribute('role','status');div.textContent=message;$('#toast-root').append(div);setTimeout(()=>div.remove(),4200);}
function chime(good=true){if(!stateCache?.settings.sound)return;try{audio ||=new (window.AudioContext||window.webkitAudioContext)();audio.resume();[good?523:196,good?784:165].forEach((freq,i)=>{const oscillator=audio.createOscillator(),gain=audio.createGain();const t=audio.currentTime+i*.12;oscillator.frequency.value=freq;gain.gain.setValueAtTime(.0001,t);gain.gain.exponentialRampToValueAtTime(.045,t+.01);gain.gain.exponentialRampToValueAtTime(.0001,t+.25);oscillator.connect(gain).connect(audio.destination);oscillator.start(t);oscillator.stop(t+.26);});}catch{}}
function action(result){notify(result.message,result.ok?'success':'warning');chime(result.ok);return result;}
function createCharacter(){
  const rig=createExplorer(THREE,{color:OUTFITS[stateCache.player.outfit]||OUTFITS[0],skin:SKINS[stateCache.player.skin]||SKINS[0]});scene.add(rig.group);return rig;
}
function makePetModel(pet){
  return createCompanion(THREE,pet);
}
function petArt(pet){
  const color=/^#[\da-f]{6}$/i.test(pet.color||'')?pet.color:'#c8aff5',kind=pet.kind||'bunny';
  const crown=kind==='bunny'?`<path d="M34 45V21q10-13 18 9v16M68 44V27q9-19 18-7v24" fill="${color}"/><path d="M42 38V24m35 14V24" stroke="#ffc3cf" stroke-width="6" stroke-linecap="round"/>`:kind==='robot'?'<path d="M60 39V19" stroke="#536a86" stroke-width="5"/><circle cx="60" cy="17" r="7" fill="#ffd052"/>':`<path d="m32 47 4-24 19 19m10 0 19-19 5 24" fill="${color}" stroke="${color}" stroke-width="5" stroke-linejoin="round"/>`;
  const wings=['owl','dragon'].includes(kind)?`<path d="M31 62 8 48l5 35 23-5m49-16 25-14-5 35-22-5" fill="${kind==='dragon'?'#86cfa1':color}"/>`:kind==='fox'||kind==='cat'?`<path d="M84 82q26-5 19-32q-17 8-13 24" fill="${color}" stroke="${color}" stroke-width="8"/><path d="m101 52 3 9" stroke="#fff2da" stroke-width="8"/>`:'';
  const face=kind==='robot'?'<rect x="35" y="51" width="50" height="24" rx="6" fill="#536a86"/><rect x="40" y="56" width="40" height="14" rx="4" fill="#5bdedc"/>':kind==='owl'?'<circle cx="45" cy="63" r="14" fill="#fff2da"/><circle cx="75" cy="63" r="14" fill="#fff2da"/><path d="m54 78 6 8 6-8" fill="#ffd052"/>':kind==='fox'?'<path d="M32 66q28 27 56 0v13q-28 20-56 0Z" fill="#fff2da"/>':'';
  const markings=kind==='cat'?'<path d="m25 72 14 3m-14 6 14-2m42-4 14-3m-14 7 14 2" stroke="#536a86" stroke-width="2" stroke-linecap="round"/>':kind==='dragon'?'<path d="m45 40 2-14 8 14m10 0 8-14 2 14" fill="#ffd052"/>':'';
  return `<svg class="pet-illustration" viewBox="0 0 120 110" aria-hidden="true"><ellipse cx="60" cy="97" rx="37" ry="6" fill="#242942" opacity=".1"/>${wings}${crown}<rect x="30" y="41" width="60" height="48" rx="${kind==='robot'?9:15}" fill="${color}"/>${face}<rect x="40" y="56" width="9" height="13" rx="4" fill="#253047"/><rect x="71" y="56" width="9" height="13" rx="4" fill="#253047"/><path d="m54 73 6 5 6-5" stroke="#253047" stroke-width="3" fill="none" stroke-linecap="round"/>${markings}<rect x="35" y="85" width="19" height="10" rx="4" fill="${color}"/><rect x="67" y="85" width="19" height="10" rx="4" fill="${color}"/></svg>`;
}
function syncState(state,result={}){
  const previousState=stateCache;
  stateCache=state;
  if(result.event==='explorer-changed'&&player){const angle=player.rotation.y;limbs.dispose();limbs=createCharacter();player=limbs.group;player.position.copy(position);player.rotation.y=angle;}
  $('#coins-count').textContent=compact(state.player.coins);$('#knowledge-count').textContent=state.player.knowledge;
  $('#income-count').textContent=`+${compact(state.incomePerSecond||0)}/s`;
  if($('#player-level'))$('#player-level').textContent=state.player.level;
  if($('#xp-progress'))$('#xp-progress').style.width=`${(state.player.xpInLevel??state.player.xp%100)}%`;
  if($('#xp-label'))$('#xp-label').textContent=`${state.player.xpInLevel??state.player.xp%100} / 100 XP`;
  collectedIds=new Set(state.progress.collected||[]);
  const expeditions=engine.getDefinitions().expeditions||[],expedition=expeditions.find(q=>q.complete&&!q.claimed)||expeditions.find(q=>!q.claimed);
  if($('#next-goal-text'))$('#next-goal-text').textContent=expedition?`${expedition.complete?'Vyzdvihni odmenu: ':''}${expedition.title} · ${Math.min(expedition.current,expedition.target)}/${expedition.target}`:'Všetky výpravy splnené. Objavuj ďalej!';
  if($('#player-name'))$('#player-name').textContent=state.player.name;
  $('#sound-btn')?.setAttribute('aria-pressed',String(state.settings.sound));
  const quests=[{title:'Prvý spoločník',text:'Vyliahni vajíčko na základni',value:Math.min(1,state.stats.hatched),target:1},{title:'Mladý vedec',text:'Objav tri nové poznatky',value:Math.min(3,state.progress.solved.length),target:3},{title:'Živá základňa',text:'Získaj štyroch spoločníkov',value:Math.min(4,state.pets.length),target:4}];
  if($('#quest-count'))$('#quest-count').textContent=`${quests.filter(q=>q.value>=q.target).length}/3`;
  const signature=quests.map(q=>q.value).join(',');
  if(signature!==lastQuestSignature){lastQuestSignature=signature;$('#quest-list').innerHTML=quests.map(q=>`<div class="quest-item ${q.value>=q.target?'completed':''}"><span class="quest-icon">${icon(q.value>=q.target?'check':'star')}</span><div class="quest-copy"><strong class="quest-title">${q.title}</strong><span class="quest-meta">${q.text}</span><div class="quest-progress"><i style="width:${q.value/q.target*100}%"></i></div></div><span class="quest-count">${q.value}/${q.target}</span></div>`).join('');}
  world?.setState(state);
  if(renderer&&state.settings.graphics!==qualityApplied)applyGraphics(state.settings.graphics||'auto');
  if(modalOpen){
    const definitions=engine.getDefinitions();
    document.querySelectorAll('[data-buy]').forEach(b=>{const d=definitions.eggDefs.find(e=>e.id===b.dataset.buy);if(d)b.disabled=state.player.knowledge<d.knowledge||state.player.coins<d.price||state.eggs.length>=state.capacity;});
    document.querySelectorAll('[data-upgrade]').forEach(b=>{const d=definitions.upgradeDefs.find(u=>u.id===b.dataset.upgrade);if(d)b.disabled=state.upgrades[d.id]>=d.maxLevel||state.player.coins<d.cost;});
    document.querySelectorAll('[data-train]').forEach(b=>{const p=state.pets.find(p=>p.id===b.dataset.train);if(p)b.disabled=p.level>=5||state.player.coins<p.trainCost;});
  }
  const equipped=state.pets.find(p=>p.id===state.equippedPet),followerSignature=equipped?JSON.stringify([equipped.id,equipped.kind,equipped.color,equipped.name,equipped.level]):null;
  if(followerSignature!==followerId){
    if(follower){scene.remove(follower);follower.userData.dispose?.();follower=null;}followerId=followerSignature;
    if(equipped){follower=makePetModel(equipped);follower.scale.setScalar(1+.04*((equipped.level||1)-1));follower.position.copy(position);scene.add(follower);}
  }
  if(previousState&&state.progress.unlocked.length>previousState.progress.unlocked.length)notify(state.progress.unlocked.includes('astral')?'Kozmické vajíčko a astrálna brána sú otvorené!':'Vedecké vajíčko je teraz odomknuté!','success');
  if(previousState&&state.player.level>previousState.player.level){notify(`Úroveň ${state.player.level}! Každý objav ťa posúva ďalej.`,'success');if(player)spawnBurst();}
  shell?.refresh(state);
  if(state.onboarding.active){
    const observed=state.onboarding.events;
    const milestone=state.stats.correct>0&&!observed.includes('quiz')?'quiz':(state.eggs.length>0||state.pets.length>0)&&!observed.includes('egg')?'egg':state.pets.length>0&&!observed.includes('hatch')?'hatch':null;
    if(milestone)engine.recordTutorial(milestone);
  }
  if(result.event==='tutorial-completed'){notify(result.message,'success');spawnBurst();}
}
function setMenu(open){menuOpen=Boolean(open)&&!shell?.open;$('#game-drawer').hidden=!menuOpen;$('#menu-toggle').setAttribute('aria-expanded',String(menuOpen));keys.clear();joystick.x=joystick.y=0;if(menuOpen)$('#coop-btn').focus();else $('#game-canvas').focus({preventScroll:true});}
function openModal(content,panel=null){if(menuOpen)setMenu(false);closeModal(false);modalOpen=true;document.body.classList.add('modal-open');modalPanel=panel;previousFocus=document.activeElement;keys.clear();joystick.x=joystick.y=0;
  $('#modal-root').innerHTML=`<div class="modal-backdrop"><section class="modal-card" role="dialog" aria-modal="true" aria-labelledby="modal-title"><button class="modal-close" data-action="close" aria-label="Zavrieť">${icon('close')}</button>${content}</section></div>`;
  const heading=$('#modal-root h2');if(heading)heading.id='modal-title';requestAnimationFrame(()=>($('#modal-root button:not(.modal-close):not([disabled]),#modal-root input:not([disabled])')||$('#modal-root .modal-close'))?.focus());
}
function closeModal(focus=true){$('#modal-root').innerHTML='';modalOpen=false;document.body.classList.remove('modal-open');modalPanel=null;activeQuiz=null;if(focus)(shell?.open?$('#title-play'):$('#game-canvas'))?.focus({preventScroll:true});}
function modalHeading(label,title,description){return `<span class="modal-eyebrow">${label}</span><h2>${title}</h2><p class="modal-description">${description}</p>`;}
function updateVRStatus(status){
  vrStatus=status;
  if(modalPanel!=='vr')return;
  const message=$('#vr-session-status'),enter=$('[data-action="enter-vr"]'),check=$('[data-action="check-vr"]');
  if(message){message.textContent=status.message;message.classList.toggle('ready',Boolean(status.supported));}
  if(enter){enter.disabled=!status.supported||status.checking||vr?.pending;enter.textContent=vr?.pending?'Pripájame headset…':'Vstúpiť do VR';}
  if(check)check.disabled=Boolean(status.checking||vr?.pending);
}
function showVR(){
  openModal(`<div class="vr-intro"><div class="vr-intro-mark" aria-hidden="true"><svg class="ui-icon"><use href="#i-vr"/></svg></div>${modalHeading('OSTROV OKOLO TEBA','Objavuj vo VR','Vstúp priamo na ostrov. Rozhliadaj sa hlavou, choď za kamarátmi a odpovedaj na kvízy pomocou ovládačov.')}<p id="vr-session-status" class="vr-status" role="status" aria-live="polite"></p><dl class="vr-controls"><div><dt>Ľavá páčka</dt><dd>Pohyb podľa smeru pohľadu</dd></div><div><dt>Pravá páčka</dt><dd>Otočenie o 30°</dd></div><div><dt>Spúšť</dt><dd>Vybrať odpoveď alebo preskúmať miesto</dd></div><div><dt>A</dt><dd>Skok</dd></div><div><dt>B alebo grip</dt><dd>Menu, mapa a spoločníci</dd></div></dl><div class="vr-intro-actions"><button class="primary-btn" data-action="enter-vr" disabled>Vstúpiť do VR</button><button class="secondary-btn" data-action="check-vr">Overiť headset</button></div><p class="vr-note">Otvor túto hru v prehliadači headsetu s podporou WebXR. Do spoločného sveta alebo arény sa pripoj pred vstupom do VR; meno a kódy zadávaj na obrazovke. Z VR sa vrátiš cez jeho menu.</p></div>`,'vr');
  updateVRStatus(vrStatus);void vr.checkSupport();
}
function showPanel(panel){
  const state=engine.getState(),defs=engine.getDefinitions();
  if(panel==='curriculum'){
    const descriptions={math:'Čísla, rovnice a premýšľanie v súvislostiach.',science:'Experimenty, energia a zákony vesmíru.',nature:'Živé organizmy, ekosystémy a naša planéta.',history:'Pramene, dejiny a kritické myslenie.',language:'Porozumenie, argumenty a jazykové objavy.',technology:'Algoritmy, dáta a bezpečie v digitálnom svete.'};
    const symbols=['∑','⚗','♧','⌛','Aa','⌘'];
    openModal(modalHeading('UČEBNÉ OBLASTI',`Tvoja ďalšia veľká otázka.`,`${AGE_LABELS[state.player.ageBand]} · 6 oblastí · štyri stupne náročnosti. Vekovú kategóriu môžeš zmeniť v profile.`)+`<div class="curriculum-grid">${Object.entries(descriptions).map(([id,description],i)=>{const m=state.mastery[id];return `<button class="subject-card" data-study="${id}"><span>${symbols[i]}</span><strong>${subjectLabels[id]}</strong><small>${description}</small><b>${m?.solved||0} / ${m?.total||4} poznatkov →</b></button>`;}).join('')}</div><div class="save-note">Každá otázka má vysvetlenie. Za nový poznatok získaš 50 herných mincí a 25 XP; precvičenie prináša menšiu odmenu.</div>`,'curriculum');return;
  }
  if(panel==='collection'){
    const entries=collectionEntries(defs,state.pets),found=entries.filter(p=>p.count).length;
    openModal(modalHeading('ALBUM SPOLOČNÍKOV','Každý má svoj príbeh.','Objav 14 spoločníkov v piatich vzácnostiach. Z vajíčka sa môže vyliahnuť aj už objavený kamarát.')+`<div class="collection-heading"><span>Objavené druhy</span><strong>${found} / ${entries.length}</strong></div><div class="collection-progress"><i style="width:${found/entries.length*100}%"></i></div><div class="collection-tabs"><button class="secondary-btn" data-panel="pets">Moja partia</button><button class="primary-btn" data-panel="collection">Album</button><button class="secondary-btn" data-panel="eggs">Batoh s vajíčkami</button></div><div class="collection-grid">${entries.map(p=>`<article class="collection-entry ${p.count?'':'locked'}">${petArt(p)}<small>${rarityLabels[p.rarity]}</small><h3>${esc(p.name)}</h3><span class="owned-count">${p.count?`V zbierke: ${p.count}`:'Zatiaľ neobjavený'}</span></article>`).join('')}</div>`,'collection');return;
  }
  const extra=adventurePanel(panel,state,defs,{heading:modalHeading,icon,compact,targets:world.interactables,position,effectiveQuality});
  if(extra){openModal(extra+(panel==='map'?`<div class="modal-actions">${coop?.getWorld()?'<button class="primary-btn" data-action="find-beacon">Najbližší spoločný maják</button>':''}<button class="secondary-btn" data-action="start-course">Rovno na štart Nebeskej dráhy ${icon('arrow')}</button></div>`:panel==='settings'?'<div class="modal-actions"><button class="secondary-btn" data-action="profile">Upraviť profil objaviteľa</button></div>':''),panel);if(panel==='settings')bindImport();return;}
  if(panel==='pets'){
    openModal(modalHeading('TVOJI SPOLOČNÍCI','Malá partia. Veľké objavy.','Všetci spoločníci vyrábajú mince. Tréning zvýši ich príjem; jeden ťa môže sprevádzať svetom.')+`<div class="collection-tabs"><button class="primary-btn" data-panel="pets">Moja partia</button><button class="secondary-btn" data-panel="collection">Album · 14 spoločníkov</button><button class="secondary-btn" data-panel="eggs">Batoh s vajíčkami</button></div>`+
      `<div class="panel-summary">${icon('coin')} Príjem základne <strong>+${compact(state.incomePerSecond)}/s</strong><span>${state.pets.length} spoločníkov</span></div><div class="panel-grid">${state.pets.length?state.pets.map(p=>`<article class="item-card pet-card rarity-${p.rarity}">${petArt(p)}<span class="tag">${rarityLabels[p.rarity]||p.rarity} · ÚROVEŇ ${p.level||1}</span><h3>${esc(p.name)}</h3><p>${icon('coin')} +${compact(p.income)}/s</p><button class="${p.id===state.equippedPet?'secondary-btn':'primary-btn'}" data-equip="${esc(p.id)}">${p.id===state.equippedPet?'Sprevádza ťa':'Vziať so sebou'}</button><div class="pet-training"><button class="secondary-btn" data-train="${esc(p.id)}" ${(p.level||1)>=5||state.player.coins<p.trainCost?'disabled':''}>${(p.level||1)>=5?'Tréning dokončený':`Trénovať · ${compact(p.trainCost||0)} mincí`}</button><small>Každá úroveň pridá 20 % pôvodného príjmu.</small></div></article>`).join(''):`<div class="empty-state">${icon('egg')}<h3>Prvý kamarát už čaká.</h3><p>Kúp vajíčko na centrálnom námestí a vyliahni ho v tvojej základni.</p><button class="primary-btn" data-waypoint="egg-basic">Ukázať cestu k vajíčkam</button></div>`}</div>`,'pets');
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
    if(journalFilter!=='all')learned=learned.filter(q=>q.subject===journalFilter);
    openModal(modalHeading('DENNÍK OBJAVOV','Vedomosti si vezmeš so sebou.',`Objavené poznatky: ${state.progress.solved.length}. Každá správna odpoveď má vysvetlenie.`)+masteryCards(state)+`<div class="journal-filters">${[['all','Všetko'],...Object.entries(subjectLabels)].map(([id,name])=>`<button class="${journalFilter===id?'primary-btn':'secondary-btn'}" data-journal-subject="${id}" aria-pressed="${journalFilter===id}">${name}</button>`).join('')}</div><div class="journal-list">${learned.length?learned.map(q=>`<article class="journal-card"><span class="tag">${esc(subjectLabels[q.subject]||q.subject||'Objav')}</span><h3>${esc(q.title||q.concept)}</h3><p>${esc(q.explanation||q.text)}</p></article>`).join(''):`<div class="empty-state">${icon('book')}<h3>Tvoj ďalší poznatok čaká.</h3><p>Navštív matematický pavilón, laboratórium alebo prírodný archív.</p><button class="primary-btn" data-waypoint="${journalFilter==='astral'?'portal-astral':journalFilter==='all'?'quiz-math':'quiz-'+journalFilter}">Cesta k poznaniu</button></div>`}</div>`,'journal');
  }
}
function distance(t){return Math.hypot(position.x-t.position.x,position.z-t.position.z);}
let waypoint=null;
async function showWaypoint(id){let t=world.interactables.find(x=>x.id===id);if(!t&&id.startsWith('crystal-')){const crystals=world.collectibles.filter(c=>!collectedIds.has(c.id));const c=crystals.find(c=>c.id===id)||crystals[0];if(c)t={...c,name:'Kryštál objavov'};}if(!t&&id.startsWith('beacon-')){const b=coop?.getWorld()?.beacons.find(b=>b.id===id&&!b.collected);if(b)t={id:b.id,name:`Spoločný maják · ${beaconFacts[id]?.[0]||'Poznanie'}`,position:{x:b.x,y:b.y,z:b.z},radius:2};}if(!t){notify('Tento cieľ už máš objavený. Vyber ďalšiu výpravu.');return;}if(courseRun)await leaveCourse();waypoint={...t,path:findWalkingPath(position,t.position,world)};drawWalkingTrail(waypoint.path);closeModal();notify(`Sleduj modrú trasu: ${t.name}`);}
function interact(){
  if(modalOpen||!currentTarget)return;
  const target=currentTarget;
  if(target.type==='egg'){
    const def=engine.getDefinitions().eggDefs.find(e=>e.id===(target.eggId||target.id));
    if(!def){notify('Toto vajíčko ešte nie je pripravené.','warning');return;}
    const state=engine.getState(),locked=state.player.knowledge<def.knowledge;
    openModal(modalHeading('TRH OBJAVOV',esc(def.name),esc(def.description))+`<div class="egg-shop-preview"><div class="egg-preview" style="--egg-color:${esc(def.color)}">${icon('egg')}</div><div><span class="tag">${locked?'ZATIAĽ ZAMKNUTÉ':'PRIPRAVENÉ NA OBJAV'}</span><p>${def.knowledge} poznatkov na odomknutie · ${state.eggs.length}/${state.capacity} v batohu</p><strong>${icon('coin')} ${compact(def.price)} mincí</strong></div></div>${rarityOdds(def.chances)}<p class="small-note">Získaš jedného náhodného spoločníka. Mince sú herné a získavaš ich hraním.</p><div class="modal-actions"><button class="secondary-btn" data-action="close">Ešte sa rozhliadnem</button><button class="primary-btn" data-buy="${def.id}" ${locked||state.player.coins<def.price||state.eggs.length>=state.capacity?'disabled':''}>${locked?icon('lock')+' Získaj viac poznatkov':'Kúpiť vajíčko '+icon('arrow')}</button></div>`,'shop');
  }
  if(target.type==='hatch')showPanel('eggs');
  if(target.type==='arena')multiplayer.open();
  if(target.type==='upgrade')showPanel('upgrades');
  if(target.type==='quiz')showQuiz(target.subject||target.id.replace('quiz-',''));
  if(target.type==='parkour')startCourse();
  if(target.type==='portal'){
    if(!stateCache.progress.unlocked.includes('astral')){notify('Astrálna brána sa otvorí po 8 nových poznatkoch.','warning');return;}
    showQuiz('astral');
  }
}
function showQuiz(subject){
  const q=engine.getQuiz(subject);if(!q){notify('Tento archív sa ešte pripravuje.');return;}
  q.options=[...q.options];for(let i=q.options.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));[q.options[i],q.options[j]]=[q.options[j],q.options[i]];}
  const fresh=!stateCache.progress.solved.includes(q.id),mastery=stateCache.mastery?.[q.subject];
  openModal(modalHeading(esc((subjectLabels[q.subject]||'TERMINÁL POZNANIA').toUpperCase()),esc(q.title),fresh?'Nový objav = 50 mincí + 1 poznatok + 25 XP.':'Precvičenie = 10 mincí + 5 XP. Poznatok už máš v denníku.')+`<div class="quiz-reward"><span>${icon('book')} ${mastery?.solved||0} / ${mastery?.total||8} poznatkov</span><span>Séria: ${stateCache.stats.streak||0}</span></div><p class="question">${esc(q.question)}</p><div class="quiz-choices">${q.options.map((o,i)=>`<button class="choice" data-answer="${o.id}"><span class="choice-letter">${String.fromCharCode(65+i)}</span><span>${esc(o.label)}</span></button>`).join('')}</div><div id="quiz-feedback" class="answer-feedback" aria-live="polite"></div><div class="quiz-footer"><button class="secondary-btn" data-action="hint">${icon('book')} Nápoveda</button><span>Chyba je príležitosť učiť sa.</span></div><p id="quiz-hint" class="hint-box" hidden></p>`,'quiz');activeQuiz=q;
}
function answer(value){if(!activeQuiz)return;const q=activeQuiz,r=engine.answerQuiz(q.id,value);chime(r.ok);const feedback=$('#quiz-feedback');
  if(r.ok){feedback.className='answer-feedback correct';feedback.innerHTML=`<strong>${icon('check')} ${r.firstSolve?'Objav potvrdený!':'Skvelé precvičenie!'}</strong><p>${esc(q.explanation)}</p><div class="quiz-reward">+${r.coins} mincí · +${r.xp} XP ${r.firstSolve?'· +1 poznatok':''}</div><div class="modal-actions"><button class="secondary-btn" data-action="close">Späť do sveta</button><button class="primary-btn" data-next-quiz="${q.subject}">Ďalší objav ${icon('arrow')}</button></div>`;document.querySelectorAll('.choice').forEach(b=>b.disabled=true);$('button',feedback)?.focus();}
  else{feedback.className='answer-feedback incorrect';feedback.innerHTML=`<strong>Premysli to ešte raz.</strong><p>${esc(r.message||'Pomôže ti nápoveda. Môžeš skúsiť znova.')}</p>`;}
}
function hatch(){const near=world.interactables.some(t=>t.type==='hatch'&&distance(t)<t.radius+1);if(!near){notify('Vajíčko vyliahneš pri inkubátore na základni.','warning');return;}const r=engine.hatchEgg();if(!r.ok){action(r);return;}chime();const p=r.pet;openModal(`<div class="hatch-celebration">${petArt(p)}</div><span class="modal-eyebrow">NOVÝ SPOLOČNÍK</span><h2>${esc(p.name)} sa pridáva k tebe!</h2><div class="hatch-rarity rarity-${p.rarity}">${rarityLabels[p.rarity]} · +${p.income} mincí / s</div><p class="modal-description">Na základni ti pomôže vyrábať mince. Vezmi ho na ďalší objav.</p><div class="modal-actions"><button class="secondary-btn" data-action="close">Pokračovať</button><button class="primary-btn" data-equip="${esc(p.id)}">Vziať so sebou ${icon('arrow')}</button></div>`,'hatch');spawnBurst();}
function help(){openModal(modalHeading('VITAJ V AKADÉMII','Každý objav je nové dobrodružstvo.','Preskúmaj ostrov, rozvíjaj spoločníkov a pozvi kamarátov na spoločnú výpravu.')+`<ol class="help-steps"><li><strong>Preskúmaj 3D svet.</strong> WASD alebo šípky = pohyb, Shift = beh, medzerník = skok. Ťahaj myšou pre kameru. E otvorí najbližšie miesto.</li><li><strong>Vybuduj partiu.</strong> Kúp vajíčko na námestí, vyliahni ho na základni a trénuj svojich spoločníkov pre vyšší príjem.</li><li><strong>Objavuj vedomosti.</strong> Matematika, veda, príroda a odomknutá astrálna brána ponúkajú 36 otázok s nápovedou a vysvetlením.</li><li><strong>Vyraz na výpravu.</strong> Zbieraj 12 kryštálov, vyzdvihuj odmeny a zvládni sedem plošín Nebeskej dráhy. M otvorí mapu, 5 výpravy.</li><li><strong>Hraj spolu.</strong> V Menu vyber Hrať spolu a pozvi až sedem kamarátov odkazom alebo kódom. Prechádzajte sa po ostrove a spoločne rozsvieťte šesť majákov. Aréna ponúka živý kvíz pre 2 až 8 hráčov a súboj spoločníkov 1 na 1.</li><li><strong>Získaj rank a výbavu.</strong> Online zápasy prinášajú rating a úlomky na vylepšenie útoku, obrany a sústredenia.</li></ol><div class="save-note">Svet sa automaticky ukladá. V nastaveniach nájdeš zálohu a voľbu grafiky pre svoj laptop.</div><button class="primary-btn" data-action="close">Poďme objavovať ${icon('arrow')}</button>`);}
function profile(afterSave){
  const p=stateCache.player;
  openModal(modalHeading('TVOJ OBJAVITEĽ','Každý príbeh má svoje meno.','Vyber si vzhľad a vekovú kategóriu. Otázky v učebných oblastiach sa prispôsobia tvojej voľbe.')+`<form id="profile-form" class="explorer-form"><div class="profile-field"><label for="profile-name">Meno objaviteľa</label><input id="profile-name" maxlength="24" value="${esc(p.name==='Objaviteľ'&&!stateCache.onboarding.profileReady?'':p.name)}" placeholder="Tvoje herné meno" required autocomplete="off"></div><fieldset class="profile-field"><legend>Veková kategória</legend><div class="age-options">${AGE_LABELS.map((label,i)=>`<label class="age-option"><input type="radio" name="age" value="${i}" ${p.ageBand===i?'checked':''}>${label}</label>`).join('')}</div></fieldset><div class="profile-grid"><fieldset class="profile-field"><legend>Farba oblečenia</legend><div class="appearance-options">${OUTFITS.map((c,i)=>`<label style="--swatch:${c}" title="Oblečenie ${i+1}"><input type="radio" name="outfit" value="${i}" aria-label="Oblečenie ${i+1}" ${p.outfit===i?'checked':''}></label>`).join('')}</div></fieldset><fieldset class="profile-field"><legend>Odtieň pokožky</legend><div class="appearance-options">${SKINS.map((c,i)=>`<label style="--swatch:${c}" title="Odtieň ${i+1}"><input type="radio" name="skin" value="${i}" aria-label="Odtieň ${i+1}" ${p.skin===i?'checked':''}></label>`).join('')}</div></fieldset></div><button class="primary-btn profile-submit" type="submit">${typeof afterSave==='function'?'Vstúpiť do akadémie ↗':'Uložiť objaviteľa'}</button></form><div class="save-note">Úroveň ${p.level} · ${p.xp} XP. Tvoj postup sa automaticky ukladá v tomto prehliadači.</div><button class="text-btn" data-action="reset-prompt">Začať nový svet</button>`,'profile');
  $('#profile-form').onsubmit=e=>{e.preventDefault();const f=new FormData(e.currentTarget),r=engine.setExplorer({name:$('#profile-name').value,ageBand:Number(f.get('age')),outfit:Number(f.get('outfit')),skin:Number(f.get('skin'))});action(r);if(r.ok){closeModal();if(typeof afterSave==='function')afterSave();}};
}
function startAdventure(withTutorial=false){
  const enter=()=>{shell.hide();closeModal();$('#welcome-card').hidden=true;if(withTutorial||!stateCache.onboarding.completed&&!stateCache.onboarding.active&&!stateCache.onboarding.rewarded){tutorialTravel=0;companionTravel=0;engine.startTutorial();}notify('Vitaj v akadémii. Menu nájdeš vpravo dole.','success');};
  if(!stateCache.onboarding.profileReady)profile(enter);else enter();
}
function exportSave(){
  const blob=new Blob([engine.exportSave()],{type:'application/json'}),url=URL.createObjectURL(blob),link=document.createElement('a');
  link.href=url;link.download='LUMEN-zaloha.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);notify('Záloha sveta bola pripravená na stiahnutie.','success');
}
function bindImport(){
  const input=$('#import-save');if(!input)return;
  input.addEventListener('change',async()=>{
    const file=input.files?.[0];if(!file)return;
    if(file.size>3*1024*1024){$('#import-status').textContent='Záloha je príliš veľká. Vyber JSON súbor hry do 3 MB.';return;}
    try{const text=await file.text();openModal(modalHeading('OBNOVA ZÁLOHY','Vrátiť sa do uloženého sveta?',`Záloha nahradí tvoj súčasný postup. Pred obnovou si môžeš stiahnuť aktuálny svet.`)+`<div class="modal-actions"><button class="secondary-btn" data-action="export-save">Zálohovať súčasný svet</button><button class="primary-btn" id="confirm-import">Obnoviť vybranú zálohu</button></div><p id="import-status" class="save-note" role="status"></p>`,'import');$('#confirm-import').onclick=()=>{const result=engine.importSave(text);if(result.ok){leaveCourse(false);relocate('spawn',{x:0,y:0,z:13});waypoint=null;closeModal();action(result);}else $('#import-status').textContent=result.message;};}
    catch{notify('Súbor sa nepodarilo prečítať. Vyber inú zálohu.','warning');}
  });
}
function applyGraphics(mode='auto',forceLow=false){
  if(!renderer||!sun)return;
  qualityApplied=mode;effectiveQuality=forceLow||mode==='low'?'low':mode;
  if(vr?.active){effectiveQuality='low';renderer.shadowMap.enabled=false;return;}
  renderer.setPixelRatio(Math.min(devicePixelRatio,effectiveQuality==='low'?1:mode==='high'?2:1.25));
  renderer.shadowMap.enabled=effectiveQuality!=='low';renderer.shadowMap.autoUpdate=false;
  const size=mode==='high'?2048:1024;
  if(sun.shadow.mapSize.x!==size){sun.shadow.map?.dispose();sun.shadow.map=null;sun.shadow.mapSize.set(size,size);}
  renderer.shadowMap.needsUpdate=true;renderer.setSize(window.innerWidth,window.innerHeight,false);
  autoFrameTime=0;autoFrames=0;
}
async function relocate(kind,destination,checkpoint){
  relocating=true;keys.clear();joystick.x=joystick.y=0;
  try{if(coop?.getWorld()){const authoritative=await coop.teleport(kind,checkpoint);if(!authoritative)return false;position.set(authoritative.x,authoritative.y,authoritative.z);}else position.set(destination.x,destination.y,destination.z);verticalVelocity=0;onGround=true;return true;}
  finally{relocating=false;}
}
async function startCourse(){
  const course=world.course;if(!course)return;
  closeModal();waypoint=null;const first=course.platforms[0];
  if(!await relocate('course-start',{x:first.x,y:first.top,z:first.z}))return;lastPlatform=first.id;
  courseRun={elapsed:0,index:0,fallDelay:0,missed:null};
  document.body.classList.add('running-course');$('#welcome-card').hidden=true;
  $('#course-status').hidden=false;notify('Nebeská dráha: preskáč sedem plošín v poradí. Pád ťa vráti na kontrolný bod.');
}
async function leaveCourse(returnToStart=true){
  const returnNeeded=returnToStart&&courseRun;
  courseRun=null;if($('#course-status'))$('#course-status').hidden=true;
  document.body.classList.remove('running-course');
  if(returnNeeded)await relocate('course-start',{x:17,y:0,z:22});
}
function updateCourse(elapsed){
  if(!courseRun)return;
  if(!modalOpen&&!menuOpen&&!shell?.open&&!vrPanel?.menuOpen&&!vr?.suspended&&!relocating)courseRun.elapsed+=elapsed;
  const run=courseRun,platforms=world.course.platforms,index=platforms.findIndex(p=>p.id===lastPlatform);
  if(onGround&&index===run.index+1){run.index=index;run.missed=null;chime();}
  if(onGround&&index>run.index+1&&run.missed!==index){run.missed=index;notify('Najprv doskoč na nasledujúcu očíslovanú plošinu.','warning');}
  if(onGround&&position.y===0&&!relocating){run.fallDelay+=elapsed;if(run.fallDelay>.45){const p=platforms[run.index];run.fallDelay=0;relocate('course-checkpoint',{x:p.x,y:p.top,z:p.z},run.index).then(ok=>{if(ok){lastPlatform=p.id;notify('Kontrolný bod. Pokračuj na ďalšiu plošinu.');}});}}
  else run.fallDelay=0;
  $('#course-time').textContent=`${run.elapsed.toFixed(1)} s`;$('#course-progress').textContent=`${run.index+1} / ${platforms.length}`;
  if(run.index===platforms.length-1){const result=engine.finishParkour(run.elapsed);leaveCourse(false);action(result);if(result.ok){spawnBurst();showPanel('missions');}}
}
function collectNearby(){
  if(modalOpen||menuOpen||shell?.open)return;
  const beacon=party?.nearestBeacon(position);if(beacon&&beacon.distance<2.7)coop.tryCollect(beacon.id);
  for(const c of world.collectibles||[]){
    if(!collectedIds.has(c.id)&&Math.hypot(position.x-c.position.x,position.y+1.2-c.position.y,position.z-c.position.z)<c.radius){
      const result=engine.collectCrystal(c.id);if(result.ok){collectedIds.add(c.id);spawnBurst(10);chime();notify(`Kryštál ${collectedIds.size}/12 · +15 mincí · +10 XP`,'success');if(waypoint?.id===c.id)waypoint=null;}
    }
  }
}
function bindUI(){
  document.addEventListener('click',e=>{const b=e.target.closest('button');if(!b)return;
    if(b.dataset.panel){showPanel(b.dataset.panel);return;}
    if(b.dataset.study){showQuiz(b.dataset.study);return;}
    if(b.dataset.buy){const r=action(engine.buyEgg(b.dataset.buy));if(r.ok){closeModal();showWaypoint('hatch');}return;}
    if(b.dataset.equip){action(engine.equipPet(b.dataset.equip));if(modalPanel==='pets')showPanel('pets');else closeModal();return;}
    if(b.dataset.train){action(engine.trainPet(b.dataset.train));showPanel('pets');return;}
    if(b.dataset.claim){action(engine.claimQuest(b.dataset.claim));showPanel('missions');return;}
    if(b.dataset.graphics){action(engine.setSetting('graphics',b.dataset.graphics));showPanel('settings');return;}
    if(b.dataset.journalSubject){journalFilter=b.dataset.journalSubject;showPanel('journal');return;}
    if(b.dataset.upgrade){action(engine.upgrade(b.dataset.upgrade));showPanel('upgrades');return;}
    if(b.dataset.answer){answer(b.dataset.answer);return;}
    if(b.dataset.nextQuiz){showQuiz(b.dataset.nextQuiz);return;}
    if(b.dataset.waypoint){showWaypoint(b.dataset.waypoint);return;}
    switch(b.dataset.action){case 'close':closeModal();break;case 'start':$('#welcome-card').hidden=true;$('#game-canvas').focus();showWaypoint('egg-basic');break;case 'hatch':hatch();break;case 'hint':if(activeQuiz){$('#quiz-hint').hidden=false;$('#quiz-hint').textContent=activeQuiz.hint;}break;
      case 'title':setMenu(false);closeModal();keys.clear();joystick.x=joystick.y=0;shell.show();break;
      case 'tutorial':setMenu(false);startAdventure(true);break;
      case 'enter-vr':void vr.start();break;
      case 'check-vr':void vr.checkSupport();break;
      case 'cancel-waypoint':waypoint=null;$('#game-canvas').focus();break;
      case 'restart-course':startCourse();break;
      case 'leave-course':leaveCourse();notify('Výprava na dráhe ukončená. Rekordy zostávajú uložené.');$('#game-canvas').focus();break;
      case 'start-course':startCourse();break;
      case 'find-beacon':{const target=party.nearestBeacon(position);if(target)showWaypoint(target.id);else notify('Spoločnú výpravu už máte splnenú.');break;}
      case 'export-save':exportSave();break;
      case 'choose-save':$('#import-save')?.click();break;
      case 'profile':profile();break;
      case 'toggle-sound':engine.setSetting('sound',!stateCache.settings.sound);chime();showPanel('settings');break;
      case 'reset-prompt':openModal(modalHeading('NOVÝ SVET','Začať úplne od začiatku?',`Vymažeš svoje mince, vajíčka, spoločníkov aj poznatky v tomto prehliadači.`)+`<div class="modal-actions"><button class="secondary-btn" data-action="close">Zachovať svet</button><button class="primary-btn" data-action="reset">Vymazať a začať</button></div>`);break;
      case 'reset':leaveCourse(false);engine.reset();relocate('spawn',{x:0,y:0,z:13});waypoint=null;closeModal();notify('Nový príbeh začína.');break;
      case 'prestige-prompt':openModal(modalHeading('NOVÁ ÉRA','Prebudovať akadémiu?',`Vajíčka, spoločníci, mince a vylepšenia sa obnovia. Vedomosti si ponecháš a príjem získa trvalý bonus.`)+`<div class="modal-actions"><button class="secondary-btn" data-action="close">Ešte objavovať</button><button class="primary-btn" data-action="prestige">Začať novú éru</button></div>`);break;
      case 'prestige':action(engine.prestige());closeModal();break;
    }
  });
  $('#help-btn')?.addEventListener('click',help);$('#profile-btn')?.addEventListener('click',profile);$('#interact-btn')?.addEventListener('click',interact);$('#jump-btn')?.addEventListener('click',jump);
  $('#settings-btn')?.addEventListener('click',()=>showPanel('settings'));
  $('#vr-btn')?.addEventListener('click',showVR);
  $('#arena-btn')?.addEventListener('click',()=>multiplayer.open());
  $('#coop-btn')?.addEventListener('click',()=>coop.open());$('#coop-status')?.addEventListener('click',()=>coop.open());
  $('#menu-toggle')?.addEventListener('click',()=>setMenu(!menuOpen));$('#menu-close')?.addEventListener('click',()=>setMenu(false));
  $('#quest-toggle')?.addEventListener('click',()=>{const open=$('#quest-details').hidden;$('#quest-details').hidden=!open;$('#quest-toggle').setAttribute('aria-expanded',String(open));});
  document.addEventListener('pointerdown',e=>{if(menuOpen&&!e.target.closest('#game-drawer,#menu-toggle'))setMenu(false);});
  $('#sound-btn')?.addEventListener('click',()=>{engine.setSetting('sound',!stateCache.settings.sound);notify(stateCache.settings.sound?'Zvuk zapnutý':'Zvuk vypnutý');chime();});
  window.addEventListener('keydown',e=>{
    if(shell?.open&&!modalOpen){if(e.key==='Enter'&&e.target===document.body)startAdventure();return;}
    if(menuOpen){if(e.key==='Escape'){e.preventDefault();setMenu(false);}return;}
    if(modalOpen){if(e.key==='Escape')closeModal();if(e.key==='Tab'){const nodes=[...document.querySelectorAll('#modal-root button:not([disabled]),#modal-root input:not([type="file"]),#modal-root select')].filter(n=>n.offsetParent!==null),a=nodes[0],b=nodes.at(-1);if(e.shiftKey&&document.activeElement===a){e.preventDefault();b?.focus();}else if(!e.shiftKey&&document.activeElement===b){e.preventDefault();a?.focus();}}return;}
    if(/^(INPUT|TEXTAREA|BUTTON|A|SELECT)$/.test(e.target.tagName))return;
    const k=e.key.toLowerCase();if(['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright',' ','shift'].includes(k)){e.preventDefault();keys.add(k);}if(k===' '&&!e.repeat)jump();if(k==='e'&&!e.repeat)interact();if(k==='escape')setMenu(true);if(k==='m'&&!e.repeat)showPanel('map');if(['1','2','3','4','5'].includes(k)&&!e.repeat)showPanel(['pets','eggs','upgrades','journal','missions'][Number(k)-1]);
  });
  window.addEventListener('keyup',e=>keys.delete(e.key.toLowerCase()));window.addEventListener('blur',()=>{keys.clear();joystick.x=joystick.y=0;pointer=null;});
  document.addEventListener('visibilitychange',()=>{keys.clear();joystick.x=joystick.y=0;lastTime=performance.now();if(document.hidden)engine.save();});
  window.addEventListener('beforeunload',()=>{engine.save();coop?.dispose();multiplayer?.dispose();vr?.dispose();vrPanel?.dispose();});
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
function jump(){if(onGround&&!modalOpen&&!menuOpen&&!shell?.open&&!relocating){verticalVelocity=8.6;onGround=false;$('#welcome-card').hidden=true;engine.recordTutorial('jump');}}
function updatePlayer(dt){
  let x=(keys.has('d')||keys.has('arrowright')?1:0)-(keys.has('a')||keys.has('arrowleft')?1:0)+joystick.x;
  let z=(keys.has('s')||keys.has('arrowdown')?1:0)-(keys.has('w')||keys.has('arrowup')?1:0)+joystick.y;
  if(vr?.active){x=vrInput.x;z=vrInput.z;yaw=vrInput.yaw;}
  if(modalOpen||menuOpen||shell?.open||vrPanel?.menuOpen||relocating||document.hidden){x=0;z=0;}
  const length=Math.hypot(x,z);if(length>1){x/=length;z/=length;}
  const speed=vr?.active?3.2:7.2*(stateCache.movementMultiplier||1)*(keys.has('shift')?1.35:1);
  const dx=(Math.cos(yaw)*x+Math.sin(yaw)*z)*speed*dt,dz=(-Math.sin(yaw)*x+Math.cos(yaw)*z)*speed*dt;
  const body=stepMotion({x:position.x,y:position.y,z:position.z,vy:verticalVelocity,onGround},dx,dz,dt,world);
  if(stateCache.onboarding.active){const travelled=Math.hypot(body.x-position.x,body.z-position.z);tutorialTravel+=travelled;if(tutorialTravel>=5)engine.recordTutorial('move');if(follower){companionTravel+=travelled;if(companionTravel>=3)engine.recordTutorial('companion');}}
  position.set(body.x,body.y,body.z);verticalVelocity=body.vy;onGround=body.onGround;lastPlatform=body.platformId;
  player.position.copy(position);
  if(length>.1||vr?.active){const angle=vr?.active?yaw:Math.atan2(-dx,-dz);let delta=angle-player.rotation.y;delta=Math.atan2(Math.sin(delta),Math.cos(delta));player.rotation.y+=delta*Math.min(1,dt*12);}
  limbs.animate(dt,clockTime,{moving:length>.1,onGround,emote:clockTime<localEmoteUntil?'wave':null});
  if(length>.1)$('#welcome-card').hidden=true;
  if(follower){followerDestination.set(position.x+Math.sin(player.rotation.y)*2.8,position.y,position.z+Math.cos(player.rotation.y)*2.8);follower.position.lerp(followerDestination,1-Math.exp(-dt*4));follower.position.y=position.y+.2+Math.sin(clockTime*5)*.13;follower.rotation.y=player.rotation.y;follower.userData.animate?.(dt,clockTime);}
  if(!relocating)coop?.updatePosition(position,player.rotation.y,length>.1,onGround);
}
function updateCamera(dt){cameraLook.set(position.x,position.y+1.9,position.z);smoothLook.lerp(cameraLook,1-Math.exp(-dt*10));const horizontal=Math.cos(pitch)*cameraDistance;desiredCamera.set(smoothLook.x+Math.sin(yaw)*horizontal,smoothLook.y+Math.sin(pitch)*cameraDistance,smoothLook.z+Math.cos(yaw)*horizontal);const clipped=clipCameraLine(smoothLook,desiredCamera,world.cameraColliders||world.colliders);desiredCamera.set(clipped.x,clipped.y,clipped.z);camera.position.lerp(desiredCamera,1-Math.exp(-dt*9));const safe=clipCameraLine(smoothLook,camera.position,world.cameraColliders||world.colliders);camera.position.set(safe.x,safe.y,safe.z);camera.lookAt(smoothLook);const fov=keys.has('shift')&&!modalOpen?55:50;camera.fov+=(fov-camera.fov)*Math.min(1,dt*5);camera.updateProjectionMatrix();}
let targetRing,waypointArrow,walkingTrail;
function drawWalkingTrail(path){
  walkingTrail?.geometry.dispose();if(walkingTrail){scene.remove(walkingTrail);walkingTrail.material.dispose();}
  const points=path.map(p=>new THREE.Vector3(p.x,.3,p.z));
  walkingTrail=new THREE.Line(new THREE.BufferGeometry().setFromPoints(points),new THREE.LineBasicMaterial({color:'#538bee',transparent:true,opacity:.75}));scene.add(walkingTrail);
}
function updateInteraction(){let nearest=null,min=Infinity;for(const t of world.interactables){const d=distance(t);if(d<t.radius&&d<min){nearest=t;min=d;}}currentTarget=nearest;const id=nearest?.id||'';if(id!==interactionId){interactionId=id;$('#interaction-prompt').hidden=!nearest;if(nearest){$('#interaction-name').textContent=nearest.name;$('#interact-btn').setAttribute('aria-label',`Preskúmať: ${nearest.name}`);} }
  targetRing.visible=!!nearest;if(nearest){targetRing.position.set(nearest.position.x,.27,nearest.position.z);targetRing.rotation.z=clockTime*.3;}
  const zone=position.x>14&&position.z>18?'Nebeská dráha':position.x<-8&&position.z>2?'Tvoja základňa':position.z<-24?'Astrálna brána':position.x>8&&position.z<-6?'Vedecké laboratórium':position.x<-8&&position.z<-6?'Prírodný archív':position.x>8?'Matematický pavilón':'Námestie objavov';$('#zone-label').textContent=zone;
  waypointArrow.visible=!!waypoint;
  if(walkingTrail)walkingTrail.visible=!!waypoint;
  if(waypoint){const d=distance(waypoint);if(d<waypoint.radius&&!/^(crystal|beacon)-/.test(waypoint.id)){waypoint=null;waypointArrow.visible=false;if(walkingTrail)walkingTrail.visible=false;notify('Si na mieste. Stlač E alebo tlačidlo Preskúmať.');}else{while(waypoint.path.length>1&&Math.hypot(position.x-waypoint.path[0].x,position.z-waypoint.path[0].z)<1.1)waypoint.path.shift();const next=waypoint.path[0]||waypoint.position;waypointArrow.position.set(position.x,position.y+4+Math.sin(clockTime*3)*.1,position.z);waypointArrow.rotation.y=Math.atan2(next.x-position.x,next.z-position.z);if($('#waypoint-name'))$('#waypoint-name').textContent=waypoint.name;if($('#waypoint-distance'))$('#waypoint-distance').textContent=`${Math.round(d)} m`;}}
  if($('#waypoint-status'))$('#waypoint-status').hidden=!waypoint;
}
function drawMinimap(){const c=$('#minimap-canvas');if(!c)return;const ctx=c.getContext('2d'),w=c.width,h=c.height;ctx.clearRect(0,0,w,h);ctx.fillStyle='#edf7df';ctx.fillRect(0,0,w,h);const to=(x,z)=>[w/2+x*1.7,h/2+z*1.7];ctx.strokeStyle='#e4d1b2';ctx.lineWidth=12;ctx.beginPath();ctx.moveTo(w/2,8);ctx.lineTo(w/2,h-8);ctx.moveTo(8,h/2);ctx.lineTo(w-8,h/2);ctx.stroke();for(const t of world.interactables){const [x,y]=to(t.position.x,t.position.z);ctx.fillStyle=t.type==='egg'?'#e7b952':t.type==='quiz'?'#9b90dc':t.type==='hatch'?'#53b7bc':'#bba7cc';ctx.beginPath();ctx.arc(x,y,t.type==='quiz'?4:3,0,Math.PI*2);ctx.fill();}for(const peer of party?.minimapPoints()||[]){const [px,py]=to(peer.x,peer.z);ctx.fillStyle='#259ba8';ctx.beginPath();ctx.arc(px,py,3.5,0,Math.PI*2);ctx.fill();}const [x,y]=to(position.x,position.z);ctx.fillStyle='#2b3551';ctx.beginPath();ctx.arc(x,y,4,0,Math.PI*2);ctx.fill();ctx.strokeStyle='#2b3551';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x-Math.sin(yaw)*8,y-Math.cos(yaw)*8);ctx.stroke();}
const particles=[];
function spawnBurst(count=28){for(let i=0;i<count;i++){const mesh=new THREE.Mesh(new THREE.IcosahedronGeometry(.11,0),material(['#ffd968','#af94ee','#69d1c4'][i%3]));mesh.castShadow=false;mesh.position.copy(position).add(new THREE.Vector3(0,2,0));scene.add(mesh);particles.push({mesh,v:new THREE.Vector3((Math.random()-.5)*8,Math.random()*7+2,(Math.random()-.5)*8),life:1.5});}}
function updateParticles(dt){for(let i=particles.length-1;i>=0;i--){const p=particles[i];p.life-=dt;p.v.y-=8*dt;p.mesh.position.addScaledVector(p.v,dt);p.mesh.rotation.x+=dt*4;if(p.life<=0){scene.remove(p.mesh);p.mesh.geometry.dispose();particles.splice(i,1);}}}
let minimapTime=0,lastModalRender=0;
const daytimeSky=new THREE.Color('#89cdec'),nightSky=new THREE.Color('#31456c');
function updateDaylight(){const daylight=.5+.5*Math.cos(clockTime*Math.PI*2/960);scene.background.copy(nightSky).lerp(daytimeSky,daylight);scene.fog.color.copy(scene.background);sun.intensity=.35+1.75*daylight;ambient.intensity=1+.3*daylight;renderer.toneMappingExposure=.9+.08*daylight;world.ambiance?.setDaylight(daylight);}
function frame(time){
  if(document.hidden||graphicsLost){lastTime=time;return;}
  const elapsed=Math.max(0,Math.min(1,(time-lastTime)/1000)),dt=Math.min(.2,elapsed);lastTime=time;clockTime+=dt;
  if(!vr?.active&&modalOpen&&time-lastModalRender<120){engine.tick(elapsed);return;}lastModalRender=time;
  if(vr?.active)vrInput=vr.getInput(dt,modalOpen||vrPanel.menuOpen||relocating);
  engine.tick(elapsed);updatePlayer(dt);collectNearby();updateCourse(elapsed);
  if(vr?.active){vr.updatePosition(position);vrPanel.update(dt,vr.getViewCamera());}else updateCamera(dt);
  world.update(dt,clockTime,stateCache);party.update(dt,clockTime,position);updateDaylight();updateInteraction();updateParticles(dt);
  minimapTime+=dt;if(minimapTime>.15){drawMinimap();minimapTime=0;}
  shadowTimer+=elapsed;if(shadowTimer>(effectiveQuality==='high'?.12:.3)){renderer.shadowMap.needsUpdate=true;shadowTimer=0;}
  if(!vr?.active&&qualityApplied==='auto'&&effectiveQuality!=='low'&&!modalOpen){autoFrameTime+=elapsed;autoFrames++;if(autoFrameTime>5&&autoFrames/autoFrameTime<32){applyGraphics('auto',true);notify('Úsporná grafika zapnutá pre plynulejšiu hru. Kvalitu môžeš zmeniť v nastaveniach.');}else if(autoFrameTime>8){autoFrameTime=0;autoFrames=0;}}
  renderer.render(scene,camera);
}
function init(){
  const canvas=$('#game-canvas');engine=new AcademyEngine(undefined,AGE_QUESTIONS);stateCache=engine.getState();
  renderer=new THREE.WebGLRenderer({canvas,antialias:true,powerPreference:'high-performance'});renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFShadowMap;renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=.98;
  scene=new THREE.Scene();scene.background=new THREE.Color('#89cdec');scene.fog=new THREE.Fog('#89cdec',60,155);
  ambient=new THREE.HemisphereLight('#e7f4ff','#739953',1.3);scene.add(ambient);sun=new THREE.DirectionalLight('#fff4dd',2.1);sun.position.set(-25,45,22);sun.castShadow=true;sun.shadow.mapSize.set(1024,1024);sun.shadow.camera.left=-52;sun.shadow.camera.right=52;sun.shadow.camera.top=52;sun.shadow.camera.bottom=-52;sun.shadow.camera.near=1;sun.shadow.camera.far=120;sun.shadow.bias=-.0005;sun.shadow.normalBias=.025;scene.add(sun);sun.target.position.set(0,0,-5);scene.add(sun.target);
  camera=new THREE.PerspectiveCamera(50,1,.1,220);world=createAcademyWorld(scene);position.set(world.spawn.x,0,world.spawn.z);const character=createCharacter();player=character.group;limbs=character;
  const arena=buildArena(THREE);arena.position.set(-20,0,20);scene.add(arena);world.interactables.push({id:'arena-online',type:'arena',name:'Aréna poznania · hra s kamarátmi',position:{x:-20,z:24.4},radius:3});
  multiplayer=createMultiplayer({openModal,closeModal,notify,getState:()=>engine.getState(),petArt,icon,heading:modalHeading,onReward:()=>{chime();spawnBurst();}});
  party=createPartyWorld(scene);let previousBeaconCount=0,previousBeaconIds=new Set();
  coop=createCoop({openModal,closeModal,notify,getState:()=>engine.getState(),
    onPeers:(players,meId)=>party.setPeers(players,meId),
    onPosition:p=>{if(p){position.set(p.x,p.y,p.z);verticalVelocity=0;onGround=p.y<=.05;}},
    onEmote:()=>{localEmoteUntil=clockTime+3;},
    onStatus:status=>{party.setConnected(status.connected);$('#coop-status').hidden=!status.connected;if(status.connected){$('#coop-world-code').textContent=`Svet ${status.code}`;$('#coop-player-count').textContent=`${status.count} / 8`;$('#coop-status').classList.toggle('reconnecting',Boolean(status.error));}},
    onGoal:(beacons,goal)=>{party.setBeacons(beacons);if(!goal){previousBeaconCount=0;previousBeaconIds.clear();if(waypoint?.id.startsWith('beacon-'))waypoint=null;return;}if($('#coop-goal-text'))$('#coop-goal-text').textContent=goal.complete?'Výprava splnená':`Majáky ${goal.collected} / ${goal.total}`;const discoveries=beacons.filter(b=>b.collected&&!previousBeaconIds.has(b.id));if(goal.collected>previousBeaconCount){chime();if(discoveries.length===1)notify(`${beaconFacts[discoveries[0].id]?.[1]||'Nový spoločný objav!'} · ${goal.collected}/${goal.total}`,'success');}previousBeaconCount=goal.collected;previousBeaconIds=new Set(beacons.filter(b=>b.collected).map(b=>b.id));if(waypoint?.id.startsWith('beacon-')&&previousBeaconIds.has(waypoint.id))waypoint=null;},
    onReward:()=>{chime();spawnBurst();}
  });
  const ringGeo=new THREE.RingGeometry(1.75,1.9,48);targetRing=new THREE.Mesh(ringGeo,new THREE.MeshBasicMaterial({color:'#fff2ab',transparent:true,opacity:.85,side:THREE.DoubleSide}));targetRing.rotation.x=-Math.PI/2;targetRing.visible=false;scene.add(targetRing);
  waypointArrow=new THREE.Group();const arrow=box(.25,.15,1.2,'#538bee',0,0,0),tip=new THREE.Mesh(new THREE.ConeGeometry(.45,.75,3),material('#538bee'));tip.rotation.x=Math.PI/2;tip.position.z=.75;waypointArrow.add(arrow,tip);waypointArrow.visible=false;scene.add(waypointArrow);
  vrPanel=createVRPanel({scene,onInteract:interact,onPanel:showPanel,onCloseModal:closeModal,onJump:jump,onExit:()=>void vr.end(),getStatus:()=>({coins:compact(stateCache.player.coins),level:stateCache.player.level,zone:$('#zone-label').textContent,targetName:currentTarget?.name||'',connected:Boolean(coop.getWorld()),message:$('#toast-root .game-toast:last-child')?.textContent||''})});
  vr=createVRMode({renderer,scene,camera,onSelect:controller=>{if(!vrPanel.select(controller)&&!modalOpen&&!relocating)interact();},onMenu:()=>vrPanel.toggleMenu(),onJump:()=>{if(!vrPanel.menuOpen)jump();},onStatus:updateVRStatus,onChange:active=>{
    keys.clear();joystick.x=joystick.y=0;pointer=null;lastTime=performance.now();setMenu(false);closeModal();player.visible=!active;vrPanel.setActive(active);document.body.classList.toggle('immersive-vr',active);$('#vr-btn').setAttribute('aria-pressed',String(active));
    if(active){$('#welcome-card').hidden=true;effectiveQuality='low';renderer.shadowMap.enabled=false;vr.updatePosition(position);}
    else{if(!graphicsLost)applyGraphics(stateCache.settings.graphics||'auto');camera.aspect=window.innerWidth/window.innerHeight;smoothLook.set(position.x,position.y+1.9,position.z);updateCamera(1);engine.save();notify('Vrátil si sa z VR. Dobrodružstvo pokračuje.');}
  }});
  void vr.checkSupport();
  engine.subscribe(syncState);syncState(stateCache);bindUI();
  shell=createAcademyShell({getState:()=>engine.getState(),petArt,onPlay:()=>startAdventure(),onTutorial:()=>startAdventure(true),onCoop:()=>coop.open(),onArena:()=>multiplayer.open(),onPanel:showPanel,onProfile:()=>profile(),onHelp:help,onPauseTutorial:()=>action(engine.pauseTutorial()),onNavigate:showWaypoint});
  if(new URL(location.href).searchParams.has('world')||new URL(location.href).searchParams.has('room'))shell.hide();
  multiplayer.joinFromUrl();
  coop.updatePosition(position,0,false,true);coop.joinFromUrl();
  if(stateCache.player.xp>0||stateCache.pets.length>0||stateCache.eggs.length>0)$('#welcome-card').hidden=true;
  const resize=()=>{if(vr.active)return;renderer.setSize(window.innerWidth,window.innerHeight,false);camera.aspect=window.innerWidth/window.innerHeight;camera.updateProjectionMatrix();};window.addEventListener('resize',resize);resize();smoothLook.set(position.x,1.9,position.z);camera.position.set(position.x,14,position.z+19);updateCamera(1);renderer.render(scene,camera);$('#loading-overlay').hidden=true;lastTime=performance.now();renderer.setAnimationLoop(frame);
  canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();graphicsLost=true;void vr.end();keys.clear();joystick.x=joystick.y=0;$('#loading-overlay').hidden=false;const message=$('#load-error');if(message){message.hidden=false;message.innerHTML='3D obraz sa prerušil. Počkaj na obnovu alebo obnov hru; postup je uložený.<br><button class="primary-btn" id="restore-game">Obnoviť hru</button>';$('#restore-game').onclick=()=>location.reload();}});
  canvas.addEventListener('webglcontextrestored',()=>{graphicsLost=false;$('#loading-overlay').hidden=true;$('#load-error').hidden=true;lastTime=performance.now();notify('3D obraz bol obnovený. Môžeš pokračovať.');});
}
try{init();}catch(error){console.error('LUMEN startup:',error);const node=$('#load-error');if(node){node.hidden=false;node.innerHTML='3D svet sa nepodarilo spustiť. Skontroluj podporu WebGL a obnov stránku.<br><button class="primary-btn" id="retry-load">Skúsiť znovu</button>';$('#retry-load').onclick=()=>location.reload();} }
