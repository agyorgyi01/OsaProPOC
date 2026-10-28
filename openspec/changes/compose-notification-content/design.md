# Design

## Context

A viselkedés: `specs/notification-content/spec.md` és `specs/inbound-invoice-processing/spec.md`. A motiváció: `proposal.md` - Why.

Az `ertesites-osszeallitas` belépés megvan, a munkája üres. A `Program.cs` csak a számlafeldolgozáshoz nyit táblát. A `notification` tábla sora `PartitionKey`, `RowKey`, `Type`, `Message`, `Taxnumber`, `Partnername` és `Invoice`. Az `Add` a 409 választ kihagyja, sort nem ír felül. A meglévő hibasorok azonossága `out-of-year`, `missing-original` és `cycle` előtagú. Az `EnsureMonth` fájlt hozhat létre. A `ReadSnapshot` hiányzó tárgyévi fájlnál az előző év előértesítését használja.

## Goals / Non-Goals

**Goals:**

- Az összeállítás a meglévő belépésből fut, Excel-olvasással és táblaírással, NAV nélkül.
- Az esedékesség, a sztornólánc és a szövegek Graph nélkül tesztelhetők.
- A `Document` sor és a `STORNO:` előtag a számlasor létrehozásakor készül, a már tárolt kapcsolat átírása nélkül.

**Non-Goals:**

- Slack-küldés, időzítő, éles végpont, küldési állapot, újrapróbálás és a `NotificationTimestamp` írása.
- Új Excel-oszlop, külön műveleti táblasor és a régi bizonylatok utólagos `Document` sora.
- A `notification` tábla meglévő `Error` sorainak átírása.

## Decisions

### 1. Két író, egy tábla

A számlafeldolgozás a `MODIFY` és a `STORNO` új számlasoránál `Document` sort ír, ugyanott, ahol a többi bejegyzés készül, a feltöltés előtt. A `CREATE` nem kap ilyet. Már ismert számla nem kap utólagos sort. A táblahiba a szeletet a feltöltés előtt állítja meg. Sikeres bejegyzés és sikertelen feltöltés után az ismétlés a 409 miatt nem ír második `Document` sort.

Az összeállítás csak `Payment` és `Error` sort ír. A `Program.cs` az `ertesites-osszeallitas` ágon nyitja a táblát és az Excel-olvasót. Új trigger nem készül.

Alternatíva: az összeállítás pótolja a régi `Document` sorokat. Elvetve, mert a spec az utólagos sort tiltja. Alternatíva: a művelet külön táblasor. Elvetve, mert a küldő minden sort olvasna.

### 2. A szabály tiszta, az olvasás nem hoz létre fájlt

Az esedékesség, a szöveg és a sztornólánc tiszta függvény. Bemenete a mai magyar dátum, a beolvasott sorok és a tárgyévi előértesítés. Nem hív Graphot és táblát.

Az olvasó a mappa gyermekeit nézi, és csak a meglevő évi fájlt tölti le. Az `EnsureMonth` nem fut. A `ReadSnapshot` előértesítés-visszaesése nem dönt: hiányzó tárgyévi fájl hiba és megállás, nem az előző év `7,5,1` helyett használt listája. Üres, hiányzó kulcs vagy hiányzó `Paraméterek` lap a tárgyévi fájlban `7,5,1`.

A sorrend: táblamegnyitás, kezdődátum, fájlok megléte, mindkét létező fájl letöltése, utána írás. Hibás kezdődátumnál Excel nem indul. Olvasási hibánál a másik létező fájl letöltése is lefut, majd egyetlen hiba sor készül. Ha a tárgyévi letöltés hibás, a szöveg azt nevezi. Egyébként az előző évit.

Alternatíva: a meglévő `ReadRegistry` egy az egyben. Elvetve, mert hiányzó tárgyévi fájlnál nem áll meg, és az előző év előértesítését venné át.

### 3. Az azonosság a típus és a kulcs, a három mező a spec szerint üres lehet

A `PartitionKey` a `Type`. A `RowKey` ettől független, hogy az üres azonosságú sorok ne fedjék egymást és a régi `Error` sorokat:

- `Document`: `{adószám}|{számlaszám}`
- `Payment`: `{magyar nap}|{adószám}|{számlaszám}`
- hibás `Fizetve?`: `paid|{adószám}|{számlaszám}`
- hibás előértesítés: `notice-days`
- hiányzó fájl: `missing-file|{fájlnév}`
- hibás kezdődátum: `start-date`
- olvasási hiba: `read|{magyar nap}`

Az adószám és a számlaszám csak a szélső whitespace-t veszíti el, sorrendi összehasonlítással. Nem a `WorkbookRules.Identity` nagybetűsítése. A `Payment` kulcs a nappal kezdődik, ezért egy számlának naponta egy sora van. Az `Add` továbbra sem ír felül: a `notice-days` kulcs egy sor, későbbi más hibás érték sem cseréli le és nem kap másodikat.

A `NotificationTimestamp` a kiküldés UTC időpontja. A hétköznapi futás írja, sikeres Slack-válasz után. Ez a futás nem olvassa és nem írja. Új fizetési sorhoz a mező nem számít. A tábla saját `Timestamp` mezője nem ez. Küldési állapot, újrapróbálás és csatorna nem kerül a sorba.

Alternatíva: a nap a `Payment` kulcs végén. Elvetve, mert a mai sorok úgy nem egy tartomány. Alternatíva: üres `RowKey` minden nem számlaszintű hibánál. Elvetve, mert egy sor felülírná a másikat.

### 4. A sztornójel a hivatkozás szövegében van

Új sztornó a `Kapcsolt bizonylatok` végére `STORNO:{számlaszám}` alakban kerül. Új módosító előtag nélkül. Ha a szám előtaggal vagy anélkül már a listában van, a mező nem változik, és előtaggal sem ismétlődik. A régi, előtag nélküli érték nem íródik át.

Az összeállítás pontosvessző mentén vág, az elem szélső whitespace-ét levágja. A `STORNO:` előtag sorrendi egyezés. Az előtag utáni rész a számlaszám. Sztornólánc az a sor, amelynek listájában van ilyen elem, továbbá az azonos adószámú sor, amelynek száma a listában van, vagy amelynek `Eredeti számla` értéke ennek a sornak a száma. Más lista nem lánc.

Alternatíva: új Excel-oszlop a műveletnek. Elvetve, mert a proposal nem vesz fel oszlopot.

### 5. A dátum és az összeg a cellából készül

A `Fizetési mód` összehasonlítás előtt a szélső whitespace lekerül. A `Fizetve?` a szélső whitespace nélkül, nagybetűvel `FIZETVE` esetén kiegyenlített. Az üres cella kiegyenlítetlen. A határidő a `Fiz. határidő` dátuma, különben a `Számla kelte`. A program által írt dátumcella nyers Excel-sorszám, a `1899-12-30` epochával. A `yyyy-MM-dd` szöveg is dátum. Más szöveg nem dátum: nincs fizetési sor és nincs külön hiba sor. A szöveg dátuma a tényleges határidő `yyyy.MM.dd` alakban, akkor is, ha a jelzés hétfőre csúszott. Az összegcella nyers, invariáns tizedespontos szám. A kiírt összeg két tizedes, tizedesvessző, ezres tagolás nélkül. A nem számként olvasható összeg ugyanaz, mint a hiányzó: a sor megmarad, az összeg helye üres. A mínusz előjel megmarad. Ugyanazon adószám és számlaszám első sora adja a szöveget, a későbbi másolat nem készít második fizetési sort.

A magyar nap a `Europe/Budapest` zóna. Az indulás éve a `Processing:StartDate` évszáma, nem a mai nap éve.

Alternatíva: a `Fizetve?` csak a pontos `FIZETVE` szövegre egyezik. Elvetve, mert a mező a szélső whitespace nélkül, nagybetűvel egyezik.

## Risks / Trade-offs

- [A régi, előtag nélküli sztornólánc fizetési sort kaphat] → a spec szerint nem sztornólánc. Új sztornó már előtagot kap. Régi fájl nem íródik át.
- [A `Document` sor a feltöltés előtt készül] → sikertelen feltöltés után az ismétlés nem ír második sort, a számlasor a következő sikeres szeletben megjön.
- [A feltöltés sikerül, a `Document` írás mégsem] → a szelet a feltöltés előtt áll, ezért a számlasor sem kerül fel. Már fent lévő számlához nincs pótlás.
- [A `notice-days` egyetlen kulcs] → más hibás érték nem kap új sort, a régi szöveg marad.

## Migration Plan

Új Azure-erőforrás nincs. A `notification` tábla és a `Processing:NotificationTableUri` marad. Az első összeállítás a meglévő Excelből írhat `Payment` és `Error` sort. `Document` sort csak a change után rögzített `MODIFY` és `STORNO` kap. Visszalépésnél a már kiírt sorok maradnak. A program nem töröl sort, és a régi `Kapcsolt bizonylatok` értéket nem írja vissza.
