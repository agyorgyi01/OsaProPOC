# operational-logging Specification

## Purpose

A futások és a hibák naplózhatók, és a naplók a számlafeldolgozás, az értesítés-összeállítás és a Slack-üzenetek küldése szerint szétválaszthatók.

## Requirements

### Requirement: Futás naplózása
A rendszer MUST a Function futásáról naplóbejegyzést készíteni, a Function azonosítójával.

#### Scenario: Számlafeldolgozás futása
- **WHEN** a számlafeldolgozás fut
- **THEN** a naplóbejegyzés a számlafeldolgozáshoz tartozik, és elkülöníthető az értesítés-összeállítás és a Slack-üzenetek küldése naplóitól

#### Scenario: Értesítés-összeállítás futása
- **WHEN** az értesítés-összeállítás fut
- **THEN** a naplóbejegyzés az értesítés-összeállításhoz tartozik, és elkülöníthető a számlafeldolgozás és a Slack-üzenetek küldése naplóitól

### Requirement: Hiba naplózása
A rendszer MUST a futás hibáját naplóbejegyzésben rögzíteni, a Function azonosítójával.

#### Scenario: Számlafeldolgozás hibája
- **WHEN** a számlafeldolgozás hibával végződik
- **THEN** a hiba naplóbejegyzése a számlafeldolgozáshoz tartozik, és elkülöníthető a másik két Function naplóitól

#### Scenario: Értesítés-összeállítás hibája
- **WHEN** az értesítés-összeállítás hibával végződik
- **THEN** a hiba naplóbejegyzése az értesítés-összeállításhoz tartozik, és elkülöníthető a másik két Function naplóitól

### Requirement: Ütemezés nélküli meghívás
A számlafeldolgozás és az értesítés-összeállítás MUST teszttel és helyi fejlesztői indítással meghívható legyen. Éles időzítő és élesben hívható végpont nem készül. A futás és a hiba a meghívott Function azonosítójával naplózott.

#### Scenario: Nincs éles elérés
- **WHEN** a két belépési pont a host része
- **THEN** egyikhez sem tartozik éles időzítő vagy élesben hívható végpont

#### Scenario: Teszt vagy helyi meghívás
- **WHEN** az egyik belépési pont teszttel vagy helyi fejlesztői indítással meghívódik
- **THEN** a futás naplója a meghívott Function azonosítóját viseli

#### Scenario: Hibás teszt vagy helyi meghívás
- **WHEN** a tesztből vagy helyi fejlesztői indításból meghívott belépési pont hibával végződik
- **THEN** a hiba naplója a meghívott Function azonosítóját viseli

### Requirement: Titkos érték maszkolása
A rendszer MUST a titkos konfigurációs kulcs aktuális értékét a naplóban a `##KulcsNeve##` formára cserélni. A nyers érték MUST nem maradhat a naplóbejegyzésben.

#### Scenario: Érték egy mondatban
- **WHEN** a `Secrets` szakasz `ExcelFile` kulcsának aktuális értéke `valami.xls`, és a naplószöveg `nem nyitható meg a valami.xls fájl`
- **THEN** a naplóbejegyzés `nem nyitható meg a ##ExcelFile## fájl`, és a `valami.xls` szöveg nem szerepel benne

### Requirement: Slack-küldés elkülöníthető azonosítója
A naplózás MUST a Slack-üzenetek küldéséhez önálló, a másik két Functiontól elkülöníthető azonosítót adni.

#### Scenario: Három Function azonosítható
- **WHEN** egy naplóbejegyzés Function-azonosítót kap
- **THEN** az azonosító a számlafeldolgozás, az értesítés-összeállítás és a Slack-üzenetek küldése közül választható, és a három érték egymástól elkülöníthető
