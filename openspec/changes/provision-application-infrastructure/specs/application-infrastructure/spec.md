# Spec Delta

## Purpose

Az OsaPro és a közös Slack-küldő saját Azure-erőforrásainak, Excel-alkalmazásengedélyének és konfigurált SharePoint-mappájának létrehozása, ellenőrzése és azonosítóhoz kötött lebontása.

## ADDED Requirements

### Requirement: Két hoszt
A létrehozás MUST az OsaPro és a közös Slack-küldő Function Appot a projekt Flex Consumption, .NET 10 isolated worker hosztján létrehozni, always-ready példány nélkül.

#### Scenario: Mindkét hoszt létrejön
- **WHEN** a létrehozás sikeres
- **THEN** létezik az OsaPro hoszt és a közös Slack-küldő hoszt, always-ready példány nélkül

### Requirement: Közös telemetria
A létrehozás MUST egy közös Application Insightshoz kötni mindkét hosztot. A két hoszt telemetriája MUST megkülönböztethető legyen. A kapcsolat értéke MUST NOT kerüljön a leltárba vagy a naplóba.

#### Scenario: Két hoszt egy telemetrián
- **WHEN** mindkét hoszt létrejött
- **THEN** mindkettő ugyanahhoz az Application Insightshoz kapcsolódik, a hosztok telemetriája külön azonosítható, és a kapcsolat értéke nincs a leltárban

### Requirement: Névképzés
Az erőforrásnév MUST az `osp` előtagból és a bemenet `dev` vagy `prod` környezetparaméteréből képződni. Más környezetérték MUST erőforrás létrehozása nélkül megállítani a műveletet. A storage-név MUST kisbetű és szám legyen. A SharePoint-mappa neve MUST a meglévő `FolderPath` maradni.

#### Scenario: Dev környezet
- **WHEN** a környezetparaméter `dev`
- **THEN** az OsaPro erőforráscsoport neve `osp-dev-rg`, a hoszt storage neve `ospdevst`

#### Scenario: Prod környezet
- **WHEN** a környezetparaméter `prod`
- **THEN** az OsaPro erőforráscsoport neve `osp-prod-rg`, a hoszt storage neve `ospprodst`

#### Scenario: Érvénytelen környezet
- **WHEN** a környezetparaméter sem `dev`, sem `prod`
- **THEN** erőforrás nem készül

### Requirement: Storage szétválasztás
A hoszt és a telepítési csomag tárolója MUST külön erőforrás legyen az egyszeri értesítések Table Storage-ától.

#### Scenario: Két tároló
- **WHEN** a létrehozás sikeres
- **THEN** a hoszt tárolója és az értesítési állapot tárolója két külön erőforrás

### Requirement: Excel alkalmazásengedély
A futó OsaPro alkalmazás MUST a `Sites.Read.All` és a `Files.ReadWrite.All` alkalmazásengedélyt megkapni, admin consenttel. Selected engedély MUST NOT helyettesíteni ezt a kört.

#### Scenario: Dokumentált kör
- **WHEN** az Excel-alkalmazás létrejön
- **THEN** az alkalmazásengedély `Sites.Read.All` és `Files.ReadWrite.All`, és az admin consent megtörtént

### Requirement: Titok a Function App környezetében
A titkos érték MUST a Function App környezetében legyen elérhető. A bemenet, a leltár, a napló és a verziókezelt fájl MUST NOT tartalmazzon jelszót, client secretet, tokent, SAS-t vagy storage-kulcsot.

#### Scenario: Leltár csak hivatkozás
- **WHEN** a létrehozás titkot helyez el
- **THEN** a leltár a tárolás helyét nevezi meg, és a titok értéke nincs benne

### Requirement: Meglévő SharePoint-hely
A művelet MUST a bemenet meglévő webhelyét és alapértelmezett dokumentumtárát használni. A webhelyet MUST NOT létrehozni. A hiányzó konfigurált mappát a telepítő MUST létrehozni.

#### Scenario: Hiányzó mappa
- **WHEN** a webhely megvan és a konfigurált mappa nincs
- **THEN** a telepítő létrehozza a mappát, és a webhelyet nem hozza létre

### Requirement: Idegen erőforrás
Névegyezés MUST NOT adjon tulajdonjogot. Foglalt név és a leltárban hiányzó azonosító esetén a művelet MUST az erőforrás módosítása nélkül megállni.

#### Scenario: Foglalt név
- **WHEN** a kért néven erőforrás van, és a leltár nem ezt az azonosítót tartalmazza
- **THEN** a művelet nem módosítja az erőforrást, és nem jelez teljes sikert

### Requirement: Ismételt létrehozás
Az ismételt létrehozás MUST a leltár azonosítóit folytatni. Meglévő alkalmazásregisztráció, engedély vagy titokhivatkozás mellett MUST NOT létrehozni másodikat, és MUST NOT titkot cserélni.

#### Scenario: Második futás
- **WHEN** a leltár már tartalmazza az alkalmazásregisztrációt és a titok hivatkozását
- **THEN** nem jön létre másik regisztráció, és a titok nem cserélődik

### Requirement: Részleges futás
Részleges létrehozás vagy lebontás után a leltár MUST megőrizni a létrejött azonosítókat. A művelet MUST NOT teljes sikert jelezni. A következő futás MUST a hiányzó saját lépést folytatni.

#### Scenario: Megszakadt létrehozás
- **WHEN** a hoszt létrejött és egy későbbi lépés hibázik
- **THEN** a leltár tartalmazza a hoszt azonosítóját, az állapot nem teljes siker, és az újrafuttatás nem hoz létre második hosztot

### Requirement: Lebontás azonosító szerint
A lebontás MUST a bemenet és a leltár egyező azonosítóit előnézetben megmutatni, és csak kifejezett megerősítés után törölni. A leltárban nem szereplő erőforrást MUST NOT törölni.

#### Scenario: Előnézet megerősítés nélkül
- **WHEN** a lebontás megerősítés nélkül indul
- **THEN** a törlendő azonosítók megjelennek, és törlés nem történik

### Requirement: Két erőforráscsoport
Az OsaPro hoszt, a hoszt storage és az értesítési Table Storage MUST egy erőforráscsoportba kerülni. A Slack-hoszt, a saját storage, az Application Insights és a workspace MUST másik erőforráscsoportba kerülni. Az OsaPro megerősítése MUST a saját csoportot törölni, és MUST NOT törölni a megosztott csoportot.

#### Scenario: Két csoport
- **WHEN** a létrehozás sikeres
- **THEN** az OsaPro erőforrásai és a megosztott erőforrások két külön erőforráscsoportban vannak

#### Scenario: OsaPro megerősítés
- **WHEN** csak az OsaPro-lebontás van megerősítve
- **THEN** az OsaPro erőforráscsoportja törlődik, a megosztott erőforráscsoport megmarad

### Requirement: Excel-regisztráció törlése
Az OsaPro megerősített lebontása MUST törölni a dedikált alkalmazásregisztrációt és a service principalt. Más alkalmazás engedélyét MUST NOT visszavonni.

#### Scenario: Regisztráció a halmazban
- **WHEN** az OsaPro-lebontás meg van erősítve
- **THEN** a dedikált regisztráció és a service principal törlődik, és más alkalmazás engedélye megmarad

### Requirement: SharePoint megmarad
A lebontás MUST NOT törölni a SharePoint-helyet, a dokumentumtárat, a telepítő által létrehozott mappát, a dokumentumokat és az Excel-fájlokat.

#### Scenario: Fájl a lebontás után
- **WHEN** a lebontás lefut
- **THEN** a meglévő Excel-fájl, a mappa és a webhely megmarad

### Requirement: Értesítési tároló törlése
Az OsaPro-környezet megerősített lebontása MUST törölni az értesítési állapot Table Storage-fiókját a hoszttal és a hoszt storage-dzsal együtt. A használati leírás MUST kimondani, hogy ez a küldési állapotot elveszi. Külön megőrző kapcsoló MUST NOT legyen.

#### Scenario: Egy megerősítés
- **WHEN** az OsaPro-környezet lebontása meg van erősítve
- **THEN** az értesítési állapot tárolója a hoszttal és a hoszt storage-dzsal együtt törlődik

### Requirement: Megosztott életciklus
Az OsaPro-környezet lebontása MUST NOT törölni a közös Slack-küldő hosztját és a közös telemetriát. Más alkalmazás identitásának engedélyét MUST NOT visszavonni.

#### Scenario: Csak az OsaPro környezete
- **WHEN** csak az OsaPro-környezet bontása van megerősítve
- **THEN** a Slack-küldő hoszt és a közös Application Insights megmarad

### Requirement: Ismert beállítások
A létrehozás MUST átadni a meglévő kód nem titkos Nav és Excel beállításneveit. NAV-jelszót és aláírókulcsot MUST NOT gyártani. Megadott operátori környezetváltozóból a NAV-titok MUST a Function App környezetbe kerülni. Hiányzó NAV-titok MUST NOT megakadályozni a teljes létrehozást. Slack-tokent MUST NOT létrehozni.

#### Scenario: Hiányzó NAV-titok
- **WHEN** a NAV-titok környezetváltozója nincs megadva
- **THEN** a létrehozás teljes lehet, és NAV-titok nem készül

#### Scenario: Megadott NAV-titok
- **WHEN** az operátor környezetváltozója meg van adva
- **THEN** az érték a Function App környezetében van, és nincs a leltárban

### Requirement: Telepítő és futó jog
A futó alkalmazás MUST NOT megkapni a telepítő adminisztratív jogát. A mappa létrehozása és az admin consent a telepítő művelete.

#### Scenario: Futó alkalmazás köre
- **WHEN** a létrehozás kész
- **THEN** a futó alkalmazás Graph-engedélye a `Sites.Read.All` és a `Files.ReadWrite.All`, és nincs nála a telepítő teljes körű webhelyjoga
