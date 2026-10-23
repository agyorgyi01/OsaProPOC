# Design

## Context

A viselkedés a `specs/application-infrastructure/spec.md` fájlban van. A motiváció a `proposal.md` Why szakaszában.

Az `OsaPro.Excel` alkalmazásidentitással, `client_credentials` tokennel hívja a Graph fájl API-t. A titok a `Secrets` szakasz három Excel kulcsa. A Worker ezeket a Function App környezetből vagy a User Secretsből olvassa. Application Insights-csomag és `OsaPro.Slack` projekt nincs. A `GET /sites/{id}/drive` hívás nincs a kódban.

## Goals / Non-Goals

**Goals:**

- Azure-erőforrás Bicepben, Entra-lépés külön műveletben.
- Leltár azonosító szerint, titokérték nélkül.
- A leltár, az ismétlés, a célazonosítás, a titokszűrés és a megerősített lebontás élő felhő nélkül tesztelhető.

**Non-Goals:**

- Alkalmazáskód buildelése, csomagolása és telepítése.
- A Slack-küldő kódja és az OsaPro felőli hívás szerződése.
- NAV-technikai felhasználó, Slack-token és Key Vault.
- Selected Graph-kör és workbook API.

## Decisions

### 1. Azure-erőforrás Bicep, Entra PowerShell 7

A Function App, a storage, az Application Insights és a workspace Bicep. Az alkalmazásregisztráció, a Graph-engedély és az admin consent Microsoft Graph hívás PowerShell 7-ből. SharePoint-webhely és mappa nem készül. A két rész ugyanazt a bemenetet és ugyanazt a leltárt használja. Korábbi PowerShellen a parancs Azure-hívás előtt megáll.

A csak PowerShell út elvetve: az Azure-erőforrás ismétlése deklaratív leírással ellenőrizhetőbb. A Graph-engedély Bicepben elvetve: az alkalmazásengedély és az admin consent nem Azure RBAC.

### 2. Egy bemenet, egy leltár

A JSON négy mező: `tenantId`, `subscriptionId`, `location`, `prefix`. Excel, Nav, SharePoint és titoklista nincs benne. A `dev` vagy `prod` a `-Environment` paraméter. A két azonosító GUID. Hiányzó vagy hibás érték Azure-hívás előtt megáll. A `location` régióazonosító. A példa helykitöltőt tartalmaz, valós azonosítót nem.

A `prefix` kisbetűvel kezdődik, csak kisbetű és szám, legfeljebb 13 karakter. Érvénytelen előtag Azure-hívás előtt megáll. A kötőjeles név `{prefix}-{env}-{szerep}`. A storage-név `{prefix}{env}{szerep}`. Kitöltött leltárkörnyezet és leltárelőtag egyezik a bemenettel. A táblázat az `osp` előtag esete.

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

Nincs `account show` és nincs `account list`. Az `az` hívás a megadott előfizetést kapja. A tokenkérés csak a megadott bérlőt kapja, mert a parancs a kettőt együtt nem fogadja. A token bérlője egyezik a bemenettel. Eltérésnél a másik bérlő nem kerül a hibába. Az ARM-azonosító a megadott előfizetésben van. A principal GUID-ra ez a vizsgálat nem vonatkozik.

Az első Azure-lépés `az login` a megadott bérlővel és előfizetéssel. A szkript a bejelentkezés eredményéből nem választ célt. Appsettings és folyamat-környezet nem bemenet. A Bicep a szkript melletti `bicep` könyvtár. A Nav- és Excel-beállítást és a NAV-titkot a CI/CD írja a Function Appra. Hiányzó NAV-titok mellett a létrehozás teljes lehet.

A leltár a `prefix` és az `environment` mezőt, valamint az erőforrás azonosítóját, típusát és tulajdonát tárolja: `environment`, `shared` vagy `business`. Az Excel client azonosító és a client secret értéke a `secretLocations` mezőben marad. A napló nem írja ki. Token, storage-kulcs, SAS és kapcsolatkarakterlánc nincs a leltárban. A leltár `complete`, ha minden kötelező lépés sikerült.

### 3. Két erőforráscsoport, három tulajdon

Az OsaPro hoszt és a hoszt storage tulajdonosa `environment`, az értesítési tárolóé `business`. A Slack-hoszt, a saját storage, az Application Insights és a workspace tulajdonosa `shared`. Az Excel-regisztráció és a service principal tulajdonosa `environment`.

Az OsaPro megerősítése a saját csoportot, a benne leltározott erőforrásokat, a regisztrációt és a service principalt törli. Külön megőrző kapcsoló nincs. A megosztott csoport a saját megerősítésére törlődik. A SharePoint-hely, a dokumentumtár, a mappa, a dokumentumok és az Excel-fájlok egyik lebontásban sem szerepelnek. A használati leírás kimondja, hogy az OsaPro-megerősítés a küldési állapotot elveszi.

Mindkét Function App Linux Flex Consumption, .NET 10 isolated, always-ready nélkül. A csomag neve `{function}-plan`, az alkalmazás csoportjában van, FC1, és nem külön leltártétel. A skála 2048 MB és legfeljebb 100 példány. A hoszt `httpsOnly`, a legkisebb TLS 1.2. A telepítési tároló `blobContainer`, a hitelesítése system-assigned identity.

Három storage-fiók készül: OsaPro hoszt, Slack hoszt és értesítési fiók. Mindhárom Standard_LRS, StorageV2, nyilvános blob nélkül, megosztott kulcs nélkül, TLS 1.2, csak HTTPS. A két hosztfiókon egy `deployment` blobtároló van. Az értesítési fiókon ilyen tároló nincs. Az értesítési tábla neve `ertesitesek`, üres. A séma későbbi change.

A Log Analytics workspace PerGB2018, 30 nap, a megosztott csoportban, egy közös Application Insightsszal. A kapcsolatkarakterlánc mindkét Function App beállítása. A megosztott modul kimenete `@secure()`. A gyökér kimenet azonosító és principal azonosító. Titok nevű kimenet nem megy tovább. Sikertelen Azure-hívásnál a napló a hiba okát tartalmazza, titok, token, storage-kulcs, SAS és JWT nélkül.

### 4. Excel-jog a dokumentált végpontkör

A futó alkalmazás új regisztráció. Az alkalmazásengedély `Sites.Read.All` és `Files.ReadWrite.All`, utána admin consent. Az Excel client secret egyszer készül. A kliensazonosítóval együtt a `Secrets__ExcelClientId` és a `Secrets__ExcelClientSecret` helyre kerül. A `Secrets__ExcelTenantId` értéket a CI/CD írja. A futó alkalmazásnak mindhárom kulcs kell. Ismételt futás a client secretet nem forgatja. A telepítés után a leltárban lévő client azonosítót és client secretet visszaírja a Function Appra. A NAV-titkot és a Nav- és Excel-beállításokat a szkript nem másolja.

A végpontoldalak, `view=graph-rest-1.0`:

- [site: get](https://learn.microsoft.com/en-us/graph/api/site-get?view=graph-rest-1.0): alkalmazás `Sites.Read.All`
- [driveItem: get](https://learn.microsoft.com/en-us/graph/api/driveitem-get?view=graph-rest-1.0), [children](https://learn.microsoft.com/en-us/graph/api/driveitem-list-children?view=graph-rest-1.0), [get content](https://learn.microsoft.com/en-us/graph/api/driveitem-get-content?view=graph-rest-1.0): alkalmazás `Files.Read.All`, magasabbként `Files.ReadWrite.All`
- [put content](https://learn.microsoft.com/en-us/graph/api/driveitem-put-content?view=graph-rest-1.0), [checkout](https://learn.microsoft.com/en-us/graph/api/driveitem-checkout?view=graph-rest-1.0), [checkin](https://learn.microsoft.com/en-us/graph/api/driveitem-checkin?view=graph-rest-1.0): alkalmazás `Files.ReadWrite.All`
- [drive: get](https://learn.microsoft.com/en-us/graph/api/drive-get?view=graph-rest-1.0): alkalmazás `Not supported`, nincs a láncban

A [Selected áttekintés](https://learn.microsoft.com/en-us/graph/permissions-selected-overview) a végpontoldalakon nem szereplő kört ír le. Nem ezt használjuk.

A telepítő a saját jogával kéri az admin consentet. Ez a jog nem kerül az alkalmazásregisztrációra. A `Files.ReadWrite.All` a tenáns fájljaira szól. A kód a konfigurált mappát használja. A használati leírás ezt kimondja.

### 5. Identitás és Azure RBAC a storage-hoz

Mindkét Function App system-assigned managed identityt kap. Az OsaPro identity a `business` tárolón Table Data Contributor. Mindkét identity a saját hoszt storage-án Storage Blob Data Contributor. A hoszt storage-on sor- és táblaadat-szerep nem készül. A Slack-küldő az értesítési tárolóra nem kap jogot. Az OsaPro a Slack-küldőt nem hívhatja: a szerződés a 8. change. A hoszt `AzureWebJobsStorage__credential` értéke `managedidentity`, mellette a blobszolgáltatás címe van.

A telemetria az `APPLICATIONINSIGHTS_CONNECTION_STRING` környezetérték. A szkript nem írja ki. A két alkalmazást a hosztnév választja el. A három Function azonosítóját a meglévő napló írja, amikor a kód fut. A Slack-handler még nincs, ezért az ő azonosítója a hoszt létrejöttével még nem jelenik meg.

### 6. Folytatás és idegen név

A leltár minden sikeres lépés után frissül. Újrafuttatáskor a leltár azonosítója a cél. A várt csoportban meglévő, képzett nevű erőforrás megmarad, második példány nem készül, a hiányzó lépés pótlódik. Máshol foglalt név megállítja a lépést, a státusz nem `complete`. Storage-név, amely a megadott előfizetésben nincs meg, de máshol foglalt, a telepítés előtt megáll. Meglévő regisztrációnál második engedély és titokforgatás nincs.

A lebontás először előnézet. Törlés megerősítés után indul, ha az élő azonosító egyezik a leltáréval. Eltérésnél törlés nincs. A csoportok a bennük lévő erőforrások után törlődnek. A leltárban maradt `sharepoint-folder` bejegyzés a törlési halmazból kimarad, és a törlése hibát ad. Sikeres OsaPro-lebontás után ezek a kulcsok kikerülnek a leltárból. A leltár állapota partial marad, amíg a megosztott erőforrások megvannak. A parancs eredménye ilyenkor complete, a kilépés sikeres.

## Risks / Trade-offs

- [`Files.ReadWrite.All` tenáns szintű írás] → A Selected kör a híváslánc végpontoldalain nem igazolt. A használati leírás kimondja a különbséget a mappára korlátozott kód és az engedély között.
- [A connection string és a client secret a Function App környezetében van] → Az Excel client azonosító és a client secret a helyi leltárban is megvan. A fájl nem kerül verziókezelésbe. A napló és a parancs kimenete nem írja ki. A kapcsolatkarakterlánc nincs a leltárban.
- [A hoszt naplója akkor jut az Application Insightsba, ha a platform a connection stringgel továbbítja] → Kódmódosítás nincs. Ha a Flex hoszt nem továbbít, az külön change.
- [Az admin consent privilegizált személyt igényel] → Jog nélkül a szkript nem jelez teljes sikert.
- [A Bicep állapot tartalmazhat érzékeny kimenetet] → A leltár csak azonosítót vesz át. A sikertelen hívás oka a naplóba kerül, titok, token, storage-kulcs, SAS és JWT nélkül.
- [A .NET 10.0 runtime szöveg, a csak blobjogú hoszt indulása, a megosztott kulcs tiltása és az admin consent élő előfizetésen nincs igazolva] → A sablon és a szkript ezt kéri. Az élő elfogadás külön futtatás. Storage-kulcs és a hoszt sor- vagy táblaadat-szerepe nem kapcsolódik be helyette.

## Migration Plan

Üzembe helyezés a használati leírás létrehozó parancsa. Alkalmazáscsomag nem megy fel. Visszavonás a lebontó parancs: előnézet, majd egy megerősítés az OsaPro csoportra és a dedikált Excel-regisztrációra. A SharePoint-hely, a mappa és az Excel-fájl marad. A megosztott csoport külön megerősítést kér.

## Open Questions

Nincs nyitott kérdés. A Slack-hívás szerződése a 8. change.
