# Verify: compose-notification-content

2026-10-28. Branch: `verify/compose-notification-content`, a lezárt `impl/compose-notification-content` commitjából: `95cb570`. A munkapéldányban a `Fizetve?` szabály a commit után változott: a mező a szélső whitespace nélkül, nagybetűvel `FIZETVE`.

Kritikus komponens: igen. Excel-olvasás létrehozás nélkül, bizonylatkapcsolat a `STORNO:` előtaggal, határidő és a `notification` tábla tartalma. NAV-lekérdezés és Slack-küldés nem készül.

`openspec validate --all --strict`: 8 elem, 0 hiba. `dotnet test OsaPro.sln`: 94 teszt, 0 hiba (Common 20, Excel 31, Worker 27, NAV 16).

## Summary

| Dimension | Status |
|---|---|
| Completeness | 10/10 tasks, 25 ADDED és 3 MODIFIED requirement. REMOVED és RENAMED nincs. |
| Correctness | 28/28 requirements match, 64/64 scenarios covered |
| Coherence | Followed |

## CRITICAL

Nincs.

## WARNING

Nincs.

## SUGGESTION

Nincs.

## Kritikus területek

Az összeállítás csak a meglevő `SZÁMLÁK{év}.xlsx` fájlt tölti le. Hiányzó fájl és sikertelen letöltés külön eredmény. Fájl és lap nem készül. A tárgyévi előértesítés a tárgyévi fájlból jön; üres vagy hiányzó érték `7,5,1`.

Új sztornó a kapcsolat végére `STORNO:{számlaszám}` alakban kerül. Új módosító előtag nélkül. A már szereplő szám, előtaggal vagy anélkül, nem íródik át. A `STORNO:` lánc nem kap fizetési sort. Az előtag nélküli lista nem lánc. A láncon kívüli módosító a saját határidejével kaphat fizetési sort.

Fizetési sor csak pontos `Átutalás` esetén készül, egy számlának egy magyar napon egy sora van. A következő esedékes napon új sor készül. Szombaton és vasárnap fizetési sor nincs. A hétvégére eső határidő a következő hétfőn jelenik meg, a szöveg dátuma a tényleges határidő. A `Fizetve?` a szélső whitespace nélkül, nagybetűvel `FIZETVE` esetén kiegyenlített. Más nem üres érték egyszeri hibasor; sztornóláncban fizetési sor nélkül.

## Külön vizsgált

A fizetési határidő a `Fiz. határidő` dátuma, különben a `Számla kelte`. Egyik sem dátum: nincs fizetési sor. A szöveg dátuma a tényleges határidő `yyyy.MM.dd` alakban. Szombat és vasárnap nem kap fizetési sort. A hétvégére eső határidő és előértesítés a következő hétfőre kerül. A határidő napjától minden későbbi hétköznap új sor készül, amíg a számla a szélső whitespace nélkül, nagybetűvel nem `FIZETVE`. A következő nap akkor is új sort kap, ha az előző sor `NotificationTimestamp` értéke üres. A futás ezt a mezőt nem olvassa. Kód: `src/OsaPro.Worker/NoticeRules.cs:60-87` és `156-177`. Teszt: `tests/OsaPro.Worker.Tests/NotificationCompositionTests.cs:81-143`.

A ` fizetve ` érték nem ad sort. Az `igen` átutalásnál hibasort és fizetési sort is ad. A hibaszöveg a cella eredeti értékét tartalmazza. Kód: `src/OsaPro.Worker/NoticeRules.cs:60-70`. Teszt: `tests/OsaPro.Worker.Tests/NotificationCompositionTests.cs:54-73`.

Sztornólánc az a sor, amelynek `Kapcsolt bizonylatok` listájában `STORNO:` elem van, a listában szereplő számok, és az azonos adószámú sor, amelynek `Eredeti számla` értéke az eredeti száma. Előtag nélküli lista nem lánc. A lánc nem kap fizetési sort. Hibás `Fizetve?` a láncban is hibasort kap. Más adószámú, ugyanarra az eredetire mutató sor fizetési sort kaphat. Új sztornó a kapcsolat végére `STORNO:{számlaszám}` alakban kerül, a már szereplő szám nem íródik át. Kód: `src/OsaPro.Worker/NoticeRules.cs:124-154`, `src/OsaPro.Worker/ProcessingRules.cs:300-315`. Teszt: `tests/OsaPro.Worker.Tests/NotificationCompositionTests.cs:147-178`, `tests/OsaPro.Worker.Tests/InvoiceProcessingTests.cs:134` és `163`.

Az összesítő Slack-szövege és az ütemezés nincs ebben a change-ben. A táblában a későbbi összesítő három része megvan: `Payment`, `Document` és `Error`. A `Document` sor egyszeri, adószám és saját számlaszám a kulcs, új `MODIFY` és `STORNO` létrehozásakor, a feltöltés előtt készül. A `CREATE` nem kap sort. A hibasorok kulcsa stabil, az `Add` nem írja felül őket. A fizetési sor naponta egy, a következő esedékes napon új. A `NotificationTimestamp` nincs a kiírt mezők között. Kód: `src/OsaPro.Worker/InvoiceProcessing.cs:219-227`, `src/OsaPro.Worker/NotificationRecords.cs:165-173`.

## Lefedett működés

A `Document` sort a számlafeldolgozás írja új `MODIFY` és `STORNO` sor létrehozásakor, a feltöltés előtt. A `CREATE` nem kap sort. Már tárolt bizonylat nem kap utólagos sort. Az összeállítás `Payment` és `Error` sort ír, `Document` sort nem. Az `Add` meglévő kulcsnál nem ír felül. A kiírt mezők a `Type`, a `Message`, a `Taxnumber`, a `Partnername` és az `Invoice`. A `NotificationTimestamp` üres marad: ezt a futás nem olvassa és nem írja.

Az `ertesites-osszeallitas` Development `--invoke` úton indul. Új időzítő és éles végpont nincs. NAV-hívás és Slack-küldés nincs. Hibás kezdődátum, hiányzó tárgyévi fájl és olvasási hiba egy `Error` sor után megáll. Az indulás évében a hiányzó előző év nem hiba. Későbbi évben megáll. A számlafeldolgozás meglévő `out-of-year` sora nem íródik át.

A hétköznapi Slack-összesítő, a háromrészes üzenet és a `NotificationTimestamp` kitöltése a `integrate-slack-sender` és a `schedule-and-orchestrate` change-ben marad.

## Final Assessment

All checks passed. Ready for archive.

Archiválás, főágra emelés és push nem indul. A code review emberi jóváhagyása még nincs meg.
