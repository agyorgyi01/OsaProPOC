# Design

## Context

A motiváció a `proposal.md` Why szakaszában van. A követelmények a `specs/application-configuration/spec.md` delta. A betöltő ma az `appsettings.json` fájlt, a környezeti változókat és a User Secrets forrást teszi a konfigurációba, környezetfájl nélkül (`src/OsaPro.Common/SecretConfiguration.cs`). A Worker a forrásokat előtte üríti, és a host nevét egy `Development` vagy `Production` logikai értékből állítja (`src/OsaPro.Worker/WorkerComposition.cs`). A `Program.IsDevelopment` a `DOTNET_ENVIRONMENT`, majd az `ASPNETCORE_ENVIRONMENT` értéket olvassa (`src/OsaPro.Worker/Program.cs`).

## Goals / Non-Goals

**Goals:**

- A kanonikus környezetnév egy tiszta függvény legyen, a Worker pedig ezt a nevet adja tovább a betöltőnek és a hostnak.
- Az ismeretlen név a konfiguráció összeállítása előtt, nem nulla kilépési kóddal állítsa meg a folyamatot.
- A környezetfájl opcionális JSON-forrás legyen, kanonikus fájlnévvel. Verziókezelt környezetfájl nem készül.

**Non-Goals:**

- A `WorkerEntry.Plan` és a `--invoke` ág nem változik.
- A `local.settings.json` nem lesz konfigurációs forrás, és továbbra sem publikálódik.
- Az infrastruktúra, a Function App beállításai és az archivált foundation change nem változik.

## Decisions

### 1. A környezetnév az `OsaPro.Common` tiszta függvénye

A függvény két szöveget kap: az `AZURE_FUNCTIONS_ENVIRONMENT` és a `DOTNET_ENVIRONMENT` értékét. Üres vagy whitespace érték hiányzónak számít. Az első kitöltött érték trim után, kis- és nagybetűtől függetlenül a `Development`, a `Test` vagy a `Production` névre illeszkedik, és a visszaadott név mindig a kanonikus írásmód. Ha mindkettő hiányzik, az eredmény `Production`. Más szöveg érvénytelen.

A `Program` olvassa a két változót, és az `ASPNETCORE_ENVIRONMENT` értéket nem adja át. Érvénytelen névnél magyar üzenetet ír, és `WorkerEntry.Failure` kilépési kóddal tér vissza. A `WorkerComposition.Build` és a `WorkerEntry` ekkor nem hívódik.

A `--invoke` továbbra is csak akkor fut, ha a kanonikus név `Development`. A host `EnvironmentName` értéke a kanonikus név, a `Test` nevet is beleértve.

Alternatíva: a .NET host alapértelmezett lánca, benne az `ASPNETCORE_ENVIRONMENT`. Elvetve, mert a spec ezt a változót kizárja, és a mai kód sem a keret alapértelmezett forráslistáját használja. Alternatíva: ismeretlen név helyett `Production`. Elvetve, mert egy elírás nem eshet vissza `Production`re.

### 2. Az `AddSources` a nevet paraméterként kapja

A sorrend: az `appsettings.json`, majd az `appsettings.{kanonikus név}.json` `optional: true` és `reloadOnChange: false` beállítással, majd a környezeti változók, végül a User Secrets, ha a hívó kéri és a titokazonosító ki van töltve. A metódus nem olvassa a folyamat környezeti változóit. A hiányzó környezetfájl így nem hiba, és a `dotnet test` nem választ `Test` környezetet, amíg a teszt maga nem adja át ezt a nevet.

A meglévő hívók új paramétere alapértelmezésben `Production`. A `WorkerComposition` a már feloldott nevet adja át. A Worker-teszt a `Development`, a `Test` és a `Production` névvel is ellenőrzi a User Secrets forrást.

Alternatíva: az `AddSources` maga olvassa a folyamat változóit. Elvetve, mert a NAV-teszt a verziókezelt Worker-könyvtárból töltene, és egy fejlesztői shell `DOTNET_ENVIRONMENT` értéke észrevétlenül `Test` fájlt töltene.

### 3. Az Azure-t a `WEBSITE_INSTANCE_ID` választja el

Ha a `WEBSITE_INSTANCE_ID` whitespace-től eltérő, a host Azure-ban fut, és a `Program` az `includeUserSecrets` értéket hamisnak adja. Helyben a változó üres, ezért mindhárom kanonikus név mellett betöltődik a User Secrets, a környezeti változók után. A döntés külön függvény, hogy a tesztnek ne kelljen a folyamat változóját átírnia.

Alternatíva: `Production` néven nincs User Secrets. Elvetve, mert a spec a helyi `Production` indításnál is kéri. Alternatíva: az `AZURE_FUNCTIONS_ENVIRONMENT` kitöltöttsége jelenti az Azure-t. Elvetve, mert ugyanezt a változót helyben is a `Test` név választására használjuk.

### 4. Nincs paraméterhez kötött környezetfájl

A `Nav:BaseUrl` ugyanolyan appsettings érték, mint a többi. A change nem ad hozzá `appsettings.Development.json`, `appsettings.Test.json` vagy `appsettings.Production.json` fájlt. Minden környezet az `appsettings.json` értékét használja, amíg egy későbbi forrás felül nem írja. Az opcionális környezetfájl mechanizmus megmarad, verziókezelt példánya nincs. Az Excel-szakasz és a `Secrets` kulcsok az alapfájlban maradnak. A `local.settings.json` másolási szabálya változatlan.

Alternatíva: `Development` és `Test` fájl, amely csak a `Nav:BaseUrl` értéket írja felül. Elvetve, mert ez a kulcs nem külön szabály.

## Risks / Trade-offs

- [Nincs verziókezelt környezetfájl] → minden kulcs az alapfájlból jön, amíg környezeti változó vagy User Secrets felül nem írja.
- [A meglévő dev Function Appon nincs `AZURE_FUNCTIONS_ENVIRONMENT`] → a publikált host `Production` marad, és az alapfájlt tölti. Ezt a változót ez a change nem állítja be.
- [Linuxon a fájlnév kis- és nagybetűre érzékeny] → a betöltő csak a kanonikus nevet illeszti a fájlnévbe.
- [Helyben beállított `WEBSITE_INSTANCE_ID`] → a host nem tölti a User Secrets forrást. A változót az Azure adja; a teszt a döntési függvényt hívja, nem a folyamat változóját írja.

## Migration Plan

Nincs adatátalakítás és nincs Azure-beállítás. A régi hívók az alapértelmezett `Production` névvel az alapfájlt töltik, amíg a Worker a feloldott nevet át nem adja. Visszavonás: a change kódja kikerül. Titok nem forog, és a telepítő nem fut.
