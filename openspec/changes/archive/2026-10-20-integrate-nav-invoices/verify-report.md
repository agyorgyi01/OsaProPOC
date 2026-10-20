# Verify: integrate-nav-invoices

2026-10-20. Branch: `verify/integrate-nav-invoices`, a `d36ecc2` impl commitból.
Kritikus komponens: igen, NAV-integráció. Merge és archiválás nem történt.

A riport a munkafát nézi. Ez a commitolt impl állapoton felül tartalmazza az `OsaPro.NAV` kiszervezést, a titkot olvasó teszteket és az éles `BaseUrl` értéket. Ezek nincsenek commitban.

`openspec validate integrate-nav-invoices --type change --strict` és `openspec validate --all --strict` sikeres. `dotnet test OsaPro.sln`: 27 teszt, 0 hiba (Common 8, Worker 4, NAV 15).

## Summary

| Dimension | Status |
|---|---|
| Completeness | 7/7 tasks, 16 ADDED requirements. REMOVED és RENAMED nincs. |
| Correctness | 16/16 requirements match, 22/22 scenarios covered |
| Coherence | Followed |

## CRITICAL

Nincs.

## WARNING

Nincs.

## SUGGESTION

1. A 2.1 feladat ismert jelszóból és aláírókulcsból várja a hash ellenőrzését. A `Request_contains_hashes_software_and_no_raw_secret` a Worker user secretjéből számolja a hash-t, és ellenőrzi, hogy a nyers titok nincs az XML-ben. Teendő: a feladat szövege mondja ki, hogy a teszt a betöltött titkot hash-eli.
2. A `NavTimestamps.Slices` pontszelete, amikor a következő szelet kezdete az ablak végére esne, nincs tesztelve. A hosszú ablak tesztje a 35 napos, 1 milliszekundummal tolt szeleteket nézi. Teendő: egy ablak, amelynek vége pontosan 1 milliszekundummal van az utolsó teljes szelet után.

## Lefedett működés

A kivonat a kezdődátumból és a tárolt InsDate-ből számol, UTC napra vág, a kezdődátum előtt nem indul, és a kapott napot követő nap `00:00:00Z` időpontjáig tart. A 35 napnál hosszabb ablak nem átfedő szeletekre bomlik. Minden lap `CREATE`, `MODIFY` és `STORNO` tétele visszaadódik. Üres találat és üres ablak üres lista, HTTP-hívás nélkül az üres ablaknál. Hibás későbbi szelet kivétel, részleges lista nélkül. A kivonat nem hívja a `queryInvoiceData` műveletet.

A bizonylatlekérdezés külön metódus. Kötelező a számlaszám, a kiállító adószáma és az `INBOUND` irány. A `batchIndex` csak akkor megy a kérésbe, ha a hívó átadja. A válasz a teljes bizonylat, tömörített és tömörítetlen Base64 mintából. Hiányzó bankszámlaszám üres mezővel sikeres.

Az átmeneti hiba (hálózat, időtúllépés, HTTP 429, HTTP 5xx) után még `RetryCount` küldés van, a szünet helyettesítve. A `funcCode` `ERROR` és a HTTP 4xx nem indul újra. A jelszó SHA-512, az aláírás SHA3-512, a nyers titok nincs az XML-ben. `tokenExchange` nincs a forrásban. A handler nem hívja a lekérdezést, trigger nincs. A tárolt InsDate-et a lekérdezés nem menti.

## Beépített döntés

A verziókezelt `BaseUrl` a `https://api-test.onlineszamla.nav.gov.hu` vagy a `https://api.onlineszamla.nav.gov.hu` lehet. A spec, a proposal, a design és az 1.1 feladat ezt rögzíti. A `Versioned_nav_settings_keep_secrets_empty` mindkét címet elfogadja. Az `appsettings.json` érvényes értéke az éles cím, a tesztcím kommentben van.

## Final Assessment

No critical issues or warnings. 2 suggestions to consider. Ready for archive (with noted improvements).

A `BaseUrl` döntés a kritikus NAV-terület warningját lezárta. Merge és archiválás nem indul.
