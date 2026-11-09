# 1. týždeň - Ivan Beneda

Odovzdal som výber úloh podľa ročníka a témy, vyhodnotenie odpovedí a tematické testy. V banke je 108 úloh a 18 kapitol.

- [Matematické jadro](../../../unity/Assets/Lumen/Core)
- [Dáta otázok a kapitol](../../../unity/Assets/Lumen/Resources/Content)
- [Test výberu ročníka](../../../unity/Tests/MathHarness/Program.cs)

Pridal som automatický test prepnutia zo 7. na 9. ročník s otvorenou otázkou. Jadro odmietne starú odpoveď bez zmeny XP, mincí a počtu pokusov. Ďalšia otázka už patrí do 9. ročníka.

MathHarness prešiel 648 kontrolami.

Doplnil som výber a tréning spoločníkov, výbavu za správne odpovede a ťahové súboje. Na webe som pridal pravidlá klasického prototypu a rovnakú banku otázok pre server ako v Unity.

Test súboja overuje, že sa kolo vyhodnotí až po oboch ťahoch. Test výbavy kontroluje, že opakované odoslanie odpovede nepridá ďalšiu odmenu.
