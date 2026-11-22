"""Original LUMEN mathematics tasks; source authoring + independent numeric proof checks.

Only the thematic scope comes from the linked SPU standard. Prompts and worked
solutions are original. Grade assignments include deliberate revision topics.
"""
from pathlib import Path
from fractions import Fraction
import ast
import json
import hashlib
import math
import operator
import re

ROOT = Path(__file__).resolve().parents[2]
SOURCE = 'https://www.statpedu.sk/files/articles/dokumenty/inovovany-statny-vzdelavaci-program/matematika_nsv_2014.pdf'
STRANDS = [
    ('Čísla a zlomky', 'Zlomky, celé čísla, racionálne čísla, mocniny a odmocniny.'),
    ('Percentá a pomer', 'Percentá, pomer, mierka, priama a nepriama úmernosť.'),
    ('Výrazy a rovnice', 'Poradie operácií, výrazy s premennou, lineárne rovnice a nerovnice.'),
    ('Geometria', 'Obvody, obsahy, objemy, Pytagorova veta a priestorová predstavivosť.'),
    ('Dáta a pravdepodobnosť', 'Kombinatorika, priemer, pravdepodobnosť, štatistika a grafy.'),
    ('Matematika v praxi', 'Slovné úlohy, jednotky, rozpočty a matematické rozhodovanie v živote.'),
]
ROWS = []

def q(grade, strand, prompt, correct, distractors, explanation, expression=None):
    index = 1 + sum(r['Grade'] == grade and r['Subject'] == strand for r in ROWS)
    position = (index - 1 + strand * 6 + grade) % 4
    options = list(map(str, distractors))
    options.insert(position, str(correct))
    row = dict(Id=f'math-g{grade}-s{strand}-{index:02}', Grade=grade,
               Subject=strand, AgeBand=2, Difficulty=[1, 2, 2, 3, 3, 4, 3, 4][index-1],
               SectionId=f'section-{strand}', Prompt=prompt, Options=options,
               CorrectOption=position, Explanation=explanation, SourceUrl=SOURCE)
    if expression:
        row['MathProof'] = expression
    ROWS.append(row)

# Grade 7: fractions, percentages, proportional reasoning, arithmetic,
# cuboids/cubes, systematic counting and everyday applications.
q(7,0,'Iskra prešla 1/4 trasy ráno a 1/2 trasy popoludní. Akú časť trasy prešla spolu?', '3/4',['2/6','1/8','1/4'],'Prevedieme 1/2 na 2/4. Potom 1/4 + 2/4 = 3/4 trasy.', '1/4+1/2')
q(7,0,'V zásobníku bolo 3/5 litra vody. Volt odobral 1/10 litra. Koľko litra zostalo?', '1/2',['2/5','1/10','7/10'],'Spoločný menovateľ je 10: 3/5 = 6/10. Odčítame 6/10 − 1/10 = 5/10 = 1/2 litra.', '3/5-1/10')
q(7,0,'Koľko je 2/3 z 9/10 kilogramu krmiva?', '3/5',['11/13','2/5','3/10'],'Násobíme čitatele aj menovatele: (2 × 9)/(3 × 10) = 18/30. Po skrátení šiestimi je výsledok 3/5 kg.', '(2/3)*(9/10)')
q(7,0,'Vypočítaj podiel 7/8 : 7/12.', '3/2',['1/2','7/20','2/3'],'Delenie zlomkom zmeníme na násobenie jeho prevrátenou hodnotou: 7/8 × 12/7 = 12/8 = 3/2.', '(7/8)/(7/12)')
q(7,0,'Aký nepravý zlomok zodpovedá zmiešanému číslu 2 a 1/4?', '9/4',['3/4','8/4','5/4'],'Dve celé sú 8/4. Po pridaní 1/4 dostaneme 9/4; rovnako (2 × 4 + 1)/4.', '2+1/4')
q(7,0,'Zapíš desatinné číslo 0,375 ako zlomok v základnom tvare.', '3/8',['3/5','5/8','37/100'],'0,375 = 375/1000. Čitateľa aj menovateľa vydelíme 125 a dostaneme 3/8.', '375/1000')

q(7,1,'Z 240 kryštálov si Astra odloží 25 %. Koľko kryštálov si odloží?', '60',['25','48','80'],'25 % je jedna štvrtina. Vypočítame 240 : 4 = 60 kryštálov.', '240*25/100')
q(7,1,'Batoh stál 150 mincí. Po zlacnení o 20 % stojí koľko mincí?', '120',['130','30','180'],'Zľava je 0,20 × 150 = 30 mincí. Nová cena je 150 − 30 = 120 mincí.', '150*(1-20/100)')
q(7,1,'V zbierke je 24 červených a 16 modrých kameňov. Aký je pomer červených k modrým v základnom tvare?', '3 : 2',['2 : 3','4 : 3','3 : 5'],'Obe časti pomeru 24 : 16 vydelíme číslom 8. Výsledok je 3 : 2; na poradí farieb záleží.')
q(7,1,'Rozdeľ 50 mincí v pomere 2 : 3. Koľko mincí dostane menší diel?', '20',['25','30','10'],'Spolu je 2 + 3 = 5 dielov. Jeden diel je 50 : 5 = 10 a menšia časť má 2 × 10 = 20 mincí.', '50*2/(2+3)')
q(7,1,'Na mape s mierkou 1 : 25 000 meria chodník 4 cm. Koľko kilometrov meria v skutočnosti?', '1',['0,1','10','100'],'Skutočná dĺžka je 4 × 25 000 = 100 000 cm. Keďže 100 000 cm = 1 km, chodník meria 1 km.', '4*25000/100000')
q(7,1,'Štyri rovnaké čerpadlá naplnia nádrž za 6 hodín. Za koľko hodín ju naplní šesť takých čerpadiel?', '4',['9','6','3'],'Ide o nepriamu úmernosť: potrebujeme 4 × 6 = 24 čerpadlohodín. Šesť čerpadiel bude pracovať 24 : 6 = 4 hodiny.', '4*6/6')

q(7,2,'Akú hodnotu má výraz (18 − 6) : 3 + 2?', '6',['4','8','2'],'Najprv zátvorka: 18 − 6 = 12. Potom delenie 12 : 3 = 4 a nakoniec sčítanie 4 + 2 = 6.', '(18-6)/3+2')
q(7,2,'Tri rovnaké balíčky obsahujú spolu 42 semienok. Koľko je v jednom balíčku?', '14',['12','21','39'],'Celkový počet rozdelíme na tri rovnaké časti: 42 : 3 = 14 semienok v balíčku.', '42/3')
q(7,2,'Neznáme číslo vynásobíme piatimi a pripočítame 8. Dostaneme 43. Ktoré číslo sme použili?', '7',['5','9','11'],'Riešime odzadu: od 43 odčítame 8, dostaneme 35. Následne 35 : 5 = 7. Skúška: 5 × 7 + 8 = 43.', '(43-8)/5')
q(7,2,'Vypočítaj 4 × (9 − 3) − 2 × 5.', '14',['110','20','24'],'V zátvorke je 6. Násobenia dajú 4 × 6 = 24 a 2 × 5 = 10. Rozdiel 24 − 10 = 14.', '4*(9-3)-2*5')
q(7,2,'Doplň chýbajúce číslo: 3/4 + ? = 5/4.', '1/2',['2','1/4','3/2'],'Od celkového súčtu odčítame známu časť: 5/4 − 3/4 = 2/4 = 1/2.', '5/4-3/4')
q(7,2,'Na dvoch misiach získaš rovnako mincí. Po pridaní bonusu 15 máš 71 mincí. Koľko dala jedna misia?', '28',['43','56','35'],'Bez bonusu zostáva 71 − 15 = 56 mincí. Dve rovnaké misie priniesli každá 56 : 2 = 28 mincí.', '(71-15)/2')

q(7,3,'Kváder má rozmery 4 dm, 3 dm a 2 dm. Aký je jeho objem v dm³?', '24',['9','18','48'],'Objem kvádra je súčin dĺžky, šírky a výšky: V = 4 × 3 × 2 = 24 dm³.', '4*3*2')
q(7,3,'Kocka má hranu 4 cm. Aký je jej povrch v cm²?', '96',['16','64','48'],'Jedna stena má obsah 4 × 4 = 16 cm². Kocka má šesť stien, preto S = 6 × 16 = 96 cm².', '6*4*4')
q(7,3,'Koľko dm³ predstavuje 2 500 cm³?', '2,5',['25','0,25','250'],'Platí 1 dm³ = 1 000 cm³. Vydelíme 2 500 : 1 000 = 2,5 dm³.', '2500/1000')
q(7,3,'Kváder má hrany 6 cm, 3 cm a 2 cm. Aký je jeho povrch v cm²?', '72',['36','66','108'],'Povrch je 2(ab + ac + bc). Dosadíme 2(6 × 3 + 6 × 2 + 3 × 2) = 2(18 + 12 + 6) = 72 cm².', '2*(6*3+6*2+3*2)')
q(7,3,'Štvorcový výbeh má obvod 36 m. Aký má obsah v m²?', '81',['9','144','72'],'Obvod štvorca je 4a, preto a = 36 : 4 = 9 m. Obsah je a² = 9 × 9 = 81 m².', '(36/4)*(36/4)')
q(7,3,'Stavba pozostáva z troch kociek s hranou 2 dm, bez prekrývania. Aký je jej objem v dm³?', '24',['12','18','8'],'Jedna kocka má objem 2³ = 8 dm³. Tri kocky spolu majú 3 × 8 = 24 dm³; ich usporiadanie objem nemení.', '3*2*2*2')

q(7,4,'Máš tri rôzne tričká a dvoje rôzne nohavice. Koľko rôznych dvojíc oblečenia môžeš vytvoriť?', '6',['5','3','9'],'Ku každému z troch tričiek patria dve možnosti nohavíc. Použijeme pravidlo súčinu: 3 × 2 = 6.', '3*2')
q(7,4,'Koľkými spôsobmi sa môžu Iskra, Volt a Astra postaviť do radu, každý práve raz?', '6',['3','9','12'],'Na prvé miesto sú tri možnosti, na druhé dve a na posledné jedna. Spolu 3 × 2 × 1 = 6 poradí.', '3*2*1')
q(7,4,'Dvojmiestny kód tvoríš z číslic 1, 2 a 3. Číslice sa môžu opakovať. Koľko kódov existuje?', '9',['6','3','12'],'Pre prvé miesto sú tri možnosti a pre druhé tiež tri. Vypočítame 3 × 3 = 9, vrátane kódov 11, 22 a 33.', '3*3')
q(7,4,'Zo štyroch petov vyberáš dvojicu. Na poradí nezáleží. Koľko rôznych dvojíc môžeš vybrať?', '6',['12','8','4'],'Usporiadaných výberov je 4 × 3 = 12. Každú dvojicu sme spočítali dvakrát, preto 12 : 2 = 6.', '4*3/2')
q(7,4,'V tabuľke je 6 červených a 4 modré kryštály. Akú časť všetkých záznamov tvoria červené?', '3/5',['2/5','3/2','1/2'],'Spolu je 6 + 4 = 10 kryštálov. Červené tvoria 6/10 = 3/5 všetkých záznamov.', '6/(6+4)')
q(7,4,'Z písmen A, B a C tvoríš dvojpísmenové značky bez opakovania. Koľko značiek môžeš vytvoriť?', '6',['3','9','12'],'Prvé písmeno vyberieme tromi spôsobmi. Po jeho použití ostávajú dve možnosti, teda 3 × 2 = 6 značiek.', '3*2')

q(7,5,'Výprava meria 2,5 km. Koľko metrov prejdeš?', '2500',['250','25','25000'],'Jeden kilometer má 1 000 metrov. Násobíme 2,5 × 1 000 = 2 500 m.', '2.5*1000')
q(7,5,'V troch fľašiach po 1 liter a jednej po 0,5 litra je spolu koľko litrov?', '3,5',['3,05','4','0,35'],'Tri litrové fľaše obsahujú 3 l. Pripočítame 0,5 l a dostaneme 3,5 l.', '3*1+0.5')
q(7,5,'Kúpiš tri balenia krmiva po 2,40 € a hračku za 1,80 €. Koľko zaplatíš v eurách?', '9',['7,2','8,4','10,8'],'Krmivo stojí 3 × 2,40 = 7,20 €. Po pridaní hračky 7,20 + 1,80 = 9,00 €.', '3*2.4+1.8')
q(7,5,'Vozidlo prejde 180 km za 2,5 hodiny rovnomernou rýchlosťou. Aká je rýchlosť v km/h?', '72',['75','45','90'],'Rýchlosť vypočítame ako dráhu delenú časom: v = 180 : 2,5 = 72 km/h.', '180/2.5')
q(7,5,'Obdĺžnikový výbeh 12 m × 8 m oplotíš, ale necháš otvor široký 2 m. Koľko metrov plota potrebuješ?', '38',['40','96','36'],'Obvod je 2(12 + 8) = 40 m. Odčítame dvojmetrový otvor: 40 − 2 = 38 m plota.', '2*(12+8)-2')
q(7,5,'Na štyri porcie krmiva potrebuješ 300 g zmesi. Koľko gramov potrebuješ na desať rovnakých porcií?', '750',['600','1200','450'],'Jedna porcia má 300 : 4 = 75 g. Na desať porcií treba 75 × 10 = 750 g.', '300/4*10')

# Grade 8: signed numbers, continued proportional reasoning, variables,
# planar figures/circles/prisms, probability and statistical summaries.
q(8,0,'Vypočítaj −7 + 12.', '5',['−5','19','−19'],'Od čísla −7 sa posunieme o 12 doprava po číselnej osi. Dostaneme číslo 5.', '-7+12')
q(8,0,'Koľko je (−4) × (−6)?', '24',['−24','10','−10'],'Súčin dvoch záporných čísel je kladný. Vypočítame 4 × 6 = 24.', '(-4)*(-6)')
q(8,0,'Vypočítaj −18 : 3 + 5.', '−1',['1','−11','−6'],'Najprv delenie: −18 : 3 = −6. Potom −6 + 5 = −1.', '-18/3+5')
q(8,0,'Ktoré číslo je opačné k číslu −2,5?', '2,5',['−2,5','0,4','−0,4'],'Opačné čísla majú súčet nula. K číslu −2,5 je preto opačné číslo 2,5.', '-(-2.5)')
q(8,0,'Ráno bolo −8 °C. Teplota stúpla o 13 °C a večer klesla o 6 °C. Koľko °C bolo večer?', '−1',['1','−27','11'],'Zmeny zapíšeme ako −8 + 13 − 6. Najprv je 5 °C, po poklese o 6 °C je −1 °C.', '-8+13-6')
q(8,0,'Vypočítaj −3/4 + 5/8.', '−1/8',['1/8','−1/4','1/4'],'Prevedieme −3/4 na −6/8. Sčítaním −6/8 + 5/8 dostaneme −1/8.', '-3/4+5/8')

q(8,1,'Cena 100 mincí sa zvýši o 10 % a potom zníži o 10 % z novej ceny. Aká je konečná cena?', '99',['100','90','101'],'Po zdražení je cena 110. Desať percent zo 110 je 11, preto nová cena je 110 − 11 = 99 mincí.', '100*1.1*0.9')
q(8,1,'Cena vrátane 20 % dane je 60 €. Aká je cena bez dane v eurách?', '50',['48','40','72'],'Cena s daňou predstavuje 120 % základu. Základ vypočítame 60 : 1,20 = 50 €.', '60/1.2')
q(8,1,'Tri diely zmesi vážia 18 g. Koľko gramov váži päť takých dielov?', '30',['25','15','108'],'Jeden diel váži 18 : 3 = 6 g. Päť dielov váži 5 × 6 = 30 g.', '18/3*5')
q(8,1,'Priama úmernosť má predpis y = 2,5x. Aké je y pri x = 8?', '20',['10,5','3,2','16'],'Do predpisu dosadíme x = 8: y = 2,5 × 8 = 20.', '2.5*8')
q(8,1,'Dvaja rovnako výkonní stavitelia dokončia úlohu za 8 hodín. Koľko hodín potrebujú štyria pri rovnakom tempe?', '4',['16','8','2'],'Práca predstavuje 2 × 8 = 16 človekohodín. Pri štyroch staviteľoch trvá 16 : 4 = 4 hodiny.', '2*8/4')
q(8,1,'Skutočná dĺžka je 50 m. Koľko centimetrov meria na pláne s mierkou 1 : 1 000?', '5',['50','0,5','500'],'Najprv 50 m = 5 000 cm. Na pláne je dĺžka 5 000 : 1 000 = 5 cm.', '50*100/1000')

q(8,2,'Vypočítaj hodnotu výrazu 3x + 2x − 4 pre x = 3.', '11',['9','15','1'],'Spojíme rovnaké členy: 3x + 2x = 5x. Pre x = 3 dostaneme 5 × 3 − 4 = 11.', '3*3+2*3-4')
q(8,2,'Akú hodnotu má výraz 2(x + 3) pre x = 4?', '14',['11','10','8'],'Najprv dosadíme: 2(4 + 3). Zátvorka má hodnotu 7, preto výsledok je 2 × 7 = 14.', '2*(4+3)')
q(8,2,'Ktorý výraz vyjadruje trojnásobok čísla x zväčšený o 2?', '3x + 2',['3(x + 2)','x + 6','2x + 3'],'Trojnásobok x zapíšeme ako 3x. Zväčšenie o 2 znamená pripočítať 2, takže dostaneme 3x + 2.')
q(8,2,'Uprav výraz 6a − 2a + 5.', '4a + 5',['4a + 3','8a + 5','9a'],'Sčítavať možno členy s rovnakou premennou: 6a − 2a = 4a. Konštanta 5 zostáva samostatne, výsledok je 4a + 5.')
q(8,2,'Za vstup zaplatíš 5 € a za každú jazdu 3 €. Tri jazdy aj vstup spolu stoja koľko eur?', '14',['15','24','9'],'Celkovú cenu vyjadruje 5 + 3n. Pre n = 3 je to 5 + 3 × 3 = 14 €.', '5+3*3')
q(8,2,'Volt si myslí číslo. Po odčítaní 7 a vydelení výsledku tromi dostane 5. Aké číslo si myslí?', '22',['8','12','36'],'Použijeme opačné operácie v opačnom poradí: 5 × 3 = 15 a 15 + 7 = 22. Skúška: (22 − 7) : 3 = 5.', '5*3+7')

q(8,3,'Rovnobežník má stranu 8 cm a príslušnú výšku 5 cm. Aký je jeho obsah v cm²?', '40',['26','20','80'],'Obsah rovnobežníka je základňa krát príslušná výška: S = 8 × 5 = 40 cm².', '8*5')
q(8,3,'Trojuholník má základňu 14 cm a príslušnú výšku 6 cm. Aký má obsah v cm²?', '42',['84','20','40'],'Obsah trojuholníka je polovica súčinu základne a výšky: S = 14 × 6 : 2 = 42 cm².', '14*6/2')
q(8,3,'Lichobežník má základne 8 cm a 14 cm, výšku 5 cm. Aký má obsah v cm²?', '55',['110','35','70'],'Použijeme S = (a + c)v/2. Dosadíme (8 + 14) × 5 : 2 = 22 × 5 : 2 = 55 cm².', '(8+14)*5/2')
q(8,3,'Kruh má polomer 5 cm. Použi π = 3,14. Aký má obsah v cm²?', '78,5',['31,4','15,7','157'],'Obsah kruhu je S = πr². Dosadíme 3,14 × 5² = 3,14 × 25 = 78,5 cm².', '3.14*5*5')
q(8,3,'Kružnica má polomer 4 m. Použi π = 3,14. Aká je jej dĺžka v metroch?', '25,12',['12,56','50,24','16'],'Dĺžka kružnice je o = 2πr. Teda 2 × 3,14 × 4 = 25,12 m.', '2*3.14*4')
q(8,3,'Hranol má obsah podstavy 12 dm² a výšku 7 dm. Aký je jeho objem v dm³?', '84',['19','42','168'],'Objem hranola je obsah podstavy krát výška: V = 12 × 7 = 84 dm³.', '12*7')

q(8,4,'Za štyri kolá si získal 4, 6, 8 a 10 bodov. Aký je aritmetický priemer?', '7',['6','8','28'],'Súčet bodov je 4 + 6 + 8 + 10 = 28. Delíme počtom štyroch kôl: 28 : 4 = 7.', '(4+6+8+10)/4')
q(8,4,'Vo vrecku sú 3 červené a 5 modrých guličiek. Aká je pravdepodobnosť vytiahnutia červenej pri rovnakých šanciach guličiek?', '3/8',['3/5','5/8','1/3'],'Spolu je 8 rovnako možných guličiek a červené sú 3. Pravdepodobnosť je 3/8.', '3/(3+5)')
q(8,4,'Aká je pravdepodobnosť párneho čísla na spravodlivej šesťstennej kocke?', '1/2',['1/6','2/3','1/3'],'Párne výsledky sú 2, 4 a 6: tri zo šiestich možností. Podiel 3/6 zjednodušíme na 1/2.', '3/6')
q(8,4,'Hodíme dvoma nezávislými spravodlivými mincami. Aká je pravdepodobnosť dvoch hláv?', '1/4',['1/2','3/4','1/3'],'Štyri rovnako pravdepodobné možnosti sú HH, HZ, ZH a ZZ. Dve hlavy má práve jedna, preto 1/4.', '1/2*1/2')
q(8,4,'Z 30 odpovedí bolo 12 správnych. Koľko percent odpovedí bolo správnych?', '40',['12','30','60'],'Podiel správnych odpovedí je 12/30 = 0,4. Na percentá ho prevedieme násobením 100: 40 %.', '12/30*100')
q(8,4,'V súbore bodov 2, 7, 4, 9, 5 urč rozdiel najväčšej a najmenšej hodnoty.', '7',['5','9','11'],'Najväčší počet bodov je 9 a najmenší 2. Ich rozdiel je 9 − 2 = 7 bodov.', '9-2')

q(8,5,'Z rozpočtu 70 € minieš 15 € a potom 2 balenia po 12 €. Koľko eur ti zostane?', '31',['43','19','37'],'Najprv dve balenia stoja 2 × 12 = 24 €. Celkové výdavky sú 15 + 24 = 39 €, zvyšok 70 − 39 = 31 €.', '70-15-2*12')
q(8,5,'Vlak ide rovnomerne 72 km/h. Koľko kilometrov prejde za 25 minút?', '30',['25','36','1800'],'Čas 25 minút je 25/60 hodiny. Dráha s = vt = 72 × 25/60 = 30 km.', '72*25/60')
q(8,5,'Koľko cm² je 2,4 m²?', '24000',['240','2400','240000'],'Keďže 1 m = 100 cm, platí 1 m² = 10 000 cm². Teda 2,4 × 10 000 = 24 000 cm².', '2.4*10000')
q(8,5,'Chodník je na pláne s mierkou 1 : 500 dlhý 7,2 cm. Aká je skutočná dĺžka v metroch?', '36',['3,6','360','72'],'Skutočná dĺžka je 7,2 × 500 = 3 600 cm. Po delení 100 získame 36 m.', '7.2*500/100')
q(8,5,'Trojuholníkový záhon má obsah 36 m² a základňu 9 m. Aká je výška na túto základňu v metroch?', '8',['4','12','6'],'Zo vzorca S = av/2 vyjadríme v = 2S/a. Dosadíme 2 × 36 : 9 = 8 m.', '2*36/9')
q(8,5,'Za päť rovnakých kníh zaplatíš 42,50 €. Koľko stojí osem takých kníh v eurách?', '68',['65','72','76'],'Jedna kniha stojí 42,50 : 5 = 8,50 €. Osem kníh stojí 8 × 8,50 = 68 €.', '42.5/5*8')

# Grade 9: powers and roots, linear reasoning, Pythagoras and solids,
# statistical investigations and practical multistep problems.
q(9,0,'Urči nezápornú druhú odmocninu z čísla 225.', '15',['25','14','112,5'],'Hľadáme nezáporné číslo, ktorého druhá mocnina je 225. Platí 15 × 15 = 225, teda √225 = 15.', '225**0.5')
q(9,0,'Vypočítaj 2⁵ × 2³.', '256',['64','128','1024'],'Pri násobení mocnín s rovnakým základom sčítame exponenty: 2⁵ × 2³ = 2⁸ = 256.', '2**5*2**3')
q(9,0,'Akú desatinnú hodnotu má 10⁻³?', '0,001',['0,003','−1000','0,01'],'Záporný exponent znamená prevrátenú hodnotu: 10⁻³ = 1/10³ = 1/1000 = 0,001.', '10**(-3)')
q(9,0,'Vypočítaj (3 × 10⁴) × (2 × 10³).', '60000000',['6000000','600000000','50000000'],'Násobíme koeficienty 3 × 2 = 6 a mocniny 10⁴ × 10³ = 10⁷. Výsledok je 6 × 10⁷ = 60 000 000.', '(3*10**4)*(2*10**3)')
q(9,0,'Vypočítaj √(81 + 144).', '15',['21','225','12'],'Najprv spočítame pod odmocninou: 81 + 144 = 225. Až potom odmocníme: √225 = 15.', '(81+144)**0.5')
q(9,0,'Vypočítaj (3/2)² − 1/4.', '2',['1','5/2','1/2'],'Druhá mocnina zlomku je 9/4. Odčítame 1/4 a dostaneme 8/4 = 2.', '(3/2)**2-1/4')

q(9,1,'Cena 80 mincí sa zvýši o 25 % a následne zníži o 20 % z novej ceny. Aká je konečná cena?', '80',['84','76','100'],'Po zdražení je 80 × 1,25 = 100 mincí. Po zlacnení o 20 % je 100 × 0,8 = 80 mincí.', '80*1.25*0.8')
q(9,1,'Dĺžky podobných útvarov sa zväčšia v pomere 3 : 2. Akým číslom sa násobí ich obsah?', '9/4',['3/2','2/3','3'],'Obsah sa mení s druhou mocninou koeficientu podobnosti. Platí (3/2)² = 9/4.', '(3/2)**2')
q(9,1,'Nepriama úmernosť má predpis y = 48/x. Aké je y pre x = 6?', '8',['42','288','6'],'Do predpisu dosadíme x = 6: y = 48 : 6 = 8. Súčin x × y je stále 48.', '48/6')
q(9,1,'Vklad 1 200 € má jednoduchý úrok 4 % ročne. Aký je úrok za dva roky bez úročenia úrokov?', '96',['48','99,84','192'],'Ročný úrok je 1 200 × 0,04 = 48 €. Pri jednoduchom úročení za dva roky získame 2 × 48 = 96 €.', '1200*4/100*2')
q(9,1,'Po zľave 30 % stojí výbava 84 €. Aká bola pôvodná cena v eurách?', '120',['109,2','114','100'],'Po zľave zostáva 70 % pôvodnej ceny. Hľadáme základ: 84 : 0,70 = 120 €.', '84/(1-30/100)')
q(9,1,'Stĺpik vysoký 1,8 m vrhá tieň 2,4 m. Strom v rovnakom čase vrhá tieň 12 m. Aká je výška stromu v metroch?', '9',['16','10,2','6'],'Trojuholníky výšky a tieňa sú podobné. Výška stromu je 12 × 1,8 : 2,4 = 9 m.', '12*1.8/2.4')

q(9,2,'Vyrieš rovnicu 2(3x − 2) = 20.', '4',['3','6','8'],'Roznásobíme: 6x − 4 = 20. Pripočítame 4 a delíme 6: x = 24/6 = 4. Skúška: 2(12 − 2) = 20.', '(20+4)/6')
q(9,2,'Vyrieš rovnicu 5x − 7 = 2x + 11.', '6',['4','18','−6'],'Odčítame 2x a pripočítame 7: 3x = 18. Vydelíme tromi, teda x = 6. Obe strany potom majú hodnotu 23.', '(11+7)/(5-2)')
q(9,2,'Vyrieš rovnicu x/3 + 2 = 7.', '15',['3','9','27'],'Najprv odčítame 2: x/3 = 5. Potom obe strany násobíme tromi: x = 15.', '(7-2)*3')
q(9,2,'Ktorý zápis vystihuje všetky riešenia nerovnice 3x − 2 < 10?', 'x < 4',['x > 4','x < 12','x = 4'],'Pripočítame 2: 3x < 12. Delíme kladným číslom 3, preto sa znak nemení: x < 4.')
q(9,2,'Dva vedľajšie uhly majú veľkosti (2x + 15)° a (3x − 10)°. Urči x.', '35',['36','31','37'],'Vedľajšie uhly majú súčet 180°. Preto 2x + 15 + 3x − 10 = 180, čiže 5x + 5 = 180 a x = 35.', '(180-15+10)/(2+3)')
q(9,2,'Akú hodnotu má y = −2x + 5 pre x = −3?', '11',['−1','1','−11'],'Dosadíme záporné číslo aj so zátvorkou: y = −2 × (−3) + 5 = 6 + 5 = 11.', '-2*(-3)+5')

q(9,3,'Pravouhlý trojuholník má odvesny 3 cm a 4 cm. Aká je prepona v centimetroch?', '5',['7','1','25'],'Pytagorova veta dáva c² = 3² + 4² = 9 + 16 = 25. Kladná dĺžka c je √25 = 5 cm.', '(3**2+4**2)**0.5')
q(9,3,'Rebrík dlhý 13 m siaha na stene do výšky 12 m. Koľko metrov je jeho päta od steny na rovnej zemi?', '5',['1','7','25'],'Rebrík je prepona. Vzdialenosť od steny a spĺňa a² = 13² − 12² = 169 − 144 = 25, teda a = 5 m.', '(13**2-12**2)**0.5')
q(9,3,'Obdĺžnik má uhlopriečku 10 cm a jednu stranu 6 cm. Aká je druhá strana v centimetroch?', '8',['4','16','64'],'Uhlopriečka tvorí preponu pravouhlého trojuholníka. Druhá strana je √(10² − 6²) = √64 = 8 cm.', '(10**2-6**2)**0.5')
q(9,3,'Valec má polomer 3 dm a výšku 10 dm. Použi π = 3,14. Aký je objem v dm³?', '282,6',['94,2','188,4','28,26'],'Objem valca je V = πr²v. Dosadíme 3,14 × 3² × 10 = 282,6 dm³.', '3.14*3*3*10')
q(9,3,'Kužeľ má obsah podstavy 12 cm² a výšku 9 cm. Aký je jeho objem v cm³?', '36',['108','54','27'],'Objem kužeľa je tretina súčinu obsahu podstavy a výšky: V = 12 × 9 : 3 = 36 cm³.', '12*9/3')
q(9,3,'Guľa má polomer 3 cm. Použi π = 3,14. Aký je jej objem v cm³?', '113,04',['37,68','84,78','339,12'],'Vzorec je V = 4πr³/3. Dosadíme 4 × 3,14 × 27 : 3 = 113,04 cm³.', '4*3.14*3**3/3')

q(9,4,'Priemer štyroch výsledkov 6, 8, x a 10 je 9. Urči x.', '12',['9','10','14'],'Súčet štyroch čísel musí byť 4 × 9 = 36. Známe hodnoty majú súčet 24, preto x = 36 − 24 = 12.', '4*9-6-8-10')
q(9,4,'Usporiadané výsledky sú 3, 8, 8, 12, 14. Ktorá je prostredná hodnota, teda medián?', '8',['9','12','3'],'Pri piatich usporiadaných hodnotách je medián tretí prvok. V tomto súbore je to číslo 8.')
q(9,4,'Hodíme dvoma nezávislými spravodlivými kockami. Aká je pravdepodobnosť súčtu 7?', '1/6',['1/12','1/7','1/3'],'Spolu je 6 × 6 = 36 usporiadaných možností. Súčet 7 tvoria dvojice (1,6), (2,5), (3,4), (4,3), (5,2), (6,1), teda 6/36 = 1/6.', '6/36')
q(9,4,'Tabuľka závislosti obsahuje dvojice (1; 4), (2; 8), (3; 12). Aká je hodnota y/x v každom riadku?', '4',['3','8','12'],'Vypočítame 4/1 = 4, 8/2 = 4 a 12/3 = 4. Stály podiel je 4 a ide o priamu úmernosť y = 4x.', '12/3')
q(9,4,'Z 60 žiakov vybralo možnosť A presne 18. Aká je relatívna početnosť možnosti A v percentách?', '30',['18','42','60'],'Relatívna početnosť je 18/60 = 0,3. Po vynásobení 100 dostaneme 30 %.', '18/60*100')
q(9,4,'Bod leží na grafe y = 2x + 1 a má súradnicu x = 3. Aká je jeho súradnica y?', '7',['6','5','4'],'Do predpisu grafu dosadíme x = 3: y = 2 × 3 + 1 = 7. Bod má súradnice (3; 7).', '2*3+1')

q(9,5,'Mesačný poplatok je 36 € a každá jazda stojí 1,50 €. Koľko jázd si využil, ak účet je 81 €?', '30',['54','45','20'],'Od účtu odčítame pevný poplatok: 81 − 36 = 45 €. Počet jázd je 45 : 1,50 = 30.', '(81-36)/1.5')
q(9,5,'Obdĺžnikový dvor má rozmery 12 m a 9 m. Koľko metrov meria jeho uhlopriečka?', '15',['21','108','3'],'Uhlopriečka je prepona: d = √(12² + 9²) = √(144 + 81) = √225 = 15 m.', '(12**2+9**2)**0.5')
q(9,5,'Prvé krmivo stojí 4 € za 3 kg a druhé 5 € za 2 kg. Aká je cena prvého krmiva za 6 kg v eurách?', '8',['12','15','9'],'Šesť kilogramov sú dve trojkilogramové balenia. Cena je 2 × 4 = 8 €; druhé krmivo by stálo 3 × 5 = 15 €.', '6/3*4')
q(9,5,'Valcová nádrž má polomer 2 dm a výšku 5 dm. Použi π = 3,14. Koľko litrov vody sa do nej zmestí?', '62,8',['31,4','125,6','6,28'],'V = πr²v = 3,14 × 4 × 5 = 62,8 dm³. Keďže 1 dm³ = 1 liter, objem je 62,8 litra.', '3.14*2*2*5')
q(9,5,'Z ceny 100 € najprv odpočítajú 15 % a potom kupón 5 €. Koľko eur zaplatíš?', '80',['85','95','75'],'Percentuálna zľava je 15 €. Po nej cena klesne na 85 € a kupón ju zníži na 85 − 5 = 80 €.', '100*(1-15/100)-5')
q(9,5,'Jeden kohútik naplní nádrž za 6 hodín, druhý za 3 hodiny. Za koľko hodín ju naplnia spolu pri stálom prietoku?', '2',['9','4,5','3'],'Za hodinu naplnia 1/6 + 1/3 = 1/2 nádrže. Celú nádrž preto naplnia za 1 : (1/2) = 2 hodiny.', '1/(1/6+1/3)')

# Numerion expansion: append two multistep tasks to each grade/topic. Existing
# 01..06 IDs, prompts, options, answer positions and explanations stay untouched.
q(7,0,'Zo 48 kryštálov použiješ najprv 3/8. Potom použiješ 1/5 zo zvyšných kryštálov. Koľko kryštálov ti zostane?', '24',['18','30','12'],'Najprv použiješ 48 × 3/8 = 18 kryštálov, takže zostane 30. Z nich použiješ 30 × 1/5 = 6. Nakoniec zostane 30 − 6 = 24 kryštálov.', '(48-48*3/8)*(1-1/5)')
q(7,0,'V nádobe je 1 a 1/2 litra vody. Odoberieš 2/5 litra vody a pridáš 3/4 litra vody. Potom odleješ 2/3 vody z nádoby. Koľko litrov vody zostane?', '37/60',['37/30','17/60','3/5'],'Najprv počítame 3/2 − 2/5 + 3/4 = 30/20 − 8/20 + 15/20 = 37/20 litra. Po odliatí 2/3 zostáva 1/3 vody. Preto zostane 37/20 × 1/3 = 37/60 litra.', '(3/2-2/5+3/4)*(1-2/3)')
q(7,1,'Chodník na mape s mierkou 1 : 20 000 meria 3 cm. Spiatočná cesta je o 25 % dlhšia než tento chodník v skutočnosti. Koľko metrov meria spiatočná cesta?', '750',['625','450','1500'],'Chodník meria v skutočnosti 3 × 20 000 = 60 000 cm = 600 m. Štvrtina zo 600 je 150. Spiatočná cesta je dlhšia o 150 m, teda meria 600 + 150 = 750 m.', '3*20000/100*(1+25/100)')
q(7,1,'Batoh po zľave 20 % stojí 72 €. Prilba stojí jednu tretinu pôvodnej ceny batoha. Koľko eur stojí prilba?', '30',['24','18','36'],'Cena 72 € predstavuje 80 % pôvodnej ceny batoha. Pôvodná cena je 72 : 0,8 = 90 €. Prilba stojí tretinu z 90 €, teda 90 : 3 = 30 €.', '72/(1-20/100)/3')
q(7,2,'Neznáme číslo zväčšíš o 4, výsledok vydelíš tromi a potom pripočítaš 2. Dostaneš 9. Aké bolo neznáme číslo?', '17',['11','21','25'],'Postupujeme odzadu: 9 − 2 = 7, potom 7 × 3 = 21 a nakoniec 21 − 4 = 17. Skúška je (17 + 4) : 3 + 2 = 7 + 2 = 9.', '(9-2)*3-4')
q(7,2,'Za štyri rovnaké balíčky a jednu tubu za 7 € zaplatíš spolu 39 €. Koľko eur stojí šesť takých balíčkov bez tuby?', '48',['39','42','56'],'Od celkovej ceny odčítame tubu: 39 − 7 = 32 €. Jeden balíček stojí 32 : 4 = 8 €. Šesť balíčkov preto stojí 6 × 8 = 48 €.', '(39-7)/4*6')
q(7,3,'Akvárium má vnútorné rozmery 60 cm, 40 cm a 30 cm. Naplníš ho do 3/4 jeho objemu. Koľko litrov vody je v akváriu?', '54',['72','18','540'],'Celý objem je 60 × 40 × 30 = 72 000 cm³. Tri štvrtiny sú 54 000 cm³. Keďže jeden liter má 1 000 cm³, v akváriu je 54 000 : 1 000 = 54 litrov.', '60*40*30*3/4/1000')
q(7,3,'Dvor má tvar obdĺžnika 14 m × 10 m. Celý záhon s rozmermi 8 m × 6 m leží vnútri dvora a nedláždi sa. Koľko štvorcových dlaždíc s hranou 0,5 m treba na zvyšok, ak nevzniká odpad?', '368',['92','184','560'],'Dvor má obsah 140 m² a záhon 48 m², takže sa dláždi 92 m². Jedna dlaždica má obsah 0,5 × 0,5 = 0,25 m². Počet dlaždíc je 92 : 0,25 = 368.', '(14*10-8*6)/(0.5*0.5)')
q(7,4,'Máš tri rôzne tričká, dvoje rôzne nohavice a dve rôzne čiapky. Dve tričká možno kombinovať s oboma nohavicami, tretie iba s jednými. Ku každej dovolenej dvojici vyberieš jednu čiapku. Koľko rôznych súprav vytvoríš?', '10',['8','12','6'],'Prvé dve tričká dávajú 2 × 2 = 4 dvojice s nohavicami. Tretie tričko pridá jednu dvojicu, spolu ich je 5. Každá má dve možnosti čiapky, preto existuje 5 × 2 = 10 súprav.', '(2*2+1)*2')
q(7,4,'Zo štyroch detí vyberáš kapitána a jeho zástupcu. Mila nemôže byť kapitánkou, ale zástupkyňou byť môže. Jedno dieťa nemôže mať obe úlohy. Koľko rôznych rozdelení úloh existuje?', '9',['6','12','8'],'Kapitána vyberieme z troch detí, pretože Mila túto úlohu mať nemôže. Pre každého kapitána zostanú tri možnosti zástupcu vrátane Mily. Dostaneme 3 × 3 = 9 rozdelení; na funkciách záleží.', '(4-1)*(4-1)')
q(7,5,'Trasa má 32 km. Ráno prejdeš 3/8 celej trasy a popoludní 1/4 celej trasy. Zvyšok ideš autobusom za 0,40 € za kilometer plus pevný poplatok 1,20 €. Koľko eur zaplatíš?', '6',['4,8','9,2','14'],'Pešo prejdeš 32 × 3/8 = 12 km a 32 × 1/4 = 8 km, spolu 20 km. Autobusu zostane 12 km. Cena je 12 × 0,40 + 1,20 = 4,80 + 1,20 = 6 €.', '(32-32*3/8-32*1/4)*0.4+1.2')
q(7,5,'Každý z 24 žiakov dostane 0,5 litra džúsu. Džús sa predáva iba v 1,5-litrových fľašiach po 2,40 €. Koľko eur sa vráti z 20 €, ak kúpiš presne potrebný počet fliaš?', '0,80',['1,20','2,40','8'],'Treba 24 × 0,5 = 12 litrov džúsu, čiže 12 : 1,5 = 8 fliaš. Cena je 8 × 2,40 = 19,20 €. Z dvadsiatich eur sa vráti 20 − 19,20 = 0,80 €.', '20-24*0.5/1.5*2.4')

q(8,0,'Vypočítaj −2,5 + 3/4 × 8 − (−1,5).', '5',['2','−5','3,5'],'Najprv násobíme: 3/4 × 8 = 6. Odčítanie záporného čísla znamená pripočítať 1,5. Dostaneme −2,5 + 6 + 1,5 = 5.', '-2.5+3/4*8-(-1.5)')
q(8,0,'Vypočítaj (−3/4 + 1/2) : (−5/6) − 1/5.', '1/10',['−1/2','−1/10','1/2'],'V zátvorke je −3/4 + 2/4 = −1/4. Delíme záporným zlomkom: (−1/4) × (−6/5) = 3/10. Odčítame 1/5 = 2/10 a získame 1/10.', '(-3/4+1/2)/(-5/6)-1/5')
q(8,1,'Tovar stojí 200 €. Najprv dostaneš zľavu 15 % a potom ďalšiu zľavu 10 % z už zníženej ceny. Nakoniec pripočítajú dopravu 5 €. Koľko eur zaplatíš?', '158',['155','165','153'],'Po prvej zľave je cena 200 × 0,85 = 170 €. Druhá zľava sa počíta zo 170 €, takže cena klesne na 170 × 0,9 = 153 €. S dopravou zaplatíš 153 + 5 = 158 €.', '200*(1-15/100)*(1-10/100)+5')
q(8,1,'Päť rovnako výkonných pracovníkov dokončí prácu za 12 dní pri 8 hodinách denne. Za koľko dní ju dokončí osem pracovníkov pri 6 hodinách denne, ak sa výkon každého nemení?', '10',['8','12','15'],'Celá práca potrebuje 5 × 12 × 8 = 480 pracovných hodín jedného človeka. Nová skupina urobí denne 8 × 6 = 48 takých hodín. Práca potrvá 480 : 48 = 10 dní.', '5*12*8/(8*6)')
q(8,2,'Rieš rovnicu 3(2x − 1) − 2(x + 4) = 17. Aké je x?', '7',['5','8','4'],'Roznásobíme zátvorky: 6x − 3 − 2x − 8 = 17. Spojíme členy na 4x − 11 = 17, preto 4x = 28. Po vydelení štyrmi dostaneme x = 7.', '(17+3+8)/(6-2)')
q(8,2,'Obdĺžnik má obvod 50 cm. Dlhšia strana je o 1 cm väčšia než trojnásobok kratšej. Aký je jeho obsah v cm²?', '114',['120','150','108'],'Kratšiu stranu označíme x a dlhšiu 3x + 1. Z obvodu dostaneme 2(x + 3x + 1) = 50, teda 8x = 48 a x = 6 cm. Dlhšia strana má 19 cm, preto obsah je 6 × 19 = 114 cm².', '((50/2-1)/4)*(3*((50/2-1)/4)+1)')
q(8,3,'Lichobežníkový dvor má základne 8 m a 14 m a výšku 5 m. Vydláždi sa 60 % jeho obsahu za 12 € za m². Koľko eur stojí dláždenie?', '396',['660','330','792'],'Obsah lichobežníka je (8 + 14) × 5 : 2 = 55 m². Dláždi sa 55 × 0,6 = 33 m². Cena je 33 × 12 = 396 €.', '(8+14)*5/2*60/100*12')
q(8,3,'Hranol má trojuholníkovú podstavu so základňou 6 cm a príslušnou výškou 4 cm. Výška hranola je 15 cm. Odoberie sa z neho 30 cm³ materiálu. Aký objem v cm³ zostane?', '150',['180','330','90'],'Obsah podstavy je 6 × 4 : 2 = 12 cm². Objem celého hranola je 12 × 15 = 180 cm³. Po odobratí materiálu zostáva 180 − 30 = 150 cm³.', '6*4/2*15-30')
q(8,4,'Aritmetický priemer štyroch meraní je 12. Po pridaní piateho merania je priemer všetkých piatich 13. Aká je hodnota piateho merania?', '17',['13','14','16'],'Prvé štyri merania majú súčet 4 × 12 = 48. Päť meraní musí mať súčet 5 × 13 = 65. Piate meranie preto má hodnotu 65 − 48 = 17.', '5*13-4*12')
q(8,4,'Vo vrecku sú tri modré a dve červené guľôčky. Náhodne vytiahneš postupne dve bez vrátenia, vždy s rovnakou šancou pre každú zostávajúcu guľôčku. Aká je pravdepodobnosť, že obe budú modré?', '3/10',['9/25','2/5','1/5'],'Všetkých usporiadaných dvojíc je 5 × 4 = 20, pretože prvú guľôčku nevraciame. Modré dvojice môžeme vytvoriť 3 × 2 = 6 spôsobmi. Pravdepodobnosť je 6/20 = 3/10.', '3*2/(5*4)')
q(8,5,'Izba má rozmery 5 m × 4 m. K jej obsahu pripočítaš 10 % podlahy ako rezervu. Jedno celé balenie pokryje 2 m² a stojí 18 €. Doprava stojí 15 €. Koľko eur zaplatíš?', '213',['198','195','231'],'Izba má 20 m², s desaťpercentnou rezervou treba 22 m². To je 22 : 2 = 11 celých balení. Podlaha stojí 11 × 18 = 198 € a s dopravou 198 + 15 = 213 €.', '(5*4)*(1+10/100)/2*18+15')
q(8,5,'Autobus ide 54 km rýchlosťou 72 km/h. Potom prejdeš pešo 1,8 km rýchlosťou 4,5 km/h. Koľko minút trvá celá cesta bez čakania a prestávok?', '69',['60','84','93'],'Autobus ide 54 : 72 = 0,75 hodiny, teda 45 minút. Peší úsek trvá 1,8 : 4,5 = 0,4 hodiny, teda 24 minút. Spolu je to 45 + 24 = 69 minút.', '(54/72+1.8/4.5)*60')

q(9,0,'Vypočítaj (√196 − 2³)² + 3².', '45',['15','27','117'],'Odmocnina zo 196 je 14 a 2³ = 8. V zátvorke zostane 6, jeho druhá mocnina je 36. Pripočítame 3² = 9, takže výsledok je 36 + 9 = 45.', '(196**0.5-2**3)**2+3**2')
q(9,0,'Vypočítaj (6 × 10⁵) : (3 × 10⁻²) − 5 × 10⁶.', '15000000',['1500000','19500000','25000000'],'Podiel koeficientov je 2 a exponent pri desiatke je 5 − (−2) = 7. Podiel je teda 2 × 10⁷ = 20 000 000. Po odčítaní 5 000 000 dostaneme 15 000 000.', '(6*10**5)/(3*10**(-2))-5*10**6')
q(9,1,'Po zľave 10 % a následnej ďalšej zľave 20 % z už zníženej ceny stojí bunda 72 €. Aká bola jej pôvodná cena v eurách?', '100',['90','96','104'],'Po oboch zľavách zostáva 0,9 × 0,8 = 0,72 pôvodnej ceny, čiže 72 %. Pôvodná cena je 72 : 0,72 = 100 €. Skúška: 100 × 0,9 × 0,8 = 72 €.', '72/((1-10/100)*(1-20/100))')
q(9,1,'Model nádoby má všetky dĺžkové rozmery 50-krát menšie než skutočná nádoba s objemom 4 m³. Aký objem má model v cm³?', '32',['80000','1600','8'],'Objem sa zmenší 50³ = 125 000-krát. Skutočná nádoba má objem 4 000 000 cm³, pretože 1 m³ = 1 000 000 cm³. Model má objem 4 000 000 : 125 000 = 32 cm³.', '4*1000000/50**3')
q(9,2,'Rieš rovnicu (x − 2)/3 + (x + 1)/2 = 7. Aké je x?', '8,6',['8,2','7','9'],'Obe strany vynásobíme šiestimi: 2(x − 2) + 3(x + 1) = 42. Po úprave je 5x − 1 = 42, teda 5x = 43. Výsledok je x = 43/5 = 8,6.', '(7*6+4-3)/(2+3)')
q(9,2,'Súčet dvoch čísel je 52. Ak väčšie číslo zmenšíme o 4, dostaneme trojnásobok menšieho. Urči väčšie číslo.', '40',['36','39','44'],'Menšie číslo označíme x a väčšie je 52 − x. Platí 52 − x − 4 = 3x, teda 48 = 4x a x = 12. Väčšie číslo je 52 − 12 = 40; kontrola: 40 − 4 = 3 × 12.', '52-(52-4)/(1+3)')
q(9,3,'Pravouhlý trojuholník má odvesny 9 cm a 12 cm. Aký je jeho obvod v centimetroch?', '36',['30','42','108'],'Najprv určíme preponu: c = √(9² + 12²) = √225 = 15 cm. Obvod je súčet všetkých troch strán: 9 + 12 + 15 = 36 cm.', '9+12+(9**2+12**2)**0.5')
q(9,3,'Valcová nádoba s polomerom 3 cm a výškou 12 cm je naplnená vodou do 2/3 výšky. Koľko plných kužeľových nádob s rovnakým polomerom a výškou 6 cm možno touto vodou naplniť bez strát?', '4',['2','3','6'],'Výška vody vo valci je 12 × 2/3 = 8 cm, jej objem je π × 3² × 8 = 72π cm³. Jeden kužeľ má objem π × 3² × 6 : 3 = 18π cm³. Podiel 72π : 18π = 4, takže naplníme štyri nádoby.', '(3**2*12*2/3)/(3**2*6/3)')
q(9,4,'Dvanásť žiakov má priemerný výsledok 7,5 bodu a ďalších osem žiakov má priemer 9 bodov. Aký je priemerný počet bodov celej dvadsaťčlennej skupiny?', '8,1',['8,25','8,4','8'],'Prvá skupina získala spolu 12 × 7,5 = 90 bodov a druhá 8 × 9 = 72 bodov. Celkový súčet 162 delíme všetkými 20 žiakmi: 162 : 20 = 8,1 bodu. Priemery skupín nemožno iba sčítať a deliť dvomi.', '(12*7.5+8*9)/(12+8)')
q(9,4,'Hodíš dvoma nezávislými spravodlivými šesťstennými kockami. Aká je pravdepodobnosť, že aspoň na jednej padne šestka?', '11/36',['1/3','1/6','25/36'],'Spolu je 6 × 6 = 36 rovnako pravdepodobných usporiadaných výsledkov. Bez šestky je ich 5 × 5 = 25. Aspoň jednu šestku má preto 36 − 25 = 11 výsledkov, teda pravdepodobnosť je 11/36.', '(6*6-5*5)/(6*6)')
q(9,5,'Požičovňa A účtuje 6 € plus 2 € za hodinu. Požičovňa B účtuje iba 3,50 € za hodinu. Na koľko celých hodín si požičiaš bicykel, aby bola A presne o 3 € lacnejšia než B?', '6',['4','5','8'],'Cenu A zapíšeme 6 + 2h a cenu B ako 3,5h. Rozdiel B − A má byť 3 €, takže 3,5h − (6 + 2h) = 3. Dostaneme 1,5h = 9 a h = 6. Skúška: A stojí 18 € a B 21 €.', '(3+6)/(3.5-2)')
q(9,5,'Otvorená nádrž má vnútornú dĺžku 4 m, šírku 3 m a hĺbku 2 m. Zvnútra sa natiera dno a všetky štyri steny dvoma vrstvami. Jedna vrstva spotrebuje 0,25 litra na m² a liter náteru stojí 10 €. Koľko eur stojí potrebný náter bez odpadu?', '200',['120','240','400'],'Dno má 4 × 3 = 12 m² a steny spolu 2 × 4 × 2 + 2 × 3 × 2 = 28 m², celkom 40 m². Dve vrstvy spotrebujú 40 × 2 × 0,25 = 20 litrov. Náter preto stojí 20 × 10 = 200 €.', '(4*3+2*4*2+2*3*2)*2*0.25*10')

def calculate(node):
    if isinstance(node, ast.Expression): return calculate(node.body)
    if isinstance(node, ast.Constant) and isinstance(node.value, (int,float)):
        return Fraction(str(node.value))
    if isinstance(node, ast.UnaryOp) and isinstance(node.op,(ast.UAdd,ast.USub)):
        return calculate(node.operand) * (-1 if isinstance(node.op,ast.USub) else 1)
    if isinstance(node, ast.BinOp):
        a,b=calculate(node.left),calculate(node.right)
        if isinstance(node.op,ast.Pow):
            if b.denominator == 1:
                return a ** int(b)
            if b == Fraction(1,2) and a >= 0:
                numerator,denominator=math.isqrt(a.numerator),math.isqrt(a.denominator)
                if numerator*numerator==a.numerator and denominator*denominator==a.denominator:
                    return Fraction(numerator,denominator)
            raise ValueError('A proof root must have an exact rational result')
        operations={ast.Add:operator.add,ast.Sub:operator.sub,ast.Mult:operator.mul,ast.Div:operator.truediv}
        if type(node.op) in operations: return operations[type(node.op)](a,b)
    raise ValueError('Unsupported mathematical proof')

def number(value):
    return Fraction(value.replace('−','-').replace(',','.'))

def fingerprint(value):
    canonical=json.dumps(value,sort_keys=True,ensure_ascii=False,separators=(',',':')).encode('utf-8')
    return hashlib.sha256(canonical).hexdigest()

def legacy_snapshot():
    return json.loads(Path(__file__).with_name('legacy_108_fingerprints.json').read_text(encoding='utf-8-sig'))

def validate():
    assert len(ROWS)==144 and len({r['Id'] for r in ROWS})==144
    original=legacy_snapshot()
    current={r['Id']:r for r in ROWS}
    assert original['QuestionCount']==108 and len(original['Questions'])==108
    for question_id,digest in original['Questions'].items():
        assert question_id in current and fingerprint(current[question_id])==digest, ('legacy question changed',question_id)
    added=[r for r in ROWS if r['Id'] not in original['Questions']]
    expected_added={f'math-g{g}-s{s}-{i:02}' for g in (7,8,9) for s in range(6) for i in (7,8)}
    assert {r['Id'] for r in added}==expected_added and all(r.get('MathProof') for r in added)
    assert len({r['Prompt'] for r in ROWS})==144, 'Repeated task wording'
    proofs=0
    for r in ROWS:
        assert len(r['Options'])==4 and len(set(r['Options']))==4, r['Id']
        assert len(r['Explanation'])>35 and r['SectionId']==f"section-{r['Subject']}"
        if r.get('MathProof'):
            expected=calculate(ast.parse(r['MathProof'],mode='eval'))
            actual=number(r['Options'][r['CorrectOption']])
            assert expected==actual, (r['Id'],expected,actual)
            for i,opt in enumerate(r['Options']):
                if i != r['CorrectOption']:
                    assert number(opt)!=expected, ('equivalent distractor',r['Id'],opt)
            proofs+=1
    for grade in (7,8,9):
        entries=[r for r in ROWS if r['Grade']==grade]
        assert len(entries)==48
        assert all(sum(r['Subject']==s for r in entries)==8 for s in range(6))
        assert all(sum(r['CorrectOption']==i for r in entries)==12 for i in range(4))
        assert {r['Difficulty'] for r in entries}=={1,2,3,4}
    return proofs

def main():
    proofs=validate()
    data=dict(SchemaVersion=2,Focus='Matematika pre 7.–9. ročník ZŠ',
              GradeRange=[7,8,9],QuestionCount=len(ROWS),SourceUrl=SOURCE,
              ScopeNote='Pôvodné úlohy LUMEN; tematické rozdelenie upraviť podľa školského programu. Zahrnuté je aj opakovanie.',
              Sections=[dict(Id=f'section-{i}',Subject=i,Name=n,Description=d,UnlockLevel=1) for i,(n,d) in enumerate(STRANDS)],
              Questions=ROWS)
    lesson_source=(ROOT/'Assets/Lumen/Core/LessonCatalog.cs').read_text(encoding='utf-8')
    literal=r'"(?:[^"\\]|\\.)*"'
    lesson_pattern=rf'L\((7|8|9),\s*([0-5]),\s*({literal}),\s*({literal}),\s*({literal}),\s*({literal})\)'
    lessons=[]
    for match in re.finditer(lesson_pattern,lesson_source):
        grade,subject=int(match[1]),int(match[2])
        values=[json.loads(match[i]) for i in (3,4,5,6)]
        lessons.append(dict(Id=f'lesson-g{grade}-s{subject}',Grade=grade,Subject=subject,
                            Title=values[0],Explanation=values[1],WorkedExample=values[2],Takeaway=values[3],SourceUrl=SOURCE))
    assert len(lessons)==18 and len({l['Id'] for l in lessons})==18
    assert fingerprint(lessons)==legacy_snapshot()['LessonsSHA256'], 'The existing 18 lessons changed'
    data['Lessons']=lessons
    target=ROOT/'Assets/Lumen/Resources/Content/curriculum.json'
    target.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    lesson_target=target.parent/'lessons.json'
    lesson_text=json.dumps(dict(SchemaVersion=1,LessonCount=18,Lessons=lessons),ensure_ascii=False,indent=2)+'\n'
    if not lesson_target.exists() or lesson_target.read_text(encoding='utf-8')!=lesson_text:
        lesson_target.write_text(lesson_text,encoding='utf-8')
    quote=lambda value:json.dumps(value,ensure_ascii=False)
    lines=['// Generated from original authored tasks in Tests/MathHarness/generate_content.py.',
           '// Regenerate with that script; arithmetic proofs are validated before emission.',
           'namespace Lumen.Core','{','    internal static class QuestionBank','    {',
           '        internal static void Populate(ContentCatalog c)','        {']
    for r in ROWS:
        lines.append('            c.Add(new Question {')
        lines.append(f'                Id = {quote(r["Id"])}, Grade = {r["Grade"]}, Subject = (Subject){r["Subject"]}, AgeBand = AgeBand.Ages13To15, Difficulty = {r["Difficulty"]}, SectionId = {quote(r["SectionId"])},')
        lines.append(f'                Prompt = {quote(r["Prompt"])}, Options = new[] {{ '+', '.join(quote(v) for v in r['Options'])+' },')
        lines.append(f'                CorrectOption = {r["CorrectOption"]}, Explanation = {quote(r["Explanation"])}, SourceUrl = {quote(SOURCE)} }});')
    lines+=['        }','    }','}']
    (ROOT/'Assets/Lumen/Core/QuestionBank.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(f'NUMERION_MATH_CONTENT_VALIDATED: 144 questions, 48 per grade, 8 per strand, 18 unchanged textbook chapters, {proofs} exact independent arithmetic proofs (all 36 additions), 108 legacy fingerprints unchanged, unique non-equivalent numeric distractors.')

if __name__=='__main__': main()
