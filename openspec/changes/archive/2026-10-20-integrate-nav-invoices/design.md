# Design

## Context

A viselkedés: `specs/inbound-invoice-query/spec.md`. A motiváció: `proposal.md`.

Az `OsaPro.Common` betölti a konfigurációt, és a `Secrets` szakasz feloldott értékeit maszkolja. Az `OsaPro.Worker` ezt használja. A számlafeldolgozás handlerjének törzse üres, időzítő és HTTP-végpont nincs. NAV-hívás még nincs.

## Goals / Non-Goals

**Goals:**

- Két, egymást nem hívó lekérdező metódus az `OsaPro.NAV` könyvtárban. A hívó paramétert ad, és C# objektumot kap.
- A NAV-kérés XML-je és az aláírása tesztből, élő NAV nélkül ellenőrizhető.
- A verziókezelt `Nav` szakasz és a két új `Secrets` kulcsnév az `OsaPro.Worker` `appsettings.json` fájljában van.

**Non-Goals:**

- A számlafeldolgozás handlerjének bekötése, időzítő, Excel, `InsDate` mentés, naptári év szűrés, értesítés, számla-beküldés.
- Élő hívás a NAV teszt- vagy éles környezetére a tesztfutásban.
- A megszakított ablak folytatása.

## Decisions

### 1. A lekérdezés az OsaPro.NAV könyvtárban van

Az `InboundInvoiceQuery` az `OsaPro.NAV` osztálykönyvtárban van, és két publikus metódust ad:

- `QueryInvoiceDigest(startDate, storedInsDate)` a kivonatlistát adja.
- `QueryInvoiceData(invoiceNumber, supplierTaxNumber, batchIndex)` a teljes bizonylatot adja. A `batchIndex` csak kötegelt módosításnál kap értéket; egyébként a kérésből kimarad.

A digest metódus nem hívja a bizonylatmetódust. Az `OsaPro.Common` nem tartalmaz NAV-típust. Az `OsaPro.Worker` handlerje üres marad, és ebben a change-ben nem hivatkozza az `OsaPro.NAV` projektet. Az `OsaPro.NAV.Tests` a metódusokat közvetlenül hívja, és a verziókezelt beállítást meg a User Secretet az `OsaPro.Worker` projektből olvassa.

Alternatíva: a metódusok az `OsaPro.Common` könyvtárban. Elvetve, mert a NAV-modul külön projekt. Alternatíva: a metódusok az `OsaPro.Worker` handlerében. Elvetve, mert a dátumok kívülről jönnek, az időzítés későbbi change, és a handler bekötése Excel nélkül nem tudná a tárolt `InsDate` értékét előállítani.

### 2. A HTTP-hívás XML POST

Mindkét metódus `POST` kérést küld. A cím a verziókezelt `BaseUrl` és az útvonal: `/invoiceService/v3/queryInvoiceDigest` vagy `/invoiceService/v3/queryInvoiceData`. A fejlécek: `content-type=application/xml` és `accept=application/xml`, UTF-8.

A közös kérésrész:

- `requestId`: a minta szerinti, kérésenként új azonosító.
- `timestamp`: UTC.
- `requestVersion`: `3.0`.
- `user/login` és `user/taxNumber`: a `Secrets:NavLogin` és a `Secrets:NavTaxNumber`.
- `user/passwordHash`: a `Secrets:NavPassword` nyers jelszavának nagybetűs SHA-512 hex értéke, `cryptoType` attribútummal `SHA-512`. A nyers jelszó nem kerül az XML-be.
- `user/requestSignature`: a `requestId`, a `timestamp` `yyyyMMddHHmmss` UTC maszkja és a `Secrets:NavSigningKey` literál összefűzésének nagybetűs SHA3-512 hex értéke, `cryptoType` attribútummal `SHA3-512`. Az aláírókulcs nem kerül az XML-be.
- `software`: a verziókezelt `Software` mezők.

`tokenExchange` és cserekulcs nincs. A névterek a bekötött NAV 3.0 XSD `targetNamespace` értékei.

Alternatíva: a jelszó és az aláírókulcs nyers elküldése. Elvetve, mert a NAV a hash értékeket várja.

### 3. A naptári napból UTC időpont megy a kivonat hívásba

A kezdődátum naptári nap. A tárolt `InsDate` UTC időpont. Az ablak kezdete az eredeti, tárolt `InsDate` UTC naptári napjának `00:00:00Z` időpontja. Ha ez a kezdődátum `00:00:00Z` időpontja elé esne, az ablak ott indul. A kapott `InsDate` a lekérdezés aktuális UTC időpontja. Az ablak vége ennek a napnak a következő napja `00:00:00Z` időpontban. A `queryInvoiceDigest` `insDate/dateTimeFrom` és `insDate/dateTimeTo` határai ezek az UTC időbélyegek. A teszt órát ad, nem a gép óráját olvassa.

A 35 napnál hosszabb ablak szeletei egymás után következnek. Egy szelet hossza legfeljebb 35 nap. A következő szelet kezdete az előző végénél 1 milliszekundummal később van, mert a NAV `insDate` határai inkluzívak. Így a határidőpontra eső bizonylat egy szeletben szerepel, és a szeletek között nincs lefedetlen időpont.

Az üres `availablePage` sikeres, üres lista. A `page` 1-től indul, és a metódus az `availablePage` értékéig kér lapot. A visszaadott kivonat a válasz `invoiceDigest` mezőit tartalmazó C# objektum.

Alternatíva: a nap határa budapesti helyi idő. Elvetve, mert a kapott naptári napból közvetlenül UTC időpont készül.

### 4. A teljes bizonylat a dekódolt számla XML

A `queryInvoiceData` válasz `invoiceData` mezője Base64. Ha a `compressedContentIndicator` igaz, a dekódolás után a tartalom kitömörítendő. A kapott XML a NAV számlaadatsémájának C# típusaira képződik. A `supplierBankAccountNumber` ennek a `supplierInfo` része. Hiányzó elem üres mező, sikeres hívás.

A számlaadattípusok a NAV 3.0 `invoiceData` sémából készülnek, és a forrásban vannak. A két operáció kérés- és válaszborítéka kézzel írt, csak a használt elemekkel.

Alternatíva: csak a bankszámlaszámot kiolvasó részfa. Elvetve, mert a metódus a teljes bizonylatot adja vissza. Alternatíva: a borítékot is a teljes API-sémából generálni. Elvetve, mert az a beküldő operációkat is behozná, amelyeket ez a change nem hív.

### 5. Az újrapróbálás a híváson belül van

Átmeneti hiba: hálózati hiba, időtúllépés, HTTP 429 és HTTP 5xx. Ezek után a metódus ugyanazt a kérést még `RetryCount` alkalommal küldi. Két küldés között `RetryDelaySeconds` másodpercet vár. A `3` és `5` kezdőérték mellett ez egy első hívás és három újrapróbálás, 5 másodperc szünettel.

HTTP 200 és `funcCode` `ERROR`, valamint a HTTP 4xx válasz üzleti vagy kéréshiba. Ezek nem indulnak újra. A várakozást a teszt helyettesíti, a teszt nem alszik.

Ha egy szelet vagy lap az újrapróbálás után is hibás vagy hiányos, a digest metódus kivételt dob, és nem ad vissza részleges listát. A kapott dátumokat egyik metódus sem menti.

Alternatíva: minden hibát újrapróbálni. Elvetve, mert az érvénytelen kérés ismétlése nem változtat az eredményen.

### 6. A Nav szakasz verziókezelt, a két kulcs a Secrets szakaszban van

Az `OsaPro.Worker` `appsettings.json` fájlja:

- `Nav:BaseUrl` a `https://api-test.onlineszamla.nav.gov.hu` vagy a `https://api.onlineszamla.nav.gov.hu` érték. A fájl egyet tartalmaz a kettő közül.
- `Nav:RetryCount` = `3`, `Nav:RetryDelaySeconds` = `5`
- `Nav:Software` a proposalban rögzített szoftverazonosítókkal
- `Secrets:NavLogin`, `Secrets:NavTaxNumber`, `Secrets:NavPassword` és `Secrets:NavSigningKey` üres értékkel

A nem üres bejelentkezési név, adószám, jelszó és aláírókulcs User Secretsből vagy `Secrets__NavLogin`, `Secrets__NavTaxNumber`, `Secrets__NavPassword` és `Secrets__NavSigningKey` környezeti változóból jön. A későbbi forrás felülírja az üres verziókezelt értéket. A meglévő maszkoló ezeket a `Secrets` értékeket takarja.

Alternatíva: a jelszó a `Nav` szakaszban. Elvetve, mert a maszkoló csak a `Secrets` szakaszt ismeri.

## Risks / Trade-offs

- [A szelethatár inkluzív, a következő szelet 1 milliszekundummal később indul] → A NAV által kifejezhető időpontok lefedettek. Finomabb időbélyeg nincs a sémában.
- [A teljes számla séma nagy, és a 2025.09.04-i leírástól elkészhet] → A bekötött XSD a 3.0-s számlaadatséma. A teszt egy kitömörített és egy tömörített mintából olvassa a bankszámlaszámot és a bizonylat gyökerét.
- [Üres verziókezelt jelszó élő híváskor felülírás nélkül hibás aláírást ad] → A teszt nem a verziókezelt üres értéket használja. Az élő érték a User Secrets vagy a környezeti változó.
- [A UTC naptári nap a budapesti éjféltől eltér] → A hívás a paraméter naptári napjának `00:00:00Z` időpontját használja, helyi időátváltás nélkül.

## Migration Plan

A handler üres marad, ezért a telepítés önmagában nem indít NAV-hívást. Visszavonáskor a `Nav` szakasz és a négy üres `Secrets` kulcsnév kivehető, az `OsaPro.NAV` projekt törölhető. Adatmigráció nincs.
