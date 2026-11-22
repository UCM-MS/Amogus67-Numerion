# Voliteľné PC VR

Natívny režim cieli na Windows PC s OpenXR headsetom. Používa Unity OpenXR, XR Management, Input System a XROrigin z balíkov pripnutých v projekte. Nie je zostavením pre samostatný Quest ani webové WebXR.

## Spustenie

1. Pripoj headset k PC a v jeho sprievodnej aplikácii vyber aktívny OpenXR runtime. Pri Quest headsete to znamená PC spojenie cez Link/Air Link alebo iný kompatibilný PC OpenXR runtime.
2. Spusti Windows hru, vytvor profil a v **Menu → Virtuálna realita** vyber **Spustiť PC VR**.
3. Hra inicializuje loader a čaká najviac 12 sekúnd na bežiaci XR displej aj skutočne sledovaný headset. Bez oboch podmienok sa VR neoznačí za aktívne a loader sa vypne.
4. Tlačidlom **Ukončiť VR a hrať na monitore** obnovíš pôvodnú kameru, desktopový HUD aj vstupné akcie. Pri strate sledovania sa pohyb zablokuje; po desiatich sekundách sa hra vráti na monitor.

Editorové **LUMEN → Prepare Academy** nastavuje OpenXR automaticky. Samostatné **LUMEN → Prepare Optional PC VR** vytvorí konfiguráciu pre Standalone: manuálny štart, single-pass instanced stereo a profily Oculus Touch, Valve Index, Microsoft Motion, HTC Vive a KHR Simple. XR sa pri štarte desktopovej hry automaticky nespúšťa.

## Ovládanie

| Vstup | Akcia |
| --- | --- |
| Ľavá páčka / Vive trackpad | Pohyb podľa smeru pohľadu |
| Pravá páčka / Vive trackpad | Otočenie po 30°, po návrate páčky do stredu |
| Pravé A / Vive klik trackpadu | Skok |
| B alebo tlačidlo menu | Otvorenie a zatvorenie ponuky |
| Pravý spúšťač | Interakcia vo svete; v ponuke výber lúčom |
| Pravá páčka v ponuke | Posúvanie obsahu |

Ponuka je priestorové plátno pred hráčom a jej poloha sa nastaví pri otvorení. Meno a IP adresu zatiaľ zadáva fyzická klávesnica PC. KHR Simple umožňuje výber a menu; bez páčok neposkytuje pohyb a skok cez ovládač. Pohyb klávesnicou zostáva k dispozícii.

## Grafika a multiplayer

Pri aktivovaní VR sa prepne HDRP rasterizácia. Ray tracing a statický path tracing nie sú počas XR povolené. Frekvenciu displeja a prezentáciu obrazu určuje headset a jeho OpenXR runtime; desktopový odomknutý FPS limit nesľubuje odomknutú frekvenciu headsetu.

Pohyb páčkou používa rovnaký CharacterController a serverovú simuláciu ako desktopový hráč. Kamera sleduje headset; vlastný model postavy je v headsete skrytý. Ostatní hráči vidia sieťovú postavu, meno a vzhľad. Poloha a gestá rúk ani samostatná sieťová animácia hlavy sa neposielajú. Pohyb tela po miestnosti nie je samostatným autoritatívnym pohybovým vstupom.

## Stav overenia

Implementácia používa skutočné OpenXR subsystémy a sledované vstupy. Fyzický headset, stereo obraz a ovládanie v pripojenom headsete zatiaľ neboli otestované. Úspešná kompilácia ani desktopový obraz túto skúšku nenahrádzajú.
