import http from 'node:http';
import {readFile,stat,mkdir} from 'node:fs/promises';
import {existsSync} from 'node:fs';
import path from 'node:path';
const root=path.resolve('public');
let api,unityApi,DB;
if(existsSync('worker/multiplayer.mjs')&&existsSync('drizzle/meta/_journal.json')){
  const {createMultiplayerApi}=await import('./worker/multiplayer.mjs');
  const {createTestD1}=await import('./tests/d1-adapter.mjs');
  const {AcademyEngine}=await import('./public/academy-engine.js');
  await mkdir('.sites-runtime',{recursive:true});
  DB=createTestD1({file:'.sites-runtime/lumen.sqlite'});
  api=createMultiplayerApi({questions:new AcademyEngine(null).getDefinitions().questions});
  if(existsSync('worker/unity-online.mjs')&&existsSync('worker/unity-curriculum.mjs')){
    const {createUnityOnlineApi}=await import('./worker/unity-online.mjs');
    const {default:questions}=await import('./worker/unity-curriculum.mjs');
    unityApi=createUnityOnlineApi({questions});
  }
  if(existsSync('worker/explore.mjs')){
    const {createExploreApi}=await import('./worker/explore.mjs');const explore=createExploreApi(),match=api;
    api=async(request,env)=>await explore(request,env)||await match(request,env);
  }
}
const mime={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.png':'image/png','.svg':'image/svg+xml','.wasm':'application/wasm','.data':'application/octet-stream','.unityweb':'application/octet-stream'};
const server=http.createServer(async(req,res)=>{try{
  const url=new URL(req.url,`http://${req.headers.host||'127.0.0.1:4173'}`);
  if(url.pathname.startsWith('/api/multiplayer')||url.pathname.startsWith('/api/unity')){
    const handler=url.pathname.startsWith('/api/unity')?unityApi:api;
    if(!handler){res.writeHead(503,{'Content-Type':'application/json'});res.end(JSON.stringify({error:'Server arény sa pripravuje.'}));return;}
    let body=Buffer.alloc(0);for await(const chunk of req){body=Buffer.concat([body,chunk]);if(body.length>196608){res.writeHead(413);res.end();return;}}
    const request=new Request(url,{method:req.method,headers:req.headers,...(body.length?{body}: {})});
    const response=await handler(request,{DB})||new Response('Not found',{status:404});
    const headers=Object.fromEntries(response.headers);const cookies=response.headers.getSetCookie();if(cookies.length)headers['set-cookie']=cookies;
    res.writeHead(response.status,headers);res.end(Buffer.from(await response.arrayBuffer()));return;
  }
  const pathname=decodeURIComponent(url.pathname);
  const entry=pathname==='/'&&existsSync(path.join(root,'unity/index.html'))?'/unity/index.html':pathname==='/classic'||pathname==='/classic/'?'/classic.html':pathname==='/'?'/index.html':pathname;
  const file=path.resolve(root,'.'+entry);if(!file.startsWith(root+path.sep)){res.writeHead(403);res.end();return;}const fileInfo=await stat(file);res.writeHead(200,{'Content-Type':mime[path.extname(file)]||'application/octet-stream','Content-Length':fileInfo.size,'Cache-Control':'no-cache'});res.end(req.method==='HEAD'?undefined:await readFile(file));
}catch(e){if(e.code==='ENOENT'){res.writeHead(404);res.end('Not found');return;}console.error(e.message);res.writeHead(500,{'Content-Type':'application/json'});res.end(JSON.stringify({error:'Server nedokázal dokončiť požiadavku.'}));}});
const port=Number(process.env.PORT||4173);
const host=process.env.HOST||'127.0.0.1';
server.listen(port,host,()=>console.log(`Local: http://${host==='0.0.0.0'?'127.0.0.1':host}:${port}${host==='0.0.0.0'?' · LAN enabled on all IPv4 interfaces':''}`));
function stop(){server.close(()=>{DB?.close();process.exit(0);});}
process.on('SIGTERM',stop);process.on('SIGINT',stop);
