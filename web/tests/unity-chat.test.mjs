import assert from 'node:assert/strict';
import {createTestD1} from './d1-adapter.mjs';
import {createUnityOnlineApi} from '../worker/unity-online.mjs';
import {CHAT_EMOTES,CHAT_TTL_MS} from '../worker/unity-chat.mjs';

let assertions=0,time=1793707200000,serial=0;
const db=createTestD1(),api=createUnityOnlineApi({now:()=>time});
const equal=(a,b,message)=>{assert.deepEqual(a,b,message);assertions++;};
const check=(value,message)=>{assert.ok(value,message);assertions++;};
const presence=name=>({name,pet:'zlomko',party:['zlomko'],grade:7,x:0,y:0,z:12,yaw:0});
async function request(path,data,person=null,status=200,headers={}) {
  const response=await api(new Request('https://lumen.test/api/unity/'+path,{method:'POST',headers:{'Content-Type':'application/json',
    ...(person?{Authorization:'Bearer '+person.identityToken}:{}),...headers},body:JSON.stringify(data)}),{DB:db});
  equal(response.status,status,path+' status');const result=await response.json();equal(result.ok,status<400,path+' ok');return result;
}
async function identity(name) {return {...await request('profile/create',{saveJson:JSON.stringify({Profile:{DisplayName:name,Grade:7}})},null,201),name};}
async function create(person) {return (await request('rooms/create',{presence:presence(person.name)},person)).room.code;}
async function join(person,code) {return await request('rooms/join',{code,presence:presence(person.name)},person);}
function input(code,text='',emoteId='',extra={}) {return {code,text,emoteId,clientMessageId:'chat-message-'+String(++serial).padStart(8,'0'),...extra};}
async function send(person,code,text='',emoteId='',status=200,extra={}) {return await request('rooms/chat/send',input(code,text,emoteId,extra),person,status);}
async function read(person,code,afterId=0,status=200) {return await request('rooms/chat',{code,afterId},person,status);}

try {
  const alice=await identity('Alica'),bob=await identity('Boris'),eve=await identity('Eva');
  const code=await create(alice);await join(bob,code);const otherCode=await create(eve);
  equal((await read(alice,code)).chat.messages,[],'first history succeeds even when empty');
  await request('rooms/chat',{code},null,401);
  await read(eve,code,0,404);await send(eve,code,'Mimo miestnosti','',404);
  await read(alice,otherCode,0,404);
  await request('rooms/chat',{code},alice,403,{Origin:'https://other.test'});
  await request('rooms/chat',{code},alice,403,{'Sec-Fetch-Site':'cross-site'});
  for(const emoteId of CHAT_EMOTES) {await send(bob,code,'',emoteId);time+=1000;}
  const unicode='🙂'.repeat(240);let reply=await send(alice,code,unicode,'',200,{authorId:eve.identityId,authorName:'Falošný autor',createdAt:1,id:1,identityToken:eve.identityToken});
  let message=reply.chat.messages.at(-1);
  equal(message.text,unicode,'limit counts Unicode codepoints, not UTF-16 surrogate halves');
  equal(message.authorId,alice.identityId,'authenticated author id is server-owned');
  equal(message.authorName,'Alica','author name comes from accepted membership, never request fields');
  equal(message.createdAt,time,'server clock owns timestamps');
  check(Number.isSafeInteger(message.id)&&message.id>1,'server generates monotonic integer ids');
  const wire=JSON.stringify(reply.chat);check(!wire.includes(alice.identityToken)&&!wire.includes(eve.identityToken)&&!wire.includes('saveJson')&&!wire.includes('secret_hash'),'chat never projects private credentials or saves');
  equal(reply.chat.messages.slice(0,6).map(m=>m.emoteId),CHAT_EMOTES,'all six allowed emotes retained in order');
  await send(alice,code,'🙂'.repeat(241),'',400);
  await send(alice,code,'','teleport',400);await send(alice,code,'both','wave',400);await send(alice,code,'','',400);
  await send(alice,code,'bad\u0001control','',400);await send(alice,code,'bad\u202ebidi','',400);
  await send(alice,code,'Profil '+alice.identityToken,'',400);
  await request('rooms/chat/send',input(code,'ok','',{clientMessageId:'short'}),alice,400);
  await request('rooms/chat/send',input(code,'ok','',{text:{injected:'object'}}),alice,400);
  const countBefore=(await db.prepare('SELECT COUNT(*) AS n FROM unity_room_messages').first()).n;
  await send(alice,code,'bad cursor','',400,{afterId:-1});await read(alice,code,1.5,400);
  equal((await db.prepare('SELECT COUNT(*) AS n FROM unity_room_messages').first()).n,countBefore,'cursor validation happens before insertion');
  await request('rooms/chat/send',input(code,'ok','',{padding:'x'.repeat(5000)}),alice,413);
  time+=1000;reply=await send(alice,code,'<b>Matematika</b> & 7 < 9');
  equal(reply.chat.messages.at(-1).text,'<b>Matematika</b> & 7 < 9','plain text is preserved; client disables rich text');
  const cursor=reply.chat.cursor;equal((await read(bob,code,cursor)).chat.messages,[],'cursor excludes already received messages');
  time+=1000;reply=await send(bob,code,'Ďalšia otázka');
  equal((await read(alice,code,cursor)).chat.messages.map(m=>m.id),[reply.chat.cursor],'cursor returns only fresh room messages');
  const charlie=await identity('Cyril');await join(charlie,code);
  equal((await read(charlie,code)).chat.messages.length,reply.chat.messages.length,'new authorized members receive recent room history');
  const burst=await Promise.all(Array.from({length:4},()=>api(new Request('https://lumen.test/api/unity/rooms/chat/send',{
    method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+charlie.identityToken},body:JSON.stringify(input(code,'burst'))}),{DB:db})));
  equal(burst.map(r=>r.status).sort(),[200,429,429,429],'concurrent requests reserve only one rate slot');
  const retryInput=input(code,'idempotent');time+=1000;
  reply=await request('rooms/chat/send',retryInput,charlie);const delivered=reply.chat.cursor;
  reply=await request('rooms/chat/send',retryInput,charlie);equal(reply.chat.cursor,delivered,'same request id retries without a duplicate or rate penalty');
  await request('rooms/chat/send',{...retryInput,text:'changed'},charlie,409);
  for(let i=0;i<10;i++) {time+=1000;await send(charlie,code,'','gg');}
  time+=1000;await send(charlie,code,'thirteenth','',429);
  await request('rooms/leave',{code},charlie);await join(charlie,code);
  await send(charlie,code,'leave cannot evade limit','',429);
  time+=60000;await join(charlie,code);await send(charlie,code,'new window');
  await read(alice,code,0,410);await send(alice,code,'expired','',410);
  await request('rooms/leave',{code},charlie);await read(charlie,code,0,404);

  const group=[];for(let i=0;i<6;i++)group.push(await identity('Skupina '+i));
  const cappedCode=await create(group[0]);for(const person of group.slice(1))await join(person,cappedCode);
  for(let round=0;round<10;round++) {for(const person of group)await send(person,cappedCode,'Správa '+round);time+=1000;}
  const capped=await read(group[0],cappedCode);
  equal(capped.chat.messages.length,50,'room retains at most fifty messages');
  equal((await db.prepare('SELECT COUNT(*) AS n FROM unity_room_messages WHERE room_code=?').bind(cappedCode).first()).n,50,'history is capped in storage, not only the response');
  check(capped.chat.messages.every((m,i,a)=>i===0||m.id>a[i-1].id),'history sequence is monotonic');
  equal(capped.chat.messages[0].text,'Správa 1','oldest ten of sixty messages are removed');
  time+=CHAT_TTL_MS+1;await join(group[0],cappedCode);
  equal((await read(group[0],cappedCode)).chat.messages,[],'ten-minute TTL hides expired history from rejoining members');
  equal((await db.prepare('SELECT COUNT(*) AS n FROM unity_room_messages WHERE room_code=?').bind(cappedCode).first()).n,0,'expiry cleanup removes old stored messages');
  await send(group[0],cappedCode,'Po vypršaní');
  await db.prepare('DELETE FROM unity_rooms WHERE code=?').bind(cappedCode).run();
  equal((await db.prepare('SELECT COUNT(*) AS n FROM unity_room_messages WHERE room_code=?').bind(cappedCode).first()).n,0,'room deletion cascades messages');
  equal((await db.prepare('SELECT COUNT(*) AS n FROM unity_chat_limits WHERE room_code=?').bind(cappedCode).first()).n,0,'room deletion cascades rate state');
  console.log('UNITY_CHAT_PASS '+assertions+' assertions');
} finally {db.close();}
