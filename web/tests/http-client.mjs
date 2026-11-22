/** Two instances model independent browsers, including hosting-layer cookies. */
export function createHttpVisitor(base){
  base=String(base).replace(/\/+$/,'');
  const jar=new Map();
  return async(path,data)=>{
    const response=await fetch(base+'/api/multiplayer'+path,{method:data?'POST':'GET',headers:{...(jar.size?{Cookie:[...jar].map(([k,v])=>`${k}=${v}`).join('; ')}:{}),...(data?{'Content-Type':'application/json'}:{})},...(data?{body:JSON.stringify(data)}:{})});
    for(const cookie of response.headers.getSetCookie()){
      const pair=cookie.split(';')[0],eq=pair.indexOf('=');
      if(eq>=0)jar.set(pair.slice(0,eq),pair.slice(eq+1));
    }
    return {status:response.status,...await response.json()};
  };
}
