# Numerion

Numerion je matematická hra pre 7. až 9. ročník ZŠ od tímu Amogus. Hráč si v akadémii vyberie ročník a tému, prejde si riešený príklad a vyskúša si ho v teste. Za správne odpovede trénuje spoločníka, s ktorým môže ísť do ťahového súboja.

## Vývoj hry

| Míľnik | Čo sa dá vyskúšať |
| --- | --- |
| [Pôvodná akadémia](prototypy/og/README.md) | Vyliahnuť prvého spoločníka, vyriešiť kvízy v troch pavilónoch a odomknúť ďalšie vajíčka. |
| [Prvý týždeň](docs/odovzdavky/w01/ivan-beneda.md) | V matematickom jadre zvoliť ročník a tému, riešiť 108 otázok a prejsť 18 kapitol. |
| [Druhý týždeň](https://github.com/UCM-MS/Amogus67-Numerion/pull/2) | Pokračovať v staršom rozpracovanom teste a po jeho dokončení si vyskúšať 36 nových úloh. |

Na tejto vetve môžeš spustiť pôvodnú akadémiu v prehliadači a otestovať matematické jadro druhého týždňa. Pre zostavenie celej 3D hry v Unity sú potrebné aj scéna a nastavenia projektu.

## Pôvodný webový prototyp

Prvá hrateľná akadémia z 8. októbra 2026 sa volala **LUMEN · Akadémia objavov**. Vznikla pre prehliadač s knižnicou Three.js. V troch pavilónoch bolo spolu 24 otázok z matematiky, vedy a prírody. Za prvé správne riešenie hráč získal poznatok, ktorý sa mu počítal k odomknutiu ďalšieho druhu vajíčka.

### Ako spustiť celú akadémiu

Otvor terminál v koreni stiahnutého repozitára. S Node.js 24 spusti tieto dva príkazy. Knižnica Three.js je pribalená.

```powershell
cd prototypy/og
node serve.mjs
```

V prehliadači otvor `http://127.0.0.1:4173/` a klikni na **Ideme objavovať**. Terminál nechaj počas hrania otvorený. Server ukončíš cez **Ctrl+C**.

**Čo skúsiť ako prvé.** Pri stánku kúp lúčne vajce za 40 mincí, zanes ho do liahne vo svojej základni a vyliahni spoločníka. Potom zájdi do pavilónu a odpovedz na otázku. Za jej prvé správne vyriešenie dostaneš poznatok. Po troch rôznych správne vyriešených otázkach sa odomkne vedecké vajce. Postup sa ukladá v danom prehliadači na rovnakej adrese.

Na počítači sa pohybuješ cez **WASD** alebo šípky, skáčeš medzerníkom a pri stánku či liahni používaš **E**. Kameru otočíš ťahaním myši a priblížiš kolieskom. Na dotykovej obrazovke máš virtuálnu páčku a tlačidlá na skok a interakciu.

### Test pravidiel pôvodnej akadémie

V repozitári sú [pravidlá prototypu](prototypy/og/dist/academy-engine.js) a [ich pôvodný test](prototypy/og/tests/academy-engine.test.mjs) z revízie `df7bba8`. Test skontroluje nákup a vyliahnutie vajíčka, všetkých 24 otázok, odmenu za prvé vyriešenie aj obnovenie uloženej hry. S Node.js ho spustíš z koreňa klonu.

```powershell
node prototypy/og/tests/academy-engine.test.mjs
```

Po úspechu sa vypíše riadok začínajúci `AcademyEngine:` a končiaci `passed.`.

## Čo robí hráč

Prvým cieľom môže byť kapitola **Zlomky bez hádania** v 7. ročníku. V učive si vyberieš tému **Čísla a zlomky** a prejdeš si riešený príklad. Jedna z úloh sa pýta, ako zapísať 0,375 ako zlomok v základnom tvare. Vysvetlenie ukáže, ako zlomok 375/1000 skrátiť na 3/8.

Tematický test vyberie šesť otázok. Na úspech potrebuješ aspoň päť správnych odpovedí. Pri chybách si môžeš pozrieť riešenie. V tréningovom súboji máš na výber silnejší ťah s otázkou alebo slabší rýchly výpad bez počítania.

## Čo pribudlo v druhom týždni

Pribudlo 36 matematických úloh. Každý ročník má teraz 48 otázok v šiestich témach. Ak už hráč v prvom týždni dokončil všetkých šesť otázok z témy, môže pokračovať dvoma novými. Jeho výsledky, mince aj získané odmeny sa zachovajú.

Spolu je v druhom týždni **144 otázok a 18 kapitol**. Pokrývajú čísla a zlomky, percentá a pomer, výrazy a rovnice, geometriu, dáta a pravdepodobnosť aj matematiku v praxi.

## Spustenie matematického jadra

Potrebuješ Git a .NET 10 SDK. Ak ešte nemáš miestnu kópiu, stiahni si priamo vetvu druhého týždňa.

```powershell
git clone --branch ivan/tyzden-02 https://github.com/UCM-MS/Amogus67-Numerion.git
cd Amogus67-Numerion
```

Ak už repozitár máš, vyber vetvu príkazom `git switch ivan/tyzden-02`. Z koreňa projektu spusti matematický test.

```powershell
dotnet run --project unity/Tests/MathHarness/MathHarness.csproj
```

Pri úspechu sa vypíše `NUMERION_MATH_HARNESS_PASS`. Test vykoná 974 kontrol vrátane načítania rozpracovaného testu z prvého týždňa, zachovania jeho výsledku a jednorazového prevzatia odmeny.

## Kde upraviť hru

| Chcem upraviť | Kde začať |
| --- | --- |
| Otázku, možnosti alebo vysvetlenie | [Generátor matematického učiva](unity/Tests/MathHarness/generate_content.py). Po zmene postupuj podľa [návodu na obnovu dát pre Unity a web](unity/Assets/Lumen/Resources/Content/README.md). |
| Výklad alebo riešený príklad v kapitole | [Kapitoly učebnice](unity/Assets/Lumen/Core/LessonCatalog.cs) |
| Výber úloh, odmeny alebo výsledok testu | [Pravidlá precvičovania](unity/Assets/Lumen/Core/LearningService.cs) a [tematické testy](unity/Assets/Lumen/Core/StudyTests.cs) |
| Spoločníkov, ich výbavu a súboje | [Prehľad tried herného jadra](unity/Assets/Lumen/Core/API.md) |

Pri pridávaní otázok zachovaj ID existujúcich úloh. Uložená pozícia podľa nich rozpozná, čo už hráč vyriešil. Po úprave učiva zopakuj zostavenie a matematický test. Ďalšie kontroly z koreňa repozitára pokrývajú aj herné pravidlá.

```powershell
dotnet run --project unity/Tests/CoreHarness/CoreHarness.csproj
dotnet run --project unity/Tests/CompanionHarness/CompanionHarness.csproj
dotnet build unity/Tests/CoreHarness/Compatibility/Compatibility.csproj
node --test web/tests/academy-engine.test.mjs web/tests/academy-journey.test.mjs
```

Prvé dva testy kontrolujú postup hráča, odmeny, výbavu a súboje. Tretí príkaz overí, že jadro možno zostaviť pre .NET Standard 2.1. Posledný otestuje pravidlá webovej hry a kroky jej tutoriálu.

## Keď sa spustenie nepodarí

Ak príkaz `dotnet` nenájde potrebné SDK alebo runtime, skontroluj výstup `dotnet --list-sdks` a `dotnet --list-runtimes`. Pre testy potrebuješ aj `Microsoft.NETCore.App 10.x`. Ak prehliadač neotvorí akadémiu, over, že terminál so serverom zostal otvorený a že si ho spustil v priečinku `prototypy/og`.

## Tím

Numerion vyvíja školský tím **Amogus (tím 3)**.

| Člen tímu | Zodpovednosť |
| --- | --- |
| Ivan Beneda | Pravidlá hry, matematické úlohy, učebnice a testy |
| Peter Baláž | Prepojenie herných systémov, multiplayer a zostavenia hry |
| Aleksandra Talyzina | 3D prostredie, spoločníci a vizuálne efekty |
| Alexander Brezina | Používateľské rozhranie, prístupnosť a prezentácia |

Pri hlásení chyby uveď týždeň, platformu, ročník a kroky, pri ktorých sa dá chyba zopakovať. Ak ide o úlohu, pripoj aj jej znenie a odpoveď, ktorú hra vyhodnotila. [GitHub Issues](https://github.com/UCM-MS/Amogus67-Numerion/issues)
