# Numerion v prehliadači

Po [zostavení webovej verzie](../README.md#zostavenie-pre-windows-a-web) sa na hlavnej adrese servera otvorí Numerion v Unity. Používa rovnakých 144 úloh pre 7. až 9. ročník, 18 kapitol a desať spoločníkov ako Windows verzia. Obe verzie sa môžu pridať do rovnakej miestnosti. [Spustenie servera a pripojenie hráčov](UNITY-ONLINE-LOCAL.md)

## Klasická webová akadémia

Na ceste `/classic` beží samostatná verzia akadémie v Three.js. Má vlastné učivo, zbierku spoločníkov a uložený postup. Nasledujúce pravidlá a ovládanie platia pre tento svet. Najstarší hrateľný prototyp má vlastný [návod na spustenie](../prototypy/og/README.md).

### Spoločné hranie

Menu → Hrať spolu → Vytvoriť spoločný svet. Kamarát otvorí pozývací odkaz alebo zadá šesťznakový kód. Až osem hráčov chodí v tom istom 3D ostrove; vidíš ich mená, pohyb, skoky, mávanie a spoločníkov. Spojenie pokračuje aj po zatvorení pozývacej karty. Mapa a tlačidlo Najbližší spoločný maják ukážu bezpečnú trasu.

Spoločná výprava má šesť majákov s krátkymi poznatkami. Do zbierania sa zapoja aspoň dvaja aktívni hráči; každý prispievateľ získa 60 serverových úlomkov presne raz. Odmena neovplyvňuje zápasový rating. Server kontroluje vzdialenosť zbierania, rýchlosť pohybu, kapacitu a pevné kontrolné body parkouru. Miestnosť zostáva po výprave otvorená, pri hodine bez aktivity vyprší. Krátke výpadky a uspaté karty sa znovu pripoja.

Aréna vytvorí miestnosť s kódom alebo odkazom. Každý hráč sa pripojí z vlastného prehliadača alebo zariadenia. Kvíz podporuje 2 až 8 hráčov, duel dvoch. Otázky, čas, skóre, zdravie, rating, úlomky a výbavu vyhodnocuje server. Odmeny sa zapíšu presne raz. Zápas má najviac šesť otázok, 20 sekúnd na odpoveď a 4 sekundy na vysvetlenie; po všetkých odpovediach sa kolo vyhodnotí hneď.

V dueli si vyberáš útok, obranu alebo sústredenie. Správna odpoveď aktivuje taktiku. Úlomky získané aktívnym hraním vylepšujú silu, štít a sústredenie na tri stupne. Rebríček obsahuje hráčov s dokončeným zápasom.

### Dobrodružstvo

36 otázok v štyroch predmetoch s nápovedou, vysvetlením, sériami a prehľadom poznatkov. Vajíčka, zbierka spoločníkov, päť úrovní tréningu, pasívny príjem, vylepšenia základne a prestíž. Šesť výprav, dvanásť kryštálov a sedem plošín parkouru s kontrolnými bodmi a osobným rekordom. Mapa naviguje okolo prekážok. Automatická grafika a úsporný režim pomáhajú pomalším zariadeniam.

Ostrov má oceán, vodopád, plávajúce záhrady, odlišné štvrte, pohyb rastlín a pomalé striedanie dňa a noci. Šesť typov spoločníkov má vlastné modely a animácie. Kamera sa skracuje pred stenami; svet využíva zdieľané geometrie, instancovanie a jeden atlas nápisov. Mobilné rozhranie má jedno Menu a zbalené úlohy.

Sólo svet sa ukladá do localStorage (pôvodný kľúč lumen-academy-3d-v2 je zachovaný). V nastaveniach sa dá exportovať a obnoviť JSON záloha. Online profil, výsledky a výbava sú uložené na serveri; prehliadač používa anonymnú HttpOnly cookie, obnovovanú pri online hraní. Záloha sólo sveta neprenáša online profil do iného prehliadača. Postavy s menovkou sú pripojení hráči; ostatné dekoratívne postavy sú NPC. Vajíčka a osobný postup zostávajú každému hráčovi vlastné. Mince a úlomky sú iba herné odmeny.

### Ovládanie

- WASD / šípky: pohyb; Shift: beh; medzerník: skok; E: preskúmať miesto.
- Ťahanie myšou: kamera; koliesko: priblíženie.
- 1–5: spoločníci, vajíčka, vylepšenia, denník, výpravy; M: mapa.
- Escape: otvoriť alebo zavrieť Menu.
- Mobil: virtuálny ovládač, skok a jedno tlačidlo Menu. Hrať spolu, Aréna aj nastavenia sú v Menu.

### VR režim

Otvor verejnú HTTPS adresu hry v prehliadači VR headsetu alebo v kompatibilnom prehliadači počítača s pripojeným headsetom. V Menu vyber VR režim, skontroluj dostupnosť a stlač Vstúpiť do VR. Je potrebná podpora WebXR `immersive-vr` a sledované ovládače. Režim používa ten istý ostrov, osobný postup a spoločný svet ako bežné hranie. Pri nedostupnom WebXR zostáva hra ovládateľná na obrazovke.

- Ľavá páčka: pohyb v smere pohľadu.
- Pravá páčka: otočenie po 30°.
- Spúšť: vybrať tlačidlo na paneli alebo preskúmať blízke miesto.
- A: skok. B alebo stisk bočného tlačidla: otvoriť či zavrieť VR menu.
- Ukáž ovládačom na panel a použi spúšť; ukončenie VR v menu vráti hru na obrazovku.

Do spoločného sveta sa pripoj pred vstupom do VR; meno a kód miestnosti zadaj na obrazovke. V headsete sa dajú vyberať odpovede a tlačidlá herných panelov. WebXR vyžaduje bezpečný kontext: verejnú HTTPS adresu alebo localhost. Bežná HTTP adresa počítača v lokálnej sieti režim VR neposkytuje. Rozhranie zobrazuje podporu zariadenia a chybu pri odmietnutí alebo prerušení relácie.

Automatizované testy bez headsetu neoverujú fyzické sledovanie hlavy, obraz v šošovkách ani pohodlie pohybu. Tie treba skontrolovať v používanom headsete.

Testy VR overujú odmietnuté povolenie, opakované spustenie, návrat kamery, páčky a tlačidlá, systémové obrazovky headsetu, výber odpovedí lúčom, stránkovanie kvízov a uvoľnenie zdrojov. Režim používa [WebXR](https://developer.mozilla.org/en-US/docs/Web/API/WebXR_Device_API) a [WebXRManager v Three.js](https://threejs.org/docs/pages/WebXRManager.html).

## Spustenie a overenie

Node.js 24 alebo novší. Po inštalácii závislostí cez pnpm install spusti node serve.mjs a otvor http://127.0.0.1:4173/. Lokálna SQLite databáza v ignorovanom .sites-runtime sa pripraví z migrácií pri štarte. Three.js aj všetky herné moduly sú lokálne assety, bez CDN.

pnpm test preverí engine, fyziku pri 10/20/60 FPS, kameru, dosiahnuteľnosť sveta, sedem skokov, modely, multiplayer cez skutočnú SQLite, súbežnosť, výplaty po výpadku a UI pripojenia. node tests/http-smoke.mjs preverí dva nezávislé cookie profily cez HTTP, celý kvíz, duel, výbavu a rebríček. Na zadanom produkčnom URL používaj --lobby-only pre kontrolu pripojenia bez testovacích výsledkov. node tests/explore-http.mjs URL preverí spoločné chodenie, gesto, korekciu neplatnej polohy a kontrolný bod. tests/ui-opponent.mjs a tests/ui-world-peer.mjs sú QA návštevníci na overenie prehliadača, nie súčasť hry.

Produkčné zostavenie: pnpm build. Zachovaný Vinext Sites build plugin kopíruje identitu, D1 binding a Drizzle migrácie do dist/.openai. Frontend zdroje sú v public/, serverová logika vo worker/, API vstup v app/api/multiplayer. Deploy sa robí cez Sites na pôvodný projekt a verejnú adresu.

## Licencia knižnice

Three.js je MIT; licencia je v public/vendor/THREE-LICENSE.txt.
