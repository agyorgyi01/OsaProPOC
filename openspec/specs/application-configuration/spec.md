# application-configuration Specification

## Purpose

A titkos konfiguráció forráskódon kívül tárolható és onnan betölthető, hogy az OsaPro futtatója titok nélkül legyen verziókezelhető.

## Requirements

### Requirement: Titkos érték nem kerül verziókezelt fájlba
A rendszer MUST a titkos konfigurációs értéket verziókezelt fájlon kívül tartani.

#### Scenario: Titok nincs a forrásban
- **WHEN** egy konfigurációs érték titkos
- **THEN** az érték nem szerepel verziókezelt fájlban

### Requirement: Helyi titok betöltése
A helyben futó host MUST a titkos konfigurációs értékeket .NET User Secretsből betölteni, `Development`, `Test` és `Production` környezetben egyaránt.

#### Scenario: Helyi Production
- **WHEN** a host helyben `Production` környezetben indul
- **THEN** a titkos konfigurációs értékek a .NET User Secretsből töltődnek

### Requirement: Azure-beli titok betöltése
Az Azure-ban futó host MUST a titkos konfigurációs értékeket Function App Settingsből vagy környezeti változóból betölteni, és MUST NOT .NET User Secrets forrást betölteni.

#### Scenario: Azure indítás
- **WHEN** az OsaPro host Azure-ban indul
- **THEN** a titkos konfigurációs értékek Function App Settingsből vagy környezeti változóból töltődnek

### Requirement: Titkos kulcs a Secrets szakaszban
A rendszer MUST a `Secrets` konfigurációs szakasz kulcsait titkos konfigurációs kulcsnak tekinteni. A szakasz a kulcsneveket nevezi meg, az értékük forráson kívül marad.

#### Scenario: Üres szakasz
- **WHEN** a `Secrets` szakasz üres
- **THEN** a host titkos kulcs nélkül indul

#### Scenario: Megnevezett kulcs
- **WHEN** a `Secrets` szakasz tartalmazza az `Excel` kulcsnevet
- **THEN** az `Excel` titkos konfigurációs kulcs, és az értéke nincs verziókezelt fájlban

### Requirement: Környezetnév
A host MUST a nevet először az `AZURE_FUNCTIONS_ENVIRONMENT`, különben a `DOTNET_ENVIRONMENT` értékéből választani. Az elfogadott nevek a `Development`, a `Test` és a `Production`; az illesztés kis- és nagybetűtől független, a fájlnév kanonikus. Ha egyik sincs megadva, a környezet `Production`. Az `ASPNETCORE_ENVIRONMENT` nem számít. Ismeretlen név a konfiguráció betöltése előtt megállítja a hostot. Automatizált teszt csak akkor választ `Test` környezetet, ha a teszt beállítja a nevet.

#### Scenario: Function App változó
- **WHEN** az `AZURE_FUNCTIONS_ENVIRONMENT` értéke `Test` és a `DOTNET_ENVIRONMENT` értéke `Development`
- **THEN** a környezet `Test`

#### Scenario: Aspnetcore kimarad
- **WHEN** csak az `ASPNETCORE_ENVIRONMENT` értéke `Development`
- **THEN** a környezet `Production`

#### Scenario: Elgépelt név
- **WHEN** a környezetnév `Staging`
- **THEN** a host a konfiguráció betöltése előtt megáll

#### Scenario: Tesztfutás
- **WHEN** automatizált teszt fut anélkül, hogy a környezetnevet beállítaná
- **THEN** a `Test` környezet nem választódik

### Requirement: Konfiguráció sorrendje
A későbbi forrás MUST felülírni a korábbit. A sorrend az `appsettings.json`, a létező `appsettings.{Environment}.json`, a környezeti változók, helyben a User Secrets. A hiányzó környezetfájl MUST NOT hibát okozni.

#### Scenario: Helyi User Secrets felülír
- **WHEN** a host helyben indul, és a User Secrets ugyanazt a kulcsot adja, mint a környezeti változó
- **THEN** a User Secrets értéke érvényes
