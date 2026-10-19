# Tasks

## 1. Solution és projektek

- [x] 1.1 Hozd létre az `OsaPro.sln` solutiont, valamint a `src/OsaPro.Common`, a `src/OsaPro.Worker`, a `tests/OsaPro.Common.Tests` és a `tests/OsaPro.Worker.Tests` projekteket `net10.0` célkerettel. A tesztprojektek xUnitot használjanak. Az `OsaPro.Worker` hivatkozza az `OsaPro.Common` projektet, a tesztprojektek a saját megvalósításukat. Ne hozz létre `OsaPro.Slack` projektet. Ellenőrizd: `dotnet build OsaPro.sln` sikeres, a négy csproj létezik, `OsaPro.Slack.csproj` nincs.
- [x] 1.2 Add hozzá az `OsaPro.Worker` verziókezelt `appsettings.json` fájlját üres `Secrets` szakasszal, és a `local.settings.json` fájlt csak a `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` beállítással. A Worker csproj kapjon User Secrets azonosítót. Ellenőrizd: egyik fájlban sincs titkos érték, a `Secrets` szakasz üres objektum.

## 2. Function-azonosítók

- [x] 2.1 Az `OsaPro.Common` rögzítse a `szamlafeldolgozas`, az `ertesites-osszeallitas` és a `slack-uzenetek-kuldese` azonosítókat. Ellenőrizd: az `OsaPro.Common.Tests` tesztje a három értéket különbözőnek látja, és a `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 3. Konfiguráció

- [x] 3.1 Az `OsaPro.Common` töltse be a host `appsettings.json` fájlját és a környezeti változókat, Development környezetben pedig a User Secrets forrást is. A `Secrets` szakasz kulcsai legyenek a titkos kulcsok. Ellenőrizd: a könyvtár tesztje üres szakasszal hiba nélkül épít konfigurációt, a memóriában adott `Secrets:ExcelFile` kulcsot titkosnak jelöli, és a verziókezelt `appsettings.json` nem tartalmazza a `valami.xls` értéket. A `dotnet test tests/OsaPro.Common.Tests` sikeres.
- [x] 3.2 A `Secrets__ExcelFile` környezeti változó a `Secrets:ExcelFile` kulcsra képződjön. Ellenőrizd: a könyvtár tesztje ebből a változóból olvassa az értéket, és az érték nincs verziókezelt fájlban. A `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 4. Naplómaszkolás

- [x] 4.1 Az `OsaPro.Common` a nem üres `Secrets` értékeket cserélje a szakaszhoz képest vett kulcsnév `##KulcsNeve##` formájára a naplóüzenetben és a strukturált szöveges mezőkben. A csere pontos és kis-nagybetű érzékeny legyen. Üres érték ne kerüljön a cseretáblába. Két kulcs azonos értékénél a nyers érték ne maradjon, a token a konfiguráció sorrendjében első kulcs neve legyen. Ellenőrizd: `ExcelFile=valami.xls` mellett a `nem nyitható meg a valami.xls fájl` szövegből `nem nyitható meg a ##ExcelFile## fájl` lesz, a `valami.xls` strukturált mezőben sem szerepel, és két azonos értékű kulcsnál a nyers érték helyett az első kulcs tokenje jelenik meg. A `dotnet test tests/OsaPro.Common.Tests` sikeres.

## 5. Worker host

- [x] 5.1 Az `OsaPro.Worker` isolated worker host kösse be az `OsaPro.Common` konfigurációját és maszkolóját. A számlafeldolgozás és az értesítés-összeállítás handlerjén ne legyen `TimerTrigger` vagy `HttpTrigger`. Ellenőrizd: az `OsaPro.Worker.Tests` reflectionnel igazolja a triggerek hiányát, és a host üres `Secrets` szakasszal elindul. A `dotnet test tests/OsaPro.Worker.Tests` sikeres.
- [x] 5.2 A handlerek a futás kezdetét és a kivételt a saját `FunctionId` mezőjükkel naplózzák, majd a kivételt továbbdobják. Az éles törzs üres legyen. A Worker adjon `InternalsVisibleTo` jogot a tesztprojektnek. Tesztből és Development környezetben a `--invoke <function-id>` hívás indítsa a handlert; más környezet és ismeretlen azonosító ne indítson handlert, az ismeretlen azonosító nem nulla kilépési kóddal érjen véget. Ellenőrizd: a sikeres és a hibás teszt a saját azonosítót írja, a másik kettőt nem, és Developmentön kívül, valamint ismeretlen azonosítónál nem fut handler. A `dotnet test tests/OsaPro.Worker.Tests` sikeres.
- [x] 5.3 A Development indítás a User Secrets forrásból töltse a `Secrets` értékét. Ellenőrizd: az `OsaPro.Worker.Tests` a User Secrets API-val eltárol egy értéket, a host ezt beolvassa, és az érték nincs a verziókezelt `appsettings.json` fájlban. A `dotnet test tests/OsaPro.Worker.Tests` sikeres.

## 6. Együttes ellenőrzés

- [x] 6.1 Futtasd a solution tesztjeit, és ellenőrizd a change határát. Ellenőrizd: a `dotnet test OsaPro.sln` sikeres, nincs `OsaPro.Slack.csproj`, és a forrásban nincs NAV-, Excel- vagy Slack-hívás, sem újrapróbálási vagy folytatási kód.

## Workflow follow-up

- A change implementációja csak a grill-me lezárása és az explicit emberi tervjóváhagyás után kezdhető.
- Az implementáció és a kódellenőrzés lezárása után a change archiválható. A delta specek szinkronja külön explicit jóváhagyást igényel.
