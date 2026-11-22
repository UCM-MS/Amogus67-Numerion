import {env} from 'cloudflare:workers';
import {createMultiplayerApi} from '../../../../worker/multiplayer.mjs';
import {createExploreApi} from '../../../../worker/explore.mjs';
import {AcademyEngine} from '../../../../public/academy-engine.js';
const api=createMultiplayerApi({questions:new AcademyEngine(null).getDefinitions().questions});
const explore=createExploreApi();
export async function GET(request:Request){return await explore(request,env)||await api(request,env)||new Response('Not found',{status:404});}
export const POST=GET;
