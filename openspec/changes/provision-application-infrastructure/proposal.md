# Proposal

## Why

A NAV-lekérdezés és az Excel-kezelés konfigurációja megvan. A két Function App hosztja és a telemetria nincs megismételhetően létrehozva. A telepítő SharePoint-helyet és mappát nem hoz létre.

## What Changes

- Létrehozás, ellenőrzés és lebontás. A JSON a `tenantId`, a `subscriptionId`, a `location` és a `prefix`. A `dev` vagy `prod` a parancs `-Environment` paramétere. A nevek a `prefix` és a környezet összetétele. A leltár a gépen marad, és nem kerül verziókezelésbe. Az Excel client azonosító és a client secret értéke benne van. Más titok nincs benne.
- Két Linux Flex Consumption Function App, .NET 10 isolated worker, always-ready nélkül. Az OsaPro a számlafeldolgozáshoz és az értesítés-összeállításhoz, a Slack-küldő külön hoszt.
- Egy közös Application Insights és egy 30 napos Log Analytics workspace. A két hoszt a saját nevével különül el.
- Az OsaPro hoszt, a hoszt storage és az értesítési Table Storage egy erőforráscsoport. A Slack-hoszt, a saját storage, az Application Insights és a workspace a másikban. Az OsaPro megerősítése a saját csoportot törli. A használati leírás kimondja, hogy a küldési állapot elvész.
- Egy Excel-alkalmazásregisztráció és service principal, `Sites.Read.All` és `Files.ReadWrite.All` engedéllyel, admin consenttel. A client azonosító és a client secret egyszer kerül a Function App környezetébe. Az Excel-bérlő azonosítóját a CI/CD írja. Az OsaPro megerősítése a regisztrációt és a service principalt törli.
- Két system-assigned managed identity. A Slack-küldő kódja, a hívás szerződése, a NAV-technikai felhasználó és a Slack-token kimarad.
- Az ismételt futás a leltár azonosítóit használja. Idegen név megállítja a lépést. Részleges futás folytatható. A lebontás előnézet után, megerősítéssel, a leltárban igazolt saját erőforrást törli. A SharePoint-hely és az Excel-fájlok maradnak.
- Rövid magyar használati leírás. Alkalmazáskód buildelése és telepítése kimarad.

## Capabilities

### New Capabilities

- `application-infrastructure`: a saját Azure- és Entra-erőforrások létrehozása, ellenőrzése és azonosítóhoz kötött lebontása. A meglévő SharePoint-hely kimarad.

### Modified Capabilities

- Nincs. A titokbetöltés, a naplózás, a NAV-lekérdezés és az Excel-kezelés követelményei változatlanok.

## Impact

- Új infrastruktúra-leírás, PowerShell-művelet, teszt és használati leírás. Az `OsaPro.Worker`, az `OsaPro.Excel` és az `OsaPro.NAV` kódja változatlan.
- Külső rendszer: Azure-előfizetés, Entra ID és a meglévő SharePoint-webhely. Key Vault nincs.
- A közös Slack-küldő hosztja létrejön. Az alkalmazása a 7. change, a hívás szerződése a 8. change.

## Érinti-e kritikus komponenst?

Igen. Az Excel-nyilvántartás kritikus terület: a futó alkalmazás `Files.ReadWrite.All` engedélyt kap, a telepítő admin consentet ad, és az OsaPro-lebontás a dedikált regisztrációt törli. A telepítő SharePoint-webhelyet és mappát nem hoz létre és nem töröl. Az értesítések üres tárolója létrejön, és az OsaPro-lebontás a küldési állapottal együtt törli. A figyelmeztetés és a küldés szabálya változatlan. NAV-technikai hozzáférés nem készül.

## Assumptions from codebase

- Az `ExcelWorkbook` a `https://graph.microsoft.com/v1.0` gyökeret hívja: webhely `GET /sites/{host}:/{path}`, mappa `GET /sites/{id}/drive/root:/{FolderPath}`, gyermekek `GET .../children` a `nextLink` követésével, checkout és checkin `POST`, tartalom `GET` és `PUT`. A `GET /sites/{id}/drive` hívás nincs a forrásban (`src/OsaPro.Excel/ExcelWorkbook.cs`).
- A token `client_credentials`, a scope `https://graph.microsoft.com/.default`. A kliensazonosító és a titok a `Secrets:ExcelClientId` és a `Secrets:ExcelClientSecret`. Managed identity ezt kódmódosítás nélkül nem helyettesíti.
- A Worker `appsettings.json` a `Nav` és az `Excel` szakaszt, valamint a `Secrets` kulcsneveket üres értékkel tartalmazza. A `host.json` csak a `"version": "2.0"` értéket tartalmazza. Application Insights-csomag nincs a Worker csprojban.
- A Function-azonosítók: `szamlafeldolgozas`, `ertesites-osszeallitas`, `slack-uzenetek-kuldese` (`src/OsaPro.Common/FunctionIds.cs`). A Slack-azonosítót futó handler még nem írja.
- A hivatalos végpontoldalak a webhelyolvasáshoz `Sites.Read.All`, a checkout, a checkin és a tartalomíráshoz `Files.ReadWrite.All` engedélyt írnak. A Selected kör ezeken az oldalakon nem szerepel.
- A szkript a parancs paramétereit és a JSON `tenantId`, `subscriptionId`, `location` és `prefix` mezőjét olvassa. Az `environment` a parancs paramétere. A `prefix` kisbetűvel kezdődik, csak kisbetű és szám, legfeljebb 13 karakter.
- A futó alkalmazás a `Secrets:ExcelTenantId`, a `Secrets:ExcelClientId` és a `Secrets:ExcelClientSecret` kulcsot kéri. A telepítő a client azonosítót és a client secretet írja. A bérlőazonosítót a CI/CD adja.
- A szkript az `appsettings.json` fájlt és a folyamat környezeti változóit nem olvassa. A művelet a megadott bérlővel és előfizetéssel jelentkezik be, mielőtt más Azure-hívás indul. A Bicep-fájl a szkript mellett van.
