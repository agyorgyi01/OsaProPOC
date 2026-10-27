# Proposal

## Why

A NAV-lekérdezés és az Excel-munkafüzet kezelése kész, de a beérkező bizonylat még nem kerül a nyilvántartásba. A kézi rögzítés addig marad, amíg a program fel nem veszi az új sorokat, ki nem hagyja a már rögzített számlát, össze nem köti az eredeti, módosító és sztornó bizonylatokat, és előre nem lépteti az `InsDate` kurzort.

## What Changes

- A meglévő számlafeldolgozó handler egy futásban végigviszi a rögzítést. Teszt és Development `--invoke` indítja. Időzítő és élesben hívható végpont nem készül. Az ütemezés a `schedule-and-orchestrate` change-ben marad. Értesítés nem megy ki.
- A futás szeletenként kérdez. Az első szelet a meglévő ablak elején indul: a tárolt `InsDate` UTC-napjának `00:00:00Z` időpontjánál, de nem a kezdődátum előtt. Ugyanabban a futásban a következő szelet az előző szelet végénél, a maradék intervallum elejénél indul, így a közös határ mindkét szeletben benne van. Az ablak nem számolódik újra a friss `InsDate` napjának elejére. A következő futás első szelete ismét a meglévő ablakkezdetet használja. A következő ciklus az előző végénél indul, ez 1 ms átfedés. Az Excel-beszúrás a duplikált sort nem veszi fel.
- Egy szelet ciklusa: NAV-szelet letöltése, Excel letöltése, a tételek bedolgozása, az `InsDate` helyi frissítése, Excel feltöltése. A feltöltés minden módosított éves fájlt elküld. A szelet akkor kész, ha mindegyik feltöltés sikerült. A már feltöltött fájl megmarad, ha egy későbbi fájl feltöltése sikertelen. Az `InsDate` csak az aktuális év fájljába kerül, és csak annak sikeres feltöltésével lép. Az aktuális év fájlja a többi módosított fájl után töltődik fel. Az előző év fájlja nem kap új `InsDate` értéket. Az ismétlés nem készít második sort. További szelet csak sikeres feltöltés után indul. Az utolsó szelet után a ciklus megáll. Külön fájlfoglalás nincs. Üres, sikeres szeletnél az `InsDate` is előrelép. Ha egy ciklus hibával leáll, hibabejegyzés készül, és a feldolgozás megáll. További szelet nem indul. A bejegyzés a következő Slack-üzenet anyaga; a küldés nem ebben a change-ben történik. A már feltöltött szeletek megmaradnak, a sikertelen feltöltés a SharePoint fájlt a szelet előtti állapotában hagyja, és az `InsDate` nem lép. Az ugyanilyen ismételt hiba nem készít második bejegyzést. Újrafutáskor a duplikációszűrés és a hibabejegyzés azonosítója véd. Külön bizonylatállapot és Excel-hibaoszlop nincs.
- A kezdődátum a verziókezelt, nem titkos `Processing:StartDate`, `yyyy-MM-dd` alakban. A meglévő konfigurációs sorrend felülírhatja. Hiányzó, üres vagy értelmezhetetlen érték a NAV-hívás előtt megáll, és az `InsDate` nem változik.
- Az `InsDate` kurzor a legutóbbi létező kezelt év `Paraméterek` lapja. Új éves fájl csak akkor készül, ha rögzítendő bizonylat keltéhez kell. A másolat viszi az `InsDate` értékét, és utána ez a fájl a kurzor. Az előző év cellája nem változik. Ha nincs kezelt évi fájl, a lekérdezés az aktuális év alapértelmezett `InsDate` értékét használja. Sikeres szelet után a program biztosítja a magyarországi aktuális év aktuális hónapjának fájlját, és oda írja az `InsDate` értéket. Meglévő, üres vagy hiányzó `InsDate` a NAV előtt megáll, és nem kap alapértelmezést.
- A számla azonossága a szállítói adószám és a számlaszám, csak a szélső whitespace levágásával. A kis- és nagybetű megmarad. A keresés mindkét kezelt év minden havi lapjára kiterjed, beleértve az aktuális szeletben írt sorokat. A program nem mozgat sort.
- Új sor az utolsó kitöltött sor után kerül. Kitöltött a sor, ha a 16 oszlop bármelyikében van nem üres érték. A hézag megmarad. Üres lapon az első adatsor a 2. sor. A `Számla megvan?`, a `Fizetve?`, az `Autó rendelés` és a `Megjegyzés` üresen készül. Meglévő sor adata nem változik, kivéve az eredeti számla `Kapcsolt bizonylatok` mezőjének bővítését.
- Új Excel-sorhoz és kezelt éven kívüli hibabejegyzéshez a meglévő teljesbizonylat-lekérdezés fut. A már nyilvántartott számla kimarad. A `batchIndex` csak kötegelt módosításnál megy. Sikertelen hívásnál a szelet nem sikeres, és az `InsDate` nem lép.
- A partnernév a kivonat `supplierName` értéke. Az adószám a kivonat `supplierTaxNumber` értéke, pontosan 8 számjegy, szövegként; áfakód és megyekód nem kerül hozzá. A számlaszám a kivonat számlaszáma, szöveg. A bankszámlaszám a teljes bizonylat `supplierBankAccountNumber` értéke, szöveg, hiányában üres. A fizetendő összeg a teljes bizonylat `invoiceGrossAmount` értéke. Az áfaösszeg a kivonat `invoiceVatAmount` értéke. A NAV által küldött érték kerül be, a nulla és a negatív is. Hiányzó mező üres cella, nem nulla. Az összeg számcella, két tizedesjeggyel, előjellel. A kelte és a fizetési határidő Excel-dátum, időrész nélkül. A határidő a kivonat `paymentDate` értéke, különben a kelte.
- A fizetési mód a teljes bizonylat `paymentMethod` értéke, különben a kivonaté. `TRANSFER` értéke `Átutalás`, `CASH` értéke `Készpénz`, `CARD` értéke `Bankkártya`, `VOUCHER` értéke `Utalvány`, `OTHER` értéke `Egyéb`. Hiányzó érték üres cella. Ismeretlen kód a kapott NAV-szöveg marad.
- Az áfatartalom a tételek `lineVatRate` mezőjéből készül, az első előfordulás sorrendjében, ismétlés nélkül, vesszővel és szóköz nélkül. A százalék a `vatPercentage` százszorosa, `%` jellel. Mentességnél a case kód kerül be. Egyszerűsített számlán a `vatContent` ismert párjai `27%`, `18%`, `5%` és `0%`; más tört a küldött tizedesekkel százalékká alakul. A hatályon kívüli és az eltérő áfaösszeg a NAV case kódja, a belföldi fordított adózás `FAD`, az árrés a NAV felsorolása, a `noVatCharge` pedig a `noVatCharge` szöveg.
- A `Kapcsolt bizonylatok` mező a módosító és sztornó számlaszámokat pontosvesszővel, szóköz nélkül tartalmazza. A meglévő sorrend megmarad, az új hivatkozás a végére kerül, az ismétlés kimarad. Az `Eredeti számla` csak az eredeti számlaszám. Ismételt szeletnél a már nyilvántartott módosító és sztornó hiányzó hivatkozása is felkerül. Más cella nem változik. Ha az eredeti megvan, új hiányzó-eredeti bejegyzés nem készül, a meglévő marad.
- Kezelt éven kívüli keltezésnél nincs Excel-sor. A szelet ettől még sikeres lehet, és egy tartós hibabejegyzés készül. Azonossága az adószám és a számlaszám; ismételt futás nem készít másodikat. Tartalma a partnernév, az adószám, a számlaszám, a kelte, a fizetendő összeg, a határidő, és az, hogy a kelte a kezelt éveken kívül van.
- Hiányzó eredetinél a módosító vagy sztornó sora elkészül, az `Eredeti számla` megkapja az eredeti számlaszámot, és külön hibabejegyzés készül. A szelet sikeres. A bejegyzés később sem vonódik vissza. Azonossága a módosító vagy sztornó saját adószáma és számlaszáma. A tartalom kiegészül az eredeti számlaszámmal és azzal, hogy az eredeti nincs a nyilvántartásban. Ugyanabban a szeletben megtalált eredetihez nem készül bejegyzés, és a kapcsolat felkerül.
- A hibabejegyzés a `notification` táblába kerül, `Type`, `Message`, `Taxnumber`, `Partnername` és `Invoice` mezővel. A típus `Error`. Külön időmező nem készül. A küldési állapot, az újrapróbálás és a csatorna a `compose-notification-content` change-ben marad.
- Az `ANNUL` továbbra is kívül van. A meglévő teljes ablakos kivonat, az `EnsureMonth`, a környezeti konfiguráció és az infrastruktúra követelményei nem változnak.

## Capabilities

### New Capabilities

- `inbound-invoice-processing`: bejövő NAV-bizonylatok rögzítése az Excel-nyilvántartásba, a duplikáció elkerülése, az eredeti, módosító és sztornó kapcsolat kezelése, az `InsDate` előreléptetése, és a későbbi értesítéshez tartozó hibabejegyzés elkészítése.

### Modified Capabilities

- Nincs új képesség a kivonatlekérdezésen és a munkafüzeten. Az értesítési tábla neve `notification`. A konfiguráció sorrendje és a naplózás nem változik.

## Impact

- Az `OsaPro.Worker` számlafeldolgozó handlerje, az `OsaPro.NAV` szeletenkénti használata és az `OsaPro.Excel` számlasor- és `InsDate`-írása, a hozzájuk tartozó tesztekkel.
- Új verziókezelt `Processing:StartDate`. Titkos kulcs nem készül.
- Külső rendszerek: NAV Online Számla és a SharePoint Excel. Slack-küldés és időzítő nem érintett.

## Érinti-e kritikus komponenst?

Igen. A NAV-integráció, az Excel-nyilvántartás és a bizonylatkapcsolatok. A határidők és az értesítések küldése nem készül el.

## Assumptions from codebase

- A `RunInvoiceProcessing` munkarésze üres (`src/OsaPro.Worker/FunctionHandlers.cs`). A `--invoke` csak Development környezetben indul (`src/OsaPro.Worker/WorkerEntry.cs`).
- A teljes ablakos kivonat hibás későbbi szeletnél nem ad részleges listát. A teljes bizonylat külön hívás. Az `EnsureMonth` nem ír számlasort és nem ír `InsDate` értéket.
- Az `appsettings.json` a tervezéskor nem tartalmazott `Processing` szakaszt. Az értesítési tábla neve `notification`.
