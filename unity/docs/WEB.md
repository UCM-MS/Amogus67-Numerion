# Unity v prehliadači

Webová verzia je Unity WebGL 2 / IL2CPP player vytvorený z rovnakých C# herných zdrojov ako Windows aplikácia. Zdieľané sú matematika pre 7.–9. ročník ZŠ, hub, pohyb, postava, menu, tutoriál, desať spoločníkov, otázkami podporované ťahové súboje a artefakty. Počet otázok a petov export preberá z aktuálneho katalógu.

`python Tools/export_web_project.py` pripraví samostatný projekt `../lumen-unity-web`. Natívny projekt používa HDRP 17.6, export Built-in Forward so slnečnými tieňmi, hmlou a normálovými mapami. Zdrojové mapy zostávajú do 4096 px. Predvolená webová kvalita 2K používa nižšiu úroveň mipmapy; v menu Grafika možno zvoliť 4K. Menšiu zdrojovú textúru toto nastavenie nezväčší. Voľba 4K mení detail textúr, rozlíšenie obrazu sleduje veľkosť okna a hustotu pixelov displeja.

Prehliadač plánuje snímky podľa displeja, bez pevného limitu 60 FPS. Výslednú frekvenciu a VRR ovplyvňujú prehliadač, výkon, monitor a systém. Natívne limity FPS a VSync uložené v spoločnom profile web neprepisuje. DXR, path tracing a PC OpenXR zostávajú natívnymi možnosťami. Prídavné platené dekorácie Synty nie sú súčasťou webového exportu; prvky hubu vytvorené našou geometriou sú spoločné. Preto rovnaká hrateľnosť neznamená identický renderer a každý dekoratívny model.

Použi nainštalovaný Unity 6000.6.6f1 s modulom Web Build Support:

```powershell
Unity.exe -batchmode -nographics -quit -projectPath <web-project> -buildTarget WebGL -executeMethod Lumen.Editor.LumenWebBuilder.Build -logFile <log>
```

Výstup je `Builds/WebGL`. Gzip súbory používajú dekompresiu v Unity loaderi. Verejná stránka servíruje výstup pod `/unity/Build/`; hlavná adresa aj `/unity/index.html` otvárajú rovnaký player. Spusti cez HTTP(S), nie priamo z disku. Po každej zmene spoločných herných zdrojov znovu priprav projekt, až potom zostav web. `WEB-PROJECTION.json` eviduje verziu enginu, nastavenia exportu a odtlačky zdrojov.

Pohyb: WASD/šípky, medzerník, pravé tlačidlo myši na kameru, E na interakciu. Na dotykovom zariadení sa dajú zapnúť virtuálne páčky, skok a interakcia. Menu a odpovede sa ovládajú priamo. Pre menší displej je vhodná orientácia na šírku.

V menu **Hrať spolu** používajú Windows a Unity Web rovnaké HTTPS miestnosti, spoločné objavovanie a serverom vyhodnocované pet súboje. Obe zariadenia musia používať rovnakú adresu online služby. Priamy UDP/LAN režim prehliadač nepodporuje; jeho stav odkazuje na spoločné online menu. Export zachováva `LumenHud.Online.cs` aj `OnlineAcademyClient.cs`, nenahrádza ich starou sólo obrazovkou. Obnovovanie pozícií na serveri je nezávislé od snímkovej frekvencie.

Lokálny postup používa PlayerPrefs/IndexedDB s priebežným ukladaním a zálohou. Obsahuje aj artefakty, pet zostavu a tréningový súboj. Vymazanie dát stránky môže odstrániť lokálny postup. V menu **Spoločný profil a prenos postupu** možno vytvoriť cloudový profil a súkromným prenosovým kódom načítať ten istý postup na druhom zariadení. Pri načítaní sa najprv zálohuje lokálny profil. Cloudové revízie chránia pred tichým prepísaním novšieho postupu; konfliktnú verziu treba najprv načítať. Kód poskytuje prístup k profilu, preto sa zobrazuje až na vyžiadanie.

OG hra zostáva samostatne pod `/classic`. Export zahŕňa iba vlastné herné zdroje a povolené vlastné assety; platené Odin/POLYGON zdrojové balíky, ich modely a surové textúry sa nekopírujú. Overenie samotného exportu nenahrádza zostavenie a vizuálny test nového WebGL playera.
