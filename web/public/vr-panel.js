import * as THREE from './vendor/three.module.js';

const INLINE = new Set(['SPAN', 'STRONG', 'B', 'EM', 'I', 'SMALL', 'LABEL', 'A', 'CODE', 'SUP', 'SUB']);
const IGNORE = new Set(['SVG', 'PATH', 'STYLE', 'SCRIPT', 'IMG', 'CANVAS']);
const normalize = value => String(value ?? '').replace(/\s+/g, ' ').trim();

// DOM visibility is checked within the modal. The desktop HUD may be hidden
// during an immersive session while its modal remains our interaction model.
export function isVRVisible(node, root, getStyle) {
  for (let element = node; element; element = element.parentElement) {
    if (element.hidden || element.getAttribute?.('aria-hidden') === 'true') return false;
    const style = element.style;
    if (style?.display === 'none' || style?.visibility === 'hidden') return false;
    const computed = getStyle?.(element);
    if (computed?.display === 'none' || computed?.visibility === 'hidden') return false;
    if (element === root) break;
  }
  return true;
}

function visibleText(node, root, getStyle) {
  if (node.nodeType === 3) return node.textContent || '';
  if (IGNORE.has(node.tagName) || !isVRVisible(node, root, getStyle)) return '';
  return [...(node.childNodes || [])].map(child => visibleText(child, root, getStyle)).join(' ');
}

export function extractVRModal(root, { getStyle } = {}) {
  const content = { title: '', question: '', entries: [], hasForms: false };
  if (!root) return content;
  const walk = (node, inheritedPriority = 4) => {
    if (node.nodeType !== 1 || IGNORE.has(node.tagName) || !isVRVisible(node, root, getStyle)) return;
    const priority = ['mp-feedback', 'answer-feedback'].some(name => node.classList?.contains(name)) ? 2 : inheritedPriority;
    if (node.tagName === 'BUTTON') {
      if (node.classList?.contains('modal-close') || node.getAttribute?.('data-action') === 'close') return;
      const baseText = normalize(visibleText(node, root, getStyle)) || normalize(node.getAttribute?.('aria-label'));
      const selected = node.getAttribute?.('aria-pressed') === 'true' || node.classList?.contains('selected');
      const correct = node.classList?.contains('correct'), wrong = node.classList?.contains('wrong');
      const text = `${correct ? '✓ Správne · ' : wrong ? 'Skús sa poučiť · ' : selected ? '✓ ' : ''}${baseText}`;
      const answerPriority = node.getAttribute?.('data-answer') !== null && node.getAttribute?.('data-answer') !== undefined || node.getAttribute?.('data-mp-answer') !== null && node.getAttribute?.('data-mp-answer') !== undefined;
      const movePriority = node.getAttribute?.('data-mp-move') !== null && node.getAttribute?.('data-mp-move') !== undefined;
      if (baseText) content.entries.push({ kind: 'button', text, baseText, node, disabled: Boolean(node.disabled || node.matches?.(':disabled')) || node.getAttribute?.('aria-disabled') === 'true', priority: answerPriority ? 1 : movePriority ? 3 : priority });
      return;
    }
    if (['question', 'mp-question', 'quiz-prompt'].some(name => node.classList?.contains(name))) {
      const text = normalize(visibleText(node, root, getStyle));
      content.question = text;
      if (text) content.entries.push({ kind: 'text', text, priority: 0 });
      return;
    }
    if (['INPUT', 'TEXTAREA', 'SELECT'].includes(node.tagName)) {
      if (node.getAttribute?.('type') !== 'hidden') content.hasForms = true;
      return;
    }
    if (/^H[1-6]$/.test(node.tagName)) {
      const text = normalize(visibleText(node, root, getStyle));
      if (!content.title && ['H1', 'H2'].includes(node.tagName)) content.title = text;
      else if (text) content.entries.push({ kind: 'heading', text, priority });
      return;
    }
    let parts = [];
    const flush = () => {
      const text = normalize(parts.join(' '));
      if (text) content.entries.push({ kind: 'text', text, priority });
      parts = [];
    };
    for (const child of node.childNodes || []) {
      if (child.nodeType === 3) parts.push(child.textContent || '');
      else if (INLINE.has(child.tagName) && !child.querySelector?.('button,input,textarea,select')) parts.push(visibleText(child, root, getStyle));
      else { flush(); walk(child, priority); }
    }
    flush();
  };
  walk(root);
  if (content.question) content.entries.sort((a, b) => a.priority - b.priority);
  return content;
}

export function wrapVRText(text, columns = 56) {
  const lines = [], words = normalize(text).split(' ').filter(Boolean);
  let line = '';
  for (let word of words) {
    if (word.length > columns) {
      if (line) { lines.push(line); line = ''; }
      while (word.length > columns) { lines.push(word.slice(0, columns)); word = word.slice(columns); }
    }
    if (!word) continue;
    if (line && line.length + word.length + 1 > columns) { lines.push(line); line = ''; }
    line += `${line ? ' ' : ''}${word}`;
  }
  if (line) lines.push(line);
  return lines;
}

// Long lesson explanations are split across pages rather than clipped. Button
// references survive pagination so answers invoke the existing game handlers.
export function paginateVRContent(entries, { columns = 56, maxUnits = 18 } = {}) {
  const pages = []; let page = [], units = 0;
  const push = entry => {
    if (page.length && units + entry.units > maxUnits) { pages.push(page); page = []; units = 0; }
    page.push(entry); units += entry.units;
  };
  for (const entry of entries) {
    const lines = wrapVRText(entry.text, entry.kind === 'button' ? 44 : columns);
    if (entry.kind === 'button') {
      const maxLines = Math.max(1, Math.floor((maxUnits * 39 - 39) / 38));
      for (let offset = 0; offset < lines.length; offset += maxLines) {
        const chunk = lines.slice(offset, offset + maxLines);
        const height = Math.max(76, chunk.length * 38 + 24);
        push({ ...entry, lines: chunk, units: (height + 15) / 39 });
      }
    } else {
      const limit = Math.max(1, Math.floor(maxUnits - .7));
      for (let offset = 0; offset < lines.length; offset += limit) {
        const chunk = lines.slice(offset, offset + limit);
        push({ ...entry, text: chunk.join(' '), lines: chunk, units: chunk.length + .5 });
      }
    }
  }
  if (page.length) pages.push(page);
  return pages.length ? pages : [[]];
}

export function createVRPanel({ scene, document: doc = globalThis.document, onInteract = () => {}, onPanel = () => {}, onCloseModal = () => {}, onExit = () => {}, onJump = () => {}, getStatus = () => ({}) } = {}) {
  const canvas = doc.createElement('canvas'); canvas.width = 1024; canvas.height = 320;
  const ctx = canvas.getContext('2d');
  const createTexture = () => {
    const next = new THREE.CanvasTexture(canvas);
    next.colorSpace = THREE.SRGBColorSpace;
    next.generateMipmaps = false; next.minFilter = THREE.LinearFilter; next.magFilter = THREE.LinearFilter;
    return next;
  };
  let texture = createTexture();
  const material = new THREE.MeshBasicMaterial({ map: texture, transparent: true, depthTest: false, depthWrite: false, toneMapped: false, side: THREE.DoubleSide });
  const geometry = new THREE.PlaneGeometry(1, 1), mesh = new THREE.Mesh(geometry, material);
  mesh.name = 'LumenVRPanel'; mesh.visible = false; mesh.renderOrder = 1000; mesh.frustumCulled = false; scene.add(mesh);
  const raycaster = new THREE.Raycaster(), eye = new THREE.Vector3(), forward = new THREE.Vector3(), desired = new THREE.Vector3(), controllerOrigin = new THREE.Vector3(), quaternion = new THREE.Quaternion(), up = new THREE.Vector3(0, 1, 0);
  const root = doc.querySelector('#modal-root'), style = doc.defaultView?.getComputedStyle?.bind(doc.defaultView);
  let active = false, menu = false, mode = '', dirty = true, recenter = true, disposed = false, elapsed = .25, pageIndex = 0, pages = [[]], hits = [], signature = '', modalTitle = '', lastStatusSignature = '';
  const Observer = doc.defaultView?.MutationObserver || globalThis.MutationObserver;
  const observer = Observer && root ? new Observer(() => { dirty = true; }) : null;
  observer?.observe(root, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ['hidden', 'disabled', 'aria-disabled', 'aria-hidden', 'aria-pressed', 'class', 'style'] });
  const color = { ink: '#30314c', muted: '#73758b', purple: '#8974dd', cream: '#fffdf4', lime: '#d8f581', pale: '#eee9ff', border: '#e4dfef' };
  const rect = (x, y, width, height, fill, radius = 22) => { ctx.fillStyle = fill; ctx.beginPath(); ctx.roundRect(x, y, width, height, radius); ctx.fill(); };
  const label = (text, x, y, size = 30, fill = color.ink, weight = '500', width) => { ctx.font = `${weight} ${size}px system-ui, sans-serif`; ctx.fillStyle = fill; ctx.fillText(text, x, y, width); };
  const button = (text, x, y, width, height, action, { disabled = false, primary = false, lines = wrapVRText(text, Math.max(12, Math.floor(width / 18))) } = {}) => {
    rect(x, y, width, height, disabled ? '#f0eef2' : primary ? color.lime : color.pale, 20);
    const textColor = disabled ? '#9695a1' : color.ink, size = width < 190 ? 26 : 30;
    const lineHeight = size + 7, start = y + (height - lines.length * lineHeight) / 2 + size;
    lines.forEach((line, index) => label(line, x + 22, start + index * lineHeight, size, textColor, '650', width - 44));
    hits.push({ x, y, width, height, action, disabled });
  };
  const setPage = index => { pageIndex = THREE.MathUtils.clamp(index, 0, pages.length - 1); dirty = true; };
  const currentModal = () => extractVRModal(root, { getStyle: style });
  function redraw() {
    const model = currentModal(), hasModal = Boolean(model.title || model.entries.length || model.hasForms);
    const nextMode = hasModal ? 'modal' : menu ? 'menu' : 'hud';
    // Countdown, score and feedback mutations redraw in place. Only a new
    // question or new controls return the reader to the first page.
    const nextSignature = hasModal ? `${model.title}|${model.question}|${model.entries.filter(entry => ['heading', 'button'].includes(entry.kind)).map(entry => `${entry.kind}:${entry.baseText || entry.text}`).join('|')}` : nextMode;
    if (nextMode !== mode || nextSignature !== signature) {
      pageIndex = 0;
      if (nextMode !== mode || model.title !== modalTitle) recenter = true;
    }
    mode = nextMode; signature = nextSignature; modalTitle = model.title;
    const status = mode === 'hud' ? getStatus() || {} : null;
    const messageLines = mode === 'hud' ? wrapVRText(status.message || '', 56) : [];
    const height = mode === 'hud' ? messageLines.length ? 344 + messageLines.length * 35 : 320 : mode === 'menu' ? 900 : 1152;
    if (canvas.height !== height) {
      // A texture's dimensions are immutable after its first GPU upload. Resize
      // once to the final layout and replace only the texture, retaining the mesh.
      texture.dispose(); canvas.height = height; texture = createTexture();
      material.map = texture; material.needsUpdate = true;
    }
    const widthMetres = mode === 'hud' ? 1.08 : 1.32;
    mesh.scale.set(widthMetres, widthMetres * height / canvas.width, 1);
    ctx.clearRect(0, 0, canvas.width, canvas.height); hits = [];
    rect(0, 0, canvas.width, canvas.height, color.cream, 36);
    label('LUMEN · VR', 46, 69, 30, color.purple, '750');
    button('Ukončiť VR', 810, 25, 184, 65, onExit);
    if (mode === 'hud') {
      const statusText = `${status.zone || 'Akadémia objavov'} · Úroveň ${status.level || 1}${status.connected ? ' · Spolu' : ''}`;
      label(statusText, 46, 116, 28, color.muted, '500', 940);
      button(status.targetName ? `Preskúmať: ${status.targetName}` : 'Preskúmať okolie', 40, 146, 710, 100, onInteract, { primary: true });
      button('Menu', 774, 146, 210, 100, () => { menu = true; recenter = true; dirty = true; });
      messageLines.forEach((line, index) => label(line, 46, 285 + index * 35, 27, color.purple, '650', 940));
      label('Ľavá páčka: pohyb · Pravá: otočenie · Spúšť: výber', 46, canvas.height - 25, 24, color.muted, '500', 940);
    } else if (mode === 'menu') {
      label('Tvoja výprava', 46, 147, 48, color.ink, '750');
      label('Namier ovládač a stlač spúšť.', 46, 198, 28, color.muted);
      const items = [
        ['Objavovať ostrov', () => { menu = false; recenter = true; dirty = true; }],
        ['Spoločníci', () => { menu = false; onPanel('pets'); dirty = true; }],
        ['Mapa akadémie', () => { menu = false; onPanel('map'); dirty = true; }],
        ['Denník poznatkov', () => { menu = false; onPanel('journal'); dirty = true; }],
        ['Skočiť', () => { menu = false; onJump(); dirty = true; }],
      ];
      items.forEach(([text, action], index) => button(text, 46, 239 + index * 103, 932, 83, action, { primary: index === 0 }));
      label('Spoločný svet pripoj pred vstupom do VR.', 46, 838, 26, color.muted, '500', 932);
    } else {
      const titleLines = wrapVRText(model.title || 'Tvoja výprava', 32).slice(0, 2);
      titleLines.forEach((line, index) => label(line, 46, 138 + index * 49, 42, color.ink, '750', 932));
      const entries = [...model.entries];
      if (model.hasForms) entries.unshift({ kind: 'text', text: 'Meno a kód miestnosti vyplň v bežnom zobrazení pred vstupom do VR.' });
      pages = paginateVRContent(entries); pageIndex = Math.min(pageIndex, pages.length - 1);
      let y = 240;
      for (const entry of pages[pageIndex]) {
        if (entry.kind === 'button') {
          const h = Math.max(76, entry.lines.length * 38 + 24);
          button(entry.text, 46, y, 932, h, () => {
            if (entry.node?.disabled || entry.node?.matches?.(':disabled') || entry.node?.getAttribute?.('aria-disabled') === 'true' || !root?.contains?.(entry.node) || !isVRVisible(entry.node, root, style)) return;
            entry.node.click(); dirty = true;
          }, { disabled: entry.disabled, primary: Boolean(entry.node?.classList?.contains('primary-btn') || entry.node?.classList?.contains('choice')), lines: entry.lines });
          y += h + 15;
        } else {
          entry.lines.forEach((line, index) => label(line, 46, y + 30 + index * 39, entry.kind === 'heading' ? 31 : 28, entry.kind === 'heading' ? color.ink : color.muted, entry.kind === 'heading' ? '750' : '500', 932));
          y += entry.lines.length * 39 + 18;
        }
      }
      label(`${pageIndex + 1} / ${pages.length}`, 46, 996, 25, color.muted);
      button('Späť', 46, 1023, 240, 84, () => setPage(pageIndex - 1), { disabled: pageIndex === 0 });
      button('Ďalšia strana', 309, 1023, 354, 84, () => setPage(pageIndex + 1), { disabled: pageIndex === pages.length - 1 });
      button('Zavrieť', 688, 1023, 290, 84, () => { onCloseModal(); menu = false; dirty = true; recenter = true; });
    }
    texture.needsUpdate = true; dirty = false;
  }
  const api = {
    get active() { return active; },
    get menuOpen() { return active && menu; },
    setActive(value) {
      if (disposed) return;
      active = Boolean(value); mesh.visible = active; menu = false; pageIndex = 0; recenter = true; dirty = true; elapsed = .25;
    },
    toggleMenu() {
      if (!active || disposed) return;
      const modal = currentModal();
      if (modal.title || modal.entries.length || modal.hasForms) { onCloseModal(); menu = true; } else menu = !menu;
      recenter = true; dirty = true;
    },
    select(controller) {
      if (!active || disposed || !controller) return false;
      controller.updateWorldMatrix(true, false);
      controller.getWorldPosition(controllerOrigin); controller.getWorldQuaternion(quaternion);
      forward.set(0, 0, -1).applyQuaternion(quaternion);
      raycaster.set(controllerOrigin, forward);
      mesh.updateWorldMatrix(true, false);
      const intersection = raycaster.intersectObject(mesh, false)[0];
      if (!intersection?.uv) return menu || mode !== 'hud';
      const x = intersection.uv.x * canvas.width, y = (1 - intersection.uv.y) * canvas.height;
      const hit = hits.find(item => x >= item.x && x <= item.x + item.width && y >= item.y && y <= item.y + item.height);
      if (hit && !hit.disabled) hit.action();
      return true;
    },
    update(dt, viewCamera) {
      if (!active || disposed || !viewCamera) return;
      elapsed += Math.max(0, dt || 0);
      if (elapsed >= .25) {
        elapsed = 0;
        if (mode === 'hud') {
          const statusSignature = JSON.stringify(getStatus() || {});
          if (statusSignature !== lastStatusSignature) { lastStatusSignature = statusSignature; dirty = true; }
        }
        if (dirty) redraw();
      }
      // The WebXR ArrayCamera already contains the rig transform in matrixWorld;
      // its parentless position is tracking-local, so do not recompute that matrix.
      eye.setFromMatrixPosition(viewCamera.matrixWorld);
      forward.set(0, 0, -1).transformDirection(viewCamera.matrixWorld); forward.y = 0;
      if (forward.lengthSq() < .001) forward.set(0, 0, -1); else forward.normalize();
      desired.copy(eye).addScaledVector(forward, mode === 'hud' ? 1.6 : 1.85);
      desired.y += mode === 'hud' ? -.48 : -.10;
      if (recenter) {
        mesh.position.copy(desired); mesh.quaternion.setFromAxisAngle(up, Math.atan2(-forward.x, -forward.z)); recenter = false;
      } else if (mode === 'hud') {
        const alpha = 1 - Math.exp(-7 * Math.max(0, dt));
        mesh.position.lerp(desired, alpha);
        quaternion.setFromAxisAngle(up, Math.atan2(-forward.x, -forward.z)); mesh.quaternion.slerp(quaternion, alpha);
      }
    },
    dispose() {
      if (disposed) return;
      disposed = true; active = false; observer?.disconnect(); scene.remove(mesh); geometry.dispose(); material.dispose(); texture.dispose(); hits = [];
    },
  };
  return api;
}
