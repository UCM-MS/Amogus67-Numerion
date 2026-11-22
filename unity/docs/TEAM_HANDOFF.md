# Numerion · tím 3 · odovzdávanie na 10 týždňov

Tento plán rozdeľuje **aktuálny AI-asistovaný projekt** na zodpovednosti a budúce odovzdávacie kroky. Nepredstavuje desať týždňov už vykonanej práce. Nevytvára minulé commity, nemení dátumy a nepripisuje existujúci kód konkrétnemu človeku.

| Člen | Oblasť | Cieľ zodpovednosti |
|---|---|---|
| Ivan Beneda | Matematika, herné pravidlá, súboje, artefakty, učebnice a testy | 25 % |
| Peter Baláž | Online služby, spoločný profil, integrácia a zostavenia | 25 % |
| Aleksandra Talyzina | 3D svet, peti, animovanie a materiály | 25 % |
| Alexander Brezina | Menu, HUD, učenie, aréna a prezentácia rozhrania | 25 % |

Percentá sú **dohodnuté zodpovednosti**, nie meranie autorstva, riadkov kódu, veľkosti ZIPov alebo odpracovaných hodín. Presné súbory a týždeň ich odovzdania určuje [TEAM_OWNERSHIP.json](TEAM_OWNERSHIP.json). Každý zdrojový súbor má jedného vlastníka a jeden týždeň; jeho `.meta` súbor sa drží rovnakého vlastníka.

## Čo dostanete

- **Spoločný základ**: zdroje zo skutočných Git stavov Unity `12f6b5c` a webu `499c4c8`. Rozbalí sa raz.
- **Úplný aktuálny zdroj**: referenčný a obnovovací balík zmrazeného stavu.
- **Štyri rolové ZIPy**: všetky zmeny jednej osoby oproti spoločnému základu.
- **Štyridsať týždenných ZIPov**: jeden pre každého člena v každom z desiatich týždňov. Obsahujú priradené celé súbory, zoznam odstránení a konkrétny odovzdávací postup. Ak v danom týždni osoba nemá nový zdrojový súbor, balík to výslovne uvádza a obsahuje kontrolné a plánovacie podklady.
- **Manifest a SHA-256**: počty, vlastníctvo, presné súbory a kontrolné súčty. Je možné overiť, že výsledné zdroje zodpovedajú zmrazenému stavu.

Vyberte **buď štyri rolové balíky naraz, alebo týždenný postup**. Týždenné ZIPy nie sú štyridsať kópií celého projektu. Sú to prírastky k spoločnému základu. Úplný referenčný balík nepoužívajte ako ďalšiu sadu zmien do už rozpracovaných vetiev.

## Desať týždňov

| Týždeň | Odovzdávacia téma |
|---|---|
| 01 | Spoločný základ, rozdelenie práce a dátový model |
| 02 | Matematický obsah a postup pre 7.–9. ročník |
| 03 | Kolekcia petov, modely a pohyb |
| 04 | Pravidlá ťahových pet súbojov |
| 05 | Artefakty a odmeny za riešenia |
| 06 | Učebnice a tematické testy |
| 07 | Online služby a spoločné miestnosti |
| 08 | Spoločný profil a priebežné uloženie |
| 09 | Integrácia herných systémov, sveta a rozhrania |
| 10 | Kontroly, zostavenia a záverečné odovzdanie |

Celé súbory, ktoré spájajú viac systémov, sa odovzdávajú naraz v 9. týždni. Kód sa nerozrezáva na nekompilovateľné úryvky. Názvy týždňov hovoria aj o kontrole a integrácii: nemusia znamenať, že každá funkcia vznikla práve vtedy. Medzistavy harmonogramu nie sú automaticky overené Unity zostavenia. Po každom spoločnom kroku treba vykonať uvedené kontroly.

## Ako odovzdávať

1. Koordinátor rozbalí spoločný základ do jedného čistého priečinka. Všetci vychádzajú z rovnakého základu.
2. Každý člen otvorí iba svoj balík daného týždňa a prečíta jeho `ODOVZDANIE.txt` a manifest. Najprv porovná očakávané súbory a kontrolné súčty.
3. Celé zdrojové súbory sa skopírujú na rovnaké cesty. Odstránenia sa vykonajú podľa vlastného zoznamu až po kontrole; ZIP ich sám nevykonáva. V 9. týždni sa najprv spoja všetky štyri balíky a až potom sa kontroluje integrácia.
4. Člen zmeny skontroluje a nahrá **sám pod svojím účtom**. Do správy commitu pravdivo uvedie, čo upravil alebo overil; pri prevzatí to označí ako import AI-asistovaného podkladu. Dátumy ani autor sa spätne neupravujú.

Spoločný lokálny míľnik možno po overení pomenovať napríklad `team-w01`, neskôr `team-w02` až `team-w10`. Značku vytvára koordinátor až po skutočnom odovzdaní. Skript nemá príkaz na Git commit, push ani zverejnenie stránky.

## Čo je zámerne vynechané

Balíky neobsahujú `.git`, knižnice a cache Unity, `node_modules`, hotové zostavenia, osobné uloženia, prihlasovacie údaje ani konfiguráciu identity existujúceho Sites projektu. **Platené zdrojové balíky Odin a POLYGON/Synty ani ich odvodené zdrojové prefaby a materiály sa neposielajú.** Ak ich tím chce používať, každý príslušný používateľ musí mať potrebné oprávnenie a import vykonať samostatne. Pôvodný web OG zostáva súčasťou zdrojového základu bez požadovaných úprav jeho hry.

Kvôli čistému importu bez plateného SDK skript iba v odovzdávaných kópiách `ProjectSettings/ProjectSettings.asset` odstráni vendorové symboly `ODIN_INSPECTOR*` a `ODIN_VALIDATOR*`. Ostatné symboly, živý projekt a Git zostávajú nezmenené. Manifest výslovne uvádza túto prenositeľnú úpravu, odstránené symboly a pôvodné aj výsledné SHA-256. Kontrolné súčty balíkov zodpovedajú takto opísanému prenositeľnému zdrojovému stavu.

## Spustenie čistého zdrojového balíka

Spoločný základ a úplná záloha používajú priečinky **LUMEN/unity/** a **LUMEN/web/**. V pôvodnom pracovnom priečinku sa web volá `lumen`; v odovzdaní má zámerne jednotný názov `web`. Lokálny návod preto nájdeš v `LUMEN/web/UNITY-ONLINE-LOCAL.md`.

Na lokálny server stačí Node.js 24 alebo novší. V `LUMEN/web` spusti `node serve.mjs`. Nevytvára ani nevyžaduje identitu existujúceho Sites projektu. Pôvodnú hru otvor cez `/classic`. V zdrojovom balíku nie je hotový Unity Web player: jeho súbory `public/unity/Build/` sú vynechané a treba ich vytvoriť exportom z Unity. Samotné `public/unity/index.html` nepredstavuje hotovú hru. Kým nové zostavenie chýba, používaj `/classic` a Windows player zostavený zo zdrojov.

Príkaz `pnpm build` používa odlišnú cestu Sites/Vite a jeho konfigurácia importuje `.openai/hosting.json`. Skutočný súbor je vynechaný, pretože patrí existujúcemu nasadeniu. Ak chcete túto cestu overiť lokálne, príkaz `node scripts/prepare-local-hosting.mjs` vytvorí výslovne lokálnu náhradu s markerom `local-only-not-a-deployed-site` a väzbou `DB`. Nečíta ani neprepisuje existujúcu konfiguráciu; `--check` nič nevytvára. Náhrada nie je identitou na publikovanie. Pri skutočnom nasadení je potrebná vlastná konfigurácia Sites; nekopírujte cudziu produkčnú identitu ani súkromné údaje. Na bežné lokálne tímové testovanie stačí `node serve.mjs` a Unity export. Žiadny priložený nástroj hru sám nepublikuje.

## Príprava balíkov pre koordinátora

Najprv len kontrola plánu, bez vytvorenia ZIPov:

```text
python Tools/prepare_handoff.py --check
python Tools/prepare_handoff.py --self-test
```

Po zastavení zmien zdrojov a po kontrolách sa zachytí stabilný stav. Výstupný priečinok musí byť mimo oboch zdrojových repozitárov:

```text
python Tools/prepare_handoff.py --capture-freeze ../LUMEN-TEAM-HANDOFF/freeze-0.8.1.json
python Tools/prepare_handoff.py --package --freeze ../LUMEN-TEAM-HANDOFF/freeze-0.8.1.json --output ../LUMEN-TEAM-HANDOFF --version 0.8.1
```

Ak sa po zmrazení zmení čo len jeden zahrnutý zdrojový súbor, vytvorenie balíkov sa zastaví. Nové zmrazenie treba spraviť až po vedomej kontrole novej verzie. Predchádzajúce balíky sa automaticky neprepisujú. Skript uchováva pôvodné skutočné Git identifikátory aj odtlačok aktuálnych zdrojov; netvrdí, že necommitované zmeny už majú vlastný commit.

Staršie balíky **0.7.0** a ich zmrazenie zostávajú historickým snapshotom. Aktuálny príklad vytvára oddelené vydanie **0.8.1**; interné priečinky a názvy ZIPov `LUMEN` sa kvôli kompatibilite nemenia.
