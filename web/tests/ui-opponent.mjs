import {setTimeout as delay} from 'node:timers/promises';
import {AcademyEngine} from '../public/academy-engine.js';
import {createHttpVisitor} from './http-client.mjs';
const [base,code]=process.argv.slice(2);
const bank=new Map(new AcademyEngine(null).getDefinitions().questions.map(q=>[q.id,q]));
const visitor=createHttpVisitor(base);
async function call(path,data){const result=await visitor(path,data);if(result.status>=400)throw Error(result.message);return result;}
const {me}=await call('/session',{name:'Overenie hráča'});
await call(`/rooms/${code}/join`,{pet:{kind:'robot',color:'#91d3ee',name:'Akademik'}});console.log('Independent test visitor joined '+code);
let round=0;
for(let i=0;i<720;i++){
 const {room}=await call(`/rooms/${code}`);
 if(room.status==='finished'){console.log('UI match finished; observer stopping.');break;}
 if(room.status==='question'&&room.round!==round&&room.players.some(p=>p.id!==me.id&&p.answered)){
  const q=bank.get(room.question.id);await call(`/rooms/${code}/answer`,{round:room.round,answerId:q.options.find(o=>o.id!==q.answer).id,move:'guard'});round=room.round;console.log('Guest answer '+round+' accepted.');
 }
 await delay(500);
}
