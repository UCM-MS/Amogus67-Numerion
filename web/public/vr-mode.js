import * as THREE from './vendor/three.module.js';

const SNAP_ANGLE = Math.PI / 6;
const finiteAxis = value => Number.isFinite(value) ? Math.max(-1, Math.min(1, value)) : 0;
const pressed = button => Boolean(button?.pressed || button?.value > .5);

/** Controller state is kept per input source, so reconnecting a hand never inherits a held button. */
export function createVRInputState() { return { hands: new WeakMap() }; }

export function readVRInput(sources, state, blocked = false) {
  let x = 0, z = 0, snap = 0, jump = false, menu = false;
  for (const source of sources || []) {
    if (!source?.gamepad) continue;
    let hand = state.hands.get(source);
    if (!hand) { hand = { turnArmed: true, jump: false, menu: false }; state.hands.set(source, hand); }
    const { axes = [], buttons = [] } = source.gamepad;
    const index = axes.length >= 4 ? 2 : 0;
    const horizontal = finiteAxis(axes[index]), vertical = finiteAxis(axes[index + 1]);
    if (source.handedness === 'left' && !blocked) {
      const length = Math.hypot(horizontal, vertical);
      if (length > .2) {
        const strength = Math.min(1, (length - .2) / .8);
        x = horizontal / length * strength; z = vertical / length * strength;
      }
    }
    if (source.handedness === 'right') {
      if (Math.abs(horizontal) < .25) hand.turnArmed = true;
      if (Math.abs(horizontal) > .65 && hand.turnArmed) {
        // A positive stick turns the view right. Three's positive yaw turns left.
        if (!blocked) snap = -Math.sign(horizontal) * SNAP_ANGLE;
        hand.turnArmed = false;
      }
      const jumpNow = pressed(buttons[4]), menuNow = pressed(buttons[5]);
      jump ||= jumpNow && !hand.jump && !blocked;
      menu ||= menuNow && !hand.menu;
      hand.jump = jumpNow; hand.menu = menuNow;
    }
  }
  return { x, z, snap, jump, menu };
}

/** Real immersive WebXR, with the desktop camera and render loop restored on every exit path. */
export function createVRMode({
  renderer, scene, camera, onSelect = () => {}, onMenu = () => {}, onJump = () => {},
  onChange = () => {}, onStatus = () => {}, navigatorXR = globalThis.navigator?.xr,
  secureContext = globalThis.isSecureContext === true,
}) {
  const xr = renderer.xr, rig = new THREE.Group(); rig.name = 'LumenVRRig';
  const controllers = [xr.getController(0), xr.getController(1)];
  const grips = [xr.getControllerGrip(0), xr.getControllerGrip(1)];
  const sourceListeners = [], resources = []; let inputState = createVRInputState(), visibilitySuspended = false;
  const anchorOffset = new THREE.Vector3(), feet = new THREE.Vector3(), direction = new THREE.Vector3();
  const headBeforeTurn = new THREE.Vector3(), headAfterTurn = new THREE.Vector3();
  let active = false, starting = false, disposed = false, cancelled = false, session = null;
  let pending = null, supportPending = null, saved = null, sessionEnded = false, localHeight = 0, lastYaw = 0;
  let status = { supported: false, checking: false, message: 'Overujem dostupnosť VR headsetu…' };

  const notify = (message, additions = {}) => {
    status = { ...status, ...additions, message };
    onStatus({ ...status, active, pending: starting });
    return { ...status, active, pending: starting };
  };
  const listen = (object, type, listener) => {
    object.addEventListener(type, listener); sourceListeners.push([object, type, listener]);
  };
  const wandGeometry = new THREE.CylinderGeometry(.022, .031, .16, 6);
  const tipGeometry = new THREE.SphereGeometry(.035, 6, 4);
  const rayGeometry = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3(0, 0, -5)]);
  resources.push(wandGeometry, tipGeometry, rayGeometry);
  controllers.forEach((controller, index) => {
    controller.name = `LumenVRPointer-${index}`;
    const color = index === 0 ? 0xbca9ff : 0xd6fb92;
    const wandMaterial = new THREE.MeshBasicMaterial({ color: 0x253847 });
    const glowMaterial = new THREE.MeshBasicMaterial({ color, toneMapped: false });
    const rayMaterial = new THREE.LineBasicMaterial({ color, transparent: true, opacity: .72, toneMapped: false });
    resources.push(wandMaterial, glowMaterial, rayMaterial);
    const wand = new THREE.Mesh(wandGeometry, wandMaterial), tip = new THREE.Mesh(tipGeometry, glowMaterial);
    wand.rotation.x = Math.PI / 2; wand.position.z = .035; tip.position.z = -.06;
    const visual = new THREE.Group(); visual.name = 'LumenVRController'; visual.add(wand, tip); visual.visible = false;
    const ray = new THREE.Line(rayGeometry, rayMaterial); ray.name = 'LumenVRRay'; ray.visible = false;
    controller.add(ray); grips[index].add(visual);
    controller.visible = false; grips[index].visible = false;
    const connected = event => {
      controller.userData.inputSource = event.data;
      const tracked = event.data?.targetRayMode === 'tracked-pointer';
      ray.visible = tracked; visual.visible = tracked;
      // The WebXRManager sets parent visibility from valid poses on each frame, never from connection alone.
    };
    const disconnected = () => {
      delete controller.userData.inputSource;
      ray.visible = false; visual.visible = false; controller.visible = false; grips[index].visible = false;
    };
    listen(controller, 'connected', connected); listen(controller, 'disconnected', disconnected);
    listen(controller, 'select', () => { if (active && sessionVisible()) onSelect(controller); });
    listen(controller, 'squeeze', () => { if (active && sessionVisible()) onMenu(); });
  });

  function getViewCamera() {
    if (!active) return camera;
    // The renderer normally updates this during render; menus and movement need the current head pose beforehand.
    rig.updateMatrixWorld(true); xr.updateCamera?.(camera);
    return xr.getCamera();
  }

  function sessionVisible() { return !session?.visibilityState || session.visibilityState === 'visible'; }

  function restore() {
    const wasActive = active; active = false;
    if (saved) {
      saved.parent ? saved.parent.add(camera) : camera.removeFromParent();
      camera.position.copy(saved.position); camera.quaternion.copy(saved.quaternion); camera.scale.copy(saved.scale);
      camera.fov = saved.fov; camera.zoom = saved.zoom;
      camera.projectionMatrix.copy(saved.projectionMatrix); camera.projectionMatrixInverse.copy(saved.projectionMatrixInverse);
      camera.updateMatrixWorld(true);
      [...controllers, ...grips].forEach((object, index) => {
        const parent = saved.controllerParents[index]; parent ? parent.add(object) : object.removeFromParent();
        object.visible = false;
      });
      xr.enabled = saved.enabled; xr.cameraAutoUpdate = saved.cameraAutoUpdate;
      xr.setReferenceSpaceType('local-floor'); xr.setFramebufferScaleFactor(1);
      if (saved.pixelRatio !== undefined) renderer.setPixelRatio?.(saved.pixelRatio);
      if (saved.size) renderer.setSize?.(saved.size.x, saved.size.y, false);
      saved = null;
    }
    rig.removeFromParent(); rig.rotation.set(0, 0, 0); anchorOffset.set(0, 0, 0); localHeight = 0;
    inputState = createVRInputState(); visibilitySuspended = false; lastYaw = 0;
    if (wasActive) onChange(false);
  }

  function handleVisibilityChange() { if (!sessionVisible()) visibilitySuspended = true; }

  function handleSessionEnd(event) {
    const endedSession = event?.target || session;
    if (!endedSession || session !== endedSession) return;
    sessionEnded = true;
    // Three restores framebuffer state in its own end listener. Run after that listener, including on setup failure.
    queueMicrotask(() => {
      if (!sessionEnded || session !== endedSession) return;
      endedSession.removeEventListener('end', handleSessionEnd);
      endedSession.removeEventListener('visibilitychange', handleVisibilityChange); session = null;
      restore();
      notify('VR je ukončené. Pokračuješ v prehliadači.');
    });
  }

  function checkSupport() {
    if (supportPending) return supportPending;
    if (!secureContext || !navigatorXR?.isSessionSupported) {
      return Promise.resolve(notify(!secureContext
        ? 'VR potrebuje bezpečné HTTPS pripojenie. Otvor zverejnenú hru v headsete.'
        : 'Tento prehliadač nemá WebXR. Otvor hru v prehliadači kompatibilného VR headsetu.',
      { supported: false, checking: false }));
    }
    notify('Overujem dostupnosť VR headsetu…', { checking: true });
    supportPending = Promise.resolve().then(() => navigatorXR.isSessionSupported('immersive-vr'))
      .then(supported => notify(active ? 'VR je aktívne.' : supported
        ? 'Headset je pripravený. Nasaď si ho a spusti VR.'
        : 'VR headset nie je dostupný. Otvor hru priamo v jeho WebXR prehliadači.',
      { supported: Boolean(supported), checking: false }))
      .catch(() => notify('Dostupnosť headsetu sa nepodarilo overiť. Skontroluj pripojenie headsetu.', { supported: false, checking: false }))
      .finally(() => { supportPending = null; });
    return supportPending;
  }

  function start() {
    if (active) return Promise.resolve(true);
    if (pending) return pending;
    if (disposed || !secureContext || !navigatorXR?.requestSession) {
      notify('VR potrebuje WebXR prehliadač headsetu a bezpečné HTTPS pripojenie.');
      return Promise.resolve(false);
    }
    cancelled = false; starting = true; sessionEnded = false;
    notify('Čakám na povolenie VR headsetu…');
    let requested;
    try {
      // Do not await support detection here: requestSession must retain the button's user activation.
      requested = navigatorXR.requestSession('immersive-vr', { optionalFeatures: ['local-floor', 'bounded-floor'] });
    } catch (error) { requested = Promise.reject(error); }
    pending = Promise.resolve(requested).then(async newSession => {
      session = newSession; session.addEventListener('end', handleSessionEnd);
      session.addEventListener('visibilitychange', handleVisibilityChange);
      if (cancelled || disposed) { await session.end(); return false; }
      let floor = true;
      try { await session.requestReferenceSpace('local-floor'); }
      catch { floor = false; await session.requestReferenceSpace('local'); }
      if (cancelled || disposed || sessionEnded) { await session?.end(); return false; }
      const size = renderer.getSize?.(new THREE.Vector2());
      saved = {
        parent: camera.parent, position: camera.position.clone(), quaternion: camera.quaternion.clone(), scale: camera.scale.clone(),
        fov: camera.fov, zoom: camera.zoom, projectionMatrix: camera.projectionMatrix.clone(), projectionMatrixInverse: camera.projectionMatrixInverse.clone(),
        enabled: xr.enabled, cameraAutoUpdate: xr.cameraAutoUpdate, pixelRatio: renderer.getPixelRatio?.(), size,
        controllerParents: [...controllers, ...grips].map(object => object.parent),
      };
      localHeight = floor ? 0 : 1.65;
      rig.position.copy(feet); rig.position.y += localHeight;
      rig.rotation.set(0, 0, 0); anchorOffset.set(0, 0, 0); scene.add(rig);
      rig.add(camera, ...controllers, ...grips); camera.position.set(0, 0, 0); camera.quaternion.identity();
      xr.setReferenceSpaceType(floor ? 'local-floor' : 'local'); xr.setFramebufferScaleFactor(.85);
      xr.enabled = true; xr.cameraAutoUpdate = true;
      await xr.setSession(session);
      if (cancelled || disposed || sessionEnded) { await session?.end(); restore(); return false; }
      active = true; onChange(true);
      notify('VR je aktívne. Ľavá páčka pohyb, pravá otočenie, spúšť výber.', { supported: true, checking: false });
      return true;
    }).catch(async error => {
      const failedSession = session;
      if (failedSession && !sessionEnded) {
        try { await failedSession.end(); } catch { /* Restore the desktop even if an unavailable headset cannot end cleanly. */ }
      }
      restore(); session?.removeEventListener('end', handleSessionEnd);
      session?.removeEventListener('visibilitychange', handleVisibilityChange); session = null;
      notify(error?.name === 'NotAllowedError' || error?.name === 'SecurityError'
        ? 'Spustenie VR nebolo povolené. Skús ho znova cez tlačidlo Spustiť VR.'
        : 'VR sa nepodarilo spustiť. Skontroluj headset a jeho WebXR prehliadač.', { checking: false });
      return false;
    }).finally(() => { starting = false; pending = null; onStatus({ ...status, active, pending: false }); });
    return pending;
  }

  async function end() {
    cancelled = true;
    const endingSession = session;
    if (endingSession) {
      try { await endingSession.end(); }
      catch { /* A disconnected headset may already have ended the session. */ }
      endingSession.removeEventListener('end', handleSessionEnd);
      endingSession.removeEventListener('visibilitychange', handleVisibilityChange);
      if (session === endingSession) session = null;
      restore(); notify('VR je ukončené. Pokračuješ v prehliadači.');
    }
    return true;
  }

  function getInput(_dt, blocked = false) {
    if (!active) return { x: 0, z: 0, yaw: 0 };
    if (!sessionVisible()) {
      visibilitySuspended = true;
      return { x: 0, z: 0, yaw: lastYaw };
    }
    if (visibilitySuspended) {
      inputState = createVRInputState();
      // Prime held edges, without acting on buttons that were pressed in a system overlay.
      readVRInput(session?.inputSources, inputState, true); visibilitySuspended = false;
    }
    const input = readVRInput(session?.inputSources, inputState, blocked);
    const view = getViewCamera();
    if (input.snap) {
      headBeforeTurn.setFromMatrixPosition(view.matrixWorld);
      rig.rotation.y += input.snap; rig.updateMatrixWorld(true);
      headAfterTurn.setFromMatrixPosition(getViewCamera().matrixWorld);
      anchorOffset.x += headBeforeTurn.x - headAfterTurn.x;
      anchorOffset.z += headBeforeTurn.z - headAfterTurn.z;
      updatePosition(feet);
    }
    if (input.menu) onMenu();
    if (input.jump) onJump();
    direction.setFromMatrixColumn(getViewCamera().matrixWorld, 2).negate().normalize();
    if (Math.hypot(direction.x, direction.z) > .001) lastYaw = Math.atan2(-direction.x, -direction.z);
    return { x: input.x, z: input.z, yaw: lastYaw };
  }

  function updatePosition(position) {
    feet.copy(position);
    if (!active && !saved) return;
    rig.position.copy(feet).add(anchorOffset); rig.position.y += localHeight;
    rig.updateMatrixWorld(true);
  }

  function dispose() {
    if (disposed) return;
    disposed = true; void end();
    for (const [object, type, listener] of sourceListeners) object.removeEventListener(type, listener);
    controllers.forEach(controller => {
      const ray = controller.getObjectByName('LumenVRRay'); ray?.removeFromParent();
    });
    grips.forEach(grip => grip.getObjectByName('LumenVRController')?.removeFromParent());
    for (const resource of resources) resource.dispose();
  }

  return {
    checkSupport, start, end, getInput, getViewCamera, updatePosition, dispose, rig, controllers,
    get active() { return active; }, get pending() { return starting; }, get status() { return { ...status, active, pending: starting }; },
    get suspended() { return active && !sessionVisible(); },
  };
}
