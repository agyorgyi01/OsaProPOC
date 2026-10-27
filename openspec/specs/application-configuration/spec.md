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

### Requirement: Környezetnév kiválasztása
A host MUST a környezetnevet először az `AZURE_FUNCTIONS_ENVIRONMENT`, különben a `DOTNET_ENVIRONMENT` értékéből olvasni. A kanonikus nevek a `Development`, a `Test` és a `Production`. Ha egyik változó sincs megadva, a környezet MUST `Production` lenni. Az `ASPNETCORE_ENVIRONMENT` MUST NOT részt venni a kiválasztásban.

#### Scenario: Function App változó
- **WHEN** az `AZURE_FUNCTIONS_ENVIRONMENT` értéke `Test` és a `DOTNET_ENVIRONMENT` értéke `Development`
- **THEN** a környezet `Test`

#### Scenario: Dotnet változó
- **WHEN** az `AZURE_FUNCTIONS_ENVIRONMENT` nincs megadva és a `DOTNET_ENVIRONMENT` értéke `Development`
- **THEN** a környezet `Development`

#### Scenario: Nincs változó
- **WHEN** az `AZURE_FUNCTIONS_ENVIRONMENT` és a `DOTNET_ENVIRONMENT` nincs megadva
- **THEN** a környezet `Production`

#### Scenario: Aspnetcore kimarad
- **WHEN** csak az `ASPNETCORE_ENVIRONMENT` értéke `Development`
- **THEN** a környezet `Production`

### Requirement: Kanonikus környezetnév
A megadott név MUST kis- és nagybetűtől függetlenül a három kanonikus név egyikére illeszkedni. A környezetfájl neve MUST a kanonikus írásmód lenni.

#### Scenario: Eltérő írásmód
- **WHEN** a környezetnév `test`
- **THEN** a környezet `Test`, és a fájl neve `appsettings.Test.json`

### Requirement: Ismeretlen környezetnév
A három kanonikus névtől eltérő megadott név MUST a konfiguráció betöltése előtt megállítani a hostot. A host MUST NOT `Production` környezetre visszaesni.

#### Scenario: Elgépelt név
- **WHEN** a környezetnév `Staging`
- **THEN** a host a konfiguráció betöltése előtt megáll, és nem `Production` környezetben indul

### Requirement: Automatizált teszt
Az automatizált tesztfutás MUST NOT a `Test` környezetet választani, és MUST NOT betölteni az `appsettings.Test.json` fájlt, kivéve ha a teszt maga beállítja a környezetnevet.

#### Scenario: Teszt futtatás
- **WHEN** automatizált teszt fut anélkül, hogy a környezetnevet beállítaná
- **THEN** a `Test` környezet nem választódik, és az `appsettings.Test.json` nem töltődik be

#### Scenario: Teszt beállítja a nevet
- **WHEN** a teszt a környezetnevet `Test` értékre állítja
- **THEN** az `appsettings.Test.json` betöltődik

### Requirement: Betöltési sorrend
A későbbi konfigurációs forrás MUST felülírni a korábbit. A sorrend MUST az `appsettings.json`, a létező `appsettings.{Environment}.json`, a környezeti változók, majd helyben a User Secrets legyen. A hiányzó környezetfájl MUST NOT hibát okozni.

#### Scenario: Környezetfájl felülír
- **WHEN** az alapfájl és a létező környezetfájl ugyanazt a kulcsot más értékkel adja
- **THEN** a környezetfájl értéke érvényes

#### Scenario: Környezeti változó felülír
- **WHEN** egy környezeti változó ugyanazt a kulcsot adja, mint a JSON fájl
- **THEN** a környezeti változó értéke érvényes

#### Scenario: Helyi User Secrets felülír
- **WHEN** a host helyben indul, és a User Secrets ugyanazt a kulcsot adja, mint a környezeti változó
- **THEN** a User Secrets értéke érvényes

#### Scenario: Hiányzó fájl
- **WHEN** a kiválasztott környezet fájlja nem létezik
- **THEN** a host elindul, és az alapfájl értéke marad

### Requirement: Helyi User Secrets mindhárom névvel
A helyben futó host MUST a User Secrets forrást a `Development`, a `Test` és a `Production` név mellett is betölteni, a környezeti változók után.

#### Scenario: Helyi Production
- **WHEN** a host helyben `Production` környezetben indul
- **THEN** a User Secrets forrás a környezeti változók után betöltődik

#### Scenario: Helyi Test
- **WHEN** a host helyben `Test` környezetben indul
- **THEN** a User Secrets forrás a környezeti változók után betöltődik

### Requirement: Azure-ban nincs User Secrets
Az Azure-ban futó host MUST NOT a .NET User Secrets forrást betölteni.

#### Scenario: Azure indítás
- **WHEN** a host Azure-ban indul
- **THEN** a titkos érték Function App Settingsből vagy környezeti változóból töltődik, User Secretsből nem
