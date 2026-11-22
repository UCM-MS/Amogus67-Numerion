import assert from 'node:assert/strict';
import * as THREE from '../public/vendor/three.module.js';
import { createPartyWorld } from '../public/party3d.js';

// The party uses real Three meshes and textures; name labels only need a canvas context.
const priorDocument = globalThis.document;
globalThis.document = { createElement() {
  const canvas = { width: 0, height: 0, getContext() { return {
    beginPath() {}, roundRect() {}, fill() {}, fillText(text) { canvas.drawnName = text; },
  }; } }; return canvas;
} };

const close = (actual, expected, message) => assert.ok(Math.abs(actual - expected) < 1e-8, `${message}: ${actual} versus ${expected}`);
const scene = new THREE.Scene(), campus = new THREE.Group(); campus.name = 'ExistingCampus'; scene.add(campus);
const party = createPartyWorld(scene);
const explorers = () => scene.children.filter(object => object.name === 'Explorer');
const pet = { kind: 'fox', color: '#ffab77', name: 'Koko' };
let visitor = { id: 'friend', name: 'Mira', pet, position: { x: 2, y: 0, z: 4, yaw: Math.PI - .05 }, moving: false, onGround: true, emote: null };
const beacons = [
  { id: 'beacon-1', x: 6, y: 1, z: 12, collected: false },
  { id: 'beacon-2', x: -8, y: 1, z: 22, collected: false },
  { id: 'beacon-3', x: 14, y: 1, z: -3, collected: true },
];
const marker = beacon => scene.children.find(object => object.position.x === beacon.x && object.position.z === beacon.z && object.children.some(child => child.geometry?.type === 'IcosahedronGeometry'));

try {
  party.setBeacons(beacons);
  assert.equal(party.nearestBeacon(new THREE.Vector3(6, 0, 12)), null, 'Offline explorers cannot collect the shared expedition');
  assert.ok(beacons.every(beacon => marker(beacon)?.visible === false));
  party.setConnected(true);
  assert.equal(marker(beacons[0]).visible, true);
  assert.equal(marker(beacons[2]).visible, false, 'An already collected beacon remains hidden');
  assert.deepEqual(party.nearestBeacon(new THREE.Vector3(6, 0, 12)), { id: 'beacon-1', distance: 0 }, 'Beacon proximity uses the same body centre as server validation');
  close(party.nearestBeacon(new THREE.Vector3(6, 2, 12)).distance, 2, 'Airborne proximity retains vertical distance');

  party.setPeers([
    { ...visitor, id: 'me' }, visitor,
    { ...visitor, id: 'disconnected', connected: false }, { id: 'no-position', name: 'Nela' },
  ], 'me');
  assert.equal(explorers().length, 1, 'Only an active remote peer creates an explorer');
  let remote = explorers()[0], label = remote.getObjectByName('ExplorerName');
  assert.ok(label?.isSprite, 'The peer has a readable name label');
  assert.deepEqual(remote.position.toArray(), [2, 0, 4]);
  assert.ok(remote.getObjectByName('Companion-fox'));
  party.update(.1, 1, new THREE.Vector3());
  close(remote.rotation.y, visitor.position.yaw, 'Initial remote heading');

  const oldRemote = remote, oldLabel = label, visualPosition = remote.position.clone(), visualYaw = remote.rotation.y;
  let oldTextureDisposals = 0, oldMaterialDisposals = 0;
  oldLabel.material.map.addEventListener('dispose', () => oldTextureDisposals++);
  oldLabel.material.addEventListener('dispose', () => oldMaterialDisposals++);
  visitor = { ...visitor, name: 'Lucia', position: { ...visitor.position, x: 3 } };
  party.setPeers([visitor], 'me');
  remote = explorers()[0]; label = remote.getObjectByName('ExplorerName');
  assert.notEqual(remote, oldRemote); assert.notEqual(label, oldLabel, 'A changed profile name creates a fresh sprite');
  assert.equal(label.material.map.image.drawnName, 'Lucia');
  assert.equal(oldRemote.parent, null); assert.equal(oldTextureDisposals, 1); assert.equal(oldMaterialDisposals, 1, 'Renaming releases the old unique label resources');
  assert.deepEqual(remote.position.toArray(), visualPosition.toArray()); close(remote.rotation.y, visualYaw, 'Renaming preserves the current visual heading');
  const originalPet = remote.getObjectByName('Companion-fox'); assert.ok(originalPet);

  const walking = { ...visitor, position: { x: 5, y: 1, z: 7, yaw: -Math.PI + .05 }, moving: true, onGround: false, emote: 'wave' };
  party.setPeers([walking], 'me');
  assert.equal(explorers()[0], remote, 'Polling updates the existing rig');
  assert.equal(remote.getObjectByName('Companion-fox'), originalPet, 'Unchanged pets retain their existing model');
  assert.deepEqual(remote.position.toArray(), [2, 0, 4], 'New network positions are interpolated on the frame clock');
  const alpha = 1 - Math.exp(-12 * .05);
  party.update(.05, 2, new THREE.Vector3());
  close(remote.position.x, 2 + 3 * alpha, 'Interpolated x');
  close(remote.position.y, alpha, 'Interpolated airborne height');
  close(remote.position.z, 4 + 3 * alpha, 'Interpolated z');
  close(remote.rotation.y, Math.PI + .01, 'Heading follows the short route across the angular boundary');
  let waving = false; remote.traverse(object => { if (object.isGroup && object.rotation.x < -2.1) waving = true; });
  assert.ok(waving, 'The authoritative wave produces a visible arm gesture');
  assert.deepEqual(party.minimapPoints(), [walking.position], 'The map uses current authoritative peer destinations');

  party.setPeers([{ ...walking, pet: { kind: 'owl', color: '#ffd052', name: 'Mia' } }], 'me');
  assert.equal(originalPet.parent, null, 'Equipping a different pet removes the old companion');
  assert.ok(remote.getObjectByName('Companion-owl'));
  assert.equal(remote.getObjectByName('Companion-fox'), undefined);
  party.setPeers([{ ...walking, pet: null }], 'me');
  assert.equal(remote.children.some(child => child.name.startsWith('Companion-')), false, 'Unequipping removes the remote companion');

  const teleported = { ...walking, position: { x: 40, y: 0, z: 40, yaw: 0 } };
  party.setPeers([teleported], 'me'); party.update(1 / 60, 3, new THREE.Vector3(40, 0, 40));
  assert.deepEqual(remote.position.toArray(), [40, 0, 40], 'A server teleport snaps rather than sliding across the campus');
  assert.equal(remote.visible, true);
  party.update(1 / 60, 3.1, new THREE.Vector3(-48, 0, -48)); assert.equal(remote.visible, false, 'Very distant peers respect the rendering budget');

  const claimed = beacons.map((beacon, index) => ({ ...beacon, collected: index === 0 || beacon.collected }));
  party.setBeacons(claimed);
  assert.equal(marker(beacons[0]).visible, false, 'A collected shared beacon disappears immediately');
  assert.equal(party.nearestBeacon(new THREE.Vector3(6, 0, 12)).id, 'beacon-2', 'Collected beacons are excluded from navigation');
  party.setBeacons(claimed.slice(0, 2)); assert.equal(marker(beacons[2]), undefined, 'Obsolete world markers leave the scene');

  let textureDisposals = 0, materialDisposals = 0, sharedGeometryDisposals = 0;
  label.material.map.addEventListener('dispose', () => textureDisposals++);
  label.material.addEventListener('dispose', () => materialDisposals++);
  remote.traverse(object => object.geometry?.addEventListener('dispose', () => sharedGeometryDisposals++));
  const beaconResources = new Set();
  marker(beacons[0]).traverse(object => { if (object.geometry) beaconResources.add(object.geometry); if (object.material) beaconResources.add(object.material); });
  const disposalCounts = new Map([...beaconResources].map(resource => [resource, 0]));
  for (const resource of beaconResources) resource.addEventListener('dispose', () => disposalCounts.set(resource, disposalCounts.get(resource) + 1));
  party.setConnected(false);
  assert.equal(explorers().length, 0); assert.deepEqual(party.minimapPoints(), []);
  assert.equal(remote.parent, null);
  assert.equal(textureDisposals, 1); assert.equal(materialDisposals, 1, 'Disconnect releases the unique name texture and material');
  assert.equal(sharedGeometryDisposals, 0, 'Disconnect leaves avatar buffers intact for other explorers');
  assert.equal(marker(beacons[1]).visible, false); assert.equal(party.nearestBeacon(new THREE.Vector3()), null);
  party.dispose();
  assert.deepEqual(scene.children, [campus], 'Disposal removes only party objects and preserves the campus');
  assert.equal(beaconResources.size, 6); assert.ok([...disposalCounts.values()].every(count => count === 1), 'Shared beacon GPU resources are released once');
  assert.equal(textureDisposals, 1); assert.equal(materialDisposals, 1, 'Party disposal does not double-dispose former peer labels');
  console.log('Party 3D: peer identity, pet changes, wave, interpolation, heading wrap, teleport, beacon proximity, collection and shared-resource disposal passed.');
} finally {
  if (priorDocument === undefined) delete globalThis.document; else globalThis.document = priorDocument;
}
