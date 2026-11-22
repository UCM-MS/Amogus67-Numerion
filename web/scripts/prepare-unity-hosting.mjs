import {readFileSync,writeFileSync,existsSync,unlinkSync,copyFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve,join,sep} from 'node:path';
import {fileURLToPath} from 'node:url';

export function splitUnityData(bytes, chunkSize = 20 * 1024 * 1024) {
  if (!bytes.length || !Number.isSafeInteger(chunkSize) || chunkSize < 1 || chunkSize > 20 * 1024 * 1024)
    throw new Error('Invalid Unity data or chunk size.');
  const hash = createHash('sha256').update(bytes).digest('hex'), files = [], parts = [];
  for (let offset = 0, index = 0; offset < bytes.length; offset += chunkSize, index++) {
    const data = bytes.subarray(offset, offset + chunkSize), file = `numerion-data-${hash.slice(0,16)}-${String(index).padStart(2,'0')}.bin`;
    files.push({file,data}); parts.push({file,bytes:data.length,integrity:'sha256-'+createHash('sha256').update(data).digest('base64')});
  }
  return {files,manifest:{schema:1,filename:'WebGL.data.unityweb',bytes:bytes.length,sha256:hash,parts}};
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = resolve('public/unity/Build'), original = join(root,'WebGL.data.unityweb');
  if (!existsSync(original)) throw new Error('Build the Unity Web player before preparing hosted assets.');
  const manifestPath=join(root,'data-manifest.json');
  const previous=existsSync(manifestPath)?JSON.parse(readFileSync(manifestPath,'utf8')):null;
  const result = splitUnityData(readFileSync(original));
  // Static hosting resolves /index.html before the application route. Keep both entry points identical.
  copyFileSync(resolve('public/unity/index.html'),resolve('public/index.html'));
  for (const item of result.files) writeFileSync(join(root,item.file),item.data);
  writeFileSync(manifestPath,JSON.stringify(result.manifest,null,2)+'\n');
  const current=new Set(result.files.map(item=>item.file));
  if(previous?.schema===1&&Array.isArray(previous.parts))for(const item of previous.parts){
    if(!/^numerion-data-[a-f0-9]{16}-\d{2}\.bin$/.test(item.file||'')||current.has(item.file))continue;
    const stale=resolve(root,item.file);
    if(!stale.startsWith(root+sep))throw new Error('Unsafe stale asset path.');
    if(existsSync(stale))unlinkSync(stale);
  }
  // Only the isolated publication checkout removes its oversized original; local previews keep it.
  if (process.argv.includes('--chunked-only')) unlinkSync(original);
  console.log(JSON.stringify({status:'UNITY_HOSTING_READY',bytes:result.manifest.bytes,parts:result.files.length,sha256:result.manifest.sha256,originalRetained:existsSync(original)}));
}
