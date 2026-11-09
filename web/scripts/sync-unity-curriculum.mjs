import {readFile,writeFile} from 'node:fs/promises';
const path=process.argv[2]||new URL('../../unity/Assets/Lumen/Resources/Content/curriculum.json',import.meta.url);
const data=JSON.parse(await readFile(path,'utf8'));
const questions=data.Questions.filter(q=>q.Grade>=7&&q.Grade<=9);
if(!questions.length)throw new Error('The grade 7–9 curriculum has not been exported yet.');
await writeFile(new URL('../worker/unity-curriculum.mjs',import.meta.url),'// Generated from the shared Unity curriculum; never edit answers independently.\nexport default '+JSON.stringify(questions,null,2)+';\n');
console.log('Shared online mathematics bank: '+questions.length+' questions.');
