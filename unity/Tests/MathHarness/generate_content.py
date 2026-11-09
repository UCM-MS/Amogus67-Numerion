"""Original LUMEN mathematics tasks; source authoring + independent numeric proof checks.

Only the thematic scope comes from the linked SPU standard. Prompts and worked
solutions are original. Grade assignments include deliberate revision topics.
"""
from pathlib import Path
from fractions import Fraction
import ast
import json
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
               Subject=strand, AgeBand=2, Difficulty=[1, 2, 2, 3, 3, 4][index-1],
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

def calculate(node):
    if isinstance(node, ast.Expression): return calculate(node.body)
    if isinstance(node, ast.Constant) and isinstance(node.value, (int,float)):
        return Fraction(str(node.value))
    if isinstance(node, ast.UnaryOp) and isinstance(node.op,(ast.UAdd,ast.USub)):
        return calculate(node.operand) * (-1 if isinstance(node.op,ast.USub) else 1)
    if isinstance(node, ast.BinOp):
        a,b=calculate(node.left),calculate(node.right)
        if isinstance(node.op,ast.Pow): return float(a) ** float(b)
        operations={ast.Add:operator.add,ast.Sub:operator.sub,ast.Mult:operator.mul,ast.Div:operator.truediv}
        if type(node.op) in operations: return operations[type(node.op)](a,b)
    raise ValueError('Unsupported mathematical proof')

def number(value):
    return Fraction(value.replace('−','-').replace(',','.'))

def validate():
    assert len(ROWS)==108 and len({r['Id'] for r in ROWS})==108
    proofs=0
    for r in ROWS:
        assert len(r['Options'])==4 and len(set(r['Options']))==4, r['Id']
        assert len(r['Explanation'])>35 and r['SectionId']==f"section-{r['Subject']}"
        if r.get('MathProof'):
            expected=calculate(ast.parse(r['MathProof'],mode='eval'))
            actual=number(r['Options'][r['CorrectOption']])
            assert abs(float(expected)-float(actual))<1e-8, (r['Id'],expected,actual)
            for i,opt in enumerate(r['Options']):
                if i != r['CorrectOption']:
                    assert abs(float(number(opt))-float(expected))>1e-8, ('equivalent distractor',r['Id'],opt)
            proofs+=1
    for grade in (7,8,9):
        entries=[r for r in ROWS if r['Grade']==grade]
        assert len(entries)==36
        assert all(sum(r['Subject']==s for r in entries)==6 for s in range(6))
        assert all(sum(r['CorrectOption']==i for r in entries)==9 for i in range(4))
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
    data['Lessons']=lessons
    target=ROOT/'Assets/Lumen/Resources/Content/curriculum.json'
    target.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (target.parent/'lessons.json').write_text(json.dumps(dict(SchemaVersion=1,LessonCount=18,Lessons=lessons),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
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
    print(f'LUMEN_MATH_CONTENT_VALIDATED: 108 questions, 36 per grade, 18 textbook chapters, {proofs} independent arithmetic proofs, unique non-equivalent numeric distractors.')

if __name__=='__main__': main()
