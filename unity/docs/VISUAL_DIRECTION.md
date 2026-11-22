# Výtvarný smer LUMEN

Akadémia má pôsobiť ako obývateľný matematický atlas: tmavé atramentové strechy, starý vápenec, mosadzné nástroje, tlmený tyrkys a prirodzená zeleň. Dominantou je centrálne observatórium. Šesť pavilónov sa rozlišuje matematickou expozíciou a nápisom. Farebné oblečenie hráča zostáva samostatnou kozmetikou.

Rozhranie používa Barlow Semi Condensed na texty a Cormorant SC na nadpisy. Nezmenené TTF, pôvod a licencie SIL OFL 1.1 sú v `Assets/Lumen/Resources/Fonts/`. Svetlé obsahové panely slúžia čítaniu; tmavý HUD necháva viditeľné prostredie. Princíp situačne dostupných informácií vychádza aj z [Riot Games User Interface Design](https://www.riotgames.com/en/artedu/user-interface-design). Škálovanie textu, ovládanie kamery a oddelené hlasitosti sú referenčné témy [WB Games Accessibility Features](https://support.wbgames.com/hc/en-us/articles/49627955891219-Hogwarts-Legacy-Accessibility-Features-A11Y); nejde o vyhlásenie podpory všetkých funkcií tohto zdroja.

Spoločné vlastné modely zahŕňajú desať odlišných siluet petov, troch mentorov a matematické expozície. `AcademyWorld.MathGarden.cs` autoruje 127 geometrických častí a približne 19 textových objektov navyše k NPC. `AcademyWorld.Atmosphere.cs` pridáva 90 rendererov pred statickým dávkovaním: strechy, vodopády, hmlu pri útesoch, paprade, intarzie a vzdialené archívy. Sú to počty objektov v kóde, nie profilované vykresľovacie volania. Atmosféra nepridáva kolízie ani priebežný aktualizačný cyklus.

Windows používa HDRP 17.6, WebGL Built-in Forward. Spoločná hrateľnosť preto neznamená identický renderer, osvetlenie ani natívne DXR. Voliteľných približne 52 natívnych dekorácií Synty webový export neobsahuje. Platené Odin/POLYGON zdrojové balíky sa do verejného exportu nekopírujú. OG verzia zostáva pod `/classic`.

Vlastné mapy materiálov majú zdrojový rozmer 4K. Web štandardne volí 2K detail; úsporný mobilný profil môže zvoliť 1K a obmedzuje hustotu pixelov a tiene. Skutočný výkon treba merať na pomenovanom zariadení. Animované exponáty a pety rešpektujú fotorežim a obmedzenie pohyblivých dekorácií.

Pred odovzdaním over rovnaký pohľad v oboch playeroch, čitateľnosť textu a vzorcov, všetkých desať petov, dotyk viacerými prstami, zmenu orientácie, priechodnosť 22 staníc a overené trasy. Snímky musia označovať platformu a zostavenie. Úplný výtvarný návrh pre tím 3 je v `work/LUMEN-DOCUMENTATION/drafts/ADD.md` pracovného priestoru.
