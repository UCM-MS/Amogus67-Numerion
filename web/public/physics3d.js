/** Collision and landing rules shared by the controller and deterministic course tests. */
const overlaps = (x, z, p, radius) => Math.abs(x-p.x)<p.hx+radius && Math.abs(z-p.z)<p.hz+radius;

/** Shorten the camera boom before it enters a landmark wall. */
export function clipCameraLine(start,end,colliders=[]) {
  const delta={x:end.x-start.x,y:end.y-start.y,z:end.z-start.z};
  const length=Math.hypot(delta.x,delta.y,delta.z);let nearest=1;
  if(length<.001)return {...end};
  for(const box of colliders){
    const lo={x:box.x-box.hx-.25,y:box.bottom??0,z:box.z-box.hz-.25};
    const hi={x:box.x+box.hx+.25,y:box.top??box.height??7,z:box.z+box.hz+.25};
    let enter=0,leave=1,hit=true;
    for(const axis of ['x','y','z']){
      if(Math.abs(delta[axis])<1e-8){if(start[axis]<lo[axis]||start[axis]>hi[axis])hit=false;continue;}
      let a=(lo[axis]-start[axis])/delta[axis],b=(hi[axis]-start[axis])/delta[axis];if(a>b)[a,b]=[b,a];
      enter=Math.max(enter,a);leave=Math.min(leave,b);if(enter>leave)hit=false;
    }
    // A player at the edge of a platform can start inside its visual bounds.
    if(hit&&enter>0&&enter<=1&&leave>=0)nearest=Math.min(nearest,Math.max(.08,enter-.45/length));
  }
  return {x:start.x+delta.x*nearest,y:start.y+delta.y*nearest,z:start.z+delta.z*nearest};
}

export function isBlocked(x, z, feet, colliders = [], platforms = [], radius = .55) {
  return colliders.some(p=>overlaps(x,z,p,radius)) || platforms.some(p=>p.top>feet+.08 && overlaps(x,z,p,radius));
}

export function supportAt(x, z, feet, platforms = []) {
  return platforms.find(p=>Math.abs(feet-p.top)<.06 && overlaps(x,z,p,.25)) || (Math.abs(feet)<.06?{id:'ground',top:0}:null);
}

export function stepMotion(body, dx, dz, dt, world) {
  const {colliders=[],platforms=[],bounds={minX:-49,maxX:49,minZ:-49,maxZ:49}}=world;
  const result={...body,landed:false};
  const steps=Math.max(1,Math.ceil(dt/.02)),step=dt/steps;
  for(let i=0;i<steps;i++){
    if(!isBlocked(result.x+dx/steps,result.z,result.y,colliders,platforms))result.x=Math.max(bounds.minX+1,Math.min(bounds.maxX-1,result.x+dx/steps));
    if(!isBlocked(result.x,result.z+dz/steps,result.y,colliders,platforms))result.z=Math.max(bounds.minZ+1,Math.min(bounds.maxZ-1,result.z+dz/steps));
    if(result.onGround&&!supportAt(result.x,result.z,result.y,platforms))result.onGround=false;
    const previousY=result.y;
    result.vy-=22*step;result.y+=result.vy*step;
    let landing=null;
    if(result.vy<=0){
      for(const p of platforms)if(previousY>=p.top-.001&&result.y<=p.top&&overlaps(result.x,result.z,p,.25)&&(!landing||p.top>landing.top))landing=p;
      if(!landing&&result.y<=0)landing={id:'ground',top:0};
    }
    if(landing){result.landed ||= !result.onGround;result.y=landing.top;result.vy=0;result.onGround=true;result.platformId=landing.id;}
    else{result.onGround=false;result.platformId=null;}
  }
  return result;
}

/** Find a walking route around walls; a one-metre grid fits the base entrance. */
export function findWalkingPath(start,target,world){
  const blocked=(x,z)=>isBlocked(x,z,0,world.colliders,world.platforms);
  let sx=Math.round(start.x),sz=Math.round(start.z);
  if(blocked(sx,sz)){
    const candidates=[];
    for(let dx=-3;dx<=3;dx++)for(let dz=-3;dz<=3;dz++)if(!blocked(sx+dx,sz+dz))candidates.push({x:sx+dx,z:sz+dz,d:Math.hypot(dx,dz)});
    candidates.sort((a,b)=>a.d-b.d);if(!candidates.length)return [{x:target.x,z:target.z}];sx=candidates[0].x;sz=candidates[0].z;
  }
  const key=(x,z)=>`${x},${z}`,queue=[{x:sx,z:sz}],parents=new Map([[key(sx,sz),null]]),bounds=world.bounds;
  let end=null;
  for(let cursor=0;cursor<queue.length;cursor++){
    const point=queue[cursor];
    if(Math.hypot(point.x-target.x,point.z-target.z)<1.15){end=point;break;}
    const directions=[[1,0],[-1,0],[0,1],[0,-1]].sort((a,b)=>Math.hypot(point.x+a[0]-target.x,point.z+a[1]-target.z)-Math.hypot(point.x+b[0]-target.x,point.z+b[1]-target.z));
    for(const [dx,dz] of directions){const x=point.x+dx,z=point.z+dz,k=key(x,z);if(parents.has(k)||x<bounds.minX+1||x>bounds.maxX-1||z<bounds.minZ+1||z>bounds.maxZ-1||blocked(x,z))continue;parents.set(k,point);queue.push({x,z});}
  }
  if(!end)return [{x:target.x,z:target.z}];
  const path=[];for(let point=end;point;point=parents.get(key(point.x,point.z)))path.push(point);path.reverse();
  // Retain corners, removing the many collinear grid points.
  const corners=path.filter((p,i)=>!i||i===path.length-1||(p.x-path[i-1].x)*(path[i+1].z-p.z)!==(p.z-path[i-1].z)*(path[i+1].x-p.x));
  corners.push({x:target.x,z:target.z});return corners;
}
