import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {gzipSync,gunzipSync} from 'node:zlib';
import {splitUnityData} from '../scripts/prepare-unity-hosting.mjs';
import {validateDataManifest,prepareNumerionData} from '../public/unity-data.js';

globalThis.location={href:'https://numerion.test/'};
const original=gzipSync(Buffer.from(Array.from({length:3000},(_,i)=>'Numerion '+i+'\n').join('')));
const split=splitUnityData(original,1024), urls=[];
assert.ok(split.files.length>1); assert.deepEqual(Buffer.concat(split.files.map(f=>f.data)),original);
for(const part of split.manifest.parts){const file=split.files.find(f=>f.file===part.file);assert.equal(part.integrity,'sha256-'+createHash('sha256').update(file.data).digest('base64'));}
validateDataManifest(split.manifest);
assert.throws(()=>validateDataManifest({...split.manifest,bytes:1}));
assert.throws(()=>validateDataManifest({...split.manifest,parts:[{...split.manifest.parts[0],file:'https://evil.test/data'}]}));
assert.throws(()=>validateDataManifest({...split.manifest,parts:[split.manifest.parts[0],split.manifest.parts[0]]}));
const loader=async(url,options)=>{
  urls.push(String(url));
  if(String(url).includes('data-manifest'))return Response.json(split.manifest);
  const file=split.files.find(f=>String(url).endsWith(f.file));assert.ok(file);assert.ok(options.integrity);
  return new Response(file.data);
};
const steps=[],prepared=await prepareNumerionData('/unity/Build/WebGL.data.unityweb?v=0.8.0',p=>steps.push(p),loader);
const loaded=Buffer.from(await (await fetch(prepared.url)).arrayBuffer());
assert.deepEqual(loaded,original);assert.deepEqual(gunzipSync(loaded),gunzipSync(original));assert.equal(steps.at(-1),1);prepared.release();
assert.equal(urls[0],'https://numerion.test/unity/Build/data-manifest.json?v=0.8.0');
const fallback=await prepareNumerionData('/unity/Build/WebGL.data.unityweb',null,async()=>new Response('',{status:404}));assert.match(fallback.url,/WebGL.data.unityweb$/);
await assert.rejects(()=>prepareNumerionData('/unity/Build/WebGL.data.unityweb',null,async url=>String(url).includes('manifest')?Response.json(split.manifest):new Response('short')));
console.log('UNITY_HOSTING_PASS: exact gzip reconstruction, integrity metadata, bounded safe parts, progress, missing/truncated data, original fallback.');
