/** A small original toy-world arena. Place the group on open grass. */
export function buildArena(THREE) {
  const arena = new THREE.Group();
  arena.name = 'lumen-friendship-arena';
  const palette = { cream: 0xfffdf4, violet: 0xa69af7, violetDark: 0x7a6cc4, mint: 0xb5dbc5, mintDark: 0x78a893, gold: 0xf2d788, navy: 0x36435c, white: 0xffffff };
  const mats = Object.fromEntries(Object.entries(palette).map(([name, color]) => [name, new THREE.MeshStandardMaterial({ color, roughness: .9, metalness: 0 })]));
  const cube = new THREE.BoxGeometry(1, 1, 1);
  const cylinder = new THREE.CylinderGeometry(1, 1, 1, 24);
  const ball = new THREE.SphereGeometry(1, 12, 8);
  function mesh(geometry, material, position, scale = [1, 1, 1], parent = arena) {
    const object = new THREE.Mesh(geometry, mats[material]);
    object.position.set(...position); object.scale.set(...scale);
    object.castShadow = position[1] > .4; object.receiveShadow = true;
    parent.add(object); return object;
  }
  const disc = mesh(cylinder, 'cream', [0, .045, 0], [5.5, .035, 5.5]);
  disc.name = 'arena-walkable-floor';
  mesh(cylinder, 'violet', [-2.1, .074, 0], [1.95, .018, 1.95]);
  mesh(cylinder, 'mint', [2.1, .076, 0], [1.95, .018, 1.95]);
  mesh(cylinder, 'cream', [0, .09, 0], [1.1, .012, 1.1]);
  const star = mesh(new THREE.OctahedronGeometry(.48, 0), 'gold', [0, .105, 0], [1, .08, 1]);
  star.rotation.y = Math.PI / 4;
  const ring = new THREE.Mesh(new THREE.RingGeometry(5.2, 5.5, 48), mats.violetDark);
  ring.rotation.x = -Math.PI / 2; ring.position.y = .075; ring.receiveShadow = true; arena.add(ring);
  for (let side = -1; side <= 1; side += 2) {
    for (let tier = 0; tier < 2; tier++) {
      mesh(cube, side < 0 ? 'violet' : 'mint', [side * (6.1 + tier * .55), .22 + tier * .25, -.5], [.55, .3 + tier * .25, 4.3]);
      mesh(cube, 'cream', [side * (6.1 + tier * .55), .41 + tier * .38, -.5], [.66, .11, 4.4]);
    }
    for (let end = -1; end <= 1; end += 2) {
      const x = side * 5.45, z = end * 3.8;
      mesh(cylinder, 'cream', [x, .18, z], [.43, .24, .43]);
      mesh(cylinder, 'navy', [x, 1.5, z], [.055, 2.65, .055]);
      mesh(ball, 'gold', [x, 2.92, z], [.14, .14, .14]);
      mesh(cube, side < 0 ? 'violet' : 'mint', [x + side * .42, 2.48, z], [.76, .7, .08]);
      mesh(new THREE.OctahedronGeometry(.17, 0), 'cream', [x + side * .42, 2.48, z + .065], [1, 1, .2]);
    }
  }
  // A friendly crystal-tailed creature watches over the entrance.
  const mascot = new THREE.Group(); mascot.position.set(0, .35, -5.45); arena.add(mascot);
  mesh(cylinder, 'cream', [0, -.13, 0], [1.15, .3, 1.15], mascot);
  mesh(cube, 'mint', [0, .62, 0], [.86, .82, .64], mascot);
  mesh(cube, 'mint', [0, 1.25, .08], [1.03, .68, .75], mascot);
  mesh(cube, 'cream', [0, 1.1, .485], [.61, .3, .12], mascot);
  for (const side of [-1, 1]) {
    const ear = mesh(new THREE.ConeGeometry(.2, .46, 4), 'mintDark', [side * .36, 1.78, .06], [1, 1, 1], mascot);
    ear.rotation.y = Math.PI / 4;
    mesh(ball, 'navy', [side * .22, 1.31, .468], [.055, .07, .035], mascot);
    mesh(ball, 'white', [side * .205, 1.334, .496], [.018, .022, .01], mascot);
    mesh(cube, 'mintDark', [side * .27, .13, .16], [.27, .22, .42], mascot);
  }
  mesh(ball, 'navy', [0, 1.14, .57], [.065, .04, .035], mascot);
  const tail = mesh(new THREE.OctahedronGeometry(.34, 0), 'violet', [.55, .64, -.48], [1, 1.5, 1], mascot);
  tail.rotation.z = -.55;
  mesh(new THREE.OctahedronGeometry(.22, 0), 'gold', [0, 2.27, 0], [1, 1.3, 1], mascot);
  for (const side of [-1, 1]) {
    mesh(cylinder, 'violetDark', [side * 2.6, .22, 4.65], [.21, .32, .21]);
    mesh(ball, 'cream', [side * 2.6, .48, 4.65], [.28, .25, .28]);
    mesh(new THREE.OctahedronGeometry(.13, 0), 'gold', [side * 2.6, .72, 4.65]);
  }
  arena.userData = { decorative: true, footprintRadius: 7.5, entry: { x: 0, z: 4.4 } };
  return arena;
}
