# Numerion

Numerion je matematická hra pre 7. až 9. ročník ZŠ od tímu Amogus. Hráč si v akadémii vyberie ročník a tému, prejde si riešený príklad a vyskúša si ho v teste. Za správne odpovede trénuje spoločníka, s ktorým môže ísť do ťahového súboja.

## Týždenné odovzdania

| Verzia | Čo sa dá vyskúšať | Odovzdané súbory |
| --- | --- | --- |
| Pôvodný prototyp (OG) | Vyliahnuť prvého spoločníka, riešiť kvízy v troch pavilónoch a odomykať vajíčka | [Celý prototyp, návod a rozdelenie tímu](prototypy/og/README.md) |
| Týždeň 1 (0.7.0) | Matematické jadro, 108 otázok, 18 kapitol a pravidlá súbojov spoločníkov | [Súbory prvého týždňa](https://github.com/UCM-MS/Amogus67-Numerion/tree/75fede897d7cac37e90223b888c76ef72b1c4bd2) · [Ivanove poznámky](docs/odovzdavky/w01/ivan-beneda.md) |
| Týždeň 2 (0.11.0) | Ďalších 36 úloh a pokračovanie z uloženej pozície prvého týždňa | [Ivanove zmeny v PR #2](https://github.com/UCM-MS/Amogus67-Numerion/pull/2) |

Pôvodnú 3D akadémiu spustíš priamo z tohto repozitára v prehliadači. Pri týždenných verziách si tu môžeš overiť matematické jadro. Na hranie ich Unity scén slúži spojený tímový balík príslušného týždňa.

[Spustiť pôvodný prototyp](#pôvodný-webový-prototyp) · [Spustiť matematické jadro](#prvé-spustenie-zo-školského-githubu) · [Otvoriť hru v Unity](#hrateľná-verzia-pre-windows-a-web) · [Ovládanie](#ovládanie-v-akadémii) · [Upraviť učivo](#kde-upraviť-hru)

## Pôvodný webový prototyp

Prvá hrateľná akadémia z 8. októbra 2026 sa volala **LUMEN · Akadémia objavov**. Vznikla pre prehliadač s knižnicou Three.js. V troch pavilónoch bolo spolu 24 otázok z matematiky, vedy a prírody. Za prvé správne riešenie hráč získal poznatok, ktorý sa mu počítal k odomknutiu ďalšieho druhu vajíčka.

### Ako spustiť celú akadémiu

Otvor terminál v koreni stiahnutého repozitára. S Node.js 24 spusti tieto dva príkazy. Knižnica Three.js je pribalená.

```powershell
cd prototypy/og
node serve.mjs
```

V prehliadači otvor `http://127.0.0.1:4173/` a klikni na **Ideme objavovať**. Terminál nechaj počas hrania otvorený. Server ukončíš cez **Ctrl+C**.

Ak máš tímový ZIP, rozbaľ `W01/NA-HRANIE/OG-WEB-ZDROJE.zip` a spusti `node serve.mjs` priamo vedľa súboru `serve.mjs`. Na Discorde je ten istý archív samostatnou prílohou k W01. Prehľad súborov každej tímovej role nájdeš v [README pôvodnej hry](prototypy/og/README.md#rozdelenie-tímu).

**Čo skúsiť ako prvé.** Pri stánku kúp lúčne vajce za 40 mincí, zanes ho do liahne vo svojej základni a vyliahni spoločníka. Potom zájdi do pavilónu a odpovedz na otázku. Za jej prvé správne vyriešenie dostaneš poznatok. Po troch rôznych správne vyriešených otázkach sa odomkne vedecké vajce. Postup sa ukladá v danom prehliadači na rovnakej adrese.

Na počítači sa pohybuješ cez **WASD** alebo šípky, skáčeš medzerníkom a pri stánku či liahni používaš **E**. Kameru otočíš ťahaním myši a priblížiš kolieskom. Na dotykovej obrazovke máš virtuálnu páčku a tlačidlá na skok a interakciu.

### Ako overiť pôvodné pravidlá zo školského repozitára

V repozitári sú [pravidlá prototypu](prototypy/og/dist/academy-engine.js) a [ich pôvodný test](prototypy/og/tests/academy-engine.test.mjs) z revízie `df7bba8`. Test skontroluje nákup a vyliahnutie vajíčka, všetkých 24 otázok, odmenu za prvé vyriešenie aj obnovenie uloženej hry. S Node.js ho spustíš z koreňa klonu.

```powershell
node prototypy/og/tests/academy-engine.test.mjs
```

Po úspechu sa vypíše riadok začínajúci `AcademyEngine:` a končiaci `passed.`. Ak si práve v rozbalenom plnom OG, ten istý test spustíš kratším príkazom `node tests/academy-engine.test.mjs`.

## Čo robí hráč

Prvým cieľom môže byť kapitola **Zlomky bez hádania** v 7. ročníku. V učive si vyberieš tému **Čísla a zlomky** a prejdeš si riešený príklad. Jedna z úloh sa pýta, ako zapísať 0,375 ako zlomok v základnom tvare. Vysvetlenie ukáže, ako zlomok 375/1000 skrátiť na 3/8.

Tematický test vyberie šesť otázok. Na úspech potrebuješ aspoň päť správnych odpovedí. Pri chybách si môžeš pozrieť riešenie. V tréningovom súboji máš na výber silnejší ťah s otázkou alebo slabší rýchly výpad bez počítania.

## Čo pribudlo v druhom týždni

Pribudlo 36 matematických úloh. Každý ročník má teraz 48 otázok v šiestich témach. Ak už hráč v prvom týždni dokončil všetkých šesť otázok z témy, môže pokračovať dvoma novými. Jeho výsledky, mince aj získané odmeny sa zachovajú.

Spolu je v druhom týždni **144 otázok a 18 kapitol**. Pokrývajú čísla a zlomky, percentá a pomer, výrazy a rovnice, geometriu, dáta a pravdepodobnosť aj matematiku v praxi.

## Prvé spustenie zo školského GitHubu

Potrebuješ Git a .NET 10 SDK. Funguje aj novšie SDK s nainštalovaným runtime .NET 10. Stiahni si repozitár a otvor jeho priečinok.

```powershell
git clone https://github.com/UCM-MS/Amogus67-Numerion.git
cd Amogus67-Numerion
```

Klon sa otvorí na hlavnej vetve. Ak chceš vyskúšať druhý týždeň, najprv zadaj `git switch ivan/tyzden-02`. Jadro zvolenej verzie potom zostavíš a overíš rovnakými príkazmi.

```powershell
dotnet build unity/Tests/MathHarness/MathHarness.csproj
dotnet run --no-build --project unity/Tests/MathHarness/MathHarness.csproj
```

Posledný riadok pre prvý týždeň obsahuje `LUMEN_MATH_HARNESS_PASS`, 648 kontrol a 108 otázok. Pri druhom týždni uvidíš `NUMERION_MATH_HARNESS_PASS`, 974 kontrol a 144 otázok. Unity Editor potrebuješ až pri spustení hernej scény.

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

Numerion vyvíja školský tím **Amogus (tím 3)**.

| Člen tímu | Oblasť odovzdania |
| --- | --- |
| Ivan Beneda | Pravidlá hry, matematické úlohy, učebnice a testy |
| Peter Baláž | Prepojenie herných systémov, multiplayer a zostavenia hry |
| Aleksandra Talyzina | 3D prostredie, spoločníci a vizuálne efekty |
| Alexander Brezina | Používateľské rozhranie, prístupnosť a prezentácia |

Každý člen má vo svojom týždennom balíku vlastný zoznam zmien v `W01/ODOVZDANIE.md` (v ďalšom týždni `W02/ODOVZDANIE.md`). Na svoju vetvu prenesie príslušné súbory z priečinka `SUBORY` a otvorí pull request. Priečinok `NA-HRANIE` slúži celému tímu na vyskúšanie rovnakej verzie.

Pri hlásení chyby uveď týždeň, platformu, ročník a kroky, pri ktorých sa dá chyba zopakovať. Ak ide o úlohu, pripoj aj jej znenie a odpoveď, ktorú hra vyhodnotila. [GitHub Issues](https://github.com/UCM-MS/Amogus67-Numerion/issues)
