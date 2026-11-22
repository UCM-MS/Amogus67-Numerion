import {createCompanionRig} from './companion-rigs.js';
// Original explorer and companion rigs. GPU resources are shared across local/remote players.
const caches=new WeakMap();
const validColor=(value,fallback)=>/^#[\da-f]{6}$/i.test(value||'')?value:fallback;
function resources(T){
  if(!caches.has(T))caches.set(T,{box:new T.BoxGeometry(1,1,1),ball:new T.SphereGeometry(1,12,8),cone:new T.ConeGeometry(1,1,6),contactCircle:new T.CircleGeometry(1,24),contactMaterial:new T.ShaderMaterial({transparent:true,depthWrite:false,vertexShader:'varying vec2 contactUv;void main(){contactUv=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}',fragmentShader:'varying vec2 contactUv;void main(){float a=(1.-smoothstep(.08,.5,length(contactUv-.5)))*.22;gl_FragColor=vec4(.05,.09,.13,a);}'}),materials:new Map(),merged:new Map(),vertexMaterial:new T.MeshStandardMaterial({color:'#ffffff',vertexColors:true,roughness:.72})});
  return caches.get(T);
}
function material(T,color){const cache=resources(T);if(!cache.materials.has(color))cache.materials.set(color,new T.MeshStandardMaterial({color,roughness:.72}));return cache.materials.get(color);}
function piece(T,parent,geometry,color,x,y,z,sx,sy,sz){const mesh=new T.Mesh(resources(T)[geometry],material(T,color));mesh.position.set(x,y,z);mesh.scale.set(sx,sy,sz);mesh.castShadow=true;mesh.receiveShadow=true;parent.add(mesh);return mesh;}
const cube=(T,g,c,x,y,z,w,h,d)=>piece(T,g,'box',c,x,y,z,w,h,d);
const ball=(T,g,c,x,y,z,w,h=w,d=w)=>piece(T,g,'ball',c,x,y,z,w,h,d);
function batch(T,group,skip=[]){
  group.updateMatrixWorld(true);const excluded=new Set(skip),objects=[],inverse=new T.Matrix4().copy(group.matrixWorld).invert();
  group.traverse(object=>{if(!object.isMesh||object.isInstancedMesh||!object.material.isMeshStandardMaterial||object.material.transparent)return;for(let p=object;p&&p!==group;p=p.parent)if(excluded.has(p))return;objects.push({object,matrix:new T.Matrix4().multiplyMatrices(inverse,object.matrixWorld)});});
  if(objects.length<2)return;
  const cache=resources(T),key=objects.map(({object,matrix})=>`${object.geometry.uuid}:${object.material.color.getHexString()}:${matrix.elements.map(n=>n.toFixed(7)).join(',')}`).join('|');
  if(!cache.merged.has(key)){
    const positions=[],normals=[],colors=[],point=new T.Vector3(),normal=new T.Vector3();
    objects.forEach(({object,matrix})=>{
      const p=object.geometry.getAttribute('position'),n=object.geometry.getAttribute('normal'),index=object.geometry.index,normalMatrix=new T.Matrix3().getNormalMatrix(matrix),count=index?.count||p.count,color=object.material.color,mirrored=matrix.determinant()<0;
      for(let i=0;i<count;i++){
        const sourceIndex=mirrored&&i%3?i+(i%3===1?1:-1):i,vertex=index?index.getX(sourceIndex):sourceIndex;
        point.fromBufferAttribute(p,vertex).applyMatrix4(matrix);normal.fromBufferAttribute(n,vertex).applyNormalMatrix(normalMatrix);
        positions.push(point.x,point.y,point.z);normals.push(normal.x,normal.y,normal.z);colors.push(color.r,color.g,color.b);
      }
    });
    const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(positions,3));geometry.setAttribute('normal',new T.Float32BufferAttribute(normals,3));geometry.setAttribute('color',new T.Float32BufferAttribute(colors,3));geometry.computeBoundingSphere();cache.merged.set(key,geometry);
  }
  objects.forEach(({object})=>object.removeFromParent());
  const mesh=new T.Mesh(cache.merged.get(key),cache.vertexMaterial);mesh.castShadow=true;mesh.receiveShadow=true;mesh.name='ExplorerParts';group.add(mesh);
}
function nameTag(T,name){
  if(typeof document==='undefined')return null;
  const canvas=document.createElement('canvas');canvas.width=512;canvas.height=128;const ctx=canvas.getContext('2d');if(!ctx)return null;
  ctx.fillStyle='rgba(27,40,67,.86)';ctx.beginPath();ctx.roundRect(4,14,504,100,38);ctx.fill();ctx.fillStyle='#fff9e8';ctx.font='700 44px Arial, sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText([...String(name||'Objaviteľ')].slice(0,24).join(''),256,66,468);
  const texture=new T.CanvasTexture(canvas);texture.colorSpace=T.SRGBColorSpace;const sprite=new T.Sprite(new T.SpriteMaterial({map:texture,transparent:true,depthWrite:false}));sprite.scale.set(2.8,.7,1);sprite.position.set(0,3.87,0);sprite.name='ExplorerName';return sprite;
}

export function createCompanion(T,pet={}){
  return createCompanionRig(T,pet,{resources,material,piece,cube,ball,batch});
}

export function createExplorer(T,{color='#f9df70',skin='#ffdab4',name,pet=null}={}){
  color=validColor(color,'#f9df70');skin=validColor(skin,'#ffdab4');
  const group=new T.Group(),body=new T.Group(),arms=[],legs=[];group.name='Explorer';group.add(body);
  const ink='#26364f',hair='#314769',cream='#fff6df',teal='#267d87',pants='#697cde';
  cube(T,body,color,0,1.72,0,1.18,1.27,.69);cube(T,body,cream,0,2.26,-.03,.57,.12,.74);
  cube(T,body,teal,-.35,1.75,.376,.12,1.06,.065);cube(T,body,teal,.35,1.75,.376,.12,1.06,.065);
  cube(T,body,teal,0,1.7,.52,.79,.96,.35);cube(T,body,'#4eb6ad',0,1.86,.728,.59,.28,.08);cube(T,body,color,0,1.49,.722,.27,.24,.09);
  cube(T,body,ink,0,1.12,0,1.22,.14,.71);cube(T,body,color,0,1.13,-.375,.23,.16,.06);
  cube(T,body,skin,0,2.77,-.015,.88,.85,.83);cube(T,body,hair,0,3.17,.015,.95,.17,.9);cube(T,body,hair,-.36,2.98,.05,.19,.33,.89);cube(T,body,hair,.36,3.03,.05,.19,.23,.89);
  cube(T,body,color,0,3.25,.015,.98,.1,.94);cube(T,body,color,0,3.18,-.51,1.06,.07,.29);
  for(const side of [-1,1]){
    cube(T,body,skin,side*.47,2.76,0,.14,.25,.23);
    cube(T,body,ink,side*.19,2.82,-.439,.1,.13,.025);cube(T,body,cream,side*.177,2.845,-.457,.027,.035,.02);
    cube(T,body,'#ed9c94',side*.3,2.65,-.438,.115,.055,.023);
    const arm=new T.Group();arm.position.set(side*.84,2.17,0);body.add(arm);cube(T,arm,color,0,-.21,0,.45,.46,.52);cube(T,arm,skin,0,-.67,0,.39,.51,.43);cube(T,arm,teal,0,-.88,0,.4,.1,.45);arms.push(arm);
    const leg=new T.Group();leg.position.set(side*.32,1.02,0);body.add(leg);cube(T,leg,pants,0,-.41,0,.46,.87,.5);cube(T,leg,cream,0,-.9,-.075,.5,.22,.72);cube(T,leg,ink,0,-1.005,-.075,.53,.07,.74);legs.push(leg);
  }
  cube(T,body,'#b26569',0,2.595,-.445,.19,.04,.027);cube(T,body,skin,0,2.7,-.475,.105,.105,.095);
  batch(T,body,[...arms,...legs]);arms.forEach(arm=>batch(T,arm));legs.forEach(leg=>batch(T,leg));
  const sprite=name?nameTag(T,name):null;if(sprite)group.add(sprite);
  const contact=new T.Mesh(resources(T).contactCircle,resources(T).contactMaterial);contact.name='ExplorerContact';contact.rotation.x=-Math.PI/2;contact.scale.set(.88,.62,1);contact.position.y=.064;group.add(contact);
  let companion=null,petSignature='';
  function setPet(value){
    const signature=value?JSON.stringify([value.kind,value.color,value.name,value.level,value.rarity]):'';if(signature===petSignature)return;
    petSignature=signature;if(companion){companion.userData.dispose?.();companion=null;}
    if(value){companion=createCompanion(T,value);companion.scale.setScalar(.76);companion.position.set(1.25,0,.55);group.add(companion);}
  }
  function animate(dt,time,{moving=false,onGround=true,yaw,emote}={}){
    contact.visible=onGround;
    if(Number.isFinite(yaw))group.rotation.y=yaw;
    const swing=moving?Math.sin(time*9)*.52:Math.sin(time*1.7)*.025;
    arms.forEach((arm,index)=>{arm.rotation.x=onGround?swing*(index?1:-1):-.42;arm.rotation.z=(index?1:-1)*.055;});
    if(emote==='wave'){arms[1].rotation.x=-2.3;arms[1].rotation.z=.55+Math.sin(time*8)*.24;}
    legs.forEach((leg,index)=>{leg.rotation.x=onGround?swing*(index?-1:1):.23;});
    body.position.y=moving&&onGround?Math.abs(Math.sin(time*9))*.035:Math.sin(time*1.7)*.018;
    if(companion){companion.position.y=Math.sin(time*(moving?8:2))*(moving?.05:.025);companion.userData.animate?.(dt,time,{moving});}
  }
  function dispose(){if(companion)companion.userData.dispose?.();if(sprite){sprite.material.map?.dispose();sprite.material.dispose();}group.removeFromParent();}
  setPet(pet);
  return {group,arms,legs,animate,setPet,dispose};
}
