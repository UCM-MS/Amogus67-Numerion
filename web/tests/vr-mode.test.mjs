import assert from 'node:assert/strict';
import * as THREE from '../public/vendor/three.module.js';
import { createVRMode, createVRInputState, readVRInput } from '../public/vr-mode.js';

const near = (a, b, label) => assert.ok(Math.abs(a - b) < 1e-8, `${label}: ${a} != ${b}`);
const source = hand => ({ handedness: hand, targetRayMode: 'tracked-pointer', gamepad: { axes: [0, 0, 0, 0], buttons: Array.from({ length: 6 }, () => ({ pressed: false, value: 0 })) } });
const left = source('left'), right = source('right');
const inputState = createVRInputState();
left.gamepad.axes = [0, 0, .1, -.1];
assert.deepEqual(readVRInput([left], inputState), { x: 0, z: 0, snap: 0, jump: false, menu: false }, 'Stick drift stays inside the radial dead zone');
left.gamepad.axes = [0, 0, 1, -1];
let input = readVRInput([left], inputState);
near(Math.hypot(input.x, input.z), 1, 'Diagonal movement has unit speed');
assert.ok(input.x > 0 && input.z < 0);
right.gamepad.axes[2] = .8;
input = readVRInput([right], inputState);
near(input.snap, -Math.PI / 6, 'Right stick turns right by thirty degrees');
assert.equal(readVRInput([right], inputState).snap, 0, 'Holding a turn never spins repeatedly');
right.gamepad.axes[2] = 0; readVRInput([right], inputState); right.gamepad.axes[2] = -.8;
near(readVRInput([right], inputState).snap, Math.PI / 6, 'Returning to centre rearms a turn');
right.gamepad.buttons[4].pressed = true; right.gamepad.buttons[5].value = 1;
input = readVRInput([right], inputState);
assert.equal(input.jump, true); assert.equal(input.menu, true);
input = readVRInput([right], inputState); assert.equal(input.jump, false); assert.equal(input.menu, false, 'Buttons react only on press edges');
right.gamepad.buttons[4].pressed = false; right.gamepad.buttons[5].value = 0; readVRInput([right], inputState);
right.gamepad.buttons[4].pressed = true;
input = readVRInput([left, right], inputState, true);
assert.equal(input.jump, false); assert.equal(input.x, 0); assert.equal(input.z, 0, 'A menu blocks movement and jumping');
assert.equal(readVRInput([right], inputState).jump, false, 'A held jump cannot fire when the menu closes');
assert.deepEqual(readVRInput([{ handedness: 'left', gamepad: { axes: [Infinity, NaN] } }], createVRInputState()), { x: 0, z: 0, snap: 0, jump: false, menu: false }, 'Malformed axes cannot inject a non-finite position');

class MockSession extends THREE.EventDispatcher {
  constructor({ floor = true } = {}) { super(); this.floor = floor; this.inputSources = []; this.ended = false; this.endCalls = 0; this.visibilityState = 'visible'; this.referenceRequests = []; }
  async requestReferenceSpace(type) { this.referenceRequests.push(type); if (type === 'local-floor' && !this.floor) throw new Error('No floor tracking'); return { type }; }
  async end() { this.endCalls++; if (!this.ended) { this.ended = true; this.dispatchEvent({ type: 'end' }); } }
}

function fixture({ floor = true, failSetup = false, requestError = null, delayedRequest = false, secureContext = true, supported = true } = {}) {
  const scene = new THREE.Scene(), desktop = new THREE.Group(), camera = new THREE.PerspectiveCamera(55, 1.7, .1, 220);
  desktop.position.set(4, 0, -3); scene.add(desktop); desktop.add(camera);
  camera.position.set(2, 6, 20); camera.rotation.set(-.2, .4, 0); scene.updateMatrixWorld(true);
  const originalPosition = camera.position.clone(), originalQuaternion = camera.quaternion.clone(), projection = camera.projectionMatrix.clone();
  const controllers = [new THREE.Group(), new THREE.Group()], grips = [new THREE.Group(), new THREE.Group()];
  const xrCamera = new THREE.ArrayCamera(), trackingPosition = new THREE.Vector3(.4, 1.6, -.2), trackingRotation = new THREE.Quaternion();
  const session = new MockSession({ floor }); let requestedSession = session, setupFailure = failSetup, requestFailure = requestError;
  let resolveRequest, requestCalls = 0, setSessionCalls = 0, scale = 1, referenceType = 'local-floor';
  let ratio = 1.5, size = new THREE.Vector2(1200, 700), menuCalls = 0, jumpCalls = 0, selections = 0;
  const changes = [], statuses = [];
  const xr = {
    enabled: false, cameraAutoUpdate: true, getController: i => controllers[i], getControllerGrip: i => grips[i], getCamera: () => xrCamera,
    setReferenceSpaceType: type => { referenceType = type; }, setFramebufferScaleFactor: value => { scale = value; },
    async setSession(value) {
      setSessionCalls++;
      if (setupFailure) { camera.position.set(100, 100, 100); ratio = 1; size.set(200, 200); throw new Error('XR framebuffer failure'); }
      await value.requestReferenceSpace(referenceType);
    },
    updateCamera(userCamera) {
      // Match Three: local XR pose is raw tracking, matrixWorld also contains the locomotion rig.
      xrCamera.position.copy(trackingPosition); xrCamera.quaternion.copy(trackingRotation); xrCamera.updateMatrix();
      xrCamera.matrixWorld.multiplyMatrices(userCamera.parent.matrixWorld, xrCamera.matrix);
    },
  };
  const renderer = { xr, getPixelRatio: () => ratio, setPixelRatio: value => { ratio = value; }, getSize: target => target.copy(size), setSize: (x, y) => size.set(x, y) };
  const navigatorXR = {
    async isSessionSupported(mode) { assert.equal(mode, 'immersive-vr'); return supported; },
    requestSession(mode, options) {
      requestCalls++; assert.equal(mode, 'immersive-vr'); assert.ok(options.optionalFeatures.includes('local-floor')); assert.equal(options.requiredFeatures, undefined);
      if (requestFailure) return Promise.reject(requestFailure);
      if (delayedRequest) return new Promise(resolve => { resolveRequest = resolve; });
      return Promise.resolve(requestedSession);
    },
  };
  const mode = createVRMode({ renderer, scene, camera, navigatorXR, secureContext, onChange: active => changes.push(active), onStatus: state => statuses.push(state), onSelect: () => selections++, onJump: () => jumpCalls++, onMenu: () => menuCalls++ });
  return { mode, renderer, scene, desktop, camera, session, controllers, grips, trackingPosition, trackingRotation, originalPosition, originalQuaternion, projection, changes, statuses,
    get requestCalls() { return requestCalls; }, get setSessionCalls() { return setSessionCalls; }, get scale() { return scale; }, get referenceType() { return referenceType; },
    get jumpCalls() { return jumpCalls; }, get menuCalls() { return menuCalls; }, get selections() { return selections; }, resolveRequest: () => resolveRequest(session),
    setFailure(value) { setupFailure = value; }, setRequestError(value) { requestFailure = value; }, nextSession() { requestedSession = new MockSession({ floor }); return requestedSession; },
    assertRestored() {
      assert.equal(camera.parent, desktop); assert.deepEqual(camera.position.toArray(), originalPosition.toArray()); assert.deepEqual(camera.quaternion.toArray(), originalQuaternion.toArray());
      assert.deepEqual(camera.projectionMatrix.elements, projection.elements); assert.equal(xr.enabled, false); assert.equal(xr.cameraAutoUpdate, true); assert.equal(mode.rig.parent, null);
      assert.ok([...controllers, ...grips].every(controller => controller.parent === null && !controller.visible));
      assert.equal(ratio, 1.5); assert.deepEqual(size.toArray(), [1200, 700]);
    },
  };
}

const unsupported = fixture({ supported: false });
assert.equal((await unsupported.mode.checkSupport()).supported, false); assert.equal(unsupported.requestCalls, 0, 'Checking support never requests a permission'); unsupported.mode.dispose();
const insecure = fixture({ secureContext: false });
assert.equal(await insecure.mode.start(), false); assert.equal(insecure.requestCalls, 0); insecure.assertRestored(); insecure.mode.dispose();

const f = fixture();
assert.equal((await f.mode.checkSupport()).supported, true);
f.mode.updatePosition(new THREE.Vector3(12, 0, -8));
const firstStart = f.mode.start();
assert.equal(f.requestCalls, 1, 'Session request happens synchronously inside the user gesture');
assert.equal(f.mode.start(), firstStart, 'Double clicking shares the pending session');
assert.equal(await firstStart, true); assert.equal(f.setSessionCalls, 1); assert.equal(f.scale, .85); assert.equal(f.mode.active, true);
assert.equal(f.camera.parent, f.mode.rig); assert.equal(f.mode.rig.parent, f.scene); assert.deepEqual(f.mode.rig.position.toArray(), [12, 0, -8]); assert.deepEqual(f.changes, [true]);
const vrLeft = source('left'), vrRight = source('right'); f.session.inputSources = [vrLeft, vrRight]; vrLeft.gamepad.axes[3] = -1;
f.trackingRotation.setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI / 2);
input = f.mode.getInput(.016);
near(input.yaw, Math.PI / 2, 'Motion uses headset heading'); near(input.z, -1, 'Forward has negative z');
const before = new THREE.Vector3().setFromMatrixPosition(f.mode.getViewCamera().matrixWorld);
vrRight.gamepad.axes[2] = .8; input = f.mode.getInput(.016);
near(input.yaw, Math.PI / 3, 'Snap turn is composed with headset yaw');
const after = new THREE.Vector3().setFromMatrixPosition(f.mode.getViewCamera().matrixWorld);
near(before.x, after.x, 'Snap rotates around the head, preserving eye x'); near(before.z, after.z, 'Snap preserves eye z');
f.mode.updatePosition(new THREE.Vector3(13, 0, -8));
near(new THREE.Vector3().setFromMatrixPosition(f.mode.getViewCamera().matrixWorld).x - after.x, 1, 'Following player feet preserves the snap anchor offset');
f.controllers[0].dispatchEvent({ type: 'connected', data: vrLeft });
assert.equal(f.controllers[0].getObjectByName('LumenVRRay').visible, true);
f.controllers[0].dispatchEvent({ type: 'select' }); f.controllers[1].dispatchEvent({ type: 'squeeze' });
assert.equal(f.selections, 1); assert.equal(f.menuCalls, 1);
vrRight.gamepad.buttons[4].pressed = true; f.mode.getInput(.016); f.mode.getInput(.016); assert.equal(f.jumpCalls, 1);
f.session.visibilityState = 'visible-blurred'; f.session.dispatchEvent({ type: 'visibilitychange' }); vrRight.gamepad.buttons[5].pressed = true;
assert.equal(f.mode.suspended, true);
input = f.mode.getInput(.016); assert.equal(input.x, 0); assert.equal(input.z, 0); near(input.yaw, Math.PI / 3, 'Overlays preserve the explorer heading');
f.controllers[0].dispatchEvent({ type: 'select' }); f.controllers[1].dispatchEvent({ type: 'squeeze' }); assert.equal(f.selections, 1); assert.equal(f.menuCalls, 1, 'System overlays suppress game actions');
f.session.visibilityState = 'visible'; f.session.dispatchEvent({ type: 'visibilitychange' }); f.mode.getInput(.016); assert.equal(f.mode.suspended, false); assert.equal(f.jumpCalls, 1); assert.equal(f.menuCalls, 1, 'Held overlay buttons do not fire on resume');
vrRight.gamepad.buttons[4].pressed = false; vrRight.gamepad.buttons[5].pressed = false; f.mode.getInput(.016);
vrRight.gamepad.buttons[5].pressed = true; f.mode.getInput(.016); assert.equal(f.menuCalls, 2);
f.controllers[0].visible = true; f.grips[0].visible = true; f.controllers[0].dispatchEvent({ type: 'disconnected' });
assert.equal(f.controllers[0].visible, false); assert.equal(f.grips[0].visible, false); assert.equal(f.controllers[0].getObjectByName('LumenVRRay').visible, false);
await f.session.end(); await Promise.resolve();
assert.equal(f.mode.active, false); assert.deepEqual(f.changes, [true, false]); f.assertRestored();
let geometryDisposals = 0; f.controllers[0].getObjectByName('LumenVRRay').geometry.addEventListener('dispose', () => geometryDisposals++);
f.mode.dispose(); f.mode.dispose(); assert.equal(geometryDisposals, 1, 'Disposal releases shared GPU geometry exactly once'); assert.equal(f.controllers[0].getObjectByName('LumenVRRay'), undefined);

const seated = fixture({ floor: false });
seated.mode.updatePosition(new THREE.Vector3(1, 2, 3)); assert.equal(await seated.mode.start(), true);
assert.equal(seated.referenceType, 'local'); near(seated.mode.rig.position.y, 3.65, 'Local tracking receives a standing eye-height offset');
assert.ok(seated.session.referenceRequests.includes('local-floor')); assert.ok(seated.session.referenceRequests.includes('local'));
await seated.mode.end(); seated.assertRestored(); seated.mode.dispose();

const rejected = fixture({ requestError: Object.assign(new Error('Rejected'), { name: 'NotAllowedError' }) });
assert.equal(await rejected.mode.start(), false); assert.equal(rejected.mode.pending, false); assert.match(rejected.mode.status.message, /nebolo povolené/); rejected.assertRestored();
rejected.setRequestError(null); assert.equal(await rejected.mode.start(), true, 'A rejected permission does not prevent a later user-approved session'); await rejected.mode.end(); rejected.assertRestored(); rejected.mode.dispose();
const broken = fixture({ failSetup: true });
assert.equal(await broken.mode.start(), false); assert.equal(broken.session.ended, true); assert.equal(broken.mode.active, false); broken.assertRestored();
broken.setFailure(false); const retrySession = broken.nextSession();
assert.equal(await broken.mode.start(), true, 'XR can be reopened after framebuffer setup failed');
broken.session.dispatchEvent({ type: 'end' }); await Promise.resolve();
assert.equal(broken.mode.active, true, 'A stale end event from the failed session cannot close its replacement');
assert.equal(retrySession.ended, false); await broken.mode.end(); broken.assertRestored(); broken.mode.dispose();

const cancelled = fixture({ delayedRequest: true });
const opening = cancelled.mode.start(); await cancelled.mode.end(); cancelled.resolveRequest();
assert.equal(await opening, false); assert.equal(cancelled.session.ended, true); assert.equal(cancelled.setSessionCalls, 0); assert.deepEqual(cancelled.changes, []); cancelled.assertRestored(); cancelled.mode.dispose();

console.log('VR runtime: input, headset yaw, pivot snap turns, overlays, support, permission denial, session lifecycle, floor fallback and cleanup passed.');
