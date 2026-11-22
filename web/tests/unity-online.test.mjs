import assert from 'node:assert/strict';
import {createTestD1} from './d1-adapter.mjs';
import {createUnityOnlineApi} from '../worker/unity-online.mjs';
import {createBattle,submit,availability,actionId,effectiveness} from '../worker/unity-battle.mjs';
import questions from '../worker/unity-curriculum.mjs';

let assertions=0,time=1793620800000;const db=createTestD1(),api=createUnityOnlineApi({questions,now:()=>time});
const check=(value,message)=>{assert.ok(value,message);assertions++;};
const equal=(actual,expected,message)=>{assert.deepEqual(actual,expected,message);assertions++;};
const save=name=>JSON.stringify({Profile:{DisplayName:name,Grade:7,Progress:{XP:0,Coins:0}},Companions:{Owned:[{SpeciesId:'ember'}],EquippedId:'ember'},Purchases:[]});
const presence=(name='Hráč',pet='ember')=>({name,pet,party:[pet],petGear:0,skin:2,outfit:4,hair:1,accessory:0,grade:7,x:0,y:0,z:12,yaw:180});
async function request(path,data={},token='',expected=200,headers={}){const response=await api(new Request('https://lumen.test/api/unity/'+path,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{}),...headers},body:JSON.stringify(data)}),{DB:db});equal(response.status,expected,path+' status');const result=await response.json();equal(result.ok,expected<400,path+' ok');return result;}
async function identity(name){return await request('profile/create',{saveJson:save(name)},'',201);}
async function pulse(person,code,sequence,p=presence(person.identityId.slice(0,4))){return await request('rooms/pulse',{code,sequence,presence:p},person.identityToken);}
async function battleState(id){return JSON.parse((await db.prepare('SELECT state_json FROM unity_battles WHERE id=?').bind(id).first()).state_json);}

try{
  const alice=await identity('Alica'),bob=await identity('Boris'),stranger=await identity('Niekto');
  check(alice.identityToken.length===101,'pairing secret has UUID + 256 random bits');
  await request('profile/load',{},'',401);await request('profile/load',{},alice.identityToken.replace(/.$/,'Z'),401);
  await request('profile/load',{},alice.identityToken,403,{Origin:'https://evil.test'});
  const paired=await request('profile/load',{},alice.identityToken);equal(paired.identityId,alice.identityId,'another device can load same identity with private pairing code');
  await request('profile/save',{profileRevision:1,saveJson:save('Nová Alica')},alice.identityToken);
  await request('profile/save',{profileRevision:1,saveJson:save('Stará Alica')},alice.identityToken,409);
  equal(JSON.parse((await request('profile/load',{},alice.identityToken)).saveJson).Profile.DisplayName,'Nová Alica','stale device cannot overwrite newer progress');
  await request('profile/save',{profileRevision:2,saveJson:'{"Profile":{},"constructor":{}}'},alice.identityToken,400);

  const made=await request('rooms/create',{presence:presence('Alica')},alice.identityToken),code=made.room.code;
  check(/^[A-Z2-9]{6}$/.test(code),'join code valid');
  const joined=await request('rooms/join',{code,presence:presence('Boris','astra')},bob.identityToken);
  equal(joined.room.players.length,2,'PC/browser clients share room');
  check(!JSON.stringify(joined.room).includes('identityToken')&&!JSON.stringify(joined.room).includes(alice.identityToken),'roster never exposes pairing secret');
  await request('rooms/pulse',{code,sequence:1,presence:presence()},stranger.identityToken,404);
  time+=200;let moved=await pulse(alice,code,1,{...presence('Alica'),x:1});
  equal(moved.room.players.find(p=>p.id===alice.identityId).x,1,'plausible movement replicated');
  time+=200;moved=await pulse(alice,code,2,{...presence('Alica'),x:70});
  equal(moved.room.players.find(p=>p.id===alice.identityId).x,1,'teleport spoof corrected');
  time+=200;await request('rooms/pulse',{code,sequence:3,presence:{...presence(),x:null}},alice.identityToken,400);
  await request('rooms/beacon',{code,beaconIndex:0},alice.identityToken,409);
  let respawn=await request('rooms/respawn',{code,destination:'course'},alice.identityToken);
  equal(respawn.room.players.find(p=>p.id===alice.identityId).courseCheckpoint,0,'server permits defined course start');
  await request('rooms/respawn',{code,destination:'checkpoint',checkpoint:8},alice.identityToken,400);
  respawn=await request('rooms/respawn',{code,destination:'spawn'},alice.identityToken);
  equal(respawn.room.players.find(p=>p.id===alice.identityId).z,12,'server spawn reset');

  let room=(await request('battle/challenge',{code,targetId:bob.identityId},alice.identityToken)).room;
  let duel=room.battle;
  equal(duel.status,'challenge','challenge waits for opponent');
  await request('battle/accept',{code,battleId:duel.id},alice.identityToken,403);
  duel=(await request('battle/accept',{code,battleId:duel.id},bob.identityToken)).room.battle;
  equal(duel.status,'active','challenged player accepts');
  check(duel.question.options.length===4&&!Object.hasOwn(duel.question,'correctOption'),'answer concealed');
  await request('battle/challenge',{code,targetId:bob.identityId},alice.identityToken,409);
  const initial=await battleState(duel.id);let round=duel.round;
  await request('battle/action',{code,battleId:duel.id,round,kind:0,targetSlot:0,optionIndex:0,questionId:'fake',actionId:`${duel.id}:${round}:0`},alice.identityToken,400);
  let action=(await request('battle/action',{code,battleId:duel.id,round,kind:0,targetSlot:0,optionIndex:initial.answer,questionId:duel.question.id,actionId:`${duel.id}:${round}:0`},alice.identityToken)).room.battle;
  check(action.submitted[0]&&!action.submitted[1],'one committed action');
  check(action.state.PendingActions.every(x=>x===null),'opponent cannot counter-pick hidden committed move');
  equal(action.artifactRewards.length,1,'correct mathematical answer creates one artifact receipt');
  await request('battle/action',{code,battleId:duel.id,round,kind:0,targetSlot:0,optionIndex:initial.answer,questionId:duel.question.id,actionId:`${duel.id}:${round}:0`},alice.identityToken,409);
  action=(await request('battle/action',{code,battleId:duel.id,round,kind:0,targetSlot:0,optionIndex:(initial.answer+1)%4,questionId:duel.question.id,actionId:`${duel.id}:${round}:1`},bob.identityToken)).room.battle;
  equal(action.round,round+1,'round resolves only after both commit');
  check(action.explanation.length>0&&action.correctOption===initial.answer,'completed round reveals worked solution');
  while(action.status==='active'){
    time+=200;const internal=await battleState(action.id),state=action.state;round=action.round;
    const pet=state.Teams[0].Pets[state.Teams[0].Active],kind=pet.Energy<2?1:0;
    await request('battle/action',{code,battleId:action.id,round,kind,targetSlot:0,optionIndex:internal.answer,questionId:action.question.id,actionId:`${action.id}:${round}:0`},alice.identityToken);
    action=(await request('battle/action',{code,battleId:action.id,round,kind:0,targetSlot:0,optionIndex:(internal.answer+1)%4,questionId:action.question.id,actionId:`${action.id}:${round}:1`},bob.identityToken)).room.battle;
  }
  const win=(await pulse(alice,code,4)).room.battle;
  equal(win.state.Winner,0,'math attacks win');equal(win.rewardXP,60,'winner gets canonical XP');equal(win.rewardCoins,100,'winner gets canonical coins');
  check(win.rewardId.startsWith('online-duel:'),'stable reward key enables persistent replay guard');
  const again=(await pulse(alice,code,5)).room.battle;equal(again.rewardId,win.rewardId,'repeated snapshot same reward id');
  equal((await db.prepare('SELECT COUNT(*) AS count FROM unity_battle_rewards WHERE battle_id=?').bind(win.id).first()).count,1,'server ledger never duplicates victory reward');

  time+=200;duel=(await request('battle/challenge',{code,targetId:bob.identityId},alice.identityToken)).room.battle;
  duel=(await request('battle/accept',{code,battleId:duel.id},bob.identityToken)).room.battle;
  await request('battle/action',{code,battleId:duel.id,round:1,kind:5,targetSlot:0,optionIndex:-1,questionId:'',actionId:`${duel.id}:1:0`},alice.identityToken);
  const quick=(await request('battle/action',{code,battleId:duel.id,round:1,kind:5,targetSlot:0,optionIndex:-1,questionId:'',actionId:`${duel.id}:1:1`},bob.identityToken)).room.battle;
  equal(quick.state.Teams[0].Pets[0].HP,92,'quick attack works without mathematics');equal(quick.artifactRewards.length,0,'quick attack grants no mathematical artifacts');
  await request('battle/forfeit',{code,battleId:duel.id},bob.identityToken);
  const noMath=(await pulse(alice,code,6)).room.battle;equal(noMath.rewardXP,0,'zero correct answers cannot farm forfeit rewards');

  // Parallel occupancy is decided by a single INSERT SELECT, not a racy read then insert.
  const extra=[];for(let i=0;i<15;i++)extra.push(await identity('Spolužiak'+i));
  const replies=await Promise.all(extra.map(person=>api(new Request('https://lumen.test/api/unity/rooms/join',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+person.identityToken},body:JSON.stringify({code,presence:presence()})}),{DB:db})));
  equal(replies.filter(r=>r.status===200).length,14,'only remaining room slots join concurrently');equal(replies.filter(r=>r.status===409).length,1,'seventeenth player rejected');
  time+=16000;await request('rooms/pulse',{code,sequence:7,presence:presence()},alice.identityToken,410);
  const rejoin=await request('rooms/join',{code,presence:presence('Alica')},alice.identityToken);equal(rejoin.room.players.length,1,'expired members are absent and identity can rejoin');

  // Integer combat vectors independent of API serialization.
  equal(effectiveness('ember','astra'),120,'type advantage');equal(effectiveness('astra','ember'),80,'type disadvantage');equal(effectiveness('zlomko','datax'),100,'same affinity neutral');
  let state=createBattle('vector',['bolt'],['ember'],1);
  submit(state,0,1,{Kind:0,TargetSlot:0,Correct:true,Topic:2,ActionId:actionId(state,0)});
  submit(state,1,1,{Kind:1,TargetSlot:0,Correct:true,ActionId:actionId(state,1)});
  equal(state.Teams[1].Pets[0].HP,77,'type floor31 then topic37 then shield14 =>23');
  equal(state.CorrectAnswers,[1,1],'correct answers recorded only on activated math moves');
  state=createBattle('heal',['ember'],['astra'],1);state.Teams[0].Pets[0].HP=40;
  submit(state,0,1,{Kind:3,TargetSlot:0,Correct:true,ActionId:actionId(state,0)});submit(state,1,1,{Kind:5,TargetSlot:0,Correct:false,ActionId:actionId(state,1)});
  equal(state.Teams[0].Pets[0].HP,62,'heal resolves before quickstrike');equal(state.Teams[0].Pets[0].HealCooldown,2,'heal has two full blocked rounds');
  check(availability(state,0,3).length>0,'cooldown prevents immediate repeat');
  console.log(`UNITY_ONLINE_PASS ${assertions} assertions · shared profiles, revision conflicts, secure room movement, 16-player cap, math/quick duels, artifact receipts, once-only rewards, disconnects.`);
}finally{db.close();}
