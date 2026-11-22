# Numerion

Numerion je matematická hra pre 7. až 9. ročník ZŠ. V akadémii si žiak vyberie učivo, prejde si riešený príklad a vyskúša sa v teste. Správne odpovede využije aj v ťahových súbojoch spoločníkov.

## Druhý týždeň - mapa, profil a chat

Na tejto vetve je práca Alexandra Brezinu na rozhraní hry. Mapa ukazuje centrálne námestie, šesť pavilónov a tri expedičné ostrovy. Keď si hráč označí cieľ, po návrate do sveta ho vedie šípka a vidí, ako ďaleko má ísť. V editore vzhľadu si postavu môže otočiť potiahnutím. Na menšom displeji ostáva náhľad na mieste, kým sa hráč posúva medzi nastaveniami. V spoločnej miestnosti môže napísať správu alebo použiť jedno zo šiestich gest.

| Chcem upraviť | Kde začať |
| --- | --- |
| Popisky pavilónov, legendu a výber cieľa | [Mapa akadémie](unity/Assets/Lumen/Runtime/LumenHud.Map.cs) |
| Smer a vzdialenosť k vybranému cieľu | [Navigácia hráča](unity/Assets/Lumen/Runtime/LumenHud.Navigation.cs) |
| Voľby vzhľadu a otáčanie postavy | [Editor profilu](unity/Assets/Lumen/Runtime/LumenHud.Profile.cs) a [3D náhľad](unity/Assets/Lumen/Runtime/LumenCharacterPreview.cs) |
| Správy a reakcie v miestnosti | [Rozhranie chatu](unity/Assets/Lumen/Runtime/LumenHud.Chat.cs) |

Pri úprave mapy sa oplatí skúsiť cestu z námestia až k portálu. Cieľ musí zostať označený aj po zatvorení mapy. V editore profilu skontroluj, či sa pri otočení postavy neposunie aj zoznam nastavení. V spoločnom Unity projekte sa dá rozloženie preveriť cez **Numerion > Validate Responsive UI**. [Čo kontroluje test rozhrania](unity/Assets/Lumen/Editor/UiLayoutValidation.cs)

## Čo sa dá spustiť z tejto vetvy

**Pôvodná akadémia v prehliadači.** S Node.js 24 spusti z koreňa repozitára tieto príkazy.

```sh
cd prototypy/og
node serve.mjs
```

Otvor `http://127.0.0.1:4173/` a vyber **Ideme objavovať**. Pri stánku kúp lúčne vajce za 40 mincí a zanes ho do liahne. Na počítači slúži na pohyb WASD alebo šípky, na skok medzerník a na interakciu E. Na dotykovej obrazovke sa používa virtuálna páčka. Pôvodný prototyp má vlastné rozhranie. Mapa, editor vzhľadu a chat opísané vyššie patria do Unity verzie.

**Testy.** V novom termináli spusti z koreňa repozitára test pôvodnej akadémie a test matematického jadra. Na druhý príkaz potrebuješ .NET 10 SDK.

```sh
node prototypy/og/tests/academy-engine.test.mjs
dotnet run --project unity/Tests/MathHarness/MathHarness.csproj
```

Prvý test preverí aj liahnutie, 24 kvízov a obnovenie hry. Matematické jadro na tejto vetve má 108 otázok a 18 kapitol, jeho test vykoná 648 kontrol. Rozšírenie na 144 otázok je v [Ivanových zmenách za druhý týždeň](https://github.com/UCM-MS/Amogus67-Numerion/pull/2).

**Zostavenie Unity hry.** Táto vetva obsahuje zdrojové súbory rozhrania. Na ich spustenie v Unity potrebuješ aj spoločnú scénu akadémie, nastavenia projektu a ostatné herné komponenty. Samotný klon tejto vetvy teda na zostavenie Windows alebo WebGL hry nestačí.

## Tím Amogus

| Člen | Oblasť |
| --- | --- |
| Ivan Beneda | Matematika, učebnice, testy a pravidlá hry |
| Peter Baláž | Integrácia, sieťové spojenie a zostavenia |
| Aleksandra Talyzina | 3D prostredie, spoločníci a efekty |
| Alexander Brezina | Rozhranie, navigácia, profil a ovládanie |

Pri chybe rozhrania uveď obrazovku, veľkosť okna a kroky, ktorými sa dá problém zopakovať. [GitHub Issues](https://github.com/UCM-MS/Amogus67-Numerion/issues)
