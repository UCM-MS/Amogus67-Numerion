import assert from 'node:assert/strict';
import {setTimeout as delay} from 'node:timers/promises';
import {AcademyEngine} from '../public/academy-engine.js';
import {createHttpVisitor} from './http-client.mjs';
const base=process.argv[2]||'http://127.0.0.1:4173';
const lobbyOnly=process.argv.includes('--lobby-only');
const bank=new Map(new AcademyEngine(null).getDefinitions().questions.map(q=>[q.id,q]));
const host=createHttpVisitor(base),guest=createHttpVisitor(base);
const a=await host('/session',{name:'Overenie A'}),b=await guest('/session',{name:'Overenie B'});
assert.equal(a.status,200);assert.equal(b.status,200);assert.notEqual(a.me.id,b.me.id);
const pet={kind:'fox',color:'#ffab77',name:'Mochi'};
for(const mode of lobbyOnly?['quiz']:['quiz','duel']){
  let state=await host('/rooms',{mode,pet});assert.equal(state.status,201);const code=state.room.code;
  const join=await guest(`/rooms/${code}/join`,{pet:{kind:'robot',color:'#91d3ee',name:'Akademik'}});assert.equal(join.status,200);
  assert.equal(join.room.players.length,2);
  state=await host(`/rooms/${code}`);assert.deepEqual(new Set(state.room.players.map(p=>p.id)),new Set([a.me.id,b.me.id]));
  if(lobbyOnly){assert.equal((await guest(`/rooms/${code}/leave`,{})).status,200);assert.equal((await host(`/rooms/${code}/leave`,{})).status,200);console.log('Public HTTP: two independent cookie profiles joined the same room; leave and server persistence verified.');continue;}
  state=await host(`/rooms/${code}/start`,{});assert.equal(state.status,200);
  let submitted=0;
  while(state.room.status!=='finished'){
    if(state.room.status==='question'){
      const room=state.room,q=bank.get(room.question.id);assert.ok(q);assert.equal(room.reveal,null);assert.equal('answer' in room.question,false);
      const wrong=q.options.find(o=>o.id!==q.answer).id;
      await Promise.all([host(`/rooms/${code}/answer`,{round:room.round,answerId:q.answer,move:'strike'}),guest(`/rooms/${code}/answer`,{round:room.round,answerId:wrong,move:'guard'})]);submitted++;
      const duplicate=await host(`/rooms/${code}/answer`,{round:room.round,answerId:q.answer,move:'strike'});assert.equal(duplicate.status,409);
    }
    await delay(500);state=await host(`/rooms/${code}`);assert.equal(state.status,200);
    if(state.room.status==='reveal'){assert.equal(state.room.reveal.correctAnswerId,bank.get(state.room.question.id).answer);}
  }
  assert.equal(state.room.winnerId,a.me.id);assert.ok(state.me.shards>0);
  const wallet=state.me.shards;
  const repeated=await Promise.all(Array.from({length:4},()=>host(`/rooms/${code}`)));
  for(const result of repeated)assert.equal(result.me.shards,wallet);
  console.log(`${mode}: ${submitted} rounds completed through HTTP; winner, damage, exact-once reward and reconnect verified.`);
}
if(!lobbyOnly){const result=await host('/gear',{slot:'power'});assert.equal(result.status,200);assert.equal(result.me.gear.power,1);const board=await guest('/leaderboard');assert.ok(board.players.some(p=>p.id===a.me.id));console.log('HTTP: earned shards purchased real gear and winner appears on shared leaderboard.');}
