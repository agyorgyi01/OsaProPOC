# Design

## Context

A repóban nincs alkalmazáskód. A motiváció: `proposal.md`. A viselkedés: `specs/application-configuration/spec.md` és `specs/operational-logging/spec.md`.

Két Function App marad. Ebben a change-ben az `OsaPro.Common` könyvtár és az `OsaPro.Worker` host készül. Az `OsaPro.Slack` host nem készül. A két belépési pontnak nincs éles időzítője és nincs élesben hívható végpontja.

## Goals / Non-Goals

**Goals:**

- A közös naplózás, a titokkezelés és a paraméterbetöltés az `OsaPro.Common` könyvtárban van, amelyet az `OsaPro.Worker` hivatkozik.
- A host teszttel és helyi fejlesztői indítással bizonyíthatóan elindul, akkor is, ha a `Secrets` szakasz üres.
- A két belépési pont futása és hibája a saját Function-azonosítójával naplózott.
- A három Function-azonosító a közös könyvtárban rögzített és egymástól különböző. Slack-küldő futás ebben a change-ben nem indul.
- A `Secrets` szakasz kulcsainak feloldott értéke a worker naplóiban `##KulcsNeve##` formára cserélődik.

**Non-Goals:**

- Azure-erőforrás, közzététel, éles trigger, NAV-, Excel- vagy Slack-hívás.
- Az `OsaPro.Slack` futtatóprojektje és annak projekthivatkozása. Ezt a 6. change adja a már létező `OsaPro.Common` könyvtárhoz.
- Újrapróbálás, hibás tétel elkülönítése, megszakítás utáni folytatás.
- A Functions host platformnaplóinak maszkolása.

## Decisions

### 1. Projektnév és felosztás

A projektnév `OsaPro.<szerep>`. A tesztprojekt neve `OsaPro.<szerep>.Tests`. További projekt ugyanezt a formát követi.

| Projekt | Szerep | Ebben a change-ben |
|---|---|---|
| `src/OsaPro.Common/OsaPro.Common.csproj` | Közös osztálykönyvtár: naplózás, titokkezelés, appsettings és környezeti paraméterek betöltése. | Készül. |
| `src/OsaPro.Worker/OsaPro.Worker.csproj` | Az időzített folyamatok isolated worker hostja. Hivatkozza az `OsaPro.Common` projektet. Itt vannak a két belépési pont handlerjei. | Készül, éles időzítő nélkül. |
| `src/OsaPro.Slack/OsaPro.Slack.csproj` | A Slack-kommunikáció Function App hostja. | Nem készül. A 6. change hozza létre, és hivatkozza az `OsaPro.Common` projektet. |
| `tests/OsaPro.Common.Tests/OsaPro.Common.Tests.csproj` | A könyvtár tesztjei: üres `Secrets` szakasz, kulcsnév, maszkolás, a három azonosító. | Készül. |
| `tests/OsaPro.Worker.Tests/OsaPro.Worker.Tests.csproj` | A host tesztjei: meghívás, futás- és hibanapló, trigger hiánya. | Készül. |

Mind `net10.0`, a repó gyökerében lévő `OsaPro.sln` solutionben. A tesztprojektek xUnitot használnak. Az `OsaPro.Worker` saját `appsettings.json` és `local.settings.json` fájlja a hostprojektben marad. Az `OsaPro.Common` a betöltés szabályát adja, nem egyetlen alkalmazás beállításait.

Alternatíva: a közös kód az `OsaPro.Worker` hostban, másolat a 6. change-ben. Elvetve, mert a naplózás, a titokkezelés és a paraméterbetöltés mindkét Function App szabálya.

### 2. A belépési pontnak nincs Azure-triggerje

A számlafeldolgozás és az értesítés-összeállítás egy-egy handler az `OsaPro.Worker` hostban. A folyamat belépése `FunctionsApplication.CreateBuilder`. Nincs `TimerTrigger` és nincs `HttpTrigger`. Az Azure Functions host indítja a folyamatot, és a gRPC csatornát ő adja. A hostteszt a Functions-worker szolgáltatást és az üres `Secrets` szakaszt építi fel, a csatornát nem nyitja meg. Az Azure által indított host trigger nélkül vár, és egyik belépési pont sem hívható éles végponton vagy időzítőn. Az időzítés későbbi change, a projektnév ettől már az időzített folyamatok helyét jelöli.

A teszt a handlert a host naplózásával és konfigurációjával közvetlenül hívja. Helyi fejlesztői indításkor a `dotnet run --project src/OsaPro.Worker -- --invoke <function-id>` hívja ugyanazt, és csak `Development` környezetben. Az argumentum hiánya vagy más környezet csak a hostot indítja, handlert nem futtat. Ismeretlen function-id nem indít handlert, és a folyamat nem nulla kilépési kóddal ér véget.

Alternatíva: HTTP-trigger csak helyi használatra. Elvetve, mert a telepített alkalmazásban élesben hívható végpont lenne. Alternatíva: időzítő üres menetrenddel. Elvetve, mert éles időzítőt hozna létre.

A handler a közös naplózással rögzíti a futás kezdetét. A kivételt a saját Function-azonosítójával naplózza, majd továbbdobja. Az éles handler törzse üres. A hibás út tesztje `InternalsVisibleTo` révén adhat hibát dobó műveletet; ez nem végpont.

### 3. A három Function-azonosító az OsaPro.Common könyvtárban van

| Function | Azonosító |
|---|---|
| Számlafeldolgozás | `szamlafeldolgozas` |
| Értesítések összeállítása | `ertesites-osszeallitas` |
| Slack-üzenetek küldése | `slack-uzenetek-kuldese` |

A naplóbejegyzés strukturált `FunctionId` mezője ezt az értéket viseli. A Slack-azonosítót ez a change nem írja futó handlerből. A 6. change az `OsaPro.Common` azonosítóját használja.

Alternatíva: megjelenítendő magyar név a naplóban. Elvetve, mert a szűréshez stabil, ékezet nélküli érték kell. Alternatíva: az azonosító csak a Slack-hostban szülessen meg. Elvetve, mert a spec már ebben a change-ben három, egymástól elkülöníthető értéket követel.

### 4. A paraméterbetöltés és a `Secrets` szakasz az OsaPro.Common könyvtárban van

A könyvtár állítja össze a konfigurációt: a host verziókezelt `appsettings.json` fájlja, a környezeti változók, helyben pedig a User Secrets. A Worker ezt az `AddSources` hívással köti be, a környezet nevétől függetlenül. Azure-ban a Function App Setting vagy a környezeti változó neve `Secrets__KulcsNeve`; a .NET ezt a `Secrets:KulcsNeve` kulcsra képezi. Erre a leképezésre a könyvtár tesztje fut; külön Azure-host teszt nem készül.

A host `appsettings.json` fájlja tartalmazza a `Secrets` szakaszt, kulcsok nélkül: `{ "Secrets": {} }`. Új kulcsnév ide kerülhet, érték nélkül. A `local.settings.json` csak a worker nem titkos hostbeállítását tartalmazza (`FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`). Titkos érték nem kerül bele.

Üres szakasznál a host elindul, és a maszkolótábla üres. A könyvtár tesztje a betöltést User Secrets és környezeti változó nélkül, memóriában adott konfigurációval ellenőrzi. A host tesztje azt ellenőrzi, hogy a helyi indítás Developmenten kívül is a User Secrets forrásból olvas.

Alternatíva: minden konfigurációs érték titok. Elvetve, mert a spec a `Secrets` szakaszra korlátozza a jelölést. Alternatíva: a titok értéke a verziókezelt fájlban placeholder. Elvetve, mert a futó konfiguráció a feloldott értéket adja, és a napló azt tartalmazza.

### 5. A maszkoló az OsaPro.Common könyvtárban a feloldott értéket cseréli

A könyvtár a handler futása előtt felépíti a cseretáblát: minden nem üres `Secrets` értékhez a szakaszhoz képest vett kulcsnév tartozik. Az `ExcelFile` értéke `valami.xls` esetén a tábla `valami.xls` → `##ExcelFile##`.

A host a könyvtár maszkolóját a worker `ILogger` útjára teszi. A maszkoló a kész üzenetben, a strukturált szöveges mezőkben és a kivétel `ToString()` szövegében minden találatot lecserél, majd továbbadja a naplót. A csere pontos, kis- és nagybetűre érzékeny. Üres érték nincs a táblában. Két kulcs azonos értékénél a nyers érték nem maradhat; a token a konfiguráció sorrendjében első kulcs neve.

Alternatíva: csak a hívás helyén kézzel írt `##KulcsNeve##`. Elvetve, mert egy mondatba ágyazott feloldott értéket nem cserélne le. Alternatíva: a .NET beépített redactionje. Elvetve, mert nem köti a konfigurációs kulcs nevét a feloldott értékhez. Alternatíva: a maszkoló csak az `OsaPro.Worker` hostban. Elvetve, mert az `OsaPro.Slack` hostnak ugyanazt a cserét kell használnia.

## Risks / Trade-offs

- [Rövid vagy hétköznapi titokérték] → minden nem üres titokértéket cserélünk, hossz-küszöb nélkül, mert a küszöb a spec szerinti kötelező cserét hagyná ki. A kulcs értékének egyedinek kell lennie.
- [Azonos érték két kulcsnál] → a nyers érték eltűnik, a token az első kulcs neve. A spec egykulcsos esetét ez nem változtatja.
- [Platformnapló a workeren kívül] → a maszkoló csak a worker `ILogger` útját fedi. Ez a change nem ír titkot a host platformnaplóiba.
- [Az `OsaPro.Common` könyvtárat egyelőre csak az `OsaPro.Worker` hivatkozza] → az `OsaPro.Slack` a 6. change-ben kapja meg ugyanazt a projekthivatkozást. Addig a közös szabály a könyvtárban van, második host nélkül.
- [Élesben trigger nélküli host] → a host elindul, és nem futtatja a handlereket. Az ütemezés későbbi change.

## Migration Plan

Nincs meglévő alkalmazás vagy adat. A change az `OsaPro.Common` és az `OsaPro.Worker` projektet, valamint a két tesztprojektet adja. Azure-erőforrás és közzététel nem készül. Visszavonás: az új projektek törlése.
