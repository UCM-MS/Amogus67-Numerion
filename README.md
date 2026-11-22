# Numerion

Numerion je matematická hra pre 7. až 9. ročník ZŠ. V akadémii si vyberieš tému, prejdeš si príklad z učebnice a vyskúšaš ho v teste. To, čo sa naučíš, využiješ v ťahových súbojoch spoločníkov. Silnejší útok vyžaduje správne riešenie, rýchly výpad môžeš použiť aj bez otázky.

## Spustenie pôvodnej akadémie

Pôvodná **LUMEN · Akadémia objavov** je hrateľný 3D prototyp pre prehliadač. Z koreňa repozitára spusti s Node.js 24 tieto príkazy.

```sh
cd prototypy/og
node serve.mjs
```

Otvor `http://127.0.0.1:4173/` a vyber **Ideme objavovať**. Začínaš so 150 mincami. Pri stánku kúp lúčne vajce za 40 mincí a zanes ho do liahne. S prvým spoločníkom sa vydaj do jedného z troch pavilónov. Po troch rôznych správnych odpovediach sa odomkne vedecké vajce. Hra si postup zapamätá v prehliadači.

| Akcia | Ovládanie |
| --- | --- |
| Pohyb a skok | WASD alebo šípky, medzerník |
| Rozhliadnutie a priblíženie | Ťahanie myšou, koliesko |
| Použitie stánku, liahne alebo pavilónu | E |
| Pohyb na dotykovej obrazovke | Virtuálna páčka a tlačidlá na skok a interakciu |

[Ovládanie, odmeny a pravidlá pôvodnej akadémie](prototypy/og/README.md)

## Vývoj hry

| Míľnik | Čo pribudlo |
| --- | --- |
| Pôvodná akadémia, 8. október 2026 | Prvý ostrov, tri pavilóny s 24 otázkami, vajíčka, liaheň a uloženie postupu. |
| Týždeň 1 | Matematika pre tri ročníky, 108 úloh v šiestich témach, 18 kapitol, pravidlá súbojov a výbava spoločníkov. [Matematické jadro a jeho testy](docs/odovzdavky/w01/ivan-beneda.md) |
| Týždeň 2 | Matematické jadro má 144 úloh. V každej téme pribudli dve otázky pre každý ročník. Hráčovi po aktualizácii ostanú výsledky, mince aj odmeny. [Rozšírenie učiva v PR #2](https://github.com/UCM-MS/Amogus67-Numerion/pull/2) |

V kapitole **Zlomky bez hádania** si siedmak napríklad precvičí prevod 0,375 na zlomok. Učebnica ukáže zápis 375/1000 a skrátenie na 3/8. Tematický test vyberie šesť otázok, na úspech treba päť správnych odpovedí.

## Učivo a zdrojové súbory

| Oblasť | Kde ju upraviť |
| --- | --- |
| Otázky, odpovede a vysvetlenia | [Generátor matematického učiva](unity/Tests/MathHarness/generate_content.py) a [postup obnovy dát](unity/Assets/Lumen/Resources/Content/README.md) |
| Kapitoly a riešené príklady | [LessonCatalog](unity/Assets/Lumen/Core/LessonCatalog.cs) |
| Precvičovanie a výsledky testov | [LearningService](unity/Assets/Lumen/Core/LearningService.cs) a [StudyTests](unity/Assets/Lumen/Core/StudyTests.cs) |
| Spoločníci, útoky a výbava | [Pravidlá herného jadra](unity/Assets/Lumen/Core/API.md) |

Pri úprave otázky zachovaj jej ID. Uložená pozícia podľa neho rozpoznáva už vyriešené úlohy.

## Overenie

Z koreňa repozitára spusti testy matematického jadra s **.NET 10 SDK** a test pôvodnej akadémie s **Node.js 24**.

```sh
dotnet run --project unity/Tests/MathHarness/MathHarness.csproj
dotnet run --project unity/Tests/CoreHarness/CoreHarness.csproj
dotnet run --project unity/Tests/CompanionHarness/CompanionHarness.csproj
node prototypy/og/tests/academy-engine.test.mjs
```

Na vetve `main` obsahuje banka 108 matematických otázok. Verziu so 144 otázkami overíš rovnakým matematickým testom po prepnutí na `ivan/tyzden-02`.

## Unity

Projekt používa Unity **6000.6.6f1**. Jeho nastavenia sú v [unity/ProjectSettings](unity/ProjectSettings) a scéna v [Academy.unity](unity/Assets/Lumen/Generated/Academy.unity). Hlavná vetva zatiaľ obsahuje len časť zdrojov tejto scény. Z čistého klonu sa dá spustiť pôvodná akadémia a samostatné testy vyššie.

## Tím Amogus

| Člen | Zameranie |
| --- | --- |
| Ivan Beneda | Matematické učivo, testy a herné pravidlá |
| Peter Baláž | Integrácia, multiplayer a zostavenia |
| Aleksandra Talyzina | Prostredie, spoločníci a vizuálne efekty |
| Alexander Brezina | Rozhranie a jeho ovládanie |

Pri hlásení chyby uveď verziu, platformu a kroky, ktorými ju vieš zopakovať. Pri matematickej úlohe pomôže aj jej znenie a zadaná odpoveď. [GitHub Issues](https://github.com/UCM-MS/Amogus67-Numerion/issues)
