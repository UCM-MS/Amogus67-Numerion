import assert from 'node:assert/strict';
import {createTestD1} from './d1-adapter.mjs';
import {createUnityOnlineApi} from '../worker/unity-online.mjs';
import questions from '../worker/unity-curriculum.mjs';

let time=1793620800000,checks=0;
const db=createTestD1(),api=createUnityOnlineApi({questions,now:()=>time});
const check=(value,message)=>{assert.ok(value,message);checks++;};
const presence=name=>({name,party:['ember'],pet:'ember',grade:9,x:0,y:0,z:12,yaw:0});
async function call(path,data,token){
  const response=await api(new Request('http://127.0.0.1:4174/api/unity/'+path,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:JSON.stringify(data)}),{DB:db});
  check(response.status<300,path+' succeeded');return response.json();
}
try{
  const alice=await call('profile/create',{saveJson:JSON.stringify({Profile:{DisplayName:'Alica',Grade:9}})}),bob=await call('profile/create',{saveJson:JSON.stringify({Profile:{DisplayName:'Boris',Grade:9}})});
  const code=(await call('rooms/create',{presence:presence('Alica')},alice.identityToken)).room.code;
  await call('rooms/join',{code,presence:presence('Boris')},bob.identityToken);
  let duel=(await call('battle/challenge',{code,targetId:bob.identityId},alice.identityToken)).room.battle;
  duel=(await call('battle/accept',{code,battleId:duel.id},bob.identityToken)).room.battle;
  let sequence=0;
  for(const committedSide of [1,0,-1]){
    const round=duel.round;
    if(committedSide>=0)await call('battle/action',{code,battleId:duel.id,round,kind:5,targetSlot:0,optionIndex:-1,questionId:'',actionId:`${duel.id}:${round}:${committedSide}`},committedSide?bob.identityToken:alice.identityToken);
    for(let step=0;step<6;step++){
      time+=10000;sequence++;
      duel=(await call('rooms/pulse',{code,sequence,presence:presence('Alica')},alice.identityToken)).room.battle;
      await call('rooms/pulse',{code,sequence,presence:presence('Boris')},bob.identityToken);
    }
    check(duel.round===round+1,'deadline resolves exactly one round when pending side is '+committedSide);
    check(duel.submitted.every(value=>!value),'expired round cannot precommit an action in the next round');
    check(duel.state.PendingActions.every(value=>value===null),'new round has no residual commands');
    check(duel.state.CorrectAnswers.every(value=>value===0),'timeouts and quick attacks cannot gain math credit');
    check(duel.artifactRewards.length===0,'timeouts do not create learning artifacts');
    check(duel.question.grade===9&&Number.isInteger(duel.question.subject),'next question retains authoritative grade and strand');
  }
  console.log(`UNITY_TIMEOUT_PASS ${checks} checks · either pending side and both absent answers resolve only the expired round.`);
}finally{db.close();}
