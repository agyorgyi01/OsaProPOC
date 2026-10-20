# Proposal

## Why

A bejövő számlák rögzítéséhez az OsaPro-nak a NAV Online Számla rendszerből kell lekérnie a befogadott bizonylatokat. Az alkalmazásalap kész, a lekérdezés még nincs meg.

## What Changes

- Új lekérdezés készül a bejövő (`INBOUND`) bizonylatokra az Online Számla API 3.0 szerint. A lista a `POST /queryInvoiceDigest` hívás. A kereső a NAV `insDate` mezője.
- A kezdődátum naptári nap, a tárolt `InsDate` UTC időpont. Mindkettő bemenő paraméter. A `queryInvoiceDigest` hívás UTC időbélyeget kap: az ablak az eredeti, tárolt `InsDate` UTC naptári napjának `00:00:00Z` időpontjánál indul, de nem a kezdődátum `00:00:00Z` időpontja előtt, és a kapott `InsDate` UTC naptári napját követő nap `00:00:00Z` időpontjáig tart, legfeljebb 35 napos szeletekben. A kapott `InsDate` a lekérdezés aktuális UTC időpontja. A tárolt `InsDate` nem a NAV-bizonylat `insDate` mezője. Ez a change nem olvassa és nem menti.
- A két lekérdezés két külön metódus. Mindkettő a NAV-válasz adattartalmát C# osztályokban adja vissza. A digest metódus minden lapot visszaad. A `CREATE`, `MODIFY` és `STORNO` tételek a válaszban maradnak. Üres ablak sikeres, üres lista.
- A `queryInvoiceData` metódus csak akkor fut, ha a hívó külön kéri. A kérés a számlát egyértelművé tevő adatokat viszi: számlaszám, `INBOUND` irány, a kiállító adószáma, kötegelt módosításnál pedig a digest `batchIndex` értéke. A metódus a teljes bizonylatot adja vissza C# osztályokban. A kibocsátó bankszámlaszáma ennek a bizonylatnak a `supplierBankAccountNumber` mezője. Ha a számla nem tartalmazza, a mező üres, és a hívás sikeres.
- Hibás vagy hiányos NAV-válasz nem számít sikeres ablaknak. Hálózati hiba, időtúllépés, HTTP 429 és HTTP 5xx után a hívás újrapróbálható. Az újrapróbálások száma a `Nav:RetryCount`, a két próbálkozás közötti szünet a `Nav:RetryDelaySeconds` verziókezelt appsettings paraméter. A kezdőérték `3` és `5`. A HTTP 200 melletti `funcCode` `ERROR` és a HTTP 4xx nem indul újra. A megszakított feldolgozás folytatása nem készül.
- A verziókezelt `Nav` szakasz tartalmazza a `BaseUrl`, `RetryCount`, `RetryDelaySeconds` és `Software` adatokat, titkos érték nélkül. A `BaseUrl` a tesztkörnyezet vagy az éles környezet gyökere: `https://api-test.onlineszamla.nav.gov.hu` vagy `https://api.onlineszamla.nav.gov.hu`. A szoftverazonosító mezők: `SoftwareId` `HU-OSP-00000000001`, `SoftwareName` `OsaPro`, `SoftwareOperation` `ONLINE_SERVICE`, `SoftwareMainVersion` `1.0`, `SoftwareDevName` `OSP`, `SoftwareDevContact` `info@nexande.ocm`.
- A technikai felhasználó bejelentkezési neve, adószáma, jelszava és aláírókulcsa a `Secrets` szakasz `NavLogin`, `NavTaxNumber`, `NavPassword` és `NavSigningKey` kulcsa. Az értékük forráson kívül marad. Cserekulcs és `/tokenExchange` nem kell.

Nem része ennek a change-nek az Excel olvasása és írása, az `InsDate` mentése, a naptári év és a már rögzített sorok szűrése, az ütemezés, az értesítés, valamint a számla-beküldés.

## Capabilities

### New Capabilities

- `inbound-invoice-query`: bejövő NAV-bizonylatok lekérdezése `insDate` ablakkal, és egy hívó által kért számla teljes bizonylatának visszaadása C# osztályokban.

### Modified Capabilities

- Nincs. A titokbetöltés és a naplómaszkolás követelményei nem változnak. A `NavLogin`, a `NavTaxNumber`, a `NavPassword` és a `NavSigningKey` a meglévő `Secrets` szakasz új kulcsa.

## Impact

- Új `OsaPro.NAV` osztálykönyvtár és `OsaPro.NAV.Tests` tesztprojekt. A lekérdező kód nem az `OsaPro.Common` része. A `Nav` beállítás az `OsaPro.Worker` `appsettings.json` fájljában marad. A számlafeldolgozás handlerje nem hivatkozza a NAV-projektet, nem kap időzítőt, és nem olvas Excelt.
- Új verziókezelt `Nav` szakasz. A `Secrets` szakasz négy új kulcsnevet kap, érték nélkül.
- Külső rendszer: NAV Online Számla teszt interfész. Excel, SharePoint és Slack nem érintett.

## Érinti-e kritikus komponenst?

Igen. A NAV-integráció, az `openspec/project-conventions.md` kritikus területei szerint.

## Assumptions from codebase

Az `OsaPro.Worker` számlafeldolgozó handlerje létezik, és a munkarésze üres (`src/OsaPro.Worker/FunctionHandlers.cs`). Időzítő és HTTP-végpont nincs. A verziókezelt `appsettings.json` csak üres `Secrets` szakaszt tartalmaz. NAV-hívás a kódban nincs. A titokmaszkoló a `Secrets` szakasz értékeit takarja, ezért a jelszó és az aláírókulcs csak akkor esik a meglévő szabály alá, ha ebben a szakaszban van.
