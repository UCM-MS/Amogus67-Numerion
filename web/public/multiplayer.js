const API='/api/multiplayer', ROOM_STORAGE='lumen-multiplayer-room-v1';
const esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const finite=value=>Number.isFinite(Number(value))?Number(value):0;
const time=value=>typeof value==='number'?value:(Number.isFinite(Number(value))?Number(value):Date.parse(value)||0);
const codeValue=value=>String(value??'').trim().toUpperCase().replace(/[^A-Z0-9]/g,'').slice(0,10);
const subjects={math:'Matematika',science:'Veda',nature:'Príroda',astral:'Astrálny výskum'};
const moves={strike:{name:'Útok',description:'Správna odpoveď zasiahne súpera.'},guard:{name:'Obrana',description:'Správna odpoveď posilní štít.'},focus:{name:'Sústredenie',description:'Správna odpoveď pripraví silnejší ďalší úder.'}};
const petKinds=new Set(['bunny','cat','fox','owl','dragon','robot']);
const onlineName=(value,fallback='Objaviteľ')=>[...String(value??'').normalize('NFKC').replace(/[^\p{L}\p{M}\p{N}\s_.-]/gu,'').replace(/\s+/gu,' ').trim()].slice(0,20).join('').trim()||fallback;
const rankLabel=rank=>rank!==null&&rank!==undefined&&String(rank).trim()!==''&&Number.isFinite(Number(rank))&&Number(rank)>=1?`Poradie #${Math.floor(Number(rank))}`:String(rank||'Zatiaľ bez poradia');
function cleanPet(pet={}){return {kind:petKinds.has(pet.kind)?pet.kind:'robot',color:/^#[\da-f]{6}$/i.test(pet.color||'')?pet.color:'#91d3ee',name:onlineName(pet.name,'Akademik')};}
function equippedPet(state={}){const pets=Array.isArray(state.pets)?state.pets:[];return cleanPet(pets.find(p=>p.id===state.equippedPet)||{name:'Akademik',kind:'robot',color:'#91d3ee'});}
function rememberedRoom(){try{return codeValue(localStorage.getItem(ROOM_STORAGE));}catch{return '';}}
function rememberRoom(code){try{if(code)localStorage.setItem(ROOM_STORAGE,code);else localStorage.removeItem(ROOM_STORAGE);}catch{}}
function shareUrl(code){const url=new URL(location.href);url.searchParams.set('room',code);url.hash='';return url.href;}

/** Cookie-authenticated, server-authoritative arena. Closing pauses polling, leaving exits the room. */
export function createMultiplayer({openModal,closeModal,notify=()=>{},getState=()=>({}),petArt=()=>'',icon=()=>'',heading=(a,b,c)=>`<span class="modal-eyebrow">${a}</span><h2>${b}</h2><p>${c}</p>`,onReward=()=>{}}){
  let active=false,disposed=false,generation=0,sessionReady=false,me=null,gearDefs=[],room=null,leaderboard=[];
  let view='lobby',mode='quiz',move='strike',selectedAnswer='',submittedRound=null,busy='',error='',pendingCode='';
  let pollTimer=null,clockTimer=null,polling=false,initializing=false,retryDelay=1000,serverOffset=0,renderKey='',lastQuestion=null;
  const controllers=new Set(),rewardedRooms=new Set();
  const shell=()=>document.querySelector('#modal-content .mp-shell')||document.querySelector('#modal-root .mp-shell')||document.querySelector('.mp-shell');
  const mine=()=>room?.players?.find(p=>p.id===me?.id);
  const running=()=>room&&['question','reveal'].includes(room.status);
  const button=(label,action,kind='secondary',disabled=false,extra='')=>`<button type="button" class="${kind}-btn" data-mp-action="${action}" ${disabled?'disabled':''} ${extra}>${label}</button>`;
  const profile=()=>me?`<div><span class="tag">${esc(rankLabel(me.rank))}</span><strong>${esc(me.name)}</strong></div><div><span>Rating <strong>${Math.round(finite(me.rating))}</strong></span><span>${icon('star')} <strong>${Math.max(0,Math.round(finite(me.shards)))}</strong> úlomkov</span></div>`:'<span>Pripájam tvoj profil…</span>';
  const status=()=>error?`<span>${esc(error)}</span>${button('Skúsiť znova','retry','text')}`:`<span>● Online aréna</span><span>${room?'Miestnosť '+esc(room.code):'Pozvi skutočných kamarátov'}</span>`;
  const tabs=()=>`<nav class="mp-tabs" aria-label="Aréna">${[['lobby','Hrať'],['leaderboard','Rebríček'],['gear','Výbava']].map(([id,label])=>`<button type="button" class="${view===id?'primary':'secondary'}-btn" data-mp-view="${id}" aria-pressed="${view===id}" ${running()&&id!=='lobby'?'disabled':''}>${label}</button>`).join('')}</nav>`;
  const playerCards=()=>`<div class="mp-player-grid">${(room?.players||[]).map(p=>{
    const self=p.id===me?.id,correct=room.reveal?.results?.find(r=>r.playerId===p.id)?.correct;
    const hp=Math.max(0,finite(p.hp)),maxHp=Math.max(120,finite(p.maxHp)||120),health=Math.min(100,hp/maxHp*100);
    return `<article class="mp-player-card ${self?'self':''} ${p.answered?'active':''} ${room.winnerId===p.id?'winner':''}"><div class="mp-player-pet">${petArt(cleanPet(p.pet))}</div><div class="mp-player-meta"><strong>${esc(p.name)} ${self?'<span class="tag">TY</span>':''}</strong><small>${esc(p.pet?.name||'Akademik')}</small><span>${room.mode==='duel'?`${Math.round(hp)} HP`:`${Math.round(finite(p.score))} bodov`}</span>${room.mode==='duel'?`<div class="mp-health" role="progressbar" aria-label="Život ${esc(p.name)}" aria-valuemin="0" aria-valuemax="${maxHp}" aria-valuenow="${hp}"><span class="mp-health-fill" style="width:${health}%"></span></div>`:''}<small data-mp-player-status>${p.connected===false&&room.status!=='finished'?'Znovu sa pripája…':room.status==='lobby'?'Pripravený':room.status==='question'?(p.answered?'Odpoveď odoslaná':'Premýšľa…'):room.status==='reveal'?(correct?'Správna odpoveď':'Nový poznatok'):room.winnerId===p.id?'Víťaz zápasu':'Zápas dokončený'}</small></div></article>`;
  }).join('')}</div>`;
  const gearPanel=()=>`<div class="mp-gear-grid">${(gearDefs.length?gearDefs:['power','shield','focus'].map(slot=>({slot,name:{power:'Silný úder',shield:'Pevný štít',focus:'Jasná myseľ'}[slot],description:'Výbava pre vedomostný súboj.',level:me?.gear?.[slot]||0,maxLevel:3,cost:null}))).map(g=>{
    const level=finite(g.level??me?.gear?.[g.slot]),max=finite(g.maxLevel)||3,cost=g.cost===null?null:finite(g.cost);
    return `<article class="mp-gear-card"><span class="tag">ÚROVEŇ ${level} / ${max}</span><h3>${esc(g.name)}</h3><p>${esc(g.description)}</p>${button(level>=max?'Dokončené':cost===null?'Cena sa načítava':`${icon('star')} ${cost} úlomkov · Vylepšiť`,'gear','primary',!!busy||level>=max||cost===null||finite(me?.shards)<cost,`data-mp-slot="${esc(g.slot)}"`)}</article>`;
  }).join('')}</div><p class="small-note">Výbavu a úlomky uchováva tvoj online profil. Úlomky získavaš dokončením zápasov.</p>`;
  const leaderboardPanel=()=>`<div class="mp-leaderboard"><div class="mp-actions"><h3>Objavitelia na vrchole</h3>${button('Obnoviť','leaderboard','text',!!busy)}</div>${leaderboard.length?`<ol>${leaderboard.map((p,index)=>`<li class="${p.id===me?.id?'self':''}"><span>${index+1}</span><div><strong>${esc(p.name)}</strong><small>${esc(rankLabel(p.rank))} · ${Math.round(finite(p.wins))} výhier / ${Math.round(finite(p.matches))} zápasov</small></div><strong>${Math.round(finite(p.rating))}</strong></li>`).join('')}</ol>`:'<div class="mp-empty"><p>Zatiaľ tu nie sú dokončené zápasy. Prvá výhra môže byť tvoja.</p></div>'}</div>`;
  const home=()=>`<div class="mp-mode-grid">${[['quiz','Kvíz s kamarátmi','2 až 8 hráčov · 6 spoločných otázok','Zbierajte body správnymi odpoveďami. Každé kolo odhalí vysvetlenie.'],['duel','Súboj spoločníkov','2 hráči · vedomosti a taktika','Zvoľ útok, obranu alebo sústredenie a pošli svojho spoločníka do arény.']].map(([id,name,meta,description])=>`<article class="mp-mode-card ${mode===id?'selected':''}"><span class="tag">${meta}</span><h3>${name}</h3><p>${description}</p>${button('Vytvoriť miestnosť '+icon('arrow'),'create','primary',!sessionReady||!!busy,`data-mp-mode="${id}"`)}</article>`).join('')}</div><form id="mp-join-form" class="mp-share"><label for="mp-room-code">Máš kód od kamaráta?</label><div><input id="mp-room-code" name="room" class="mp-code" inputmode="text" autocomplete="off" spellcheck="false" maxlength="10" placeholder="AB12CD" required aria-describedby="mp-join-hint">${button('Pripojiť sa','join','primary',!sessionReady||!!busy)}</div><small id="mp-join-hint">Pošli mu odkaz alebo zadaj kód jeho miestnosti.</small></form><p class="small-note">Tvoj spoločník: <strong>${esc(equippedPet(getState()).name)}</strong>. Ak ešte žiadneho nemáš, sprevádza ťa Akademik.</p>`;
  const lobby=()=>`<div class="mp-lobby"><div class="mp-share"><label for="mp-invite-link">Pozvi kamaráta do miestnosti <strong class="mp-code">${esc(room.code)}</strong></label><div><input id="mp-invite-link" readonly value="${esc(shareUrl(room.code))}" aria-label="Odkaz na pozvanie">${button('Kopírovať odkaz','copy','secondary')}</div><small>${room.mode==='duel'?'Súboj sa začne s dvomi hráčmi.':'Kvíz sa začne s 2 až 8 hráčmi.'}</small></div><div data-mp-players>${playerCards()}</div><div class="mp-actions">${room.hostId===me?.id?button('Spustiť zápas '+icon('arrow'),'start','primary',(room.players?.length||0)<2||!!busy):'<p>Čakáme, kým hostiteľ spustí zápas.</p>'}${button('Odísť z miestnosti','leave','text',!!busy)}</div><p class="small-note">Pri zatvorení arény môžeš miestnosť znovu otvoriť. Zápas riadi spoločný server.</p></div>`;
  const round=()=>{
    const reveal=room.status==='reveal',q=room.question||lastQuestion||{},selfResult=room.reveal?.results?.find(r=>r.playerId===me?.id);
    return `<div class="mp-round-bar"><span class="tag">${room.mode==='duel'?'SÚBOJ SPOLOČNÍKOV':'KVÍZ S KAMARÁTMI'} · KOLO ${room.round} / ${room.totalRounds||6}</span><div class="mp-timer" role="timer" aria-label="Zostávajúci čas" aria-live="off"><span data-mp-countdown>–</span> s</div></div><div data-mp-players>${playerCards()}</div><span class="tag">${esc(subjects[q.subject]||'Poznanie')}</span><h3 class="mp-question" tabindex="-1">${esc(q.prompt||'Pripravuje sa ďalšia otázka…')}</h3>${room.mode==='duel'?`<div class="mp-moves" role="group" aria-label="Taktika spoločníka">${Object.entries(moves).map(([id,m])=>`<button type="button" class="mp-move ${move===id?'selected':''}" data-mp-move="${id}" aria-pressed="${move===id}" title="${esc(m.description)}" ${reveal?'disabled':''}><strong>${m.name}</strong><small>${m.description}</small></button>`).join('')}</div>`:''}<div class="mp-answer-grid">${(q.options||[]).map((option,index)=>{
      const correct=reveal&&option.id===room.reveal?.correctAnswerId,selected=option.id===selectedAnswer;
      return `<button type="button" class="mp-answer ${selected?'selected':''} ${correct?'correct':''} ${reveal&&selected&&!correct?'wrong':''}" data-mp-answer="${esc(option.id)}" aria-pressed="${selected}" ${reveal?'disabled':''}><span class="choice-letter">${String.fromCharCode(65+index)}</span><span>${esc(option.text)}</span></button>`;
    }).join('')}</div><div class="mp-feedback ${reveal?(selfResult?.correct?'correct':'learning'):''}" role="status" aria-live="polite">${reveal?`<strong>${selfResult?.correct?'Správne!':'Ďalší poznatok pre teba.'}</strong><p>${esc(room.reveal?.explanation||'')}</p><small>${selfResult?`${Math.round(finite(selfResult.points))} bodov${room.mode==='duel'?` · ${Math.round(finite(selfResult.damage))} zásah`:''}`:'Ďalšie kolo sa pripravuje.'}</small>`:(mine()?.answered||submittedRound===room.round?'Odpoveď je odoslaná. Počkáme na ostatných.':'Vyber jednu odpoveď. Každý hráč odpovedá raz za kolo.')}</div><div class="mp-actions">${button('Odísť zo zápasu','leave','text',!!busy)}</div>`;
  };
  const finished=()=>{
    const ordered=[...(room.players||[])].sort((a,b)=>room.mode==='duel'?(finite(b.hp)-finite(a.hp)||finite(b.score)-finite(a.score)):finite(b.score)-finite(a.score)),winner=ordered.find(p=>p.id===room.winnerId);
    const reward=room.rewards?.find(r=>r.playerId===me?.id),ratingDelta=reward?.ratingDelta;
    const rewardCopy=reward?(finite(reward.shards)>0?`Získal si <strong>${Math.round(finite(reward.shards))} úlomkov</strong>.`:'Za tento zápas nepribudli úlomky. Odmenu získaš za aspoň jednu správnu odpoveď a dokončenie zápasu.'):'Výsledok je uložený v tvojom online profile.';
    return `<div class="mp-result"><span class="tag">ZÁPAS DOKONČENÝ</span><h3>${room.winnerId===me?.id?'Tvoje vedomosti vyhrali!':winner?`${esc(winner.name)} víťazí!`:'Vyrovnaný zápas!'}</h3><p>${rewardCopy}${ratingDelta!=null?` Rating ${finite(ratingDelta)>=0?'+':''}${Math.round(finite(ratingDelta))}.`:''}</p><div data-mp-players>${playerCards()}</div><ol class="mp-results-list">${ordered.map(p=>`<li><strong>${esc(p.name)}${p.id===me?.id?' · TY':''}</strong><span>${Math.round(finite(p.score))} bodov${room.mode==='duel'?` · ${Math.max(0,Math.round(finite(p.hp)))} HP`:''}</span></li>`).join('')}</ol><p><strong>${esc(rankLabel(me?.rank))}</strong> · rating ${Math.round(finite(me?.rating))}</p><div class="mp-actions">${button('Nový zápas','leave','primary',!!busy)}${button('Vylepšiť výbavu','view-gear','secondary')}</div></div>`;
  };
  const content=()=>view==='gear'?gearPanel():view==='leaderboard'?leaderboardPanel():!room?home():room.status==='lobby'?lobby():room.status==='finished'?finished():round();

  function updateControls(){
    const root=shell();if(!root)return;
    const expired=room&&time(room.deadline)<=Date.now()+serverOffset;
    const locked=!room||room.status!=='question'||!!mine()?.answered||submittedRound===room.round||!!busy||expired;
    root.querySelectorAll('[data-mp-answer],[data-mp-move]').forEach(b=>b.disabled=locked);
    const start=root.querySelector('[data-mp-action="start"]');if(start)start.disabled=!!busy||(room?.players?.length||0)<2;
    root.querySelectorAll('[data-mp-action="create"],[data-mp-action="join"]').forEach(b=>b.disabled=!!busy||!sessionReady);
    root.querySelectorAll('[data-mp-action="leave"],[data-mp-action="leaderboard"]').forEach(b=>b.disabled=!!busy);
    root.querySelectorAll('[data-mp-answer]').forEach(b=>{b.classList.toggle('selected',b.dataset.mpAnswer===selectedAnswer);b.setAttribute('aria-pressed',String(b.dataset.mpAnswer===selectedAnswer));});
  }
  function updateCountdown(){
    const root=shell();if(!root||!room)return;
    const remaining=Math.max(0,Math.ceil((time(room.deadline)-Date.now()-serverOffset)/1000));
    const display=root.querySelector('[data-mp-countdown]');if(display)display.textContent=String(remaining);
    root.querySelector('.mp-timer')?.classList.toggle('urgent',room.status==='question'&&remaining<=5);
    updateControls();
  }
  function render(force=false){
    if(!active)return;
    const root=shell();if(!root)return;
    const key=`${view}:${room?.code||'home'}:${room?.status||'home'}:${room?.round||0}:${room?.question?.id||''}:${room?.status==='lobby'?room.hostId:''}:${sessionReady}`;
    const profileNode=root.querySelector('.mp-profile');if(profileNode)profileNode.innerHTML=profile();
    const statusNode=root.querySelector('.mp-status');if(statusNode){statusNode.innerHTML=status();statusNode.classList.toggle('offline',!!error);}
    if(!force&&key===renderKey){
      const players=root.querySelector('[data-mp-players]');if(players)players.innerHTML=playerCards();
      const feedback=root.querySelector('.mp-feedback');if(feedback&&room?.status==='question'&&(mine()?.answered||submittedRound===room.round))feedback.textContent='Odpoveď je odoslaná. Počkáme na ostatných.';
      updateCountdown();return;
    }
    const before=document.activeElement,focusId=before?.id,selection=before?.selectionStart,inputValue=root.querySelector('#mp-room-code')?.value;
    const focusData=Object.fromEntries(['mpAction','mpView','mpSlot','mpMode','mpAnswer','mpMove'].filter(key=>before?.dataset?.[key]!==undefined).map(key=>[key,before.dataset[key]]));
    renderKey=key;
    root.innerHTML=`${heading('ONLINE ARÉNA','Vedomosti spájajú.',room?'Spoločný zápas. Skutoční hráči.':'Pozvi kamarátov na kvíz alebo vedomostný súboj spoločníkov.')}<div class="mp-status" role="status">${status()}</div><div class="mp-profile">${profile()}</div>${tabs()}<div class="mp-content">${content()}</div>`;
    root.closest('.modal-card')?.classList.add('wide');
    const title=root.querySelector('h2');if(title)title.id='modal-title';
    if(inputValue&&root.querySelector('#mp-room-code'))root.querySelector('#mp-room-code').value=inputValue;
    if(focusId&&root.querySelector(`#${focusId}`)){const target=root.querySelector(`#${focusId}`);target.focus({preventScroll:true});if(typeof selection==='number')try{target.setSelectionRange(selection,selection);}catch{}}
    updateCountdown();
    if(!focusId){
      const control=Object.keys(focusData).length?[...root.querySelectorAll('[data-mp-action],[data-mp-view],[data-mp-answer],[data-mp-move]')].find(node=>!node.disabled&&Object.entries(focusData).every(([key,value])=>node.dataset[key]===value)):null;
      if(control)control.focus({preventScroll:true});else if(room?.status==='question')root.querySelector('.mp-question')?.focus({preventScroll:true});
    }
  }
  async function request(path,body){
    const controller=new AbortController();controllers.add(controller);
    const timeout=setTimeout(()=>controller.abort(),12000);
    try{
      const response=await fetch(API+path,{method:body===undefined?'GET':'POST',credentials:'same-origin',cache:'no-store',headers:body===undefined?{Accept:'application/json'}:{Accept:'application/json','Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:controller.signal});
      const data=await response.json().catch(()=>({}));
      if(!response.ok){const failure=new Error(data.message||data.error||(response.status===503?'Online aréna sa práve pripája. Skús to o chvíľu.':'Pripojenie sa nepodarilo.'));failure.status=response.status;throw failure;}
      return data;
    }finally{clearTimeout(timeout);controllers.delete(controller);}
  }
  function accept(data){
    if(!active)return;
    if(data.room){
      const received=time(data.room.serverNow||data.serverNow);
      if(room?.code===data.room.code&&received&&received<time(room.serverNow))return;
      const previous=room;room=data.room;rememberRoom(room.code);
      if(received)serverOffset=received-Date.now();
      if(!previous||previous.code!==room.code){submittedRound=null;selectedAnswer='';lastQuestion=null;}
      if(previous?.round!==room.round){submittedRound=null;selectedAnswer='';move='strike';}
      if(room.question)lastQuestion=room.question;
      if(room.status==='question'&&!mine()?.answered&&submittedRound===room.round&&busy!=='answer')submittedRound=null;
    }
    if(data.me)me=data.me;if(Array.isArray(data.gearDefs))gearDefs=data.gearDefs;
    if(Array.isArray(data.players)&&!data.room)leaderboard=data.players;
    error='';retryDelay=1000;render();
    if(room?.status==='finished'&&!rewardedRooms.has(room.code)){
      rewardedRooms.add(room.code);const reward=room.rewards?.find(r=>r.playerId===me?.id);
      const earned=reward&&finite(reward.shards)>0;
      if(earned)onReward({roomCode:room.code,me,reward});
      notify(earned?`Zápas dokončený · +${Math.round(finite(reward.shards))} úlomkov.`:reward?'Zápas dokončený. Tentoraz bez odmeny.':'Výsledok zápasu je uložený.',earned?'success':'info');
    }
  }
  function abortRequests(){controllers.forEach(c=>c.abort());controllers.clear();}
  function pause(){clearTimeout(pollTimer);clearInterval(clockTimer);pollTimer=clockTimer=null;abortRequests();}
  function schedule(delay=room?1000:10000){clearTimeout(pollTimer);if(active&&!document.hidden&&room?.status!=='finished')pollTimer=setTimeout(poll,delay);}
  function startClock(){clearInterval(clockTimer);if(active&&!document.hidden)clockTimer=setInterval(updateCountdown,250);}
  function clearRoom(message=''){
    const code=room?.code;generation++;abortRequests();polling=false;busy='';room=null;lastQuestion=null;selectedAnswer='';submittedRound=null;rememberRoom('');view='lobby';error=message;
    try{const url=new URL(location.href);if(code&&codeValue(url.searchParams.get('room'))===code){url.searchParams.delete('room');history.replaceState(null,'',url.href);}}catch{}
    render(true);schedule();
  }
  async function poll(){
    if(!active||document.hidden||polling)return;
    if(!sessionReady){await initialize();return;}
    polling=true;const run=generation,requestedCode=room?.code;
    try{const data=await request(requestedCode?`/rooms/${encodeURIComponent(requestedCode)}`:'/me');if(active&&run===generation&&(!requestedCode||room?.code===requestedCode))accept(data);}
    catch(e){if(active&&run===generation&&!document.hidden){
      if(requestedCode&&room?.code===requestedCode&&[404,410].includes(e.status))clearRoom(e.message||'Miestnosť už bola uzavretá. Vytvor novú alebo zadaj nový kód.');
      else{error=e.name==='AbortError'?'Pripojenie trvá dlhšie. Obnovujem ho…':e.message;retryDelay=Math.min(10000,retryDelay*2);render();}
    }}
    finally{if(run===generation){polling=false;if(active)schedule(error?retryDelay:room?1000:10000);}}
  }
  async function perform(action,operation){
    if(busy||!active)return;
    busy=action;error='';updateControls();const run=generation,requestedCode=room?.code;
    try{const data=await operation();if(!active||run!==generation)return null;accept(data);return data;}
    catch(e){if(active&&run===generation){if(action==='leave'&&room?.code===requestedCode&&[404,410].includes(e.status))clearRoom(e.message||'Miestnosť už bola uzavretá.');else{error=e.name==='AbortError'?'Pripojenie sa prerušilo. Skús to znova.':e.message;render();}}return null;}
    finally{if(active&&run===generation){busy='';render(action==='gear'||action==='leaderboard');schedule();}}
  }
  async function join(code){
    code=codeValue(code);if(code.length<4){error='Zadaj celý kód miestnosti od kamaráta.';render();return;}
    view='lobby';return perform('join',()=>request(`/rooms/${encodeURIComponent(code)}/join`,{pet:equippedPet(getState())}));
  }
  async function initialize(){
    if(initializing||!active||document.hidden)return;
    initializing=true;
    const run=generation;
    try{
      const session=await request('/session',{name:onlineName(getState()?.player?.name)});
      if(!active||run!==generation)return;
      accept(session);
      const data=await request('/me');if(!active||run!==generation)return;sessionReady=true;accept(data);
      const explicit=pendingCode;pendingCode='';const restore=codeValue(data.activeRoomCode||data.me?.activeRoomCode||rememberedRoom());
      if(explicit){
        const joined=await join(explicit);
        if(!joined&&!room&&restore&&restore!==explicit&&active&&run===generation){
          try{accept(await request(`/rooms/${encodeURIComponent(restore)}`));notify('Pred novým pozvaním odíď zo svojej aktuálnej miestnosti.','warning');}catch{}
        }
      }
      else if(restore){try{const restored=await request(`/rooms/${encodeURIComponent(restore)}`);if(active&&run===generation)accept(restored);}catch(e){if(e.name!=='AbortError'){rememberRoom('');room=null;render(true);}}}
      schedule();
    }catch(e){if(active&&run===generation&&!document.hidden){error=e.name==='AbortError'?'Aréna sa nestihla pripojiť. Skús to znova.':e.message;render();schedule(retryDelay);}}
    finally{if(run===generation)initializing=false;}
  }
  async function open(){
    if(disposed)return;
    if(active&&shell()){render();return;}
    active=true;generation++;initializing=false;busy='';error='';renderKey='';view='lobby';
    openModal('<div class="mp-shell"></div>','multiplayer');render(true);startClock();
    await initialize();
  }
  function close(){const own=!!shell();active=false;generation++;busy='';polling=false;pause();if(own)closeModal();}
  function joinFromUrl(){let code='';try{code=codeValue(new URL(location.href).searchParams.get('room'));}catch{}if(!code||code.length<4)return false;pendingCode=code;return open();}
  async function handleClick(event){
    const b=event.target.closest('button');if(!b||!b.closest('.mp-shell')||b.disabled)return;
    if(b.dataset.mpView){view=b.dataset.mpView;render(true);if(view==='leaderboard')await perform('leaderboard',()=>request('/leaderboard'));else if(view==='gear')await perform('gear',()=>request('/me'));return;}
    if(b.dataset.mpMove){move=b.dataset.mpMove;shell()?.querySelectorAll('[data-mp-move]').forEach(node=>{const chosen=node.dataset.mpMove===move;node.classList.toggle('selected',chosen);node.setAttribute('aria-pressed',String(chosen));});return;}
    if(b.dataset.mpAnswer){
      if(!room||room.status!=='question'||mine()?.answered||submittedRound===room.round||busy||time(room.deadline)<=Date.now()+serverOffset)return;
      selectedAnswer=b.dataset.mpAnswer;submittedRound=room.round;updateControls();
      const code=room.code,roundNumber=room.round;const result=await perform('answer',()=>request(`/rooms/${encodeURIComponent(code)}/answer`,{round:roundNumber,answerId:selectedAnswer,move}));
      if(!result&&active)await poll();return;
    }
    switch(b.dataset.mpAction){
      case 'create':mode=b.dataset.mpMode==='duel'?'duel':'quiz';await perform('create',()=>request('/rooms',{mode,pet:equippedPet(getState())}));break;
      case 'join':await join(shell()?.querySelector('#mp-room-code')?.value);break;
      case 'start':if(room?.hostId===me?.id)await perform('start',()=>request(`/rooms/${encodeURIComponent(room.code)}/start`,{}));break;
      case 'leave':if(room){const code=room.code;const result=await perform('leave',()=>request(`/rooms/${encodeURIComponent(code)}/leave`,{}));if(result&&active)clearRoom();}break;
      case 'copy':{const input=shell()?.querySelector('#mp-invite-link');if(!input)break;try{await navigator.clipboard.writeText(input.value);notify('Odkaz je skopírovaný. Pošli ho kamarátovi.','success');}catch{input.focus();input.select();notify('Označený odkaz skopíruj klávesmi Ctrl+C.');}break;}
      case 'gear':if(['power','shield','focus'].includes(b.dataset.mpSlot))await perform('gear',()=>request('/gear',{slot:b.dataset.mpSlot}));break;
      case 'leaderboard':await perform('leaderboard',()=>request('/leaderboard'));break;
      case 'view-gear':view='gear';render(true);await perform('gear',()=>request('/me'));break;
      case 'retry':error='';render();if(!sessionReady)await initialize();else await poll();break;
    }
  }
  function handleSubmit(event){if(event.target.id!=='mp-join-form'||!active)return;event.preventDefault();void join(shell()?.querySelector('#mp-room-code')?.value);}
  function visibility(){if(!active)return;if(document.hidden)pause();else{startClock();void poll();}}
  document.addEventListener('click',handleClick);document.addEventListener('submit',handleSubmit);document.addEventListener('visibilitychange',visibility);
  const observer=typeof MutationObserver!=='undefined'?new MutationObserver(()=>{if(active&&!shell()){active=false;generation++;busy='';pause();}}):null;
  observer?.observe(document.getElementById('modal-root')||document.getElementById('modal-content')||document.body,{childList:true,subtree:true});
  function dispose(){close();disposed=true;observer?.disconnect();document.removeEventListener('click',handleClick);document.removeEventListener('submit',handleSubmit);document.removeEventListener('visibilitychange',visibility);}
  return {open,close,dispose,joinFromUrl};
}
