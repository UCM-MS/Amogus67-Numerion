import assert from 'node:assert/strict';
import {stepMotion,isBlocked,supportAt,findWalkingPath,clipCameraLine} from '../public/physics3d.js';
const points=[[20,22,.35],[24,22,.8],[28,24,1.2],[32,24,1.7],[36,27,2.2],[36,31,2.7],[32,34,3.1]];
const platforms=points.map(([x,z,top],i)=>({id:`trail-${i}`,x,z,top,hx:1.75,hz:1.75}));
const world={platforms,colliders:[],bounds:{minX:-49,maxX:49,minZ:-49,maxZ:49}};
const cameraWall=[{x:0,z:5,hx:3,hz:1,top:6}];
const shortened=clipCameraLine({x:0,y:2,z:0},{x:0,y:3,z:12},cameraWall);
assert.ok(shortened.z<3.75&&shortened.z>2.5,'The camera stops in front of a building instead of entering it');
assert.deepEqual(clipCameraLine({x:0,y:9,z:0},{x:0,y:9,z:12},cameraWall),{x:0,y:9,z:12},'A camera above a roof keeps its full boom');
assert.deepEqual(clipCameraLine({x:8,y:2,z:0},{x:8,y:3,z:12},cameraWall),{x:8,y:3,z:12},'A parallel ray beside the wall is not clipped');
assert.equal(isBlocked(24,22,0,[],platforms),true,'Ground movement cannot walk through a raised platform');
assert.equal(isBlocked(24,22,.8,[],platforms),false,'The top of a platform remains walkable');
assert.equal(supportAt(24,22,.8,platforms).id,'trail-1');
let body={x:20,y:.35,z:22,vy:0,onGround:true};
for(let i=1;i<platforms.length;i++){
  const p=platforms[i],dx=p.x-body.x,dz=p.z-body.z,distance=Math.hypot(dx,dz);
  body.vy=8.6;body.onGround=false;
  for(let frame=0;frame<90;frame++){
    const remaining=Math.hypot(p.x-body.x,p.z-body.z),movement=Math.min(7.2/60,remaining);
    body=stepMotion(body,remaining>.01?(p.x-body.x)/remaining*movement:0,remaining>.01?(p.z-body.z)/remaining*movement:0,1/60,world);
    if(body.onGround&&body.platformId===p.id)break;
  }
  assert.equal(body.platformId,p.id,`Course jump ${i} (${distance.toFixed(1)}m) can be completed at default speed`);
}
body=stepMotion({...body,x:44,z:40,vy:0,onGround:true},0,0,.05,world);
assert.equal(body.onGround,false,'Walking off a slab starts a fall');
for(let i=0;i<120;i++)body=stepMotion(body,0,0,1/60,world);
assert.equal(body.y,0,'Falling lands safely on the island');
assert.equal(body.onGround,true);
assert.equal(isBlocked(0,0,3,[{x:0,z:0,hx:1,hz:1}],[]),true,'Landmark walls continue blocking horizontal movement');
const walled={colliders:[{x:0,z:0,hx:.3,hz:4}],platforms:[],bounds:{minX:-12,maxX:12,minZ:-12,maxZ:12}};
const route=findWalkingPath({x:-4,z:0},{x:4,z:0},walled);
assert.ok(route.some(p=>Math.abs(p.z)>=5),'Guidance takes the player around a long wall');
for(let i=1;i<route.length;i++)for(let t=0;t<=1;t+=.1)assert.equal(isBlocked(route[i-1].x+(route[i].x-route[i-1].x)*t,route[i-1].z+(route[i].z-route[i-1].z)*t,0,walled.colliders,[]),false,'Each visible route segment stays walkable');
console.log('Physics: raised platforms, seven reachable course jumps, walls and safe landing passed.');
// Slow rendering must retain the same reachable course, not shorten each jump.
for(const fps of [10,20]){
  let runner={x:20,y:.35,z:22,vy:0,onGround:true};
  for(const p of platforms.slice(1)){
    runner.vy=8.6;runner.onGround=false;
    for(let i=0;i<fps*2;i++){
      const remaining=Math.hypot(p.x-runner.x,p.z-runner.z),step=Math.min(7.2/fps,remaining);
      runner=stepMotion(runner,remaining>.01?(p.x-runner.x)/remaining*step:0,remaining>.01?(p.z-runner.z)/remaining*step:0,1/fps,world);
      if(runner.onGround&&runner.platformId===p.id)break;
    }
    assert.equal(runner.platformId,p.id,`${fps} FPS still lands on ${p.id}`);
  }
}
