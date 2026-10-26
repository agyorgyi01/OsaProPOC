# Spec Delta

## ADDED Requirements

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

### Requirement: Fejlesztői és teszt NAV-cím
`Development` és `Test` környezetben a betöltött `Nav:BaseUrl` értéke MUST `https://api-test.onlineszamla.nav.gov.hu` lenni, amíg későbbi forrás felül nem írja.

#### Scenario: Development
- **WHEN** a host `Development` környezetben indul, és későbbi forrás nem írja felül a `Nav:BaseUrl` értéket
- **THEN** a betöltött érték `https://api-test.onlineszamla.nav.gov.hu`

#### Scenario: Test
- **WHEN** a host `Test` környezetben indul, és későbbi forrás nem írja felül a `Nav:BaseUrl` értéket
- **THEN** a betöltött érték `https://api-test.onlineszamla.nav.gov.hu`

### Requirement: Éles NAV-cím az alapfájlban
A verziókezelt `appsettings.json` `Nav:BaseUrl` értéke MUST `https://api.onlineszamla.nav.gov.hu` lenni.

#### Scenario: Alapfájl
- **WHEN** a verziókezelt `appsettings.json` a `Nav` szakaszt tartalmazza
- **THEN** a `BaseUrl` értéke `https://api.onlineszamla.nav.gov.hu`

### Requirement: Production környezetfájl nincs
`appsettings.Production.json` MUST NOT készülni. `Production` környezetben, későbbi felülírás nélkül, a betöltött `Nav:BaseUrl` MUST az alapfájl értéke maradni.

#### Scenario: Production indítás
- **WHEN** a host `Production` környezetben indul, és későbbi forrás nem írja felül a `Nav:BaseUrl` értéket
- **THEN** nincs `appsettings.Production.json`, és a betöltött érték `https://api.onlineszamla.nav.gov.hu`

### Requirement: Környezetfájl csak felülírás
Az `appsettings.Development.json` és az `appsettings.Test.json` MUST létezni, és MUST csak a `Nav:BaseUrl` felülírását tartalmazni. Titkos értéket MUST NOT tartalmazni. Üres környezetfájl MUST NOT készülni.

#### Scenario: Development fájl
- **WHEN** a verziókezelt `appsettings.Development.json` betöltődik
- **THEN** a `Nav:BaseUrl` értéke `https://api-test.onlineszamla.nav.gov.hu`, és a fájl nem tartalmaz titkos értéket

#### Scenario: Test fájl
- **WHEN** a verziókezelt `appsettings.Test.json` betöltődik
- **THEN** a `Nav:BaseUrl` értéke `https://api-test.onlineszamla.nav.gov.hu`, és a fájl nem tartalmaz titkos értéket

### Requirement: Környezetfájl a buildben
A létező `appsettings.Development.json` és `appsettings.Test.json` MUST a build és a publish kimenetbe kerülni ugyanúgy, mint az `appsettings.json`. A `local.settings.json` MUST NOT a publish kimenetbe kerülni.

#### Scenario: Publish
- **WHEN** a Worker publikálódik
- **THEN** a két környezetfájl és az `appsettings.json` a kimenetben van, a `local.settings.json` nincs

#### Scenario: Build
- **WHEN** a Worker buildelődik
- **THEN** a két környezetfájl a build kimenetben van
