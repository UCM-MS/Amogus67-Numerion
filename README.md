# Numerion

Numerion je matematická hra pre 7. až 9. ročník ZŠ. Na ostrovoch akadémie zbieraš spoločníkov, vyberáš im výbavu a bojuješ s nimi v ťahových súbojoch. Pri silnejšom útoku riešiš úlohu zo svojej témy. Keď si ešte neveríš, môžeš zvoliť slabší výpad bez otázky alebo si postup precvičiť v učebnici.

Na vetve `main` je spoločná verzia **0.11.0 za 2. týždeň**. Obsahuje hru v Unity pre Windows a web, 144 matematických úloh, 18 kapitol, desať spoločníkov a tri expedičné ostrovy. [Pôvodná akadémia](#pôvodná-akadémia) sa dá spustiť samostatne aj bez Unity.

## Spustenie v Unity

1. Stiahni projekt príkazom `git clone https://github.com/UCM-MS/Amogus67-Numerion.git`.
2. V Unity Hube otvor **Projects > Add > Add project from disk** a vyber priečinok `Amogus67-Numerion/unity`.
3. Otvor ho v editore **6000.6.6f1** a počkaj na dokončenie prvého importu. Verzia je uvedená v [ProjectVersion.txt](unity/ProjectSettings/ProjectVersion.txt).
4. V okne **Project** otvor scénu [Assets/Lumen/Generated/Academy.unity](unity/Assets/Lumen/Generated/Academy.unity) a stlač **Play**. Ak potrebuješ znova pripraviť scénu a jej nastavenia, v editore vyber **Numerion > Prepare Academy**.

## Prvé minúty v akadémii

Na titulnej obrazovke stlač **Hrať**, vyber si ročník a prejdi úvod so sprievodcom. V menu otvor **Výprava > Učebnice a testy**, zvoľ si tému a prečítaj si riešený príklad. V siedmackej kapitole *Zlomky bez hádania* uvidíš, prečo 1/3 + 1/6 dáva polovicu. Tematický test vyberie šesť otázok. Päť správnych odpovedí stačí na úspech. Po teste si môžeš prejsť chyby aj s vysvetlením.

Mapu otvoríš klávesom **M**. Nájdeš na nej pavilóny aj portály na výpravy **Príliv zlomkov**, **Geometrické ruiny** a **Observatórium rovníc**. Na ostrovy sa dostaneš cez portály. Ak chceš vyzvať spoluhráča, v menu otvor **Spoločne > Hrať s priateľmi**, založ miestnosť a daj mu jej šesťznakový kód.

| Akcia | Klávesnica a myš |
| --- | --- |
| Pohyb, beh a skok | WASD alebo šípky, ľavý Shift, medzerník |
| Rozhliadnutie a vzdialenosť kamery | Podrž pravé tlačidlo myši a pohni myšou, priblíženie nastav kolieskom |
| Rozhovor alebo použitie stanovišťa | E |
| Mapa, denník, výber učiva | M, J, K |
| Chat a hlavné menu | Enter, Esc |

Na dotykovej obrazovke zapni virtuálne ovládanie. Pohyb ovláda páčka, skok a interakcia majú vlastné tlačidlá. Na menšom displeji odporúčame orientáciu na šírku.

## Zostavenie pre Windows a web

Príkazy spúšťaj v PowerShelli z koreňa repozitára. Pred zostavením zatvor Unity Editor s otvoreným projektom. Pri prvom zostavení potrebuje Unity prístup k svojim balíkom.

| Cieľ | Čo potrebuješ | Výstup |
| --- | --- | --- |
| Windows | Unity 6000.6.6f1 s podporou zostavenia pre Windows | `unity/Builds/Windows/LumenAcademy.exe` |
| Prehliadač | Ten istý editor s modulom **Web Build Support**, Python 3 a Node.js 24 | `lumen-unity-web/Builds/WebGL`, lokálny server ho podáva z `web/public/unity` |

**Windows.** Spusti zostavenie a potom otvor hráča. Pri prenose na druhý počítač skopíruj celý priečinok `unity/Builds/Windows` aj s dátami.

```powershell
.\unity\Tools\Build-Unity.ps1 -Action Build
.\unity\Builds\Windows\LumenAcademy.exe
```

**Web.** Prvý príkaz vytvorí webový projekt zo spoločných zdrojov, zostaví ho a pripraví súbory pre server. Druhý otvorí hru v prehliadači na lokálnej adrese `http://127.0.0.1:4174/`.

```powershell
.\unity\Tools\Build-Web.ps1
.\unity\Tools\Start-Lumen.ps1 -Mode Web
```

**Hra dvoch hráčov.** Po zostavení oboch verzií spusti `.\unity\Tools\Start-Lumen.ps1 -Mode Both`. Otvorí Windows hráča aj web na tom istom serveri. Pre ďalšie zariadenie v rovnakej sieti pridaj `-LAN` a otvor na ňom adresu, ktorú skript vypíše. Každý hráč použije vlastný profil. [Miestnosti a lokálne testovanie](web/UNITY-ONLINE-LOCAL.md)

Ak je Unity nainštalované mimo Unity Hubu, zostavovaciemu skriptu zadaj `-UnityPath` s úplnou cestou k `Unity.exe`. Pri chybe skontroluj `unity/Artifacts/build.log` pre Windows alebo `lumen-unity-web/Artifacts/build-6.6.log` pre web. Podrobnosti o vykresľovaní a voliteľných modeloch sú v [poznámkach k webovej verzii](unity/docs/WEB.md) a [prehľade grafických podkladov](unity/docs/ASSETS.md).

## Pôvodná akadémia

**LUMEN · Akadémia objavov** je prvý hrateľný prototyp. S Node.js 24 ho spustíš z koreňa repozitára.

```sh
cd prototypy/og
node serve.mjs
```

Otvor `http://127.0.0.1:4173/` a vyber **Ideme objavovať**. Začínaš so 150 mincami. Kúp lúčne vajce za 40 mincí a zanes ho do liahne. S prvým spoločníkom sa vydaj do jedného z troch pavilónov. Tri rôzne správne odpovede odomknú vedecké vajce. Postup sa ukladá v prehliadači. [Ovládanie a pravidlá pôvodnej akadémie](prototypy/og/README.md)

## Vývoj hry

| Verzia | Čo pribudlo |
| --- | --- |
| Pôvodná akadémia, 8. október 2026 | Prvý ostrov v prehliadači, tri pavilóny s 24 otázkami, vajíčka a liaheň. |
| Týždeň 1 | Akadémia v Unity, základné rozhranie a sieťová hra. Matematické jadro obsahovalo 108 úloh v šiestich témach, 18 kapitol a pravidlá súbojov spoločníkov. [Matematické jadro prvého týždňa](docs/odovzdavky/w01/ivan-beneda.md) |
| Týždeň 2 | Všetky tri ročníky dostali po 12 úloh navyše. Pribudli expedičné ostrovy, mapa s navigáciou, úprava postavy a chat v miestnosti. Uložené výsledky hráča sa prenášajú aj do rozšíreného učiva. |

## Úprava hry

| Časť | Kde začať |
| --- | --- |
| Otázky a vysvetlenia | [Generátor učiva](unity/Tests/MathHarness/generate_content.py) a [postup obnovy dát pre Unity aj server](unity/Assets/Lumen/Resources/Content/README.md) |
| Učebnice a tematické testy | [Kapitoly a riešené príklady](unity/Assets/Lumen/Core/LessonCatalog.cs), [precvičovanie](unity/Assets/Lumen/Core/LearningService.cs) a [vyhodnotenie testu](unity/Assets/Lumen/Core/StudyTests.cs) |
| Spoločníci, útoky a výbava | [Pravidlá herného jadra](unity/Assets/Lumen/Core/API.md) |
| Ostrovy, portály a prostredie | [World](unity/Assets/Lumen/World) |
| Ovládanie, kamera a rozhranie | [Runtime](unity/Assets/Lumen/Runtime) |
| Spoločné miestnosti | [Server a pravidlá online hry](web/worker) |

Pri úprave existujúcej otázky zachovaj jej ID. Uložený profil podľa neho rozpoznáva vyriešené úlohy.

### Testy

Testy jadra používajú **.NET 10 SDK**, testy prototypu a miestností **Node.js 24**. Spusti ich z koreňa repozitára.

```sh
dotnet run --project unity/Tests/MathHarness/MathHarness.csproj
dotnet run --project unity/Tests/CoreHarness/CoreHarness.csproj
dotnet run --project unity/Tests/CompanionHarness/CompanionHarness.csproj
node prototypy/og/tests/academy-engine.test.mjs
node web/tests/unity-chat.test.mjs
```

Matematický test kontroluje aj výber podľa ročníka a načítanie staršieho profilu. Testy spoločníkov overujú súboj, výbavu a odmeny. Pri hlásení chyby uveď platformu a postup, ktorým ju vieš zopakovať. Pri chybnej matematickej úlohe pomôže jej znenie a zadaná odpoveď. [GitHub Issues](https://github.com/UCM-MS/Amogus67-Numerion/issues)

## Tím 3 · Amogus

| Člen | Zameranie |
| --- | --- |
| Ivan Beneda | Matematické učivo, testy a herné pravidlá |
| Peter Baláž | Integrácia, multiplayer a zostavenia |
| Aleksandra Talyzina | Prostredie, spoločníci a vizuálne efekty |
| Alexander Brezina | Rozhranie a jeho ovládanie |
