import {existsSync,lstatSync,mkdirSync,writeFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

// A clean source handoff intentionally excludes the real Sites project identity.
// This explicit local-only placeholder lets Vite resolve its configuration import.
export function prepareLocalHosting(projectRoot,{check=false}={}){
  const root=path.resolve(projectRoot),directory=path.join(root,'.openai'),file=path.join(directory,'hosting.json');
  if(existsSync(file))return {status:'EXISTING_CONFIG_UNCHANGED',created:false};
  if(existsSync(directory)&&lstatSync(directory).isSymbolicLink())throw new Error('Refusing a linked .openai directory.');
  if(check)return {status:'LOCAL_CONFIG_MISSING',created:false};
  mkdirSync(directory,{recursive:true});
  const template={project_id:'local-only-not-a-deployed-site',d1:'DB',r2:null};
  writeFileSync(file,JSON.stringify(template,null,2)+'\n',{encoding:'utf8',flag:'wx'});
  return {status:'LOCAL_PLACEHOLDER_CREATED_NOT_FOR_DEPLOYMENT',created:true};
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const result=prepareLocalHosting(fileURLToPath(new URL('../',import.meta.url)),{check:process.argv.includes('--check')});
  console.log(JSON.stringify(result));
}
