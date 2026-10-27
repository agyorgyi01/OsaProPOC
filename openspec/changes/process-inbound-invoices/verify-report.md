# Verify: process-inbound-invoices

2026-10-27. Branch: `impl/process-inbound-invoices`. Az utolsó commit `55d46e8`. A riport a munkafát nézi: az értesítési tábla, a mezők és a KISS-egyszerűsítés ebben a commitban még nincs.

A `verify/process-inbound-invoices` ág nem nyílt meg. A Git-munkarend szerint az a lezárt impl commitból indul, ez a munkafa pedig nincs commitolva. Főágra emelés, archiválás és push nem történt.

Kritikus komponens: igen. NAV-integráció, Excel-nyilvántartás és bizonylatkapcsolatok. A határidők és az értesítések küldése nem készül el.

`openspec validate --all --strict`: 6 elem, 0 hiba. `dotnet test OsaPro.sln`: 84 teszt, 0 hiba (Common 20, Excel 30, Worker 18, NAV 16).

## Summary

| Dimension | Status |
|---|---|
| Completeness | 13/13 tasks, 33 ADDED requirements. MODIFIED, REMOVED és RENAMED a delta specben nincs. |
| Correctness | 33/33 requirements match, 66/66 scenarios covered |
| Coherence | Followed |

## CRITICAL

Nincs.

## WARNING

Nincs. A „Csak a tartalom” forgatókönyv kettévált. Éven kívüli és hiányzó eredeti sor a három azonosságmezőt a bizonylat adataival tölti. Hibás ciklus sora üres szöveggel írja őket. A döntés 2026-10-27-én született.

## SUGGESTION

1. Nincs közvetlen teszt a `Processing:StartDate` későbbi konfigurációs forrásból való felülírására, az ismeretlen fizetési kódra és az egyszerűsített `0.1` áfatartalomra. A kód kezeli mindhármat: a környezet a JSON után töltődik (`src/OsaPro.Common/SecretConfiguration.cs:17`), az ismeretlen kód a kapott szöveg marad (`src/OsaPro.Worker/ProcessingRules.cs:107`), a más tört százalékká alakul (`src/OsaPro.Worker/ProcessingRules.cs:214`). Teendő: egy-egy állítás ezekre a három forgatókönyvre.

## Kritikus területek

A duplikációt az adószám nyolc számjegye és a számlaszám azonosítja, csak a szélső whitespace vágásával, a betűzés megőrzésével. A már nyilvántartott és az ugyanabban a szeletben már felvett számla nem kap új sort, és nem kér teljes bizonylatot. A következő szelet az előző végénél indul, a közös határ mindkét szeletben benne van; az Excel-beszúrás a már rögzített sort kihagyja. Az `ANNUL` nem készít sort. Az ugyanolyan hibabejegyzés másodszor nem íródik.

A bizonylatkapcsolat a sorok után dől el. A később álló eredeti nem kap hiányzó-eredeti bejegyzést. A `Kapcsolt bizonylatok` a meglévő sorrendet tartja, az új szám a végére kerül, ismétlés nélkül. Hiányzó eredetinél a módosító vagy sztornó sora elkészül, az `Eredeti számla` az eredeti szám, a bejegyzés megmarad. Ismételt szeletnél csak a hiányzó hivatkozás kerül fel, más cella nem.

Új sorban a `Számla megvan?`, a `Fizetve?`, az `Autó rendelés` és a `Megjegyzés` üres. Meglévő sorban csak a `Kapcsolt bizonylatok` cella cserélődik. Az új sor az utolsó kitöltött sor után kerül, a hézag megmarad. A feltöltés a letöltött másolat bedolgozott változatát küldi; a közben történt felhasználói írás a design ismert kockázata, külön foglalás nincs.

Részleges hibánál a már feltöltött szelet és a már kiírt bejegyzés megmarad. A hibás ciklus egy bejegyzést ír, és további szelet nem indul. Sikertelen teljes bizonylatnál feltöltés nincs, az `InsDate` nem lép. Az éven kívüli tétel és a hiányzó eredeti nem állítja le a szeletet. Üres vagy hibás kezdődátum, üres táblacím és hiányzó `InsDate` a NAV előtt megáll. A tartományhiba nem ad vissza részleges kivonatlistát. A teljes ablakos `QueryInvoiceDigest` és a `NavTimestamps.Slices` 1 ms-os léptetése változatlan.

Az `InsDate` csak az aktuális év fájljába kerül, a többi módosított fájl feltöltése után. Csak ennek a feltöltésnek a sikerével lép. Üres, sikeres szeletnél is előrelép. Az előző év cellája nem változik. A szeletvágás a futás elején készül, és a friss kurzor napjának elejére nem lép vissza.

## Lefedett működés

A futás a meglévő handlerből és a Development `--invoke` útjáról indul. Új időzítő, éles végpont és Slack-küldés nincs. Checkout a feldolgozás olvasásán és feltöltésén nincs. A `batchIndex` csak `MODIFY` kérésben megy ki. A `notification` tábla sora `Type`, `Message`, `Taxnumber`, `Partnername` és `Invoice`; külön időmező, küldési állapot, újrapróbálás és csatorna nincs. A verziókezelt `NotificationTableUri` üres. A kanonikus infrastruktúra-spec és a bicep a munkafában már a `notification` nevet használja; az archiválás nem futott.

## Final Assessment

No critical issues or warnings. 1 suggestion to consider. Ready for archive (with noted improvements).

Archiválás, főágra emelés és push nem indul. Az impl munkafa nincs a munkarend szerint lezárva.
