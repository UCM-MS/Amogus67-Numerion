import assert from 'node:assert/strict';
import test from 'node:test';
import {AcademyEngine} from '../public/academy-engine.js';
import {AGE_QUESTIONS} from '../public/curriculum.js';
import {AGE_LABELS,OUTFITS,SKINS,TUTORIAL_STEPS,tutorialStep,collectionEntries} from '../public/academy-journey.js';

class MemoryStorage{
  constructor(){this.data=new Map();}
  getItem(key){return this.data.get(key)||null;}
  setItem(key,value){this.data.set(key,String(value));}
}
const subjects=['math','science','nature','history','language','technology'];
const tutorialEvents=['move','jump','quiz','egg','hatch','companion'];
const profile=(ageBand=1)=>({name:'Nina',ageBand,outfit:1,skin:2});

test('Shared curriculum contains 96 valid questions, evenly split by age, subject and difficulty',()=>{
  assert.equal(AGE_QUESTIONS.length,96);
  assert.equal(new Set(AGE_QUESTIONS.map(q=>q.id)).size,96,'Every saved question ID is unique');
  assert.equal(AGE_LABELS.length,4);
  for(let age=0;age<4;age++)for(const subject of subjects){
    const pool=AGE_QUESTIONS.filter(q=>q.ageBand===age&&q.subject===subject);
    assert.equal(pool.length,4,`Age ${age}, ${subject} has four questions`);
    assert.deepEqual(pool.map(q=>q.difficulty).sort(),[1,2,3,4]);
    assert.deepEqual([...new Set(pool.map(q=>q.answer))].sort(),['a','b','c','d'],'Correct options do not favour a fixed slot');
    for(const q of pool){
      assert.ok(typeof q.question==='string'&&q.question.length>10);
      assert.ok(typeof q.explanation==='string'&&q.explanation.length>10);
      assert.equal(q.options.length,4);
      assert.deepEqual(q.options.map(o=>o.id).sort(),['a','b','c','d']);
      assert.equal(q.options.filter(o=>o.id===q.answer).length,1);
      assert.equal(new Set(q.options.map(o=>o.label.trim())).size,4,'The four displayed choices are distinct');
      assert.ok(q.options.every(o=>typeof o.label==='string'&&o.label.trim().length>0));
    }
  }
});

test('Explorer profile normalizes names and appearance limits and survives storage reload',()=>{
  const storage=new MemoryStorage(),engine=new AcademyEngine(storage,AGE_QUESTIONS);
  const changed=engine.setExplorer({name:'\u0000  Nina\t ',ageBand:3,outfit:99.7,skin:2.9});
  assert.equal(changed.ok,true);
  const state=engine.getState();
  assert.equal(state.player.name,'Nina');
  assert.equal(state.player.ageBand,3);
  assert.equal(state.player.outfit,OUTFITS.length-1);
  assert.equal(state.player.skin,2);
  assert.equal(state.onboarding.profileReady,true);
  const restored=new AcademyEngine(storage,AGE_QUESTIONS).getState();
  assert.deepEqual(restored.player,state.player);
  assert.equal(restored.onboarding.profileReady,true);
  assert.equal(engine.setExplorer({name:'A'.repeat(40),ageBand:0,outfit:-10,skin:999}).ok,true);
  assert.equal(engine.getState().player.name.length,24);
  assert.equal(engine.getState().player.outfit,0);
  assert.equal(engine.getState().player.skin,SKINS.length-1);
  for(const ageBand of [-1,4,1.2,'1',NaN]){
    const before=engine.getState().player;
    assert.equal(engine.setExplorer({...profile(),ageBand}).ok,false);
    assert.deepEqual(engine.getState().player,before,'Invalid cohort leaves the saved profile intact');
  }
  assert.equal(engine.setExplorer({...profile(),name:'\n\t\u0000'}).ok,false);
});

test('Every cohort receives its own six subject pools and practice preserves mastery',()=>{
  for(let ageBand=0;ageBand<4;ageBand++){
    const engine=new AcademyEngine(null,AGE_QUESTIONS);
    assert.equal(engine.setExplorer(profile(ageBand)).ok,true);
    for(const subject of subjects){
      const seen=new Set();
      for(let index=0;index<4;index++){
        const q=engine.getQuiz(subject);
        assert.equal(q.ageBand,ageBand);
        assert.equal(q.subject,subject);
        assert.equal(seen.has(q.id),false,'Unsolved questions are offered before practice');
        seen.add(q.id);
        const result=engine.answerQuiz(q.id,q.answer);
        assert.equal(result.ok,true);assert.equal(result.firstSolve,true);
      }
      assert.deepEqual(engine.getState().mastery[subject],{solved:4,total:4});
      const practice=engine.getQuiz(subject);
      assert.equal(practice.ageBand,ageBand);
      assert.equal(engine.answerQuiz(practice.id,practice.answer).firstSolve,false);
      assert.deepEqual(engine.getState().mastery[subject],{solved:4,total:4});
    }
    assert.equal(engine.getState().player.knowledge,24);
  }
});

test('Changing cohort changes lessons while new solved IDs survive save, reload and export/import',()=>{
  const storage=new MemoryStorage(),engine=new AcademyEngine(storage,AGE_QUESTIONS);
  engine.setExplorer(profile(0));
  const young=engine.getQuiz('technology');
  assert.equal(engine.answerQuiz(young.id,young.answer).firstSolve,true);
  engine.setExplorer(profile(3));
  const advanced=engine.getQuiz('technology');
  assert.notEqual(advanced.id,young.id);assert.equal(advanced.ageBand,3);
  assert.deepEqual(engine.getState().mastery.technology,{solved:0,total:4});
  assert.equal(engine.answerQuiz(advanced.id,advanced.answer).firstSolve,true);
  const restored=new AcademyEngine(storage,AGE_QUESTIONS);
  assert.deepEqual(restored.getState().progress.solved,[young.id,advanced.id]);
  assert.equal(restored.getState().player.knowledge,2);
  assert.deepEqual(restored.getState().mastery.technology,{solved:1,total:4});
  const imported=new AcademyEngine(null,AGE_QUESTIONS);
  assert.equal(imported.importSave(restored.exportSave()).ok,true);
  assert.deepEqual(imported.getState().progress.solved,[young.id,advanced.id]);
  imported.setExplorer(profile(0));
  assert.deepEqual(imported.getState().mastery.technology,{solved:1,total:4});
  assert.notEqual(imported.getQuiz('technology').id,young.id,'Completed questions remain complete when returning to a cohort');
});

test('Omitting optional curriculum retains the original 36-question academy and existing saves',()=>{
  const storage=new MemoryStorage(),legacy=new AcademyEngine(storage);
  const definitions=legacy.getDefinitions();
  assert.equal(definitions.questions.length,36);
  assert.deepEqual(definitions.subjects.map(s=>s.id),['math','science','nature','astral']);
  assert.equal(definitions.questions.some(q=>q.ageBand!==undefined),false);
  assert.deepEqual(legacy.getState().mastery.math,{solved:0,total:8});
  assert.deepEqual(legacy.getState().mastery.astral,{solved:0,total:12});
  assert.equal(legacy.getQuiz('history'),null);
  const q=legacy.getQuiz('math');
  assert.equal(q.id,'math-01');
  legacy.answerQuiz(q.id,q.answer);
  const extended=new AcademyEngine(storage,AGE_QUESTIONS);
  assert.equal(extended.getDefinitions().questions.length,132);
  assert.deepEqual(extended.getState().progress.solved,[q.id],'A pre-upgrade save keeps its earned knowledge');
  assert.equal(extended.getState().player.knowledge,1);
  assert.equal(extended.getQuiz('math').ageBand,1,'The upgraded game now offers the saved cohort');
  const legacyAgain=new AcademyEngine(storage);
  assert.equal(legacyAgain.getDefinitions().questions.length,36);
  assert.deepEqual(legacyAgain.getState().progress.solved,[q.id]);
});

test('Tutorial grants exactly 100 coins and 40 XP once after six unique gameplay events',()=>{
  const engine=new AcademyEngine(null,AGE_QUESTIONS),before=engine.getState();
  assert.deepEqual(TUTORIAL_STEPS.map(step=>step.id),tutorialEvents);
  assert.equal(engine.recordTutorial('move').ok,false,'Events outside the active tutorial are ignored');
  assert.equal(engine.startTutorial().ok,true);
  assert.equal(tutorialStep(engine.getState()).id,'move');
  assert.equal(engine.recordTutorial('unknown').ok,false);
  assert.deepEqual(engine.getState().onboarding.events,[]);
  for(const event of tutorialEvents.slice(0,-1)){
    assert.equal(engine.recordTutorial(event).ok,true);
    assert.equal(engine.recordTutorial(event).ok,false,'Duplicate gameplay events cannot advance twice');
    assert.equal(engine.getState().player.coins,before.player.coins);
    assert.equal(engine.getState().player.xp,before.player.xp);
  }
  assert.equal(tutorialStep(engine.getState()).id,'companion');
  assert.equal(engine.recordTutorial('companion').event,'tutorial-completed');
  const complete=engine.getState();
  assert.equal(complete.player.coins,before.player.coins+100);
  assert.equal(complete.player.xp,before.player.xp+40);
  assert.equal(complete.stats.totalEarned,before.stats.totalEarned+100);
  assert.equal(complete.onboarding.active,false);
  assert.equal(complete.onboarding.completed,true);
  assert.equal(complete.onboarding.rewarded,true);
  assert.equal(tutorialStep(complete),null);
  for(const event of tutorialEvents)assert.equal(engine.recordTutorial(event).ok,false);
  assert.equal(engine.getState().player.coins,complete.player.coins);
});

test('Tutorial pauses, reloads and resumes from the current step',()=>{
  const storage=new MemoryStorage(),engine=new AcademyEngine(storage,AGE_QUESTIONS);
  engine.startTutorial();engine.recordTutorial('move');engine.recordTutorial('jump');
  assert.equal(engine.pauseTutorial().ok,true);
  assert.equal(engine.getState().onboarding.active,false);
  assert.equal(engine.recordTutorial('quiz').ok,false);
  const restored=new AcademyEngine(storage,AGE_QUESTIONS);
  assert.deepEqual(restored.getState().onboarding.events,['move','jump']);
  assert.equal(restored.getState().onboarding.active,false);
  assert.equal(tutorialStep(restored.getState()).id,'quiz');
  restored.startTutorial();
  assert.deepEqual(restored.getState().onboarding.events,['move','jump'],'Resuming retains completed tutorial steps');
  const resumed=new AcademyEngine(storage,AGE_QUESTIONS);
  assert.equal(resumed.getState().onboarding.active,true,'An active guide survives refresh');
  resumed.startTutorial();
  assert.deepEqual(resumed.getState().onboarding.events,['move','jump'],'Repeated start does not erase an active run');
  const before=resumed.getState().player;
  for(const event of tutorialEvents.slice(2))resumed.recordTutorial(event);
  assert.equal(resumed.getState().onboarding.completed,true);
  assert.equal(resumed.getState().player.coins,before.coins+100);
  assert.equal(resumed.getState().player.xp,before.xp+40);
});

test('Replaying and restoring a completed tutorial never farms its one-time reward',()=>{
  const storage=new MemoryStorage(),engine=new AcademyEngine(storage,AGE_QUESTIONS);
  engine.startTutorial();tutorialEvents.forEach(event=>engine.recordTutorial(event));
  const before=engine.getState();
  const restored=new AcademyEngine(storage,AGE_QUESTIONS);
  assert.equal(restored.getState().onboarding.rewarded,true);
  restored.startTutorial();
  assert.deepEqual(restored.getState().onboarding.events,[],'A completed guide can be replayed from the beginning');
  assert.equal(restored.getState().onboarding.completed,false);
  assert.equal(restored.getState().onboarding.rewarded,true);
  tutorialEvents.forEach(event=>restored.recordTutorial(event));
  assert.equal(restored.getState().onboarding.completed,true);
  assert.equal(restored.getState().player.coins,before.player.coins);
  assert.equal(restored.getState().player.xp,before.player.xp);
  const imported=new AcademyEngine(null,AGE_QUESTIONS);
  assert.equal(imported.importSave(restored.exportSave()).ok,true);
  imported.startTutorial();tutorialEvents.forEach(event=>imported.recordTutorial(event));
  assert.equal(imported.getState().player.coins,before.player.coins);
  assert.equal(imported.getState().player.xp,before.player.xp);
});

test('Collection catalogue reports all fourteen species and counts owned variants',()=>{
  const engine=new AcademyEngine(null,AGE_QUESTIONS),defs=engine.getDefinitions();
  const entries=collectionEntries(defs,[{name:'Mochi',kind:'bunny'},{name:'Mochi',kind:'bunny'},{name:'Nova',kind:'dragon'}]);
  assert.equal(entries.length,14);
  assert.equal(entries.find(p=>p.name==='Mochi').count,2);
  assert.equal(entries.find(p=>p.name==='Nova').count,1);
  assert.equal(entries.find(p=>p.name==='Pip').count,0);
});
