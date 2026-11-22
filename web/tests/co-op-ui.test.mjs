import assert from 'node:assert/strict';
import {createCoop} from '../public/co-op.js';

const original=new Map(['document','location','history','localStorage','navigator','fetch','setInterval','clearInterval'].map(k=>[k,globalThis[k]]));
const realNow=Date.now,listeners=new Map(),storage=new Map(),intervals=new Map(),calls=[];
let clock=1000,nextTimer=0,world=null,expired=false,rejectMove=false,holdMove=false,pendingMove=null,membershipMissing=false,rejectJoin=false,leaveFailures=0,equipped=null;
Date.now=()=>clock;
const node=(id,value='')=>({id,value,focus(){document.activeElement=this;},select(){}});
const root={present:false,html:'',inputs:new Map(),set innerHTML(html){this.html=html;this.inputs.clear();for(const m of html.matchAll(/<input\b[^>]*id="([^"]+)"[^>]*>/g)){const value=m[0].match(/value="([^"]*)"/)?.[1]||'';this.inputs.set('#'+m[1],node(m[1],value));}},get innerHTML(){return this.html;},querySelector(s){return this.inputs.get(s)||null;}};
globalThis.document={hidden:false,activeElement:null,querySelector(s){return root.present&&s.includes('.coop-shell')?root:null;},addEventListener(k,f){listeners.set(k,f);},removeEventListener(k,f){if(listeners.get(k)===f)listeners.delete(k);}};
globalThis.location={href:'https://academy.test/?world=AB23CD'};
globalThis.history={replaceState(_a,_b,url){location.href=url;}};
globalThis.localStorage={getItem:k=>storage.get(k)||null,setItem:(k,v)=>storage.set(k,v),removeItem:k=>storage.delete(k)};
Object.defineProperty(globalThis,'navigator',{configurable:true,value:{clipboard:{writeText:async()=>{}}}});
globalThis.setInterval=f=>{const id=++nextTimer;intervals.set(id,f);return id;};globalThis.clearInterval=id=>intervals.delete(id);
const me={id:'self',name:'Luna',shards:0};
const snapshot=()=>({world:{...structuredClone(world),serverNow:clock},meId:me.id,me:{...me},...(world?.goal.complete?{reward:{id:'world-AB23CD-self',shards:60}}:{})});
const response=(data,status=200)=>({ok:status<400,status,json:async()=>structuredClone(data)});
globalThis.fetch=async(url,options)=>{
  assert.equal(options.credentials,'same-origin');const path=url.replace('/api/multiplayer',''),body=options.body?JSON.parse(options.body):null;calls.push({path,body,signal:options.signal});
  if(path==='/session'){assert.ok([...body.name].length<=20);assert.ok(!body.name.includes('🐱'));return response({me});}
  if(path==='/worlds'||path.endsWith('/join')){if(rejectJoin){rejectJoin=false;return response({error:'movement_too_fast',position:{...world.players[0].position}},409);}membershipMissing=false;if(!world)world={code:'AB23CD',hostId:'self',players:[{id:'self',name:'Luna',pet:body.pet,position:{...body.position},moving:false,onGround:true},{id:'friend',name:'<img src=x>',pet:{name:'Pip'},position:{x:2,y:0,z:13,yaw:0},moving:true,onGround:true}],beacons:Array.from({length:6},(_,i)=>({id:'b'+i,x:i,y:0,z:13,collected:false})),goal:{collected:0,total:6,complete:false,reward:60}};else world.players[0].position={...body.position};return response(snapshot());}
  if(path.endsWith('/move')){
    if(holdMove&&!body.teleport)return new Promise((_resolve,reject)=>{pendingMove=options.signal;options.signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true});});
    if(membershipMissing)return response({error:'not_a_member'},403);
    if(rejectMove){rejectMove=false;return response({error:'movement_too_fast',message:'Server korriguje pohyb.',position:{x:1,y:0,z:13,yaw:0}},409);}
    world.players[0].position=body.teleport?{x:body.checkpoint??0,y:body.teleport==='spawn'?0:2,z:13,yaw:0}:{...body.position};if(body.pet)world.players[0].pet=body.pet;return response(snapshot());
  }
  if(path.endsWith('/collect')){world.beacons.forEach(b=>b.collected=true);world.goal={collected:6,total:6,complete:true,reward:60};me.shards=60;return response(snapshot());}
  if(path.endsWith('/leave'))return leaveFailures-->0?response({error:'server_error',message:'Dočasný výpadok.'},503):response({me});
  if(path==='/worlds/AB23CD'){return expired?response({error:'world_expired',message:'Svet vypršal.'},410):membershipMissing?response({error:'not_a_member'},403):response(snapshot());}
  throw new Error('Unexpected endpoint '+path);
};
const peers=[],statuses=[],goals=[],rewards=[],corrections=[],emotes=[];
const options={openModal(){root.present=true;},closeModal(){root.present=false;},getState(){return {player:{name:'🐱 Dlhé slovenské meno hráča'},pets:equipped?[equipped]:[],equippedPet:equipped?.id};},onPeers:(p,id)=>peers.push({p,id}),onStatus:s=>statuses.push(s),onGoal:(b,g)=>goals.push({b,g}),onReward:r=>rewards.push(r),onPosition:p=>corrections.push(p),onEmote:e=>emotes.push(e)};
const flush=()=>new Promise(resolve=>setTimeout(resolve,0));
async function tick(ms=250){clock+=ms;for(const f of intervals.values())f();await flush();}
const act=async action=>{const target={dataset:{coopAction:action},disabled:false,closest:s=>s==='[data-coop-action]'?target:root};await listeners.get('click')({target});};
let coop;
try{
  coop=createCoop(options);await coop.joinFromUrl();await coop.open();
  assert.ok(root.innerHTML.includes('&lt;img src=x&gt;'),'Peer text is escaped');
  assert.deepEqual(peers.at(-1).p.map(p=>p.id),['friend'],'The renderer receives only remote players');
  assert.equal(peers.at(-1).id,'self');assert.equal(coop.getWorld().code,'AB23CD');
  for(let i=0;i<100;i++)coop.updatePosition({x:3,y:0,z:13},.1,true,true);
  assert.equal(calls.filter(c=>c.path.endsWith('/move')).length,0,'Animation frames never write requests');
  await tick(249);assert.equal(calls.filter(c=>c.path.endsWith('/move')).length,0);
  await tick(1);assert.equal(calls.filter(c=>c.path.endsWith('/move')).length,1,'Moves begin after250ms');
  coop.updatePosition({x:4,y:0,z:13},.2,true,true);await tick(100);assert.equal(calls.filter(c=>c.path.endsWith('/move')).length,1);await tick(150);assert.equal(calls.filter(c=>c.path.endsWith('/move')).length,2);
  await act('close');assert.equal(root.present,false);const beforeCloseCalls=calls.length;await tick(750);assert.ok(calls.length>beforeCloseCalls,'Closing the modal preserves the shared world');
  document.hidden=true;listeners.get('visibilitychange')();const hiddenCalls=calls.length;await tick(2000);assert.equal(calls.length,hiddenCalls,'Hidden tabs stop networking');document.hidden=false;listeners.get('visibilitychange')();await flush();
  rejectMove=true;coop.updatePosition({x:40,y:0,z:13},0,true,true);await tick(250);assert.deepEqual(corrections.at(-1),{x:1,y:0,z:13,yaw:0},'Rejected moves expose only authoritative correction');
  const correctionCount=corrections.length;await tick(750);assert.equal(corrections.length,correctionCount,'Ordinary accepted snapshots do not reposition local controls');
  const relocated=await coop.teleport('course-checkpoint',3);assert.deepEqual(relocated,{x:3,y:2,z:13,yaw:0});assert.equal(calls.at(-1).body.checkpoint,3);
  assert.equal(await coop.teleport('course-checkpoint',7),null,'Invalid checkpoint indices never reach the server');
  holdMove=true;coop.updatePosition({x:4,y:2,z:13},0,true,true);await tick(250);const racingMove=pendingMove;assert.ok(racingMove);const safeTeleport=await coop.teleport('spawn');assert.equal(racingMove.aborted,true,'Teleport cancels ordinary movement before writing its canonical pose');assert.deepEqual(safeTeleport,{x:0,y:0,z:13,yaw:0});holdMove=false;await flush();
  await coop.open();await act('wave');assert.deepEqual(emotes,['wave']);await tick(250);assert.equal(calls.at(-1).body.emote,'wave','A wave is sent through the throttled movement channel');const waveRequests=calls.filter(c=>c.body?.emote).length;await act('wave');await tick(250);assert.equal(calls.filter(c=>c.body?.emote).length,waveRequests,'Rapid repeated waves cannot flood the API');
  const petPackets=calls.filter(c=>c.body?.pet&&c.path.endsWith('/move')).length;equipped={id:'fox-1',kind:'fox',color:'#efb4bc',name:'🦊 Moja dlhá líška vo svete kamarátov'};await tick(250);assert.equal(calls.at(-1).body.pet.kind,'fox','Changing companion schedules a move without rejoining');assert.ok([...calls.at(-1).body.pet.name].length<=20);assert.ok(!calls.at(-1).body.pet.name.includes('🦊'));assert.equal(calls.filter(c=>c.body?.pet&&c.path.endsWith('/move')).length,petPackets+1);await tick(2000);assert.equal(calls.at(-1).body.pet,undefined,'Unchanged companions are omitted from heartbeat packets');
  assert.equal(await coop.tryCollect('b0'),true);assert.equal(rewards.length,1);await tick(2000);assert.equal(rewards.length,1,'Repeated server rewards fire once');
  const copy=coop.getWorld();copy.players.length=0;assert.equal(coop.getWorld().players.length,2,'Snapshots are defensive copies');
  holdMove=true;coop.updatePosition({x:4,y:2,z:13},0,true,true);await tick(250);assert.ok(pendingMove);document.hidden=true;listeners.get('visibilitychange')();await flush();assert.equal(pendingMove.aborted,true);holdMove=false;document.hidden=false;listeners.get('visibilitychange')();await flush();
  expired=true;await tick(750);assert.equal(coop.getWorld(),null,'Expired worlds return to solo play');assert.equal(storage.has('lumen-coop-world-v1'),false);assert.equal(statuses.at(-1).connected,false);
  expired=false;location.href='https://academy.test/?world=AB23CD';await coop.joinFromUrl();await coop.leave();assert.equal(coop.getWorld(),null);assert.ok(!location.href.includes('world='),'Leaving removes the invite parameter');
  assert.equal(goals.at(-1).g,null,'Leaving clears the shared goal');
  coop.dispose();assert.equal(intervals.size,0);assert.equal(listeners.size,0);
  storage.set('lumen-coop-world-v1','AB23CD');location.href='https://academy.test/';world.players[0].position={x:23,y:4,z:-6,yaw:.7};const farPose={...world.players[0].position},beforeRestoreCorrections=corrections.length;coop=createCoop(options);await coop.joinFromUrl();assert.equal(coop.getWorld().code,'AB23CD','Reload restores the remembered world');assert.equal(corrections.length,beforeRestoreCorrections+1);assert.deepEqual(corrections.at(-1),farPose,'A remembered world restores the authoritative pose once');await tick(2000);assert.deepEqual(calls.at(-1).body.position,farPose,'Reload never sends the stale spawn pose');
  membershipMissing=true;const joinsBefore=calls.filter(c=>c.path.endsWith('/join')).length;await tick(750);assert.equal(calls.filter(c=>c.path.endsWith('/join')).length,joinsBefore+1,'A stale member rejoins the living world');assert.equal(coop.getWorld().code,'AB23CD');assert.equal(storage.get('lumen-coop-world-v1'),'AB23CD','Temporary lost membership retains the invitation');assert.equal(rewards.length,1,'Rejoining cannot celebrate the same reward twice');
  leaveFailures=1;assert.equal(await coop.leave(),false);assert.equal(coop.getWorld().code,'AB23CD');assert.equal(storage.get('lumen-coop-world-v1'),'AB23CD','Failed leave remembers membership until acknowledged');await tick(750);assert.equal(coop.getWorld(),null,'A failed leave is retried and cleared after acknowledgement');coop.dispose();
  storage.set('lumen-coop-world-v1','AB23CD');location.href='https://academy.test/';membershipMissing=true;coop=createCoop(options);await coop.joinFromUrl();assert.equal(coop.getWorld().code,'AB23CD','Stored GET resumes by joining if its membership expired');coop.dispose();
  location.href='https://academy.test/?world=AB23CD';world.players[0].position={x:-12,y:2,z:8,yaw:1};rejectJoin=true;const joinCanonical={...world.players[0].position},beforeJoinCorrections=corrections.length;coop=createCoop(options);await coop.joinFromUrl();assert.equal(coop.getWorld().code,'AB23CD');assert.equal(corrections.length,beforeJoinCorrections+1);assert.deepEqual(calls.at(-1).body.position,joinCanonical,'Explicit refresh retries join with the server canonical pose');assert.deepEqual(corrections.at(-1),joinCanonical);coop.dispose();
  console.log('Co-op UI passed: sanitized identities, safe markup, remote peers, request throttling, hidden/close lifecycle, teleport races, wave rate limit, canonical restore/rejoin, transient leave retries, expiry and reward dedupe.');
}finally{coop?.dispose();Date.now=realNow;for(const [key,value]of original){if(key==='navigator')Object.defineProperty(globalThis,key,{configurable:true,value});else globalThis[key]=value;}}
