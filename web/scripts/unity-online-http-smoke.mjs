import assert from 'node:assert/strict';
import questions from '../worker/unity-curriculum.mjs';

const origin=new URL(process.argv[2]||'http://127.0.0.1:4174');
const ownerProduction='https://numerion.example.invalid';
if(!['127.0.0.1','localhost','[::1]'].includes(origin.hostname)&&!(origin.origin===ownerProduction&&process.argv.includes('--owner-production')))
  throw new Error('Use the loopback preview, or explicitly select --owner-production for the owned Numerion Site.');
origin.pathname='/';origin.search='';origin.hash='';
let checks=0,code='',alice,bob;
const expect=(actual,expected,label)=>{assert.deepEqual(actual,expected,label);checks++;};
const p=name=>({name,skin:2,outfit:5,hair:1,accessory:2,pet:'ember',petGear:0,grade:9,x:0,y:0,z:12,yaw:90,party:['ember','ratio','geora']});
async function call(path,data={},token='',status=200){
  const response=await fetch(new URL('/api/unity/'+path,origin),{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:JSON.stringify(data),signal:AbortSignal.timeout(10000)});
  expect(response.status,status,path+' HTTP');const reply=await response.json();expect(reply.ok,status<400,path+' result');return reply;
}
const save=name=>JSON.stringify({Profile:{DisplayName:name,Grade:9},Companions:{Owned:[{SpeciesId:'ember'},{SpeciesId:'ratio'},{SpeciesId:'geora'}]},Purchases:[]});
let heartbeatSequence=10;
async function keepAlive(){
  const sequence=heartbeatSequence++;
  await Promise.all([[alice,'HTTP Alica'],[bob,'HTTP Boris']].map(([person,name])=>call('rooms/pulse',{code,sequence,presence:p(name)},person.identityToken)));
}
try{
  alice=await call('profile/create',{saveJson:save('HTTP Alica')},'',201);
  bob=await call('profile/create',{saveJson:save('HTTP Boris')},'',201);
  await call('profile/save',{profileRevision:1,saveJson:save('HTTP Alica')},alice.identityToken);
  await call('profile/save',{profileRevision:1,saveJson:save('Stará verzia')},alice.identityToken,409);
  code=(await call('rooms/create',{presence:p('HTTP Alica')},alice.identityToken)).room.code;
  let room=(await call('rooms/join',{code,presence:p('HTTP Boris')},bob.identityToken)).room;
  expect(room.players.length,2,'two independent clients share room');
  expect(room.players.find(peer=>peer.id===alice.identityId).outfit,5,'appearance survives HTTP serialization');
  expect(JSON.stringify(room).includes(alice.identityToken),false,'room does not leak profile secret');
  await new Promise(resolve=>setTimeout(resolve,120));
  room=(await call('rooms/pulse',{code,sequence:1,presence:{...p('HTTP Alica'),x:1}},alice.identityToken)).room;
  expect(room.players.find(peer=>peer.id===alice.identityId).x,1,'plausible movement reaches peer state');
  room=(await call('rooms/respawn',{code,destination:'course'},alice.identityToken)).room;
  expect(room.players.find(peer=>peer.id===alice.identityId).courseCheckpoint,0,'approved course teleport');
  await call('rooms/respawn',{code,destination:'spawn'},alice.identityToken);
  await keepAlive();
  let battle=(await call('battle/challenge',{code,targetId:bob.identityId},alice.identityToken)).room.battle;
  expect(battle.status,'challenge','explicit invitation');
  battle=(await call('battle/accept',{code,battleId:battle.id},bob.identityToken)).room.battle;
  expect(battle.question.grade,9,'grade-nine question over HTTP');
  expect(Number.isInteger(battle.question.subject),true,'mathematics strand provided');
  const first=battle.round;
  await keepAlive();
  await call('battle/action',{code,battleId:battle.id,round:first,kind:5,targetSlot:0,optionIndex:-1,questionId:'',actionId:`${battle.id}:${first}:0`},alice.identityToken);
  battle=(await call('battle/action',{code,battleId:battle.id,round:first,kind:5,targetSlot:0,optionIndex:-1,questionId:'',actionId:`${battle.id}:${first}:1`},bob.identityToken)).room.battle;
  expect(battle.state.Teams[0].Pets[0].HP,92,'weak attack needs no question');
  expect(battle.artifactRewards.length,0,'weak attack grants no artifact');
  for(let safety=0;battle.status==='active'&&safety<80;safety++){
    await keepAlive();
    const definition=questions.find(q=>battle.question.id.startsWith(q.Id+':'));
    assert.ok(definition,'known authored mathematical question');
    const answer=battle.question.options.indexOf(definition.Options[definition.CorrectOption]),round=battle.round;
    const pet=battle.state.Teams[0].Pets[battle.state.Teams[0].Active],kind=pet.Energy<2||pet.StrikePP===0?1:0;
    await call('battle/action',{code,battleId:battle.id,round,kind,targetSlot:0,optionIndex:answer,questionId:battle.question.id,actionId:`${battle.id}:${round}:0`},alice.identityToken);
    battle=(await call('battle/action',{code,battleId:battle.id,round,kind:1,targetSlot:0,optionIndex:(answer+1)%4,questionId:battle.question.id,actionId:`${battle.id}:${round}:1`},bob.identityToken)).room.battle;
  }
  const won=(await call('rooms/pulse',{code,sequence:2,presence:p('HTTP Alica')},alice.identityToken)).room.battle;
  expect(won.status,'finished','full battle finishes');expect(won.state.Winner,0,'correct answers win');
  expect(won.rewardXP,60,'canonical XP award');assert.ok(won.rewardId&&won.artifactRewards.length);checks++;
  const repeated=(await call('rooms/pulse',{code,sequence:3,presence:p('HTTP Alica')},alice.identityToken)).room.battle;
  expect(repeated.rewardId,won.rewardId,'stable once-only reward receipt');
  expect(repeated.artifactRewards,won.artifactRewards,'snapshot replay cannot create artifact receipts');
  const classic=await fetch(new URL('/classic',origin));expect(classic.status,200,'OG route preserved');
  console.log(`UNITY_HTTP_SMOKE_PASS ${checks} checks · two independent HTTP identities, room movement, grade-nine pet duel, artifacts, profile conflict and OG route.`);
}finally{
  for(const player of [alice,bob])if(code&&player)try{await call('rooms/leave',{code},player.identityToken);}catch{}
}
