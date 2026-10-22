# Tasks

## 1. Leltár és tiszta döntések

- [x] 1.1 Készíts tiszta műveleteket a leltár folytatására, az idegen név elutasítására, a titokérték kiszűrésére és a lebontási halmaz kiválasztására. A tulajdon `environment`, `shared` vagy `business`. Ellenőrizd: automatizált teszt igazolja, hogy a második futás a meglévő azonosítót használja és nem készít másodikat, a foglalt idegen név nem kerül a leltárba, a client secret, a token, a SAS és a storage-kulcs kikerül a leltárból és a naplószövegből, az OsaPro-megerősítés tartalmazza a saját erőforráscsoportot, a hosztot, a hoszt storage-ot, az értesítési tárolót és a dedikált regisztrációt, nem tartalmazza a megosztott csoportot, a Slack-hosztot és a telemetriát, és megerősítés nélkül a törlési halmaz üres. A teszt élő Azure-hívás nélkül fut.

## 2. Azure-erőforrás

- [x] 2.1 Írd le Bicepben a két erőforráscsoportot és bennük a két Flex Consumption Function Appot, .NET 10 isolated workerrel, always-ready nélkül, a saját hoszt-storage-ukkal, a külön értesítési Table Storage-dzsal, a közös Application Insightsszal és a workspace-szel, valamint a storage-hoz kötött managed identity szerepekkel. A `APPLICATIONINSIGHTS_CONNECTION_STRING` a hoszt környezetébe kerül, a Bicep kimenete csak azonosító. Ellenőrizd: a Bicep build hibátlan, a két csoport külön erőforrás, a sablonban nincs always-ready példány, a kimeneti lista nem tartalmaz titoknevű értéket, a `dev` paraméterből `osp-dev-rg` és `ospdevst`, a `prod` paraméterből `osp-prod-rg` és `ospprodst` képződik, és más környezetértéknél a képzés megáll.

## 3. Entra, SharePoint és életciklus

- [x] 3.1 Készítsd el a PowerShell 7 létrehozó, ellenőrző és lebontó műveletet a közös bemenettel és leltárral. A létrehozás az 1.1 döntéseit használja, pótolja a hiányzó saját lépést, és részleges hiba után nem jelez teljes sikert. Az Excel-regisztráció a `Sites.Read.All` és a `Files.ReadWrite.All` engedélyt kéri admin consenttel, a client secretet egyszer írja a Function App környezetbe, és a mappát a telepítő hozza létre. A nem titkos Nav és Excel kulcsnevek átmennek. A NAV-titok csak megadott operátori környezetváltozóból másolódik, és hiányában a létrehozás teljes lehet. Slack-token nem készül. Ellenőrizd: az 1.1 tesztjei továbbra is sikeresek, és egy hamis Graph-válaszokkal futó teszt igazolja, hogy a második futás nem kér új titkot, nem ad második engedélyt, és hiányzó NAV-titok mellett nem gyárt jelszót.

- [x] 3.2 A lebontás előnézete és megerősítése csak a leltár és az élő erőforrás egyező azonosítóit törli. Eltérő azonosítónál nincs törlés. Az OsaPro egy megerősítése a saját erőforráscsoportot, a hosztot, a hoszt storage-ot, az értesítési tárolót, a dedikált regisztrációt és a service principalt törli. A megosztott csoport kimarad. SharePoint-hívás a lebontásban nincs. Ellenőrizd: automatizált teszt igazolja az előnézetet törlés nélkül, az azonosító-eltérés miatti megállást, és hogy a SharePoint-hely, a mappa, a dokumentum és az Excel-fájl nincs a törlési halmazban.

## 4. Használati leírás

- [x] 4.1 Írj rövid magyar használati leírást a szükséges eszközökről, a két erőforráscsoportról, a telepítő adminisztratív jogáról, a futó alkalmazás `Sites.Read.All` és `Files.ReadWrite.All` köréről, valamint a létrehozás, az ellenőrzés és a lebontás parancsairól. A leírás mondja ki, hogy a nevek az `osp` előtagból és a `dev` vagy `prod` paraméterből képződnek, a `Files.ReadWrite.All` tenáns szintű, a kód csak a konfigurált mappát használja, az OsaPro egy megerősítése törli a saját csoportot, az értesítési tárolót és a dedikált regisztrációt, és a NAV-titok az operátor környezetváltozójából kerül a Function App környezetbe. Ellenőrizd: a leírt parancsok a szkript súgójában szerepelnek, és a leírás nem tartalmaz titokértéket.

## Workflow follow-up

- A tervjóváhagyás és az implementáció után a change a projekt Git-munkarendje szerint megy tovább. Archiválás és delta-spec szinkron csak külön explicit jóváhagyással.
