import * as THREE from './vendor/three.module.js';
import {createCompanion} from './avatar3d.js';
import {addAcademyLandmarks,addCampusSurfaceDetail} from './academy-landmarks.js';

/** Original, procedural Academy world. Ground is y=0; all interactions stay walkable. */
export function createAcademyWorld(scene) {
  const root = new THREE.Group();
  root.name = 'AcademyWorld';
  scene.add(root);
  const colliders = [], cameraOccluders = [], interactables = [], animated = [], platforms = [], collectibles = [], labels = [];
  const windTime={value:0}, daylight={value:1};
  // Hundreds of toy-world pieces share a handful of GPU buffers and materials.
  const materials = new Map(), geometryCache = new Map();
  const mat = (color, opts = {}) => {
    const key = JSON.stringify([color, opts]);
    if (!materials.has(key)) materials.set(key, new THREE.MeshStandardMaterial({ color, roughness: 0.82, ...opts }));
    return materials.get(key);
  };
  const geometry = (key, create) => {
    if (!geometryCache.has(key)) geometryCache.set(key, create());
    return geometryCache.get(key);
  };
  const cubeGeometry = new THREE.BoxGeometry(1, 1, 1);
  const sphereGeometry = new THREE.SphereGeometry(1, 16, 10);
  const palette = {
    grass: mat('#4aa676'), mint: mat('#a2d4ae'), road: mat('#e6cd98'), edge: mat('#fff0c5'),
    stone: mat('#8299ae'), ink: mat('#22354e'), cream: mat('#fff1d7'), peach: mat('#eaa278'),
    coral: mat('#d76556'), purple: mat('#ae88e0'), violet: mat('#5e4a97'), blue: mat('#5ab4d6'),
    teal: mat('#238f89'), cyan: mat('#73cee2'), gold: mat('#f4b735'), brown: mat('#825b46'), dark: mat('#365f50'),
    white: mat('#fffcf0'), leaf: mat('#268568'), leafLight: mat('#55b27d'),
    sand:mat('#d7b878'), cliff:mat('#617675'), clay:mat('#c99369'), meadow:mat('#5caa76'), scienceLawn:mat('#6aaab3'),
    autumn:mat('#dc9e43'), scienceLeaf:mat('#438e9f'), copper:mat('#ad6943'),
    cyanGlow: mat('#64e5ee', { emissive: '#39c9d2', emissiveIntensity: 0.7 }),
    goldGlow: mat('#ffe180', { emissive: '#ffd15c', emissiveIntensity: 0.55 }),
    purpleGlow: mat('#c8afff', { emissive: '#8963eb', emissiveIntensity: 0.7 }),
  };
  [palette.grass,palette.meadow,palette.scienceLawn].forEach(material=>addCampusSurfaceDetail(material,'grass'));
  [palette.road,palette.stone].forEach(material=>addCampusSurfaceDetail(material,'stone'));
  addCampusSurfaceDetail(palette.cliff,'rock');
  // Vertex wind also works on instanced plants, without CPU updates to hundreds of blades.
  [palette.leaf,palette.leafLight,palette.scienceLeaf,palette.autumn].forEach(material=>{
    material.onBeforeCompile=shader=>{
      shader.uniforms.uWindTime=windTime;
      shader.vertexShader=`uniform float uWindTime;\n${shader.vertexShader}`.replace('#include <begin_vertex>',`#include <begin_vertex>
        vec3 plantPosition = position;
        #ifdef USE_INSTANCING
          plantPosition = (instanceMatrix * vec4(position, 1.0)).xyz;
        #endif
        float sway = sin(uWindTime * 1.25 + plantPosition.x * .7 + plantPosition.z * .4) * .035;
        transformed.x += sway * max(position.y + .5, 0.0);
        transformed.z += sway * .45 * max(position.y + .5, 0.0);`);
    };
    material.customProgramCacheKey=()=> 'academy-plant-wind-v1';
  });
  const box = (w,h,d,x,y,z,m,parent=root) => {
    const mesh = new THREE.Mesh(cubeGeometry, typeof m === 'string' ? mat(m) : m);
    mesh.scale.set(w,h,d);mesh.position.set(x,y,z); mesh.castShadow = h > .12; mesh.receiveShadow = true; parent.add(mesh); return mesh;
  };
  const cylinder = (r1,r2,h,x,y,z,m,parent=root,segments=12) => {
    const mesh = new THREE.Mesh(geometry(`c:${r1}:${r2}:${h}:${segments}`, () => new THREE.CylinderGeometry(r1,r2,h,segments)), m);
    mesh.position.set(x,y,z); mesh.castShadow=h>.16 && !m.transparent; mesh.receiveShadow=true; parent.add(mesh); return mesh;
  };
  const sphere = (r,x,y,z,m,parent=root,scale=null) => {
    const mesh=new THREE.Mesh(sphereGeometry,m);
    mesh.position.set(x,y,z); mesh.scale.set(r*(scale?.[0]||1),r*(scale?.[1]||1),r*(scale?.[2]||1)); mesh.castShadow=r>.2; mesh.receiveShadow=true; parent.add(mesh); return mesh;
  };
  const ring = (radius,tube,x,y,z,m,parent=root,vertical=false) => {
    const mesh = new THREE.Mesh(geometry(`r:${radius}:${tube}`, () => new THREE.TorusGeometry(radius,tube,6,40)),m);
    mesh.position.set(x,y,z); if(!vertical)mesh.rotation.x=-Math.PI/2; parent.add(mesh); return mesh;
  };
  const collide = (x,z,hx,hz,top=3) => colliders.push({x,z,hx,hz,top});
  // Elevated beams obstruct the camera without becoming invisible walls for walkers below.
  const cameraBlock = (x,z,hx,hz,bottom,top) => cameraOccluders.push({x,z,hx,hz,bottom,top});
  const interaction = (id,type,name,x,z,extra={}) => interactables.push({id,type,name,position:{x,z},radius:3,...extra});
  const batchGroup = (parent,skip=[]) => {
    parent.updateMatrixWorld(true);const inverse=new THREE.Matrix4().copy(parent.matrixWorld).invert(),groups=new Map(),excluded=new Set(skip);
    parent.traverse(object=>{if(!object.isMesh||object.isInstancedMesh||object.material.transparent)return;for(let p=object;p&&p!==parent;p=p.parent)if(excluded.has(p))return;const key=`${object.geometry.uuid}:${object.material.uuid}:${object.castShadow}:${object.receiveShadow}`;if(!groups.has(key))groups.set(key,[]);groups.get(key).push(object);});
    groups.forEach(objects=>{if(objects.length<2)return;const first=objects[0],mesh=new THREE.InstancedMesh(first.geometry,first.material,objects.length),matrix=new THREE.Matrix4();mesh.castShadow=first.castShadow;mesh.receiveShadow=first.receiveShadow;mesh.name='AnimatedPropBatch';objects.forEach((object,index)=>{matrix.multiplyMatrices(inverse,object.matrixWorld);mesh.setMatrixAt(index,matrix);object.removeFromParent();});mesh.computeBoundingSphere();parent.add(mesh);});
  };
  const label = (text,x,y,z,width=5,height=1.1,color='#293d52',bg='#fff6de',parent=root) => {
    const canvas=document.createElement('canvas'); canvas.width=512; canvas.height=128;
    const ctx=canvas.getContext('2d');
    ctx.fillStyle=bg; ctx.beginPath(); ctx.roundRect(4,4,504,120,21); ctx.fill();
    ctx.strokeStyle='rgba(255,255,255,.8)';ctx.lineWidth=5;ctx.stroke();
    ctx.fillStyle=color;ctx.font='900 38px Arial, sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';
    const words=text.split('\n'); words.forEach((s,i)=>ctx.fillText(s,256,words.length===1?64:42+i*42,470));
    const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;
    const mesh=new THREE.Mesh(new THREE.PlaneGeometry(width,height),new THREE.MeshBasicMaterial({map:texture,side:THREE.DoubleSide,transparent:true}));
    mesh.position.set(x,y,z);parent.add(mesh);labels.push(mesh);return mesh;
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
    const leaves=x>20?(z<-8?palette.scienceLeaf:palette.autumn):palette.leaf;
    cylinder(.24*size,.32*size,2*size,x,size,z,palette.brown);
    if(style===1){
      cylinder(0,1.45*size,2.2*size,x,2.1*size,z,leaves,root,5);
      cylinder(0,1.15*size,1.9*size,x,3.1*size,z,palette.leafLight,root,5);
    } else {
      const crown = box(2.5*size,2.3*size,2.4*size,x,2.9*size,z,leaves); crown.rotation.y=Math.PI/7;
      box(1.8*size,1.1*size,1.7*size,x+.35*size,4*size,z-.15*size,palette.leafLight);
    }
    collide(x,z,.35*size,.35*size,4.6*size);
  };
  const hedge = (x,z,w=2,d=1) => {box(w,.8,d,x,.4,z,palette.leafLight);};
  const bench = (x,z,rot=0) => {
    const g=new THREE.Group();g.position.set(x,0,z);g.rotation.y=rot;root.add(g);
    box(3,.20,.75,0,.65,0,palette.brown,g);box(3,.65,.18,0,1.1,-.3,palette.peach,g);
    [-1.1,1.1].forEach(a=>box(.16,.65,.65,a,.32,0,palette.ink,g));
  };
  const lamp = (x,z) => {
    cylinder(.1,.13,3.7,x,1.85,z,palette.ink);box(.7,.15,.7,x,3.68,z,palette.ink);
    sphere(.3,x,3.45,z,palette.goldGlow);collide(x,z,.16,.16,3.8);
  };
  const star = (parent,x,y,z,scale=.35,m=palette.gold) => {
    const geo=geometry('star',()=>{const shape=new THREE.Shape();for(let i=0;i<10;i++){const angle=Math.PI/2+i*Math.PI/5,rr=i%2===0?1:.44;const xx=Math.cos(angle)*rr,yy=Math.sin(angle)*rr;i?shape.lineTo(xx,yy):shape.moveTo(xx,yy);}shape.closePath();return new THREE.ExtrudeGeometry(shape,{depth:.1,bevelEnabled:false});});
    const mesh=new THREE.Mesh(geo,m);mesh.position.set(x,y,z);mesh.scale.set(scale,scale,1);parent.add(mesh);return mesh;
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
    batchGroup(group);return group;
  };
  const kid = (x,z,color,rot=0,role=0) => {
    const g=new THREE.Group();g.position.set(x,0,z);g.rotation.y=rot;root.add(g);
    box(.72,.82,.4,0,1.08,0,mat(color),g);box(.62,.62,.6,0,1.82,0,palette.peach,g);
    box(.67,.17,.64,0,2.1,-.035,palette.brown,g);box(.68,.25,.15,0,1.99,-.3,palette.brown,g);
    [-.21,.21].forEach(xx=>{box(.23,.62,.26,xx,.45,0,palette.ink,g);box(.29,.19,.45,xx,.1,.08,palette.white,g);box(.14,.74,.23,xx*2.4,1.03,0,palette.peach,g);box(.075,.09,.025,xx*.65,1.84,.31,palette.ink,g);});
    box(.14,.035,.035,0,1.66,.315,palette.ink,g);
    if(role===0){box(.6,.7,.28,0,1.15,-.33,palette.coral,g);box(.15,.2,.07,0,1.2,-.51,palette.gold,g);}
    if(role===1){box(.79,.13,.72,0,2.2,0,palette.gold,g);box(.49,.31,.47,0,2.4,0,palette.gold,g);}
    if(role===2){box(.56,.22,.06,0,1.86,.35,palette.cream,g);[-.13,.13].forEach(xx=>box(.18,.15,.075,xx,1.86,.39,palette.blue,g));}
    if(role===3){box(.62,.68,.05,0,1.02,.24,palette.cream,g);box(.55,.13,.7,0,2.2,0,palette.leaf,g);}
    if(role===4){box(.34,.44,.11,.53,.95,.13,palette.purple,g);box(.28,.37,.02,.53,.95,.20,palette.cream,g);}
    batchGroup(g);
    animated.push({kind:'npc',object:g,baseY:0,phase:x+z});return g;
  };

  // The full walkable campus rests on a coastal island, with a warm shelf above the sea.
  cylinder(71,71,.5,0,-.28,0,palette.grass,root,96);
  cylinder(74.5,77,.9,0,-.64,0,palette.sand,root,96);
  cylinder(77,70,3,0,-2.55,0,palette.cliff,root,64);
  const oceanUniforms={uTime:windTime,uDaylight:daylight};
  const oceanMaterial=new THREE.ShaderMaterial({uniforms:oceanUniforms,vertexShader:`
    uniform float uTime; varying vec3 seaPosition;
    void main(){vec3 p=position;p.z+=sin(p.x*.052+uTime*.48)*cos(p.y*.044-uTime*.29)*.13;
    seaPosition=(modelMatrix*vec4(p,1.)).xyz;gl_Position=projectionMatrix*modelViewMatrix*vec4(p,1.);}`,
    fragmentShader:`uniform float uTime;uniform float uDaylight;varying vec3 seaPosition;
    void main(){float distanceToIsland=length(seaPosition.xz);float shallow=1.-smoothstep(74.,112.,distanceToIsland);
    vec3 color=mix(vec3(.045,.29,.44),vec3(.12,.61,.62),shallow);
    float ripple=.5+.5*sin(seaPosition.x*.24+uTime*.72)*sin(seaPosition.z*.2-uTime*.51);
    color+=pow(ripple,14.)*vec3(.17,.26,.25);
    float shore=1.-smoothstep(.25,1.9,abs(distanceToIsland-77.+sin(uTime+seaPosition.x*.11)*.3));
    color=mix(color,vec3(.83,.96,.89),shore*.5);gl_FragColor=vec4(color*(.35+.65*uDaylight),1.);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
    }`});
  const ocean=new THREE.Mesh(new THREE.PlaneGeometry(420,420,32,32),oceanMaterial);ocean.rotation.x=-Math.PI/2;ocean.position.y=-.68;ocean.name='LivingOcean';root.add(ocean);
  // District grounds and planting colors make each learning landmark recognizable at a distance.
  cylinder(13.7,13.7,.018,24,.009,3,palette.clay,root,48);
  cylinder(13,13,.018,24,.009,-18,palette.scienceLawn,root,48);
  cylinder(14,14,.018,-26,.009,-18,palette.meadow,root,48);
  cylinder(10.5,10.5,.018,0,.008,-34,palette.violet,root,32);
  // Grass, roads and plaza: an intentionally open navigation network.
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
  [-6.5,6.5].forEach(x=>{box(.35,5,.35,x,2.8,-3.1,palette.teal);collide(x,-3.1,.24,.24,5.65);});
  box(15,.42,4.9,0,5.35,-1.5,palette.teal);
  cameraBlock(0,-1.5,7.5,2.5,5.14,5.67);
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
  box(18,2.0,.45,-17,1.04,3,palette.teal);collide(-17,3,9,.3,2.1);
  box(.45,1.5,13,-25.7,.79,9.8,palette.teal);collide(-25.7,9.8,.27,6.5,1.55);
  // The east entrance aligns with the plaza road, so the base is directly reachable from spawn.
  [{z:6.35,length:6.1},{z:16.05,length:.5}].forEach(part=>{
    box(.45,1.5,part.length,-8.3,.79,part.z,palette.teal);collide(-8.3,part.z,.27,part.length/2,1.55);
  });
  [-25.7,-8.3].forEach(x=>{box(.65,5,.65,x,2.54,18,palette.teal);collide(x,18,.4,.4,5.4);});
  box(18.3,.45,.75,-17,5.1,18,palette.teal);label('TVOJA ZÁKLADŇA',-17,5.1,18.41,12,1.0,'#fff9e9','#43a89a');
  cameraBlock(-17,18,9.15,.375,4.875,5.325);
  // Incubator silhouette with translucent glass and a glowing circular hatch area.
  const hatchGroup=new THREE.Group();hatchGroup.position.set(-16,-.42,7);root.add(hatchGroup);
  cylinder(2.1,2.3,.65,0,.78,0,palette.teal,hatchGroup);cylinder(2.15,2.15,.16,0,1.2,0,palette.cream,hatchGroup);
  const glass=mat('#c5f5f8',{transparent:true,opacity:.20,roughness:.15,metalness:.05,depthWrite:false});
  cylinder(1.65,1.65,2.8,0,2.7,0,glass,hatchGroup,32);
  ring(1.77,.12,0,1.33,0,palette.cyanGlow,hatchGroup);ring(1.77,.12,0,4.15,0,palette.cyanGlow,hatchGroup);
  cylinder(1.85,1.85,.3,0,4.38,0,palette.teal,hatchGroup);
  cameraBlock(-16,7,1.85,1.85,3.81,4.11);
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
  batchGroup(coinOrbit);

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
  box(7.6,.4,7.0,23.4,.24,3,palette.cream);box(7.3,4.3,6.6,23.4,2.6,2.9,palette.peach);collide(23.4,2.9,3.8,3.45,6.1);
  box(8.2,.6,7.5,23.4,5.0,2.9,palette.coral);
  [21.2,25.6].forEach(x=>windowPane(x,2.65,6.24,1.6,1.9));
  label('DOM LOGIKY',23.4,4.5,6.72,6,.84,'#885c52','#fff0d6');
  const cube=box(1.6,1.6,1.6,23.4,6.55,2.9,palette.gold);cube.rotation.set(.35,.5,.1);animated.push({kind:'spin',object:cube,phase:0});
  label('1 + 2 = 3',23.4,2.4,6.32,3.0,.6,'#655a4c','#ffe2a5');
  // Science building: blue domed observatory with an original atom sculpture.
  terminal(16,-16,'cyan','VEDA','POKUSY • VESMÍR • OBJAVY','science','quiz-science');
  box(8,.4,8,24.5,.22,-17,palette.cream);box(7.4,4,7.4,24.5,2.42,-17,palette.blue);collide(24.5,-17,3.85,3.85,8.7);
  const dome=new THREE.Mesh(new THREE.SphereGeometry(4.0,24,12,0,Math.PI*2,0,Math.PI/2),palette.teal);dome.position.set(24.5,4.6,-17);root.add(dome);dome.castShadow=true;
  [22.2,26.8].forEach(x=>windowPane(x,2.4,-13.22,1.5,1.7));label('LABORATÓRIUM',24.5,4.1,-12.9,6.3,.8,'#225966','#e8fcff');
  const atom=new THREE.Group();atom.position.set(24.5,6.6,-17);root.add(atom);sphere(.48,0,0,0,palette.goldGlow,atom);
  for(let i=0;i<3;i++){const orbit=ring(1.8,.055,0,0,0,palette.cyanGlow,atom,true);orbit.rotation.set(i*Math.PI/3,.65+i*.55,0);sphere(.18,Math.cos(i*2)*1.7,Math.sin(i*2)*1.7,0,palette.cream,atom);}
  animated.push({kind:'spin',object:atom,phase:1});
  // Nature zone: greenhouse facade, giant seed and garden beds.
  terminal(-18,-16,'purple','PRÍRODA','ŽIVOT • PLANÉTA • EKOLÓGIA','nature','quiz-nature');
  box(7.8,.4,7.6,-26,.22,-18,palette.cream);box(7.3,3.9,7.1,-26,2.4,-18,palette.mint);collide(-26,-18,3.8,3.7,4.9);
  const roof=box(8.4,.4,8,-26,4.63,-18,palette.teal);roof.rotation.z=-.07;
  [-28.15,-24].forEach(x=>windowPane(x,2.5,-14.37,1.7,2.1));label('ZÁHRADA OBJAVOV',-26,4.0,-14.13,6.7,.78,'#377a69','#ebfff0');
  cylinder(.17,.27,2.8,-26,6.0,-18,palette.leaf);
  const leafA=sphere(.9,-26.7,6.3,-18,palette.leafLight,root,[1.5,.45,.65]);leafA.rotation.z=.42;
  const leafB=sphere(.9,-25.3,6.8,-18,palette.leaf,root,[1.5,.45,.65]);leafB.rotation.z=-.42;
  [-31.5,-20.5].forEach(x=>{box(2.6,.5,5,x,.45,-20,palette.brown);box(2.3,.12,4.7,x,.76,-20,palette.dark);for(let z=-21.5;z<=-18.5;z+=1){sphere(.45,x,.95,z,palette.leafLight);star(root,x,1.42,z+.15,.18,palette.gold);}});

  // Warm brick mathematics, cool brass observatory, and a leafy conservatory each have their own silhouette.
  [20,22.3,24.5,26.8].forEach(x=>{box(.23,3.4,.2,x,2.3,6.35,palette.copper);box(.43,.18,.32,x,4,6.35,palette.cream);});
  box(7.7,.22,.35,23.4,1.3,6.4,palette.copper);box(7.7,.22,.35,23.4,4.05,6.4,palette.cream);
  [20.1,26.7].forEach(x=>{box(.62,1.1,.62,x,5.6,3,palette.coral);box(.95,.22,.92,x,6.2,3,palette.gold);});
  const clockFace=cylinder(.56,.56,.1,23.4,3.55,6.53,palette.gold);clockFace.rotation.x=Math.PI/2;
  box(.065,.37,.06,23.4,3.67,6.6,palette.ink);const clockHand=box(.27,.065,.07,23.51,3.55,6.61,palette.ink);clockHand.rotation.z=.2;
  ring(3.96,.07,24.5,4.63,-17,palette.gold);
  [21.4,27.6].forEach(x=>{box(.15,3.6,.16,x,2.4,-13.15,palette.cream);box(.34,.2,.38,x,4.25,-13.15,palette.gold);});
  const solar=new THREE.Group();solar.position.set(25.3,5.4,-18.3);solar.rotation.x=-.23;root.add(solar);
  for(let i=0;i<3;i++){box(1.05,.08,1.9,i*1.15-1.15,0,0,palette.ink,solar);for(let row=0;row<3;row++)box(.82,.045,.48,i*1.15-1.15,.07,row*.6-.6,palette.cyan,solar);}
  const antenna=cylinder(.055,.055,2.9,21.5,6.2,-18.5,palette.cream);sphere(.19,21.5,7.72,-18.5,palette.goldGlow);
  [-29.4,-27.15,-24.85,-22.6].forEach(x=>{box(.16,3.5,.18,x,2.4,-14.33,palette.teal);box(.45,.14,.32,x,4.2,-14.33,palette.gold);});
  box(7.2,.19,.31,-26,4.38,-14.32,palette.teal);box(7.7,.21,.35,-26,1.28,-14.32,palette.teal);
  for(let i=0;i<5;i++){const x=-30+i*1.4;sphere(.37,x,4.66,-14.22,i%2?palette.leaf:palette.leafLight,root,[1.2,.75,.65]);}
  // Three signs share the texture atlas; navigation stays outside all terminal approach pads.
  label('MESTEČKO LOGIKY',28,1.4,11,5.2,.57,'#fff5dc','#a75f45');
  label('MODRÁ HVEZDÁREŇ',30,1.4,-9,5.2,.57,'#e7ffff','#287585');
  label('ZELENÉ ÚDOLIE',-31,1.4,-9,5,.57,'#efffda','#397d52');

  // Astral gate: depth in the distance and a visible future goal.
  pad(0,-30,'purple',4.0);box(10,.35,7,0,.22,-32,palette.violet);
  [-3.7,3.7].forEach(x=>{box(1.05,7.5,1.15,x,4.05,-32.6,palette.violet);box(1.45,.45,1.6,x,7.9,-32.6,palette.gold);collide(x,-32.6,.62,.65,8.2);});
  box(8.5,1.0,1.3,0,7.48,-32.6,palette.violet);
  const portalRing=ring(3.1,.21,0,4.0,-32.5,palette.purpleGlow,root,true);
  const portalSurface=new THREE.Mesh(new THREE.CircleGeometry(2.92,64),new THREE.MeshBasicMaterial({color:'#ad85ed',transparent:true,opacity:.24,side:THREE.DoubleSide,depthWrite:false}));portalSurface.position.set(0,4,-32.57);root.add(portalSurface);
  const portalSparkles=new THREE.Group();portalSparkles.position.set(0,4,-32.4);root.add(portalSparkles);
  for(let i=0;i<12;i++){const a=i*Math.PI*2/12;star(portalSparkles,Math.cos(a)*2.85,Math.sin(a)*2.85,0,.12,i%2?palette.goldGlow:palette.cyanGlow);}
  animated.push({kind:'portal',object:portalSparkles,phase:0});
  batchGroup(portalSparkles);
  label('ASTRÁLNA BRÁNA',0,8.7,-32.48,8,1.0,'#fff9ee','#7f62be');
  label('3 OBLASTI · JEDEN VEĽKÝ OBJAV',0,.95,-28.36,6,.6,'#634c95','#f0e3ff');
  interaction('portal-astral','portal','Astrálna brána',0,-30);

  // Banners and plaza furniture, placed away from the traversal lanes.
  const banner = (x,z,text,color,rot=0) => {
    cylinder(.08,.12,6.1,x,3.05,z,palette.cream);
    const g=new THREE.Group();g.position.set(x+.88,4.95,z);g.rotation.y=rot;root.add(g);
    box(1.65,2.05,.06,0,0,0,palette[color],g);label(text,0,.13,.042,1.5,.65,'#fff8ea','rgba(0,0,0,0)',g);star(g,0,-.57,.055,.26,palette.cream);
    collide(x,z,.16,.16,6.1);
  };
  banner(-10,-5,'01','coral');banner(10,-5,'02','teal');banner(-6,-24,'03','violet');banner(6,-24,'04','violet');
  bench(-8,-7,.4);bench(8,-7,-.4);bench(11,11,-Math.PI/2);bench(-30,4,Math.PI/2);
  [[-7,21],[7,21],[-11,-12],[11,-12],[-7,-25],[7,-25],[33,4],[-34,4]].forEach(([x,z])=>lamp(x,z));
  // Large stylized tree borders frame the campus. No trunk interrupts a subject pad.
  const treePositions=[[-36,18,1.6],[-31,24,1.3],[-25,29,1.2],[-17,31,1.55],[-7,30,1.3],[8,30,1.3],[16,28,1.55],[43,24,1.4],[34,16,1.7],[36,7,1.35],[35,-5,1.5],[34,-15,1.4],[28,-27,1.6],[17,-29,1.2],[-17,-30,1.35],[-30,-29,1.6],[-37,-18,1.6],[-36,-7,1.25],[-36,4,1.3],[-30,13,1.2],[-7,-19,.85],[8,-19,.9]];
  treePositions.forEach(([x,z,s],i)=>tree(x,z,s,i%3===0?1:0));
  [[-12,24],[12,24],[31,10],[-33,9],[-12,-25],[12,-25]].forEach(([x,z])=>{
    cylinder(1.1,1.3,.45,x,.22,z,palette.cream);hedge(x,z,1.7,1.7);for(let i=0;i<4;i++)sphere(.14,x+Math.cos(i*1.7)*.6,1.02,z+Math.sin(i*1.7)*.6,i%2?palette.peach:palette.gold);
  });
  // Friendly decorative students populate landmarks without changing gameplay.
  kid(-6,6,'#ec9273',-.6,0);kid(9,2,'#6da9cb',.9,1);kid(11,-18,'#a787d6',-1.0,2);kid(-12,-17,'#edb951',.65,3);kid(-20,16,'#73af9b',-.4,4);
  // A real ascending jump course, with wide landings and a clear approach road.
  // These slabs deliberately use vertical platform physics instead of flat wall colliders.
  road(11,22,18,4);
  for(let x=7;x<18;x+=3)box(.8,.025,.18,x,.061,22,palette.edge);
  const coursePoints=[[20,22,.35],[24,22,.8],[28,24,1.2],[32,24,1.7],[36,27,2.2],[36,31,2.7],[32,34,3.1]];
  coursePoints.forEach(([x,z,top],index)=>{
    const hx=index===0||index===6?1.9:1.75, hz=index===0||index===6?1.9:1.75;
    platforms.push({id:`trail-${index}`,x,z,hx,hz,top});
    const surface=index===6?palette.gold:index%2?palette.purple:palette.blue;
    box(hx*2,top,hz*2,x,top/2,z,surface);
    box(hx*2-.18,.022,hz*2-.18,x,top+.004,z,palette.cream);
    // Edge lights make the usable top face legible even when viewed from below.
    const glow=index===6?palette.goldGlow:palette.cyanGlow;
    [-1,1].forEach(side=>{
      box(hx*2,.07,.07,x,top-.04,z+side*(hz-.025),glow);
      box(.07,.07,hz*2,x+side*(hx-.025),top-.04,z,glow);
    });
    const number=label(String(index+1),x,top+.021,z,1.05,1.05,'#566085','#fff7e5');number.rotation.x=-Math.PI/2;
  });
  [-1,1].forEach(side=>{box(.3,3.9,.3,17,1.95,22+side*2.45,palette.violet);});
  box(.35,.4,5.3,17,3.85,22,palette.purple);
  const trailSign=label('NEBESKÁ DRÁHA',16.8,4.62,22,6,.83,'#5e5090','#eee5ff');trailSign.rotation.y=-Math.PI/2;
  const trailHint=label('7 PLOŠÍN · 1 DOBRODRUŽSTVO',16.75,3.83,22,4.9,.46,'#5e5090','#fff7e5');trailHint.rotation.y=-Math.PI/2;
  interaction('parkour-start','parkour','Nebeská dráha',17,22);
  const course={id:'sky-trail',name:'Nebeská dráha',platforms,spawn:{x:20,z:22,y:.35}};
  const finish=platforms.at(-1);
  cylinder(.07,.09,3.2,finish.x-1.4,finish.top+1.6,finish.z-1.45,palette.violet);
  box(1.45,.82,.045,finish.x-.72,finish.top+2.64,finish.z-1.45,palette.gold);
  star(root,finish.x-.72,finish.top+2.66,finish.z-1.415,.27,palette.cream);
  const finishSign=label('CIEĽ',finish.x,finish.top+.74,finish.z+1.95,2.4,.57,'#7a5f2b','#fff1bd');
  const finishStar=star(root,finish.x,finish.top+1.3,finish.z,.6,palette.goldGlow);
  animated.push({kind:'crystal',object:finishStar,baseY:finish.top+1.3,phase:5});

  // Explore the whole campus: every crystal sits on an unobstructed walking lane.
  const crystalGeometry=new THREE.OctahedronGeometry(.45,0);
  const crystalPoints=[[2,10],[-4,21],[10,22],[13,9],[9,4],[12,-8],[10,-17],[0,-24],[-11,-16],[-15,-8],[-5,4],[-13,13]];
  crystalPoints.forEach(([x,z],index)=>{
    const group=new THREE.Group();group.position.set(x,1.15,z);root.add(group);
    const crystal=new THREE.Mesh(crystalGeometry,index%3===2?palette.purpleGlow:palette.cyanGlow);
    crystal.scale.set(.68,1.28,.68);group.add(crystal);
    const orbit=ring(.43,.022,0,-.19,0,palette.goldGlow,group);orbit.rotation.z=.2;
    const shadow=cylinder(.30,.30,.018,x,.08,z,palette.teal);shadow.castShadow=false;
    collectibles.push({id:`crystal-${String(index+1).padStart(2,'0')}`,position:{x,y:1.15,z},radius:1.5,mesh:group,shadow});
    animated.push({kind:'crystal',object:group,baseY:1.15,phase:index*.7});
  });

  // Quiet details around the open lawn, batched into just two flower draw calls.
  const flowerSites=[[-10,28],[-21,25],[-32,28],[-38,12],[-31,-6],[-18,-25],[13,-25],[31,-8],[29,12],[11,29]];
  const blooms=new THREE.InstancedMesh(new THREE.OctahedronGeometry(.19,0),palette.peach,flowerSites.length*4);
  const stems=new THREE.InstancedMesh(cubeGeometry,palette.leaf,flowerSites.length*4);
  const flowerMatrix=new THREE.Object3D();let flowerIndex=0;
  flowerSites.forEach(([x,z],site)=>{
    for(let i=0;i<4;i++){
      const xx=x+Math.cos(i*2.2+site)*.75,zz=z+Math.sin(i*2.2+site)*.75;
      flowerMatrix.position.set(xx,.46+(i%2)*.12,zz);flowerMatrix.scale.set(1,.7,1);flowerMatrix.updateMatrix();blooms.setMatrixAt(flowerIndex,flowerMatrix.matrix);
      flowerMatrix.position.y=.22;flowerMatrix.scale.set(.045,.44,.045);flowerMatrix.updateMatrix();stems.setMatrixAt(flowerIndex++,flowerMatrix.matrix);
    }
  });
  root.add(blooms,stems);
  cylinder(3.2,3.2,.08,-36,.08,32,palette.blue,root,28);
  ring(3.3,.17,-36,.10,32,palette.cream);
  [-2,-1,1,2].forEach((offset,index)=>{
    const lily=cylinder(.4,.4,.035,-36+offset,.14,32+Math.sin(index)*1.7,palette.leaf,root,8);lily.castShadow=false;
    sphere(.13,-36+offset,.23,32+Math.sin(index)*1.7,palette.purple);
  });
  // A small timber footbridge and flower baskets turn the pond into a quiet destination.
  for(let i=0;i<7;i++)box(1.9,.045,.36,-36,.062,30.8+i*.4,palette.brown);
  [-1,1].forEach(side=>{box(.075,.075,2.9,-36+side*.96,.86,32,palette.cream);[30.65,32,33.35].forEach(z=>box(.09,.86,.09,-36+side*.96,.45,z,palette.brown));});

  // Wind-driven grass uses a single draw and avoids roads, landings and new co-op beacons.
  const grassGeo=new THREE.BufferGeometry();grassGeo.setAttribute('position',new THREE.Float32BufferAttribute([-.07,0,0,.07,0,0,.035,1,0,0,0,-.07,0,0,.07,.015,.82,.025,-.065,0,-.045,.065,0,.045,-.02,.67,.02],3));grassGeo.computeVertexNormals();
  const grassMaterial=mat('#65b580',{side:THREE.DoubleSide});grassMaterial.onBeforeCompile=palette.leaf.onBeforeCompile;grassMaterial.customProgramCacheKey=palette.leaf.customProgramCacheKey;
  const grass=new THREE.InstancedMesh(grassGeo,grassMaterial,144),grassTransform=new THREE.Object3D();grass.name='WindMeadow';
  const grassSites=[[-34,-12],[-32,18],[29,-26],[31,10],[-22,26],[-10,29]],beacons=[[6,12],[-8,22],[14,-3],[-12,-18],[4,-27],[30,18]];
  for(let i=0;i<144;i++){
    const site=grassSites[i%grassSites.length],a=i*2.3999,r=1+(i%19)*.19;let x=site[0]+Math.cos(a)*r,z=site[1]+Math.sin(a)*r;
    if(beacons.some(([bx,bz])=>Math.hypot(x-bx,z-bz)<2.5)||colliders.some(c=>Math.abs(x-c.x)<c.hx+.8&&Math.abs(z-c.z)<c.hz+.8)){x=site[0];z=site[1]+4.5;}
    grassTransform.position.set(x,.04,z);grassTransform.rotation.y=a;grassTransform.scale.set(.8+(i%3)*.2,.3+(i%7)*.055,.8+(i%3)*.2);grassTransform.updateMatrix();grass.setMatrixAt(i,grassTransform.matrix);
  }
  grass.computeBoundingSphere();root.add(grass);

  // Floating garden islands and a flowing waterfall make the skyline an adventure landscape.
  const waterfallMaterial=new THREE.ShaderMaterial({uniforms:{uTime:windTime,uDaylight:daylight},transparent:true,depthWrite:false,side:THREE.DoubleSide,
    vertexShader:'varying vec2 flowUv;void main(){flowUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}',
    fragmentShader:`uniform float uTime;uniform float uDaylight;varying vec2 flowUv;
    void main(){float streak=pow(.5+.5*sin(flowUv.x*39.+sin(flowUv.y*13.-uTime*5.)*.75),5.);vec3 color=mix(vec3(.2,.7,.76),vec3(.84,1.,.95),streak);gl_FragColor=vec4(color*(.4+.6*uDaylight),.66+streak*.2);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
    }`});
  let waterfall;
  [[-42,14,-35,1],[43,17,-31,.72],[9,22,-55,.6]].forEach(([x,y,z,size],index)=>{
    const island=new THREE.Group();island.position.set(x,y,z);island.scale.setScalar(size);root.add(island);
    const cliff=new THREE.Mesh(geometry('sky-cliff',()=>new THREE.ConeGeometry(1,1,7)),palette.cliff);cliff.scale.set(5.3,5.4,5.3);cliff.position.y=-2.58;cliff.rotation.z=Math.PI;island.add(cliff);
    cylinder(5,5.3,.3,0,.04,0,palette.leafLight,island,16);cylinder(.17,.23,1.8,-1.4,1.1,-1,palette.brown,island);
    box(2.4,2,2.2,-1.4,2.8,-1,palette.leaf,island);box(1.7,.8,1.6,-1.1,3.9,-1,palette.leafLight,island);
    if(index===0){waterfall=new THREE.Mesh(new THREE.PlaneGeometry(1.5,14.4,1,12),waterfallMaterial);waterfall.position.set(4.35,-6.7,.8);island.add(waterfall);}
    batchGroup(island);animated.push({kind:'island',object:island,baseY:y,phase:index*2.1});
  });
  const dustPositions=[],dustPhases=[];
  for(let i=0;i<88;i++){
    const x=i<24?-37.6+Math.sin(i*2.1)*1.1:(i%2?-28:26)+Math.sin(i*1.8)*6,z=i<24?-34.2+Math.cos(i*2.8)*1.1:-17+Math.cos(i*1.9)*7;
    dustPositions.push(x,.4+(i%11)*.17,z);dustPhases.push(i*.73);
  }
  const dustGeo=new THREE.BufferGeometry();dustGeo.setAttribute('position',new THREE.Float32BufferAttribute(dustPositions,3));dustGeo.setAttribute('aPhase',new THREE.Float32BufferAttribute(dustPhases,1));
  const dustMaterial=new THREE.ShaderMaterial({uniforms:{uTime:windTime,uDaylight:daylight},transparent:true,depthWrite:false,blending:THREE.AdditiveBlending,
    vertexShader:'uniform float uTime;attribute float aPhase;varying float phase;void main(){phase=aPhase;vec3 p=position;p.y+=sin(uTime*.8+aPhase)*.18;vec4 mv=modelViewMatrix*vec4(p,1.);gl_Position=projectionMatrix*mv;gl_PointSize=clamp(48./max(1.,-mv.z),1.3,7.);}',
    fragmentShader:'uniform float uDaylight;varying float phase;void main(){float glow=pow(max(0.,1.-length(gl_PointCoord-.5)*2.),1.5);vec3 c=mix(vec3(.48,.95,1.),vec3(1.,.79,.38),step(.5,fract(phase)));gl_FragColor=vec4(c,glow*(.48+.3*(1.-uDaylight)));}'
  });
  const particles=new THREE.Points(dustGeo,dustMaterial);particles.name='GardenFireflies';root.add(particles);
  // Far landscape and floating clouds create a complete toy-world horizon.
  [-47,-40,40,47].forEach((x,i)=>{
    const hill=new THREE.Mesh(new THREE.ConeGeometry(8+i%2*3,9+i%2*4,5),palette.leafLight);hill.position.set(x,3,-46+i%2*5);root.add(hill);
  });
  [[-35,20,-37],[17,24,-42],[35,21,19],[-30,23,25],[-5,26,-48]].forEach(([x,y,z])=>{
    const cloud=new THREE.Group();cloud.position.set(x,y,z);root.add(cloud);
    box(5.4,1.25,2.5,0,0,0,palette.white,cloud);box(3.2,1.2,2.8,-.7,.7,0,palette.white,cloud);box(2.1,.8,2,2,-.3,0,palette.white,cloud);
    batchGroup(cloud);
    animated.push({kind:'cloud',object:cloud,baseX:x,phase:z});
  });
  // Suspended pennants across the spawn road.
  const poleX=[-6.6,6.6];poleX.forEach(x=>{cylinder(.1,.13,9.4,x,4.7,23,palette.teal);collide(x,23,.16,.16,9.4);});
  box(13.2,.055,.055,0,9.25,23,palette.brown);
  for(let i=0;i<9;i++){
    const points=new Float32Array([-.45,0,0,.45,0,0,0,-.7,0]);const geo=new THREE.BufferGeometry();geo.setAttribute('position',new THREE.BufferAttribute(points,3));geo.computeVertexNormals();
    const flag=new THREE.Mesh(geo,new THREE.MeshStandardMaterial({color:['#ffc957','#91d3ee','#b997ed','#ef807d'][i%4],side:THREE.DoubleSide}));flag.position.set(-5.3+i*1.3,9.17,23);root.add(flag);
  }
  const landmarks=addAcademyLandmarks(THREE,{root,palette,box,cylinder,sphere,ring,label,collide,cameraBlock,geometry,animated,star});

  let lastPetSignature='', activeState={};
  const makePet = (index,pet) => {
    const g=createCompanion(THREE,pet||{});g.position.set(((index%3)-1)*4.5,0,-Math.floor(index/3)*2.5);g.scale.setScalar(1.08+Math.min(5,Number(pet?.level)||1)*.025);petDisplay.add(g);
    return g;
  };
  const setState = state => {
    activeState=state||{};
    const collected=new Set(activeState.progress?.collected||[]);
    collectibles.forEach(item=>{item.mesh.visible=!collected.has(item.id);item.shadow.visible=item.mesh.visible;});
    const pets=Array.isArray(activeState.pets)?activeState.pets:[];
    const signature=JSON.stringify(pets.map(p=>p&&typeof p==='object'?[p.id,p.kind,p.color,p.name,p.level,p.rarity]:p));
    if(signature!==lastPetSignature){
      lastPetSignature=signature;
      // Keep the three display podiums; replace only pets we own.
      for(let i=petDisplay.children.length-1;i>=0;i--)if(petDisplay.children[i].userData.pet)petDisplay.children[i].userData.dispose?.();
      pets.slice(0,6).forEach((pet,i)=>{const object=makePet(i,pet);object.userData.pet=true;object.userData.baseY=object.position.y;object.userData.phase=i;});
    }
    const unlocked=!!(activeState.progress?.unlocked?.includes('astral')||activeState.portalUnlocked||activeState.astralUnlocked||activeState.unlocks?.astral||(activeState.zones?.math?.mastery>=3&&activeState.zones?.science?.mastery>=3&&activeState.zones?.nature?.mastery>=3));
    portalSurface.material.opacity=unlocked?.56:.24;
    portalRing.material.emissiveIntensity=unlocked?1.4:.7;
  };
  const update = (dt,time,state) => {
    windTime.value=time;
    if(state&&state!==activeState)setState(state);
    animated.forEach(item=>{
      const o=item.object,p=item.phase||0;
      if(item.kind==='egg'||item.kind==='heroEgg'){o.position.y=item.baseY+Math.sin(time*1.6+p)*.12;o.rotation.y=time*(item.kind==='heroEgg'?.18:.28)+p;}
      else if(item.kind==='spin'){o.rotation.y+=dt*.38;o.rotation.z=Math.sin(time*.35+p)*.1;}
      else if(item.kind==='portal'){o.rotation.z=-time*.12;}
      else if(item.kind==='coins'){o.rotation.y=time*.65;o.position.y=item.baseY+Math.sin(time*2)*.1;}
      else if(item.kind==='npc'){o.position.y=Math.sin(time*1.8+p)*.025;}
      else if(item.kind==='cloud'){o.position.x=item.baseX+Math.sin(time*.035+p)*2;}
      else if(item.kind==='island'){o.position.y=item.baseY+Math.sin(time*.42+p)*.14;}
      else if(item.kind==='crystal'){if(o.visible){o.position.y=item.baseY+Math.sin(time*2.1+p)*.15;o.rotation.y=time*.8+p;}}
    });
    petDisplay.children.forEach(o=>{if(o.userData.pet){o.position.y=o.userData.baseY+Math.sin(time*2+o.userData.phase)*.10;o.rotation.y=Math.sin(time*.8+o.userData.phase)*.08;o.userData.animate?.(dt,time);}});
  };
  setState({});
  // Bake static pieces into shared draws. Animated props and mutable displays stay independent.
  // World-space matrices preserve rotations, dimensions and nested building transforms exactly.
  root.updateMatrixWorld(true);
  // All static signs share a compact 2048-wide atlas and one draw instead of forty textures/draws.
  const labelCount=labels.length,atlasRows=Math.ceil(labelCount/4),atlasCanvas=document.createElement('canvas');atlasCanvas.width=2048;atlasCanvas.height=atlasRows*128;
  const atlasContext=atlasCanvas.getContext('2d'),labelPositions=[],labelUvs=[];
  labels.forEach((mesh,index)=>{
    const col=index%4,row=Math.floor(index/4);atlasContext.drawImage(mesh.material.map.image,col*512,row*128,512,128);
    const positions=mesh.geometry.getAttribute('position'),uvs=mesh.geometry.getAttribute('uv'),indices=mesh.geometry.index;
    for(let i=0;i<indices.count;i++){
      const vertex=indices.getX(i),point=new THREE.Vector3().fromBufferAttribute(positions,vertex).applyMatrix4(mesh.matrixWorld);
      labelPositions.push(point.x,point.y,point.z);labelUvs.push((col+uvs.getX(vertex))/4,1-(row+1-uvs.getY(vertex))/atlasRows);
    }
    mesh.removeFromParent();mesh.material.map.dispose();mesh.material.dispose();mesh.geometry.dispose();
  });
  const atlasTexture=new THREE.CanvasTexture(atlasCanvas);atlasTexture.colorSpace=THREE.SRGBColorSpace;
  const labelGeometry=new THREE.BufferGeometry();labelGeometry.setAttribute('position',new THREE.Float32BufferAttribute(labelPositions,3));labelGeometry.setAttribute('uv',new THREE.Float32BufferAttribute(labelUvs,2));
  const labelAtlas=new THREE.Mesh(labelGeometry,new THREE.MeshBasicMaterial({map:atlasTexture,transparent:true,alphaTest:.05,depthWrite:false,side:THREE.DoubleSide}));labelAtlas.name='DistrictSignAtlas';root.add(labelAtlas);labels.length=0;
  const mutableObjects=new Set([petDisplay,portalRing,portalSurface,...animated.map(item=>item.object),...collectibles.map(item=>item.shadow)]);
  const staticGroups=new Map();
  root.traverse(object=>{
    if(!object.isMesh||object.isInstancedMesh||Array.isArray(object.material)||object.material.transparent)return;
    for(let ancestor=object;ancestor&&ancestor!==root;ancestor=ancestor.parent)if(mutableObjects.has(ancestor))return;
    const key=`${object.geometry.uuid}:${object.material.uuid}:${object.castShadow}:${object.receiveShadow}`;
    if(!staticGroups.has(key))staticGroups.set(key,[]);
    staticGroups.get(key).push(object);
  });
  let staticMeshCount=0, staticBatchCount=0;
  const rootInverse=new THREE.Matrix4().copy(root.matrixWorld).invert(), instanceMatrix=new THREE.Matrix4();
  staticGroups.forEach(objects=>{
    if(objects.length<3)return;
    const first=objects[0], batch=new THREE.InstancedMesh(first.geometry,first.material,objects.length);
    batch.name='StaticCampusBatch';batch.castShadow=first.castShadow;batch.receiveShadow=first.receiveShadow;
    batch.instanceMatrix.setUsage(THREE.StaticDrawUsage);
    objects.forEach((object,index)=>{instanceMatrix.multiplyMatrices(rootInverse,object.matrixWorld);batch.setMatrixAt(index,instanceMatrix);object.removeFromParent();});
    batch.computeBoundingSphere();root.add(batch);staticMeshCount+=objects.length;staticBatchCount++;
  });
  const ambiance={ocean,waterfall,particles,windTime,daylight,setDaylight(value){daylight.value=Math.max(.1,Math.min(1,Number(value)||.1));}};
  return { root,colliders,cameraColliders:[...colliders,...cameraOccluders],interactables,platforms,course,collectibles,animated,ambiance,landmarks,update,setState,renderStats:{staticMeshes:staticMeshCount,staticBatches:staticBatchCount,labelCount,labelBatches:1},spawn:{x:0,z:13},bounds:{minX:-49,maxX:49,minZ:-49,maxZ:49} };
}
