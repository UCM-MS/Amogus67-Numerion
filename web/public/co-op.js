const API='/api/multiplayer', STORAGE='lumen-coop-world-v1', REWARDS='lumen-coop-rewards-v1';
const esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const name=(value,fallback='Objaviteľ')=>[...String(value??'').normalize('NFKC').replace(/[^\p{L}\p{M}\p{N}\s_.-]/gu,'').replace(/\s+/gu,' ').trim()].slice(0,20).join('').trim()||fallback;
const code=value=>String(value??'').trim().toUpperCase().replace(/[^A-Z2-9]/g,'').slice(0,6);
const finite=value=>Number.isFinite(Number(value))?Number(value):0;
const clone=value=>value?JSON.parse(JSON.stringify(value)):value;
const read=key=>{try{return localStorage.getItem(key);}catch{return null;}};
const write=(key,value)=>{try{value===null?localStorage.removeItem(key):localStorage.setItem(key,value);}catch{}};
function pet(state){const p=state?.pets?.find(p=>p.id===state.equippedPet)||{};return {kind:['bunny','cat','fox','owl','dragon','robot'].includes(p.kind)?p.kind:'robot',color:/^#[\da-f]{6}$/i.test(p.color||'')?p.color:'#91d3ee',name:name(p.name,'Akademik')};}

/** Shared walking stays connected when its modal closes. Identity lives only in the HttpOnly cookie. */
export function createCoop({openModal,closeModal,notify=()=>{},getState=()=>({}),onPeers=()=>{},onStatus=()=>{},onGoal=()=>{},onReward=()=>{},onPosition=()=>{},onEmote=()=>{}}){
  let world=null,meId=null,me=null,lastPet='',disposed=false,busy=false,currentAction='',transport=false,generation=0,sessionPromise=null,resumePromise=null;
  let latest={position:{x:0,y:0,z:13,yaw:0},moving:false,onGround:true},lastSent=null,lastSend=-Infinity,lastNetwork=-Infinity,retryAt=0,backoff=750,error='',lastStatus='',renderKey='',pendingEmote=null,lastEmote=-Infinity,pendingLeave=false;
  const controllers=new Set(),collectCooldown=new Map(),teamWarnings=new Set();
  let rewarded;try{rewarded=new Set(JSON.parse(read(REWARDS)||'[]'));}catch{rewarded=new Set();}
  const shell=()=>document.querySelector('#modal-root .coop-shell')||document.querySelector('.coop-shell');
  const urlFor=worldCode=>{const url=new URL(location.href);url.searchParams.set('world',worldCode);url.searchParams.delete('room');url.hash='';return url.href;};
  const button=(label,action,style='secondary',disabled=false)=>`<button type="button" class="${style}-btn" data-coop-action="${action}" ${disabled?'disabled':''}>${label}</button>`;
  function status(connected){const value={connected,code:world?.code||null,count:world?.players?.length||0,...(error?{error}:{})};const key=JSON.stringify(value);if(key!==lastStatus){lastStatus=key;onStatus(value);}}
  function clearWorld(){const previousCode=world?.code;generation++;controllers.forEach(c=>c.abort());world=null;lastSent=null;lastPet='';busy=false;currentAction='';pendingLeave=false;pendingEmote=null;write(STORAGE,null);collectCooldown.clear();teamWarnings.clear();try{const url=new URL(location.href);if(previousCode&&code(url.searchParams.get('world'))===previousCode){url.searchParams.delete('world');history.replaceState(null,'',url.href);}}catch{}onPeers([],meId);onGoal([],null);status(false);renderKey='';render(true);}
  async function request(path,body){
    const controller=new AbortController();controllers.add(controller);const timeout=setTimeout(()=>controller.abort(),10000);
    try{const response=await fetch(API+path,{method:body===undefined?'GET':'POST',credentials:'same-origin',cache:'no-store',headers:body===undefined?{Accept:'application/json'}:{Accept:'application/json','Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:controller.signal});
      const data=await response.json().catch(()=>({}));if(!response.ok){const failure=new Error(data.message||'Spoločný svet sa nepodarilo pripojiť.');failure.status=response.status;failure.code=data.error;failure.data=data;failure.path=path;throw failure;}return data;
    }finally{clearTimeout(timeout);controllers.delete(controller);}
  }
  function accept(data){
    if(disposed)return;if(data.me){me=data.me;meId=data.me.id;}if(data.meId)meId=data.meId;
    if(data.world){if(world?.code===data.world.code&&finite(data.world.serverNow)<finite(world.serverNow))return;if(world?.goal?.needsTeam&&!data.world.goal?.needsTeam){collectCooldown.clear();teamWarnings.clear();}world=data.world;write(STORAGE,world.code);
      onPeers((world.players||[]).filter(p=>p.id!==meId).map(clone),meId);onGoal(clone(world.beacons||[]),clone(world.goal));
    }
    error='';backoff=750;retryAt=0;status(!!world);render();
    const reward=data.reward;if(reward?.id&&finite(reward.shards)>0&&!rewarded.has(reward.id)){rewarded.add(reward.id);while(rewarded.size>40)rewarded.delete(rewarded.values().next().value);write(REWARDS,JSON.stringify([...rewarded]));onReward(clone(reward));notify(`Spoločná výprava dokončená · +${finite(reward.shards)} úlomkov.`, 'success');}
  }
  async function ensureSession(force=false){
    if(meId&&!force)return;if(sessionPromise)return sessionPromise;const run=generation;
    sessionPromise=(async()=>{const data=await request('/session',{name:name(getState()?.player?.name)});if(!disposed&&run===generation)accept(data);})();
    try{await sessionPromise;}finally{sessionPromise=null;}
  }
  const canonical=p=>p&&['x','y','z','yaw'].every(k=>Number.isFinite(p[k]));
  function correctPosition(position){latest={position:clone(position),moving:false,onGround:true};lastSent=clone(latest);lastSend=Date.now();onPosition(clone(position));}
  function failure(e){if(disposed||e.name==='AbortError')return;const correction=e.data?.position||e.data?.world?.players?.find(p=>p.id===meId)?.position;if(e.path?.endsWith('/move')&&canonical(correction)){correctPosition(correction);retryAt=Date.now()+250;error='';status(!!world);render();return;}error=e.message;retryAt=Date.now()+backoff;backoff=Math.min(10000,backoff*2);if([403,404,410].includes(e.status)&&e.code!=='not_a_member'&&world)clearWorld();else{status(false);render();}}
  async function requestJoin(worldCode,run=generation){let sent=clone(latest);const currentPet=pet(getState()),petSignature=JSON.stringify(currentPet);try{return {data:await request(`/worlds/${worldCode}/join`,{pet:currentPet,position:sent.position}),sent,petSignature};}catch(e){if(run!==generation||disposed||!canonical(e.data?.position))throw e;correctPosition(e.data.position);sent=clone(latest);return {data:await request(`/worlds/${worldCode}/join`,{pet:currentPet,position:sent.position}),sent,petSignature};}}
  async function connect(worldCode,{joining=false,creating=false}={}){
    if(busy||disposed)return null;busy=true;currentAction='connect';error='';render();let run=generation;
    try{await ensureSession();if(disposed||run!==generation)return null;const initial=clone(latest),payload={pet:pet(getState()),position:clone(initial.position)};
      let data,sent=initial;if(joining){const joined=await requestJoin(worldCode,run);data=joined.data;sent=joined.sent;}else{try{data=await request(creating?'/worlds':`/worlds/${worldCode}`,creating?payload:undefined);}catch(e){if(!creating&&e.status===403&&e.code==='not_a_member'){const joined=await requestJoin(worldCode,run);data=joined.data;sent=joined.sent;}else throw e;}}
      if(disposed||run!==generation)return null;accept(data);const restored=!creating&&!joining?world?.players?.find(p=>p.id===meId)?.position:null;if(canonical(restored)){correctPosition(restored);lastSent=clone(latest);}else lastSent=sent;lastPet=JSON.stringify(!creating&&!joining?world?.players?.find(p=>p.id===meId)?.pet||{}:payload.pet);lastSend=lastNetwork=Date.now();return data;
    }catch(e){failure(e);if(!world&&[403,404,410].includes(e.status))write(STORAGE,null);return null;}
    finally{if(run===generation){busy=false;currentAction='';render();}}
  }
  async function join(worldCode){worldCode=code(worldCode);if(worldCode.length!==6){error='Zadaj šesťznakový kód spoločného sveta.';render(true);return null;}return connect(worldCode,{joining:true});}
  async function resume(){
    if(resumePromise||world)return resumePromise;const explicit=code(new URL(location.href).searchParams.get('world')),stored=code(read(STORAGE));if(!explicit&&!stored)return false;
    resumePromise=(async()=>{if(explicit)return !!(await join(explicit));return !!(await connect(stored));})();
    try{return await resumePromise;}finally{resumePromise=null;}
  }
  async function open(){if(disposed)return;openModal('<div class="coop-shell"></div>','coop');renderKey='';render(true);try{await ensureSession();if(!world)await resume();render();}catch(e){failure(e);}}
  function render(force=false){
    const root=shell();if(!root||disposed)return;const key=JSON.stringify([world?.code,(world?.players||[]).map(p=>[p.id,p.name]),world?.goal?.collected,world?.goal?.complete,world?.goal?.needsTeam,meId,error,busy,pendingEmote]);if(!force&&key===renderKey)return;
    const oldInput=root.querySelector('#coop-code-input')?.value||'',focusId=document.activeElement?.id;renderKey=key;
    const content=world?`<div class="coop-share"><label for="coop-invite">Spoločný svet · ${world.players?.length||1} / 8 hráčov</label><strong class="coop-code">${esc(world.code)}</strong><div class="coop-input-row"><input id="coop-invite" readonly value="${esc(urlFor(world.code))}" aria-label="Odkaz na spoločný svet">${button('Kopírovať odkaz','copy')}</div><small>Pošli kamarátovi tento odkaz. Uvidíte sa na tom istom ostrove ako skutoční hráči.</small></div><div class="coop-players">${(world.players||[]).map(p=>`<div class="coop-player"><span aria-hidden="true">${esc([...name(p.name)][0])}</span><div><strong>${esc(p.name)}${p.id===meId?' · ty':''}</strong><small>${p.id===world.hostId?'Zakladateľ sveta':'Kamarát na výprave'}</small></div></div>`).join('')}</div><div class="coop-goal"><strong>${world.goal?.complete?'Výprava dokončená!':'Spoločne rozsvieťte ostrov'} · ${finite(world.goal?.collected)} / ${finite(world.goal?.total)||6}</strong><p>${world.goal?.complete?'Každý, kto zbieral, získal 60 úlomkov.':world.goal?.needsTeam?'Posledný maják rozsvietite s dvomi kamarátmi, ktorí zbierali.':'Nájdite šesť žiariacich majákov. Aspoň dvaja kamaráti sa zapoja do zbierania.'}</p><div class="progress-track" role="progressbar" aria-label="Spoločné majáky" aria-valuemin="0" aria-valuemax="6" aria-valuenow="${Math.min(6,finite(world.goal?.collected))}"><span style="width:${Math.min(100,finite(world.goal?.collected)/6*100)}%"></span></div></div><div class="coop-actions">${button('Vrátiť sa do sveta','close','primary')}${world.goal?.complete?'':'<button type="button" class="secondary-btn" data-action="find-beacon">Najbližší maják</button>'}${button('Zamávať','wave','secondary',busy||!!pendingEmote)}${button('Odísť zo spoločného sveta','leave','text',busy)}</div>`:
      `<div class="coop-hero"><strong>Jeden ostrov. Skutoční kamaráti.</strong><p>Prechádzajte sa spolu, skáčte po dráhe a pomáhajte si so spoločnou výpravou. Do sveta sa zmestí osem hráčov.</p></div><div class="coop-actions">${button('Vytvoriť spoločný svet','create','primary',busy||!meId)}</div><form id="coop-join-form" class="coop-join"><label for="coop-code-input">Máš kód od kamaráta?</label><div class="coop-input-row"><input id="coop-code-input" class="coop-code-input" maxlength="6" autocomplete="off" spellcheck="false" placeholder="AB23CD" required aria-label="Šesťznakový kód sveta">${button('Pripojiť sa','join','secondary',busy||!meId)}</div></form>`;
    root.innerHTML=`<span class="coop-kicker">DOBRODRUŽSTVO S KAMARÁTMI</span><h2 id="modal-title">Hrať spolu</h2><p>${world?'Zatvor túto kartu a pokračujte spolu po ostrove.':'Pozvi kamaráta do tvojho 3D sveta.'}</p>${error?`<div class="coop-error" role="alert">${esc(error)} ${button('Skúsiť znova','retry','text',busy)}</div>`:''}${content}<p class="small-note">Postup akadémie zostáva tvoj. Spoločné majáky a online odmena patria výprave v tejto miestnosti.</p>`;
    if(oldInput&&root.querySelector('#coop-code-input'))root.querySelector('#coop-code-input').value=oldInput;
    if(focusId)root.querySelector(`#${focusId}`)?.focus({preventScroll:true});
  }
  const changed=()=>!lastSent||latest.moving!==lastSent.moving||latest.onGround!==lastSent.onGround||['x','y','z','yaw'].some(k=>Math.abs(latest.position[k]-lastSent.position[k])>.01);
  async function tick(){
    if(disposed||document.hidden||busy||transport||Date.now()<retryAt)return;if(pendingLeave){await leave();return;}if(!world){if(read(STORAGE))await resume();else if(shell()&&!meId){try{await ensureSession();render();}catch(e){failure(e);}}return;}const current=Date.now(),currentPet=pet(getState()),petSignature=JSON.stringify(currentPet),petChanged=petSignature!==lastPet;let sending=false;
    if(((changed()||pendingEmote||petChanged)&&current-lastSend>=250)||current-lastSend>=2000)sending=true;else if(current-lastNetwork<750)return;
    transport=true;const run=generation,worldCode=world.code,sent=clone(latest),emote=pendingEmote;
    try{const data=await request(`/worlds/${worldCode}${sending?'/move':''}`,sending?{...sent,...(petChanged?{pet:currentPet}:{}),...(emote?{emote}:{})}:undefined);if(disposed||run!==generation)return;
      if(sending){lastSent=sent;if(petChanged)lastPet=petSignature;lastSend=current;if(pendingEmote===emote)pendingEmote=null;}lastNetwork=Date.now();accept(data);
    }catch(e){if(run!==generation||disposed)return;if(e.status===401||(e.status===403&&e.code==='not_a_member')){try{if(e.status===401){meId=null;await ensureSession(true);}const joined=await requestJoin(worldCode,run);if(run===generation){lastSent=joined.sent;lastPet=joined.petSignature;lastSend=lastNetwork=Date.now();accept(joined.data);}}catch(reconnectError){if(run===generation)failure(reconnectError);}}else failure(e);}
    finally{transport=false;}
  }
  function updatePosition(position,yaw,moving,onGround){latest={position:{x:finite(position.x),y:finite(position.y),z:finite(position.z),yaw:finite(yaw)},moving:!!moving,onGround:!!onGround};}
  async function tryCollect(id){
    const current=Date.now();if(!world||disposed||document.hidden||busy||transport||current<(collectCooldown.get(id)||0)||current<retryAt)return false;
    if(!world.beacons?.some(b=>b.id===id&&!b.collected))return false;if(changed()&&current-lastSend<250)return false;
    busy=true;currentAction='collect';collectCooldown.set(id,current+1500);const run=generation,worldCode=world.code;
    try{if(changed()){const sent=clone(latest),moved=await request(`/worlds/${worldCode}/move`,sent);if(run!==generation||disposed)return false;lastSent=sent;lastSend=Date.now();accept(moved);}
      const data=await request(`/worlds/${worldCode}/collect`,{id});if(run!==generation||disposed)return false;lastNetwork=Date.now();accept(data);return !!world?.beacons?.find(b=>b.id===id)?.collected;
    }catch(e){if(run===generation){if(e.code==='need_team'){if(!teamWarnings.has(id)){teamWarnings.add(id);notify(e.message,'warning');}collectCooldown.set(id,current+5000);}else failure(e);}return false;}
    finally{if(run===generation){busy=false;currentAction='';render();}}
  }
  async function teleport(kind,checkpoint){
    if(!world||disposed||(busy&&currentAction!=='collect')||!['spawn','course-start','course-checkpoint'].includes(kind)||(kind==='course-checkpoint'&&(!Number.isInteger(checkpoint)||checkpoint<0||checkpoint>6)))return null;busy=true;currentAction='teleport';const run=++generation;controllers.forEach(c=>c.abort());
    try{const data=await request(`/worlds/${world.code}/move`,{...clone(latest),teleport:kind,...(kind==='course-checkpoint'?{checkpoint}:{})});if(run!==generation||disposed)return null;accept(data);const position=world.players?.find(p=>p.id===meId)?.position;if(position){latest={position:clone(position),moving:false,onGround:true};lastSent=clone(latest);lastSend=lastNetwork=Date.now();}return clone(position)||null;
    }catch(e){if(run===generation)failure(e);return null;}finally{if(run===generation){busy=false;currentAction='';render();}}
  }
  async function leave(){if(disposed)return false;const worldCode=world?.code;busy=true;currentAction='leave';generation++;controllers.forEach(c=>c.abort());let finished=false;
    try{if(worldCode)await request(`/worlds/${worldCode}/leave`,{});finished=true;}
    catch(e){if([401,403,404,410].includes(e.status))finished=true;else{pendingLeave=true;if(e.name==='AbortError'){retryAt=Date.now()+750;status(false);}else failure(e);}}
    finally{busy=false;currentAction='';if(finished){error='';clearWorld();}else render(true);}return finished;
  }
  async function handleClick(event){const target=event.target.closest('[data-coop-action]');if(!target||!target.closest('.coop-shell')||target.disabled||disposed)return;
    switch(target.dataset.coopAction){case 'create':await connect('',{creating:true});break;case 'join':await join(shell()?.querySelector('#coop-code-input')?.value);break;case 'close':closeModal();break;case 'leave':await leave();break;case 'wave':if(world&&!busy&&Date.now()-lastEmote>=2000){pendingEmote='wave';lastEmote=Date.now();onEmote('wave');render();await tick();}break;case 'retry':error='';retryAt=0;if(world)await tick();else{try{await ensureSession();await resume();render(true);}catch(e){failure(e);}}break;
      case 'copy':{const input=shell()?.querySelector('#coop-invite');if(!input)break;try{await navigator.clipboard.writeText(input.value);notify('Odkaz je skopírovaný. Pošli ho kamarátovi.','success');}catch{input.focus();input.select();notify('Odkaz skopíruj klávesmi Ctrl+C.');}break;}
    }
  }
  function submit(event){if(event.target.id!=='coop-join-form'||disposed)return;event.preventDefault();if(!busy&&meId)void join(shell()?.querySelector('#coop-code-input')?.value);}
  function visibility(){if(document.hidden){controllers.forEach(c=>c.abort());status(false);}else{lastNetwork=-Infinity;void tick();}}
  const timer=setInterval(()=>void tick(),250);document.addEventListener('click',handleClick);document.addEventListener('submit',submit);document.addEventListener('visibilitychange',visibility);
  function dispose(){disposed=true;generation++;clearInterval(timer);controllers.forEach(c=>c.abort());controllers.clear();document.removeEventListener('click',handleClick);document.removeEventListener('submit',submit);document.removeEventListener('visibilitychange',visibility);}
  return {open,joinFromUrl:resume,updatePosition,tryCollect,teleport,leave,dispose,getWorld:()=>clone(world)};
}
