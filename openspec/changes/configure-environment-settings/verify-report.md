# Verify: configure-environment-settings

2026-10-26. Branch: `impl/configure-environment-settings`. A terv és az implementáció a munkafában van, nincs commitolva.
Kritikus komponens: igen, a NAV-integráció. A `Development` és a `Test` fájl a teszt NAV-címet rögzíti. Az éles cím az alapfájlban marad. Push, spec-szinkron, archiválás és főágra emelés nem történt.

Using change: `configure-environment-settings`. Másik change: `/opsx-verify <név>`.

`openspec validate configure-environment-settings --type change --strict`: érvényes. A `dotnet test OsaPro.sln` ebben a menetben korábban sikeres volt: 20 common, 7 worker, 15 NAV, 28 Excel. A publish kimenetben megvan az `appsettings.json`, az `appsettings.Development.json` és az `appsettings.Test.json`. A `local.settings.json` és az `appsettings.Production.json` nincs benne.

## Summary

| Dimension | Status |
|---|---|
| Completeness | 7/7 tasks, 12 ADDED requirements. MODIFIED, REMOVED és RENAMED nincs. |
| Correctness | 12/12 requirements match, 23/23 scenarios covered |
| Coherence | Followed |

## CRITICAL

Nincs.

## WARNING

Nincs.

## SUGGESTION

1. Az `Aspnetcore kimarad` forgatókönyvhöz nincs olyan teszt, amely az `ASPNETCORE_ENVIRONMENT` változót beállítja. A `Program` csak a másik két változót olvassa (`src/OsaPro.Worker/Program.cs:6`).
2. A publish forgatókönyv nincs a tesztkészletben. A csproj a két környezetfájlt a build kimenetbe másolja, és a `local.settings.json` publish szabálya `Never` (`src/OsaPro.Worker/OsaPro.Worker.csproj:24`). A kézi publish ezt igazolta.
3. Az ismeretlen név tesztje a `WorkerStartup` döntését nézi, a `Program` belépését nem (`tests/OsaPro.Worker.Tests/WorkerTests.cs:137`). A belépés a döntés után tér vissza (`src/OsaPro.Worker/Program.cs:11`).

## Lefedett működés

A környezetnév tiszta függvény. Az `AZURE_FUNCTIONS_ENVIRONMENT` előrébb áll, mint a `DOTNET_ENVIRONMENT`. Üres érték hiányzik. A `test` írásmód `Test` lesz. A `Staging` érvénytelen, és nem esik vissza `Production`re (`src/OsaPro.Common/HostEnvironment.cs:13`).

A források sorrendje: alapfájl, opcionális környezetfájl, környezeti változó, majd User Secrets (`src/OsaPro.Common/SecretConfiguration.cs:16`). Az `AddSources` nem olvassa a folyamat környezetét. Az alapértelmezett név `Production`, ezért a `DOTNET_ENVIRONMENT=Test` mellett sem töltődik az `appsettings.Test.json`.

A host a feloldott nevet adja a konfigurációnak (`src/OsaPro.Worker/WorkerComposition.cs:22`). Helyben a `Development`, a `Test` és a `Production` is olvas User Secrets forrást. Kitöltött `WEBSITE_INSTANCE_ID` mellett a forrás kimarad (`src/OsaPro.Common/HostEnvironment.cs:35`). A `--invoke` továbbra is csak `Development` néven futtat handlert (`src/OsaPro.Worker/WorkerEntry.cs:40`).

A két környezetfájl csak a `https://api-test.onlineszamla.nav.gov.hu` címet tartalmazza. Az alapfájl `Nav:BaseUrl` értéke `https://api.onlineszamla.nav.gov.hu`. `appsettings.Production.json` nincs. Az infrastruktúra és az archivált `setup-application-foundation` change nem módosult.

## Final Assessment

No critical issues or warnings. 3 suggestions to consider. Ready for archive (with noted improvements).

Emberi code review jóváhagyás nincs ebben a riportban. Archiválás, spec-szinkron és merge nem indul.
