# inbound-invoice-query Specification

## Purpose

Bejövő NAV-bizonylatok lekérdezése a kezdődátum és a tárolt InsDate által meghatározott insDate ablakban, valamint egy külön kért számla teljes bizonylatának visszaadása.

## Requirements

### Requirement: Bejövő kivonat lekérdezése
A rendszer MUST a hívó kezdődátuma és tárolt InsDate értéke alapján a bejövő bizonylatok kivonatát a NAV insDate mezőjére lekérni.

#### Scenario: Két paraméterből induló lekérdezés
- **WHEN** a hívó kezdődátumot és tárolt InsDate értéket ad át
- **THEN** a rendszer a `queryInvoiceDigest` műveletet `INBOUND` iránnyal és `insDate` keresővel hívja

### Requirement: Lekérdezési ablak
A rendszer MUST az ablakot az eredeti, tárolt InsDate UTC naptári napjának `00:00:00Z` időpontjától a kapott InsDate UTC naptári napját követő nap `00:00:00Z` időpontjáig képezni, a kezdődátum `00:00:00Z` időpontjánál nem korábbról. A kapott InsDate a lekérdezés aktuális UTC időpontja. A `queryInvoiceDigest` hívás ezeket az UTC időbélyegeket kapja az `insDate` határokon.

#### Scenario: Az ablak a kezdődátumnál nem korábbi
- **WHEN** a tárolt InsDate UTC naptári napjának `00:00:00Z` időpontja a kezdődátum `00:00:00Z` időpontja elé esik
- **THEN** az ablak a kezdődátum `00:00:00Z` időpontjánál kezdődik

#### Scenario: Az ablak a kapott napot követő nap elejéig tart
- **WHEN** a hívó lekérdezi a kivonatot
- **THEN** az ablak vége a kapott InsDate UTC naptári napját követő nap `00:00:00Z` időpontja, és a hívás UTC `insDate` időbélyegeket küld

### Requirement: Legfeljebb 35 napos szeletek
A rendszer MUST a 35 napnál hosszabb ablakot egymást nem átfedő, legfeljebb 35 napos szeletekre bontani, amelyek együtt lefedik az ablakot.

#### Scenario: Hosszú ablak több szelet
- **WHEN** az ablak kezdete és vége között több mint 35 nap van
- **THEN** minden `queryInvoiceDigest` szelet legfeljebb 35 napos, és a szeletek együtt kiadják az ablakot

### Requirement: Teljes kivonatlista
A rendszer MUST minden szelet minden lapjának kivonattételét strukturált adatként visszaadni.

#### Scenario: Több lap
- **WHEN** egy szelet válasza egynél több lapot jelez
- **THEN** a visszaadott lista a további lapok tételeit is tartalmazza

#### Scenario: Művelettípusok megmaradnak
- **WHEN** a kivonat `CREATE`, `MODIFY` és `STORNO` tételt tartalmaz
- **THEN** mindhárom művelettípus szerepel a visszaadott listában

### Requirement: Üres ablak
A rendszer MUST az üres találatot sikeres, üres listaként visszaadni.

#### Scenario: Nincs bizonylat
- **WHEN** a NAV egyik szeletre sem ad kivonattételt
- **THEN** a hívás sikeres, és a lista üres

### Requirement: Külön bizonylatlekérdezés
A rendszer MUST a teljes bizonylatot csak a hívó külön kérésére lekérni.

#### Scenario: A kivonat nem kér bizonylatot
- **WHEN** a hívó csak a kivonatot kéri
- **THEN** a rendszer nem hívja a `queryInvoiceData` műveletet

#### Scenario: Kért számla
- **WHEN** a hívó számlaszámot és kiállítói adószámot ad át
- **THEN** a rendszer a `queryInvoiceData` műveletet `INBOUND` iránnyal ezekkel az adatokkal hívja

### Requirement: Kötegelt módosítás indexe
A rendszer MUST a `batchIndex` értéket csak kötegelt módosítás lekérdezésekor elküldeni.

#### Scenario: Kötegelt módosítás
- **WHEN** a kért bizonylat kötegelt módosítás, és a hívó `batchIndex` értéket ad át
- **THEN** a `queryInvoiceData` kérés tartalmazza a `batchIndex` értéket

#### Scenario: Nem kötegelt bizonylat
- **WHEN** a kért bizonylat nem kötegelt módosítás
- **THEN** a `queryInvoiceData` kérés nem tartalmaz `batchIndex` értéket

### Requirement: Teljes bizonylat visszaadása
A rendszer MUST a `queryInvoiceData` válaszának bizonylatát strukturált adatként visszaadni.

#### Scenario: Bankszámlaszám a bizonylaton
- **WHEN** a NAV-bizonylat tartalmazza a `supplierBankAccountNumber` mezőt
- **THEN** a visszaadott bizonylat ugyanezt a bankszámlaszámot tartalmazza

### Requirement: Hiányzó bankszámlaszám
A rendszer MUST a bankszámlaszám nélküli bizonylatot sikeresen visszaadni, üres bankszámlaszám mezővel.

#### Scenario: A mező hiányzik
- **WHEN** a NAV-bizonylaton nincs `supplierBankAccountNumber`
- **THEN** a hívás sikeres, és a visszaadott bankszámlaszám üres

### Requirement: Sikertelen ablak
A rendszer MUST a hibás vagy hiányos NAV-választ sikertelen lekérdezésként jelezni.

#### Scenario: Egy szelet hibás
- **WHEN** egy szelet vagy lap a megengedett újrapróbálás után is hibás vagy hiányos
- **THEN** a kivonatlekérdezés sikertelen, és nem ad vissza részleges listát sikeres eredményként

### Requirement: Újrapróbálás átmeneti hiba után
A rendszer MUST a hálózati hiba, az időtúllépés, a HTTP 429 és a HTTP 5xx után a hívást a `RetryCount` értékének megfelelő számú alkalommal újrapróbálni, két próbálkozás között `RetryDelaySeconds` másodpercet várva.

#### Scenario: Verziókezelt kezdőérték
- **WHEN** a verziókezelt `Nav` szakasz a kezdőértékeket tartalmazza
- **THEN** a `RetryCount` értéke `3`, a `RetryDelaySeconds` értéke `5`

#### Scenario: Három újrapróbálás
- **WHEN** az első hívás átmeneti kommunikációs hiba miatt meghiúsul, és a `RetryCount` értéke `3`
- **THEN** a rendszer ugyanazt a kérést még háromszor elküldi, a próbálkozások között `RetryDelaySeconds` másodperc szünettel

### Requirement: Üzleti hiba nem indul újra
A rendszer MUST a HTTP 200 melletti `funcCode` `ERROR` választ és a HTTP 4xx választ újrapróbálás nélkül sikertelenként visszaadni.

#### Scenario: Üzleti hiba
- **WHEN** a NAV válasz `funcCode` értéke `ERROR`, vagy a HTTP-státusz 4xx
- **THEN** a rendszer nem küldi újra a kérést, és a lekérdezés sikertelen

### Requirement: Verziókezelt Nav beállítás
A rendszer MUST a lekérdezést a verziókezelt `Nav` szakasz `BaseUrl` és `Software` értékeivel küldeni. A `BaseUrl` a tesztkörnyezet vagy az éles környezet gyökere.

#### Scenario: Engedélyezett gyökér és szoftverazonosító
- **WHEN** a verziókezelt `Nav` szakasz a rögzített kezdőértékeket tartalmazza
- **THEN** a `BaseUrl` értéke `https://api-test.onlineszamla.nav.gov.hu` vagy `https://api.onlineszamla.nav.gov.hu`, a `SoftwareId` értéke `HU-OSP-00000000001`, a `SoftwareName` értéke `OsaPro`, a `SoftwareOperation` értéke `ONLINE_SERVICE`, a `SoftwareMainVersion` értéke `1.0`, a `SoftwareDevName` értéke `OSP`, és a `SoftwareDevContact` értéke `info@nexande.ocm`

### Requirement: Titkos NAV-kulcsok
A rendszer MUST a technikai felhasználó bejelentkezési nevét, adószámát, jelszavát és aláírókulcsát a `Secrets` szakasz `NavLogin`, `NavTaxNumber`, `NavPassword` és `NavSigningKey` kulcsából használni.

#### Scenario: Bejelentkezés és adószám
- **WHEN** a rendszer kivonatot vagy bizonylatot kérdez le
- **THEN** a kérés a `NavLogin` és a `NavTaxNumber` értékét viszi

#### Scenario: Az érték nincs verziókezelt fájlban
- **WHEN** a verziókezelt konfiguráció a `Nav` és a `Secrets` szakaszt tartalmazza
- **THEN** a `NavLogin`, a `NavTaxNumber`, a `NavPassword` és a `NavSigningKey` értéke nincs a fájlban

### Requirement: Cserekulcs nélkül
A rendszer MUST a kivonat és a bizonylat lekérdezését `tokenExchange` és cserekulcs nélkül végrehajtani.

#### Scenario: Lekérdező hívás
- **WHEN** a rendszer kivonatot vagy bizonylatot kérdez le
- **THEN** nem hívja a `tokenExchange` műveletet, és nem használ cserekulcsot

### Requirement: A tárolt InsDate változatlan
A rendszer MUST a kapott kezdődátumot és tárolt InsDate értéket csak a lekérdezés számításához használni.

#### Scenario: Sikeres kivonat
- **WHEN** a kivonatlekérdezés sikerül
- **THEN** a rendszer nem menti a tárolt InsDate értékét
