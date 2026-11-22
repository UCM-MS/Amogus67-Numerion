// Integer, deterministic twin of Lumen.Core.PetBattleRules. Keep its conformance test in sync.
export const SPECIES = Object.freeze({ember:'Iskra',bolt:'Volt',astra:'Astra',zlomko:'Zlomko',percenta:'Percenta',rovnik:'Rovnik',geora:'Geora',prizma:'Prizma',datax:'Datax',ratio:'Ratio'});
const AFFINITY={ember:0,zlomko:0,datax:0,ratio:0,bolt:1,percenta:1,rovnik:1,astra:2,geora:2,prizma:2};
const TOPIC={ember:5,bolt:2,astra:3,zlomko:0,percenta:1,rovnik:2,geora:3,prizma:3,datax:4,ratio:1};
const cost=[2,0,1,3,0,0], priority=[1,3,2,2,4,1];
const clamp=(value,max)=>Math.min(max,Math.max(0,value));
const fighter=id=>({SpeciesId:id,Nickname:SPECIES[id],HP:100,MaxHP:100,AttackBonus:0,Energy:4,StrikePP:12,FocusPP:4,HealPP:2,HealCooldown:0,Shield:0,Focused:false});
export const actionId=(state,side)=>`${state.Id}:${state.Round}:${side}`;
export function cleanParty(party){return [...new Set(Array.isArray(party)?party.filter(id=>Object.hasOwn(SPECIES,id)):[])].slice(0,3);}
export function createBattle(id,left,right,seed=1){
  if(!id||id.length>128)throw new Error('invalid_battle');
  left=cleanParty(left);right=cleanParty(right);if(!left.length||!right.length)throw new Error('empty_party');
  return {Id:id,Seed:seed|0,Round:1,RoundsResolved:0,Phase:0,Winner:-2,IsPractice:false,
    Teams:[{Name:'Tím 1',Pets:left.map(fighter),Active:0},{Name:'Tím 2',Pets:right.map(fighter),Active:0}],PendingActions:[null,null],RewardClaimed:[false,false],CorrectAnswers:[0,0],
    Log:['Začína priateľský súboj. Správna matematická odpoveď aktivuje zvolený ťah.']};
}
export function effectiveness(a,b){if(a===b||!(a in AFFINITY)||!(b in AFFINITY))return 100;const x=AFFINITY[a],y=AFFINITY[b];if(x===y)return 100;return x===0&&y===2||x===2&&y===1||x===1&&y===0?120:80;}
export function availability(state,side,kind,target=0){
  if(!state||![0,1].includes(side))return 'Neplatný stav súboja.';
  if(state.Phase===1)return 'Súboj sa už skončil.';
  if(state.PendingActions[side])return 'Tvoj ťah je už potvrdený.';
  const team=state.Teams[side],pet=team.Pets[team.Active];
  if(!pet||pet.HP<=0)return 'Spoločník nemôže pokračovať.';
  if(!Number.isInteger(kind)||kind<0||kind>5)return 'Neznámy ťah.';
  if(kind===4)return !Number.isInteger(target)||target<0||target>=team.Pets.length||target===team.Active||team.Pets[target].HP<=0?'Vyber iného zdravého spoločníka.':'';
  if(pet.Energy<cost[kind])return 'Nedostatok energie. Zvoľ ochranný štít.';
  if(kind===0&&pet.StrikePP<=0)return 'Výpady sú vyčerpané. Vystriedaj spoločníka.';
  if(kind===2&&(pet.FocusPP<=0||pet.Focused))return 'Sústredenie už pôsobí alebo je vyčerpané.';
  if(kind===3&&(pet.HealPP<=0||pet.HealCooldown>0||pet.HP>=pet.MaxHP))return 'Obnova ešte nie je dostupná.';
  return '';
}
export function submit(state,side,round,command){
  if(!state||![0,1].includes(side)||!command)return {Accepted:false,Error:'Neplatný ťah.'};
  if(state.Phase===1)return {Accepted:false,Error:'Súboj sa už skončil.'};
  if(command.Topic!==undefined&&(!Number.isInteger(command.Topic)||command.Topic< -1||command.Topic>5))return {Accepted:false,Error:'Neplatná téma otázky.'};
  if(round!==state.Round||command.ActionId!==actionId(state,side)||state.PendingActions[side])return {Accepted:false,Duplicate:true,Error:'Tento ťah už bol prijatý alebo patrí do iného kola.'};
  const error=availability(state,side,command.Kind,command.TargetSlot);if(error)return {Accepted:false,Error:error};
  state.PendingActions[side]={Kind:command.Kind,TargetSlot:command.TargetSlot|0,Correct:!!command.Correct,ActionId:command.ActionId,Topic:Number.isInteger(command.Topic)?command.Topic:-1};
  const resolved=state.PendingActions.every(Boolean);if(resolved)resolve(state);
  return {Accepted:true,RoundResolved:resolved,Complete:state.Phase===1,Round:state.Round,Winner:state.Winner};
}
export function forfeit(state,side,round){if(!state||state.Phase===1||![0,1].includes(side)||state.Round!==round)return false;state.Phase=1;state.Winner=1-side;state.PendingActions=[null,null];state.Log=[state.Teams[side].Name+' ukončil súboj.'];return true;}
function apply(state,side,log){
  const command=state.PendingActions[side],team=state.Teams[side],pet=team.Pets[team.Active];
  if(pet.HP<=0){log.push(pet.Nickname+' už nemôže vykonať ťah.');return;}
  if(command.Kind===5){const defender=state.Teams[1-side].Pets[state.Teams[1-side].Active];defender.HP=clamp(defender.HP-Math.max(0,8-defender.Shield),defender.MaxHP);defender.Shield=0;log.push(pet.Nickname+' používa rýchly výpad.');return;}
  if(!command.Correct){pet.Energy=clamp(pet.Energy+1,6);log.push(pet.Nickname+': nesprávna odpoveď, ťah sa neaktivoval.');return;}
  state.CorrectAnswers[side]++;
  if(command.Kind===4){team.Active=command.TargetSlot;log.push(team.Name+' povoláva '+team.Pets[team.Active].Nickname+'.');return;}
  pet.Energy-=cost[command.Kind];
  if(command.Kind===1){pet.Shield=14;pet.Energy=clamp(pet.Energy+2,6);log.push(pet.Nickname+' vytvára ochranný štít.');}
  if(command.Kind===2){pet.FocusPP--;pet.Focused=true;log.push(pet.Nickname+' sa sústredí.');}
  if(command.Kind===3){pet.HealPP--;pet.HealCooldown=3;const amount=Math.min(30,pet.MaxHP-pet.HP);pet.HP+=amount;log.push(pet.Nickname+' obnovuje '+amount+' HP.');}
  if(command.Kind===0){pet.StrikePP--;const foe=state.Teams[1-side],defender=foe.Pets[foe.Active];const effect=effectiveness(pet.SpeciesId,defender.SpeciesId);let damage=Math.floor((26+pet.AttackBonus)*effect/100);if(command.Topic>=0&&command.Topic===TOPIC[pet.SpeciesId])damage=Math.floor(damage*120/100);if(pet.Focused){damage=Math.floor(damage*150/100);pet.Focused=false;}damage=Math.max(0,damage-defender.Shield);defender.Shield=0;defender.HP=clamp(defender.HP-damage,defender.MaxHP);log.push(pet.Nickname+': výpad '+damage+' poškodenia'+(effect>100?' · typová výhoda.':'.'));}
}
function resolve(state){
  const log=['Kolo '+state.Round],a=priority[state.PendingActions[0].Kind],b=priority[state.PendingActions[1].Kind];const first=a===b?((state.Seed>>>0)+(state.Round>>>0))%2:a>b?0:1;
  apply(state,first,log);apply(state,1-first,log);
  for(const team of state.Teams){for(const pet of team.Pets){pet.Shield=0;pet.HealCooldown=Math.max(0,pet.HealCooldown-1);}const active=team.Pets[team.Active];if(active.HP>0)active.Energy=clamp(active.Energy+1,6);else{const next=team.Pets.findIndex(p=>p.HP>0);if(next>=0){team.Active=next;log.push(team.Name+': na scénu prichádza '+team.Pets[next].Nickname+'.');}}}
  state.RoundsResolved++;const left=state.Teams[0].Pets.some(p=>p.HP>0),right=state.Teams[1].Pets.some(p=>p.HP>0);
  if(!left||!right||state.Round>=100){state.Phase=1;state.Winner=left&&!right?0:!left&&right?1:-1;log.push(state.Winner===-1?'Priateľská remíza.':'Víťaz: '+state.Teams[state.Winner].Name+'.');}else state.Round++;
  state.PendingActions=[null,null];state.Log=log;
}
