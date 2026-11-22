import assert from 'node:assert/strict';
import {mkdtempSync,existsSync,readFileSync,writeFileSync,rmSync} from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {prepareLocalHosting} from '../scripts/prepare-local-hosting.mjs';

let checks=0;const equal=(a,b,label)=>{assert.deepEqual(a,b,label);checks++;};
const root=mkdtempSync(path.join(os.tmpdir(),'lumen-local-hosting-'));
try{
  const file=path.join(root,'.openai','hosting.json');
  equal(prepareLocalHosting(root,{check:true}).status,'LOCAL_CONFIG_MISSING','read-only check reports absent config');
  equal(existsSync(path.join(root,'.openai')),false,'check creates no directory');
  equal(prepareLocalHosting(root).created,true,'explicit preparation creates local placeholder');
  const value=JSON.parse(readFileSync(file,'utf8'));
  equal(value.project_id,'local-only-not-a-deployed-site','identity is a conspicuous local marker');
  equal(value.d1,'DB','matches local API binding');equal(value.r2,null,'no existing storage identity');
  const owner='{"project_id":"KEEP_OWNER_CONFIGURATION"}\n';writeFileSync(file,owner,'utf8');
  equal(prepareLocalHosting(root).created,false,'existing owner config is untouched');
  equal(readFileSync(file,'utf8'),owner,'no overwrite of owner config bytes');
  equal(prepareLocalHosting(root,{check:true}).status,'EXISTING_CONFIG_UNCHANGED','check reports existing config without showing it');
  console.log(`LOCAL_HOSTING_PASS ${checks} checks · explicit local template, read-only check and existing identity preservation.`);
}finally{
  const checked=path.resolve(root);
  if(path.dirname(checked)!==path.resolve(os.tmpdir())||!path.basename(checked).startsWith('lumen-local-hosting-'))throw new Error('Unsafe temporary cleanup path.');
  rmSync(checked,{recursive:true,force:true});
}
