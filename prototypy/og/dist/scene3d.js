import * as THREE from './vendor/three.module.js';

/** Original, procedural Academy world. Ground is y=0; all interactions stay walkable. */
export function createAcademyWorld(scene) {
  const root = new THREE.Group();
  root.name = 'AcademyWorld';
  scene.add(root);
  const colliders = [], interactables = [], animated = [];
  const mat = (color, opts = {}) => new THREE.MeshStandardMaterial({ color, roughness: 0.82, ...opts });
  const palette = {
    grass: mat('#82cfad'), mint: mat('#b9ead6'), road: mat('#f3e5c6'), edge: mat('#fff4dd'),
    stone: mat('#d4e5ec'), ink: mat('#26394f'), cream: mat('#fff7e5'), peach: mat('#ffb591'),
    coral: mat('#ef807d'), purple: mat('#b997ed'), violet: mat('#7865bc'), blue: mat('#91d3ee'),
    teal: mat('#53bbae'), cyan: mat('#91d3ee'), gold: mat('#ffc957'), brown: mat('#a47c64'), dark: mat('#54786f'),
    white: mat('#ffffff'), leaf: mat('#43ab89'), leafLight: mat('#68c99c'),
    cyanGlow: mat('#64e5ee', { emissive: '#39c9d2', emissiveIntensity: 0.7 }),
    goldGlow: mat('#ffe180', { emissive: '#ffd15c', emissiveIntensity: 0.55 }),
    purpleGlow: mat('#c8afff', { emissive: '#8963eb', emissiveIntensity: 0.7 }),
  };
  const box = (w,h,d,x,y,z,m,parent=root) => {
    const mesh = new THREE.Mesh(new THREE.BoxGeometry(w,h,d), typeof m === 'string' ? mat(m) : m);
    mesh.position.set(x,y,z); mesh.castShadow = true; mesh.receiveShadow = true; parent.add(mesh); return mesh;
  };
  const cylinder = (r1,r2,h,x,y,z,m,parent=root,segments=12) => {
    const mesh = new THREE.Mesh(new THREE.CylinderGeometry(r1,r2,h,segments), m);
    mesh.position.set(x,y,z); mesh.castShadow=true; mesh.receiveShadow=true; parent.add(mesh); return mesh;
  };
  const sphere = (r,x,y,z,m,parent=root,scale=null) => {
    const mesh=new THREE.Mesh(new THREE.SphereGeometry(r,20,14),m);
    mesh.position.set(x,y,z); if(scale) mesh.scale.set(...scale); mesh.castShadow=true; mesh.receiveShadow=true; parent.add(mesh); return mesh;
  };
  const ring = (radius,tube,x,y,z,m,parent=root,vertical=false) => {
    const mesh = new THREE.Mesh(new THREE.TorusGeometry(radius,tube,8,48),m);
    mesh.position.set(x,y,z); if(!vertical)mesh.rotation.x=-Math.PI/2; parent.add(mesh); mesh.castShadow=true; return mesh;
  };
  const collide = (x,z,hx,hz) => colliders.push({x,z,hx,hz});
  const interaction = (id,type,name,x,z,extra={}) => interactables.push({id,type,name,position:{x,z},radius:3,...extra});
  const label = (text,x,y,z,width=5,height=1.1,color='#293d52',bg='#fff6de',parent=root) => {
    const canvas=document.createElement('canvas'); canvas.width=1024; canvas.height=256;
    const ctx=canvas.getContext('2d');
    ctx.fillStyle=bg; ctx.beginPath(); ctx.roundRect(8,8,1008,240,42); ctx.fill();
    ctx.strokeStyle='rgba(255,255,255,.8)';ctx.lineWidth=10;ctx.stroke();
    ctx.fillStyle=color;ctx.font='900 76px Arial, sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';
    const words=text.split('\n'); words.forEach((s,i)=>ctx.fillText(s,512,words.length===1?128:84+i*84,940));
    const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;
    const mesh=new THREE.Mesh(new THREE.PlaneGeometry(width,height),new THREE.MeshBasicMaterial({map:texture,side:THREE.DoubleSide,transparent:true}));
    mesh.position.set(x,y,z);parent.add(mesh);return mesh;
  };
  const road = (x,z,w,d) => box(w,.045,d,x,.027,z,palette.road);
  const pad = (x,z,color,r=2.4) => {
    cylinder(r,r+.15,.16,x,.10,z,palette.cream);
    cylinder(r-.2,r-.2,.025,x,.20,z,palette[color]);
    ring(r-.08,.08,x,.23,z,palette[color+'Glow']||palette.goldGlow);
  };
  const windowPane = (x,y,z,w,h,parent=root) => {
    box(w+.18,h+.18,.13,x,y,z,palette.cream,parent);
    box(w,h,.15,x,y,z+.02,palette.blue,parent);
    box(.07,h,.17,x,y,z+.035,palette.white,parent);
    box(w,.07,.17,x,y,z+.035,palette.white,parent);
  };
  const tree = (x,z,size=1,style=0) => {
    cylinder(.24*size,.32*size,2*size,x,size,z,palette.brown);
    if(style===1){
      cylinder(0,1.45*size,2.2*size,x,2.1*size,z,palette.leaf,root,5);
      cylinder(0,1.15*size,1.9*size,x,3.1*size,z,palette.leafLight,root,5);
    } else {
      const crown = box(2.5*size,2.3*size,2.4*size,x,2.9*size,z,palette.leaf); crown.rotation.y=Math.PI/7;
      box(1.8*size,1.1*size,1.7*size,x+.35*size,4*size,z-.15*size,palette.leafLight);
    }
    collide(x,z,.35*size,.35*size);
  };
  const hedge = (x,z,w=2,d=1) => {box(w,.8,d,x,.4,z,palette.leafLight);};
  const bench = (x,z,rot=0) => {
    const g=new THREE.Group();g.position.set(x,0,z);g.rotation.y=rot;root.add(g);
    box(3,.20,.75,0,.65,0,palette.brown,g);box(3,.65,.18,0,1.1,-.3,palette.peach,g);
    [-1.1,1.1].forEach(a=>box(.16,.65,.65,a,.32,0,palette.ink,g));
  };
  const lamp = (x,z) => {
    cylinder(.1,.13,3.7,x,1.85,z,palette.ink);box(.7,.15,.7,x,3.68,z,palette.ink);
    sphere(.3,x,3.45,z,palette.goldGlow);collide(x,z,.16,.16);
  };
  const star = (parent,x,y,z,scale=.35,m=palette.gold) => {
    const shape=new THREE.Shape();for(let i=0;i<10;i++){const angle=Math.PI/2+i*Math.PI/5;const rr=i%2===0?scale:scale*.44;const xx=Math.cos(angle)*rr, yy=Math.sin(angle)*rr; i?shape.lineTo(xx,yy):shape.moveTo(xx,yy);}shape.closePath();
    const mesh=new THREE.Mesh(new THREE.ExtrudeGeometry(shape,{depth:.1,bevelEnabled:false}),m);mesh.position.set(x,y,z);parent.add(mesh);return mesh;
  };
  const egg = (x,y,z,kind='basic',scale=1,parent=root) => {
    const group=new THREE.Group();group.position.set(x,y,z);group.scale.setScalar(scale);parent.add(group);
    const colors = {basic:['#ffdeb2','#f39f67'],science:['#91e3ec','#4aaebf'],cosmic:['#c7acfb','#7c5fc9']};
    const c=colors[kind]||colors.basic;
    sphere(.87,0,.1,0,mat(c[0]),group,[1,1.27,1]);sphere(.70,0,.59,0,mat(c[0]),group,[1,1.22,1]);
    const belt=ring(.83,.085,0,.08,0,mat(c[1]),group);
    [-.48,.45].forEach((xx,i)=>star(group,xx,.5-i*.6,.75,.22,palette.gold));
    if(kind==='cosmic'){const halo=ring(1.23,.035,0,.4,0,palette.purpleGlow,group);halo.rotation.z=.3;}
    if(kind==='science'){[-.4,0,.4].forEach(xx=>sphere(.075,xx,.76,.58,palette.cream,group));}
    return group;
  };
  const kid = (x,z,color,rot=0) => {
    const g=new THREE.Group();g.position.set(x,0,z);g.rotation.y=rot;root.add(g);
    box(.72,.82,.4,0,1.08,0,mat(color),g);box(.62,.62,.6,0,1.82,0,palette.peach,g);
    box(.67,.17,.64,0,2.1,-.035,palette.brown,g);box(.68,.25,.15,0,1.99,-.3,palette.brown,g);
    [-.21,.21].forEach(xx=>{box(.23,.62,.26,xx,.45,0,palette.ink,g);box(.29,.19,.45,xx,.1,.08,palette.white,g);box(.14,.74,.23,xx*2.4,1.03,0,palette.peach,g);box(.075,.09,.025,xx*.65,1.84,.31,palette.ink,g);});
    box(.14,.035,.035,0,1.66,.315,palette.ink,g);
    animated.push({kind:'npc',object:g,baseY:0,phase:x+z});return g;
  };

  // Grass, roads and plaza: an intentionally open navigation network.
  box(104,.5,104,0,-.28,0,palette.grass);
  road(0,8,12,40);road(0,-17,44,8);road(1,4,46,8);road(-16,13,22,9);road(0,-28,10,18);
  cylinder(11.7,11.7,.06,0,.058,0,palette.road,root,64);
  ring(11.55,.12,0,.13,0,palette.edge);
  // Small circular inlays add the rhythm of a toy campus without clipping the floor.
  for(let z=8;z<24;z+=3)box(1.15,.035,.3,0,.064,z,palette.edge);
  for(let x=-11;x<=11;x+=3)box(.3,.035,1.15,x,.066,4,palette.edge);
  [-1,1].forEach(side=>{for(let i=0;i<4;i++)hedge(side*8.1,13+i*2.2,1.1,1.5);});

  // Egg market: open shelves, a broad canopy, three selectable egg pedestals.
  box(14,.3,5,0,.18,-1.4,palette.stone);
  box(14,.15,5,0,.4,-1.4,palette.cream);
  [-6.5,6.5].forEach(x=>{box(.35,5,.35,x,2.8,-3.1,palette.teal);collide(x,-3.1,.24,.24);});
  box(15,.42,4.9,0,5.35,-1.5,palette.teal);
  for(let x=-7;x<=7;x+=1)box(.75,.12,5,x,5.60,-1.5,x%2===0?palette.cream:palette.mint);
  label('AKADÉMIA VAJÍČOK',0,4.8,1.05,9,1.13,'#284e53','#e5fff2');
  label('UČ SA · OBJAVUJ · ZBIERAJ',0,3.87,-3.33,8,.58,'#4a6680','#fff7e5');
  box(11,.8,.35,0,.87,-3.7,palette.brown);
  for(let i=0;i<12;i++){const colors=[palette.blue,palette.purple,palette.gold,palette.peach];box(.5,.5+(i%3)*.13,.25,-5.2+i*.9,1.5,-3.6,colors[i%4]);}
  const kioskEggs=[];
  [{x:-4,id:'egg-basic',kind:'basic',name:'Štartovacie vajíčko',text:'OBJAVITEĽ'}, {x:0,id:'egg-science',kind:'science',name:'Vedecké vajíčko',text:'VEDÁTOR'}, {x:4,id:'egg-cosmic',kind:'cosmic',name:'Kozmické vajíčko',text:'KOZMICKÉ'}].forEach((data,i)=>{
    cylinder(1.4,1.6,.9,data.x,.8,-.7,[palette.peach,palette.blue,palette.purple][i]);
    cylinder(1.52,1.52,.15,data.x,1.31,-.7,palette.cream);
    const e=egg(data.x,2.4,-.7,data.kind,.8);kioskEggs.push(e);animated.push({kind:'egg',object:e,baseY:2.4,phase:i*1.8});
    label(data.text,data.x,.76,.79,2.6,.48,'#34516b','#fff8e7');
    pad(data.x,1.25,['gold','cyan','purple'][i],1.18);
    interaction(data.id,'egg',data.name,data.x,0,{eggId:data.id});
  });
  // A radiant center monument behind the stalls anchors the skyline.
  cylinder(2.5,2.9,.4,0,.28,-8,palette.cream);cylinder(2.05,2.3,1.0,0,.95,-8,palette.violet);
  const heroEgg=egg(0,3.4,-8,'cosmic',1.5);animated.push({kind:'heroEgg',object:heroEgg,baseY:3.4,phase:0});
  ring(3,.12,0,1.5,-8,palette.goldGlow);
  label('VEDOMOSTI SÚ TVOJA SUPERSILA',0,1.1,-5.75,5.5,.57,'#fff8e7','#7160ae');

  // Player base: generous open frontage, hatch chamber, upgrade station and pets.
  box(18,.12,16,-17,-.055,11,palette.mint);
  box(18,.04,16,-17,.01,11,palette.cream);
  box(17.6,.02,15.6,-17,.03,11,palette.mint);
  box(18,2.0,.45,-17,1.04,3,palette.teal);collide(-17,3,9,.3);
  box(.45,1.5,13,-25.7,.79,9.8,palette.teal);collide(-25.7,9.8,.27,6.5);
  // The east entrance aligns with the plaza road, so the base is directly reachable from spawn.
  [{z:6.35,length:6.1},{z:16.05,length:.5}].forEach(part=>{
    box(.45,1.5,part.length,-8.3,.79,part.z,palette.teal);collide(-8.3,part.z,.27,part.length/2);
  });
  [-25.7,-8.3].forEach(x=>{box(.65,5,.65,x,2.54,18,palette.teal);collide(x,18,.4,.4);});
  box(18.3,.45,.75,-17,5.1,18,palette.teal);label('TVOJA ZÁKLADŇA',-17,5.1,18.41,12,1.0,'#fff9e9','#43a89a');
  // Incubator silhouette with translucent glass and a glowing circular hatch area.
  const hatchGroup=new THREE.Group();hatchGroup.position.set(-16,-.42,7);root.add(hatchGroup);
  cylinder(2.1,2.3,.65,0,.78,0,palette.teal,hatchGroup);cylinder(2.15,2.15,.16,0,1.2,0,palette.cream,hatchGroup);
  const glass=mat('#c5f5f8',{transparent:true,opacity:.20,roughness:.15,metalness:.05,depthWrite:false});
  cylinder(1.65,1.65,2.8,0,2.7,0,glass,hatchGroup,32);
  ring(1.77,.12,0,1.33,0,palette.cyanGlow,hatchGroup);ring(1.77,.12,0,4.15,0,palette.cyanGlow,hatchGroup);
  cylinder(1.85,1.85,.3,0,4.38,0,palette.teal,hatchGroup);
  [-1,1].forEach(a=>box(.22,2.6,.22,a*1.6,2.7,-.7,palette.cream,hatchGroup));
  const hatchEgg=egg(0,2.5,0,'basic',.76,hatchGroup);animated.push({kind:'egg',object:hatchEgg,baseY:2.5,phase:2});
  label('LIAHEŇ',-16,4.48,7.05,4,.7,'#fff8e9','#42a99b');
  pad(-16,9.6,'cyan',1.5);interaction('hatch','hatch','Vyliahni vajíčko',-16,7);
  box(3.3,1.1,2.4,-22,.59,7,palette.violet);box(3.5,.2,2.6,-22,1.26,7,palette.purple);
  box(2.25,1.7,.24,-22,2.24,6.65,palette.ink);box(2,1.45,.26,-22,2.24,6.68,palette.purpleGlow);
  label('VYLEPŠENIA',-22,3.49,7.0,4,.68,'#fff8e9','#7865bc');star(root,-22,2.34,6.83,.44,palette.cream);
  interaction('upgrade','upgrade','Vylepši svoju základňu',-22,7);
  const petDisplay=new THREE.Group();petDisplay.position.set(-16,.09,13.5);root.add(petDisplay);
  [-4.5,0,4.5].forEach(x=>{cylinder(1.25,1.4,.18,x,.06,0,palette.white,petDisplay);});
  label('TVOJI MALÍ POMOCNÍCI',-16,.72,15.8,8,.56,'#438878','#fff8e7');
  // Base income pipe and coin fountain communicates idle income at a glance.
  box(2.7,.9,2,-10.8,.49,6.5,palette.gold);label('+ ENERGIA',-10.8,.52,7.53,2.7,.47,'#70582f','#ffecae');
  const coinOrbit=new THREE.Group();coinOrbit.position.set(-10.8,1.59,6.5);root.add(coinOrbit);
  for(let i=0;i<5;i++){const coin=cylinder(.25,.25,.09,Math.cos(i*Math.PI*2/5)*.85,.2+Math.sin(i)*.1,Math.sin(i*Math.PI*2/5)*.85,palette.goldGlow,coinOrbit);coin.rotation.z=Math.PI/2;}
  animated.push({kind:'coins',object:coinOrbit,baseY:1.59,phase:0});

  const terminal = (x,z,color,title,subtitle,zone,id) => {
    pad(x,z,color,2.35);
    box(1.75,1.0,1.1,x,.75,z-.65,palette[color]);box(1.95,.16,1.25,x,1.3,z-.65,palette.cream);
    const screen=box(1.4,1.15,.18,x,1.95,z-.86,palette.ink);screen.rotation.x=-.10;
    box(1.2,.92,.20,x,1.96,z-.835,palette[color+'Glow']||palette.goldGlow);
    label('?',x,2.04,z-.72,.54,.57,'#fffef6','rgba(0,0,0,0)');
    label(title,x,3.3,z-.65,5.1,.78,'#29445b','#fff9e9');
    label(subtitle,x,.47,z+1.94,4.1,.44,'#47667d','#fff9e9');
    interaction(id,'quiz',title,x,z,{zone});
  };
  // Mathematics pavilion: visibly three-dimensional equation blocks and hanging forms.
  terminal(16,4,'gold','MATEMATIKA','ČÍSLA • LOGIKA • RYTMUS','math','quiz-math');
  box(7.6,.4,7.0,23.4,.24,3,palette.cream);box(7.3,4.3,6.6,23.4,2.6,2.9,palette.peach);collide(23.4,2.9,3.8,3.45);
  box(8.2,.6,7.5,23.4,5.0,2.9,palette.coral);
  [21.2,25.6].forEach(x=>windowPane(x,2.65,6.24,1.6,1.9));
  label('DOM LOGIKY',23.4,4.5,6.72,6,.84,'#885c52','#fff0d6');
  const cube=box(1.6,1.6,1.6,23.4,6.55,2.9,palette.gold);cube.rotation.set(.35,.5,.1);animated.push({kind:'spin',object:cube,phase:0});
  label('1 + 2 = 3',23.4,2.4,6.32,3.0,.6,'#655a4c','#ffe2a5');
  // Science building: blue domed observatory with an original atom sculpture.
  terminal(16,-16,'cyan','VEDA','POKUSY • VESMÍR • OBJAVY','science','quiz-science');
  box(8,.4,8,24.5,.22,-17,palette.cream);box(7.4,4,7.4,24.5,2.42,-17,palette.blue);collide(24.5,-17,3.85,3.85);
  const dome=new THREE.Mesh(new THREE.SphereGeometry(4.0,24,12,0,Math.PI*2,0,Math.PI/2),palette.teal);dome.position.set(24.5,4.6,-17);root.add(dome);dome.castShadow=true;
  [22.2,26.8].forEach(x=>windowPane(x,2.4,-13.22,1.5,1.7));label('LABORATÓRIUM',24.5,4.1,-12.9,6.3,.8,'#225966','#e8fcff');
  const atom=new THREE.Group();atom.position.set(24.5,6.6,-17);root.add(atom);sphere(.48,0,0,0,palette.goldGlow,atom);
  for(let i=0;i<3;i++){const orbit=ring(1.8,.055,0,0,0,palette.cyanGlow,atom,true);orbit.rotation.set(i*Math.PI/3,.65+i*.55,0);sphere(.18,Math.cos(i*2)*1.7,Math.sin(i*2)*1.7,0,palette.cream,atom);}
  animated.push({kind:'spin',object:atom,phase:1});
  // Nature zone: greenhouse facade, giant seed and garden beds.
  terminal(-18,-16,'purple','PRÍRODA','ŽIVOT • PLANÉTA • EKOLÓGIA','nature','quiz-nature');
  box(7.8,.4,7.6,-26,.22,-18,palette.cream);box(7.3,3.9,7.1,-26,2.4,-18,palette.mint);collide(-26,-18,3.8,3.7);
  const roof=box(8.4,.4,8,-26,4.63,-18,palette.teal);roof.rotation.z=-.07;
  [-28.15,-24].forEach(x=>windowPane(x,2.5,-14.37,1.7,2.1));label('ZÁHRADA OBJAVOV',-26,4.0,-14.13,6.7,.78,'#377a69','#ebfff0');
  cylinder(.17,.27,2.8,-26,6.0,-18,palette.leaf);
  const leafA=sphere(.9,-26.7,6.3,-18,palette.leafLight,root,[1.5,.45,.65]);leafA.rotation.z=.42;
  const leafB=sphere(.9,-25.3,6.8,-18,palette.leaf,root,[1.5,.45,.65]);leafB.rotation.z=-.42;
  [-31.5,-20.5].forEach(x=>{box(2.6,.5,5,x,.45,-20,palette.brown);box(2.3,.12,4.7,x,.76,-20,palette.dark);for(let z=-21.5;z<=-18.5;z+=1){sphere(.45,x,.95,z,palette.leafLight);star(root,x,1.42,z+.15,.18,palette.gold);}});

  // Astral gate: depth in the distance and a visible future goal.
  pad(0,-30,'purple',4.0);box(10,.35,7,0,.22,-32,palette.violet);
  [-3.7,3.7].forEach(x=>{box(1.05,7.5,1.15,x,4.05,-32.6,palette.violet);box(1.45,.45,1.6,x,7.9,-32.6,palette.gold);collide(x,-32.6,.62,.65);});
  box(8.5,1.0,1.3,0,7.48,-32.6,palette.violet);
  const portalRing=ring(3.1,.21,0,4.0,-32.5,palette.purpleGlow,root,true);
  const portalSurface=new THREE.Mesh(new THREE.CircleGeometry(2.92,64),new THREE.MeshBasicMaterial({color:'#ad85ed',transparent:true,opacity:.24,side:THREE.DoubleSide,depthWrite:false}));portalSurface.position.set(0,4,-32.57);root.add(portalSurface);
  const portalSparkles=new THREE.Group();portalSparkles.position.set(0,4,-32.4);root.add(portalSparkles);
  for(let i=0;i<12;i++){const a=i*Math.PI*2/12;star(portalSparkles,Math.cos(a)*2.85,Math.sin(a)*2.85,0,.12,i%2?palette.goldGlow:palette.cyanGlow);}
  animated.push({kind:'portal',object:portalSparkles,phase:0});
  label('ASTRÁLNA BRÁNA',0,8.7,-32.48,8,1.0,'#fff9ee','#7f62be');
  label('3 OBLASTI · JEDEN VEĽKÝ OBJAV',0,.95,-28.36,6,.6,'#634c95','#f0e3ff');
  interaction('portal-astral','portal','Astrálna brána',0,-30);

  // Banners and plaza furniture, placed away from the traversal lanes.
  const banner = (x,z,text,color,rot=0) => {
    cylinder(.08,.12,6.1,x,3.05,z,palette.cream);
    const g=new THREE.Group();g.position.set(x+.88,4.95,z);g.rotation.y=rot;root.add(g);
    box(1.65,2.05,.06,0,0,0,palette[color],g);label(text,0,.13,.042,1.5,.65,'#fff8ea','rgba(0,0,0,0)',g);star(g,0,-.57,.055,.26,palette.cream);
    collide(x,z,.16,.16);
  };
  banner(-10,-5,'01','coral');banner(10,-5,'02','teal');banner(-6,-24,'03','violet');banner(6,-24,'04','violet');
  bench(-8,-7,.4);bench(8,-7,-.4);bench(11,11,-Math.PI/2);bench(-30,4,Math.PI/2);
  [[-7,21],[7,21],[-11,-12],[11,-12],[-7,-25],[7,-25],[33,4],[-34,4]].forEach(([x,z])=>lamp(x,z));
  // Large stylized tree borders frame the campus. No trunk interrupts a subject pad.
  const treePositions=[[-36,18,1.6],[-31,24,1.3],[-25,29,1.2],[-17,31,1.55],[-7,30,1.3],[8,30,1.3],[16,28,1.55],[27,24,1.4],[34,16,1.7],[36,7,1.35],[35,-5,1.5],[34,-15,1.4],[28,-27,1.6],[17,-29,1.2],[-17,-30,1.35],[-30,-29,1.6],[-37,-18,1.6],[-36,-7,1.25],[-36,4,1.3],[-30,13,1.2],[-7,-19,.85],[8,-19,.9]];
  treePositions.forEach(([x,z,s],i)=>tree(x,z,s,i%3===0?1:0));
  [[-12,24],[12,24],[31,10],[-33,9],[-12,-25],[12,-25]].forEach(([x,z])=>{
    cylinder(1.1,1.3,.45,x,.22,z,palette.cream);hedge(x,z,1.7,1.7);for(let i=0;i<4;i++)sphere(.14,x+Math.cos(i*1.7)*.6,1.02,z+Math.sin(i*1.7)*.6,i%2?palette.peach:palette.gold);
  });
  // Friendly decorative students populate landmarks without changing gameplay.
  kid(-6,6,'#f79a81',-.6);kid(9,2,'#69b5ce',.9);kid(11,-18,'#a78adb',-1.0);kid(-12,-17,'#f1bf63',.65);kid(-20,16,'#8dbcb0',-.4);
  // Learning trail stepping stones beyond the main path.
  for(let i=0;i<9;i++){const x=37+Math.sin(i*.8)*2, z=22-i*3;box(2.3,.5+(i%3)*.18,2.2,x,.23+(i%3)*.09,z,i%2?palette.purple:palette.gold);}
  label('ĽAHKO? SKÚS SKOK!',37,2.4,23.5,6,.75,'#516677','#fff4dc');
  // Far landscape and floating clouds create a complete toy-world horizon.
  [-47,-40,40,47].forEach((x,i)=>{
    const hill=new THREE.Mesh(new THREE.ConeGeometry(8+i%2*3,9+i%2*4,5),palette.leafLight);hill.position.set(x,3,-46+i%2*5);root.add(hill);
  });
  [[-35,20,-37],[17,24,-42],[35,21,19],[-30,23,25],[-5,26,-48]].forEach(([x,y,z])=>{
    const cloud=new THREE.Group();cloud.position.set(x,y,z);root.add(cloud);
    box(5.4,1.25,2.5,0,0,0,palette.white,cloud);box(3.2,1.2,2.8,-.7,.7,0,palette.white,cloud);box(2.1,.8,2,2,-.3,0,palette.white,cloud);
    animated.push({kind:'cloud',object:cloud,baseX:x,phase:z});
  });
  // Suspended pennants across the spawn road.
  const poleX=[-6.6,6.6];poleX.forEach(x=>{cylinder(.1,.13,6.8,x,3.4,23,palette.teal);collide(x,23,.16,.16);});
  box(13.2,.055,.055,0,6.65,23,palette.brown);
  for(let i=0;i<9;i++){
    const points=new Float32Array([-.45,0,0,.45,0,0,0,-.7,0]);const geo=new THREE.BufferGeometry();geo.setAttribute('position',new THREE.BufferAttribute(points,3));geo.computeVertexNormals();
    const flag=new THREE.Mesh(geo,new THREE.MeshStandardMaterial({color:['#ffc957','#91d3ee','#b997ed','#ef807d'][i%4],side:THREE.DoubleSide}));flag.position.set(-5.3+i*1.3,6.57,23);root.add(flag);
  }

  let lastPetSignature='', activeState={};
  const makePet = (index,pet) => {
    const g=new THREE.Group();g.position.set(((index%3)-1)*4.5,0,-Math.floor(index/3)*2.5);petDisplay.add(g);
    const color=pet?.color || ['#ffc86e','#91ddea','#b99be7','#a2dcae','#f4a6ac'][index%5];const pmat=mat(color);
    box(1.35,1.15,1.05,0,.83,0,pmat,g);box(1.47,1.05,1.2,0,1.8,.06,pmat,g);
    [-.42,.42].forEach(xx=>{box(.29,.43,.32,xx,2.43,-.10,pmat,g);box(.14,.23,.05,xx,2.44,.08,palette.peach,g);box(.14,.2,.035,xx*.63,1.92,.68,palette.ink,g);box(.31,.23,.5,xx,.18,.08,palette.cream,g);});
    box(.22,.11,.05,0,1.70,.69,palette.cream,g);star(g,0,.9,.55,.21,palette.cream);
    return g;
  };
  const setState = state => {
    activeState=state||{};
    const pets=Array.isArray(activeState.pets)?activeState.pets:[];
    const signature=JSON.stringify(pets.map(p=>p?.id||p?.name||p?.type||p));
    if(signature!==lastPetSignature){
      lastPetSignature=signature;
      // Keep the three display podiums; replace only pets we own.
      for(let i=petDisplay.children.length-1;i>=0;i--)if(petDisplay.children[i].userData.pet)petDisplay.remove(petDisplay.children[i]);
      pets.slice(0,6).forEach((pet,i)=>{const object=makePet(i,pet);object.userData.pet=true;object.userData.baseY=object.position.y;object.userData.phase=i;});
    }
    const unlocked=!!(activeState.progress?.unlocked?.includes('astral')||activeState.portalUnlocked||activeState.astralUnlocked||activeState.unlocks?.astral||(activeState.zones?.math?.mastery>=3&&activeState.zones?.science?.mastery>=3&&activeState.zones?.nature?.mastery>=3));
    portalSurface.material.opacity=unlocked?.56:.24;
    portalRing.material.emissiveIntensity=unlocked?1.4:.7;
  };
  const update = (dt,time,state) => {
    if(state)setState(state);
    animated.forEach(item=>{
      const o=item.object,p=item.phase||0;
      if(item.kind==='egg'||item.kind==='heroEgg'){o.position.y=item.baseY+Math.sin(time*1.6+p)*.12;o.rotation.y=time*(item.kind==='heroEgg'?.18:.28)+p;}
      else if(item.kind==='spin'){o.rotation.y+=dt*.38;o.rotation.z=Math.sin(time*.35+p)*.1;}
      else if(item.kind==='portal'){o.rotation.z=-time*.12;}
      else if(item.kind==='coins'){o.rotation.y=time*.65;o.position.y=item.baseY+Math.sin(time*2)*.1;}
      else if(item.kind==='npc'){o.position.y=Math.sin(time*1.8+p)*.025;}
      else if(item.kind==='cloud'){o.position.x=item.baseX+Math.sin(time*.035+p)*2;}
    });
    petDisplay.children.forEach(o=>{if(o.userData.pet){o.position.y=o.userData.baseY+Math.sin(time*2+o.userData.phase)*.10;o.rotation.y=Math.sin(time*.8+o.userData.phase)*.08;}});
  };
  setState({});
  return { root,colliders,interactables,animated,update,setState,spawn:{x:0,z:13},bounds:{minX:-49,maxX:49,minZ:-49,maxZ:49} };
}
