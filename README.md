# Numerion

Numerion je matematická hra pre 7. až 9. ročník ZŠ od tímu Amogus. Hráč si v akadémii vyberie ročník a tému, prejde si riešený príklad a vyskúša si ho v teste. Za správne odpovede trénuje spoločníka, s ktorým môže ísť do ťahového súboja.

## Čo je hotové v 2. týždni

Pribudlo 36 matematických úloh. Každý ročník má teraz 48 otázok v šiestich témach. Ak už hráč v prvom týždni dokončil všetkých šesť otázok z témy, môže pokračovať dvoma novými. Jeho výsledky, mince aj získané odmeny sa zachovajú.

## Zostavenie a spustenie

Matematické jadro si môžeš zostaviť a vyskúšať v konzole aj pred spojením tímových častí. Potrebuješ .NET 10 SDK alebo novšie SDK s nainštalovaným runtime .NET 10. Z koreňa repozitára spusti oba príkazy.

```powershell
dotnet build unity/Tests/MathHarness/MathHarness.csproj
dotnet run --no-build --project unity/Tests/MathHarness/MathHarness.csproj
```

Úspešný test vypíše `NUMERION_MATH_HARNESS_PASS`. Overí aj to, že hráč s uloženou pozíciou z prvého týždňa môže pokračovať novými úlohami.

### Spoločná hra v Unity

Na hranie treba spojiť časti všetkých štyroch členov za rovnaký týždeň. V spojenom projekte W02 pridaj do Unity Hubu priečinok `unity`, otvor ho v Editore `6000.6.6f1` a spusti scénu `Assets/Lumen/Generated/Academy.unity` tlačidlom **Play**.

Windows verziu zostavíš z koreňa spojeného projektu týmto príkazom v PowerShelli.

```powershell
.\unity\Tools\Build-Unity.ps1 -Action Build
```

Výsledný súbor je `unity/Builds/Windows/LumenAcademy.exe`.

## Ovládanie v akadémii

| Čo chceš spraviť | Kláves alebo myš |
| --- | --- |
| Chodiť, bežať, skočiť | WASD alebo šípky, Shift na beh, medzerník na skok |
| Rozhliadnuť sa, priblížiť pohľad | Podrž pravé tlačidlo myši, vzdialenosť zmeň kolieskom |
| Použiť kiosk alebo sa porozprávať | E v blízkosti postavy alebo kiosku |
| Otvoriť mapu, učivo alebo denník | M pre mapu, K pre učivo, J pre denník |
| Otvoriť menu | Esc |

[Odovzdanie prvého týždňa](docs/odovzdavky/w01/ivan-beneda.md) · [Matematické dáta a ich generovanie](unity/Assets/Lumen/Resources/Content/README.md)
