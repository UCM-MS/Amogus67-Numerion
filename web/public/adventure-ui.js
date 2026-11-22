const escape = value => String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const labels={math:'Matematika',science:'Veda',nature:'Príroda',astral:'Astrálny výskum'};
const rarity={common:'Bežný',uncommon:'Neobyčajný',rare:'Vzácny',epic:'Epický',legendary:'Legendárny'};

export function masteryCards(state){
  return `<div class="mastery-grid">${Object.entries(state.mastery||{}).map(([id,m])=>`<article class="mastery-card"><strong>${labels[id]||escape(id)}</strong><span>${m.solved} / ${m.total} poznatkov</span><div class="mission-progress" role="progressbar" aria-label="${labels[id]||id}" aria-valuemin="0" aria-valuemax="${m.total}" aria-valuenow="${m.solved}"><i style="width:${m.total?m.solved/m.total*100:0}%"></i></div></article>`).join('')}</div>`;
}

export function rarityOdds(chances=[]){
  return `<div class="rarity-odds" aria-label="Šance na spoločníkov">${chances.map(p=>`<span class="rarity-${p.rarity}"><strong>${rarity[p.rarity]||escape(p.rarity)}</strong> ${Math.round(p.chance*100)} %</span>`).join('')}</div>`;
}

export function adventurePanel(panel,state,defs,ctx){
  const {heading,icon,compact,targets,position,effectiveQuality}=ctx;
  if(panel==='missions'){
    const rank=m=>m.claimed?2:m.complete?0:1,missions=[...(defs.expeditions||[])].sort((a,b)=>rank(a)-rank(b)),claimed=missions.filter(m=>m.claimed).length;
    return heading('VÝPRAVY AKADÉMIE','Každá výprava má svoj príbeh.',`Dokonči ciele a vyzdvihni si odmeny. Splnené výpravy: ${claimed} / ${missions.length}.`)+
      `<div class="collection-stat">${icon('star')} Kryštály ${state.progress.collected?.length||0} / 12 <span>Najlepšia séria: ${state.stats.bestStreak||0}</span><span>Dráha: ${state.stats.bestParkourTime?state.stats.bestParkourTime.toFixed(1)+' s':'zatiaľ bez času'}</span></div><div class="mission-grid">${missions.map(m=>`<article class="mission-card ${m.claimed?'claimed':''}"><span class="tag">${m.claimed?'ODMENA VYZDVIHNUTÁ':m.complete?'VÝPRAVA SPLNENÁ':'VÝPRAVA PREBIEHA'}</span><h3>${escape(m.title)}</h3><p>${escape(m.description)}</p><div class="mission-progress" role="progressbar" aria-label="${escape(m.title)}" aria-valuenow="${Math.min(m.current,m.target)}" aria-valuemin="0" aria-valuemax="${m.target}"><i style="width:${Math.min(100,m.current/m.target*100)}%"></i></div><div class="mission-reward"><strong>${Math.min(m.current,m.target)} / ${m.target}</strong><span>${icon('coin')} ${m.rewardCoins} · ${m.rewardXp} XP</span></div>${m.claimed?`<div class="save-note">${icon('check')} Výborne, objaviteľ!</div>`:m.complete?`<button class="primary-btn" data-claim="${escape(m.id)}">Vyzdvihnúť odmenu</button>`:`<button class="secondary-btn" data-waypoint="${escape(m.waypointId)}">Ukázať cestu ${icon('arrow')}</button>`}</article>`).join('')}</div>`;
  }
  if(panel==='map'){
    return heading('MAPA AKADÉMIE','Kam povedie ďalší objav?',`Vyber miesto. Modrá šípka a vzdialenosť ťa povedú cez ostrov.`)+
      `<div class="map-grid">${targets.map(t=>{const d=Math.round(Math.hypot(t.position.x-position.x,t.position.z-position.z));const locked=t.type==='portal'&&!state.progress.unlocked.includes('astral')||t.type==='egg'&&state.player.knowledge<(defs.eggDefs.find(e=>e.id===t.eggId)?.knowledge||0);return `<button class="map-destination" data-waypoint="${escape(t.id)}">${icon(t.type==='egg'?'egg':t.type==='quiz'?'book':t.type==='hatch'?'star':'arrow')}<span><strong>${escape(t.name)}</strong><small>${locked?'Potrebuješ viac poznatkov':t.type==='parkour'?'Skoky · kontrolné body · osobný rekord':t.type==='hatch'?'Liaheň na tvojej základni':'Sleduj smer vo svete'}</small></span><b>${d} m</b></button>`;}).join('')}</div><div class="save-note">${icon('star')} Na cestách hľadaj 12 trblietavých kryštálov. Každý prinesie 15 mincí a 10 XP.</div>`;
  }
  if(panel==='settings'){
    return heading('TVOJA AKADÉMIA','Hraj tak, ako ti to vyhovuje.',`Nastav výkon alebo si zálohuj svoj pokrok na inom zariadení.`)+
      `<div class="settings-grid"><section class="settings-option"><h3>Kvalita grafiky</h3><p>Automatická voľba zníži náročnosť, keď hra beží pomaly.</p><div class="modal-actions">${[['auto','Automatická'],['low','Úsporná'],['high','Vysoká']].map(([id,name])=>`<button class="${(state.settings.graphics||'auto')===id?'primary-btn':'secondary-btn'}" data-graphics="${id}" aria-pressed="${(state.settings.graphics||'auto')===id}">${name}</button>`).join('')}</div><span class="save-note">Aktívne vykresľovanie: ${effectiveQuality==='low'?'úsporné':effectiveQuality==='high'?'vysoké':'automatické'}.</span></section><section class="settings-option"><h3>Zvuk objavov</h3><button class="secondary-btn" data-action="toggle-sound">${state.settings.sound?'Vypnúť zvuk':'Zapnúť zvuk'}</button></section><section class="settings-option"><h3>Záloha tvojho sveta</h3><p>Postup sa ukladá v tomto prehliadači. Súbor zálohy prenesie meno, spoločníkov, mince aj poznatky.</p><div class="modal-actions"><button class="primary-btn" data-action="export-save">Stiahnuť zálohu</button><button class="secondary-btn" data-action="choose-save">Vybrať zálohu</button><input id="import-save" type="file" accept="application/json,.json" hidden></div><p id="import-status" class="save-note" role="status"></p></section></div><div class="save-note">Pohyb: WASD / šípky · Shift: beh · Space: skok · E: interakcia · M: mapa · 5: výpravy.</div>`;
  }
  return null;
}
