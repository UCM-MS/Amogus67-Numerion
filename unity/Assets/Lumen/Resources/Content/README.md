# Učivo a spoločníci

- `curriculum.json` obsahuje 108 otázok. Na každý ročník od 7. po 9. pripadá 36 otázok rozdelených do šiestich tém.
- `lessons.json` obsahuje 18 krátkych kapitol s riešenými príkladmi.
- `pets.json` obsahuje katalóg desiatich spoločníkov a ich matematické zameranie.

Jadro Unity používa otázky z `QuestionBank.cs` a kapitoly z `LessonCatalog.cs`. Po úprave obsahu spusti z koreňa repozitára generátor a synchronizuj banku pre webový server.

~~~powershell
python unity/Tests/MathHarness/generate_content.py
node web/scripts/sync-unity-curriculum.mjs
~~~

Klasický prototyp v prehliadači má vlastnú staršiu banku v `web/public/curriculum.js`.
