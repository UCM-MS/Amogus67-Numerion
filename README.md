# Numerion

Numerion je matematická hra pre 7. až 9. ročník ZŠ od tímu Amogus. Hráč si v akadémii vyberie ročník a tému, prejde si riešený príklad a vyskúša si ho v teste. Za správne odpovede trénuje spoločníka, s ktorým môže ísť do ťahového súboja.

**Druhý týždeň vývoja (W02, verzia 0.11.0).** V tejto vetve je Ivanova časť projektu. Ak otváraš školský repozitár prvýkrát, začni konzolovým testom uvedeným nižšie. Na hranie v 3D použi spojený tímový projekt W02.

[Spustiť jadro](#prvé-spustenie-zo-školského-githubu) · [Otvoriť hru v Unity](#hrateľná-verzia-pre-windows-a-web) · [Ovládanie](#ovládanie-v-akadémii) · [Upraviť učivo](#kde-upraviť-hru)

## Čo robí hráč

Prvým cieľom môže byť kapitola **Zlomky bez hádania** v 7. ročníku. V učive si vyberieš tému **Čísla a zlomky** a prejdeš si riešený príklad. V jednej z nových úloh máš 48 kryštálov. Minieš tri osminy zásoby a potom pätinu zo zvyšku. Hra vysvetlí aj druhý krok výpočtu, pri ktorom už počítaš z 30 kryštálov.

Tematický test vyberie šesť otázok. Na úspech potrebuješ aspoň päť správnych odpovedí. Pri chybách si môžeš pozrieť riešenie. V tréningovom súboji máš na výber silnejší ťah s otázkou alebo slabší rýchly výpad bez počítania.

## Čo pribudlo v druhom týždni

Pribudlo 36 matematických úloh. Každý ročník má teraz 48 otázok v šiestich témach. Ak už hráč v prvom týždni dokončil všetkých šesť otázok z témy, môže pokračovať dvoma novými. Jeho výsledky, mince aj získané odmeny sa zachovajú.

Spolu je v tomto míľniku **144 otázok a 18 kapitol**. Pokrývajú čísla a zlomky, percentá a pomer, výrazy a rovnice, geometriu, dáta a pravdepodobnosť aj matematiku v praxi. [Odovzdanie prvého týždňa](docs/odovzdavky/w01/ivan-beneda.md)

## Prvé spustenie zo školského GitHubu

Potrebuješ Git a .NET 10 SDK. Funguje aj novšie SDK s nainštalovaným runtime .NET 10. Nasledujúce príkazy stiahnu vetvu druhého týždňa, zostavia matematické jadro a spustia jeho test v konzole.

```powershell
git clone --branch ivan/tyzden-02 --single-branch https://github.com/UCM-MS/Amogus67-Numerion.git
cd Amogus67-Numerion
dotnet build unity/Tests/MathHarness/MathHarness.csproj
dotnet run --no-build --project unity/Tests/MathHarness/MathHarness.csproj
```

V poslednom riadku hľadaj `NUMERION_MATH_HARNESS_PASS`, 974 kontrol a 144 otázok. Test overí aj pokračovanie z uloženej pozície prvého týždňa. Unity Editor potrebuješ až pri spustení hernej scény.

## Hrateľná verzia pre Windows a web

Použi úplný tímový projekt pre druhý týždeň. V tímovom balíku ho nájdeš v priečinku `W02/NA-HRANIE`. Rozbaľ ho do samostatného priečinka. V koreni sú vedľa seba `unity` a `web`. Úplný Unity projekt spoznáš podľa súboru `unity/ProjectSettings/ProjectVersion.txt`.

**V Unity Editore** pridaj do Unity Hubu priečinok `unity`, otvor ho vo verzii `6000.6.6f1` a počkaj na prvý import. V okne Project otvor scénu `Assets/Lumen/Generated/Academy.unity` a stlač **Play**.

**Pre Windows** zostav hru z koreňa spojeného projektu v PowerShelli.

```powershell
.\unity\Tools\Build-Unity.ps1 -Action Build
```

Hru spustíš cez `unity/Builds/Windows/LumenAcademy.exe`. Pri prenose na ďalší počítač skopíruj celý priečinok `Windows` vrátane herných dát vedľa EXE.

**Pre prehliadač** pridaj k rovnakej verzii Editora v Unity Hube modul **Web Build Support**. Na prípravu projektu potrebuješ Python 3 a na lokálny server Node.js 24. Prvý príkaz vytvorí webovú zostavu, ďalšie dva ju sprístupnia na tvojom počítači. Začni v koreni spojeného projektu.

```powershell
.\unity\Tools\Build-Web.ps1
cd web
node serve.mjs
```

Po zostavení otvor v prehliadači adresu vypísanú v termináli (štandardne `http://127.0.0.1:4173/`). Terminál nechaj počas hrania otvorený. Server ukončíš cez **Ctrl+C**.

## Ovládanie v akadémii

| Čo chceš spraviť | Kláves alebo myš |
| --- | --- |
| Chodiť, bežať, skočiť | WASD alebo šípky, ľavý Shift na beh, medzerník na skok |
| Rozhliadnuť sa, priblížiť pohľad | Podrž pravé tlačidlo myši, vzdialenosť zmeň kolieskom |
| Použiť kiosk alebo sa porozprávať | E v blízkosti postavy alebo kiosku |
| Otvoriť mapu, učivo alebo denník | M pre mapu, K pre učivo, J pre denník |
| Otvoriť menu | Esc |

## Kde upraviť hru

| Chcem upraviť | Kde začať |
| --- | --- |
| Otázku, možnosti alebo vysvetlenie | [Generátor matematického učiva](unity/Tests/MathHarness/generate_content.py). Po zmene postupuj podľa [návodu na obnovu dát pre Unity a web](unity/Assets/Lumen/Resources/Content/README.md). |
| Výklad alebo riešený príklad v kapitole | [Kapitoly učebnice](unity/Assets/Lumen/Core/LessonCatalog.cs) |
| Výber úloh, odmeny alebo výsledok testu | [Pravidlá precvičovania](unity/Assets/Lumen/Core/LearningService.cs) a [tematické testy](unity/Assets/Lumen/Core/StudyTests.cs) |
| Spoločníkov, ich výbavu a súboje | [Prehľad tried herného jadra](unity/Assets/Lumen/Core/API.md) |

Pri pridávaní otázok zachovaj ID existujúcich úloh. Uložená pozícia podľa nich rozpozná, čo už hráč vyriešil. Po úprave učiva zopakuj zostavenie a matematický test. Ďalšie kontroly z koreňa školského klonu pokrývajú aj herné pravidlá.

```powershell
dotnet run --project unity/Tests/CoreHarness/CoreHarness.csproj
dotnet run --project unity/Tests/CompanionHarness/CompanionHarness.csproj
dotnet build unity/Tests/CoreHarness/Compatibility/Compatibility.csproj
node --test web/tests/academy-engine.test.mjs web/tests/academy-journey.test.mjs
```

Prvé dva testy kontrolujú postup hráča, odmeny, výbavu a súboje. Tretí príkaz overí, že jadro možno zostaviť pre .NET Standard 2.1. Posledný otestuje pravidlá pôvodného webového prototypu a jeho tutoriál.

## Keď sa spustenie nepodarí

| Čo vidíš | Čo skontrolovať |
| --- | --- |
| `dotnet` žiada inú verziu .NET | Príkazom `dotnet --list-runtimes` skontroluj, či je nainštalovaný `Microsoft.NETCore.App 10.x`. |
| Unity Hub nevidí projekt alebo chýba scéna Academy | Otvor podpriečinok `unity` z úplného tímového balíka W02. Školská vetva obsahuje jadro a jeho testy. |
| Skript nenájde nainštalovaný Editor | Obom skriptom môžeš pridať parameter `-UnityPath` s úplnou cestou k súboru `Unity.exe`. |
| Prehliadač neotvorí herné dáta | Over, že webové zostavenie dobehlo, a spusti `node serve.mjs` z priečinka `web`. Podrobnosti o zostavení sú v `lumen-unity-web/Artifacts/build-6.6.log` (web) alebo `unity/Artifacts/build.log` (Windows). |

## Tím

Numerion vyvíja školský tím **Amogus (tím 3)**. Programovanie majú na starosti Ivan Beneda a Peter Baláž, grafiku Aleksandra Talyzina a Alexander Brezina. Každý odovzdáva svoju časť po týždňoch cez vlastnú vetvu.

Pri hlásení chyby uveď týždeň, platformu, ročník a kroky, pri ktorých sa dá chyba zopakovať. Ak ide o úlohu, pripoj aj jej znenie a odpoveď, ktorú hra vyhodnotila. [GitHub Issues](https://github.com/UCM-MS/Amogus67-Numerion/issues)
