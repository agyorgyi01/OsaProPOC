# Tasks

## 1. Verziókezelt Nav beállítás

- [ ] 1.1 Az `OsaPro.Worker` `appsettings.json` fájlja kapjon `Nav` szakaszt: `BaseUrl` `https://api-test.onlineszamla.nav.gov.hu`, `RetryCount` `3`, `RetryDelaySeconds` `5`, és a `Software` mezők a proposal szerint (`SoftwareId` `HU-OSP-00000000001`, `SoftwareName` `OsaPro`, `SoftwareOperation` `ONLINE_SERVICE`, `SoftwareMainVersion` `1.0`, `SoftwareDevName` `OSP`, `SoftwareDevContact` `info@nexande.ocm`). A `Secrets` szakasz nevezze meg a `NavLogin`, `NavTaxNumber`, `NavPassword` és `NavSigningKey` kulcsot üres értékkel. Ellenőrizd: a verziókezelt fájlban a négy titkos érték üres, a `RetryCount` és a `RetryDelaySeconds` a kezdőérték, és a `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 2. Aláírt kérés

- [ ] 2.1 Az `OsaPro.Common` készítse el a két lekérdezés kézi XML-borítékát. A kérés `requestVersion` értéke `3.0`, iránya `INBOUND`, a `passwordHash` a nyers jelszó nagybetűs SHA-512 hex értéke `cryptoType=SHA-512` attribútummal, a `requestSignature` a `requestId`, a `yyyyMMddHHmmss` UTC időbélyeg és az aláírókulcs literáljának nagybetűs SHA3-512 hex értéke `cryptoType=SHA3-512` attribútummal. A nyers jelszó és az aláírókulcs ne kerüljön az XML-be. A kérés ne tartalmazzon `tokenExchange` elemet vagy cserekulcsot. Ellenőrizd: az `OsaPro.Common.Tests` egy ismert jelszóból és aláírókulcsból a várt hash értékeket és a szoftverazonosító mezőket látja az XML-ben, a nyers titkot nem. A `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 3. Számlaadattípusok

- [ ] 3.1 Az `OsaPro.Common` forrásába kerüljön a NAV Online Számla 3.0 számlaadatsémájából készült C# típuskészlet. A `queryInvoiceData` válasz Base64 `invoiceData` mezője ezekre a típusokra képződjön; ha a `compressedContentIndicator` igaz, a dekódolás után a tartalom tömörítését bontsa. Ellenőrizd: az `OsaPro.Common.Tests` egy tömörítetlen és egy tömörített mintából visszaadja a `supplierBankAccountNumber` értéket, hiányzó mezőnél üres bankszámlaszámmal sikeres az olvasás. A `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 4. HTTP-küldés és újrapróbálás

- [ ] 4.1 A közös küldő `POST` kérést küldjön a `BaseUrl` és az `/invoiceService/v3/queryInvoiceDigest` vagy `/invoiceService/v3/queryInvoiceData` útvonalra, `content-type=application/xml` és `accept=application/xml` fejléccel, UTF-8 törzzsel. Átmeneti hiba (hálózati hiba, időtúllépés, HTTP 429, HTTP 5xx) után ugyanazt a kérést még `RetryCount` alkalommal küldje, két küldés között `RetryDelaySeconds` másodpercet várva. A várakozás helyettesíthető legyen. HTTP 200 és `funcCode` `ERROR`, valamint HTTP 4xx válasz ne induljon újra. Ellenőrizd: az `OsaPro.Common.Tests` hamis HTTP-kezelővel három újrapróbálást számol `RetryCount` `3` mellett, a szünetet valódi alvás nélkül, és üzleti hibánál egyetlen küldést lát. A `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 5. Kivonatlekérdezés

- [ ] 5.1 Az `InboundInvoiceQuery.QueryInvoiceDigest` a kezdődátumból és a tárolt `InsDate` értékből számolja az ablakot. A kezdődátum naptári nap, a tárolt `InsDate` UTC időpont. A kezdet az eredeti, tárolt `InsDate` UTC naptári napjának `00:00:00Z` időpontja, de nem a kezdődátum `00:00:00Z` időpontja előtt. A kapott `InsDate` a teszt által adott aktuális UTC időpont. A vég ennek a napnak a következő napja `00:00:00Z` időpontban. A `queryInvoiceDigest` hívás ezeket az UTC időbélyegeket kapja. A 35 napnál hosszabb ablak szeletei legfeljebb 35 naposak, nem fedik egymást, és a következő szelet 1 milliszekundummal az előző vége után indul. A metódus minden lapot elkér, és a `CREATE`, `MODIFY`, `STORNO` tételeket strukturált kivonatként adja vissza. Üres találat sikeres, üres lista. Hibás vagy hiányos szelet vagy lap a megengedett újrapróbálás után kivétel, részleges lista nélkül. A metódus nem hívja a `queryInvoiceData` műveletet, és nem menti a kapott dátumokat. Ellenőrizd: az `OsaPro.Common.Tests` a napváltót, a kezdődátum előtti levágást, a több szeletet, a több lapot, az üres ablakot és a sikertelen szeletet hamis HTTP-válaszokkal igazolja. A `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 6. Bizonylatlekérdezés

- [ ] 6.1 Az `InboundInvoiceQuery.QueryInvoiceData` csak a hívó külön kérésére fusson. A kérés vigye a számlaszámot, a kiállító adószámát és az `INBOUND` irányt. Kötegelt módosításnál vigye a `batchIndex` értéket, egyébként hagyja ki. A válasz a teljes bizonylat C# objektuma legyen, benne a kibocsátó bankszámlaszámával, ha a számla tartalmazza. Ellenőrizd: az `OsaPro.Common.Tests` a kérés XML-jében látja vagy hiányolni látja a `batchIndex` elemet, és a visszaadott bizonylat bankszámlaszáma egyezik a mintával. A `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 7. Együttes ellenőrzés

- [ ] 7.1 A számlafeldolgozás handlerje ne hívja a lekérdezést, és ne kapjon `TimerTrigger` vagy `HttpTrigger` attribútumot. A forrás ne hívja a `tokenExchange` műveletet. Ellenőrizd: a `dotnet test OsaPro.sln` sikeres, és a Worker handler törzse nem indít NAV-hívást.

## Workflow follow-up

- A change implementációja csak a grill-me lezárása és az explicit emberi tervjóváhagyás után kezdhető.
- Az implementáció és a kódellenőrzés lezárása után a change archiválható. A delta specek szinkronja külön explicit jóváhagyást igényel.
