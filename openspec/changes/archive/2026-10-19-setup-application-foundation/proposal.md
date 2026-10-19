# Proposal

## Why

A későbbi számlakezelésnek előbb egy futtatható OsaPro-alap kell, konfigurációval és naplózással. Ezek nélkül a NAV-, Excel- és Slack-change-eknek nincs közös, titok nélküli és ellenőrizhető kiindulópontja.

## What Changes

- Az `OsaPro.Worker` .NET 10 isolated worker futtatóprojekt készül az OsaPro Function Apphoz. Ez az időzített folyamatok helye. Ebben a change-ben éles időzítő nem készül.
- A számlafeldolgozás és az értesítés-összeállítás két Function belépési pont az `OsaPro.Worker` projektben. Üzleti működést nem valósítanak meg. Élesben hívható végpont nem készül. Teszttel és helyi fejlesztői indítással meghívhatók, és a futást valamint a hibát a saját Function-azonosítójukkal naplózzák.
- A közös Slack-küldő külön Function és külön Function App marad. A futtatóprojektje `OsaPro.Slack`, és nem ebben a change-ben készül.
- Az `OsaPro.Common` osztálykönyvtár készül az egységes naplózásra, a titokkezelésre és az appsettings, valamint a környezeti paraméterek betöltésére. Az `OsaPro.Worker` ezt hivatkozza. Az `OsaPro.Slack` a 6. change-ben hivatkozza ugyanezt a könyvtárat.
- A titkos konfiguráció helyben .NET User Secretsből, Azure-ban Function App Settingsből vagy környezeti változóból töltődik. Titok nem kerül verziókezelt fájlba. A titkos kulcsokat a `Secrets` szakasz nevezi meg. Ebben a change-ben a szakasz üres lehet.
- A futások és a hibák naplózhatók. A naplók szétválaszthatók a számlafeldolgozás, az értesítés-összeállítás és a Slack-üzenetek küldése szerint. A titkos konfigurációs érték a naplóban csak `##KulcsNeve##` formában jelenik meg.
- A közös hibakezelés alapelv marad: az átmeneti kommunikációs hiba utáni újrapróbálás, a hibás tétel elkülönítése és a megszakítás utáni folytatás a későbbi change-ek feladata. Itt nem készül hozzá integrációs kód.

## Capabilities

### New Capabilities

- `application-configuration`: a titkos konfiguráció tárolása és betöltése forráskódon kívül.
- `operational-logging`: futások és hibák naplózása, a három Function szerint szétválaszthatóan.

### Modified Capabilities

- Nincs. Kanonikus specifikáció még nem létezik.

## Impact

- Új `OsaPro.Common` és `OsaPro.Worker` projekt, valamint a hozzájuk tartozó tesztprojektek.
- Nem jön létre `OsaPro.Slack` host, NAV-, Excel- vagy Slack-hívás.
- A két Function Appból álló határ nem változik.

## Érinti-e kritikus komponenst?

Nem. A NAV-integráció, az Excel-nyilvántartás, a bizonylatkapcsolatok, valamint a határidők és értesítések üzleti működése nem része ennek a change-nek.

## Assumptions from codebase

A repóban nincs alkalmazáskód. Ellenőrizve: nincs `*.cs`, `*.csproj` vagy `*.sln` fájl, ezért egyetlen viselkedést sem lehetett kódsorból levezetni. A terv a change dokumentumaiból és a projektkeret már elfogadott döntéseiből áll.
