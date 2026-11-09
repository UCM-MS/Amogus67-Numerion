# Pravidlá Numerionu

Jadro je čisté C#. Unity a server nad ním riešia zobrazenie, uloženie a komunikáciu.

- `LearningService` vyberá otázky podľa ročníka a témy, hodnotí odpovede a vedie postup. Pri precvičovaní po vyčerpaní otázok sa odmeny nepripisujú.
- `StudyTestService` pripraví tematický test. Výsledok a vysvetlenia nesprávnych odpovedí sú dostupné po dokončení.
- `CompanionRules` rieši výber spoločníka, tréning a zbierku. `ArtifactRules` spravuje výbavu získanú za správne odpovede.
- `PetBattleRules` prijíma potvrdené ťahy, vyhodnocuje kolá a výsledok súboja. Pri online hre správnosť odpovede overuje server pred odovzdaním ťahu jadru.

Kontroly z koreňa repozitára

~~~powershell
dotnet run --project unity/Tests/MathHarness/MathHarness.csproj
dotnet run --project unity/Tests/CompanionHarness/CompanionHarness.csproj
dotnet run --project unity/Tests/CoreHarness/CoreHarness.csproj
dotnet build unity/Tests/CoreHarness/Compatibility/Compatibility.csproj
~~~

[Dáta a ich generovanie](../Resources/Content/README.md)
