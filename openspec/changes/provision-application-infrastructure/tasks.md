# Tasks

## 1. Leltár

- [x] 1.1 Leltár folytatása, idegen név elutasítása, titok kiszűrése, lebontási halmaz. A tulajdon `environment`, `shared` vagy `business`. Ellenőrizd élő Azure nélkül: a második futás a meglévő azonosítót használja, az idegen név nem kerül a leltárba, az Excel client azonosító és a client secret értéke a leltárban marad és a naplóból kikerül, a token, a SAS, a storage-kulcs és a kapcsolatkarakterlánc a leltárból és a naplóból kikerül, az OsaPro-megerősítés a saját csoportot, a hosztot, a két storage-ot és a regisztrációt tartalmazza, a megosztott csoportot, a Slack-hosztot és a telemetriát nem, és megerősítés nélkül a törlési halmaz üres.

## 2. Azure-erőforrás

- [x] 2.1 Két erőforráscsoport, két Linux Flex Consumption Function App, .NET 10 isolated, always-ready nélkül, alkalmazásonként egy FC1 csomag, hoszt storage `deployment` tárolóval, üres `ertesitesek` tábla, közös Application Insights és 30 napos workspace, storage-hoz kötött managed identity szerepek. A hoszt megosztott kulcsa tiltva. Blobjog csak a saját hoszton. Táblajog csak az OsaPro identitynek az értesítési fiókon. A hoszton sor- és táblaadat-szerep nincs. Ellenőrizd: a Bicep build hibátlan, a kimenet csak azonosító, az `osp` előtag `dev` mellett `osp-dev-rg` és `ospdevst`, `prod` mellett `osp-prod-rg` és `ospprodst`, és az előtag a JSON `prefix` mezője.

## 3. Entra és életciklus

- [x] 3.1 PowerShell 7 létrehozás, ellenőrzés és lebontás. A JSON a bérlő, az előfizetés, a régió és az előtag. A bejelentkezés ezekkel az azonosítókkal indul. A tokenkérés csak a bérlőt kapja. Érvénytelen előtag és korábbi PowerShell Azure-hívás előtt megáll. A várt csoportban meglévő erőforrás nem készül újra. Az előfizetésen kívül foglalt storage-név a telepítés előtt megáll. Az Excel-regisztráció a `Sites.Read.All` és a `Files.ReadWrite.All` engedélyt kéri admin consenttel, a client azonosítót és a client secretet a Function Appra írja, ismételt futáskor a leltárból visszaírja, új titkot nem készít, az Excel-bérlő azonosítóját nem. SharePoint-mappa nem készül. A Nav- és Excel-beállítás a CI/CD feladata. Ellenőrizd: a második futás nem kér új titkot és nem ad második engedélyt, és a cél nem a bejelentkezett fiókból jön.

- [x] 3.2 A lebontás előnézete törlés nélkül jelenik meg. Törlés csak megerősítés után, egyező azonosítóra indul. Az OsaPro a saját csoportot, a hosztot, a két storage-ot, a regisztrációt és a service principalt törli. A megosztott csoport és a SharePoint kimarad.

## 4. Használati leírás

- [x] 4.1 Magyar használati leírás az eszközökről, a két csoportról, a telepítő jogáról, a `Sites.Read.All` és `Files.ReadWrite.All` körről, valamint a három parancsról. A leírás tartalmazza a bemenetet, a `prefix` névképzést, a tenáns szintű fájlengedélyt, az OsaPro-törlés adatvesztését, és hogy a Nav- és Excel-beállítást a CI/CD írja. Ellenőrizd: a parancsok a súgóban vannak, és a leírásban nincs titokérték.

## Workflow follow-up

- Archiválás és delta-spec szinkron csak külön jóváhagyással.
