import {readFile,writeFile,mkdir,stat} from 'node:fs/promises';
import path from 'node:path';
import {randomUUID} from 'node:crypto';

// Opt-in companion for the real Unity RoomChatSmokeCheck. No stored profile or credentials are read.
const CANONICAL='https://numerion.example.invalid';
const options=new Map(),flags=new Set();
for(let i=2;i<process.argv.length;i++){
  const key=process.argv[i];
  if(key==='--owner-production'){flags.add(key);continue;}
  if(!['--native-output','--origin','--timeout','--output'].includes(key)||!process.argv[i+1]||process.argv[i+1].startsWith('--'))
    throw new Error('Use --native-output, --origin, optional --timeout/--output, and --owner-production for the owned public Site.');
  if(options.has(key))throw new Error('Specify each option once.');
  options.set(key,process.argv[++i]);
}
function artifactPath(value,label){
  if(!value||!path.isAbsolute(value))throw new Error(label+' must be an absolute path under Artifacts.');
  const resolved=path.resolve(value);
  if(!resolved.split(path.sep).some(part=>part.toLowerCase()==='artifacts'))throw new Error(label+' must be under Artifacts.');
  return resolved;
}
const nativeOutput=artifactPath(options.get('--native-output'),'Native evidence');
const output=options.has('--output')?artifactPath(options.get('--output'),'Peer evidence'):'';
if(output&&output.toLowerCase()===nativeOutput.toLowerCase())throw new Error('Peer output must differ from native evidence.');
if(!options.has('--origin'))throw new Error('Select the exact native --origin.');
const origin=new URL(options.get('--origin'));
if(origin.username||origin.password||origin.pathname!=='/'||origin.search||origin.hash)throw new Error('Use an origin without credentials, path, query, or fragment.');
const loopback=['localhost','127.0.0.1','[::1]'].includes(origin.hostname)&&['http:','https:'].includes(origin.protocol);
if(!loopback&&!(origin.origin===CANONICAL&&flags.has('--owner-production')))
  throw new Error('Use loopback, or the owned canonical Site with --owner-production.');
const rawTimeout=options.get('--timeout')||'180';
if(!/^\d+$/.test(rawTimeout))throw new Error('Timeout must be an integer from 30 to 240 seconds.');
const timeout=Number(rawTimeout);
if(timeout<30||timeout>240)throw new Error('Timeout must be from 30 to 240 seconds.');
const started=Date.now(),deadline=started+timeout*1000;
const result={schemaVersion:1,evidenceKind:'Actual native UnityWebRequest SDK plus an independent HTTP peer; no rendered Web or native UI claim.',
  phase:'WAIT_NATIVE_ROOM',origin:origin.origin,passed:false,timedOut:false,checks:0,elapsedSeconds:0,
  nativeTextSeen:false,nativeWaveSeen:false,peerTextDelivered:false,peerHeartDelivered:false,nativePassed:false,
  heartbeatCount:0,leaveRequested:false,cleanupSucceeded:false,error:''};
let person,code='',runId='',expectedNative='',expectedPeer='',nativeAuthor='',nativeTextId=0,nativeWaveId=0,peerTextId=0,peerHeartId=0;
let heartbeatTimer,heartbeatPending=false,heartbeatFailure=false,sequence=0,stopping=false,interrupted=false;
const signal=()=>{interrupted=true;};process.on('SIGINT',signal);process.on('SIGTERM',signal);
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
function ensure(value,label){if(!value)throw new Error(label);result.checks++;}
function bounded(){if(interrupted)throw new Error('QA interrupted.');if(Date.now()>=deadline){result.timedOut=true;throw new Error('Timed out waiting for the native SDK chat roundtrip.');}}
const presence={name:'Numerion HTTP QA',skin:2,outfit:5,hair:1,accessory:0,pet:'zlomko',petGear:0,grade:7,x:0,y:0,z:12,yaw:0,party:['zlomko']};
async function call(route,data,token='',expected=200){
  const response=await fetch(new URL('/api/unity/'+route,origin),{method:'POST',redirect:'error',
    headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:JSON.stringify(data),signal:AbortSignal.timeout(8000)});
  ensure(response.status===expected,'Unexpected HTTP status for '+route+'.');
  let reply;try{reply=await response.json();}catch{throw new Error('Non-JSON response for '+route+'.');}
  ensure(reply?.ok===true,'The service rejected '+route+'.');return reply;
}
async function nativeEvidence(){
  try{
    const info=await stat(nativeOutput);ensure(info.size<=65536,'Native evidence exceeds the bounded QA format.');
    return JSON.parse(await readFile(nativeOutput,'utf8'));
  }catch(error){if(error.code==='ENOENT'||error instanceof SyntaxError)return null;throw error;}
}
function verifyRun(evidence){
  ensure(evidence.RunId===runId&&evidence.RoomCode===code&&evidence.ServerOrigin===origin.origin,'The native QA run changed while the peer was connected.');
}
async function heartbeat(){
  if(heartbeatPending||stopping)return;heartbeatPending=true;
  try{await call('rooms/pulse',{code,sequence:++sequence,presence},person.identityToken);result.heartbeatCount++;}
  catch{heartbeatFailure=true;}finally{heartbeatPending=false;}
}
async function chatSend(text='',emoteId=''){
  const reply=await call('rooms/chat/send',{code,text,emoteId,clientMessageId:randomUUID().replaceAll('-','')},person.identityToken);
  const item=reply.chat?.messages?.findLast(m=>m.authorId===person.identityId&&m.text===text&&m.emoteId===emoteId);
  ensure(item&&Number.isSafeInteger(item.id),'The peer send did not return its own server-generated echo.');return item.id;
}
try{
  while(!code){
    bounded();const evidence=await nativeEvidence();
    if(evidence?.TimedOut||['FAIL','TIMEOUT'].includes(evidence?.Phase))throw new Error('Native QA reported failure before the peer could join.');
    if(evidence?.RoomCode&&/^[A-Z2-9]{6}$/.test(evidence.RoomCode)){
      ensure(evidence.ServerOrigin===origin.origin,'Native evidence uses a different origin.');
      ensure(/^[0-9a-f]{12}$/.test(evidence.RunId||''),'Native evidence has no valid unique run id.');
      ensure(evidence.SDKText==='Numerion native SDK chat QA '+evidence.RunId&&evidence.PeerText==='Numerion HTTP peer chat QA '+evidence.RunId,'Native evidence is not the expected chat smoke contract.');
      ensure(!evidence.Passed,'Native QA already completed; start a fresh native host.');
      code=evidence.RoomCode;runId=evidence.RunId;expectedNative=evidence.SDKText;expectedPeer=evidence.PeerText;
    }else await sleep(250);
  }
  result.phase='JOINING_NATIVE_ROOM';
  const saveJson=JSON.stringify({Profile:{DisplayName:presence.name,Grade:7},Companions:{Owned:[{SpeciesId:'zlomko'}],EquippedId:'zlomko'},Purchases:[]});
  person=await call('profile/create',{saveJson},'',201);
  const joined=await call('rooms/join',{code,presence},person.identityToken);
  ensure(joined.room?.players?.some(peer=>peer.id===person.identityId),'The independent peer was not admitted to the native room.');
  heartbeatTimer=setInterval(heartbeat,4000);
  result.phase='WAIT_NATIVE_TEXT_AND_WAVE';
  while(!result.nativeTextSeen||!result.nativeWaveSeen){
    bounded();if(heartbeatFailure)throw new Error('Independent peer presence heartbeat failed.');
    const evidence=await nativeEvidence();if(evidence){verifyRun(evidence);if(evidence.TimedOut||evidence.Phase==='FAIL')throw new Error('Native QA failed while waiting for SDK messages.');}
    const reply=await call('rooms/chat',{code,afterId:0},person.identityToken);
    for(const message of reply.chat?.messages||[]){
      if(message.text===expectedNative&&message.authorId!==person.identityId&&message.authorName==='Numerion QA'){
        nativeAuthor=message.authorId;nativeTextId=message.id;result.nativeTextSeen=true;
      }
      if(nativeAuthor&&message.authorId===nativeAuthor&&message.emoteId==='wave'&&message.id>nativeTextId){
        nativeWaveId=message.id;result.nativeWaveSeen=true;
      }
    }
    if(!result.nativeTextSeen||!result.nativeWaveSeen)await sleep(750);
  }
  result.phase='SEND_PEER_TEXT_AND_HEART';
  peerTextId=await chatSend(expectedPeer);result.peerTextDelivered=true;
  await sleep(1250);bounded();peerHeartId=await chatSend('','heart');result.peerHeartDelivered=true;
  result.phase='WAIT_NATIVE_PASS';
  while(!result.nativePassed){
    bounded();if(heartbeatFailure)throw new Error('Independent peer presence heartbeat failed.');
    const evidence=await nativeEvidence();
    if(evidence){
      verifyRun(evidence);
      if(evidence.TimedOut||evidence.Phase==='FAIL')throw new Error('Native QA did not accept the completed peer roundtrip.');
      if(evidence.Passed){
        ensure(evidence.Phase==='PASS'&&evidence.SelfTextEcho&&evidence.SelfWaveEcho&&evidence.PeerTextSeen&&evidence.PeerHeartSeen&&evidence.IndependentPeerPresent,'Native PASS is missing required chat observations.');
        ensure(evidence.SelfTextId===nativeTextId&&evidence.SelfWaveId===nativeWaveId&&evidence.PeerTextId===peerTextId&&evidence.PeerHeartId===peerHeartId,'Native and HTTP observations disagree on server message sequence ids.');
        result.nativePassed=true;
      }
    }
    if(!result.nativePassed)await sleep(500);
  }
  result.phase='PASS';result.passed=true;
}catch(error){
  result.phase=result.timedOut?'TIMEOUT':'FAIL';
  // Only locally authored, bounded diagnostics are exported. Transport/library error text can contain URLs.
  result.error=error instanceof Error&&/^(QA interrupted|Timed out|Unexpected HTTP|Non-JSON response|The service rejected|Native |The native |The independent |Independent peer |The peer send)/.test(error.message)?error.message:'The chat SDK peer check could not complete.';
}finally{
  stopping=true;clearInterval(heartbeatTimer);
  while(heartbeatPending)await sleep(50);
  if(person&&code){result.leaveRequested=true;try{await call('rooms/leave',{code},person.identityToken);result.cleanupSucceeded=true;}catch{}}
  process.removeListener('SIGINT',signal);process.removeListener('SIGTERM',signal);
  result.elapsedSeconds=Math.round((Date.now()-started)/1000);
  if(output){await mkdir(path.dirname(output),{recursive:true});await writeFile(output,JSON.stringify(result,null,2)+'\n','utf8');}
  if(result.passed)console.log(`NUMERION_CHAT_NATIVE_SDK_HTTP_PEER_PASS ${result.checks} checks; native text/wave and independent peer text/heart observed; cleanup=${result.cleanupSucceeded}. No rendered UI claim.`);
  else{console.error(`NUMERION_CHAT_NATIVE_SDK_HTTP_PEER_${result.phase}: ${result.error}`);process.exitCode=1;}
  person=null;code='';
}
