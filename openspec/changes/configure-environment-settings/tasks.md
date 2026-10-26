# Tasks

## 1. Környezetnév

- [x] 1.1 Az `OsaPro.Common` adjon tiszta függvényt, amely az `AZURE_FUNCTIONS_ENVIRONMENT` és a `DOTNET_ENVIRONMENT` szöveget kapja. Üres és whitespace érték hiányzik. Az első kitöltött érték trim után, kis- és nagybetűtől függetlenül `Development`, `Test` vagy `Production`, és a visszaadott név kanonikus. Mindkettő hiányánál az eredmény `Production`. Más szöveg érvénytelen. Az `ASPNETCORE_ENVIRONMENT` nem paraméter. Ellenőrizd: az `OsaPro.Common.Tests` lefedi a Function App elsőbbséget, a dotnet változót, a hiányzó változókat, az `test` írásmódot, a whitespace értéket és a `Staging` nevet. A `dotnet test tests/OsaPro.Common.Tests` sikeres.
- [x] 1.2 Külön függvény döntse el a User Secrets betöltését a `WEBSITE_INSTANCE_ID` szövegéből. Whitespace-től eltérő érték Azure, és a User Secrets kimarad. Üres vagy whitespace érték helyben futást jelent. Ellenőrizd: a teszt a két szöveget paraméterként adja, a folyamat környezeti változóját nem írja, és a `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 2. Konfiguráció betöltése

- [x] 2.1 Az `AddSources` a kanonikus környezetnevet paraméterként kapja, alapértelmezése `Production`. A források sorrendje: `appsettings.json`, opcionális `appsettings.{név}.json` `reloadOnChange: false` beállítással, környezeti változók, majd User Secrets, ha a hívó kéri és az azonosító ki van töltve. A metódus nem olvassa a folyamat környezeti változóit. A hiányzó környezetfájl nem hiba. Ellenőrizd: a környezetfájl felülírja az alapfájlt, a környezeti változó a JSON-t, a User Secrets a környezeti változót, hiányzó fájlnál a hostérték megépül, és a folyamat `DOTNET_ENVIRONMENT=Test` értéke mellett az alapértelmezett hívás nem tölti az `appsettings.Test.json` fájlt. A `dotnet test tests/OsaPro.Common.Tests` sikeres.
- [x] 2.2 Készüljön `appsettings.Development.json` és `appsettings.Test.json`. Mindkettő csak a `Nav:BaseUrl` értékét állítsa `https://api-test.onlineszamla.nav.gov.hu` címre. Az `appsettings.json` `Nav:BaseUrl` értéke maradjon `https://api.onlineszamla.nav.gov.hu`, a kikommentezett tesztcím kerüljön ki. `appsettings.Production.json` ne készüljön. Az Excel-szakasz és az üres `Secrets` kulcsok az alapfájlban maradjanak. A két új fájl a Worker csprojban az `appsettings.json` mellé kerüljön, `CopyToOutputDirectory` értéke `PreserveNewest` legyen, és ne kapjon `CopyToPublishDirectory` `Never` értéket. A `local.settings.json` publish szabálya maradjon. Ellenőrizd: a verziókezelt fájlokban nincs titkos érték, a tesztcím csak a két környezetfájlban van, a build kimenet tartalmazza mindkettőt, a publish kimenet is tartalmazza őket, a `local.settings.json` viszont nincs a publish kimenetben.

## 3. Worker host

- [x] 3.1 A `Program` a két változót a tiszta függvénynek adja, és az `ASPNETCORE_ENVIRONMENT` értéket nem olvassa. Érvénytelen névnél magyar üzenetet ír, `WorkerEntry.Failure` kóddal kilép, és nem hívja a `WorkerComposition.Build` és a `WorkerEntry` kódot. Érvényes névnél a host `EnvironmentName` értéke a kanonikus név, a `Test` nevet is beleértve. Az `includeUserSecrets` az Azure-döntés hamis ágán hamis. A `--invoke` továbbra is csak `Development` néven fut. Ellenőrizd: az `OsaPro.Worker.Tests` érvénytelen névnél nem épít konfigurációt és nem nulla kódot lát, `Test` néven a host neve `Test` és a betöltött `Nav:BaseUrl` a tesztcím, `Production` néven és hiányzó környezetfájllal az éles cím marad, a `--invoke` Developmentön kívül nem futtat handlert. A `dotnet test tests/OsaPro.Worker.Tests` sikeres.
- [x] 3.2 A helyi host a `Development`, a `Test` és a `Production` név mellett is a User Secrets forrásból olvasson, a környezeti változók után. Ellenőrizd: az `OsaPro.Worker.Tests` mindhárom néven beolvassa a teszt titokértéket, és az érték nincs verziókezelt fájlban. A `dotnet test tests/OsaPro.Worker.Tests` sikeres.

## 4. Együttes ellenőrzés

- [x] 4.1 Futtasd a solution tesztjeit, és ellenőrizd a change határát. Ellenőrizd: a `dotnet test OsaPro.sln` sikeres, nincs `appsettings.Production.json`, az infrastruktúra és az archivált `setup-application-foundation` change nem módosult, és a `WorkerEntry.Plan` `--invoke` ága csak `Development` néven indít handlert.

## Workflow follow-up

- Az implementáció csak a grill-me lezárása és az explicit emberi tervjóváhagyás után kezdhető.
- Archiválás és delta-spec szinkron csak külön jóváhagyással.
