# Spec Delta

## Purpose

Éves SharePoint Excel-fájl és a kért havi munkalap biztosítása, a Paraméterek lap létrehozásával és az értékek évváltási átvételével.

## ADDED Requirements

### Requirement: Kért év és hónap
A rendszer MUST a hívó által megadott évre és hónapra biztosítani a munkafüzetet. A rendszer MUST NOT számlát olvasni, és MUST NOT számlasort írni.

#### Scenario: Hívó évvel és hónappal
- **WHEN** a hívó évet és hónapot ad át
- **THEN** a rendszer ennek az évnek a fájlját és ennek a hónapnak a lapját biztosítja, számla olvasása és számlasor írása nélkül

### Requirement: Kezelt évek
A rendszer MUST fájlt vagy havi lapot csak az aktuális és az előző naptári évre készíteni, magyarországi dátum szerint.

#### Scenario: Kezelt év
- **WHEN** a kért év az aktuális vagy az előző magyarországi naptári év
- **THEN** a rendszer létrehozhatja vagy kiegészítheti az év fájlját

#### Scenario: Kezelt éven kívül
- **WHEN** a kért év nem az aktuális és nem az előző magyarországi naptári év
- **THEN** a rendszer nem hoz létre fájlt, nem készít lapot, és a művelet sikertelen

### Requirement: Szélső whitespace levágása
A rendszer MUST a fájlnév, a munkalapnév, a fejléc, a paraméterkulcs és a paraméterérték szélső whitespace-ét az azonosítás és a másolás előtt levágni. A belső whitespace MUST megmaradni.

#### Scenario: Szóközös hónaplap
- **WHEN** a fájlban ` Május ` lap van, és a hívó májust kéri
- **THEN** a rendszer ezt a lapot használja, és nem hoz létre másik `Május` lapot

#### Scenario: Szóközös fejléc
- **WHEN** az első sorban a fejléc ` Partner neve `
- **THEN** a rendszer ezt a `Partner neve` oszlopnak tekinti, és nem szúr be másik oszlopot

#### Scenario: Belső szóköz a fejlécben
- **WHEN** az első sorban a fejléc `Partner  neve`, és a `Partner neve` nincs a sorban
- **THEN** a rendszer ezt nem a `Partner neve` oszlopnak tekinti

#### Scenario: Szóközös kulcs
- **WHEN** az előző fájl kulcsa ` InsDate `
- **THEN** a rendszer ezt az `InsDate` kulcsnak tekinti

### Requirement: Éves fájl neve és mappája
A rendszer MUST az éves fájlt `SZÁMLÁKYYYY.xlsx` néven, a konfigurált SharePoint-mappában keresni és létrehozni. A keresés a nagybetűs néven történik, az ékezetek megtartásával. A rendszer MUST NOT másik mappában keresni.

#### Scenario: 2026-os fájl
- **WHEN** a hívó a 2026-os évet kéri
- **THEN** a fájl neve `SZÁMLÁK2026.xlsx`, és a konfigurált mappában van

#### Scenario: Kisbetűs fájlnév
- **WHEN** a mappában a `számlák2026.xlsx` fájl van, és a hívó 2026-ot kéri
- **THEN** a rendszer ezt a fájlt használja, és nem hoz létre másik `SZÁMLÁK2026.xlsx` fájlt

#### Scenario: Ékezet nélküli fájlnév
- **WHEN** a mappában a `SZAMLAK2026.xlsx` fájl van, és a hívó 2026-ot kéri
- **THEN** a rendszer nem ezt a fájlt használja

#### Scenario: Előző év ugyanabban a mappában
- **WHEN** a rendszer a 2026-os fájl paramétereihez az előző év fájlját olvassa
- **THEN** a fájl neve `SZÁMLÁK2025.xlsx`, és ugyanabban a mappában van

### Requirement: Új éves fájl lapjai
A rendszer MUST a hiányzó éves fájlt a `Paraméterek` lappal és csak a kért havi lappal létrehozni. A rendszer MUST NOT a többi havi lapot létrehozni, és MUST NOT sablonfájlt használni.

#### Scenario: Első májusi kérés
- **WHEN** a `SZÁMLÁK2026.xlsx` fájl nincs meg, és a hívó 2026 májusát kéri
- **THEN** a fájlban a `Paraméterek` lap és a `Május` lap jön létre, más havi lap nem

### Requirement: Havi lap neve
A rendszer MUST a havi lapot a kért hónap magyar nevével elnevezni: `Január`, `Február`, `Március`, `Április`, `Május`, `Június`, `Július`, `Augusztus`, `Szeptember`, `Október`, `November`, `December`. Meglévő lapot a nagybetűs név alapján ismer fel, az ékezetek megtartásával, és az egyező lapot nem nevezi át.

#### Scenario: Május
- **WHEN** a hívó májust kéri, és a havi lap létrejön
- **THEN** a lap neve `Május`

#### Scenario: Kisbetűs hónaplap
- **WHEN** a fájlban `május` lap van, és a hívó májust kéri
- **THEN** a rendszer ezt a lapot használja, nem hoz létre `Május` lapot, és a `május` nevet nem írja át

#### Scenario: Ékezet nélküli hónaplap
- **WHEN** a fájlban `Majus` lap van, és a hívó májust kéri
- **THEN** a rendszer a `Majus` lapot nem májusnak tekinti, és létrehozza a `Május` lapot

### Requirement: Fejléc az első sor
A rendszer MUST a havi lap első sorába, ebben a sorrendben a `Partner neve`, `Adószám`, `Bankszámlaszám`, `Számlasorszám`, `Eredeti számla`, `Kapcsolt bizonylatok`, `Számla kelte`, `Fiz. határidő`, `Fiz. összeg`, `Áfa tartalom`, `Áfa összege`, `Fizetési mód`, `Számla megvan?`, `Fizetve?`, `Autó rendelés` és `Megjegyzés` oszlopneveket írni. A rendszer MUST NOT sort beszúrni a fejléc fölé.

#### Scenario: Új havi lap
- **WHEN** a rendszer havi lapot hoz létre
- **THEN** az első sor pontosan ez a tizenhat oszlopnév, a rögzített sorrendben, és alatta nincs adatsor

### Requirement: Oszlop azonosítása a fejlécnévvel
A rendszer MUST a havi lap oszlopait az első sor nagybetűs fejléce alapján azonosítani. Az ékezetek megmaradnak. Ismételt névnél az első egyezés az oszlop. Egyező fejlécet a rendszer nem nevez át, és MUST NOT második oszlopot beszúrni.

#### Scenario: Kisbetűs fejléc
- **WHEN** az első sor tartalmazza a `bankszámlaszám` nevet
- **THEN** a rendszer ezt a `Bankszámlaszám` oszlopnak tekinti, és nem szúr be másik `Bankszámlaszám` oszlopot

#### Scenario: Ismételt fejléc
- **WHEN** az első sorban a `Bankszámlaszám` név kétszer szerepel
- **THEN** az első egyezés a `Bankszámlaszám` oszlop, és a rendszer nem szúr be harmadik ilyen oszlopot

#### Scenario: Ékezet nélküli fejléc
- **WHEN** az első sor tartalmazza a `Bankszamlaszam` nevet, és a `Bankszámlaszám` nincs a sorban
- **THEN** a rendszer a `Bankszamlaszam` oszlopot nem a `Bankszámlaszám` oszlopnak tekinti

### Requirement: Hiányzó oszlop beszúrása
A rendszer MUST minden hiányzó elfogadott oszlopot beszúrni, akár mind a tizenhat hiányzik. A művelet emiatt MUST sikeres maradni. A tőle jobbra lévő oszlopok a saját fejlécükkel és adataikkal együtt csúsznak el. Az új oszlopban csak a fejléc készül el. A rendszer MUST NOT meglévő oszlopot átnevezni, törölni vagy átrendezni.

#### Scenario: Bankszámlaszám hiányzik
- **WHEN** az első sorban a `Partner neve` és az `Adószám` után a `Számlasorszám` áll, és a `Bankszámlaszám` nincs a sorban
- **THEN** a `Bankszámlaszám` az `Adószám` és a `Számlasorszám` közé kerül, a `Számlasorszám` oszlopa jobbra csúszik, és az új oszlopban csak a fejléc van kitöltve

#### Scenario: Idegen oszlop a kanonikusak között
- **WHEN** az első sor `Partner neve`, `Adószám`, `Saját`, `Számlasorszám`, és a `Bankszámlaszám` nincs a sorban
- **THEN** a `Bankszámlaszám` a `Saját` és a `Számlasorszám` közé kerül, a `Saját` oszlop a helyén marad

#### Scenario: Egyetlen elfogadott fejléc sincs
- **WHEN** a meglévő havi lap első sora egyetlen elfogadott oszlopnevet sem tartalmaz
- **THEN** a rendszer a tizenhat oszlopot a rögzített sorrendben a sor elejére szúrja be, a meglévő cellák jobbra csúsznak, és a művelet sikeres

### Requirement: Meglévő fájl megőrzése
A rendszer MUST a már létező éves fájl meglévő munkalapjait felülírás nélkül meghagyni. Hiányzó elfogadott oszlop beszúrása ettől függetlenül megtörténhet a kért havi lapon.

#### Scenario: Létező májusi lap
- **WHEN** a `SZÁMLÁK2026.xlsx` és a `Május` lap már létezik, és a hívó 2026 májusát kéri
- **THEN** a rendszer nem hozza létre újra a fájlt, és a `Május` lap meglévő celláit nem cseréli le

### Requirement: Hiányzó havi lap
A rendszer MUST a létező éves fájlban csak a hiányzó, kért havi lapot létrehozni.

#### Scenario: Csak a május hiányzik
- **WHEN** a `SZÁMLÁK2026.xlsx` létezik, a `Május` lap nincs meg, és a hívó 2026 májusát kéri
- **THEN** a rendszer létrehozza a `Május` lapot, és más havi lapot nem hoz létre

### Requirement: Paraméterek kulcsai
A rendszer MUST a `Paraméterek` lapon az `InsDate` és az `Előértesítési napok` kulcsot használni. Az A oszlop a kulcs neve, a B oszlop az érték. Új lapon az első sor az `InsDate`, a második az `Előértesítési napok`. A rendszer MUST a kulcsot a nagybetűs neve alapján megtalálni, az ékezetek megtartásával. Ismételt kulcsnál az első egyezés értéke kerül át, és a rendszer MUST NOT második sort írni az új lapra.

#### Scenario: Új Paraméterek lap
- **WHEN** a rendszer `Paraméterek` lapot hoz létre
- **THEN** az A1 cella `InsDate`, az A2 cella `Előértesítési napok`, és az értékek a B oszlopban vannak

#### Scenario: Felcserélt sorok
- **WHEN** az előző fájl `Paraméterek` lapján az `Előértesítési napok` az `InsDate` fölött áll
- **THEN** a rendszer mindkét értéket a kulcs neve alapján olvassa

#### Scenario: Ismételt kulcs
- **WHEN** az előző fájlban az `InsDate` kulcs kétszer szerepel
- **THEN** az új lapra az első egyezés értéke kerül, és csak egy `InsDate` sor készül

#### Scenario: Kisbetűs kulcs
- **WHEN** az előző fájl kulcsa `insdate`
- **THEN** a rendszer ezt az `InsDate` kulcsnak tekinti, és nem ír alapértelmezést helyette

#### Scenario: Ékezet nélküli kulcs
- **WHEN** az előző fájl kulcsa `Eloertesitesi napok`, és az `Előértesítési napok` nincs a lapon
- **THEN** a rendszer ezt nem az `Előértesítési napok` kulcsnak tekinti

### Requirement: Paraméterek átvétele
A rendszer MUST az új `Paraméterek` lap értékeit a `SZÁMLÁK{év-1}.xlsx` fájlból, kulcsnév szerint átvenni, akkor is, ha az előző év kívül esik a két kezelt éven. A rendszer MUST NOT ezt a fájlt létrehozni. A csak whitespace-ből álló érték hiány. A többi értéket a rendszer a szélső whitespace nélkül, egyébként változtatás nélkül MUST másolni.

#### Scenario: 2024-es folytatási pont
- **WHEN** a magyarországi dátum 2026-ra esik, a `SZÁMLÁK2025.xlsx` nincs meg, a `SZÁMLÁK2024.xlsx` megvan, és a hívó 2025-öt kéri
- **THEN** a rendszer a 2024-es fájlból veszi át a kitöltött értékeket, és nem hozza létre a `SZÁMLÁK2024.xlsx` fájlt

#### Scenario: Változatlan másolt érték
- **WHEN** az előző fájl `InsDate` értéke `  2024-06-01T00:00:00Z  `
- **THEN** az új lap értéke `2024-06-01T00:00:00Z`

### Requirement: Hiányzó paraméter alapértelmezése
A rendszer MUST hiányzó előző fájl, hiányzó kulcs, nulla hosszú vagy csak whitespace-ből álló érték helyett alapértelmezést írni. Az `InsDate` az előző év december 31-ének szöveges UTC időpontja. Az `Előértesítési napok` értéke `7,5,1`.

#### Scenario: Nincs előző fájl
- **WHEN** a 2026-os fájl létrejön, és a `SZÁMLÁK2025.xlsx` nincs meg
- **THEN** az `InsDate` értéke `2025-12-31T00:00:00Z`, az `Előértesítési napok` értéke `7,5,1`

#### Scenario: Üres kulcs
- **WHEN** az előző fájlban az `InsDate` kulcs megvan, és az értéke üres
- **THEN** az új lap `InsDate` értéke az előző év december 31-ének szöveges UTC időpontja

#### Scenario: Csak whitespace
- **WHEN** az előző fájl `Előértesítési napok` értéke szóközökből áll
- **THEN** az új lap értéke `7,5,1`

### Requirement: Meglévő Paraméterek lap
A rendszer MUST a már létező `Paraméterek` lapot változatlanul hagyni. Ha az éves fájl létezik, de a `Paraméterek` lapja nincs meg, a rendszer MUST csak ezt a lapot létrehozni, az átvételi és alapértelmezési szabály szerint.

#### Scenario: Meglévő lap
- **WHEN** az éves fájl `Paraméterek` lapja már létezik
- **THEN** a rendszer a lap egyetlen celláját sem módosítja

#### Scenario: Hiányzó lap egy létező fájlban
- **WHEN** a `SZÁMLÁK2026.xlsx` létezik, és nincs `Paraméterek` lapja
- **THEN** a rendszer létrehozza a `Paraméterek` lapot, és a meglévő havi lapokat nem cseréli le

### Requirement: Verziókezelt Excel-cím
A rendszer MUST a mappát a verziókezelt `Excel` szakasz `SiteHost`, `SitePath` és `FolderPath` értékéből meghatározni. A `FolderPath` a webhely alapértelmezett dokumentumtárának gyökerétől értendő. A verziókezelt szakasz MUST NOT Graph-elem GUID-ot tartalmazni. Hiányzó `FolderPath` mappa esetén a művelet MUST sikertelen lenni, és a rendszer MUST NOT létrehozni a mappát.

#### Scenario: Mappa címe
- **WHEN** a rendszer éves fájlt keres vagy hoz létre
- **THEN** a `SiteHost`, a `SitePath` és a `FolderPath` verziókezelt beállítás, és egyik értéke sem titok

#### Scenario: Hiányzó mappa
- **WHEN** a `FolderPath` mappa nincs a dokumentumtárban
- **THEN** a művelet sikertelen, a mappa nem jön létre, és éves fájl sem keletkezik

### Requirement: Graph-belépés és titkok
A rendszer MUST saját jogon, felhasználó nélkül bejelentkezni. A rendszer MUST az `ExcelTenantId`, az `ExcelClientId` és az `ExcelClientSecret` kulcsot a `Secrets` szakaszból használni.

#### Scenario: Felhasználó nélkül
- **WHEN** a rendszer a SharePoint-mappát éri el
- **THEN** a belépéshez nem kér felhasználói bejelentkezést

#### Scenario: Az érték nincs verziókezelt fájlban
- **WHEN** a verziókezelt konfiguráció az `Excel` és a `Secrets` szakaszt tartalmazza
- **THEN** az `ExcelTenantId`, az `ExcelClientId` és az `ExcelClientSecret` értéke nincs a fájlban

### Requirement: Csak a beállított dokumentumtár
A rendszer MUST a `Lists.SelectedOperations.Selected` alkalmazásjogosultságot írási joggal, csak a webhely alapértelmezett dokumentumtárára használni. A rendszer MUST a tárban csak a `FolderPath` mappát használni.

#### Scenario: Más tár
- **WHEN** a webhelyen az alapértelmezett dokumentumtáron kívül másik tár vagy lista is van
- **THEN** a rendszer azt nem éri el

#### Scenario: Csak a beállított mappa
- **WHEN** a dokumentumtárban a `FolderPath` mappán kívül másik mappa is van
- **THEN** a rendszer abban nem keres és nem hoz létre fájlt

### Requirement: Újrapróbálás átmeneti Graph-hiba után
A rendszer MUST a hálózati hiba, az időtúllépés, a HTTP 429 és a HTTP 5xx után a hívást az `Excel` szakasz `RetryCount` értékének megfelelő számú alkalommal újrapróbálni, két próbálkozás között `RetryDelaySeconds` másodpercet várva. Ezek a paraméterek MUST a `Nav` szakasztól függetlenek lenni.

#### Scenario: Verziókezelt kezdőérték
- **WHEN** a verziókezelt `Excel` szakasz a kezdőértékeket tartalmazza
- **THEN** a `RetryCount` értéke `3`, a `RetryDelaySeconds` értéke `5`

#### Scenario: Három újrapróbálás
- **WHEN** az első Graph-hívás átmeneti kommunikációs hiba miatt meghiúsul, és a `RetryCount` értéke `3`
- **THEN** a rendszer ugyanazt a kérést még háromszor elküldi, a próbálkozások között `RetryDelaySeconds` másodperc szünettel

### Requirement: Zárolt munkafüzet nem indul újra
A rendszer MUST a zárolt vagy ütköző munkafüzetet újrapróbálás nélkül sikertelen műveletként visszaadni. A már meglévő lapok MUST megmaradni.

#### Scenario: A felhasználó megnyitotta a fájlt
- **WHEN** a Graph a munkafüzet zárolása vagy írási ütközés miatt utasítja el a hívást
- **THEN** a rendszer nem küldi újra a kérést, a művelet sikertelen, és a meglévő lapok változatlanok
