# Proposal

## Why

A NAV-lekérdezés és az Excel-kezelés konfigurációja megvan, a két Function App hosztja, a telemetria és a SharePoint-mappa viszont nincs megismételhetően létrehozva. Portálon kattintott, leltár nélküli beállítás nem bontható le azonosító szerint úgy, hogy a meglévő SharePoint-hely megmaradjon.

## What Changes

- Létrehozó, ellenőrző és lebontó művelet készül egyetlen nem titkos bemeneti JSON-nal és egy géppel olvasható leltárral. Az erőforrásnevek az `osp` előtagból és a bemenet `dev` vagy `prod` környezetparaméteréből képződnek. A SharePoint-mappa neve a meglévő `FolderPath`.
- Két Flex Consumption Function App jön létre, .NET 10 isolated workerrel, always-ready példány nélkül: az OsaPro a számlafeldolgozáshoz és az értesítés-összeállításhoz, a közös Slack-küldő külön hosztként.
- Egy közös Application Insights és a hozzá szükséges háttértár készül. A két alkalmazás a saját hosztneve szerint különül el. A Function-azonosítót a meglévő napló adja, ahol a kód már írja.
- Az OsaPro hoszt, a hoszt storage és az értesítési Table Storage egy erőforráscsoport. A Slack-hoszt, a saját storage, az Application Insights és a workspace másik erőforráscsoport. Az OsaPro egy megerősítése a saját csoportot és a benne lévő leltározott erőforrásokat törli, a megosztott csoportot nem. A használati leírás kimondja, hogy a küldési állapot elvész.
- Az Excel-eléréshez egy alkalmazásregisztráció, service principal, `Sites.Read.All` és `Files.ReadWrite.All` alkalmazásengedély, valamint admin consent készül. Az Excel client secret egyszer készül, és csak a Function App környezetbe kerül. A NAV-titkot a szkript nem gyártja: az operátor környezetváltozójából másolja a Function App környezetbe, ha a változó megvan. Hiányzó NAV-titok mellett a létrehozás teljes lehet. A telepítő létrehozza a konfigurált mappát a meglévő webhely alapértelmezett dokumentumtárában. Az OsaPro megerősítése a dedikált regisztrációt és a service principalt is törli. Más alkalmazás engedélye megmarad. A mappa marad.
- A két Function App managed identityt kap. A Slack-küldő alkalmazáskódja és a hívás szerződése nem készül el. NAV-technikai felhasználó és Slack-token nem készül.
- Az ismételt futás a leltár azonosítóit használja. Névegyezés nem vesz át idegen erőforrást. Részleges futás nem teljes siker, és folytatható. A lebontás előnézet és megerősítés után csak a leltárban igazolt saját erőforrást törli. A SharePoint-hely, a dokumentumok és az Excel-fájlok nem törölhetők. Megosztott identitás engedélye nem vonható vissza.
- Rövid magyar használati leírás készül. Alkalmazáskód buildelése és telepítése nem része a change-nek.

## Capabilities

### New Capabilities

- `application-infrastructure`: a saját Azure- és Entra-erőforrások, valamint a konfigurált SharePoint-mappa létrehozása, ellenőrzése és azonosítóhoz kötött lebontása.

### Modified Capabilities

- Nincs. A titokbetöltés, a naplózás, a NAV-lekérdezés és az Excel-kezelés követelményei nem változnak.

## Impact

- Új infrastruktúra-leírás, PowerShell-művelet, automatizált ellenőrzés és használati leírás. Az `OsaPro.Worker`, az `OsaPro.Excel` és az `OsaPro.NAV` kódja nem változik.
- Külső rendszer: Azure-előfizetés, Entra ID és a már létező SharePoint-webhely. Key Vault nincs.
- A közös Slack-küldő hosztja létrejön. Az alkalmazása a 7. change, a hívás szerződése a 8. change.

## Érinti-e kritikus komponenst?

Igen. Az Excel-nyilvántartás, az `openspec/project-conventions.md` kritikus területei szerint: a futó alkalmazás `Files.ReadWrite.All` engedélyt kap, a telepítő a konfigurált mappát biztosítja, és az OsaPro-lebontás megerősítése a dedikált regisztrációt törli. A határidők és értesítések területe annyiban érintett, hogy az egyszeri értesítések állapotának üres tárolója létrejön, és az OsaPro-lebontás megerősítése ezt a tárolót a küldési állapottal együtt törli. A figyelmeztetés és a küldés szabálya nem változik. NAV-technikai hozzáférés nem készül.

## Assumptions from codebase

- Az `ExcelWorkbook` a `https://graph.microsoft.com/v1.0` gyökeret hívja: webhely `GET /sites/{host}:/{path}`, mappa `GET /sites/{id}/drive/root:/{FolderPath}`, gyermekek `GET .../children` a `nextLink` követésével, checkout és checkin `POST`, tartalom `GET` és `PUT`. A `GET /sites/{id}/drive` hívás nincs a forrásban (`src/OsaPro.Excel/ExcelWorkbook.cs`).
- A token `client_credentials`, a scope `https://graph.microsoft.com/.default`. A kliensazonosító és a titok a `Secrets:ExcelClientId` és a `Secrets:ExcelClientSecret` (`ExcelWorkbook.cs`, tokenkérés). Managed identity nem helyettesíti ezt kódmódosítás nélkül.
- A Worker `appsettings.json` a `Nav` és az `Excel` szakaszt, valamint a `Secrets` kulcsneveket üres értékkel tartalmazza. A `host.json` csak a `"version": "2.0"` értéket tartalmazza. Application Insights-csomag nincs a Worker csprojban.
- A Function-azonosítók: `szamlafeldolgozas`, `ertesites-osszeallitas`, `slack-uzenetek-kuldese` (`src/OsaPro.Common/FunctionIds.cs`). A Slack-azonosítót futó handler még nem írja.
- A hivatalos végpontoldalak alkalmazásoszlopa a webhelyolvasáshoz `Sites.Read.All`, a checkout, a checkin és a tartalomíráshoz `Files.ReadWrite.All`. A Selected kör ezeken az oldalakon nem szerepel. A választott kör ezért ez a kettő.
