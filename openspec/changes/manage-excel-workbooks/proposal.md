# Proposal

## Why

A számlák későbbi rögzítéséhez léteznie kell az éves SharePoint Excel-fájlnak, a keltezéshez tartozó havi munkalapnak és a `Paraméterek` lapnak. Az alkalmazásalap és a NAV-lekérdezés kész, a munkafüzet kezelése még nincs meg. Évváltáskor a folytatási pontnak át kell kerülnie, hogy számla ne maradjon ki.

## What Changes

- A hívó évet és hónapot ad. Ez a change nem olvassa a számlákat, és nem ír számlasort.
- Fájl vagy havi lap csak az aktuális és az előző naptári évre készül, magyarországi dátum szerint. Más évre a művelet nem hoz létre fájlt és nem készít lapot.
- Az éves fájl neve `SZÁMLÁKYYYY.xlsx`, például `SZÁMLÁK2026.xlsx`. Minden éves fájl ugyanabban a konfigurált SharePoint-mappában van. A fájlnév, a munkalapnév, a fejléc és a paraméterkulcs azonosítása előtt a szélső whitespace levágódik, utána a nagybetűs alak számít. Az ékezetek számítanak: `május` ugyanaz, mint `Május`, a `Majus` nem. A belső szóköz megmarad.
- Ha a fájl még nincs meg, létrejön a `Paraméterek` lap és a kért havi lap. A többi hónap nem jön létre. Nincs külön sablonfájl: a havi lap az elfogadott 16 oszlopból készül.
- A havi lap neve a magyar hónapnév, naptári sorrendben: `Január`-tól `December`-ig. Új név ebben a rögzített alakban készül. Meglévő, nagybetűsítés után egyező nevet a program nem nevez át. A fejléc az első sor. A program nem szúr be sort a fejléc fölé.
- Hiányzó oszlop a 16 oszlop rögzített helyére kerül, beszúrással, akár mind a tizenhat hiányzik. A hiányzó oszlop a következő meglévő kanonikus fejléc elé kerül. Ha ilyen nincs, az utolsó meglévő kanonikus fejléc mögé kerül. A felhasználó saját oszlopa a helyén marad. Üres első sor esetén a tizenhat oszlop a sor elejére kerül, a meglévő cellák jobbra csúsznak, és a művelet sikeres. Ha ugyanaz az elfogadott név kétszer szerepel, az első egyezés az oszlop, és második oszlop nem készül. Az új oszlopban csak a fejléc készül el. Meglévő oszlopot a program nem nevez át, nem töröl és nem rendez át.
- Ha a fájl már létezik, a program nem írja felül. A hiányzó kért havi lapot létrehozza. Ha a `Paraméterek` lap hiányzik, csak azt hozza létre. A meglévő `Paraméterek` lap változatlan marad.
- A `Paraméterek` lap két kulcsa az `InsDate` és az `Előértesítési napok`. Az A oszlop a kulcs neve, a B oszlop az érték. Új lapon az első sor az `InsDate`, a második az `Előértesítési napok`. A kulcs azonosítása a nagybetűs alakon történik, az ékezetek számítanak. Ismételt kulcsnál az első egyezés értéke kerül át, második sor nem készül. A másolás eszerint történik.
- Új `Paraméterek` lap az előző év `SZÁMLÁK{év-1}.xlsx` fájljából veszi az értékeket, akkor is, ha az az év kívül esik a két kezelt éven. Ilyen fájlt a program nem hoz létre. A nulla hosszú és a csak whitespace-ből álló érték hiány. A többi érték a szélső whitespace nélkül, egyébként változtatás nélkül másolódik. Hiányzó fájl, hiányzó kulcs vagy ilyen üres érték esetén az `InsDate` az előző év december 31-e, szöveges UTC időpontként, például `2025-12-31T00:00:00Z`. Az `Előértesítési napok` értéke `7,5,1`.
- A mappa címe a verziókezelt `Excel` szakasz `SiteHost`, `SitePath` és `FolderPath` mezője. A `FolderPath` a webhely alapértelmezett dokumentumtárának gyökerétől értendő. Graph-elem GUID nincs a beállításban. Ha a `FolderPath` mappa nincs meg, a művelet sikertelen, és a program a mappát nem hozza létre.
- Az alkalmazás saját jogon, felhasználó nélkül jelentkezik be. A `Secrets` szakasz új kulcsai: `ExcelTenantId`, `ExcelClientId`, `ExcelClientSecret`. Az értékük nincs verziókezelt fájlban.
- Az alkalmazásjogosultság `Lists.SelectedOperations.Selected`, írási joggal, csak az alapértelmezett dokumentumtárra. A program a tárban csak a `FolderPath` mappát használja.
- Az `Excel:RetryCount` kezdőértéke `3`, az `Excel:RetryDelaySeconds` kezdőértéke `5`. Ezek nem a `Nav` szakasz paraméterei. Hálózati hiba, időtúllépés, HTTP 429 és HTTP 5xx ennyiszer újrapróbálható. Zárolt vagy ütköző munkafüzetnél a művelet hibával ér véget, a meglévő lapok megmaradnak.

Nem része ennek a change-nek a számlasor rögzítése, a duplikáció kezelése, a bizonylatkapcsolat, a feldolgozás utáni `InsDate` frissítés, az értesítés, a Worker bekötése és az időzítés.

## Capabilities

### New Capabilities

- `excel-workbook`: éves SharePoint Excel-fájl és a kért havi munkalap biztosítása, a `Paraméterek` lap létrehozásával és az értékek évváltási átvételével.

### Modified Capabilities

- Nincs. A titokbetöltés és a naplómaszkolás követelményei nem változnak. Az `ExcelTenantId`, az `ExcelClientId` és az `ExcelClientSecret` a meglévő `Secrets` szakasz új kulcsa.

## Impact

- Új `OsaPro.Excel` osztálykönyvtár és `OsaPro.Excel.Tests` tesztprojekt. A munkafüzet-kezelés nem az `OsaPro.Common` része. A Worker nem hivatkozza a projektet, nem kap időzítőt, és nem hívja a műveletet.
- Új verziókezelt `Excel` szakasz: `SiteHost`, `SitePath`, `FolderPath`, `RetryCount`, `RetryDelaySeconds`. A `Secrets` szakasz három új kulcsnevet kap, érték nélkül.
- Külső rendszer: Microsoft Graph. Az írás az alapértelmezett dokumentumtár beállított mappájára korlátozódik.

## Érinti-e kritikus komponenst?

Igen. Az Excel-nyilvántartás, az `openspec/project-conventions.md` kritikus területei szerint.

## Assumptions from codebase

- A számlafeldolgozó és az értesítés-összeállító handler törzse üres (`src/OsaPro.Worker/FunctionHandlers.cs`, 8–16. sor). `TimerTrigger` és `HttpTrigger` nincs.
- A Worker csak az `OsaPro.Common` projektet hivatkozza. A `UserSecretsId` értéke `8f4e2c1a-6b7d-4e9f-a1c2-3d4e5f607182` (`src/OsaPro.Worker/OsaPro.Worker.csproj`, 8–15. sor).
- A verziókezelt `appsettings.json` a `Nav` szakaszt és a `NavLogin`, `NavTaxNumber`, `NavPassword`, `NavSigningKey` kulcsneveket tartalmazza, üres titokkal. `Excel` szakasz nincs (`src/OsaPro.Worker/appsettings.json`, 1–22. sor). A `SoftwareDevContact` értéke `info@nexande.ocm` (13. sor).
- A NAV-lekérdezés az `OsaPro.NAV` könyvtárban van. A Worker ezt a projektet nem hivatkozza. Az `src` alatt nincs Excel-projekt és nincs Excel-hívás.
