# Spec Delta

## Purpose

A fizetési teendők, a bizonylatértesítések és a hibajelzések összeállítása, valamint a Slackra szánt sorok tárolása a notification táblában.

## ADDED Requirements

### Requirement: Összeállító futás
Az értesítés-összeállítás MUST a kezelt évek nyilvántartásából fizetési, hiba- és üresnapi sort írni a `notification` táblába. Éles időzítő és élesben hívható végpont MUST NOT készülni. A futás MUST NOT Slack-üzenetet küldeni, MUST NOT NAV-ot hívni, és MUST NOT Excel-fájlt létrehozni vagy módosítani.

#### Scenario: Teszt vagy helyi indítás
- **WHEN** teszt vagy helyi Development indítás elindítja az értesítés-összeállítást
- **THEN** a futás sort írhat a `notification` táblába, időzítő és éles végpont nélkül, Slack-üzenet és NAV-hívás nélkül, és az Excel-fájlok változatlanok maradnak

### Requirement: Küldési időpont
A futás MUST NOT írni és MUST NOT olvasni a `NotificationTimestamp` mezőt. Küldési állapot, újrapróbálás és csatorna MUST NOT készülni. A futás MUST NOT meglévő sort törölni vagy felülírni.

#### Scenario: Új fizetési sor
- **WHEN** a futás fizetési sort ír
- **THEN** a `NotificationTimestamp` üres marad, és küldési állapot, újrapróbálás vagy csatorna nem készül

### Requirement: Bizonylatértesítés tartalma
A `Document` sor `Message` értéke `MODIFY` esetén MUST `{eredeti számlaszám} módosítása érkezett, számlaszám: {számlaszám}` lenni. `STORNO` esetén MUST `{eredeti számlaszám} sztornózás érkezett, számlaszám: {számlaszám}` lenni. A `Taxnumber`, a `Partnername` és az `Invoice` MUST a bizonylat adata lenni. Az `Invoice` MUST a saját számlaszám lenni. Az eredeti számlaszám MUST csak a `Message` mezőben szerepelni.

#### Scenario: Módosító szövege
- **WHEN** egy `MODIFY` bizonylat `Document` sora a táblában van, az eredeti szám `E-1`, a saját szám `SZ-2`
- **THEN** a `Type` értéke `Document`, a `Message` értéke `E-1 módosítása érkezett, számlaszám: SZ-2`, az `Invoice` értéke `SZ-2`

#### Scenario: Sztornó szövege
- **WHEN** egy `STORNO` bizonylat `Document` sora a táblában van, az eredeti szám `E-1`, a saját szám `SZ-3`
- **THEN** a `Message` értéke `E-1 sztornózás érkezett, számlaszám: SZ-3`, az `Invoice` értéke `SZ-3`

### Requirement: Bizonylatértesítés keletkezése
Az összeállítás MUST NOT `Document` sort írni. A már tárolt bizonylat MUST NOT utólagos `Document` sort kapni. Meglévő `Document` sor MUST NOT ismétlődni. A `CREATE` MUST NOT bizonylatértesítést kapni. A `Notification` típus MUST NOT ezekhez a sorokhoz készülni.

#### Scenario: Már tárolt módosító
- **WHEN** a nyilvántartásban `MODIFY` sor van, és hozzá nincs `Document` sor
- **THEN** az összeállítás nem ír `Document` sort

#### Scenario: Új számla
- **WHEN** a nyilvántartásban `CREATE` sor van
- **THEN** `Document` sor nem készül

### Requirement: Sztornójel olvasása
A `Kapcsolt bizonylatok` pontosvesszővel elválasztott eleme közül a `STORNO:` előtaggal kezdődő elem sztornó számlaszám. A számlaszám az előtag utáni rész, a kis- és nagybetű megtartásával. Előtag nélküli elem MUST NOT sztornó lenni. Az összeállítás MUST NOT átírni a cellát.

#### Scenario: Előtagos elem
- **WHEN** a mező `SZ-1;STORNO:SZ-3`
- **THEN** a `SZ-3` sztornó, a `SZ-1` nem sztornó

#### Scenario: Régi előtag nélküli elem
- **WHEN** a mező csak `SZ-3`
- **THEN** a `SZ-3` nem sztornó, és a cella változatlan marad

### Requirement: Sztornólánc
Ha egy eredeti sor `Kapcsolt bizonylatok` mezője sztornó elemet tartalmaz, fizetési sor MUST NOT készülni az eredetiről, a mezőben szereplő számlákról és azokról a sorokról, amelyek `Eredeti számla` értéke ez az eredeti, azonos adószám mellett. Előtag nélküli lista MUST NOT sztornólánc lenni. A láncon kívüli `MODIFY` a saját határidejével és `Fizetve?` értékével kaphat fizetési sort.

#### Scenario: Sztornó a listában
- **WHEN** az eredeti mezője `SZ-1;STORNO:SZ-3`, és az eredeti, a `SZ-1` és a `SZ-3` is átutalásos, kiegyenlítetlen számla
- **THEN** egyikről sem készül fizetési sor

#### Scenario: Régi lista előtag nélkül
- **WHEN** az eredeti mezője `SZ-1;SZ-3`, és nincs `STORNO:` elem
- **THEN** a lista nem sztornólánc, és a kiegyenlítetlen átutalásos sor fizetési sort kaphat

#### Scenario: Módosító sztornó nélkül
- **WHEN** egy `MODIFY` saját határideje esedékes, és az eredeti listájában nincs sztornó elem
- **THEN** a `MODIFY` fizetési sort kaphat

### Requirement: Fizetési mód
Fizetési sor MUST csak akkor készülni, ha a `Fizetési mód` szélső whitespace nélkül pontosan `Átutalás`. Más mód MUST NOT fizetési sort kapni.

#### Scenario: Átutalás
- **WHEN** a `Fizetési mód` értéke ` Átutalás `
- **THEN** a számla fizetési sort kaphat

#### Scenario: Készpénz
- **WHEN** a `Fizetési mód` értéke `Készpénz`
- **THEN** fizetési sor nem készül

### Requirement: Fizetési szöveg
A fizetési sor `Type` értéke MUST `Payment` lenni. A `Message` MUST `{számlaszám} fizetés esedékes {fizetési határidő}, összeg: {számla bruttó összeg}` lenni, előértesítésnél, határidőnapon és lejárt tételnél is.

#### Scenario: Azonos szöveg
- **WHEN** ugyanaz a számla előértesítésként, határidőnapon vagy lejárt tételként kerül sorba
- **THEN** a `Message` mindhárom esetben ugyanezt a mintát követi

### Requirement: Összeg és határidő
A dátum MUST `yyyy.MM.dd` lenni, és a tényleges `Fiz. határidő`, ennek hiányában a `Számla kelte`. Ha egyik sem dátum, fizetési sor MUST NOT készülni. Az összeg a `Fiz. összeg`, két tizedes, tizedesvessző, ezres tagolás és pénznem nélkül. A mínusz előjel MUST megmaradni. Hiányzó összegnél a sor MUST megmaradni, az összeg helye üres.

#### Scenario: Határidő és összeg
- **WHEN** a számlaszám `SZ-1`, a határidő 2026-10-30, a `Fiz. összeg` 1500,5
- **THEN** a `Message` értéke `SZ-1 fizetés esedékes 2026.10.30, összeg: 1500,50`

#### Scenario: Negatív és hiányzó összeg
- **WHEN** a `Fiz. összeg` `-1500`, majd egy másik számlánál üres
- **THEN** az első összeg `-1500,00`, a második sor megmarad, és az összeg helye üres

#### Scenario: Üres határidő
- **WHEN** a `Fiz. határidő` üres, a `Számla kelte` 2026-03-01
- **THEN** a szöveg dátuma `2026.03.01`

### Requirement: Fizetési ismétlés
Egy számlának egy magyarországi naptári napon MUST egy fizetési sora lenni. A következő esedékes napon új sor MUST készülni akkor is, ha az előző sor `NotificationTimestamp` értéke üres. A futás ezt a mezőt MUST NOT használni a döntéshez. Kihagyott napra MUST NOT pótló sor készülni.

#### Scenario: Ugyanaz a nap
- **WHEN** a számlának a mai magyar napon már van fizetési sora
- **THEN** második fizetési sor nem készül

#### Scenario: Következő esedékes nap
- **WHEN** a számla tegnap fizetési sort kapott, a sor még nincs kiküldve, és ma is esedékes
- **THEN** ma új fizetési sor készül

#### Scenario: Kihagyott nap
- **WHEN** a hétfői futás elmaradt, és a számla kedden is esedékes
- **THEN** csak a keddi sor készül

### Requirement: Hétvége
Szombati és vasárnapi futás MUST NOT fizetési sort írni. A hétvégére eső határidő és előértesítés a következő hétfő sorába kerül. Ünnepnapnaptár MUST NOT készülni. A határidő cellája MUST NOT változni. Hiba- és bizonylatsor hétvégén is megmaradhat.

#### Scenario: Szombati futás
- **WHEN** a futás szombaton indul, és van lejárt átutalásos számla
- **THEN** fizetési sor nem készül

#### Scenario: Vasárnapi határidő
- **WHEN** a határidő vasárnap, és a következő futás hétfő
- **THEN** a hétfői fizetési sor a vasárnapi határidő dátumát tartalmazza, a cella pedig változatlan

### Requirement: Esedékes napok
A fizetési sor a határidő előtti előértesítési naptári napon, a határidő napján és minden későbbi hétköznapon készül, amíg a számla kiegyenlítetlen. Több, ugyanarra a hétfőre eső jelzés MUST egy sort adni. A szöveg dátuma a tényleges határidő.

#### Scenario: Előértesítés
- **WHEN** a határidő péntek, az előértesítés 1 nap, és a futás csütörtök
- **THEN** egy fizetési sor készül, a pénteki határidő dátumával

#### Scenario: Lejárt hétköznap
- **WHEN** a határidő hétfő volt, a számla kedden még kiegyenlítetlen, és a futás kedden indul
- **THEN** egy keddi fizetési sor készül

#### Scenario: Egy hétfő, több jelzés
- **WHEN** a határidő vasárnap, és az 1 napos előértesítés szombat
- **THEN** hétfőn egy fizetési sor készül

### Requirement: Kiegyenlítettség
A számla csak akkor kiegyenlített, ha a `Fizetve?` értéke pontosan `FIZETVE`. Üres cella kiegyenlítetlen. Kiegyenlített számla MUST NOT fizetési sort kapni.

#### Scenario: Fizetve
- **WHEN** a `Fizetve?` értéke `FIZETVE`, és a határidő ma esedékes
- **THEN** fizetési sor nem készül

#### Scenario: Üres cella
- **WHEN** a `Fizetve?` cella üres, és a számla egyébként esedékes átutalás
- **THEN** fizetési sor készül

### Requirement: Hibás Fizetve
Ha a `Fizetve?` nem üres és nem pontosan `FIZETVE`, a számla kiegyenlítetlen, és egyszeri `Error` sor MUST készülni. A `Message` MUST `{számlaszám} Fizetve? értéke hibás: {érték}. Kiegyenlítetlenként kezeljük.` lenni. Átutalásnál a fizetési sor is elkészül. Más módnál csak a hiba sor készül. Sztornóláncnál fizetési sor nincs, a hiba sor van. Ugyanarra a számlára második hiba sor MUST NOT készülni.

#### Scenario: Hibás érték átutalásnál
- **WHEN** a `Fizetve?` értéke `igen`, a mód `Átutalás`, és a számla esedékes
- **THEN** a hiba `Message` értéke `SZ-1 Fizetve? értéke hibás: igen. Kiegyenlítetlenként kezeljük.`, és fizetési sor is készül

#### Scenario: Hibás érték sztornóláncban
- **WHEN** a számla sztornóláncban van, és a `Fizetve?` értéke `igen`
- **THEN** hiba sor készül, fizetési sor nem készül

#### Scenario: Ismételt futás
- **WHEN** ugyanerre a számlára már készült hibás `Fizetve?` sor
- **THEN** második hiba sor nem készül

### Requirement: Számla megvan
A `Számla megvan?` cella bármely értéke MUST NOT értesítést kapni, és MUST NOT befolyásolni a fizetési sort.

#### Scenario: Üres és kitöltött cella
- **WHEN** az egyik esedékes számla `Számla megvan?` cellája üres, a másiké `IGEN`
- **THEN** emiatt egyikhez sem készül sor, és mindkettő a fizetési szabály szerint kerülhet fizetési sorba

### Requirement: Előértesítési napok
Az előértesítés MUST csak a tárgyévi `Előértesítési napok` értékét használni, mindkét kezelt év számláira. Üres, csak whitespace, hiányzó kulcs vagy hiányzó `Paraméterek` lap MUST a `7,5,1` értéket jelenteni, és ez MUST NOT hiányzó fájl legyen. Érvényes érték vesszővel elválasztott, 1 vagy nagyobb egész, szélső whitespace nélkül. Az ismétlődő szám egyszer számít.

#### Scenario: Üres tárgyévi érték
- **WHEN** a tárgyévi érték üres, az előző év értéke `3`
- **THEN** mindkét év számláin a 7, az 5 és az 1 napos előértesítés érvényes

#### Scenario: Ismétlődő szám
- **WHEN** az érték `7, 7, 1`
- **THEN** a 7 és az 1 napos előértesítés érvényes

### Requirement: Hibás előértesítés
Nulla, negatív vagy nem egész érték mellett előértesítés MUST NOT készülni. A határidőnapi és a lejárt sor mindkét évben MUST elkészülni. Egy egyszeri `Error` sor MUST készülni, üres azonossággal. A `Message` MUST `Az Előértesítési napok értéke hibás: {érték}.` lenni. Ismételt futás MUST NOT második sort írni.

#### Scenario: Nulla a listában
- **WHEN** az érték `7,0,1`, és egy számla határidőnapja ma van
- **THEN** előértesítés nem készül, a határidőnapi fizetési sor elkészül, és egy hiba sor készül a `7,0,1` értékkel

#### Scenario: Ismételt futás
- **WHEN** ugyanez a hibás érték már kapott hiba sort
- **THEN** második hiba sor nem készül

### Requirement: Hiányzó tárgyévi fájl
Hiányzó tárgyévi `SZÁMLÁK{év}.xlsx` esetén egy egyszeri `Error` sor MUST készülni, és a futás MUST megállni. A `Message` MUST `A tárgyévi nyilvántartás hiányzik: SZÁMLÁK{év}.xlsx. A feldolgozás megállt.` lenni. Az azonosság üres. Más sor MUST NOT készülni. A fájl MUST NOT létrejönni.

#### Scenario: Nincs tárgyévi fájl
- **WHEN** a 2026-os fájl nincs meg
- **THEN** egy hiba sor készül a `SZÁMLÁK2026.xlsx` névvel, más sor nem készül, és a fájl nem jön létre

#### Scenario: Ismételt futás
- **WHEN** ugyanez a hiányzó fájl már kapott hiba sort
- **THEN** második hiba sor nem készül

### Requirement: Előző évi fájl
Az indulás éve a `Processing:StartDate` naptári éve. Ebben az évben a hiányzó előző évi fájl MUST NOT hiba lenni. Ha a fájl megvan, a futás MUST a számláit is feldolgozni. Későbbi évben a hiányzó előző évi fájl egy egyszeri `Error` sor, és a futás megáll. A `Message` MUST `Az előző évi nyilvántartás hiányzik: SZÁMLÁK{év}.xlsx. A feldolgozás megállt.` lenni. Az azonosság üres. Más sor nem készül, és a fájl nem jön létre.

#### Scenario: Indulás éve
- **WHEN** a kezdődátum `2026-03-01`, a futás 2026-ban van, és a 2025-ös fájl nincs meg
- **THEN** emiatt hiba sor nem készül, a 2026-os számlák feldolgozhatók

#### Scenario: Későbbi év
- **WHEN** a futás 2027-ben van, és a 2026-os fájl nincs meg
- **THEN** egy hiba sor készül a `SZÁMLÁK2026.xlsx` névvel, más sor nem készül, és a fájl nem jön létre

#### Scenario: Meglévő előző év az indulás évében
- **WHEN** az indulás évében az előző évi fájl megvan
- **THEN** annak a számlái is feldolgozhatók

### Requirement: Hibás kezdődátum
Hiányzó, üres vagy nem `yyyy-MM-dd` kezdődátum esetén egy egyszeri `Error` sor MUST készülni, és a futás MUST megállni Excel-olvasás nélkül. A `Message` MUST `A kezdődátum hiányzik vagy hibás.` lenni. Az azonosság üres. Más sor MUST NOT készülni.

#### Scenario: Hibás érték
- **WHEN** a kezdődátum `2026.03.01`
- **THEN** egy hiba sor készül, Excel nem olvasódik, és más sor nem készül

#### Scenario: Ismételt futás
- **WHEN** ugyanez a hiba már kapott sort
- **THEN** második hiba sor nem készül

### Requirement: Olvasás a sorok előtt
A meglévő évi fájlok olvasása MUST befejeződni, mielőtt más sor íródna. Olvasási hibánál csak egy `Error` sor MUST készülni. A `Message` MUST `A nyilvántartás olvasása megállt: SZÁMLÁK{év}.xlsx.` lenni. Az azonosság üres. `Payment` és `Idle` sor MUST NOT készülni. Ha a tárgyévi olvasás hibás, a szöveg a tárgyévi fájlt nevezi. Egyébként az előző évit. Ugyanazon a magyar napon második sor MUST NOT készülni. A következő napon, ha a hiba megmarad, új sor MUST készülni.

#### Scenario: Egy fájl olvashatatlan
- **WHEN** az előző évi fájl megvan, de az olvasása sikertelen, a tárgyévi olvasás sikerül
- **THEN** egy hiba sor készül az előző évi fájl nevével, fizetési és üresnapi sor nem készül

#### Scenario: Mindkét fájl olvashatatlan
- **WHEN** a tárgyévi és az előző évi olvasás is sikertelen
- **THEN** egy hiba sor készül, és a szöveg a tárgyévi fájlt nevezi

#### Scenario: Következő nap
- **WHEN** a hiba ma már kapott sort, és holnap az olvasás még sikertelen
- **THEN** ma második sor nem készül, holnap új sor készül

### Requirement: Üres hétköznap
Hétfőtől péntekig, ha az aznapi üzenetek között nincs `Payment` tétel, és aznapi fizetési sor sincs a táblában, egy `Idle` sor MUST készülni. A `Message` MUST `mára nincs teendő, igyál egy kávét` lenni. Naponta egy sor készül, üres azonossággal. `Document` és `Error` sor ettől megmaradhat. Hétvégén, hiányzó fájlnál, hibás kezdődátumnál és olvasási hibánál ilyen sor MUST NOT készülni. Régebbi, kiküldetlen fizetési sor MUST NOT tölteni a napot.

#### Scenario: Nincs fizetési tétel
- **WHEN** hétfői futásban csak egy `Error` sor készül, és aznapi fizetési sor nincs
- **THEN** egy `Idle` sor készül a megadott szöveggel, és az `Error` sor megmarad

#### Scenario: Van aznapi fizetési sor
- **WHEN** a táblában már van a mai napra fizetési sor
- **THEN** `Idle` sor nem készül

#### Scenario: Megállás
- **WHEN** a tárgyévi fájl hiányzik
- **THEN** `Idle` sor nem készül

### Requirement: Sor mezői
Számlaszintű sor MUST kitölteni a `Taxnumber`, a `Partnername` és az `Invoice` mezőt a nyilvántartás sora szerint. Az adószám és a számlaszám csak a szélső whitespace-t veszíti el, a kis- és nagybetű megmarad. Fájl-, kezdődátum-, olvasási, előértesítés- és `Idle` sorban ez a három mező MUST üres szöveg lenni.

#### Scenario: Fizetési sor mezői
- **WHEN** a számla partnere `Kiss Anna`, az adószáma `12345678`, a száma `Ab-1`
- **THEN** a fizetési sor `Taxnumber`, `Partnername` és `Invoice` értéke ezek az értékek

#### Scenario: Üres azonosság
- **WHEN** `Idle` vagy fájlhiba sor készül
- **THEN** a `Taxnumber`, a `Partnername` és az `Invoice` üres szöveg

### Requirement: Meglévő hibasor
A számlafeldolgozás által már írt `Error` sor MUST megmaradni. Az összeállítás MUST NOT átírni és MUST NOT megismételni.

#### Scenario: Éven kívüli hiba
- **WHEN** a táblában már van a számlafeldolgozás éven kívüli `Error` sora
- **THEN** az összeállítás nem írja át, és nem készít belőle második sort

### Requirement: Kezelt évek
A futás MUST csak az aktuális és az előző magyarországi naptári év fájlját olvasni. Más év számlája MUST NOT sort kapni.

#### Scenario: Két év
- **WHEN** a mai magyar dátum 2026-ban van
- **THEN** a futás a `SZÁMLÁK2026.xlsx` és a `SZÁMLÁK2025.xlsx` fájlt kezeli, más évi fájlt nem
