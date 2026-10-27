# Spec Delta

## Purpose

A titkos konfiguráció forráskódon kívül tárolható és onnan betölthető, hogy az OsaPro futtatója titok nélkül legyen verziókezelhető.

## ADDED Requirements

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
