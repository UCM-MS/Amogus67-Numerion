import assert from 'node:assert/strict';
import * as THREE from '../public/vendor/three.module.js';
import {createAcademyWorld} from '../public/scene3d.js';
import {isBlocked, stepMotion,clipCameraLine} from '../public/physics3d.js';
import {createExplorer,createCompanion} from '../public/avatar3d.js';

// CanvasTexture construction needs a canvas, but geometry/physics do not need WebGL.
const priorDocument=globalThis.document;
globalThis.document={createElement(){return {width:0,height:0,getContext(){return {
  beginPath(){},roundRect(){},fill(){},stroke(){},fillText(){},drawImage(){},
};}};}};
let world;
try { world=createAcademyWorld(new THREE.Scene()); }
finally { if(priorDocument===undefined)delete globalThis.document;else globalThis.document=priorDocument; }

assert.equal(world.platforms.length,7,'The complete jump course is present');
assert.equal(world.course.platforms,world.platforms,'Rendering and physics share the same actual slabs');
assert.equal(world.collectibles.length,12,'The campus has twelve unique discoverable crystals');
assert.equal(new Set(world.collectibles.map(item=>item.id)).size,12);
assert.equal(world.interactables.find(item=>item.id==='parkour-start')?.type,'parkour');
const targetPositions={'egg-basic':[-4,0],'egg-science':[0,0],'egg-cosmic':[4,0],hatch:[-16,7],upgrade:[-22,7],'quiz-math':[16,4],'quiz-science':[16,-16],'quiz-nature':[-18,-16],'portal-astral':[0,-30],'parkour-start':[17,22]};
for(const [id,[x,z]] of Object.entries(targetPositions)){const target=world.interactables.find(item=>item.id===id);assert.deepEqual(target?.position,{x,z},`${id} retains the controller/network world contract`);}
assert.ok(world.colliders.every(item=>Number.isFinite(item.top)&&item.top>0),'Camera collision has finite landmark heights');

for(const platform of world.platforms){
  assert.ok(platform.hx>=1.6&&platform.hz>=1.6,'Landings have enough room for the avatar');
  assert.equal(world.colliders.some(c=>Math.abs(c.x-platform.x)<c.hx+platform.hx&&Math.abs(c.z-platform.z)<c.hz+platform.hz),false,`${platform.id} has no tree or landmark inside its landing`);
}
for(const item of [...world.interactables,...world.collectibles]){
  assert.equal(world.colliders.some(c=>Math.abs(c.x-item.position.x)<c.hx+.55&&Math.abs(c.z-item.position.z)<c.hz+.55),false,`${item.id} has an unobstructed centre`);
}
// Check actual campus connectivity, including the walled base entrance, at avatar width.
const reachable=[],queue=[[world.spawn.x,world.spawn.z]],visited=new Set([`${world.spawn.x},${world.spawn.z}`]);
for(let cursor=0;cursor<queue.length;cursor++){
  const [x,z]=queue[cursor];reachable.push({x,z});
  for(const [dx,dz] of [[1,0],[-1,0],[0,1],[0,-1]]){
    const nx=x+dx,nz=z+dz,key=`${nx},${nz}`;
    if(visited.has(key)||nx<world.bounds.minX+1||nx>world.bounds.maxX-1||nz<world.bounds.minZ+1||nz>world.bounds.maxZ-1||isBlocked(nx,nz,0,world.colliders,world.platforms))continue;
    visited.add(key);queue.push([nx,nz]);
  }
}
for(const item of [...world.interactables,...world.collectibles]){
  assert.ok(reachable.some(point=>Math.hypot(point.x-item.position.x,point.z-item.position.z)<item.radius),`${item.id} can be reached from spawn without crossing a wall`);
}
for(const [x,z] of [[6,12],[-8,22],[14,-3],[-12,-18],[4,-27],[30,18]]){
  assert.equal(isBlocked(x,z,0,world.colliders,world.platforms),false,`Co-op beacon ${x},${z} has an open centre`);
  assert.ok(reachable.some(point=>Math.hypot(point.x-x,point.z-z)<1.5),`Co-op beacon ${x},${z} is reachable from spawn`);
}

// Instancing must retain collision/render agreement and nested rotated prop transforms.
world.root.updateMatrixWorld(true);
const cubeMatrices=[];let meshDraws=0;
world.root.traverse(object=>{
  if(!object.isMesh)return;
  meshDraws++;
  if(object.geometry.type!=='BoxGeometry')return;
  if(object.isInstancedMesh){
    for(let index=0;index<object.count;index++){
      const matrix=new THREE.Matrix4();object.getMatrixAt(index,matrix);
      cubeMatrices.push(matrix.premultiply(object.matrixWorld));
    }
  }else cubeMatrices.push(object.matrixWorld.clone());
});
const close=(actual,expected,label)=>assert.ok(Math.abs(actual-expected)<1e-5,`${label}: ${actual} versus ${expected}`);
const matrixAt=(x,y,z)=>cubeMatrices.find(matrix=>{
  const p=new THREE.Vector3().setFromMatrixPosition(matrix);
  return Math.abs(p.x-x)<1e-5&&Math.abs(p.y-y)<1e-5&&Math.abs(p.z-z)<1e-5;
});
for(const platform of world.platforms){
  const matrix=matrixAt(platform.x,platform.top/2,platform.z);
  assert.ok(matrix,`${platform.id} has a rendered platform slab`);
  const topCorner=new THREE.Vector3(.5,.5,.5).applyMatrix4(matrix);
  close(topCorner.x,platform.x+platform.hx,`${platform.id} east edge`);
  close(topCorner.y,platform.top,`${platform.id} landing height`);
  close(topCorner.z,platform.z+platform.hz,`${platform.id} south edge`);
}
const benchSeat=matrixAt(11,.65,11);
assert.ok(benchSeat,'The rotated nested bench remains present after batching');
const benchEnd=new THREE.Vector3(.5,0,0).applyMatrix4(benchSeat);
close(benchEnd.x,11,'Rotated bench x');close(benchEnd.z,12.5,'Rotated bench z');
assert.ok(world.renderStats.staticMeshes>200,'Static pieces are batched for laptop performance');
assert.ok(meshDraws<330,'The detailed campus leaves draw budget for the arena and explorers');
assert.ok(world.renderStats.labelCount>=40&&world.renderStats.labelBatches===1,'Readable district signs share one atlas draw');
assert.deepEqual(world.landmarks.map(item=>item.name),['AcademyRotunda','ArrivalPromenade','CompanionSanctuary','DiscoverySupplyCart'],'The hub has distinct arrival, academy and collection landmarks');
assert.ok(world.colliders.some(item=>item.x===7.2&&item.z===-10&&item.top>12),'The tall clock tower has matching camera and character collision');
assert.equal(isBlocked(0,23,0,world.colliders,world.platforms),false,'The grand arrival arch keeps its full centre open');
assert.ok(world.cameraColliders.length>world.colliders.length,'Overhead architecture has a dedicated camera collision set');
assert.ok(world.cameraColliders.every(c=>Number.isFinite(c.top)&&Number.isFinite(c.bottom??0)&&(c.bottom??0)<c.top));
const baseView={x:-13,y:1.9,z:10},baseBoom={x:-13,y:1.9+Math.sin(.39)*20,z:10+Math.cos(.39)*20};
const clippedBaseCamera=clipCameraLine(baseView,baseBoom,world.cameraColliders);
assert.ok(clippedBaseCamera.z<17.1,'A camera approaching the hatch stops before the teal sanctuary lintel');
assert.ok(clippedBaseCamera.z>baseView.z+3,'The camera retains useful distance beneath the pergola');
assert.equal(isBlocked(-13,17.65,0,world.colliders,world.platforms),false,'Camera-only lintels leave their ground passage walkable');
const marketView={x:0,y:1.9,z:0},marketBoom={x:0,y:12,z:-10};
assert.ok(clipCameraLine(marketView,marketBoom,world.cameraColliders).z>-6,'The egg-market canopy also shortens an upward camera boom');
assert.ok(world.renderStats.staticMeshes>500,'The architectural detail uses the same instanced buffers');
const surfaceMaterials=new Set();world.root.traverse(object=>{if(object.isMesh&&object.material?.customProgramCacheKey?.().startsWith('academy-surface-'))surfaceMaterials.add(object.material);});
assert.ok(surfaceMaterials.size>=5,'Grass, paving, cliffs and district floors get resolution-independent surface detail');
for(const surface of surfaceMaterials){
  const shader={vertexShader:'#include <begin_vertex>',fragmentShader:'#include <color_fragment>'};surface.onBeforeCompile(shader);
  assert.ok(shader.vertexShader.includes('instanceMatrix*academyWorldPosition'),'Surface detail preserves instanced world transforms');
  assert.ok(shader.fragmentShader.includes('academySurfacePosition'),'Surface variation samples the actual world position');
}
const atlas=world.root.getObjectByName('DistrictSignAtlas');
assert.equal(atlas.material.map.image.width,2048,'The sign atlas fits conservative GPU texture limits');
assert.ok(atlas.material.map.image.height<=2048);
const atlasUvs=atlas.geometry.getAttribute('uv');for(let index=0;index<atlasUvs.count;index++)assert.ok(atlasUvs.getX(index)>=0&&atlasUvs.getX(index)<=1&&atlasUvs.getY(index)>=0&&atlasUvs.getY(index)<=1,'Sign UVs stay within their atlas');
world.ambiance.setDaylight(.3);assert.equal(world.ambiance.ocean.material.uniforms.uDaylight.value,.3,'Water responds to the shared day cycle');
const floatingIsland=world.animated.find(item=>item.kind==='island'),islandY=floatingIsland.object.position.y;
world.update(1/60,3.5);assert.equal(world.ambiance.windTime.value,3.5,'Water, grass and particles share one animation clock');
assert.notEqual(floatingIsland.object.position.y,islandY,'Sky gardens float without rebuilding their geometry');
assert.ok(world.ambiance.waterfall&&world.ambiance.particles,'The landscape includes flowing water and ambient particles');

// Dynamic objects must keep their own visibility and transforms after static batching.
for(const item of world.collectibles){
  assert.equal(item.mesh.parent,world.root,'Collectible animation is not baked into a static batch');
  assert.equal(item.shadow.parent,world.root,'Collectible shadow visibility remains independent');
}
world.setState({progress:{collected:['crystal-01','crystal-12']}});
for(const index of [0,11]){assert.equal(world.collectibles[index].mesh.visible,false);assert.equal(world.collectibles[index].shadow.visible,false);}
assert.equal(world.collectibles[1].mesh.visible,true);
const animatedEgg=world.animated.find(item=>item.kind==='egg'),eggY=animatedEgg.object.position.y;
const crystalY=world.collectibles[1].mesh.position.y;
world.update(1/60,1);
assert.notEqual(animatedEgg.object.position.y,eggY,'Market eggs still float');
assert.notEqual(world.collectibles[1].mesh.position.y,crystalY,'Available crystals still float');
world.setState({pets:[{id:'test-pet',name:'Pip',color:'#ffc86e'}],progress:{collected:[]}});
const displayedPets=[];world.root.traverse(object=>{if(object.userData.pet)displayedPets.push(object);});
assert.equal(displayedPets.length,1,'A newly hatched companion can still appear on its podium');
const petY=displayedPets[0].position.y;world.update(1/60,2);assert.notEqual(displayedPets[0].position.y,petY,'Owned companions stay animated');
assert.equal(world.collectibles[0].mesh.visible,true,'Resetting progress restores collectible art');
world.setState({pets:[{id:'test-pet',name:'Nova',color:'#7bb5ee',kind:'owl',level:2}],progress:{collected:[]}});
const importedPets=[];world.root.traverse(object=>{if(object.userData.pet)importedPets.push(object);});
assert.equal(importedPets.length,1);
assert.notEqual(importedPets[0],displayedPets[0],'Importing changed art under the same companion ID refreshes its podium');
const importedColor=new THREE.Color('#7bb5ee');let colorFound=false;
importedPets[0].traverse(object=>{const colors=object.isMesh&&object.geometry.getAttribute('color');if(colors)for(let i=0;i<colors.count;i++)if(Math.abs(colors.getX(i)-importedColor.r)<1e-6&&Math.abs(colors.getY(i)-importedColor.g)<1e-6&&Math.abs(colors.getZ(i)-importedColor.b)<1e-6)colorFound=true;});
assert.ok(colorFound,'Imported companion color reaches its batched visible geometry');
world.setState({pets:[{id:'test-pet',name:'Nova',color:'#7bb5ee',kind:'owl',level:3}],progress:{collected:[]}});
const leveledPets=[];world.root.traverse(object=>{if(object.userData.pet)leveledPets.push(object);});
assert.notEqual(leveledPets[0],importedPets[0],'A companion level change also refreshes the podium');
world.setState({pets:[],progress:{collected:[]}});
assert.equal(leveledPets[0].parent,null,'Resetting ownership removes the old companion display');

// Play every jump with the same controller physics and unupgraded movement speed.
let body={...world.course.spawn,vy:0,onGround:true};
for(const platform of world.platforms.slice(1)){
  body.vy=8.6;body.onGround=false;
  for(let frame=0;frame<90;frame++){
    const remaining=Math.hypot(platform.x-body.x,platform.z-body.z),movement=Math.min(7.2/60,remaining);
    body=stepMotion(body,remaining>.01?(platform.x-body.x)/remaining*movement:0,remaining>.01?(platform.z-body.z)/remaining*movement:0,1/60,world);
    if(body.onGround&&body.platformId===platform.id)break;
  }
  assert.equal(body.platformId,platform.id,`${platform.id} is reachable on the rendered course at default speed`);
}
assert.equal(body.y,world.platforms.at(-1).top,'The complete course ends on its visible finish platform');

// Shared local/remote rigs animate independently of physics and preserve shared GPU resources.
const explorer=createExplorer(THREE),secondExplorer=createExplorer(THREE);
assert.equal(explorer.arms.length,2);assert.equal(explorer.legs.length,2);
assert.equal(explorer.group.getObjectByName('ExplorerName'),undefined,'The local avatar does not add a floating name label');
explorer.group.position.set(2,3,4);explorer.animate(1/60,1,{moving:true,onGround:true});
assert.deepEqual(explorer.group.position.toArray(),[2,3,4],'Avatar gait never moves the authoritative physics position');
assert.notEqual(explorer.arms[0].rotation.x,0,'The explorer has a walking gait');
explorer.animate(1/60,2,{onGround:false,yaw:.4,emote:'wave'});assert.equal(explorer.group.rotation.y,.4);assert.ok(explorer.arms[1].rotation.x<-2,'A remote wave is a distinct arm pose');
assert.equal(explorer.group.getObjectByName('ExplorerContact').visible,false,'Airborne explorers hide their contact ellipse');
explorer.animate(1/60,2.1,{onGround:true});assert.equal(explorer.group.getObjectByName('ExplorerContact').visible,true,'Landing restores cheap contact grounding');
explorer.setPet({kind:'owl',color:'#ffc86e',name:'Mia'});const owl=explorer.group.children.find(child=>child.userData.kind==='owl');assert.ok(owl);
explorer.setPet({kind:'owl',color:'#ffc86e',name:'Mia'});assert.equal(explorer.group.children.find(child=>child.userData.kind==='owl'),owl,'Repeated state sync reuses the existing companion');
explorer.setPet({kind:'owl',color:'#ffc86e',name:'Mia',level:5,rarity:'legendary'});
const trainedOwl=explorer.group.children.find(child=>child.userData.kind==='owl');assert.notEqual(trainedOwl,owl,'Training refreshes an equipped companion’s visible rank');assert.equal(trainedOwl.userData.level,5);assert.equal(trainedOwl.userData.rarity,'legendary');
explorer.setPet(null);assert.equal(owl.parent,null,'Unequipping detaches the companion');
assert.equal(trainedOwl.parent,null,'Unequipping detaches the trained rig too');
const firstMesh=group=>{let first;group.traverse(object=>{if(!first&&object.isMesh)first=object;});return first;};
assert.equal(firstMesh(explorer.group).geometry,firstMesh(secondExplorer.group).geometry,'Players share primitive GPU buffers');
let sharedDisposed=false;firstMesh(explorer.group).geometry.addEventListener('dispose',()=>sharedDisposed=true);
explorer.dispose();secondExplorer.dispose();assert.equal(sharedDisposed,false,'Disposing a remote rig does not destroy another player’s shared geometry');
const silhouettes=new Set();let maxPetDraws=0;
for(const kind of ['bunny','cat','fox','owl','robot','dragon']){
  const pet=createCompanion(THREE,{kind,color:'#ffc86e'});pet.userData.animate(1/60,1);pet.updateMatrixWorld(true);
  const size=new THREE.Box3().setFromObject(pet).getSize(new THREE.Vector3());silhouettes.add(size.toArray().map(n=>n.toFixed(2)).join(','));
  let draws=0;pet.traverse(object=>{if(object.isMesh)draws++;});maxPetDraws=Math.max(maxPetDraws,draws);assert.ok(draws<=16,'Companion detail stays within its per-player draw budget');pet.userData.dispose();
}
assert.equal(silhouettes.size,6,'All six companion kinds have distinct three-dimensional silhouettes');
const blinkPet=createCompanion(THREE,{kind:'cat',name:'Pip',level:3,rarity:'rare'}),blinkEyes=blinkPet.getObjectByName('CompanionEyes');
const eyeAnchor=blinkEyes.position.clone();let blinkObserved=false;
for(let time=0;time<6;time+=.02){blinkPet.userData.animate(1/60,time,{moving:true});if(blinkEyes.scale.y<.5)blinkObserved=true;assert.ok(blinkEyes.position.equals(eyeAnchor),'Blinking never changes the eye anchor');}
assert.ok(blinkObserved,'Companions blink over a complete idle cycle');
const animatedFoot=blinkPet.getObjectByName('CompanionFoot');blinkPet.userData.animate(1/60,1,{moving:true});assert.ok(Math.abs(animatedFoot.rotation.x)>.05,'Following companions have a foot gait');blinkPet.userData.dispose();
globalThis.document={createElement(){return {width:0,height:0,getContext(){return {beginPath(){},roundRect(){},fill(){},fillText(){}};}};}};
let remote;
try {remote=createExplorer(THREE,{name:'Mira'});} finally {if(priorDocument===undefined)delete globalThis.document;else globalThis.document=priorDocument;}
const sprite=remote.group.getObjectByName('ExplorerName');assert.ok(sprite?.isSprite,'A named remote player gets a readable label');
let nameTextureDisposed=false,nameMaterialDisposed=false;sprite.material.map.addEventListener('dispose',()=>nameTextureDisposed=true);sprite.material.addEventListener('dispose',()=>nameMaterialDisposed=true);remote.dispose();
assert.ok(nameTextureDisposed&&nameMaterialDisposed,'Unique remote name resources are released on disconnect');
console.log(`World: reachable campus/co-op beacons, seven real landings, batched signs, atmosphere and shared explorer/pet rigs passed (${meshDraws} world mesh draws; companions max ${maxPetDraws}).`);
