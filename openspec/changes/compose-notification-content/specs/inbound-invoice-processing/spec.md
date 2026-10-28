# Spec Delta

## MODIFIED Requirements

### Requirement: Bizonylatkapcsolat
A `Kapcsolt bizonylatok` mező MUST a módosító számlaszámot előtag nélkül, a sztornót `STORNO:` előtaggal, pontosvesszővel és szóköz nélkül tartalmazni. A már tárolt, előtag nélküli értéket a rendszer MUST NOT átírni. A meglévő sorrend MUST megmaradni, az új hivatkozás MUST a végére kerülni, és a már szereplő szám MUST NOT ismétlődni. Az `Eredeti számla` MUST csak az eredeti számlaszám lenni. A `CREATE`, a `MODIFY` és a `STORNO` MUST önálló sorba kerülni, a saját kelte szerinti lapra.

#### Scenario: Új módosító
- **WHEN** az eredeti `Kapcsolt bizonylatok` értéke `SZ-1`, és új módosító érkezik `SZ-2` számmal
- **THEN** a mező `SZ-1;SZ-2`, az `Eredeti számla` pedig az eredeti számlaszám

#### Scenario: Új sztornó
- **WHEN** az eredeti `Kapcsolt bizonylatok` értéke `SZ-1`, és új sztornó érkezik `SZ-3` számmal
- **THEN** a mező `SZ-1;STORNO:SZ-3`

#### Scenario: Ismételt hivatkozás
- **WHEN** a `SZ-1` már szerepel a `Kapcsolt bizonylatok` mezőben
- **THEN** a mező nem kap második `SZ-1` értéket

#### Scenario: Régi sztornó előtag nélkül
- **WHEN** a `SZ-3` előtag nélkül már szerepel a mezőben, és ugyanaz a sztornó ismét kapcsolódik
- **THEN** a mező nem íródik át, és nem kap `STORNO:SZ-3` értéket

### Requirement: Ismételt szelet kapcsolata
Az ismételt szelet MUST a már nyilvántartott módosító és sztornó hiányzó hivatkozását is az eredeti `Kapcsolt bizonylatok` mezőjének végére írni. A módosító szám MUST előtag nélkül, a sztornó szám MUST `STORNO:` előtaggal kerülni a mezőbe. A már tárolt, előtag nélküli szám MUST NOT átíródni, és MUST NOT másodikként, előtaggal bekerülni. Más cella MUST NOT változni. Ha az eredeti megvan, új hiányzó-eredeti bejegyzés MUST NOT készülni. A meglévő bejegyzés MUST megmaradni.

#### Scenario: A kapcsolat írása megszakadt
- **WHEN** a módosító sora már a nyilvántartásban van, és az eredeti `Kapcsolt bizonylatok` mezője még nem tartalmazza a módosító számát
- **THEN** az ismételt szelet a számot előtag nélkül a mező végére írja, és más cellát nem változtat

#### Scenario: Hiányzó sztornóhivatkozás
- **WHEN** a sztornó sora már a nyilvántartásban van, és az eredeti mezője még nem tartalmazza a számát
- **THEN** az ismételt szelet a `STORNO:` előtaggal írja a számot a mező végére

#### Scenario: Az eredeti az ismétléskor megvan
- **WHEN** a hiányzó-eredeti bejegyzés már létezik, és az ismételt szelet megtalálja az eredetit
- **THEN** új bejegyzés nem készül, a meglévő megmarad, és a kapcsolat felkerül

#### Scenario: Régi előtag nélküli szám
- **WHEN** a sztornó száma előtag nélkül már a mezőben van
- **THEN** az ismételt szelet nem írja át, és nem fűz hozzá második `STORNO:` elemet

### Requirement: Hibabejegyzés határa
A hibabejegyzés a `notification` táblába kerül. A `Type` értéke `Error`, a `Message` a tartalom. Éven kívüli és hiányzó eredeti sor MUST a bizonylat adataival kitölteni a `Taxnumber`, a `Partnername` és az `Invoice` mezőt. Hibás ciklus sora MUST üres szöveggel írni ezt a három oszlopot. A `Notification` típus későbbi értesítéshez van. A `NotificationTimestamp` üres marad. Küldési állapot, újrapróbálás és csatorna MUST NOT készülni.

#### Scenario: Számlaszintű tartalom
- **WHEN** éven kívüli vagy hiányzó eredeti hibabejegyzés készül
- **THEN** a sor `Type` értéke `Error`, a `Message` tartalmazza a tartalmat, a `Taxnumber`, a `Partnername` és az `Invoice` a bizonylat adata, a `NotificationTimestamp` üres, küldési állapot, újrapróbálás és csatorna nélkül

#### Scenario: Ciklus üres azonossága
- **WHEN** hibás ciklus hibabejegyzés készül
- **THEN** a sor `Type` értéke `Error`, a `Message` a leállást tartalmazza, a `Taxnumber`, a `Partnername` és az `Invoice` üres szöveg, az oszlopok a sorban vannak, a `NotificationTimestamp` üres, küldési állapot, újrapróbálás és csatorna nélkül

## ADDED Requirements

### Requirement: Bizonylatértesítés rögzítéskor
A `MODIFY` és a `STORNO` számlasorának létrehozásakor a rendszer MUST egy `Document` sort írni a `notification` táblába. A `CREATE` MUST NOT ilyen sort kapni. Már tárolt bizonylat MUST NOT utólagos sort kapni. Ismételt futás MUST NOT második sort írni ugyanarra az adószámra és számlaszámra. A `Taxnumber`, a `Partnername` és az `Invoice` MUST a bizonylat adata lenni, és az `Invoice` a saját számlaszám. A `NotificationTimestamp` MUST üres maradni.

#### Scenario: Új módosító
- **WHEN** új `MODIFY` számlasor készül, az eredeti szám `E-1`, a saját szám `SZ-2`
- **THEN** egy `Document` sor készül, a `Message` értéke `E-1 módosítása érkezett, számlaszám: SZ-2`, az `Invoice` értéke `SZ-2`, a `NotificationTimestamp` üres

#### Scenario: Új sztornó
- **WHEN** új `STORNO` számlasor készül, az eredeti szám `E-1`, a saját szám `SZ-3`
- **THEN** a `Message` értéke `E-1 sztornózás érkezett, számlaszám: SZ-3`, az `Invoice` értéke `SZ-3`

#### Scenario: Új számla
- **WHEN** új `CREATE` számlasor készül
- **THEN** `Document` sor nem készül

#### Scenario: Már tárolt bizonylat
- **WHEN** a `MODIFY` vagy a `STORNO` sora már a nyilvántartásban van, és `Document` sora nincs
- **THEN** utólagos `Document` sor nem készül

#### Scenario: Ismételt futás
- **WHEN** ugyanarra az adószámra és számlaszámra már készült `Document` sor
- **THEN** második `Document` sor nem készül
