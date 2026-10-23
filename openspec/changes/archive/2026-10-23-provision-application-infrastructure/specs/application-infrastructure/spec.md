# Spec Delta

## Purpose

Az OsaPro és a közös Slack-küldő saját Azure-erőforrásainak és Excel-alkalmazásengedélyének létrehozása, ellenőrzése és azonosítóhoz kötött lebontása. A meglévő SharePoint-hely nem készül és nem törlődik.

## ADDED Requirements

### Requirement: Rögzített bérlő és előfizetés
A művelet a bemenet `tenantId` és `subscriptionId` értékét MUST használni. Ezeket MUST NOT a bejelentkezett fiókból kiolvasni, és MUST NOT `account show` vagy `account list` hívással megszerezni. Az Azure-hívás, a tokenkérés kivételével, MUST a megadott előfizetést kapni. A tokenkérés MUST csak a megadott bérlőt kapni, és a token bérlője MUST egyezzen vele. Hiányzó vagy nem GUID érték MUST Azure-hívás előtt megállítani a műveletet.

#### Scenario: Hiányzó előfizetés
- **WHEN** a bemenetben nincs előfizetés-azonosító
- **THEN** a művelet Azure-hívás nélkül megáll

#### Scenario: Megadott cél
- **WHEN** a bemenet bérlőt és előfizetést ad
- **THEN** a bejelentkezés a bérlőt és az előfizetést használja, a többi Azure-hívás az előfizetést, a tokenkérés csak a bérlőt

#### Scenario: Idegen bérlő
- **WHEN** a token bérlője nem a megadott bérlő
- **THEN** a művelet megáll, és a hiba nem írja ki a másik bérlőt

#### Scenario: Idegen előfizetés
- **WHEN** a kapott erőforrás-azonosító másik előfizetésre mutat
- **THEN** a művelet megáll, és az erőforrást nem használja

### Requirement: Két hoszt
A létrehozás MUST az OsaPro és a közös Slack-küldő Function Appot Linux Flex Consumption, .NET 10 isolated worker hosztként létrehozni, always-ready példány nélkül. Az alkalmazásonkénti Flex-csomag MUST az alkalmazás erőforráscsoportjában lenni.

#### Scenario: Mindkét hoszt létrejön
- **WHEN** a létrehozás sikeres
- **THEN** létezik az OsaPro hoszt és a közös Slack-küldő hoszt, always-ready példány nélkül

### Requirement: Közös telemetria
A létrehozás MUST egy közös Application Insightshoz kötni mindkét hosztot. A két hoszt telemetriája MUST megkülönböztethető legyen. A kapcsolat értéke MUST NOT kerüljön a leltárba vagy a naplóba.

#### Scenario: Két hoszt egy telemetrián
- **WHEN** mindkét hoszt létrejött
- **THEN** mindkettő ugyanahhoz az Application Insightshoz kapcsolódik, a hosztok telemetriája külön azonosítható, és a kapcsolat értéke nincs a leltárban

### Requirement: Névképzés
Az erőforrásnév MUST a JSON `prefix` értékéből és a parancs `dev` vagy `prod` környezetéből képződni. A `prefix` MUST kisbetűvel kezdődni, csak kisbetű és szám lehet, legfeljebb 13 karakter. Érvénytelen előtag vagy környezet MUST Azure-hívás nélkül megállítani a műveletet. A storage-név MUST kötőjel nélkül képződni. A bemenet MUST NOT felsorolni a képzett neveket. A kitöltött leltárelőtag és leltárkörnyezet MUST egyezni a bemenettel.

#### Scenario: Dev környezet
- **WHEN** a `prefix` értéke `osp` és a környezetparaméter `dev`
- **THEN** az OsaPro erőforráscsoport neve `osp-dev-rg`, a hoszt storage neve `ospdevst`

#### Scenario: Prod környezet
- **WHEN** a `prefix` értéke `osp` és a környezetparaméter `prod`
- **THEN** az OsaPro erőforráscsoport neve `osp-prod-rg`, a hoszt storage neve `ospprodst`

#### Scenario: Más előtag
- **WHEN** a `prefix` értéke `ab` és a környezetparaméter `dev`
- **THEN** az OsaPro erőforráscsoport neve `ab-dev-rg`, a hoszt storage neve `abdevst`

#### Scenario: Érvénytelen előtag
- **WHEN** a `prefix` nagybetűt, kötőjelet vagy 13-nál több karaktert tartalmaz, vagy hiányzik
- **THEN** Azure-hívás nem indul, és erőforrás nem készül

#### Scenario: Érvénytelen környezet
- **WHEN** a környezetparaméter sem `dev`, sem `prod`
- **THEN** erőforrás nem készül

#### Scenario: Eltérő leltárkörnyezet
- **WHEN** a leltár környezete ki van töltve, és nem a parancs paramétere
- **THEN** a művelet megáll

#### Scenario: Eltérő leltárelőtag
- **WHEN** a leltár előtagja ki van töltve, és nem a bemenet `prefix` értéke
- **THEN** a művelet megáll

### Requirement: Bemenet határa
A létrehozás JSON-bemenete MUST a `tenantId`, a `subscriptionId`, a `location` és a `prefix` legyen. A `location` MUST a megadott régióazonosító maradni, a fiók régiója MUST NOT pótolni. A szkript MUST NOT appsettingsből vagy folyamat-környezetből olvasni létrehozási adatot. A Bicep-fájl MUST a szkript melletti leírás lenni. A szkript MUST a megadott bérlővel és előfizetéssel bejelentkezni, mielőtt más Azure-hívás indul.

#### Scenario: Nincs helyi konfiguráció
- **WHEN** a létrehozás elindul
- **THEN** a szkript nem olvas appsettings fájlt, és nem használ folyamat-környezeti változót létrehozási adatként

#### Scenario: Bejelentkezés a megadott bérlővel
- **WHEN** a művelet elindul
- **THEN** a bejelentkezés a bemenet bérlőjét és előfizetését használja, és nem olvassa ki a már bejelentkezett fiókot

### Requirement: Storage szétválasztás
A létrehozás MUST három storage-fiókot létrehozni: két hosztfiókot és egy értesítési fiókot. Mindkét hosztfiókon MUST egy `deployment` nevű blobtároló lenni. Az értesítési fiókon ez a tároló MUST NOT lenni. Az értesítési fiók MUST egy üres `ertesitesek` táblát tartalmazni. A hoszt storage MUST letiltani a megosztott kulcsot és a nyilvános blobelérést. A Function App tárolóbeállítása MUST managed identity és a blobszolgáltatás címe lenni.

#### Scenario: Két tároló
- **WHEN** a létrehozás sikeres
- **THEN** a hoszt tárolója és az értesítési állapot tárolója két külön erőforrás, és az értesítési fiókon nincs `deployment` tároló

### Requirement: Storage-jog
Mindkét Function App MUST system-assigned managed identityt kapni. Mindkettő MUST a saját hoszt storage-án blobadat-szerepet kapni. Az OsaPro identity MUST táblaadat-szerepet kapni az értesítési fiókon. A Slack identity MUST NOT kapni jogot az értesítési fiókon. A hoszt storage-on sor- és táblaadat-szerep MUST NOT készülni.

#### Scenario: Saját hoszt és értesítési tábla
- **WHEN** a létrehozás sikeres
- **THEN** mindkét app csak a saját hoszt storage-án kap blobjogot, és csak az OsaPro identity kap táblajogot az értesítési fiókon

### Requirement: Excel alkalmazásengedély
A futó OsaPro alkalmazás MUST a `Sites.Read.All` és a `Files.ReadWrite.All` alkalmazásengedélyt megkapni, admin consenttel. Selected engedély MUST NOT helyettesíteni ezt a kört.

#### Scenario: Dokumentált kör
- **WHEN** az Excel-alkalmazás létrejön
- **THEN** az alkalmazásengedély `Sites.Read.All` és `Files.ReadWrite.All`, és az admin consent megtörtént

### Requirement: Titok a Function App környezetében
A titkos érték MUST a Function App környezetében legyen elérhető. Az Excel client azonosító és a client secret értéke MUST a helyi leltárban is megmaradni. A napló, a bemenet és a verziókezelt fájl MUST NOT tartalmazni ezeket. Tokent, SAS-t, storage-kulcsot és kapcsolatkarakterláncot a leltár MUST NOT tartalmazni.

#### Scenario: Excel-titok a leltárban
- **WHEN** a létrehozás titkot helyez el
- **THEN** a leltár tartalmazza az Excel client azonosító és a client secret értékét, a napló nem

### Requirement: Meglévő SharePoint-hely
A művelet MUST NOT létrehozni SharePoint-webhelyet vagy mappát. A hiányzó mappa MUST NOT a telepítő feladata lenni.

#### Scenario: Hiányzó mappa
- **WHEN** a létrehozás lefut
- **THEN** SharePoint-mappa nem készül, és a webhely sem készül

### Requirement: Idegen erőforrás
A várt erőforráscsoportban már meglévő, képzett nevű erőforrást a művelet MUST használni, és MUST NOT létrehozni belőle másodikat. Más helyen foglalt név MUST az erőforrás módosítása nélkül megállítani a műveletet. A megadott előfizetésen kívül foglalt storage-név MUST a telepítés előtt megállítani a műveletet.

#### Scenario: Foglalt név
- **WHEN** a kért néven erőforrás van, és az nincs a várt erőforráscsoportban
- **THEN** a művelet nem módosítja az erőforrást, és nem jelez teljes sikert

#### Scenario: Meglévő saját erőforrás
- **WHEN** a képzett nevű erőforrás már a várt erőforráscsoportban van
- **THEN** nem készül második erőforrás, és a művelet ezt használja

#### Scenario: Előfizetésen kívül foglalt storage-név
- **WHEN** a storage-név a megadott előfizetésben nincs meg, és máshol foglalt
- **THEN** a telepítés nem indul

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
A lebontás MUST NOT törölni a SharePoint-helyet, a dokumentumtárat, a mappát, a dokumentumokat és az Excel-fájlokat. A leltárban maradt SharePoint-bejegyzés MUST kimaradni a törlési halmazból.

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
A létrehozás MUST NOT bemásolni a Nav- és Excel-beállításokat és a NAV-titkokat a Function App környezetébe. Ezek az appsettingsből és a környezetből kerülnek fel, a CI/CD végzi. NAV-jelszót és aláírókulcsot MUST NOT gyártani. Hiányzó NAV-titok MUST NOT megakadályozni a teljes létrehozást. Slack-tokent MUST NOT létrehozni.

#### Scenario: Hiányzó NAV-titok
- **WHEN** a NAV-titok nincs a bemenetben
- **THEN** a létrehozás teljes lehet, és NAV-titok nem készül

#### Scenario: Megadott NAV-beállítás
- **WHEN** a bemenet Nav- vagy Excel-beállítást vagy NAV-titoknevet tartalmaz
- **THEN** a szkript nem írja a Function App környezetébe, és a titok nincs a leltárban

### Requirement: Excel client secret
A szkript MUST az Excel client azonosítót és a client secretet a Function App környezetbe írni. Új client secret MUST csak akkor készülni, ha a leltárban még nincs értéke. Ismételt létrehozás MUST ezt az értéket visszaírni, és MUST NOT új titkot készíteni. Az Excel-bérlő azonosítóját MUST NOT írni.

#### Scenario: Excel client secret
- **WHEN** a létrehozás először helyezi el az Excel-titkot
- **THEN** a környezetben ott a client azonosító és a client secret, az Excel-bérlő azonosítója nincs a szkript által írva

#### Scenario: Ismételt visszaírás
- **WHEN** a leltárban már megvan a client azonosító és a client secret értéke
- **THEN** a telepítés után mindkettő visszakerül a Function App környezetébe, és új titok nem készül

### Requirement: Azure-hiba
Sikertelen Azure-hívásnál a művelet MUST a hiba okát a naplóba írni. A napló MUST NOT tartalmazni titkot, tokent, storage-kulcsot vagy SAS-t.

#### Scenario: Sikertelen telepítés
- **WHEN** a Bicep-telepítés hibával tér vissza
- **THEN** a napló tartalmazza az Azure hibaokát, és nem tartalmaz titkot

### Requirement: PowerShell 7
A művelet MUST PowerShell 7 alatt futni. Korábbi PowerShell MUST a munka és minden Azure-hívás előtt megállítani a parancsot.

#### Scenario: Korábbi PowerShell
- **WHEN** a parancs Windows PowerShell 5 alatt indul
- **THEN** megáll, és Azure-hívás nem történik

### Requirement: Telepítő és futó jog
A futó alkalmazás MUST NOT megkapni a telepítő adminisztratív jogát. Az admin consent a telepítő művelete. SharePoint-mappa létrehozása MUST NOT a telepítő feladata lenni.

#### Scenario: Futó alkalmazás köre
- **WHEN** a létrehozás kész
- **THEN** a futó alkalmazás Graph-engedélye a `Sites.Read.All` és a `Files.ReadWrite.All`, és nincs nála a telepítő teljes körű webhelyjoga
