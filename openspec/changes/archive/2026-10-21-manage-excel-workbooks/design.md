# Design

## Context

A viselkedés: `specs/excel-workbook/spec.md`. A motiváció: `proposal.md` - Why.

Az `OsaPro.Common` betölti a konfigurációt, és a `Secrets` szakasz feloldott értékeit maszkolja. Az `OsaPro.Worker` ezt használja. A handler törzse üres, időzítő és HTTP-végpont nincs. A Worker nem hivatkozza az `OsaPro.NAV` és az `OsaPro.Excel` projektet. Az `OsaPro.Excel` létezik, és a meglévő fájlt workbook-munkamenettel módosítja. A `host.json` csak a `"version": "2.0"` értéket tartalmazza.

A Graph Excel API alkalmazásoszlopa nem támogatott. A `workbook: createSession` és a `worksheetCollection: add` oldalán az Application érték `Not supported`:

- https://learn.microsoft.com/en-us/graph/api/workbook-createsession?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/worksheetcollection-add?view=graph-rest-1.0

A fájl API saját jogon hívható. Az ellenőrzés 2026-10-21-i, `view=graph-rest-1.0`.

## Goals / Non-Goals

**Goals:**

- Egy hívható művelet az `OsaPro.Excel` könyvtárban. A hívó évet és hónapot ad. A művelet a SharePoint-fájlt a spec szerint biztosítja.
- A lapnév, az oszlopsorrend, a kezelt év, a paraméterérték és a helyi cellacsúsztatás Graph nélkül tesztelhető.
- A verziókezelt `Excel` szakasz és a három `Secrets` kulcsnév az `OsaPro.Worker` `appsettings.json` fájljában van.

**Non-Goals:**

- A Worker projekt-hivatkozása, a handler bekötése, időzítő és HTTP-végpont.
- Számlasor, NAV-mező és a feldolgozás utáni `InsDate` frissítése.
- Élő Graph-hívás a tesztfutásban. Slack küldése és a jelzés tárolása.
- A SharePoint-engedély kiadása a kódból. A `host.json` és a függvény-időkorlát módosítása.

## Decisions

### 1. A művelet az OsaPro.Excel könyvtárban van

Az `ExcelWorkbook` az `OsaPro.Excel` osztálykönyvtárban van. A publikus művelet `EnsureMonth(year, month)`. Az `OsaPro.Common` nem tartalmaz Excel-típust. Az `OsaPro.Worker` ebben a change-ben nem hivatkozza az `OsaPro.Excel` projektet, és a handler üres marad. Az `OsaPro.Excel.Tests` a műveletet közvetlenül hívja. A verziókezelt beállítást az `OsaPro.Worker` projektből olvassa, ugyanúgy, ahogy a NAV-teszt.

A szabályok külön típusban vannak, Graph nélkül: hónapnév, oszlopsorrend, kezelt év, fájlnév, paramétermásolás, alapértelmezés és a cellacsúsztatás. A Graph-adapter csak a kész döntést hajtja végre.

Alternatíva: a művelet az `OsaPro.Common` könyvtárban. Elvetve, mert a Common a naplózás, a titokkezelés és a paraméterbetöltés. Alternatíva: a művelet a Worker handlerében. Elvetve, mert a hívó későbbi change adja az évet és a hónapot.

### 2. Új és meglévő fájl is a DocumentFormat.OpenXml 3.5.1 csomaggal készül

Más Excel-könyvtár nincs. Hiányzó `SZÁMLÁKYYYY.xlsx` esetén a könyvtár sablonfájl nélkül állítja össze a munkafüzetet: a `Paraméterek` lap és a kért havi lap. A cellák szövegek. Az `InsDate` alapértelmezése és az `Előértesítési napok` szöveges számformátumot kapnak, hogy az Excel ne alakítsa őket dátummá vagy számmá.

A kész bájtok feltöltése:

`PUT /sites/{site-id}/drive/root:/{FolderPath}/SZÁMLÁKYYYY.xlsx:/content?@microsoft.graph.conflictBehavior=fail`

A `driveItem` leírása szerint a `PUT` alapértelmezése `replace`, a paraméter az URL-ben van, az értékei `fail`, `replace` és `rename`. A `fail` kell, hogy a közben létrejött azonos nevű fájl ne cserélődjön le. Átnevezés nincs, mert abból második éves fájl keletkezne. Ehhez a létrehozáshoz checkout nem kell. Ha a szolgáltatás jelzi, hogy a név már foglalt, nincs helyettesítő feltöltés: a meglévő fájl útja fut tovább. Az egyszerű feltöltés határa 250 MB.

Meglévő fájlt a program nem épít újra. Checkout után, csak `204` válasz esetén tölti le. A letöltés `302` választ ad, a `Location` előhitelesített címét kell követni, és arra a címre Authorization fejléc nem megy. A helyi módosítás ugyanazzal az OpenXml csomaggal történik. A visszatöltés a meglévő elemre szól: `PUT /sites/{site-id}/drive/items/{item-id}/content`. Utána checkin. A checkin törzse JSON.

A kért havi lapon a beszúrás helyétől jobbra lévő cella viszi a tárolt értékét, a képlet szövegét és a cellastílusát. A `=B2` szövege `=B2` marad. A balra lévő cellák és a többi lap nem íródnak át. Az egyesített cellák, a táblák és a diagramok nem íródnak át. Az új oszlopban csak a fejléc készül. A fájlnév, a munkalapnév, a fejléc, a kulcs és az érték összehasonlítás előtt `Trim`, utána `ToUpperInvariant`. Az ékezet megmarad. Új név a rögzített alakban készül. Meglévő egyező név nem íródik át.

Az előző év fájlja, ha a mappában megvan, checkout nélkül töltődik le, és csak olvasható. A kulcsok és az értékek a letöltött lap celláiból olvasódnak, nem a Graph Excel API `text` mezőjéből. Ha a fájl nincs a mappában, az alapértelmezés érvényes. Ha a fájl megvan, de a letöltése a saját újrapróbálás után is sikertelen, a művelet sikertelen, és alapértelmezés nem készül helyette.

Alternatíva: workbook-munkamenet. Elvetve, mert az alkalmazásoszlop nem támogatott. Alternatíva: a meglévő fájl újraépítése. Elvetve, mert a felhasználó cellái elvesznének.

### 3. A cím a webhely alapértelmezett dokumentumtára

A Graph gyökere `https://graph.microsoft.com/v1.0`. A webhely a `GET /sites/{SiteHost}:{SitePath}` hívás. A `GET /sites/{site-id}/drive` hívás nem használható: a `drive: get` oldalán az Application érték `Not supported`.

A mappa a dokumentált driveItem útvonalon érhető el, amelynek alkalmazásoszlopa támogatott:

`GET /sites/{site-id}/drive/root:/{FolderPath}`

A gyermekek: `GET /sites/{site-id}/drive/items/{folder-id}/children`. A válasz `@odata.nextLink` értékét követni kell, amíg a keresett név megvan vagy a lapok elfogynak. Az alapértelmezett lapméret 200. A program csak ezen a táron, csak ebben a mappában keres. Más tár, lista vagy mappa útvonala nincs a kódban.

Üres `SiteHost`, `SitePath` vagy `FolderPath` esetén a művelet Graph-hívás előtt sikertelen. Ha a `FolderPath` mappa nincs a dokumentumtárban, a művelet sikertelen, és a mappa nem jön létre. Az 1–12 tartományon kívüli hónap ugyanígy sikertelen, fájl nélkül.

A kezelt év a hívó órájának `Europe/Budapest` naptári éve és az azt megelőző év. A teszt órát ad, nem a gép óráját olvassa.

### 4. Foglalás, visszatöltés és checkin

A checkout `POST /sites/{site-id}/drive/items/{item-id}/checkout`, törzs nélkül. A dokumentált siker a `204`. Minden más válasz, a hálózati hiba is, a checkout saját körében ismétlődik. Csak `204` után van letöltés és módosítás. Más válasz után nincs letöltés és nincs checkin.

Sikeres checkout után a letöltés, a helyi módosítás vagy a visszatöltés hibája sikertelen művelet. Checkin ilyenkor nincs, a szerveren a régi tartalom marad. Ha a visszatöltés sikerül és a checkin nem, a művelet akkor is sikertelen: az új tartalom a szerveren van, a checkout megmarad. A checkin `POST /sites/{site-id}/drive/items/{item-id}/checkin`.

A következő hívás újra checkoutot kér. Csak az új `204` után megy tovább. Ismeretlen foglalásra a program nem hív checkint, mert a checkout és a checkin oldal nem mondja meg, ki tartja a zárolást. A `publication` facet `checkedOutBy` tulajdonsága létezik, és alapból nem jön vissza. A terv nem használja foglalás feloldására. A `driveItem: discardCheckout` nincs a tervben.

Minden hiba `ExcelWorkbookException`. Külön hibaok nincs. A szöveg évet, hónapot és fájlnevet tartalmazhat, cellatartalmat és titkot nem. Az `EnsureMonth` nem tér vissza sikerrel, és az `InsDate` értékét nem írja. A funkció meglévő hibnaplója rögzíti a kivételt, ha a hívás a funkcióban fut. Új naplótároló nincs. A későbbi Slack-összesítő a naplóbejegyzést jeleníti meg, és a munkafüzetet nem nyitja meg. Ez a change az értesítést nem küldi el.

### 5. Saját jogú belépés, a titok a Secrets szakaszban

A token kérés `POST https://login.microsoftonline.com/{ExcelTenantId}/oauth2/v2.0/token`, `client_credentials` típussal. A scope `https://graph.microsoft.com/.default`. A kliensazonosító és a titok a `Secrets:ExcelClientId` és a `Secrets:ExcelClientSecret`.

Az engedély neve nincs a specben. A kód nem ad engedélyt. A hívások oldalain a legszűkebb alkalmazásengedély:

| Hívás | Legszűkebb alkalmazásengedély | Magasabb, amely szintén szerepel |
| --- | --- | --- |
| `site: get` | `Sites.Read.All` | `Sites.ReadWrite.All` |
| `driveItem: get`, mappa gyermekei, `driveItem: get content` | `Files.Read.All` | `Files.ReadWrite.All`, `Sites.Read.All`, `Sites.ReadWrite.All` |
| `driveItem: put content`, checkout, checkin | `Files.ReadWrite.All` | `Sites.ReadWrite.All` |

A `Lists.SelectedOperations.Selected`, a `Files.SelectedOperations.Selected` és a `Sites.Selected` ezeken az oldalakon nem szerepel. A `drive: get` alkalmazásoszlopa `Not supported`, ezért nincs a láncban.

Az `OsaPro.Worker` `appsettings.json` fájlja az `Excel:SiteHost`, `Excel:SitePath`, `Excel:FolderPath`, `Excel:RetryCount` = `3` és `Excel:RetryDelaySeconds` = `5` kulcsot tartalmazza. A három `Secrets` Excel kulcs értéke üres. Új checkout-kulcs nincs. A nem üres titkok User Secretsből vagy `Secrets__ExcelTenantId`, `Secrets__ExcelClientId` és `Secrets__ExcelClientSecret` környezeti változóból jönnek. A meglévő maszkoló a `Secrets` értékeket takarja. A napló évet, hónapot és fájlnevet írhat. Cellatartalmat és titkot nem ír.

### 6. Két külön újrapróbálási kör, ugyanazzal a két számmal

A checkout nem `204` válasza az első hívás után még `RetryCount` alkalommal megy ki, a próbák között `RetryDelaySeconds` másodperccel. A kezdőérték mellett ez 15 másodperc várakozás. A többi Graph-hívás hálózati hibája, időtúllépése, HTTP 429 és HTTP 5xx válasza külön körben ugyanazt a két számot kapja. A két kör nem ágyazódik egymásba, és a checkout hibája nem fogyasztja a letöltés újrapróbálásait. A számok az `Excel` szakaszból jönnek, a `Nav` szakaszból nem. A várakozást a teszt helyettesíti, a teszt nem alszik.

A `host.json` nem változik, és függvény-időkorlát nem kerül bele. A kezdőértékek várakozása a platform alapértelmezett futási idején belül marad. Ha a hoszt közben leállítja a futást, a fájl a program checkoutjában maradhat, és a következő hívás csak újabb `204` után megy tovább.

## Risks / Trade-offs

- [A képlethivatkozás, az egyesített cella, a tábla és a diagram nem frissül oszlopbeszúráskor] → Ez a rögzített határ. A cella értéke, képletszövege és stílusa csúszik.
- [Az egyszerű feltöltés határa 250 MB] → Feltöltési munkamenet nincs. Nagyobb fájlnál a visszatöltés sikertelen.
- [Alkalmazásos tokennel érzékenységi címkével védett fájl tartalma nem cserélhető] → A `driveItem: put content` oldal ezt írja. A művelet a visszatöltés hibája, checkin nélkül.
- [A `publication.checkedOutBy` nem dönti el a saját foglalást] → A terv csak a `204` után módosít, és ismeretlen foglalást nem old fel.
- [A mappa listája lapozott] → A `nextLink` nélkül egy későbbi lapon lévő éves fájl hiányzónak látszana.
- [Üres cím vagy titok élő híváskor Graph-hiba] → Üres cím Graph-hívás előtt sikertelen. A teszt nem a verziókezelt üres titkot használja.

## Migration Plan

A handler üres marad, ezért a telepítés önmagában nem nyit Excel-fájlt. Üzembe helyezés előtt az üzemeltető a címmezőket és a titkokat User Secretsbe vagy környezeti változóba teszi. Az engedélyt nem a program adja. Visszavonáskor az `Excel` szakasz és a három üres `Secrets` kulcsnév kivehető, az `OsaPro.Excel` projekt törölhető. SharePoint-fájl emiatt nem törlődik. Adatmigráció nincs.

## Official references

- https://learn.microsoft.com/en-us/graph/api/site-get?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/drive-get?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/driveitem-get?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/driveitem-list-children?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/driveitem-checkout?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/driveitem-checkin?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/driveitem-get-content?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/driveitem-put-content?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/resources/driveitem?view=graph-rest-1.0
- https://learn.microsoft.com/en-us/graph/api/resources/publicationfacet?view=graph-rest-1.0
