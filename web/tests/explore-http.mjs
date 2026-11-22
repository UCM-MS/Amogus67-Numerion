import assert from 'node:assert/strict';
import {setTimeout as delay} from 'node:timers/promises';
import {createHttpVisitor} from './http-client.mjs';

const base=process.argv[2]||'http://127.0.0.1:4173';
const a=createHttpVisitor(base),b=createHttpVisitor(base);
const pet={kind:'fox',color:'#f6b593',name:'Líška'};
const pa=await a('/session',{name:'Kontrola sveta A'}),pb=await b('/session',{name:'Kontrola sveta B'});
assert.equal(pa.status,200);assert.equal(pb.status,200);assert.notEqual(pa.me.id,pb.me.id);
const created=await a('/worlds',{pet,position:{x:0,y:0,z:13,yaw:0}});assert.equal(created.status,201);
const code=created.world.code;
try{
  const joined=await b(`/worlds/${code}/join`,{pet,position:{x:2,y:0,z:13,yaw:0}});assert.equal(joined.status,200);assert.equal(joined.world.players.length,2);
  await delay(300);
  const moved=await b(`/worlds/${code}/move`,{position:{x:3,y:0,z:12,yaw:.6},moving:true,onGround:true,emote:'wave'});assert.equal(moved.status,200);
  const observed=await a(`/worlds/${code}`),remote=observed.world.players.find(p=>p.id===pb.me.id);
  assert.equal(remote.position.x,3);assert.equal(remote.position.z,12);assert.equal(remote.moving,true);assert.equal(remote.emote,'wave');
  const invalid=await b(`/worlds/${code}/move`,{position:{x:40,y:0,z:40,yaw:0},moving:true,onGround:true});assert.equal(invalid.status,409);assert.equal(invalid.position.x,3);
  const teleported=await b(`/worlds/${code}/move`,{teleport:'course-checkpoint',checkpoint:3,position:{x:40,y:0,z:40,yaw:0},moving:false,onGround:true});assert.equal(teleported.status,200);
  assert.deepEqual(teleported.world.players.find(p=>p.id===pb.me.id).position,{x:32,y:1.7,z:24,yaw:0});
  const out=await b(`/worlds/${code}/leave`,{});assert.equal(out.status,200);
  const remaining=await a(`/worlds/${code}`);assert.equal(remaining.world.players.length,1);
  console.log('HTTP shared world: two independent cookie identities, live motion/emote, server correction, fixed checkpoint and leave passed.');
}finally{await a(`/worlds/${code}/leave`,{});await b(`/worlds/${code}/leave`,{});}
