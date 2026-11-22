import assert from 'node:assert/strict';
import {createMultiplayer} from '../public/multiplayer.js';

// Minimal DOM seam: exercise lifecycle and API interactions without a WebGL browser.
const listeners=new Map(),storage=new Map(),calls=[],rewards=[],notifications=[];
const globals=['document','location','history','localStorage','MutationObserver','fetch'];
const originals=new Map(globals.map(key=>[key,globalThis[key]]));
const classList=initial=>{const values=new Set(initial.split(/\s+/));return {add:c=>values.add(c),toggle(c,force){if(force===undefined)force=!values.has(c);if(force)values.add(c);else values.delete(c);},contains:c=>values.has(c)};};
const node=()=>({innerHTML:'',textContent:'',id:'',classList:classList(''),focus(){document.activeElement=this;},setAttribute(){},select(){},setSelectionRange(){}});
const root={present:false,writes:0,html:'',buttons:[],nodes:new Map(),classList:classList('mp-shell'),closest(){return {classList:classList('modal-card')};},
  set innerHTML(value){
    this.html=value;this.writes++;this.buttons=[];this.nodes=new Map();
    for(const match of value.matchAll(/<button\b([^>]*)>/g)){
      const attrs=Object.fromEntries([...match[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(m=>[m[1],m[2]]));
      const button={...node(),dataset:{},disabled:/\bdisabled\b/.test(match[1]),classList:classList(attrs.class||''),closest:selector=>selector==='button'?button:root};
      for(const [key,value] of Object.entries(attrs))if(key.startsWith('data-'))button.dataset[key.slice(5).replace(/-([a-z])/g,(_,c)=>c.toUpperCase())]=value;
      this.buttons.push(button);
    }
    for(const selector of ['.mp-profile','.mp-status','[data-mp-players]','.mp-feedback','.mp-timer','[data-mp-countdown]','.mp-question','h2'])this.nodes.set(selector,node());
    if(value.includes('id="mp-room-code"'))this.nodes.set('#mp-room-code',{...node(),id:'mp-room-code',value:'',selectionStart:0});
    if(value.includes('id="mp-invite-link"'))this.nodes.set('#mp-invite-link',{...node(),id:'mp-invite-link',value:(value.match(/id="mp-invite-link"[^>]*value="([^"]*)"/)||[])[1]||''});
  },get innerHTML(){return this.html;},
  querySelector(selector){if(this.nodes.has(selector))return this.nodes.get(selector);return this.querySelectorAll(selector)[0]||null;},
  querySelectorAll(selector){return this.buttons.filter(b=>selector.split(',').some(part=>{const match=part.match(/\[data-mp-([\w-]+)(?:="([^"]*)")?\]/);if(!match)return false;const key='mp'+match[1].replace(/(?:^|-)([a-z])/g,(_,c)=>c.toUpperCase());return b.dataset[key]!==undefined&&(match[2]===undefined||b.dataset[key]===match[2]);}));},
};
globalThis.document={hidden:false,activeElement:null,body:{},getElementById(){return {};},querySelector(selector){return root.present&&selector.includes('.mp-shell')?root:null;},addEventListener(type,listener){listeners.set(type,listener);},removeEventListener(type,listener){if(listeners.get(type)===listener)listeners.delete(type);}};
globalThis.location={href:'https://academy.example/play?world=academy'};
globalThis.history={replaceState(_state,_title,url){location.href=url;}};
globalThis.localStorage={getItem:key=>storage.get(key)||null,setItem:(key,value)=>storage.set(key,value),removeItem:key=>storage.delete(key)};
globalThis.MutationObserver=class {observe(){}disconnect(){}};
let me={id:'self',name:'<img src=x onerror=alert(1)>',rank:1,rating:1000,wins:0,matches:0,shards:120,gear:{power:0,shield:0,focus:0}};
let room=null,sessionFails=true,answerRelease=null,holdPoll=false,pollSignal=null,roomErrorStatus=0,leaveErrorStatus=0;
const gearDefs=()=>['power','shield','focus'].map(slot=>({slot,name:slot,description:'Učenie posilní spoločníka.',level:me.gear[slot],maxLevel:3,cost:80*2**me.gear[slot]}));
const response=(data,status=200)=>({ok:status<400,status,json:async()=>structuredClone(data)});
const snapshot=()=>({...room,serverNow:Date.now()+55000});
globalThis.fetch=async(url,options)=>{
  assert.equal(options.credentials,'same-origin','Identity is sent only through the same-origin cookie');
  const path=url.replace('/api/multiplayer',''),body=options.body?JSON.parse(options.body):null;
  calls.push({path,body,signal:options.signal});
  if(path==='/session')return sessionFails?response({error:'unavailable',message:'Obnova <script>alert(1)</script>'},503):response({me});
  if(path==='/me')return response({me,gearDefs:gearDefs(),activeRoomCode:room?.code||null});
  if(path==='/rooms'){
    room={code:'TEST12',mode:body.mode,status:'lobby',hostId:'self',players:[{id:'self',name:me.name,pet:body.pet,score:0,hp:120,answered:false,connected:true},{id:'friend',name:'<svg onload=alert(1)>',pet:{color:'red" onload="alert(1)',kind:'unknown',name:'Mia'},score:0,hp:120,answered:false,connected:true}],round:0,totalRounds:6,question:null,deadline:0,reveal:null,winnerId:null};
    return response({room:snapshot(),me,gearDefs:gearDefs()});
  }
  if(path.endsWith('/join')){
    room={...room,code:path.split('/')[2]};return response({room:snapshot(),me});
  }
  if(path.endsWith('/start')){
    room={...room,status:'question',round:1,question:{id:'q1',subject:'math',prompt:'Koľko je <script>2 + 3?</script>',options:[{id:'a',text:'5 <img onerror=x>'},{id:'b',text:'6'},{id:'c',text:'7'}]},deadline:Date.now()+75000};
    return response({room:snapshot(),me});
  }
  if(path.endsWith('/answer'))return new Promise((resolve,reject)=>{
    answerRelease=()=>{room.players[0].answered=true;resolve(response({room:snapshot(),me}));};
    options.signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true});
  });
  if(path.endsWith('/leave')){const status=leaveErrorStatus;if([404,410].includes(status))room=null;return status?response({message:'Miestnosť už bola uzavretá.'},status):response({me});}
  if(path==='/gear'){me={...me,shards:me.shards-80,gear:{...me.gear,[body.slot]:me.gear[body.slot]+1}};return response({me,gearDefs:gearDefs()});}
  if(path==='/leaderboard')return response({players:[{id:'friend',name:'<script>malice</script>',rank:'Vedec',rating:1200,wins:2,matches:3}],me});
  if(path.match(/^\/rooms\/[A-Z0-9]+$/)){
    if(roomErrorStatus){const status=roomErrorStatus;if([404,410].includes(status))room=null;return response({message:status===503?'Server sa znovu pripája.':'Miestnosť už bola uzavretá.'},status);}
    if(holdPoll)return new Promise((_resolve,reject)=>{pollSignal=options.signal;options.signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true});});
    return response({room:snapshot(),me,gearDefs:gearDefs()});
  }
  throw new Error(`Unexpected API call ${path}`);
};
const options={openModal(){root.present=true;},closeModal(){root.present=false;},notify(message){notifications.push(message);},onReward(reward){rewards.push(reward);},getState(){return {player:{name:'  Ｌｕｎａ 🤖 abcdefghijklmnopqrstuvwxyz  '},equippedPet:'owned',pets:[{id:'owned',name:'  Ｐｉｐ ⭐ abcdefghijklmnopqrstuvwxyz  ',kind:'bunny',color:'#ffc86e'}]};},petArt(pet){return `<svg fill="${pet.color}"></svg>`;},icon(){return '<svg></svg>';}};
const click=button=>listeners.get('click')({target:button});
const action=(name,extra={})=>({...node(),dataset:{mpAction:name,...extra},disabled:false,closest:selector=>selector==='button'?actionButton:root});
let actionButton;
const act=(name,extra={})=>{actionButton=action(name,extra);return click(actionButton);};
const tick=()=>new Promise(resolve=>setTimeout(resolve,0));
let arena;
try{
  arena=createMultiplayer(options);
  await arena.open();
  assert.ok(root.querySelector('.mp-status').innerHTML.includes('&lt;script&gt;'),'Service errors are escaped and reviewable');
  assert.equal(root.querySelector('[data-mp-action="create"]').disabled,true,'Room controls stay disabled until identity is ready');
  sessionFails=false;await act('retry');
  assert.equal(calls.find(call=>call.path==='/session').body.name,'Luna abcdefghijklmno','Offline names are normalized, stripped and limited by Unicode code point for the online API');
  assert.equal(root.querySelector('[data-mp-action="create"]').disabled,false,'Retry recovers a failed session');
  assert.ok(root.innerHTML.includes('Poradie #1'),'Numeric leaderboard rank has a visible position label');
  await act('create',{mpMode:'duel'});
  assert.equal(calls.find(call=>call.path==='/rooms').body.pet.color,'#ffc86e','The equipped companion is used');
  assert.equal(calls.find(call=>call.path==='/rooms').body.pet.name,'Pip abcdefghijklmnop','Companion names follow the same 20-code-point server contract');
  assert.ok(root.innerHTML.includes('&lt;svg onload=alert(1)&gt;'),'Peer names cannot inject HTML');
  assert.ok(root.innerHTML.includes('fill="#91d3ee"'),'Remote companion colors are constrained before rendering');
  assert.deepEqual([...storage], [['lumen-multiplayer-room-v1','TEST12']],'Only a room code is stored locally, never an identity token');
  await act('start');
  assert.ok(root.innerHTML.includes('&lt;script&gt;2 + 3?&lt;/script&gt;'),'Remote question text is escaped');
  const questionWrites=root.writes,answerButtons=root.querySelectorAll('[data-mp-answer]');
  document.activeElement=answerButtons[1];
  await act('retry');
  assert.equal(root.writes,questionWrites,'Same-round polling does not replace the answer DOM');
  assert.equal(root.querySelectorAll('[data-mp-answer]')[1],answerButtons[1],'Answer focus retains its node between polls');
  assert.equal(document.activeElement,answerButtons[1]);
  assert.ok(Number(root.querySelector('[data-mp-countdown]').textContent)<=20&&Number(root.querySelector('[data-mp-countdown]').textContent)>=19,'Countdown corrects the server/client clock offset');
  const guard=root.querySelectorAll('[data-mp-move]').find(b=>b.dataset.mpMove==='guard');await click(guard);
  const pendingAnswer=click(answerButtons[0]);
  await click(answerButtons[0]);
  assert.equal(calls.filter(call=>call.path.endsWith('/answer')).length,1,'A fast double click submits just one authoritative answer');
  assert.deepEqual(calls.find(call=>call.path.endsWith('/answer')).body,{round:1,answerId:'a',move:'guard'});
  assert.ok(answerButtons.every(b=>b.disabled),'Pending answers cannot be changed');
  answerRelease();await pendingAnswer;
  assert.ok(answerButtons.every(b=>b.disabled),'Confirmed answers stay locked for this round');
  assert.equal(root.writes,questionWrites,'Submitting an answer also preserves its DOM');
  room={...room,status:'reveal',reveal:{correctAnswerId:'a',explanation:'Dve plus tri je <strong>päť</strong>.',results:[{playerId:'self',correct:true,points:100,damage:0,move:'guard'}]}};
  await act('retry');
  assert.ok(root.innerHTML.includes('&lt;strong&gt;päť&lt;/strong&gt;'),'Reveal explanations are escaped');
  assert.ok(root.querySelectorAll('[data-mp-answer]')[0].classList.contains('correct'),'The server chooses the highlighted correct answer');
  room={...room,status:'finished',winnerId:'friend',rewards:[{playerId:'self',shards:30,ratingDelta:15}],players:room.players.map(p=>({...p,score:p.id==='self'?100:60,hp:p.id==='self'?80:110}))};
  await act('retry');
  assert.equal(rewards.length,1,'Only a verified positive server reward triggers the celebration callback');
  const duelResults=root.innerHTML.match(/<ol class="mp-results-list">([\s\S]*?)<\/ol>/)[1];
  assert.ok(duelResults.indexOf('&lt;svg onload=alert(1)&gt;')<duelResults.indexOf('&lt;img src=x onerror=alert(1)&gt;'),'The healthier defensive duel winner appears before the higher-scoring loser');
  await act('view-gear');
  assert.ok(root.innerHTML.includes('80 úlomkov'),'Gear prices come from the server response');
  await act('gear',{mpSlot:'power'});
  assert.ok(root.innerHTML.includes('160 úlomkov'),'The next gear level shows its new server price');
  assert.equal(root.querySelectorAll('[data-mp-action="gear"]').find(b=>b.dataset.mpSlot==='power').disabled,true,'An unaffordable upgrade is disabled');
  await act('leaderboard');
  arena.close();assert.equal(root.present,false,'Closing returns control to the world');
  await arena.open();
  assert.ok(calls.filter(call=>call.path==='/rooms/TEST12').length>=3,'A reopened arena restores the authenticated room');
  arena.close();room={...room,code:'AFK001',winnerId:null,rewards:[{playerId:'self',shards:0,ratingDelta:0}]};
  await arena.open();
  assert.ok(root.innerHTML.includes('Za tento zápas nepribudli úlomky'),'A zero-reward result explains the participation requirement honestly');
  assert.equal(rewards.length,1,'An AFK result does not trigger reward confetti');
  assert.ok(notifications.includes('Zápas dokončený. Tentoraz bez odmeny.'));
  holdPoll=true;
  const pendingPoll=act('retry');await tick();
  document.hidden=true;listeners.get('visibilitychange')();await pendingPoll;
  assert.equal(pollSignal.aborted,true,'Hidden tabs cancel pending polling');
  const hiddenCalls=calls.length;await tick();assert.equal(calls.length,hiddenCalls,'Hidden tabs do not start new polling');
  arena.dispose();assert.equal(listeners.size,0,'Disposal removes arena event listeners');
  holdPoll=false;document.hidden=false;room.status='lobby';
  location.href='https://academy.example/?room=JOIN99';
  arena=createMultiplayer({...options,getState:()=>({player:{name:'🤖!!!'},pets:[{id:'unequipped',name:'Old pet',color:'#ffc86e',kind:'cat'}]})});await arena.joinFromUrl();
  assert.ok(calls.some(call=>call.path==='/rooms/JOIN99/join'),'Shared URLs join the real server room');
  assert.equal(calls.filter(call=>call.path==='/session').at(-1).body.name,'Objaviteľ','A profile with no permitted online characters uses a valid fallback');
  assert.deepEqual(calls.find(call=>call.path==='/rooms/JOIN99/join').body.pet,{kind:'robot',color:'#91d3ee',name:'Akademik'},'Without an equipped companion the neutral academy pet can join');
  roomErrorStatus=404;await act('retry');
  assert.ok(root.innerHTML.includes('mp-mode-grid'),'An expired room returns the player to room creation');
  assert.equal(storage.has('lumen-multiplayer-room-v1'),false,'An expired room is no longer remembered');
  assert.ok(root.innerHTML.includes('Miestnosť už bola uzavretá.'),'Expiry retains a readable reason');
  assert.equal(new URL(location.href).searchParams.has('room'),false,'An expired invite is removed from the current URL');
  roomErrorStatus=0;await act('create',{mpMode:'quiz'});
  roomErrorStatus=410;await act('retry');
  assert.ok(root.innerHTML.includes('mp-mode-grid'),'A gone room also returns to a usable home');
  roomErrorStatus=0;await act('create',{mpMode:'quiz'});
  leaveErrorStatus=404;await act('leave');
  assert.ok(root.innerHTML.includes('mp-mode-grid'),'Leaving an already-deleted room is handled gracefully');
  assert.equal(storage.has('lumen-multiplayer-room-v1'),false);
  leaveErrorStatus=0;await act('create',{mpMode:'quiz'});
  roomErrorStatus=503;await act('retry');
  assert.equal(storage.get('lumen-multiplayer-room-v1'),'TEST12','A transient service failure retains the room for reconnect');
  assert.ok(root.querySelector('#mp-invite-link'),'Transient failures preserve the current lobby');
  roomErrorStatus=0;await act('retry');
  room={...room,status:'finished',winnerId:'self',players:room.players.map(p=>({...p,score:p.id==='self'?100:60,hp:p.id==='self'?1:120}))};
  await act('retry');
  const quizResults=root.innerHTML.match(/<ol class="mp-results-list">([\s\S]*?)<\/ol>/)[1];
  assert.ok(quizResults.indexOf('&lt;img src=x onerror=alert(1)&gt;')<quizResults.indexOf('&lt;svg onload=alert(1)&gt;'),'Quiz results remain ordered by score, independently of HP');
  arena.dispose();
  location.href='https://academy.example/';
  arena=createMultiplayer({...options,getState:()=>({player:{name:'\u{10400}'.repeat(21)},pets:[]})});await arena.open();
  const astralName=calls.filter(call=>call.path==='/session').at(-1).body.name;
  assert.equal([...astralName].length,20,'Non-BMP letters count as one Unicode code point each');
  assert.equal(astralName.length,40,'Name trimming never cuts a surrogate pair');
  arena.dispose();
  console.log('Multiplayer UI: escaped content, valid Unicode names, rank/rewards, expiry recovery, quiz/duel standings, stable single answers, cookie reconnect, server gear and polling cleanup passed.');
}finally{
  arena?.dispose();
  for(const [key,value] of originals){if(value===undefined)delete globalThis[key];else globalThis[key]=value;}
}
