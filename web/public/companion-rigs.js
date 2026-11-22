/** Six original companion families. Each rig owns transforms while sharing immutable GPU buffers. */
export function createCompanionRig(T,pet,{resources,material,piece,cube,ball,batch}){
  const kinds=['bunny','cat','fox','owl','robot','dragon'];
  const kind=kinds.includes(pet.kind)?pet.kind:'bunny';
  const color=/^#[\da-f]{6}$/i.test(pet.color||'')?pet.color:'#bc9bf2';
  const level=Math.min(5,Math.max(1,Math.floor(Number(pet.level)||1)));
  const rarity=['common','uncommon','rare','epic','legendary'].includes(pet.rarity)?pet.rarity:'common';
  const rarityColor={common:'#a7d7bc',uncommon:'#8adbe4',rare:'#80a7ff',epic:'#bb92f8',legendary:'#ffd16a'}[rarity];
  const cream='#fff2da',ink='#25364f',pink='#f19bb1',gold='#ffd052',metal='#536a86';
  const highlight=`#${new T.Color(color).offsetHSL(0,-.05,.075).getHexString()}`;
  const group=new T.Group(),body=new T.Group(),eyes=new T.Group(),ears=[],tails=[],wings=[],feet=[];
  group.name=`Companion-${kind}`;body.name='CompanionBody';group.add(body);body.add(eyes);
  group.userData.kind=kind;group.userData.level=level;group.userData.rarity=rarity;
  eyes.name='CompanionEyes';eyes.position.set(0,1.22,kind==='robot'?-.646:kind==='fox'?-.594:-.58);
  const cache=resources(T);
  if(!cache.companionStar){
    const shape=new T.Shape();for(let i=0;i<10;i++){const a=Math.PI/2+i*Math.PI/5,r=i%2?.45:1;i?shape.lineTo(Math.cos(a)*r,Math.sin(a)*r):shape.moveTo(Math.cos(a)*r,Math.sin(a)*r);}shape.closePath();
    cache.companionStar=new T.ExtrudeGeometry(shape,{depth:.08,bevelEnabled:false});
  }
  const star=(parent,x,y,z,size,starColor=gold)=>{const mesh=new T.Mesh(cache.companionStar,material(T,starColor));mesh.position.set(x,y,z);mesh.scale.set(size,size,size);parent.add(mesh);return mesh;};
  const earPivot=(side,y=1.52)=>{const ear=new T.Group();ear.position.set(side*.32,y,-.04);body.add(ear);ear.name='CompanionEar';ears.push(ear);return ear;};
  function tailPivot(x=.28,y=.57,z=.4){const tail=new T.Group();tail.position.set(x,y,z);tail.name='CompanionTail';body.add(tail);tails.push(tail);return tail;}
  if(kind==='bunny'){
    ball(T,body,color,0,.65,.05,.58,.58,.51);ball(T,body,color,0,1.2,-.07,.59,.54,.52);
    ball(T,body,cream,0,.62,-.445,.32,.35,.065);
    for(const side of [-1,1]){
      const ear=earPivot(side,1.58);ball(T,ear,color,0,.29,0,.17,.57,.15);ball(T,ear,pink,0,.31,-.129,.083,.4,.027);
      ball(T,body,cream,side*.16,1.035,-.54,.2,.12,.07);
    }
    ball(T,body,pink,0,1.09,-.625,.065,.05,.05);
    const tail=tailPivot(0,.56,.52);ball(T,tail,cream,0,0,0,.22);
  }else if(kind==='owl'){
    ball(T,body,color,0,.75,.04,.63,.65,.49);ball(T,body,highlight,0,1.2,-.04,.64,.55,.51);
    for(const side of [-1,1]){
      ball(T,body,cream,side*.24,1.23,-.51,.29,.31,.095);
      const ear=earPivot(side,1.55);piece(T,ear,'cone',color,0,.14,0,.24,.42,.25);
      const wing=new T.Group();wing.position.set(side*.49,.87,.03);wing.name='CompanionWing';body.add(wing);wings.push(wing);
      ball(T,wing,color,side*.13,-.06,0,.29,.43,.31);
      for(let i=0;i<3;i++)cube(T,wing,cream,side*.2,-.04-i*.14,-.23,.22,.055,.06);
    }
    piece(T,body,'cone',gold,0,1.025,-.665,.15,.22,.16).rotation.x=-Math.PI/2;
    for(let row=0;row<3;row++)for(const side of [-1,1]){const feather=cube(T,body,cream,side*(.11+row*.035),.89-row*.15,-.425,.12,.06,.03);feather.rotation.z=side*.3;}
  }else if(kind==='robot'){
    cube(T,body,color,0,.69,.02,.86,.75,.73);cube(T,body,highlight,0,1.21,-.06,1.04,.79,.88);
    cube(T,body,metal,0,1.23,-.522,.85,.44,.08);cube(T,body,'#5bdedc',0,1.23,-.57,.72,.31,.04);
    cube(T,body,cream,0,.95,-.39,.59,.1,.07);cube(T,body,metal,0,1.72,-.04,.075,.36,.075);ball(T,body,gold,0,1.96,-.04,.12);
    for(const side of [-1,1]){
      ball(T,body,metal,side*.54,1.21,-.04,.14);cube(T,body,metal,side*.55,.67,.02,.17,.48,.25);cube(T,body,cream,side*.55,.42,-.02,.23,.19,.29);
      cube(T,body,'#5bdedc',side*.35,.69,-.38,.055,.27,.03);
    }
    cube(T,body,metal,0,.65,-.38,.39,.35,.06);star(body,0,.65,-.43,.13,gold);
    for(let i=0;i<3;i++)cube(T,body,ink,-.1+i*.1,1.03,-.615,.045,.035,.025);
  }else{
    cube(T,body,color,0,.67,kind==='fox'?.09:.04,kind==='fox'?.82:.89,.74,kind==='fox'?1.02:.83);
    cube(T,body,highlight,0,1.22,-.1,kind==='cat'?1.01:.96,.78,.88);
    ball(T,body,cream,0,.65,-.4,.29,.36,.07);
    for(const side of [-1,1]){
      const ear=earPivot(side,1.57);piece(T,ear,'cone',color,0,.16,0,.28,.47,.29).rotation.y=Math.PI/6;
      piece(T,ear,'cone',kind==='fox'?cream:pink,0,.13,-.065,.15,.29,.16).rotation.y=Math.PI/6;
      ball(T,body,pink,side*.35,1.06,-.548,.1,.052,.026);
    }
    if(kind==='fox'){
      cube(T,body,cream,0,1.07,-.657,.59,.29,.29);cube(T,body,ink,0,1.14,-.84,.14,.1,.12);
      const tail=tailPivot(.34,.58,.43);tail.rotation.x=-.38;
      ball(T,tail,color,.1,.15,.44,.31,.3,.65);ball(T,tail,cream,.1,.22,.96,.29,.28,.27);
      for(const side of [-1,1])cube(T,body,color,side*.41,1.15,-.6,.15,.13,.07);
    }else if(kind==='cat'){
      ball(T,body,cream,0,1.05,-.543,.29,.13,.06);ball(T,body,pink,0,1.105,-.624,.06,.05,.035);
      for(const side of [-1,1])for(let i=0;i<2;i++){const whisker=cube(T,body,ink,side*.46,1.025+i*.09,-.546,.22,.02,.023);whisker.rotation.z=side*(i?-.16:.13);}
      const tail=tailPivot(.4,.48,.4);tail.rotation.x=-.7;ball(T,tail,color,0,.14,.36,.16,.18,.48);ball(T,tail,cream,0,.29,.76,.15,.21,.17);
      for(let i=0;i<3;i++)cube(T,body,color,-.2+i*.2,1.57,-.53,.075,.13,.028);
    }else{
      cube(T,body,cream,0,1.1,-.63,.61,.26,.29);for(const side of [-1,1])ball(T,body,color,side*.18,1.21,-.8,.04,.026,.03);
      if(!cache.dragonWing){const shape=new T.Shape();shape.moveTo(0,0);shape.lineTo(.72,.51);shape.lineTo(.93,-.34);shape.lineTo(.5,-.14);shape.lineTo(.24,-.36);shape.closePath();cache.dragonWing=new T.ExtrudeGeometry(shape,{depth:.07,bevelEnabled:false});}
      for(const side of [-1,1]){
        const wing=new T.Group();wing.name='CompanionWing';wing.position.set(side*.44,.95,.12);body.add(wing);wings.push(wing);
        const membrane=new T.Mesh(cache.dragonWing,material(T,rarity==='legendary'?'#eab06f':'#86cfa1'));membrane.scale.x=side;wing.add(membrane);
        const spar=cube(T,wing,color,side*.34,.26,.035,.78,.065,.1);spar.rotation.z=side*.51;
        piece(T,body,'cone',gold,side*.27,1.85,.02,.12,.37,.14);
      }
      for(let i=0;i<3;i++)piece(T,body,'cone',gold,0,.89-i*.14,.49+i*.19,.11,.22,.13);
      const tail=tailPivot(0,.52,.44);ball(T,tail,color,0,-.03,.41,.22,.2,.61);piece(T,tail,'cone',gold,0,.08,.92,.17,.24,.2);
    }
    if(kind!=='fox')cube(T,body,ink,0,1.01,-.624,.14,.028,.03);
  }
  // Collars identify collection rarity; earned stars and a crown show training progress in the world.
  if(kind!=='robot'){
    cube(T,body,rarityColor,0,.91,-.38,.74,.12,.1);
    star(body,0,.86,-.465,.115,level>=3?gold:cream);
  }
  for(let i=0;i<Math.min(3,Math.floor((level+1)/2));i++)star(body,-.15+i*.15,.56,-.46,.048,rarityColor);
  if(rarity==='legendary'||level===5){
    const crownY=kind==='bunny'?1.66:1.77;
    cube(T,body,gold,0,crownY,0,.45,.09,.37);
    for(let i=0;i<3;i++)piece(T,body,'cone',gold,-.17+i*.17,crownY+.12,-.08,.085,.22,.085);
    ball(T,body,rarityColor,0,crownY+.16,-.225,.055,.055,.026);
  }
  if(rarity==='epic'||rarity==='legendary')for(const side of [-1,1])star(body,side*.43,.73,-.44,.08,rarityColor);
  const footZ=['cat','fox','dragon'].includes(kind)?[-.26,.3]:[-.1];
  for(const z of footZ)for(const side of [-1,1]){
    const foot=new T.Group();foot.name='CompanionFoot';foot.position.set(side*.28,.29,z);body.add(foot);feet.push(foot);
    ball(T,foot,kind==='robot'?metal:cream,0,-.1,-.06,kind==='bunny'?.2:.17,.15,.25);
    if(kind!=='robot')for(let i=0;i<2;i++)cube(T,foot,kind==='dragon'?gold:color,-.055+i*.11,-.14,-.295,.04,.045,.03);
  }
  for(const side of [-1,1]){
    ball(T,eyes,ink,side*.22,0,0,.095,.125,.035);
    ball(T,eyes,'#ffffff',side*.204,.044,-.035,.029,.034,.014);
  }
  const movingParts=[eyes,...ears,...tails,...wings,...feet];
  batch(T,body,movingParts);movingParts.forEach(part=>batch(T,part));
  const seed=[...String(pet.name||kind)+color].reduce((sum,c)=>sum+c.charCodeAt(0),0),phase=(seed%61)*.13;
  group.userData.animate=(dt,time,{moving=false}={})=>{
    const t=Number.isFinite(time)?time:0;
    body.position.y=Math.sin(t*2.2+phase)*.022;
    wings.forEach((wing,index)=>{wing.rotation.z=(index?1:-1)*(.17+Math.sin(t*(kind==='dragon'?4.3:3.2)+phase)*.2);});
    ears.forEach((ear,index)=>{ear.rotation.z=Math.sin(t*1.7+phase+index*.8)*.055;ear.rotation.x=Math.sin(t*1.2+phase)*.045;});
    tails.forEach(tail=>{tail.rotation.y=Math.sin(t*2.2+phase)*.31;});
    feet.forEach((foot,index)=>{foot.rotation.x=moving?Math.sin(t*9+index*Math.PI)*.38:Math.sin(t*1.5+phase+index)*.025;});
    // Blink around the eye centre, so the pupils never collapse toward the floor.
    eyes.scale.y=(t+phase)%4.7<.12?.09:1;
  };
  group.userData.dispose=()=>group.removeFromParent();
  return group;
}
