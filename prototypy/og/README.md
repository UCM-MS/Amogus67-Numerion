# LUMEN · Akadémia objavov

Pôvodný prehliadačový prototyp Numerionu z 8. októbra 2026. Začínaš v akadémii so 150 mincami. Kúpiš si prvé vajce, vyliahneš spoločníka a v troch pavilónoch zbieraš poznatky z matematiky, vedy a prírody. Celý prototyp sa dá hrať sólo.

## Spustenie

Z koreňa školského repozitára spusti s Node.js 24 nasledujúce príkazy.

```powershell
cd prototypy/og
node serve.mjs
```

Otvor `http://127.0.0.1:4173/` v prehliadači s podporou WebGL 2 a stlač **Ideme objavovať**. Knižnica Three.js je súčasťou projektu. Terminál nechaj otvorený, kým hráš. **Ctrl+C** ukončí server. Chyba `EADDRINUSE` znamená, že port 4173 už používa iný lokálny server.

## Ovládanie

| Akcia | Ovládanie |
| --- | --- |
| Pohyb a skok | WASD alebo šípky, medzerník na skok |
| Rozhliadnutie a priblíženie | Ťahanie myšou po svete, koliesko na zmenu vzdialenosti |
| Použitie stánku, liahne alebo pavilónu | Kláves E v blízkosti objektu |
| Otvorenie panelov | 1 spoločníci, 2 vajíčka, 3 vylepšenia, 4 denník |
| Pohyb na dotykovej obrazovke | Virtuálna páčka a tlačidlá na skok a interakciu |

## Herný cyklus

1. Pri stánku kúp **Lúčne vajce** za 40 mincí a zanes ho do liahne na svojej základni. Vyliahnutý spoločník zarába mince. V paneli spoločníkov si vyberieš, ktorý bude chodiť za tebou.
2. Vstúp do jedného z troch pavilónov. Za prvé správne vyriešenie otázky dostaneš 50 mincí, 25 XP a jeden poznatok. Opätovné správne riešenie rovnakej otázky dá 10 mincí a 5 XP. V pavilónoch je spolu 24 rôznych otázok.
3. S troma poznatkami si môžeš kúpiť vedecké vajce. Pri ôsmich sa sprístupní kozmické vajce aj astrálna brána. Mince môžeš použiť aj na rýchlejší pohyb, väčší batoh alebo vyšší príjem.

Šesť spoločníkov a dvanásť poznatkov umožní začať novú éru s trvalým bonusom k príjmu. Pri ďalšom hraní otvor rovnakú adresu v rovnakom prehliadači. Postup sa ukladá do jeho lokálneho úložiska.

## Overenie

V priečinku `prototypy/og` spusti `node tests/academy-engine.test.mjs`. Test prejde nákup vajíčka, kapacitu batoha, liahnutie, 24 otázok, opakované odmeny, vylepšenia, príjem, novú éru aj obnovenie uloženej hry. Po úspechu vypíše riadok začínajúci `AcademyEngine:` a končiaci `passed.`.

## Rozdelenie tímu

Pri každej časti prototypu je uvedený tímový kontakt a miesto v zdrojovom kóde.

| Člen tímu | Oblasť | Súbory |
| --- | --- | --- |
| Ivan Beneda | Herné pravidlá. Otázky, odmeny, vajíčka, spoločníci a uloženie postupu. | [Pravidlá akadémie](dist/academy-engine.js) a [test herného cyklu](tests/academy-engine.test.mjs) |
| Peter Baláž | Programové prepojenie. Pohyb postavy, kamera, vstup z klávesnice aj dotyku, prepojenie scény s pravidlami a lokálne spustenie. | [Ovládanie a integrácia](dist/game3d.js), [server](serve.mjs) a [spúšťacie príkazy](package.json) |
| Aleksandra Talyzina | Grafika sveta. Priestorové rozloženie ostrova, pavilóny, modely postáv a spoločníkov. | [3D scéna akadémie](dist/scene3d.js) |
| Alexander Brezina | Rozhranie. Úvodná obrazovka, ukazovatele mincí a poznatkov, panely a dotykové ovládacie prvky. | [Rozloženie obrazovky](dist/index.html) a [vzhľad rozhrania](dist/style.css) |

Zdrojová revízia prototypu je `b5b9d80`. Hra používa knižnicu Three.js. [Licencia MIT](dist/vendor/THREE-LICENSE.txt)
