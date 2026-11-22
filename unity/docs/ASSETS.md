# Lokálne licencované modely

Projekt používa používateľom importovaný **POLYGON Sampler Pack / PolygonStarter** od Synty Studios v `Assets/SyntyStudios/PolygonStarter`. Tento dostupný balík obsahuje stromy, skaly, vozidlá a ukážkové rekvizity; nejde o plnú knižnicu všetkých POLYGON balíkov.

`Lumen.Editor.SyntyAcademySetup.Configure()` pripraví `Resources/Generated/SyntyDecor.prefab` s 36 dekoratívnymi modelmi pre šesť ostrovov a spoločnú záhradu. Vytvorí vlastné kópie materiálov pre HDRP/Lit a zachová pôvodné textúry a modely bez zmien dodávateľských súborov. Veda má expozíciu aerodynamiky s lietadlom; technológia model automobilu; história historický štít. Stromy a skaly dopĺňajú okraje ostrovov.

Dekorácie nemajú kolidery ani herné skripty. Pri headless serveri sa nenačítavajú, takže autoritatívna fyzika zostáva totožná so samostatným hraním. Ak balík chýba, príprava ho preskočí a hra používa vlastný procedurálny svet.

Licencované modely, textúry, odvodené materiály a prefab s referenciami ponechaj v súkromnom lokálnom projekte. Nepridávaj ich do verejného repozitára ani verejnej zdrojovej zálohy. Na inom počítači ich vlastník importuje cez svoj Unity Asset Store účet. Pravidlá použitia určuje licencia zakúpeného balíka a Unity Asset Store EULA; tento súbor ju nenahrádza.
