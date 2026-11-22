import assert from 'node:assert/strict';
import * as THREE from '../public/vendor/three.module.js';
import { createVRPanel, extractVRModal, paginateVRContent, wrapVRText } from '../public/vr-panel.js';

const text = value => ({ nodeType: 3, textContent: value });
class Element {
  constructor(tag, children = [], attrs = {}) {
    this.nodeType = 1; this.tagName = tag.toUpperCase(); this.childNodes = children.map(child => typeof child === 'string' ? text(child) : child);
    this.attrs = attrs; this.style = {}; this.hidden = false; this.disabled = false; this.clicks = 0;
    this.classList = { contains: name => (attrs.class || '').split(' ').includes(name) };
    this.childNodes.forEach(child => { child.parentElement = this; });
  }
  getAttribute(name) { return this.attrs[name] ?? null; }
  querySelector(selector) {
    const tags = selector.split(',').map(tag => tag.toUpperCase());
    const queue = [...this.childNodes];
    while (queue.length) { const node = queue.shift(); if (tags.includes(node.tagName)) return node; queue.push(...(node.childNodes || [])); }
    return null;
  }
  contains(target) { return target === this || this.childNodes.some(child => child.nodeType === 1 && child.contains(target)); }
  click() { this.clicks++; this.onClick?.(); }
  replace(children = []) { this.childNodes = children; children.forEach(child => { child.parentElement = this; }); }
}
const h2 = new Element('h2', ['Svetlo a rastliny']);
const question = new Element('p', ['Prečo rastliny potrebujú svetlo na ', new Element('strong', ['fotosyntézu']), '?'], { class: 'question' });
const answer = new Element('button', [new Element('span', ['A']), new Element('span', ['Aby vytvárali živiny.'])], { class: 'choice', 'data-answer': 'correct' });
const disabled = new Element('button', ['Ďalší poznatok']); disabled.disabled = true;
const hiddenHint = new Element('p', ['Toto je skrytá nápoveda.']); hiddenHint.hidden = true;
const invisibleButton = new Element('button', ['Tajná odpoveď']);
const invisibleParent = new Element('div', [invisibleButton], { 'aria-hidden': 'true' });
const displayNone = new Element('p', ['Skrytý výsledok']); displayNone.style.display = 'none';
const close = new Element('button', [new Element('svg')], { class: 'modal-close', 'aria-label': 'Zavrieť' });
const root = new Element('div', [new Element('section', [close, h2, question, answer, disabled, hiddenHint, invisibleParent, displayNone])]);
let extracted = extractVRModal(root, { getStyle: node => node.style });
assert.equal(extracted.title, 'Svetlo a rastliny');
assert.equal(extracted.entries[0].text, 'Prečo rastliny potrebujú svetlo na fotosyntézu ?');
assert.deepEqual(extracted.entries.filter(entry => entry.kind === 'button').map(entry => [entry.text, entry.disabled]), [['A Aby vytvárali živiny.', false], ['Ďalší poznatok', true]]);
assert.ok(!extracted.entries.some(entry => /skryt|Skryt|Tajn|Zavrieť/.test(entry.text)), 'Hidden ancestors and icon-only close controls are excluded');
assert.equal(extracted.hasForms, false);
const form = new Element('input', [], { type: 'text' }), hiddenInput = new Element('input', [], { type: 'hidden' });
root.childNodes.push(form, hiddenInput); form.parentElement = root; hiddenInput.parentElement = root;
assert.equal(extractVRModal(root).hasForms, true, 'Text entry has an explicit outside-headset fallback');
root.childNodes.pop(); root.childNodes.pop();

const lesson = Array.from({ length: 240 }, (_, index) => `poznatok${index}`).join(' ');
const pages = paginateVRContent([{ kind: 'text', text: lesson }, { kind: 'button', text: 'Správna odpoveď', node: answer, disabled: false }], { columns: 38, maxUnits: 9 });
assert.ok(pages.length > 5, 'Long lessons remain accessible through pagination');
assert.equal(pages.flat().filter(entry => entry.kind === 'text').map(entry => entry.text).join(' '), lesson, 'Pagination preserves every lesson word');
assert.equal(pages.flat().find(entry => entry.kind === 'button').node, answer, 'Answer identity is retained across pages');
assert.ok(pages.every(page => page.reduce((sum, entry) => sum + entry.units, 0) <= 9));
const buttonPages = paginateVRContent(Array.from({ length: 9 }, (_, index) => ({ kind: 'button', text: `Odpoveď ${index}` })));
assert.equal(buttonPages.length, 2, 'Nine short buttons cannot overlap the reserved navigation footer');
assert.ok(buttonPages.every(page => page.reduce((sum, entry) => sum + entry.units * 39, 0) <= 702));
assert.deepEqual(wrapVRText('abcdefghijklmnop', 5), ['abcde', 'fghij', 'klmno', 'p'], 'Unbroken strings cannot overflow the plane');

let observer, contextCalls = 0;
class Observer { constructor(callback) { this.callback = callback; observer = this; } observe() {} disconnect() { this.disconnected = true; } }
const drawn = [];
const canvas = { width: 0, height: 0, getContext() { return { beginPath() {}, roundRect() {}, fill() {}, clearRect() { drawn.length = 0; contextCalls++; }, fillText(value) { drawn.push(value); } }; } };
const doc = { createElement: () => canvas, querySelector: () => root, defaultView: { MutationObserver: Observer, getComputedStyle: node => node.style } };
const scene = new THREE.Scene(), campus = new THREE.Group(); scene.add(campus);
const camera = new THREE.PerspectiveCamera();
// Match WebXR's parentless ArrayCamera: only matrixWorld includes the player rig.
camera.matrixWorld.makeTranslation(20, 2, 13);
camera.getWorldPosition = () => { throw new Error('XR matrix must not be overwritten'); };
camera.getWorldDirection = () => { throw new Error('XR matrix must not be overwritten'); };
let closed = 0, exits = 0, interacted = 0, jumped = 0, openedPanel;
let status = { level: 3, zone: 'Les poznania', connected: true, targetName: 'Matematika' };
const panel = createVRPanel({ scene, document: doc, getStatus: () => status, onCloseModal: () => { closed++; root.replace(); observer.callback(); }, onExit: () => exits++, onInteract: () => interacted++, onJump: () => jumped++, onPanel: name => { openedPanel = name; } });
const mesh = scene.getObjectByName('LumenVRPanel');
assert.equal(mesh.visible, false); assert.equal(panel.active, false);
const initialTexture = mesh.material.map; let initialTextureReleased = 0;
initialTexture.addEventListener('dispose', () => initialTextureReleased++);
panel.setActive(true); panel.update(0, camera);
assert.notEqual(mesh.material.map, initialTexture); assert.equal(initialTextureReleased, 1, 'HUD-to-modal resizing releases the original GPU texture');
const modalTexture = mesh.material.map; let modalTextureReleased = 0;
modalTexture.addEventListener('dispose', () => modalTextureReleased++);
assert.equal(modalTexture.generateMipmaps, false); assert.equal(modalTexture.minFilter, THREE.LinearFilter); assert.equal(modalTexture.colorSpace, THREE.SRGBColorSpace);
assert.equal(mesh.visible, true); assert.equal(mesh.position.x, 20); assert.equal(mesh.position.y, 1.9); assert.equal(mesh.position.z, 11.15, 'Panel includes the room-scale player rig translation');
assert.ok(drawn.includes('Svetlo a rastliny')); assert.ok(drawn.includes('Prečo rastliny potrebujú svetlo na fotosyntézu ?'));
const controller = new THREE.Group(); controller.position.set(20, 2, 13);
const aimPixel = (x, y) => {
  mesh.updateWorldMatrix(true, false);
  const target = mesh.localToWorld(new THREE.Vector3(x / canvas.width - .5, .5 - y / canvas.height, 0));
  controller.quaternion.setFromRotationMatrix(new THREE.Matrix4().lookAt(controller.position, target, new THREE.Vector3(0, 1, 0)));
  controller.updateMatrixWorld(true);
};
// The first text row takes 57 pixels; its first answer is at y=297.
aimPixel(400, 340); assert.equal(panel.select(controller), true); assert.equal(answer.clicks, 1, 'A real ray through a choice invokes the existing answer button');
answer.disabled = true;
aimPixel(400, 340); panel.select(controller); assert.equal(answer.clicks, 1, 'A button disabled after the last redraw cannot submit another answer');
answer.disabled = false;
const section = root.childNodes[0], originalChildren = section.childNodes;
section.childNodes = originalChildren.filter(node => node !== answer);
aimPixel(400, 340); panel.select(controller); assert.equal(answer.clicks, 1, 'A stale answer removed by a network repaint cannot be submitted');
section.childNodes = originalChildren;
controller.quaternion.setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI);
assert.equal(panel.select(controller), true, 'Missing a modal does not accidentally interact with the world');
const renderedBefore = contextCalls;
hiddenHint.hidden = false; observer.callback(); panel.update(.1, camera);
assert.equal(contextCalls, renderedBefore, 'Mutation bursts wait for the bounded texture update interval');
panel.update(.15, camera); assert.ok(contextCalls > renderedBefore); assert.ok(drawn.includes('Toto je skrytá nápoveda.'));
assert.equal(mesh.material.map, modalTexture, 'Content redraws of the same dimensions retain the existing GPU allocation');

panel.toggleMenu(); assert.equal(closed, 1); assert.equal(panel.menuOpen, true, 'Movement pauses immediately when the menu is opened');
panel.update(.25, camera); assert.equal(panel.menuOpen, true); assert.ok(drawn.includes('Spoločníci'));
assert.equal(modalTextureReleased, 1); assert.notEqual(mesh.material.map, modalTexture, 'Modal-to-menu resizing replaces the texture');
const menuTexture = mesh.material.map; let menuTextureReleased = 0;
menuTexture.addEventListener('dispose', () => menuTextureReleased++);
aimPixel(400, 385); panel.select(controller); assert.equal(openedPanel, 'pets'); assert.equal(panel.menuOpen, false);
panel.update(.25, camera);
assert.equal(menuTextureReleased, 1); assert.notEqual(mesh.material.map, menuTexture, 'Menu-to-HUD resizing replaces the texture');
assert.ok(drawn.some(value => value.includes('Les poznania'))); assert.ok(drawn.some(value => value.includes('Spolu')));
aimPixel(400, 190); panel.select(controller); assert.equal(interacted, 1, 'The compact HUD still allows nearby world interaction');
controller.quaternion.setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI); assert.equal(panel.select(controller), false, 'An unobstructed HUD miss permits world interaction');
status = { ...status, message: 'Rastliny pri fotosyntéze využívajú svetlo a uvoľňujú kyslík.' };
const hudTexture = mesh.material.map; let hudTextureReleased = 0;
hudTexture.addEventListener('dispose', () => hudTextureReleased++);
panel.update(.25, camera); assert.equal(canvas.height, 414); assert.ok(drawn.some(value => value.includes('Rastliny pri fotosyntéze'))); assert.ok(drawn.some(value => value.includes('kyslík.')), 'Shared educational facts remain readable in the headset');
assert.equal(hudTextureReleased, 1); assert.notEqual(mesh.material.map, hudTexture);
const messageTexture = mesh.material.map; let messageTextureReleased = 0;
messageTexture.addEventListener('dispose', () => messageTextureReleased++);
status = { ...status, level: 4 }; panel.update(.25, camera);
assert.equal(mesh.material.map, messageTexture); assert.equal(messageTextureReleased, 0, 'A toast HUD computes its final height once and keeps its texture on same-size status updates');
aimPixel(900, 55); panel.select(controller); assert.equal(exits, 1, 'Ending VR is reachable in the compact HUD');

panel.toggleMenu(); panel.update(.25, camera); aimPixel(400, 685); panel.select(controller); assert.equal(jumped, 1);
assert.equal(messageTextureReleased, 1);
// Live-room countdown and tactic feedback must not throw a reader off page two.
const countdown = new Element('span', ['20'], { 'data-mp-countdown': '' });
const liveQuestion = new Element('h3', ['Ktorá planéta je najbližšie k Slnku?'], { class: 'mp-question' });
const liveAnswers = Array.from({ length: 9 }, (_, index) => new Element('button', [`Možnosť ${index}`], { 'data-mp-answer': `${index}` }));
panel.toggleMenu(); panel.update(.25, camera);
const liveMenuTexture = mesh.material.map; let liveMenuTextureReleased = 0;
liveMenuTexture.addEventListener('dispose', () => liveMenuTextureReleased++);
root.replace([new Element('h2', ['Spoločný kvíz']), countdown, liveQuestion, ...liveAnswers]); observer.callback(); panel.update(.25, camera);
assert.notEqual(mesh.material.map, liveMenuTexture); assert.equal(liveMenuTextureReleased, 1, 'Opening a modal from the menu uses a fresh texture with the new dimensions');
assert.ok(drawn.includes('1 / 2')); assert.ok(drawn.includes('Ktorá planéta je najbližšie k Slnku?'));
aimPixel(500, 1060); panel.select(controller); panel.update(.25, camera); assert.ok(drawn.includes('2 / 2'));
countdown.childNodes[0].textContent = '19'; observer.callback(); panel.update(.25, camera);
assert.ok(drawn.includes('2 / 2'), 'A countdown update retains the selected page');
liveAnswers[8].attrs['aria-pressed'] = 'true'; observer.callback(); panel.update(.25, camera);
assert.ok(drawn.includes('✓ Možnosť 8')); assert.ok(drawn.includes('2 / 2'), 'Selected-answer feedback is visible without resetting the page');
liveQuestion.childNodes[0].textContent = 'Ktorá planéta je najväčšia?'; observer.callback(); panel.update(.25, camera);
assert.ok(drawn.includes('1 / 2'), 'A new question returns to its first page');
panel.setActive(false); assert.equal(mesh.visible, false); assert.equal(panel.select(controller), false);
let releases = 0; for (const resource of [mesh.geometry, mesh.material, mesh.material.map]) resource.addEventListener('dispose', () => releases++);
panel.dispose(); panel.dispose(); assert.equal(releases, 3); assert.equal(observer.disconnected, true); assert.deepEqual(scene.children, [campus]);
console.log('VR panel: readable quizzes, visibility, disabled and stale answers, pagination, controller rays, tracked rig placement, menu, world interaction, facts, exit and resource cleanup passed.');
