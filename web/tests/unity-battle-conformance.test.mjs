import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {submit} from '../worker/unity-battle.mjs';
const vectors=JSON.parse(readFileSync(new URL('./pet-battle-conformance.json',import.meta.url),'utf8')).vectors;
function numericState(value){const copy=structuredClone(value);delete copy.Log;return copy;}
let count=0;
for(const vector of vectors){
  const state=structuredClone(vector.initial);
  const left=submit(state,0,state.Round,vector.left);assert.ok(left.Accepted,vector.label+' left accepted');
  const right=submit(state,1,state.Round,vector.right);assert.ok(right.Accepted,vector.label+' right accepted');
  assert.deepEqual(numericState(state),numericState(vector.expected),vector.label);count++;
}
console.log(`UNITY_COMBAT_CONFORMANCE_PASS ${count} shared C#/server vectors (all combat state except localized log text).`);
