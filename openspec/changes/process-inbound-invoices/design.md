# Design

## Context

A viselkedés: `specs/inbound-invoice-processing/spec.md`. A motiváció: `proposal.md` - Why.

A `QueryInvoiceDigest` a teljes ablak minden szeletét elkéri, és hibás későbbi szeletnél nem ad vissza listát. A `NavTimestamps.Slices` a következő szeletet 1 ms-mal később kezdi, hogy a meglévő kivonatlekérdezés szeletei ne fedjék egymást. A `QueryInvoiceData` külön hívás. Az `EnsureMonth` fájlt és lapot biztosít, számlasort és `InsDate` értéket nem ír. A `RunInvoiceProcessing` munkarésze üres. A Worker nem hivatkozza a NAV- és az Excel-projektet. Az értesítési tábla neve `notification`. A küldési állapot, az újrapróbálás és a csatorna későbbi change.

## Goals / Non-Goals

**Goals:**

- A számlafeldolgozás egy futása a spec szerint rögzít, a meglévő handlerből.
- A mezőképzés, a szeletvágás és az azonosság Graph és NAV nélkül tesztelhető.
- A meglévő teljes ablakos kivonat és az `EnsureMonth` szerződése változatlan marad.

**Non-Goals:**

- Időzítő, éles végpont, Slack-küldés, értesítési szöveg, küldési állapot és újrapróbálás.
- A `NavTimestamps.Slices` átírása és a kanonikus kivonatspec módosítása.
- A `notification` tábla küldési oszlopainak felvétele.

## Decisions

### 1. A futás a Workerben áll össze, a szabály külön marad

A `RunInvoiceProcessing` a rögzítést hívja. Új trigger nem készül. A `--invoke` útja marad.

A szeletvágás, az azonosság, a sor értékei, az áfatartalom és a kapcsolatdöntés tiszta függvény. Ezek nem hívnak NAV-ot, Graphot és táblát. A NAV-objektumokból a Worker képezi a bemenetüket. A Worker hivatkozza az `OsaPro.NAV` és az `OsaPro.Excel` projektet.

Az azonosság a szállítói adószám és a számlaszám, csak a szélső whitespace levágásával, sorrendi összehasonlítással. Nem a `WorkbookRules.Identity` nagybetűsítése.

Alternatíva: a szabályok az `OsaPro.Common` könyvtárban. Elvetve, mert a Common a naplózás és a konfiguráció. Alternatíva: a `QueryInvoiceDigest` adja vissza a részleges szeleteket. Elvetve, mert a hibás teljes ablak továbbra sem adhat vissza részleges listát.

### 2. A feldolgozás saját szeletvágást használ

Az ablak kezdete és vége a meglévő `NavTimestamps.Window`. A feldolgozás szeletei ettől külön készülnek. Egy szelet legfeljebb 35 nap. A következő szelet az előző végénél indul. A közös határ 1 ms átfedés, mindkét szeletben benne van. Az Excel-beszúrás a duplikált sort elhagyja. A vágás a futás elején, a kiolvasott `InsDate` alapján készül, és sikeres szelet után nem számolódik újra.

A `InboundInvoiceQuery` új művelete egy megadott `from`–`to` tartomány minden lapját kéri. Lap- vagy szelethiba kivétel, ennek a tartománynak a tételei nélkül. A `QueryInvoiceDigest` és a `NavTimestamps.Slices` marad.

Alternatíva: a meglévő `Slices` használata. Elvetve, mert az 1 ms-ot hagy ki a határon, a spec pedig a közös határt kéri.

### 3. Egy szelet két menetben rögzül

Az első menet a kivonat sorrendjében halad. Már nyilvántartott számla kimarad, teljes bizonylat sem kell hozzá. Kezelt éven kívüli tételhez teljes bizonylat és egy hibabejegyzés készül, Excel-sor nem. Egyébként teljes bizonylat és új sor készül, és a sor azonnal belép a szelet keresési készletébe.

A második menet a szelet minden `MODIFY` és `STORNO` tételét nézi, a már nyilvántartottakat is. Ha az eredeti a készletben megvan, a hiányzó számlaszám a `Kapcsolt bizonylatok` végére kerül, és más cella nem változik. Új hiányzó-eredeti bejegyzés nem készül. A meglévő bejegyzés marad. Ha az eredeti nincs meg, és a módosító vagy sztornó sora most készül, az `Eredeti számla` az eredeti számlaszám, és külön hibabejegyzés készül. A bejegyzés később nem törlődik.

A teljes bizonylat hibája, a NAV-letöltés, az Excel-letöltés és a feltöltés hibája leállítja a ciklust az `InsDate` előreléptetése előtt. Hibabejegyzés készül, további szelet nem indul. A helyi, még nem feltöltött Excel-másolat nem kerül fel. A már feltöltött szelet és a már elkészült hibabejegyzés megmarad. Ugyanilyen ismételt hiba nem készít második bejegyzést. A kezelt éven kívüli tétel és a hiányzó eredeti nem állítja le a ciklust. Üres, sikeres szeletnél az `InsDate` akkor is a feltöltött fájlba kerül. A hibás ciklus bejegyzése a következő Slack-üzenet anyaga. A küldés nem ebben a change-ben történik.

Alternatíva: a kapcsolat azonnal, a tétel sorrendjében dől el. Elvetve, mert a később álló eredeti hamis hiányzó-eredeti bejegyzést kapna.

### 4. Egy szelet egy letöltés és egy feltöltés

A szelet ciklusa: a NAV-szelet letöltése, az Excel letöltése, a tételek bedolgozása a helyi másolatba, az `InsDate` frissítése ugyanebben a másolatban, majd az Excel feltöltése. A következő NAV-szelet csak ezután indul. Az utolsó szelet után a ciklus megáll. Checkout, checkin és egyéb fájlfoglalás nincs. Graph Excel-munkamenet nincs. Az `EnsureMonth` továbbra sem ír számlasort és nem ír `InsDate` értéket; a szelet nem ezen az úton ír.

A teljes bizonylat és a hibabejegyzés a bedolgozásban készül, az `InsDate` frissítése és a feltöltés előtt. Sikertelen bedolgozásnál feltöltés nincs. A feltöltés minden módosított éves fájlt elküld. A szelet akkor kész, ha mindegyik sikerült. A már feltöltött fájl megmarad, ha egy későbbi sikertelen. Az `InsDate` csak az aktuális év fájljába kerül, és csak annak sikeres feltöltésével lép. Az aktuális év fájlja a többi módosított fájl után töltődik fel. Az előző év fájlja nem kap új `InsDate` értéket. Az ismétlés a már fent lévő sort nem írja újra.

A kurzor a legújabb létező kezelt év `Paraméterek` lapja. Nincs ilyen fájl: a lekérdezés az aktuális magyarországi év alapértelmezett `InsDate` értékét használja, NAV előtt fájl nélkül. Üres vagy hiányzó érték a NAV előtt megáll, alapértelmezés írása nélkül.

A sor a bizonylat kelte szerinti hónapba kerül. A bedolgozás a helyi másolatban történik. Az előző év cellája nem változik. A helyi másolat a szelet végének UTC időpontját kapja, ezredmásodperccel, a magyarországi aktuális év aktuális hónapjának fájljában. A hiányzó fájl vagy havi lap a helyi másolatban készül, és a feltöltés viszi fel.

Alternatíva: foglalás a NAV-hívás idejére. Elvetve, mert a ciklus letölt, bedolgoz, feltölt, külön lock nélkül.

### 5. A hibabejegyzés tartalom, küldési oszlop nélkül

A bejegyzés kezelése az `INotificationLog` felület, a táblaírás külön osztály. Azonosság, ok és a spec mezői a `Message` szövegébe kerülnek. A `notification` tábla sora `Type`, `Message`, `Taxnumber`, `Partnername` és `Invoice`. Éven kívüli és hiányzó eredeti sor a három azonosságmezőt a bizonylat adataival tölti. Hibás ciklus sora üres szöveggel írja őket. A hibasor típusa `Error`. A `Notification` típus későbbi értesítéshez van. A Table Storage saját `Timestamp` mezőt ad, külön időmező nem készül. Az `Add` ugyanarra az azonosságra nem ír második sort. Küldési állapot, újrapróbálás és csatorna nem kerül a sorba.

A tábla címe a nem titkos `Processing:NotificationTableUri`. A kezdődátum a nem titkos `Processing:StartDate`. Mindkettő a meglévő konfigurációs sorrenddel felülírható. Hiányzó vagy értelmezhetetlen érték a NAV előtt megáll, `InsDate` nélkül. A teszt hamis tárolót kap, táblahívás nélkül.

Kezelt éven kívüli tétel nem kap hiányzó-eredeti bejegyzést, mert sora sem készül.

Alternatíva: a bejegyzés csak a folyamat memóriájában él. Elvetve, mert az ismételt futás nem látná, és második bejegyzést készítene. Alternatíva: a küldési séma most készül. Elvetve, mert az a `compose-notification-content` change.

### 6. A kezdődátum és a mezők a Worker `appsettings.json` fájljában jelennek meg

A verziókezelt fájl a `Processing:StartDate` kulcsot kapja. A `Processing:NotificationTableUri` a környezet saját címe, ezért a verziókezelt fájlban üres, és a későbbi forrás tölti. Titkos kulcs nem készül.

A sor mezői a spec szerint képződnek a kivonatból és a teljes bizonylatból. A felhasználói négy oszlop üresen kerül az új sorba. Meglévő sornál csak a `Kapcsolt bizonylatok` bővül.

Alternatíva: a táblacím titok. Elvetve, mert a cím nem titok, a hozzáférés managed identity.

## Risks / Trade-offs

- [A közös határ kétszer jön a NAV-ból] → az azonosság a második sort eldobja.
- [A megszakadt szelet sorai `InsDate` nélkül maradnak] → az ismétlés a sort és a hibabejegyzést azonosság alapján kihagyja.
- [A letöltés és a feltöltés között a felhasználó is írja a fájlt] → a feltöltés a letöltött másolat bedolgozott változatát küldi. Külön foglalás nincs.
- [A későbbi change küldési oszlopokat vár egy üres táblán] → ez a change csak a tartalom mezőit írja, a küldési oszlopokat nem foglalja le.
- [Sok havi lap olvasása egy szelet elején] → a nyilvántartás két év, a keresés a szelethez letöltött helyi másolaton megy, nem cellánkénti Graph-hívással.

## Migration Plan

Új telepítési erőforrás nincs. Az első futás előtt a `Processing:StartDate` és a `Processing:NotificationTableUri` a meglévő sorrendben érvényes érték. Hiányuknál a futás NAV és írás nélkül megáll. Visszalépésnél a már kiírt sor és `InsDate` marad; a program nem töröl sort és nem von vissza hibabejegyzést.
