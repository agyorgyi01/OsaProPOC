# Proposal

## Why

A host ma egyetlen `appsettings.json` fájlt tölt, ezért a fejlesztői és a tesztkörnyezeti NAV-cím nem válik el az éles címtől. A környezet nevét sem a Function App saját változójából olvassa, így a tesztkörnyezet nem választható ki az automatizált tesztektől függetlenül.

## What Changes

- A környezet neve először az `AZURE_FUNCTIONS_ENVIRONMENT`, különben a `DOTNET_ENVIRONMENT` érték. A három név: `Development`, `Test` és `Production`. Ha egyik sincs megadva, a host `Production`. Az `ASPNETCORE_ENVIRONMENT` kiesik a kiválasztásból.
- A `dotnet test` nem választja a `Test` nevet, és nem tölti az `appsettings.Test.json` fájlt, kivéve ha maga a teszt állítja be a nevet.
- A későbbi forrás felülír. A sorrend: `appsettings.json`, majd a létező `appsettings.{Environment}.json`, majd a környezeti változók, helyben pedig a User Secrets mindhárom névnél. A hiányzó környezetfájl nem hiba.
- A név kis- és nagybetűtől függetlenül a három kanonikus név egyikére illeszkedik. A fájlnév mindig a kanonikus írásmód. Más megadott név a konfiguráció betöltése előtt megállítja a hostot, és nem esik vissza `Production`re.
- Közös, nem titkos érték az `appsettings.json` fájlban marad. Az egyetlen ismert környezetfüggő érték a `Nav:BaseUrl`: `Development` és `Test` esetén `https://api-test.onlineszamla.nav.gov.hu`. Az éles cím, `https://api.onlineszamla.nav.gov.hu`, az alapfájlban marad, ezért `appsettings.Production.json` nem készül.
- A meglévő környezetfájlok ugyanúgy a build és a publish kimenetbe kerülnek, mint az `appsettings.json`. A `local.settings.json` a publishből kimarad.
- Titkos érték nem kerül verziókezelt fájlba. A `Secrets` szakasz továbbra is csak a kulcsneveket nevezi meg.
- A `--invoke` viselkedése nem változik. Az infrastruktúra és a telepítés nem része ennek a change-nek. Egy későbbi teszt Function Appon az `AZURE_FUNCTIONS_ENVIRONMENT=Test` beállítás külön change.

## Capabilities

### New Capabilities

- Nincs.

### Modified Capabilities

- `application-configuration`: a környezet kiválasztása, a környezetfájl betöltése és a nem titkos felülírás. A titok továbbra is verziókezelt fájlon kívül marad.

## Impact

- Az `OsaPro.Common` konfigurációbetöltése és az `OsaPro.Worker` hostja, valamint a Worker `appsettings` fájljai és a hozzájuk tartozó tesztek.
- A NAV-lekérdezés, az Excel-kezelés, a naplózás és az infrastruktúra követelményei változatlanok. Az archivált `setup-application-foundation` change nem módosul.
- Azure-ban a titok továbbra is Function App Setting vagy környezeti változó. A telepítő nem másolja be a Nav- és Excel-beállításokat.

## Érinti-e kritikus komponenst?

Igen. A NAV-integráció kritikus terület: `Development` és `Test` környezetben a betöltött `Nav:BaseUrl` a tesztgyökér, az alapfájlban maradó éles gyökér helyett. Az Excel-nyilvántartás, a bizonylatkapcsolatok, valamint a határidők és értesítések szabálya nem változik.

## Assumptions from codebase

- Az `AddSources` ma az `appsettings.json` fájlt, a környezeti változókat és a User Secrets forrást tölti. Környezetfájlt nem (`src/OsaPro.Common/SecretConfiguration.cs`). A Worker a forrásokat előtte üríti (`src/OsaPro.Worker/WorkerComposition.cs`).
- A `Program.IsDevelopment` a `DOTNET_ENVIRONMENT`, majd az `ASPNETCORE_ENVIRONMENT` értéket olvassa, és csak a `Development` névre igaz. A host környezeti neve ebből `Development` vagy `Production`. Az `AZURE_FUNCTIONS_ENVIRONMENT` nincs olvasva (`src/OsaPro.Worker/Program.cs`).
- Az `appsettings.json` érvényes `Nav:BaseUrl` értéke `https://api.onlineszamla.nav.gov.hu`. A tesztcím kommentben van. Az `Excel:FolderPath` értéke `OsaPro.Test`. A `Secrets` kulcsok üresek.
- A `launchSettings.json` Project profilja a `DOTNET_ENVIRONMENT=Development` értéket adja. A `local.settings.json` csak a `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` értéket tartalmazza, és nem publikálódik.
