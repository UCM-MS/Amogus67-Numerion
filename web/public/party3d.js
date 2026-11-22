import * as THREE from './vendor/three.module.js';
import { createExplorer } from './avatar3d.js';

/** Remote visitors and the shared expedition use the same world coordinates. */
export function createPartyWorld(scene) {
  const peers = new Map(), beacons = new Map();
  const geometry = new THREE.IcosahedronGeometry(.58, 0);
  const stemGeometry = new THREE.CylinderGeometry(.035, .035, 2, 6);
  const ringGeometry = new THREE.RingGeometry(.7, .84, 24);
  const crystalMaterial = new THREE.MeshStandardMaterial({color:'#6bf3e0',emissive:'#31c4b0',emissiveIntensity:.6,metalness:.15,roughness:.25});
  const ringMaterial = new THREE.MeshBasicMaterial({color:'#b6fff2',transparent:true,opacity:.8,side:THREE.DoubleSide});
  const stemMaterial = new THREE.MeshBasicMaterial({color:'#69dfd0',transparent:true,opacity:.45});
  let connected = false;
  const destination = new THREE.Vector3();

  function setPeers(players = [], meId) {
    const active = new Set();
    for (const visitor of players) {
      if (visitor.id === meId || !visitor.position || visitor.connected === false) continue;
      active.add(visitor.id);
      let peer = peers.get(visitor.id);
      if (!peer || peer.visitor.name !== visitor.name) {
        const previousPosition=peer?.rig.group.position.clone(),previousYaw=peer?.rig.group.rotation.y;
        if(peer){scene.remove(peer.rig.group);peer.rig.dispose();}
        const rig = createExplorer(THREE,{name:visitor.name,color:['#88cbe8','#b1a0ed','#9ddba4','#f6b593'][peers.size%4],pet:visitor.pet});
        if(previousPosition)rig.group.position.copy(previousPosition);else rig.group.position.set(visitor.position.x,visitor.position.y,visitor.position.z);
        rig.group.rotation.y=previousYaw??visitor.position.yaw;
        scene.add(rig.group); peer = {rig,visitor,petSignature:''}; peers.set(visitor.id,peer);
      }
      peer.visitor = visitor;
      const signature = JSON.stringify(visitor.pet || null);
      if (peer.petSignature !== signature) {peer.rig.setPet(visitor.pet);peer.petSignature=signature;}
    }
    for (const [id,peer] of peers) if (!active.has(id)) {scene.remove(peer.rig.group);peer.rig.dispose();peers.delete(id);}
  }
  function setBeacons(items = []) {
    const active = new Set();
    for (const item of items) {
      active.add(item.id); let marker = beacons.get(item.id);
      if (!marker) {
        const group = new THREE.Group(), crystal = new THREE.Mesh(geometry,crystalMaterial);
        crystal.castShadow=false; crystal.position.y=1.9; group.add(crystal);
        const stem = new THREE.Mesh(stemGeometry,stemMaterial);stem.position.y=1.05;group.add(stem);
        const ring = new THREE.Mesh(ringGeometry,ringMaterial);ring.rotation.x=-Math.PI/2;ring.position.y=.07;group.add(ring);
        group.position.set(item.x,0,item.z);scene.add(group);marker={group,crystal,item};beacons.set(item.id,marker);
      }
      marker.item=item;marker.group.visible=connected&&!item.collected;
    }
    for(const [id,marker] of beacons)if(!active.has(id)){scene.remove(marker.group);beacons.delete(id);}
  }
  function setConnected(value){connected=Boolean(value);if(!connected)setPeers([]);for(const marker of beacons.values())marker.group.visible=connected&&!marker.item.collected;}
  function update(dt,time,localPosition){
    for(const {rig,visitor} of peers.values()) {
      destination.set(visitor.position.x,visitor.position.y,visitor.position.z);
      const distance=rig.group.position.distanceTo(destination);
      if(distance>12)rig.group.position.copy(destination);else rig.group.position.lerp(destination,1-Math.exp(-12*dt));
      const delta=visitor.position.yaw-rig.group.rotation.y;
      rig.group.rotation.y+=Math.atan2(Math.sin(delta),Math.cos(delta))*Math.min(1,dt*12);
      rig.group.visible=rig.group.position.distanceToSquared(localPosition)<85*85;
      rig.animate(dt,time,{moving:visitor.moving,onGround:visitor.onGround,emote:visitor.emote});
    }
    for(const {crystal,item} of beacons.values()) {crystal.rotation.y=time*.9;crystal.position.y=1.9+Math.sin(time*2+item.x)*.15;}
  }
  function nearestBeacon(position) {
    if(!connected)return null;
    const candidates=[...beacons.values()].filter(b=>!b.item.collected).map(b=>({id:b.item.id,distance:Math.hypot(position.x-b.item.x,position.y+1-b.item.y,position.z-b.item.z)}));
    candidates.sort((a,b)=>a.distance-b.distance);return candidates[0]||null;
  }
  function minimapPoints(){return [...peers.values()].map(p=>p.visitor.position);}
  function dispose(){setPeers([]);for(const b of beacons.values())scene.remove(b.group);beacons.clear();for(const resource of [geometry,stemGeometry,ringGeometry,crystalMaterial,ringMaterial,stemMaterial])resource.dispose();}
  return {setPeers,setBeacons,setConnected,update,nearestBeacon,minimapPoints,dispose};
}
