# inbound-invoice-processing Specification

## Purpose

Bejövő NAV-bizonylatok felvétele a SharePoint Excel-nyilvántartásba, az ismételt sorok elkerülésével, az eredeti és a kapcsolt bizonylatok összekötésével, valamint az InsDate kurzor előreléptetésével.

## Requirements

### Requirement: Egy feldolgozási futás
A számlafeldolgozás egy futása MUST a bejövő bizonylatokat rögzíteni. Éles időzítő és élesben hívható végpont MUST NOT készülni. A futás MUST NOT értesítést küldeni.

#### Scenario: Teszt vagy helyi indítás
- **WHEN** teszt vagy helyi Development indítás elindítja a számlafeldolgozást
- **THEN** a futás rögzíti a bizonylatokat, időzítő és éles végpont nélkül, és értesítés nem megy ki

### Requirement: Szelet kezdete
A feldolgozás MUST szeletenként kérdezni. Az első szelet MUST a tárolt InsDate UTC-napjának `00:00:00Z` időpontjánál indulni, de MUST NOT a kezdődátum `00:00:00Z` időpontja előtt. Ugyanabban a futásban a következő szelet MUST az előző szelet végénél indulni. A közös határ 1 ms átfedés. A futás MUST NOT a friss InsDate napjának elejére visszalépni, ha az korábbi ennél a határnál. Az Excel-beszúrás a duplikált sort kihagyja.

#### Scenario: Első szelet az ablak elején
- **WHEN** a tárolt InsDate `2026-01-15T18:00:00Z`, és ez nem korábbi a kezdődátumnál
- **THEN** az első szelet `2026-01-15T00:00:00Z` időpontnál indul

#### Scenario: Kezdődátum határa
- **WHEN** a tárolt InsDate napja a kezdődátum elé esik
- **THEN** az első szelet a kezdődátum `00:00:00Z` időpontjánál indul

#### Scenario: Következő szelet a maradék elején
- **WHEN** egy szelet vége `2026-02-05T00:00:00.000Z`, és a futásnak van még hátralévő ablaka
- **THEN** a következő szelet `2026-02-05T00:00:00.000Z` időpontnál indul, a közös 1 ms mindkét szeletben benne van, és a már rögzített számla nem kap új sort

### Requirement: Következő futás ablaka
A következő futás első szelete MUST a tárolt InsDate UTC-napjának `00:00:00Z` időpontjánál indulni. Az átfedő határ már rögzített számlája MUST NOT új sorként bekerülni.

#### Scenario: Ismételt határ
- **WHEN** az előző futás a szelet végére írta az InsDate értéket, és a következő futás ugyanezt a határidőpontot ismét megkapja
- **THEN** az első szelet ennek a napnak a `00:00:00Z` időpontjánál indul, és a már rögzített számla nem kap új sort

### Requirement: Szeletciklus
Egy szelet MUST ebben a sorrendben készüljön: a NAV-szelet letöltése, az Excel letöltése, a tételek bedolgozása, az InsDate helyi frissítése, majd az Excel feltöltése. További szelet MUST csak sikeres feltöltés után indulni. Az utolsó szelet után a ciklus MUST megállni. A feldolgozás MUST NOT külön fájlfoglalást használni.

#### Scenario: Van következő szelet
- **WHEN** a szelet feltöltése sikerült, és a futásnak van még szelete
- **THEN** a következő NAV-letöltés csak ezután indul

#### Scenario: Utolsó szelet
- **WHEN** a futás utolsó szeletének feltöltése sikerült
- **THEN** újabb NAV-letöltés és Excel-letöltés nem indul

#### Scenario: Sikertelen feltöltés
- **WHEN** a helyi bedolgozás elkészült, és a feltöltés sikertelen
- **THEN** a SharePoint fájl a szelet előtti állapotban marad, és az InsDate nem lép

### Requirement: Több fájl feltöltése
A feltöltés MUST minden olyan éves fájlt elküldeni, amelyet a szelet módosított. A szelet MUST csak akkor kész lenni, ha mindegyik feltöltés sikerült. A már feltöltött fájl MUST megmaradni, ha egy későbbi fájl feltöltése sikertelen. Az InsDate MUST csak az aktuális év fájljába kerülni, és MUST csak annak sikeres feltöltésével előrelépni. Az előző év fájlja MUST NOT új InsDate értéket kapni. Az ismételt futás MUST NOT második sort készíteni.

#### Scenario: Az aktuális év fájlja sikertelen
- **WHEN** a sor fájlja már feltöltődött, és az aktuális év fájljának feltöltése sikertelen
- **THEN** a sor megmarad, az InsDate nem lép, és az ismétlés nem készít második sort

#### Scenario: Minden fájl fent van
- **WHEN** a szelet minden módosított fájlja feltöltődött
- **THEN** a szelet kész, és a következő szelet indulhat

### Requirement: Az aktuális év fájlja utoljára
Az aktuális év fájlja MUST a szelet többi módosított fájlja után feltöltődni.

#### Scenario: Előző év és aktuális év
- **WHEN** a szelet az előző év fájlját és az aktuális év fájlját is módosítja
- **THEN** az előző év fájlja töltődik fel előbb, az aktuális év fájlja utána

### Requirement: Sikeres szelet
A szelet sorai és hibabejegyzései a bedolgozásban készülnek, majd az InsDate a szelet végére kerül. Üres, sikeres szeletnél az InsDate MUST akkor is előrelépni. A SharePoint MUST a feltöltéssel megkapni a sorokat és az InsDate értéket.

#### Scenario: Üres szelet
- **WHEN** a szelet lekérdezése sikerül, és nincs rögzítendő bizonylat
- **THEN** az InsDate a szelet végére kerül

#### Scenario: Van rögzítendő bizonylat
- **WHEN** a szelet sorai és hibabejegyzései hiba nélkül elkészülnek
- **THEN** az InsDate ezután a szelet végének UTC időpontja

### Requirement: Hibás szelet
Hibás szeletnél a már feltöltött szeletek sorai, InsDate értéke és hibabejegyzései MUST megmaradni. A hibás szelet InsDate értéke MUST NOT előrelépni. Külön bizonylatállapot és Excel-hibaoszlop MUST NOT készülni.

#### Scenario: Későbbi szelet hibás
- **WHEN** az első szelet lezárult, és a következő szelet lekérdezése sikertelen
- **THEN** egy hibabejegyzés készül, a feldolgozás megáll, további szelet nem indul, és az első szelet InsDate értéke megmarad

### Requirement: Hibás ciklus
Ha egy ciklus hibával leáll, a rendszer MUST egy hibabejegyzést készíteni, és a feldolgozás MUST megállni. További szelet MUST NOT indulni. A bejegyzés a következő Slack-üzenet anyaga. Ismételt ugyanilyen hiba MUST NOT második bejegyzést készíteni. A küldés MUST NOT ebben a change-ben történni.

#### Scenario: Sikertelen ciklus
- **WHEN** egy ciklus NAV-letöltése, Excel-letöltése, bedolgozása vagy feltöltése hibával leáll
- **THEN** egy hibabejegyzés készül, és újabb ciklus nem indul

#### Scenario: Ismételt ciklus hiba
- **WHEN** ugyanaz a ciklus ugyanilyen hibával újra leáll
- **THEN** második hibabejegyzés nem készül

#### Scenario: Korábbi szelet már fent van
- **WHEN** az első szelet feltöltése sikerült, és a következő szelet feltöltése sikertelen
- **THEN** az első szelet sorai és InsDate értéke megmarad, az ismételt futás nem készít második sort

### Requirement: Kezdődátum
A kezdődátum MUST a verziókezelt, nem titkos `Processing:StartDate` értéke lenni, `yyyy-MM-dd` alakban. A meglévő konfigurációs sorrend későbbi forrása MUST felülírni. Hiányzó, üres vagy értelmezhetetlen érték MUST a NAV-hívás előtt megállítani a futást, és az InsDate MUST NOT változni.

#### Scenario: Érvényes kezdődátum
- **WHEN** a `Processing:StartDate` értéke `2026-01-01`
- **THEN** a lekérdezés nem indul `2026-01-01T00:00:00Z` előtt

#### Scenario: Környezeti felülírás
- **WHEN** a verziókezelt érték `2026-01-01`, és egy későbbi konfigurációs forrás `2026-02-01` értéket ad
- **THEN** a kezdődátum `2026-02-01`

#### Scenario: Hiányzó kezdődátum
- **WHEN** a `Processing:StartDate` hiányzik, üres vagy nem `yyyy-MM-dd` alakú
- **THEN** a futás a NAV-hívás előtt megáll, és az InsDate nem változik

### Requirement: InsDate olvasása
Az InsDate kurzor MUST a legutóbbi létező kezelt év `Paraméterek` lapjáról származni. Ha nincs kezelt évi fájl, a lekérdezés MUST az aktuális magyarországi év alapértelmezett InsDate értékét használni. Meglévő lapon üres vagy hiányzó InsDate MUST a NAV-hívás előtt megállítani a futást, és a rendszer MUST NOT alapértelmezést írni helyette.

#### Scenario: Két kezelt év
- **WHEN** az előző és az aktuális év fájlja is létezik
- **THEN** a kurzor az aktuális év `Paraméterek` lapjának InsDate értéke

#### Scenario: Nincs fájl
- **WHEN** a magyarországi aktuális év 2026, és egyik kezelt év fájlja sincs meg
- **THEN** a lekérdezés `2025-12-31T00:00:00Z` értéktől számol, és a NAV-hívás előtt fájl nem készül

#### Scenario: Üres érték
- **WHEN** a kiválasztott `Paraméterek` lapon az InsDate kulcs hiányzik vagy üres
- **THEN** a futás a NAV-hívás előtt megáll, és a cella nem kap alapértelmezést

### Requirement: InsDate írása
Új éves fájl MUST csak akkor készülni, ha rögzítendő bizonylat kelte vagy a sikeres szelet kurzora igényli. A másolat MUST vinni az InsDate értékét, és utána ez a fájl MUST a kurzor lenni. Az előző év cellája MUST NOT változni. Sikeres szelet után a rendszer MUST biztosítani a magyarországi aktuális év aktuális hónapjának fájlját, és az InsDate értéket oda MUST írni.

#### Scenario: Új aktuális évi fájl
- **WHEN** sikeres szelet után az aktuális év fájlja még nincs meg, és az előző év InsDate értéke `2025-12-31T00:00:00Z`
- **THEN** az új fájl ezt az értéket viszi tovább, a kurzor az új fájlba kerül, és az előző év cellája `2025-12-31T00:00:00Z` marad

#### Scenario: Aktuális hónap
- **WHEN** a magyarországi aktuális hónap március, és a szelet sikeres
- **THEN** az InsDate az aktuális év márciusi fájljának `Paraméterek` lapjára kerül

### Requirement: Számla azonossága
A számla azonossága MUST a szállítói adószám és a számlaszám együttese lenni. A rendszer MUST csak a szélső whitespace-t levágni. A kis- és nagybetű MUST megmaradni. Már nyilvántartott számla MUST NOT új sort kapni, és a meglévő sor adata MUST NOT változni.

#### Scenario: Ismételt számla
- **WHEN** ugyanaz az adószám és számlaszám már szerepel valamelyik kezelt év egy havi lapján
- **THEN** új sor nem készül, és a meglévő sor változatlan marad

#### Scenario: Eltérő betű
- **WHEN** a nyilvántartott számlaszám `Ab-1`, és a kivonat számlaszáma `ab-1`
- **THEN** a két érték nem ugyanaz a számla

### Requirement: Keresés
A rendszer MUST mindkét kezelt év minden havi lapján keresni, az aktuális szeletben írt sorokat is beleértve. A rendszer MUST NOT sort mozgatni, törölni vagy másik lapra helyezni.

#### Scenario: Másik év
- **WHEN** a számla az előző év egyik havi lapján szerepel
- **THEN** a keresés megtalálja, és új sor nem készül

#### Scenario: Ugyanazon szelet sora
- **WHEN** a szelet korábbi tétele már sort írt ugyanarra az adószámra és számlaszámra
- **THEN** a későbbi tétel ezt a sort megtalálja

### Requirement: Új sor helye
Új sor MUST az utolsó kitöltött sor után kerülni. Kitöltött a sor, ha a 16 oszlop bármelyikében van nem üres érték. A hézag MUST megmaradni. Üres lapon az első adatsor MUST a 2. sor lenni.

#### Scenario: Köztes üres sor
- **WHEN** a 3. sor üres, és a 4. sor egyik oszlopa kitöltött
- **THEN** az új sor a 4. sor után kerül, a 3. sor üres marad

#### Scenario: Üres lap
- **WHEN** a havi lapon csak a fejléc van kitöltve
- **THEN** az új sor a 2. sor

### Requirement: Felhasználói oszlopok
Új sorban a `Számla megvan?`, a `Fizetve?`, az `Autó rendelés` és a `Megjegyzés` MUST üres lenni. Meglévő sorban a rendszer MUST NOT mást írni, mint az eredeti számla `Kapcsolt bizonylatok` mezőjének bővítését.

#### Scenario: Új sor
- **WHEN** a rendszer új számlasort ír
- **THEN** ez a négy oszlop üres

#### Scenario: Meglévő sor
- **WHEN** a számla már szerepel a nyilvántartásban
- **THEN** a felhasználó által írt cellák változatlanok maradnak

### Requirement: Teljes bizonylat
Új Excel-sorhoz és kezelt éven kívüli hibabejegyzéshez a rendszer MUST lekérni a teljes bizonylatot. Már nyilvántartott számla teljes bizonylatát MUST NOT lekérni. A `batchIndex` MUST csak kötegelt módosításnál szerepelni a kérésben. Sikertelen lekérdezésnél a szelet MUST sikertelen lenni, és az InsDate MUST NOT előrelépni.

#### Scenario: Új sor
- **WHEN** a kivonattételből új Excel-sor készül
- **THEN** a rendszer lekéri a teljes bizonylatot

#### Scenario: Már rögzített számla
- **WHEN** a kivonattétel már szerepel a nyilvántartásban
- **THEN** a teljes bizonylat nem kérdezhető le ehhez a tételhez

#### Scenario: Kötegelt módosítás
- **WHEN** az új sor kötegelt módosítás, és a kivonat `batchIndex` értéket ad
- **THEN** a teljesbizonylat-kérés tartalmazza ezt a `batchIndex` értéket

#### Scenario: Sikertelen teljes bizonylat
- **WHEN** a teljes bizonylat lekérdezése sikertelen
- **THEN** a szelet nem sikeres, és az InsDate nem lép előre

### Requirement: Azonosító mezők
A partnernév MUST a kivonat `supplierName` értéke lenni. Az adószám MUST a kivonat `supplierTaxNumber` értéke lenni, pontosan 8 számjegy, szövegként. Áfakód és megyekód MUST NOT hozzákerülni. A számlaszám MUST a kivonat számlaszáma lenni, szövegként.

#### Scenario: Adószám
- **WHEN** a kivonat adószáma `12345678`
- **THEN** az `Adószám` cella a `12345678` szöveg, áfakód és megyekód nélkül

### Requirement: Bankszámlaszám
A bankszámlaszám MUST a teljes bizonylat `supplierBankAccountNumber` értéke lenni, szövegként. Hiányzó mező MUST üres cellát adni.

#### Scenario: Van számlaszám
- **WHEN** a teljes bizonylat bankszámlaszámot tartalmaz
- **THEN** a `Bankszámlaszám` cella ugyanezt a szöveget tartalmazza

#### Scenario: Nincs számlaszám
- **WHEN** a teljes bizonylat nem tartalmaz bankszámlaszámot
- **THEN** a `Bankszámlaszám` cella üres

### Requirement: Összegek
A fizetendő összeg MUST a teljes bizonylat `invoiceGrossAmount` értéke lenni. Az áfaösszeg MUST a kivonat `invoiceVatAmount` értéke lenni. A rendszer MUST a küldött értéket írni, a nullát és a negatívot is. Hiányzó mező MUST üres cella lenni. A rendszer MUST NOT hiányzó összegből másik összeget képezni. Az összegcellák MUST számcellák lenni, két tizedesjeggyel és előjellel.

#### Scenario: Negatív módosítás
- **WHEN** a NAV a fizetendő összegre `-1500` értéket küld
- **THEN** a `Fiz. összeg` cella `-1500.00`

#### Scenario: Hiányzó bruttó
- **WHEN** a teljes bizonylat nem tartalmaz `invoiceGrossAmount` értéket
- **THEN** a `Fiz. összeg` cella üres, és nem a nettó meg az áfa összege

### Requirement: Dátumcellák
A `Számla kelte` és a `Fiz. határidő` MUST Excel-dátum lenni, időrész nélkül. A határidő MUST a kivonat `paymentDate` értéke lenni, ennek hiányában a számla kelte.

#### Scenario: Van határidő
- **WHEN** a kivonat fizetési dátuma `2026-03-15`
- **THEN** a `Fiz. határidő` ennek a napnak a dátuma, időrész nélkül

#### Scenario: Nincs határidő
- **WHEN** a kivonat nem tartalmaz `paymentDate` értéket, és a számla kelte `2026-03-01`
- **THEN** a `Fiz. határidő` `2026-03-01`

### Requirement: Fizetési mód
A fizetési mód MUST a teljes bizonylat `paymentMethod` értéke lenni, ennek hiányában a kivonaté. `TRANSFER` MUST `Átutalás`, `CASH` MUST `Készpénz`, `CARD` MUST `Bankkártya`, `VOUCHER` MUST `Utalvány`, `OTHER` MUST `Egyéb` lenni. Hiányzó érték MUST üres cella lenni. Ismeretlen kód MUST a kapott NAV-szöveg maradni.

#### Scenario: Ismert kód
- **WHEN** a teljes bizonylat fizetési módja `CARD`
- **THEN** a `Fizetési mód` cella `Bankkártya`

#### Scenario: Ismeretlen kód
- **WHEN** a fizetési mód olyan kód, amely nincs a felsorolásban
- **THEN** a cella a kapott NAV-szöveget tartalmazza

### Requirement: Áfatartalom
Az áfatartalom MUST a tételek `lineVatRate` mezőiből, az első előfordulás sorrendjében, ismétlés nélkül, vesszővel és szóköz nélkül készülni. A százalék MUST a `vatPercentage` százszorosa lenni, `%` jellel. Mentességi case esetén a rendszer MUST a case kódot írni, nem a nulla százalékot. A mentesség indoklásának szövege MUST NOT a cellába kerülni.

#### Scenario: Ismételt kulcs és mentesség
- **WHEN** a tételek áfája rendre `0.27`, `0.27`, `AAM` és `TAM`
- **THEN** az `Áfa tartalom` cella `27%,AAM,TAM`

#### Scenario: Nulla kulcs
- **WHEN** a tétel `vatPercentage` értéke `0`, és nincs mentességi case
- **THEN** a lista ezt `0%` értékkel tartalmazza

### Requirement: Egyszerűsített áfatartalom
Egyszerűsített számla `vatContent` értékénél `0.2126` MUST `27%`, `0.1525` MUST `18%`, `0.0426` MUST `5%`, a `0` MUST `0%` lenni. Más tört MUST a küldött tizedesekkel százalékká alakulni. Ez a megfeleltetés MUST csak egyszerűsített számlára vonatkozni.

#### Scenario: Ismert tartalom
- **WHEN** az egyszerűsített tétel `vatContent` értéke `0.2126`
- **THEN** a lista `27%` értéket tartalmaz

#### Scenario: Más tört
- **WHEN** az egyszerűsített tétel `vatContent` értéke `0.1`
- **THEN** a lista a küldött tizedesekből képzett százalékot tartalmazza

#### Scenario: Normál számla
- **WHEN** a számla nem egyszerűsített, és a tétel `vatPercentage` értéke `0.27`
- **THEN** a lista `27%`, és a `vatContent` párosítás nem érvényesül

### Requirement: További áfajelölés
A hatályon kívüli eset és az áfaösszeg-eltérés MUST a NAV case kódját adni. A belföldi fordított adózás MUST `FAD` lenni. Az árrés MUST a NAV felsorolt értéke lenni. A `noVatCharge` MUST a `noVatCharge` szöveg lenni. Ezek MUST ugyanabba a vesszős listába kerülni, ismétlés nélkül, az első tételsorrend szerint.

#### Scenario: Vegyes jelölés
- **WHEN** a tételek között `FAD`, árrés `TRAVEL_AGENCY` és `noVatCharge` szerepel, a `FAD` pedig kétszer
- **THEN** a lista ezeket egyszer, az első előfordulás sorrendjében, vesszővel és szóköz nélkül tartalmazza

### Requirement: Bizonylatkapcsolat
A `Kapcsolt bizonylatok` mező MUST a módosító és sztornó számlaszámokat pontosvesszővel, szóköz nélkül tartalmazni. A meglévő sorrend MUST megmaradni, az új hivatkozás MUST a végére kerülni, és a már szereplő szám MUST NOT ismétlődni. Az `Eredeti számla` MUST csak az eredeti számlaszám lenni. A `CREATE`, a `MODIFY` és a `STORNO` MUST önálló sorba kerülni, a saját kelte szerinti lapra.

#### Scenario: Új módosító
- **WHEN** az eredeti `Kapcsolt bizonylatok` értéke `SZ-1`, és új módosító érkezik `SZ-2` számmal
- **THEN** a mező `SZ-1;SZ-2`, az `Eredeti számla` pedig az eredeti számlaszám

#### Scenario: Ismételt hivatkozás
- **WHEN** a `SZ-1` már szerepel a `Kapcsolt bizonylatok` mezőben
- **THEN** a mező nem kap második `SZ-1` értéket

### Requirement: Ismételt szelet kapcsolata
Az ismételt szelet MUST a már nyilvántartott módosító és sztornó hiányzó hivatkozását is az eredeti `Kapcsolt bizonylatok` mezőjének végére írni. Más cella MUST NOT változni. Ha az eredeti megvan, új hiányzó-eredeti bejegyzés MUST NOT készülni. A meglévő bejegyzés MUST megmaradni.

#### Scenario: A kapcsolat írása megszakadt
- **WHEN** a módosító sora már a nyilvántartásban van, és az eredeti `Kapcsolt bizonylatok` mezője még nem tartalmazza a módosító számát
- **THEN** az ismételt szelet a számot a mező végére írja, és más cellát nem változtat

#### Scenario: Az eredeti az ismétléskor megvan
- **WHEN** a hiányzó-eredeti bejegyzés már létezik, és az ismételt szelet megtalálja az eredetit
- **THEN** új bejegyzés nem készül, a meglévő megmarad, és a kapcsolat felkerül

### Requirement: Kezelt éven kívüli bizonylat
Kezelt éven kívüli keltezésnél a rendszer MUST NOT Excel-sort és éves fájlt írni. A szelet MUST ettől még sikeres lehet. A rendszer MUST egy tartós hibabejegyzést készíteni. Ismételt futás MUST NOT második bejegyzést készíteni ugyanarra az adószámra és számlaszámra.

#### Scenario: Régi kelte
- **WHEN** a bizonylat kelte nem az aktuális és nem az előző magyarországi naptári év
- **THEN** Excel-sor nem készül, egy hibabejegyzés készül, és a szelet ettől még lezárható

#### Scenario: Ismételt futás
- **WHEN** ugyanerre az adószámra és számlaszámra már készült éven kívüli hibabejegyzés
- **THEN** második bejegyzés nem készül

### Requirement: Éven kívüli bejegyzés tartalma
A bejegyzés MUST tartalmazni a partnernevet, az adószámot, a számlaszámot, a keltét, a fizetendő összeget, a határidőt, és azt, hogy a kelte a kezelt éveken kívül van. A fizetendő összeg MUST a küldött érték lenni, hiányában üres.

#### Scenario: Tartalom
- **WHEN** kezelt éven kívüli bizonylatról hibabejegyzés készül
- **THEN** a bejegyzés ezt a hat adatot és a kezelt éven kívüli kelte okát tartalmazza

### Requirement: Hiányzó eredeti
Ha az eredeti a keresés után nincs meg, a módosító vagy sztornó sora MUST elkészülni, az `Eredeti számla` MUST megkapni az eredeti számlaszámot, és külön hibabejegyzés MUST készülni. A szelet MUST sikeres maradni. A bejegyzés MUST NOT visszavonódni. Azonossága MUST a módosító vagy sztornó saját adószáma és számlaszáma lenni.

#### Scenario: Nincs eredeti
- **WHEN** a módosító eredeti számlája a keresés után sincs a nyilvántartásban
- **THEN** a módosító sora elkészül, az `Eredeti számla` az eredeti számlaszám, és egy hibabejegyzés készül

#### Scenario: Későbbi szelet sem vonja vissza
- **WHEN** egy későbbi szelet sem hozza be az eredetit
- **THEN** a hiányzó eredeti hibabejegyzése megmarad

### Requirement: Hiányzó eredeti tartalma
A bejegyzés MUST tartalmazni az eredeti számlaszámot és azt, hogy az eredeti nincs a nyilvántartásban. Ugyanabban a szeletben megtalált eredetinél a rendszer MUST NOT hibabejegyzést készíteni, és a kapcsolatot MUST feltenni.

#### Scenario: Ugyanazon szelet
- **WHEN** az eredeti és a módosító ugyanabban a szeletben rögzíthető
- **THEN** hibabejegyzés nem készül, és az eredeti `Kapcsolt bizonylatok` mezője megkapja a módosító számát

### Requirement: Hibabejegyzés határa
A hibabejegyzés a `notification` táblába kerül. A `Type` értéke `Error`, a `Message` a tartalom. Éven kívüli és hiányzó eredeti sor MUST a bizonylat adataival kitölteni a `Taxnumber`, a `Partnername` és az `Invoice` mezőt. Hibás ciklus sora MUST üres szöveggel írni ezt a három oszlopot. A `Notification` típus későbbi értesítéshez van. Külön időmező MUST NOT készülni. Küldési állapot, újrapróbálás és csatorna MUST NOT készülni.

#### Scenario: Számlaszintű tartalom
- **WHEN** éven kívüli vagy hiányzó eredeti hibabejegyzés készül
- **THEN** a sor `Type` értéke `Error`, a `Message` tartalmazza a tartalmat, a `Taxnumber`, a `Partnername` és az `Invoice` a bizonylat adata, külön időmező és küldési oszlop nélkül

#### Scenario: Ciklus üres azonossága
- **WHEN** hibás ciklus hibabejegyzés készül
- **THEN** a sor `Type` értéke `Error`, a `Message` a leállást tartalmazza, a `Taxnumber`, a `Partnername` és az `Invoice` üres szöveg, az oszlopok a sorban vannak, külön időmező és küldési oszlop nélkül

### Requirement: Rögzíthető művelet
A rendszer MUST a `CREATE`, a `MODIFY` és a `STORNO` bizonylatot rögzíteni. Az `ANNUL` MUST NOT számlasorként vagy bizonylatként bekerülni.

#### Scenario: Három művelet
- **WHEN** a kivonat `CREATE`, `MODIFY` és `STORNO` tételt tartalmaz
- **THEN** mindhárom a saját kelte szerint rögzíthető

#### Scenario: Annul
- **WHEN** a feldolgozás `ANNUL` művelettel találkozik
- **THEN** abból számlasor nem készül
