# Design

## Context

A viselkedés: `specs/application-infrastructure/spec.md`. A motiváció: `proposal.md` - Why.

Az `OsaPro.Excel` alkalmazásidentitással, `client_credentials` tokennel hívja a Graph fájl API-t. A titok a `Secrets` szakasz három Excel kulcsa. A Worker ezeket Function App környezetből vagy User Secretsből olvassa. Application Insights-csomag és `OsaPro.Slack` projekt nincs. A `GET /sites/{id}/drive` hívás nincs a kódban, mert az alkalmazásoszlopa nem támogatott.

## Goals / Non-Goals

**Goals:**

- Azure-erőforrás deklaratív leírása, Entra- és SharePoint-lépés külön műveletben.
- Leltár, amely saját és megosztott erőforrást azonosító szerint tart nyilván, titokérték nélkül.
- A tiszta döntések, az ismétlés, a célazonosítás, a titokszűrés és a megerősített lebontás élő felhő nélkül tesztelhetők.

**Non-Goals:**

- Az alkalmazáskód buildelése, csomagolása és telepítése.
- A Slack-küldő kódja és az OsaPro felőli hívás szerződése.
- NAV-technikai felhasználó, Slack-token és Key Vault.
- A Selected Graph-kör és a workbook API.

## Decisions

### 1. Azure-erőforrás Bicep, Entra és SharePoint PowerShell 7

A Function App, a storage, az Application Insights és a workspace Bicep. Az alkalmazásregisztráció, a Graph-engedély, az admin consent és a mappa Microsoft Graph hívás PowerShell 7-ből. A két rész ugyanazt a bemenetet és ugyanazt a leltárt használja.

Alternatíva: minden lépés csak PowerShell. Elvetve, mert az Azure-erőforrás ismétlése és a részleges folytatás deklaratív leírással ellenőrizhetőbb. Alternatíva: a Graph-engedély is Bicep. Elvetve, mert az alkalmazásengedély és az admin consent nem Azure RBAC.

### 2. Egy bemenet, egy leltár

A nem titkos bemenet egy JSON: tenant, subscription, régió, a `dev` vagy `prod` környezetparaméter, SharePoint-cím és mappa, valamint a verziókezelt Nav és Excel értékek. Az erőforrásneveket a szkript képzi, a bemenet nem sorolja fel őket. NAV-titoknál a bemenet csak az operátor környezetváltozójának nevét tartalmazza. Ha a változó megvan, a szkript az értéket a Function App környezetbe írja, és sehova máshova nem írja ki. A NAV-titkot nem gyártja. Hiányzó NAV-titok mellett a létrehozás teljes lehet.

Az előtag rögzített: `osp`. A környezetparaméter csak `dev` vagy `prod`. Más értéknél a létrehozás erőforrás nélkül megáll. A kötőjeles név `osp-{env}-{szerep}`. A storage-név `osp{env}{szerep}`, kisbetű és szám, kötőjel nélkül.

| Szerep | `dev` | `prod` |
|---|---|---|
| OsaPro erőforráscsoport | `osp-dev-rg` | `osp-prod-rg` |
| Megosztott erőforráscsoport | `osp-dev-shared-rg` | `osp-prod-shared-rg` |
| OsaPro Function App | `osp-dev-func` | `osp-prod-func` |
| Slack Function App | `osp-dev-slack` | `osp-prod-slack` |
| Application Insights | `osp-dev-ai` | `osp-prod-ai` |
| Log Analytics workspace | `osp-dev-log` | `osp-prod-log` |
| Excel-alkalmazásregisztráció | `osp-dev-excel` | `osp-prod-excel` |
| OsaPro hoszt storage | `ospdevst` | `ospprodst` |
| Slack hoszt storage | `ospdevslackst` | `ospprodslackst` |
| Értesítési Table Storage | `ospdevtbl` | `ospprodtbl` |

A SharePoint-mappa neve a meglévő `FolderPath`, nem ebből a táblából képződik.

A leltár JSON a létrejött vagy felhasznált erőforrások azonosítóját, típusát és tulajdonát tárolja: `environment`, `shared` vagy `business`. Az Excel-megfeleltetés a kulcsnevet és a Function App környezetbeli helyet mutatja, például `Secrets__ExcelClientId`, értéket nem. A kapcsolatkarakterlánc, a client secret és a storage-kulcs nincs a leltárban. A leltár státusza `complete` csak akkor, ha minden kötelező lépés sikerült.

### 3. Két erőforráscsoport, három tulajdon

Az OsaPro hoszt, a saját telepítési storage és az értesítési Table Storage egy erőforráscsoport. A hoszt és a hoszt storage tulajdonosa `environment`, a tárolóé `business`. A Slack-küldő hoszt, a saját telepítési storage, az Application Insights és a workspace másik erőforráscsoport, tulajdonuk `shared`. Az Excel-alkalmazásregisztráció és a service principal a tenánsban van, tulajdonuk `environment`.

Az OsaPro egy megerősítése törli a saját erőforráscsoportot, a benne leltározott erőforrásokat, a dedikált alkalmazásregisztrációt és a service principalt. Külön megőrző kapcsoló nincs. Más alkalmazás engedélye megmarad. A megosztott erőforráscsoport csak a saját megerősítésére törlődik. A SharePoint-hely, a dokumentumtár, a telepítő által létrehozott mappa, a dokumentumok és az Excel-fájlok egyik lebontásban sem szerepelnek. A használati leírás kimondja, hogy az OsaPro-megerősítés a küldési állapotot elveszi.

Mindkét Function App Flex Consumption, .NET 10 isolated, always-ready beállítás nélkül. A hoszt storage csak a platform és a telepítési csomag helye. Az értesítési tábla üresen jön létre, séma nélkül. A séma a későbbi change feladata.

### 4. Excel-jog a dokumentált végpontkör

A futó alkalmazás egy új regisztráció. Az alkalmazásengedély `Sites.Read.All` és `Files.ReadWrite.All`, utána admin consent. Az Excel client secret egyszer készül, és csak a Function App környezet `Secrets__ExcelClientSecret` helyére kerül, a tenant- és kliensazonosítóval együtt. Ismételt futás nem forgatja. A NAV-titok az operátor környezetváltozójából kerül a Function App környezetbe. A szkript NAV-jelszót és aláírókulcsot nem gyárt.

A végpontoldalak, `view=graph-rest-1.0`:

- [site: get](https://learn.microsoft.com/en-us/graph/api/site-get?view=graph-rest-1.0): alkalmazás `Sites.Read.All`
- [driveItem: get](https://learn.microsoft.com/en-us/graph/api/driveitem-get?view=graph-rest-1.0), [children](https://learn.microsoft.com/en-us/graph/api/driveitem-list-children?view=graph-rest-1.0), [get content](https://learn.microsoft.com/en-us/graph/api/driveitem-get-content?view=graph-rest-1.0): alkalmazás `Files.Read.All`, magasabbként `Files.ReadWrite.All`
- [put content](https://learn.microsoft.com/en-us/graph/api/driveitem-put-content?view=graph-rest-1.0), [checkout](https://learn.microsoft.com/en-us/graph/api/driveitem-checkout?view=graph-rest-1.0), [checkin](https://learn.microsoft.com/en-us/graph/api/driveitem-checkin?view=graph-rest-1.0): alkalmazás `Files.ReadWrite.All`
- [drive: get](https://learn.microsoft.com/en-us/graph/api/drive-get?view=graph-rest-1.0): alkalmazás `Not supported`, nincs a láncban

A [Selected áttekintés](https://learn.microsoft.com/en-us/graph/permissions-selected-overview) a végpontoldalakon nem szereplő kört ír le. Nem ezt használjuk.

A telepítő a saját jogával hozza létre a mappát és kéri az admin consentet. Ez a jog nem kerül az alkalmazásregisztrációra. A `Files.ReadWrite.All` a tenáns fájljaira szól. A kód csak a konfigurált mappát használja. Ezt a különbséget a használati leírás kimondja.

### 5. Identitás és Azure RBAC csak a storage-hoz

Mindkét Function App system-assigned managed identityt kap. Az OsaPro identity a `business` tárolón Table Data Contributor. Mindkét identity csak a saját hoszt storage-án kapja a Flex Consumption telepítéséhez szükséges blobjogot. A Slack-küldő nem kap jogot az értesítési tárolóra. OsaPro nem kap jogot a Slack-küldő meghívására: a szerződés a 8. change.

A telemetria az `APPLICATIONINSIGHTS_CONNECTION_STRING` környezetérték. A szkript nem írja ki. A két alkalmazást a hosztnév választja el. A három Function azonosítóját a meglévő napló írja, amikor a kód fut. A Slack-handler még nincs, ezért az ő azonosítója csak a hoszt létrejöttével még nem jelenik meg.

### 6. Folytatás és idegen név

A leltár minden sikeres lépés után frissül. Újrafuttatáskor a leltár azonosítója a cél. Ha a név foglalt, és az azonosító nincs a leltárban, a lépés megáll, és a státusz nem `complete`. Meglévő regisztrációnál a szkript nem hoz létre második engedélyt és nem forgat titkot. Hiányzó lépést pótol.

A lebontás először előnézet. Törlés csak megerősítés után, és csak akkor, ha az élő erőforrás azonosítója egyezik a leltáréval. Eltérésnél nincs törlés.

## Risks / Trade-offs

- [`Files.ReadWrite.All` tenáns szintű írás] → A szűkebb Selected kör a jelenlegi híváslánc végpontoldalain nem igazolt. A használati leírás kimondja a különbséget a mappára korlátozott kód és az engedély között.
- [A connection string és a client secret a Function App környezetében van] → Értékük nincs a leltárban, a naplóban és a verziókezelt fájlban. A parancs kimenete sem írja ki őket.
- [A hoszt naplója csak akkor jut az Application Insightsba, ha a platform a connection stringgel továbbítja] → Kódmódosítás nincs. Ha a Flex hoszt nem továbbít, az külön alkalmazáskód-change, nem ennek a hiánypótlása.
- [Az admin consent privilegizált személyt igényel] → A szkript enélkül nem jelez teljes sikert. A consent nem automatizálható jog nélkül.
- [A Bicep állapot tartalmazhat érzékeny kimenetet] → A leltár a Bicep kimenetéből csak azonosítót vesz át. Az érzékeny kimenet nem megy tovább.

## Migration Plan

Üzembe helyezés a használati leírás létrehozó parancsa. Alkalmazáscsomag nem megy fel. Visszavonás a lebontó parancs: előnézet, majd egy megerősítés az OsaPro erőforráscsoportra, a benne lévő erőforrásokra és a dedikált Excel-regisztrációra. A SharePoint-hely, a mappa és az Excel-fájl marad. A megosztott erőforráscsoport külön megerősítést kér.

## Open Questions

Nincs olyan nyitott kérdés, amely a specet, a megközelítést vagy a feladatbontást megváltoztatná. A Slack-hívás szerződése szándékosan a 8. change.
