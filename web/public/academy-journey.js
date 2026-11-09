export const AGE_LABELS = ['6–9 rokov', '10–12 rokov', '13–15 rokov', '16+ rokov'];
export const OUTFITS = ['#f9df70', '#62d3c2', '#b9a1ff', '#f49e8e', '#74b9ef', '#dbe8ed'];
export const SKINS = ['#ffdab4', '#dfad82', '#b77e58', '#754b36'];
export const TUTORIAL_STEPS = [
  { id: 'move', title: 'Tvoj prvý krok', text: 'Prejdi aspoň 5 metrov. Použi WASD alebo ľavý ovládač na mobile.', key: 'W A S D', icon: 'arrow' },
  { id: 'jump', title: 'Malý skok do veľkého sveta', text: 'Vyskúšaj skok medzerníkom alebo tlačidlom so šípkou.', key: 'SPACE', icon: 'arrow' },
  { id: 'quiz', title: 'Vedomosti otvárajú svet', text: 'Vyrieš prvú otázku. Správna odpoveď ti zarobí na vajíčko.', key: 'OBJAVIŤ', icon: 'book', panel: 'curriculum' },
  { id: 'egg', title: 'Vyber si prvé vajíčko', text: 'Sleduj modrú cestu k lúčnemu vajíčku. Priblíž sa, stlač E a kúp ho za herné mince.', key: 'E', icon: 'egg', waypoint: 'egg-basic' },
  { id: 'hatch', title: 'Niekto na teba čaká', text: 'Dones vajíčko do inkubátora na svojej základni a vyliahni spoločníka.', key: 'E', icon: 'egg', waypoint: 'hatch' },
  { id: 'companion', title: 'Už na to nie si sám', text: 'Vezmi peta so sebou a prejdi s ním 3 metre. Potom je akadémia celá tvoja.', key: 'PET', icon: 'star', panel: 'pets' }
];
export function tutorialStep(state) {
  const events = new Set(state.onboarding?.events || []);
  return TUTORIAL_STEPS.find(s => !events.has(s.id)) || null;
}
export function collectionEntries(defs, pets) {
  return Object.entries(defs.petPools).flatMap(([rarity, entries]) => entries.map(p => ({ ...p, rarity, count: pets.filter(owned => owned.name === p.name && owned.kind === p.kind).length })));
}
