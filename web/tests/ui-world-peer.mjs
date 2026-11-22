import {setTimeout as delay} from 'node:timers/promises';
import {createHttpVisitor} from './http-client.mjs';
const [base,code]=process.argv.slice(2),call=createHttpVisitor(base);
const {me}=await call('/session',{name:'Testovací hráč'});
const joined=await call(`/worlds/${code}/join`,{pet:{kind:'fox',color:'#f6b593',name:'Líška'},position:{x:3,y:0,z:13,yaw:0}});
if(joined.status!==200)throw Error(joined.message);
console.log('Independent test visitor joined shared world '+code);
let stopped=false,signature='';process.on('SIGINT',()=>{stopped=true;});
try{
  for(let i=0;i<240&&!stopped;i++){
    const x=3+Math.sin(i*.35),z=12+Math.cos(i*.35),yaw=Math.atan2(-Math.cos(i*.35),Math.sin(i*.35));
    const snapshot=await call(`/worlds/${code}/move`,{position:{x,y:0,z,yaw},moving:true,onGround:true,...(i%30===0?{emote:'wave'}:{})});
    if(snapshot.status!==200)throw Error(snapshot.message);
    const host=snapshot.world.players.find(p=>p.id!==me.id);
    if(host){const key=[Math.round(host.position.x),Math.round(host.position.z)].join(',');if(key!==signature){signature=key;console.log('Observed browser visitor at '+key);}}
    await delay(500);
  }
}finally{await call(`/worlds/${code}/leave`,{});console.log('Test visitor left shared world.');}
