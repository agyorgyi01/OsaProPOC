# Design

## Context

A viselkedés: `specs/excel-workbook/spec.md`. A motiváció: `proposal.md` - Why.

Az `OsaPro.Common` betölti a konfigurációt, és a `Secrets` szakasz feloldott értékeit maszkolja. Az `OsaPro.Worker` ezt használja. A handler törzse üres, időzítő és HTTP-végpont nincs. A Worker nem hivatkozza az `OsaPro.NAV` projektet. Excel-hívás a kódban nincs. A verziókezelt `appsettings.json` a `Nav` szakaszt és a négy NAV `Secrets` kulcsnevet tartalmazza, titokérték nélkül.

## Goals / Non-Goals

**Goals:**

- Egy hívható művelet az `OsaPro.Excel` könyvtárban. A hívó évet és hónapot ad. A művelet a SharePoint-fájlt a spec szerint biztosítja.
- A lapnév, az oszlopsorrend, a kezelt év és a paraméterérték tiszta szabályként, Graph nélkül tesztelhető.
- A verziókezelt `Excel` szakasz és a három új `Secrets` kulcsnév az `OsaPro.Worker` `appsettings.json` fájljában van.

**Non-Goals:**

- A Worker projekt-hivatkozása, a handler bekötése, időzítő és HTTP-végpont.
- Számlasor, NAV-mező és a feldolgozás utáni `InsDate` frissítése.
- Élő Graph-hívás a tesztfutásban.
- A SharePoint-jogosultság kiadása a kódból. Azt az üzemeltető adja a dokumentumtárra.

## Decisions

### 1. A művelet az OsaPro.Excel könyvtárban van

Az `ExcelWorkbook` az `OsaPro.Excel` osztálykönyvtárban van. A publikus művelet `EnsureMonth(year, month)`. Az `OsaPro.Common` nem tartalmaz Excel-típust. Az `OsaPro.Worker` ebben a change-ben nem hivatkozza az `OsaPro.Excel` projektet, és a handler üres marad. Az `OsaPro.Excel.Tests` a műveletet közvetlenül hívja. A verziókezelt beállítást és a User Secretet az `OsaPro.Worker` projektből olvassa, ugyanúgy, ahogy a NAV-teszt.

A szabályok külön típusban vannak, Graph nélkül: hónapnév, oszlopsorrend, kezelt év, fájlnév, paramétermásolás és az alapértelmezés. A Graph-adapter csak a kész döntést hajtja végre.

Alternatíva: a művelet az `OsaPro.Common` könyvtárban. Elvetve, mert a Common a naplózás, a titokkezelés és a paraméterbetöltés. Alternatíva: a művelet a Worker handlerében. Elvetve, mert a hívó későbbi change adja az évet és a hónapot.

### 2. Új fájl a memóriában készül, a meglévő fájl a Graph Excel API-n módosul

Hiányzó `SZÁMLÁKYYYY.xlsx` esetén a könyvtár `DocumentFormat.OpenXml` csomaggal, sablonfájl nélkül állítja össze a munkafüzetet: a `Paraméterek` lap és a kért havi lap. A cellák szövegek. Az `InsDate` alapértelmezése és az `Előértesítési napok` szöveges számformátumot kapnak, hogy az Excel ne alakítsa őket dátummá vagy számmá. A kész bájtok a Graph `PUT .../drive/root:/{FolderPath}/SZÁMLÁKYYYY.xlsx:/content` hívással kerülnek fel.

Meglévő fájlt a program nem tölt le és nem tölt vissza. Workbook-munkamenetet nyit, `persistChanges=true` értékkel. Hiányzó lapot a munkamenet ad hozzá. Hiányzó oszlopot a tartomány `insert` művelete szúrja be, jobbra tolással. A munkamenet a kért havi lap első sorát olvassa.

A fájlnév, a munkalapnév, a fejléc, a `Paraméterek` kulcs és a paraméterérték összehasonlítás előtt `Trim` műveletet kap. Utána az összehasonlítás `ToUpperInvariant` szerint történik. Az ékezetek megmaradnak: `május` és `Május` egyezik, a `Majus` nem. Az `insdate` az `InsDate`, az `Eloertesitesi napok` nem az `Előértesítési napok`. Új fájl, lap, fejléc és kulcs a rögzített alakban készül. Meglévő egyező nevet a program nem ír át. Ismételt fejlécnél az első egyezés az oszlop, további ugyanilyen oszlop nem készül. Ismételt paraméterkulcsnál a lapon felülről az első egyezés értéke kerül át, az új lapon egy sor készül.

A hiányzó oszlop a következő, már meglévő kanonikus fejléc elé kerül. Ha ilyen nincs, az utolsó meglévő kanonikus fejléc mögé kerül. Ha egyetlen kanonikus fejléc sincs, mind a tizenhat oszlop a sor elejére kerül, rögzített sorrendben, és a meglévő cellák jobbra csúsznak. A hiányzó oszlopok száma nem teszi sikertelenné a műveletet. Új lapon nincs beszúrás: az első sor azonnal a tizenhat nevet kapja.

Alternatíva: a meglévő fájlt is újraépíteni és felülírni. Elvetve, mert a felhasználó cellái elvesznének. Alternatíva: bináris sablon a repóban. Elvetve, mert a lapok az ismert oszloplistából készülnek.

### 3. A cím a webhely alapértelmezett dokumentumtára

A Graph gyökere `https://graph.microsoft.com/v1.0`. A webhely a `GET /sites/{SiteHost}:{SitePath}` hívás. A tár a webhely alapértelmezett meghajtója, `GET /sites/{site-id}/drive`. A `FolderPath` ennek a gyökerétől értendő. A program csak ezen a meghajtón, csak ebben a mappában keres. Más tár, lista vagy mappa útvonala nincs a kódban.

Üres `SiteHost`, `SitePath` vagy `FolderPath` esetén a művelet Graph-hívás előtt sikertelen. Ha a `FolderPath` mappa nincs a dokumentumtárban, a művelet sikertelen, és a mappa nem jön létre. Az 1–12 tartományon kívüli hónap ugyanígy sikertelen, fájl nélkül.

A kezelt év a hívó órájának `Europe/Budapest` naptári éve és az azt megelőző év. A teszt órát ad, nem a gép óráját olvassa.

Alternatíva: Graph-elem GUID a beállításban. Elvetve, mert a cím a három olvasható mező. Alternatíva: UTC naptári év. Elvetve, mert a spec magyarországi dátumot kér.

### 4. A paraméter másolása kulcsnév szerint, szövegként olvasva

Az előző év fájlja, ha megvan, csak olvasható. A `Paraméterek` lap A oszlopában a kulcs neve, a B oszlopban az érték van. A program a Graph-tartomány `text` mezőjét olvassa. A nulla hosszú és a csak whitespace-ből álló szöveg hiány. A többi értékről a szélső whitespace levágódik, a belső whitespace megmarad. A másolt szöveg nem kap új számformátumot és nem értelmeződik dátumként.

Az alapértelmezett `InsDate` a kért év előtti év december 31-e, `yyyy-12-31T00:00:00Z` szöveggel. Az `Előértesítési napok` alapértelmezése a `7,5,1` szöveg. Meglévő `Paraméterek` lap celláit a program nem olvassa írás céljából, és nem módosítja.

Alternatíva: a Graph `values` mezője. Elvetve, mert a dátumként tárolt cella sorszámot adna, és a másolat átalakulna.

### 5. Saját jogú belépés, a titok a Secrets szakaszban

A token kérés `POST https://login.microsoftonline.com/{ExcelTenantId}/oauth2/v2.0/token`, `client_credentials` típussal. A scope `https://graph.microsoft.com/.default`. A kliensazonosító és a titok a `Secrets:ExcelClientId` és a `Secrets:ExcelClientSecret`. Az alkalmazásregisztráció jogosultsága `Lists.SelectedOperations.Selected`. A tárra szóló írási jogot az üzemeltető adja; a kód nem hoz létre ilyen engedélyt.

Az `OsaPro.Worker` `appsettings.json` fájlja:

- `Excel:SiteHost`, `Excel:SitePath`, `Excel:FolderPath` üres szöveggel. Nem titkok.
- `Excel:RetryCount` = `3`, `Excel:RetryDelaySeconds` = `5`
- `Secrets:ExcelTenantId`, `Secrets:ExcelClientId` és `Secrets:ExcelClientSecret` üres értékkel

A nem üres titkok User Secretsből vagy `Secrets__ExcelTenantId`, `Secrets__ExcelClientId` és `Secrets__ExcelClientSecret` környezeti változóból jönnek. A meglévő maszkoló a `Secrets` értékeket takarja. A napló évet, hónapot és fájlnevet írhat. Cellatartalmat és titkot nem ír.

Alternatíva: a titkok az `Excel` szakaszban. Elvetve, mert a maszkoló csak a `Secrets` szakaszt ismeri. Alternatíva: `Sites.Selected` a teljes webhelyre. Elvetve, mert a hozzáférés a dokumentumtárra szól.

### 6. Az újrapróbálás a híváson belül van, a zárolás nincs

Átmeneti hiba: hálózati hiba, időtúllépés, HTTP 429 és HTTP 5xx. Ezek után ugyanaz a kérés még `RetryCount` alkalommal megy ki. Két küldés között `RetryDelaySeconds` másodperc telik el. A `3` és `5` kezdőérték mellett ez egy első hívás és három újrapróbálás. A várakozást a teszt helyettesíti, a teszt nem alszik. A számok az `Excel` szakaszból jönnek, a `Nav` szakaszból nem.

A HTTP 409 és a HTTP 423 zárolás vagy ütközés. Ezek nem indulnak újra. A művelet sikertelen, és nem töröl meglévő lapot. A megszakadt kiegészítést a következő `EnsureMonth` hívás folytatja: a már létrejött lap megmarad, a hiányzó lap vagy oszlop pótlódik.

Alternatíva: a zárolást is újrapróbálni. Elvetve, mert a nyitott fájl a várakozás alatt is zárolt maradhat, és a spec a következő hívásra hagyja.

## Risks / Trade-offs

- [A Graph `text` a megjelenített szöveget adja, nem mindig a nyers tárolt értéket] → A másolat ezt a szöveget viszi tovább, számformátum nélkül. Az általunk írt alapértelmezés eleve szövegcella.
- [Üres első sorú meglévő havi lapon mind a tizenhat oszlop a sor elejére kerül, és a régi cellák jobbra csúsznak] → Ez a kért viselkedés. Csak a kért lap érintett, új sor nem kerül a fejléc fölé.
- [A munkamenet több hívásból áll, egy hiba félkész lapot hagyhat] → A következő hívás a hiányzó lapot vagy oszlopot pótolja, és a meglévő `Paraméterek` lapot nem írja felül.
- [Üres verziókezelt cím vagy titok élő híváskor Graph-hiba] → Üres cím Graph-hívás előtt sikertelen. A teszt nem a verziókezelt üres titkot használja.
- [Az alapértelmezett dokumentumtár nem a felhasználó számlatára] → A `FolderPath` csak ezen a táron érvényes. Más tárhoz a beállítás és a jogosultság nem elég.

## Migration Plan

A handler üres marad, ezért a telepítés önmagában nem nyit Excel-fájlt. Üzembe helyezés előtt az üzemeltető kitölti a három címmezőt, a titkokat User Secretsbe vagy környezeti változóba teszi, és a `Lists.SelectedOperations.Selected` írási jogot az alapértelmezett dokumentumtárra adja. Visszavonáskor az `Excel` szakasz és a három üres `Secrets` kulcsnév kivehető, az `OsaPro.Excel` projekt törölhető. SharePoint-fájl emiatt nem törlődik. Adatmigráció nincs.
