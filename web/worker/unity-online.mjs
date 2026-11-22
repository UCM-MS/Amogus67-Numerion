import {SPECIES,cleanParty,createBattle,actionId,availability,submit,forfeit} from './unity-battle.mjs';

const BASE='/api/unity',PRESENCE_MS=15000,ROOM_TTL=30*60*1000,CHALLENGE_MS=40000,TURN_MS=60000,MAX_BODY=196608;
const encoder=new TextEncoder(),ALPHABET='ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const BEACONS=[[0,0,40],[42,0,16],[42,0,-32],[0,0,-56],[-42,0,-32],[-42,0,16]];
const COURSE=Array.from({length:9},(_,i)=>[Math.floor(i/3)===1?25.4-(i%3)*2.7:20+(i%3)*2.7,.4+i*.4,22+Math.floor(i/3)*3.3]);
const MATH_TOPICS=['Čísla a zlomky','Percentá a pomer','Výrazy a rovnice','Geometria','Dáta a pravdepodobnosť','Praktická matematika'];
class ApiError extends Error{constructor(status,error,message){super(message);this.status=status;this.error=error;}}
const fail=(status,error,message)=>{throw new ApiError(status,error,message);};
const changed=r=>Number(r?.meta?.changes||0);
const object=v=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const json=(value,status=200)=>new Response(JSON.stringify({ok:status<400,...value}),{status,headers:{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store, private','X-Content-Type-Options':'nosniff'}});
const random=n=>crypto.getRandomValues(new Uint8Array(n));
const hex=b=>[...b].map(x=>x.toString(16).padStart(2,'0')).join('');
const hash=async value=>hex(new Uint8Array(await crypto.subtle.digest('SHA-256',encoder.encode(value))));
const roomCode=()=>[...random(6)].map(x=>ALPHABET[x%ALPHABET.length]).join('');
const integer=(v,min,max,fallback=min)=>Number.isInteger(v)?Math.min(max,Math.max(min,v)):fallback;
function name(v){if(typeof v!=='string')return 'Objaviteľ';const result=v.normalize('NFKC').trim().replace(/[^\p{L}\p{M}\p{N} ._-]/gu,'').replace(/\s+/gu,' ');return [...result].slice(0,20).join('')||'Objaviteľ';}
function cleanPresence(raw,old=null,elapsed=.2){
  if(!object(raw))fail(400,'invalid_presence','Chýbajú údaje postavy.');
  if(!['x','y','z','yaw'].every(k=>typeof raw[k]==='number'&&Number.isFinite(raw[k])))fail(400,'invalid_position','Poloha musí obsahovať konečné čísla.');
  if(Math.abs(raw.x)>84||Math.abs(raw.z)>84||raw.y< -12||raw.y>40)fail(400,'outside_world','Poloha je mimo akadémie.');
  const party=cleanParty(raw.party),pet=party.includes(raw.pet)?raw.pet:'';
  const next={name:name(raw.name),skin:integer(raw.skin,0,7),outfit:integer(raw.outfit,0,11),hair:integer(raw.hair,0,3),accessory:integer(raw.accessory,0,3),
    pet,petGear:integer(raw.petGear,0,2),grade:integer(raw.grade,7,9,7),party,x:raw.x,y:raw.y,z:raw.z,yaw:((raw.yaw%360)+360)%360,courseCheckpoint:old?.courseCheckpoint??-1};
  // Teleports are not accepted as movement. Both clients keep a predicted local controller;
  // the returned owner snapshot is the authoritative correction when a packet is implausible.
  const prior=old||{x:0,y:0,z:12};const dt=Math.min(2,Math.max(.05,elapsed));
  if(Math.hypot(next.x-prior.x,next.z-prior.z)>9*dt+2.5||Math.abs(next.y-prior.y)>20*dt+2.5){next.x=prior.x;next.y=prior.y;next.z=prior.z;}
  if(next.courseCheckpoint>=0&&next.courseCheckpoint<8){const cp=COURSE[next.courseCheckpoint+1];if(Math.hypot(next.x-cp[0],next.y-cp[1],next.z-cp[2])<1.7)next.courseCheckpoint++;}
  return next;
}
function saveJson(raw){
  if(typeof raw!=='string'||encoder.encode(raw).length>128*1024)fail(400,'invalid_save','Profil je príliš veľký alebo chýba.');
  let save;try{save=JSON.parse(raw);}catch{fail(400,'invalid_save','Profil nemá platný formát.');}
  if(!object(save)||!object(save.Profile))fail(400,'invalid_save','Chýba herný profil.');
  const forbidden=['__proto__','constructor','prototype'];
  function bounded(v,depth=0){if(depth>12)fail(400,'invalid_save','Profil je príliš vnorený.');if(typeof v==='number'&&!Number.isFinite(v))fail(400,'invalid_save','Profil obsahuje neplatné číslo.');if(typeof v==='string'&&v.length>12000)fail(400,'invalid_save','Text v profile je príliš dlhý.');if(Array.isArray(v)){if(v.length>2000)fail(400,'invalid_save','Profil obsahuje príliš veľa záznamov.');for(const item of v)bounded(item,depth+1);}else if(object(v))for(const [k,item]of Object.entries(v)){if(forbidden.includes(k))fail(400,'invalid_save','Neplatné pole profilu.');bounded(item,depth+1);}}
  bounded(save);save.Profile.DisplayName=name(save.Profile.DisplayName);return JSON.stringify(save);
}
async function body(request){
  if(!request.headers.get('Content-Type')?.toLowerCase().startsWith('application/json'))fail(415,'json_required','Požiadavka musí obsahovať JSON.');
  if(Number(request.headers.get('Content-Length'))>MAX_BODY)fail(413,'body_too_large','Požiadavka je príliš veľká.');
  const chunks=[];let length=0;const reader=request.body?.getReader();
  if(reader)for(;;){const {done,value}=await reader.read();if(done)break;length+=value.byteLength;if(length>MAX_BODY){await reader.cancel();fail(413,'body_too_large','Požiadavka je príliš veľká.');}chunks.push(value);}
  const bytes=new Uint8Array(length);let offset=0;for(const chunk of chunks){bytes.set(chunk,offset);offset+=chunk.length;}
  let data;try{data=length?JSON.parse(new TextDecoder().decode(bytes)):{};}catch{fail(400,'invalid_json','JSON sa nepodarilo prečítať.');}if(!object(data))fail(400,'invalid_json','Očakáva sa objekt JSON.');return data;
}

export function createUnityOnlineApi({questions=[],now=()=>Date.now()}={}){
  const bank=questions.filter(q=>q&&[7,8,9].includes(q.Grade)&&q.Options?.length===4&&Number.isInteger(q.CorrectOption)&&q.CorrectOption>=0&&q.CorrectOption<4);
  let lastCleanup=-Infinity;
  async function auth(request,db){const token=request.headers.get('Authorization')?.match(/^Bearer ([0-9a-f-]{36})\.([0-9a-f]{64})$/i);if(!token)fail(401,'identity_required','Najprv vytvor alebo prepoj cloudový profil.');const player=await db.prepare('SELECT * FROM unity_profiles WHERE id=? AND secret_hash=?').bind(token[1],await hash(token[2])).first();if(!player)fail(401,'identity_invalid','Súkromný kód profilu neplatí. Prepoj ho znova.');return player;}
  const profileReply=p=>({identityId:p.id,profileRevision:p.revision,saveJson:p.save_json});
  async function cleanup(db,time){if(time-lastCleanup<30000)return;lastCleanup=time;await db.batch([
    db.prepare('UPDATE unity_profiles SET active_room=NULL WHERE active_room IN (SELECT code FROM unity_rooms WHERE updated_at<?)').bind(time-ROOM_TTL),
    db.prepare('DELETE FROM unity_rooms WHERE updated_at<?').bind(time-ROOM_TTL),
    db.prepare('DELETE FROM unity_battles WHERE updated_at<?').bind(time-24*60*60*1000),
  ]);}
  async function member(db,player,code,time){
    if(typeof code!=='string'||!/^[A-Z2-9]{6}$/.test(code))fail(400,'invalid_code','Kód miestnosti má šesť písmen alebo číslic.');
    const row=await db.prepare('SELECT m.*,r.updated_at FROM unity_room_members m JOIN unity_rooms r ON r.code=m.room_code WHERE m.room_code=? AND m.player_id=?').bind(code,player.id).first();
    if(!row)fail(404,'room_missing','Miestnosť neexistuje alebo do nej nie si pripojený.');
    if(row.updated_at<time-ROOM_TTL||row.last_seen<time-PRESENCE_MS)fail(410,'presence_expired','Spojenie s miestnosťou vypršalo. Pripoj sa znova kódom.');return row;
  }
  function prepareQuestion(battle){
    const pool=bank.filter(q=>q.Grade===battle.grade);if(!pool.length)fail(503,'questions_unavailable','Matematické otázky sa pripravujú. Skús súboj neskôr.');
    const selected=pool[((battle.state.Seed>>>0)+battle.state.Round*17)%pool.length];
    const order=[0,1,2,3],shift=((battle.state.Seed>>>0)+battle.state.Round)%4;for(let i=0;i<shift;i++)order.push(order.shift());
    battle.question={id:selected.Id+':'+battle.state.Round,prompt:selected.Prompt,options:order.map(i=>selected.Options[i]),grade:selected.Grade,subject:selected.Subject,topic:MATH_TOPICS[selected.Subject]||'Matematika'};
    battle.answer=order.indexOf(selected.CorrectOption);battle.questionExplanation=selected.Explanation;battle.topic=selected.Subject;
  }
  async function rewards(db,battleId,state,time){
    if(state.Phase!==1||state.Winner<0||state.CorrectAnswers[state.Winner]<1)return;
    const playerId=state.Winner===0?state.leftId:state.rightId;
    await db.prepare('INSERT OR IGNORE INTO unity_battle_rewards (battle_id,player_id,xp,coins,pet_xp,created_at) VALUES (?,?,60,100,40,?)').bind(battleId,playerId,time).run();
  }
  async function updateBattle(db,id,time,mutate=null){
    for(let retry=0;retry<8;retry++){
      const row=await db.prepare('SELECT * FROM unity_battles WHERE id=?').bind(id).first();if(!row)fail(404,'battle_missing','Súboj už nie je dostupný.');
      const battle=JSON.parse(row.state_json),before=JSON.stringify(battle);let actionError=null;
      if(battle.status==='challenge'&&battle.deadline<=time){battle.status='declined';battle.message='Výzva vypršala.';}
      if(battle.status==='active'){
        const active=await db.prepare('SELECT player_id FROM unity_room_members WHERE room_code=? AND last_seen>=?').bind(row.room_code,time-PRESENCE_MS).all();
        const ids=active.results.map(x=>x.player_id),missing=battle.playerIds.map(id=>!ids.includes(id));
        if(missing.some(Boolean)){
          if(missing.every(Boolean)){battle.state.Phase=1;battle.state.Winner=-1;battle.state.PendingActions=[null,null];battle.state.Log=['Súboj skončil po odpojení oboch hráčov.'];}
          else forfeit(battle.state,missing[0]?0:1,battle.state.Round);
          battle.status='finished';battle.deadline=0;
        }else if(time>=battle.deadline){
          const priorAnswer=battle.answer,explanation=battle.questionExplanation;
          const expiredRound=battle.state.Round;
          for(let side=0;side<2&&battle.state.Phase!==1&&battle.state.Round===expiredRound;side++)if(!battle.state.PendingActions[side])submit(battle.state,side,expiredRound,{Kind:1,TargetSlot:0,Correct:false,ActionId:actionId(battle.state,side),Topic:-1});
          battle.explanation=explanation;battle.correctOption=priorAnswer;
          battle.status=battle.state.Phase===1?'finished':'active';battle.deadline=battle.status==='active'?time+TURN_MS:0;if(battle.status==='active')prepareQuestion(battle);
        }
      }
      try{if(mutate)await mutate(battle,row);}catch(error){if(!(error instanceof ApiError))throw error;actionError=error;}
      if(battle.state){battle.state.leftId=battle.playerIds[0];battle.state.rightId=battle.playerIds[1];}
      const after=JSON.stringify(battle);let revision=row.revision;
      if(after!==before){const write=await db.prepare('UPDATE unity_battles SET state_json=?,status=?,revision=revision+1,updated_at=? WHERE id=? AND revision=?').bind(after,battle.status,time,id,row.revision).run();if(!changed(write))continue;revision++;}
      if(battle.status==='finished')await rewards(db,id,battle.state,time);
      if(actionError)throw actionError;return {row:{...row,revision,status:battle.status},battle};
    }fail(409,'battle_busy','Súboj sa práve aktualizuje. Skús ťah znova.');
  }
  async function battleView(db,record,viewerId){
    if(!record)return null;const {battle,row}=record,side=battle.playerIds.indexOf(viewerId);if(side<0)return null;
    const state=battle.state?JSON.parse(JSON.stringify(battle.state)):null;const submitted=state?state.PendingActions.map(Boolean):[false,false];if(state)state.PendingActions=[null,null];
    const earned=await db.prepare('SELECT * FROM unity_battle_rewards WHERE battle_id=? AND player_id=?').bind(row.id,viewerId).first();
    return {id:row.id,status:battle.status,challengerId:battle.playerIds[0],opponentId:battle.playerIds[1],side,round:state?.Round||0,revision:row.revision,submitted,deadline:battle.deadline,
      state,question:battle.status==='active'?battle.question:null,explanation:battle.explanation||battle.message||'',correctOption:battle.correctOption??-1,
      rewardId:earned?'online-duel:'+row.id:'',rewardXP:earned?.xp||0,rewardCoins:earned?.coins||0,rewardPetXP:earned?.pet_xp||0,
      artifactRewards:battle.receipts?.[side]||[]};
  }
  async function snapshot(db,code,player,time){
    const members=await db.prepare('SELECT * FROM unity_room_members WHERE room_code=? AND last_seen>=? ORDER BY player_id').bind(code,time-PRESENCE_MS).all();
    const battleRows=await db.prepare("SELECT id,left_id,right_id,status FROM unity_battles WHERE room_code=? AND (status IN ('challenge','active') OR updated_at>=?) ORDER BY created_at DESC LIMIT 40").bind(code,time-120000).all();
    const belongs=b=>b.left_id===player.id||b.right_id===player.id;
    const own=battleRows.results.find(b=>belongs(b)&&['challenge','active'].includes(b.status))||battleRows.results.find(belongs);const record=own?await updateBattle(db,own.id,time):null;
    const room=await db.prepare('SELECT beacons_mask FROM unity_rooms WHERE code=?').bind(code).first();const beaconsMask=room?.beacons_mask||0;
    return {code,revision:record?.row.revision||0,serverTime:time,beaconsMask,teamShards:beaconsMask.toString(2).split('1').length*10-10,players:members.results.map(m=>{const p=JSON.parse(m.presence_json);delete p.party;const active=battleRows.results.find(b=>(b.status==='challenge'||b.status==='active')&&(b.left_id===m.player_id||b.right_id===m.player_id));return {id:m.player_id,...p,seenAt:m.last_seen,battleId:active?.id||''};}),battle:await battleView(db,record,player.id)};
  }
  async function leave(db,player,code,time){
    const battles=await db.prepare("SELECT id FROM unity_battles WHERE room_code=? AND (left_id=? OR right_id=?) AND status IN ('challenge','active')").bind(code,player.id,player.id).all();
    for(const row of battles.results)await updateBattle(db,row.id,time,b=>{if(b.status==='challenge')b.status='declined';else if(b.status==='active'){forfeit(b.state,b.playerIds.indexOf(player.id),b.state.Round);b.status='finished';b.deadline=0;}});
    await db.batch([db.prepare('DELETE FROM unity_room_members WHERE room_code=? AND player_id=?').bind(code,player.id),db.prepare('UPDATE unity_profiles SET active_room=NULL WHERE id=? AND active_room=?').bind(player.id,code)]);
  }
  return async function api(request,env){
    const url=new URL(request.url);if(url.pathname!==BASE&&!url.pathname.startsWith(BASE+'/'))return null;
    try{
      if(!env?.DB)fail(503,'database_unavailable','Spoločný svet sa pripravuje. Sólo hranie je dostupné.');
      const origin=request.headers.get('Origin');if(origin&&origin!==url.origin||request.headers.get('Sec-Fetch-Site')==='cross-site')fail(403,'cross_origin','Použi adresu hry.');
      if(request.method!=='POST')fail(405,'method_not_allowed','Online protokol používa POST.');
      const data=await body(request),path=url.pathname.slice(BASE.length+1),db=env.DB,time=now();await cleanup(db,time);
      if(path==='profile/create'){
        const recent=await db.prepare('SELECT COUNT(*) AS count FROM unity_profiles WHERE created_at>?').bind(time-60000).first();if(recent.count>=100)fail(429,'too_many_profiles','Vzniká veľa profilov. Skús to o minútu.');
        const saved=saveJson(data.saveJson),id=crypto.randomUUID(),secret=hex(random(32));
        await db.prepare('INSERT INTO unity_profiles (id,secret_hash,save_json,created_at,last_seen) VALUES (?,?,?,?,?)').bind(id,await hash(secret),saved,time,time).run();
        return json({identityToken:id+'.'+secret,identityId:id,profileRevision:1,saveJson:saved},201);
      }
      const player=await auth(request,db);
      if(path==='profile/load')return json(profileReply(player));
      if(path==='profile/save'){
        const saved=saveJson(data.saveJson);if(!Number.isInteger(data.profileRevision)||data.profileRevision<1)fail(400,'invalid_revision','Chýba verzia cloudového profilu.');
        const write=await db.prepare('UPDATE unity_profiles SET save_json=?,revision=revision+1,last_seen=? WHERE id=? AND revision=?').bind(saved,time,player.id,data.profileRevision).run();
        if(!changed(write))fail(409,'profile_conflict','Cloud má novší postup z druhého zariadenia. Načítaj cloudový profil; nič sme neprepísali.');
        return json({identityId:player.id,profileRevision:data.profileRevision+1,message:'Postup uložený v cloude.'});
      }
      if(path==='rooms/create'||path==='rooms/join'){
        const presence=cleanPresence(data.presence);presence.x=0;presence.y=0;presence.z=12;
        if(player.active_room){const previous=await db.prepare('SELECT last_seen FROM unity_room_members WHERE room_code=? AND player_id=?').bind(player.active_room,player.id).first();if(previous&&previous.last_seen>=time-PRESENCE_MS)fail(409,'already_in_room','Najprv odíď z aktuálnej miestnosti.');await leave(db,player,player.active_room,time);}
        let code=typeof data.code==='string'?data.code.trim().toUpperCase():'';
        if(path==='rooms/create'){
          const total=await db.prepare('SELECT COUNT(*) AS count FROM unity_rooms WHERE updated_at>=?').bind(time-ROOM_TTL).first();if(total.count>=200)fail(429,'too_many_rooms','Akadémia má veľa otvorených miestností. Pripoj sa existujúcim kódom.');
          for(let attempt=0;attempt<6;attempt++){code=roomCode();try{await db.prepare('INSERT INTO unity_rooms (code,created_at,updated_at) VALUES (?,?,?)').bind(code,time,time).run();break;}catch(error){if(!String(error.message).includes('UNIQUE'))throw error;code='';}}if(!code)fail(503,'code_unavailable','Kód sa nepodarilo vytvoriť.');
        }
        if(!/^[A-Z2-9]{6}$/.test(code))fail(400,'invalid_code','Kód miestnosti má šesť znakov.');
        const room=await db.prepare('SELECT * FROM unity_rooms WHERE code=? AND updated_at>=?').bind(code,time-ROOM_TTL).first();if(!room)fail(404,'room_missing','Miestnosť neexistuje alebo vypršala.');
        const result=await db.batch([
          db.prepare('INSERT INTO unity_room_members (room_code,player_id,presence_json,last_seen) SELECT ?,?,?,? WHERE (SELECT COUNT(*) FROM unity_room_members WHERE room_code=? AND last_seen>=?)<16 AND EXISTS (SELECT 1 FROM unity_profiles WHERE id=? AND active_room IS NULL)').bind(code,player.id,JSON.stringify(presence),time,code,time-PRESENCE_MS,player.id),
          db.prepare('UPDATE unity_profiles SET active_room=?,last_seen=? WHERE id=? AND active_room IS NULL AND EXISTS (SELECT 1 FROM unity_room_members WHERE room_code=? AND player_id=?)').bind(code,time,player.id,code,player.id),
          db.prepare('UPDATE unity_rooms SET updated_at=? WHERE code=?').bind(time,code),
        ]);if(!changed(result[0]))fail(409,'room_full','Miestnosť je plná alebo už hráš inde. Limit je 16 hráčov.');
        return json({room:await snapshot(db,code,player,time),message:'Spoločný svet · rovnaký kód funguje na PC aj webe.'});
      }
      const code=typeof data.code==='string'?data.code.trim().toUpperCase():'';
      if(path==='rooms/leave'){if(code)await leave(db,player,code,time);return json({message:'Zo spoločného sveta si odpojený.'});}
      const own=await member(db,player,code,time);
      if(path==='rooms/respawn'){
        const p=JSON.parse(own.presence_json);let destination;
        if(data.destination==='spawn'){destination=[0,0,12];p.courseCheckpoint=-1;}
        else if(data.destination==='course'){destination=COURSE[0];p.courseCheckpoint=0;}
        else if(data.destination==='checkpoint'&&Number.isInteger(data.checkpoint)&&data.checkpoint>=0&&data.checkpoint<9&&data.checkpoint<=p.courseCheckpoint)destination=COURSE[data.checkpoint];
        else fail(400,'invalid_destination','Vyber štart alebo už dosiahnutý kontrolný bod.');
        [p.x,p.y,p.z]=destination;await db.prepare('UPDATE unity_room_members SET presence_json=?,last_seen=? WHERE room_code=? AND player_id=?').bind(JSON.stringify(p),time,code,player.id).run();
        return json({room:await snapshot(db,code,player,time)});
      }
      if(path==='rooms/beacon'){
        if(!Number.isInteger(data.beaconIndex)||data.beaconIndex<0||data.beaconIndex>=6)fail(400,'invalid_beacon','Neplatný maják.');
        const p=JSON.parse(own.presence_json),b=BEACONS[data.beaconIndex];if(Math.hypot(p.x-b[0],p.y-b[1],p.z-b[2])>3)fail(409,'beacon_too_far','Priblíž sa k tímovému majáku.');
        await db.prepare('UPDATE unity_rooms SET beacons_mask=beacons_mask | ?,updated_at=? WHERE code=?').bind(1<<data.beaconIndex,time,code).run();
        return json({room:await snapshot(db,code,player,time),message:'Tímový maják rozsvietený.'});
      }
      if(path==='rooms/pulse'){
        if(!Number.isInteger(data.sequence)||data.sequence<1||data.sequence>2147483647)fail(400,'invalid_sequence','Neplatné poradie pohybu.');
        if(data.sequence>own.sequence&&time-own.last_seen>=80){const presence=cleanPresence(data.presence,JSON.parse(own.presence_json),(time-own.last_seen)/1000);await db.prepare('UPDATE unity_room_members SET presence_json=?,sequence=?,last_seen=? WHERE room_code=? AND player_id=? AND sequence<?').bind(JSON.stringify(presence),data.sequence,time,code,player.id,data.sequence).run();}
        await db.prepare('UPDATE unity_rooms SET updated_at=? WHERE code=? AND updated_at<?').bind(time,code,time-5000).run();
        return json({room:await snapshot(db,code,player,time)});
      }
      if(path==='battle/challenge'){
        const target=await db.prepare('SELECT * FROM unity_room_members WHERE room_code=? AND player_id=? AND last_seen>=?').bind(code,data.targetId,time-PRESENCE_MS).first();
        if(!target||data.targetId===player.id)fail(400,'invalid_opponent','Vyber iného pripojeného hráča.');
        const mine=JSON.parse(own.presence_json),theirs=JSON.parse(target.presence_json);if(!mine.party.length||!theirs.party.length)fail(400,'pet_required','Obaja hráči potrebujú aspoň jedného spoločníka.');
        const id=crypto.randomUUID(),seed=new DataView(random(4).buffer).getInt32(0),state=createBattle(id,mine.party,theirs.party,seed);state.Teams[0].Name=mine.name;state.Teams[1].Name=theirs.name;
        const battle={playerIds:[player.id,data.targetId],status:'challenge',deadline:time+CHALLENGE_MS,grade:Math.min(mine.grade,theirs.grade),state,explanation:'',correctOption:-1,receipts:[[],[]]};prepareQuestion(battle);
        const write=await db.prepare("INSERT INTO unity_battles (id,room_code,left_id,right_id,status,state_json,created_at,updated_at) SELECT ?,?,?,?,'challenge',?,?,? WHERE NOT EXISTS (SELECT 1 FROM unity_battles WHERE room_code=? AND status IN ('challenge','active') AND (left_id IN (?,?) OR right_id IN (?,?)))").bind(id,code,player.id,data.targetId,JSON.stringify(battle),time,time,code,player.id,data.targetId,player.id,data.targetId).run();
        if(!changed(write))fail(409,'battle_in_progress','Jeden z hráčov už má súboj alebo čakajúcu výzvu.');return json({room:await snapshot(db,code,player,time),message:'Výzva odoslaná. Súper má 40 sekúnd na prijatie.'});
      }
      if(['battle/accept','battle/decline','battle/action','battle/forfeit'].includes(path)){
        if(typeof data.battleId!=='string'||data.battleId.length>128)fail(400,'invalid_battle','Chýba identifikátor súboja.');
        const permitted=await db.prepare('SELECT room_code,left_id,right_id FROM unity_battles WHERE id=?').bind(data.battleId).first();
        if(!permitted||permitted.room_code!==code||permitted.left_id!==player.id&&permitted.right_id!==player.id)fail(403,'not_your_battle','Tento súboj nepatrí tvojmu profilu.');
        await updateBattle(db,data.battleId,time,(battle,row)=>{
          const side=battle.playerIds.indexOf(player.id);if(side<0||row.room_code!==code)fail(403,'not_your_battle','Tento súboj nepatrí tvojmu profilu.');
          if(path==='battle/accept'){if(side!==1)fail(403,'opponent_only','Výzvu prijíma oslovený hráč.');if(battle.status==='active')return;if(battle.status!=='challenge')fail(409,'challenge_expired','Výzva už neplatí.');battle.status='active';battle.deadline=time+TURN_MS;return;}
          if(path==='battle/decline'){if(battle.status==='challenge'){battle.status='declined';battle.message='Výzva bola odmietnutá.';}return;}
          if(path==='battle/forfeit'){if(battle.status==='challenge'){battle.status='declined';return;}if(battle.status==='active'){forfeit(battle.state,side,battle.state.Round);battle.status='finished';battle.deadline=0;}return;}
          if(battle.status!=='active')fail(409,'battle_not_active','Súboj už nie je aktívny.');
          if(data.round!==battle.state.Round||data.actionId!==actionId(battle.state,side))fail(409,'stale_turn','Tento ťah patrí do iného kola. Obnov stav súboja.');
          const error=availability(battle.state,side,data.kind,data.targetSlot);if(error)fail(409,'invalid_move',error);
          if(data.kind!==5&&(data.questionId!==battle.question.id||!Number.isInteger(data.optionIndex)||data.optionIndex<0||data.optionIndex>3))fail(400,'invalid_answer','Vyber odpoveď na aktuálnu matematickú otázku.');
          const answer=battle.answer,explanation=battle.questionExplanation,correct=data.kind!==5&&data.optionIndex===answer,result=submit(battle.state,side,data.round,{Kind:data.kind,TargetSlot:data.targetSlot,Correct:correct,ActionId:data.actionId,Topic:battle.topic});
          if(!result.Accepted)fail(409,'turn_already_submitted',result.Error);
          if(correct){battle.receipts??=[[],[]];battle.receipts[side].push({id:'gear:'+battle.state.Id+':'+side+':'+data.round,topic:battle.topic,grade:battle.grade});}
          if(result.RoundResolved){battle.explanation=explanation;battle.correctOption=answer;battle.status=battle.state.Phase===1?'finished':'active';battle.deadline=battle.status==='active'?time+TURN_MS:0;if(battle.status==='active')prepareQuestion(battle);}
        });
        return json({room:await snapshot(db,code,player,time)});
      }
      fail(404,'endpoint_missing','Táto online operácia neexistuje.');
    }catch(error){if(error instanceof ApiError)return json({error:error.error,message:error.message},error.status);console.error('Unity online request failed:',error?.message);return json({error:'server_error',message:'Online služba nedokončila požiadavku. Skús ju o chvíľu znova.'},500);}
  };
}

export const unityOnlineLimits=Object.freeze({presenceMs:PRESENCE_MS,roomTtlMs:ROOM_TTL,maxPlayers:16,turnMs:TURN_MS});
