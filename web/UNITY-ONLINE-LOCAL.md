# Unity PC + Web · spoločné lokálne testovanie

Tento nový online server nie je automaticky publikovaný na verejnej adrese. Pôvodná hra na `/classic` zostáva zachovaná. PC a Unity Web používajú tie isté miestnosti, matematické otázky pre 7.–9. ročník, súboje a cloudový profil.

Po zostavení webového hráča otvor PowerShell v priečinku `web` tohto repozitára. Server používa Node.js 24 alebo novší.

```powershell
$env:PORT='4174'
node serve.mjs
```

Otvor `http://127.0.0.1:4174/`. Webový klient si zvolí server podľa svojej adresy. Windows klient spusti s `-lumen-online-origin http://127.0.0.1:4174`. Adresa sa dá zmeniť aj v paneli **Hrať spolu**, keď nie si v miestnosti. Vytvor miestnosť a druhý hráč zadá jej šesťznakový kód. Každý má najprv vlastný profil; súkromný párovací kód používaj iba na prenesenie vlastného postupu medzi zariadeniami. Ten istý profil nemá súčasne dve postavy v jednej miestnosti.

Pre prístup z ďalších zariadení v rovnakej sieti nastav pred spustením:

```powershell
$env:HOST='0.0.0.0'
$env:PORT='4174'
node serve.mjs
```

Na druhom zariadení otvor `http://ADRESA-POCITACA:4174/` s jeho skutočnou súkromnou IPv4 adresou. Rovnakú adresu zadaj do Windows klienta. Počítač musí povoľovať prichádzajúce spojenia aplikácie Node na súkromnej sieti; tento návod nemení firewall ani nenastavuje prístup z internetu. Predvolené spustenie počúva iba na tomto počítači.

Pre dva samostatné Windows profily na jednom počítači pridaj rozdielne voľby `-lumen-profile-suffix hrac1` a `-lumen-profile-suffix hrac2`. Každý hráč tak dostane vlastnú uloženú hru aj cloudovú identitu. Lokálna databáza so súkromnými profilmi je v `.sites-runtime` a Git ju ignoruje.

Miestnosť podporuje 16 hráčov. Server prijíma autoritatívne snímky pohybu; klient ich posiela päťkrát za sekundu a zobrazenie ostatných hráčov vyhladzuje samostatne. Súboje sú ťahové, odpovede a odmeny vyhodnocuje server. Toto lokálne HTTP pripojenie neposkytuje bezpečný kontext pre WebXR.

Overenie pravidiel a online servera:

```powershell
node tests/unity-online.test.mjs
node tests/unity-battle-conformance.test.mjs
node tests/unity-online-timeouts.test.mjs
```

Pri bežiacom lokálnom serveri overíš aj skutočný HTTP prenos: `node scripts/unity-online-http-smoke.mjs http://127.0.0.1:4174`. Kontrola vytvorí dva testovacie profily, odohrá celý matematický duel, overí konflikty a opakovanie odmien a nakoniec hráčov odpojí. Nepíše súkromné kódy do výstupu a odmieta vzdialené adresy.
