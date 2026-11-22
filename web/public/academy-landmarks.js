/** Authored campus architecture, using the world's shared material and geometry pools. */
export function addAcademyLandmarks(T, {root,palette:p,box,cylinder,sphere,ring,label,collide,cameraBlock,geometry,animated,star}) {
  const landmarks=[];
  function landmark(name,x,z){const group=new T.Group();group.name=name;group.position.set(x,0,z);root.add(group);landmarks.push(group);return group;}
  function column(parent,x,z,height=6.8){
    box(.64,.3,.68,x,.19,z,p.cream,parent);box(.38,height,.38,x,height/2+.32,z,p.mint,parent);
    box(.68,.28,.68,x,height+.3,z,p.cream,parent);box(.78,.1,.78,x,height+.49,z,p.gold,parent);
  }
  // A curved masonry arch is open all the way to the ground. It never hides a flat wall in the entrance.
  const archGeometry=geometry('academy-open-arch',()=>{
    const shape=new T.Shape(),outer=1,inner=.79;
    shape.moveTo(-outer,0);shape.absarc(0,0,outer,Math.PI,0,true);shape.lineTo(inner,0);shape.absarc(0,0,inner,0,Math.PI,false);shape.closePath();
    return new T.ExtrudeGeometry(shape,{depth:.16,bevelEnabled:false,curveSegments:14});
  });
  function arch(parent,x,y,z,radius,material=p.cream){
    const mesh=new T.Mesh(archGeometry,material);mesh.position.set(x,y,z);mesh.scale.set(radius,radius,1);mesh.castShadow=true;mesh.receiveShadow=true;parent.add(mesh);
    // Narrow boxes follow the curved masonry, leaving the actual arch opening clear.
    for(let i=0;i<14;i++){
      const a0=i*Math.PI/14,a1=(i+1)*Math.PI/14,points=[];
      for(const a of [a0,a1])for(const r of [radius*.79,radius])points.push({x:x+Math.cos(a)*r,y:y+Math.sin(a)*r});
      const minX=Math.min(...points.map(p=>p.x)),maxX=Math.max(...points.map(p=>p.x)),minY=Math.min(...points.map(p=>p.y)),maxY=Math.max(...points.map(p=>p.y));
      cameraBlock(parent.position.x+(minX+maxX)/2,parent.position.z+z+.08,(maxX-minX)/2,.08,minY,maxY);
    }
    return mesh;
  }

  // The academy's two slender clock towers frame the egg gallery instead of blocking its open frontage.
  const academy=landmark('AcademyRotunda',0,-9.7);
  for(const side of [-1,1]){
    const x=side*7.2;
    box(3.05,.34,3.1,x,.23,-.3,p.cream,academy);
    box(2.5,8.5,2.5,x,4.65,-.3,p.teal,academy);
    box(2.64,.24,2.64,x,2.2,-.3,p.mint,academy);box(2.64,.24,2.64,x,6.1,-.3,p.mint,academy);
    box(2.85,.36,2.85,x,8.9,-.3,p.cream,academy);
    box(2.1,1.6,2.1,x,9.84,-.3,p.purple,academy);
    const roof=new T.Mesh(geometry('academy-spire',()=>new T.ConeGeometry(1,1,4)),p.violet);roof.position.set(x,11.5,-.3);roof.scale.set(2,2.2,2);roof.rotation.y=Math.PI/4;academy.add(roof);roof.castShadow=true;
    sphere(.18,x,12.7,-.3,p.goldGlow,academy);
    for(const yy of [3.6,6.95]){
      box(1.23,1.75,.1,x,yy,1,p.cream,academy);box(1.02,1.5,.12,x,yy,1.06,p.blue,academy);
      box(.075,1.5,.13,x,yy,1.1,p.cream,academy);box(1.03,.075,.13,x,yy,1.1,p.cream,academy);
    }
    box(.9,.1,.13,x,7.48,1.14,p.gold,academy);
    collide(x,-10,1.29,1.29,12.8);
    column(academy,side*4.55,.15,6.9);
    collide(side*4.55,-9.55,.28,.28,7.5);
    // A deep cornice and three small lintel panels create a layered, readable silhouette.
    box(4.4,.3,1.3,side*4.7,7.12,-.2,p.teal,academy);
    box(4.6,.16,1.45,side*4.7,7.36,-.2,p.cream,academy);
    cameraBlock(side*4.7,-9.9,2.3,.725,6.97,7.44);
  }
  arch(academy,0,6.95,.15,4.55);
  arch(academy,0,6.95,.29,4.18,p.gold);
  box(3,.24,1.4,0,11.7,-.18,p.cream,academy);box(2.5,.18,1.6,0,11.92,-.18,p.gold,academy);
  const emblem=new T.Group();emblem.position.set(0,10.05,.55);academy.add(emblem);
  ring(.82,.085,0,0,0,p.goldGlow,emblem,true);star(emblem,0,0,.04,.52,p.cream);
  label('L U M E N',0,8.15,-8.5,5.2,.9,'#fff6d9','#287d79');
  label('AKADÉMIA OBJAVOV',0,7.45,-8.48,5.5,.48,'#fff6d9','#287d79');

  // Arrival avenue: a generous open arch, patterned paving, lantern posts and planted beds.
  const arrival=landmark('ArrivalPromenade',0,23);
  for(const side of [-1,1]){
    column(arrival,side*6.6,0,8.5);
    box(.95,.2,1.05,side*6.6,8.9,0,p.teal,arrival);
    sphere(.34,side*6.6,9.38,0,p.goldGlow,arrival);
    for(const z of [-7.5,-3.5,4]){
      box(2.2,.23,2.2,side*9,.16,z,p.cream,arrival);box(1.93,.17,1.93,side*9,.32,z,p.brown,arrival);
      sphere(.75,side*9,.83,z,p.leaf,arrival,[1.12,.7,1.12]);sphere(.35,side*9-.35,1.18,z+.25,p.purple,arrival);
      sphere(.28,side*9+.38,1.12,z-.2,p.peach,arrival);
    }
  }
  arch(arrival,0,8.55,-.08,6.6,p.teal);arch(arrival,0,8.55,.1,6.05,p.cream);
  label('TVOJ SVET. TVOJE OBJAVY.',0,10.8,23.26,7.6,.72,'#3f6a66','#fff4d9');
  for(let z=7;z<27;z+=2){
    box(.12,.017,1.5,-5.1,.063,z,p.cream);box(.12,.017,1.5,5.1,.063,z,p.cream);
    for(const side of [-1,1]){const tile=box(.6,.018,.6,side*3.8,.064,z,p.mint);tile.rotation.y=Math.PI/4;}
  }

  // A sheltered companion sanctuary gives the player's collection a home with useful visual landmarks.
  const sanctuary=landmark('CompanionSanctuary',-17,10.5);
  for(const x of [-7.4,7.4]){
    column(sanctuary,x,7.15,4.2);
    // These columns sit on the existing solid boundary posts.
    box(.7,.18,7.2,x,4.62,3.55,p.cream,sanctuary);
    cameraBlock(-17+x,14.05,.35,3.6,4.53,4.71);
  }
  box(15.8,.25,.9,0,4.65,7.15,p.teal,sanctuary);box(16.2,.14,1.1,0,4.86,7.15,p.mint,sanctuary);
  cameraBlock(-17,17.65,8.1,.55,4.525,4.93);
  for(let x=-6.5;x<7;x+=1.3)box(.17,.17,3.6,x,4.8,5.8,p.cream,sanctuary);
  for(let x=-6.5;x<7;x+=1.3)cameraBlock(-17+x,16.3,.085,1.8,4.715,4.885);
  for(const [x,z] of [[-5.4,2.6],[.4,2.6],[5.6,2.6]]){
    cylinder(1.5,1.58,.15,x,.2,z,p.purple,sanctuary,20);
    cylinder(1.25,1.25,.09,x,.32,z,p.cream,sanctuary,20);
    box(.82,.21,.64,x,.46,z+.56,p.peach,sanctuary);
    box(.74,.25,.14,x,.56,z+.87,p.cream,sanctuary);
    sphere(.17,x-.24,.57,z+.55,p.gold,sanctuary,[1,.5,1]);
  }
  label('SPOLOČNÍCI · TVOJA MALÁ VEĽKÁ PARTIA',-17,3.75,17.83,12.8,.65,'#31756d','#efffe8');
  // Books, watering cans, paw tiles and a supply cart turn the empty lawn into a lived-in campus.
  const supply=landmark('DiscoverySupplyCart',-29,7.1);
  box(2.3,.16,1.3,0,.72,0,p.brown,supply);box(.1,1.1,1.45,-1.17,.95,0,p.teal,supply);
  for(const side of [-1,1]){const wheel=cylinder(.38,.38,.13,side*.87,.38,.44,p.ink,supply,12);wheel.rotation.x=Math.PI/2;}
  for(let i=0;i<5;i++)box(.22,.55+(i%3)*.12,.76,-.63+i*.3,1.1,0,[p.gold,p.purple,p.blue][i%3],supply);
  box(.13,1.7,.13,1.1,1.2,0,p.teal,supply);
  label('MALÉ KROKY. VEĽKÉ OBJAVY.',-28.9,2.18,7.28,4.2,.46,'#397b78','#fff7e2');
  for(const [x,z] of [[-20,10.7],[-17,10.7],[-14,10.7]]){
    cylinder(.29,.29,.016,x,.062,z,p.cream,root,12);
    for(const side of [-1,1])cylinder(.11,.11,.016,x+side*.24,.063,z-.33,p.cream,root,10);
  }

  // Side gardens use common box/leaf buffers, keeping detail independent of display resolution.
  for(const [x,z,accent] of [[11,-7,p.purple],[-10,-9,p.peach],[11,15,p.gold],[-29,20,p.purple]]){
    box(2.5,.22,2.5,x,.17,z,p.cream);box(2.25,.12,2.25,x,.34,z,p.brown);
    for(let i=0;i<3;i++){
      const a=i*Math.PI*2/3;sphere(.47,x+Math.cos(a)*.57,.7,z+Math.sin(a)*.57,p.leafLight);
      sphere(.17,x+Math.cos(a)*.55,1.1,z+Math.sin(a)*.55,accent);
    }
  }
  return landmarks;
}

/** World-space detail stays crisp on high-density displays without downloading large textures. */
export function addCampusSurfaceDetail(material,style='stone'){
  material.onBeforeCompile=shader=>{
    shader.vertexShader=`varying vec3 academySurfacePosition;\n${shader.vertexShader}`.replace('#include <begin_vertex>',`#include <begin_vertex>
      vec4 academyWorldPosition=vec4(transformed,1.0);
      #ifdef USE_INSTANCING
        academyWorldPosition=instanceMatrix*academyWorldPosition;
      #endif
      academySurfacePosition=(modelMatrix*academyWorldPosition).xyz;`);
    shader.fragmentShader=`varying vec3 academySurfacePosition;\n${shader.fragmentShader}`.replace('#include <color_fragment>',`#include <color_fragment>
      vec2 academyCell=floor(academySurfacePosition.xz*${style==='grass'?'5.0':'2.3'});
      float academyGrain=fract(sin(dot(academyCell,vec2(127.1,311.7)))*43758.5453);
      diffuseColor.rgb*=.96+academyGrain*.08;
      ${style==='stone'?`vec2 academyPaving=abs(fract(academySurfacePosition.xz*.66)-.5);
      float academySeam=smoothstep(.465,.49,max(academyPaving.x,academyPaving.y));
      diffuseColor.rgb=mix(diffuseColor.rgb,diffuseColor.rgb*.91,academySeam);`:''}`);
  };
  material.customProgramCacheKey=()=>`academy-surface-${style}-v1`;
}
