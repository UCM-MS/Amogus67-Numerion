import {env} from 'cloudflare:workers';
import {createUnityOnlineApi} from '../../../../worker/unity-online.mjs';
import questions from '../../../../worker/unity-curriculum.mjs';
const api=createUnityOnlineApi({questions});
export async function POST(request:Request){return await api(request,env)||new Response('Not found',{status:404});}
