# Proposal

## Why

A számlafeldolgozás rögzíti a bizonylatokat és a hibákat. A fizetési teendő, a bizonylatértesítés és a hibajelzés még nincs a `notification` táblában. A későbbi hétköznapi OsaPro-futás olvassa a táblát, és egy üzenetet ad a közös Slack-küldőnek.

## What Changes

- Az `ertesites-osszeallitas` futás a kezelt évek Excel-fájljait olvassa, és `Payment` és `Error` sort ír a `notification` táblába. Teszt és Development `--invoke` indítja. Időzítő, éles végpont, NAV-hívás és Slack-küldés nincs. Az ütemezés a `schedule-and-orchestrate`, a küldés a `build-shared-slack-sender` és az `integrate-slack-sender` change-ben marad.
- A `NotificationTimestamp` a kiküldés UTC ideje. A hétköznapi futás írja, sikeres Slack-válasz után. Ez a change nem írja és nem olvassa. Küldési állapot, újrapróbálás és csatorna nem készül.
- A `MODIFY` és a `STORNO` `Document` sorát a számlafeldolgozás írja, amikor a számlasort létrehozza. Az összeállítás nem pótolja a már tárolt bizonylatot, és a `CREATE` nem kap bizonylatsort. A `MODIFY` szövege: `{eredeti számlaszám} módosítása érkezett, számlaszám: {számlaszám}`. A `STORNO` szövege: `{eredeti számlaszám} sztornózás érkezett, számlaszám: {számlaszám}`. Ugyanarra a számlára második sor nem készül.
- Új Excel-oszlop és külön műveleti sor nincs. Az új sztornó a `Kapcsolt bizonylatok` végére `STORNO:{számlaszám}` alakban kerül. A módosító előtag nélkül kerül a végére. A régi, előtag nélküli érték marad, és nem sztornólánc.
- Fizetési sor csak `Átutalás` esetén készül, típusa `Payment`. Szövege előértesítésnél, határidőnapon és lejárt tételnél is: `{számlaszám} fizetés esedékes {fizetési határidő}, összeg: {számla bruttó összeg}`. A dátum `yyyy.MM.dd`. Az összeg két tizedes, tizedesvessző, ezres tagolás és pénznem nélkül. Hiányzó összegnél a sor megmarad, az összeg helye üres. A mínusz előjel megmarad.
- Egy számlának egy magyar napon egy fizetési sora van. A következő esedékes napon új sor készül akkor is, ha az előző még nincs kiküldve. Kihagyott napot a futás nem pótol. Szombaton és vasárnap fizetési sor nincs. A hétvégére eső határidő és előértesítés a következő hétfőn jelenik meg.
- Az előértesítés a tárgyév `Előértesítési napok` mezője, mindkét év számlájára. Üres érték `7,5,1`. Hibás, nulla vagy negatív érték mellett előértesítés nincs, a határidőnapi és a lejárt sor megmarad, és egy üres azonosságú `Error` sor készül: `Az Előértesítési napok értéke hibás: {érték}.`
- A `Fizetve?` csak pontosan `FIZETVE` esetén kiegyenlített. Az üres cella kiegyenlítetlen. Más érték kiegyenlítetlen, és egyszeri hiba: `{számlaszám} Fizetve? értéke hibás: {érték}. Kiegyenlítetlenként kezeljük.` Átutalásnál a fizetési sor is elkészül. Más fizetési módnál és sztornóláncban csak a hiba sor készül. A `Számla megvan?` cella nem készít értesítést.
- Hiányzó tárgyévi fájl, üres azonossággal: `A tárgyévi nyilvántartás hiányzik: SZÁMLÁK{év}.xlsx. A feldolgozás megállt.` A futás megáll, a fájl nem jön létre, más sor nincs. Az indulás éve a `Processing:StartDate` éve. Ebben az évben a hiányzó előző év nem hiba; ha a fájl megvan, a számlái beleszámítanak. Későbbi évben: `Az előző évi nyilvántartás hiányzik: SZÁMLÁK{év}.xlsx. A feldolgozás megállt.`
- Hibás kezdődátum Excel-olvasás nélkül, üres azonossággal: `A kezdődátum hiányzik vagy hibás.` Olvasási hibánál mindkét meglevő fájl olvasása lefut, majd egy `Error` sor készül: `A nyilvántartás olvasása megállt: SZÁMLÁK{év}.xlsx.` Ugyanazon a napon második sor nincs. A következő napon, ha a hiba megmarad, új sor készül. Ilyenkor `Document` és `Payment` sor nincs.
- A számlaszintű sor tölti a `Taxnumber`, `Partnername` és `Invoice` mezőt. A fájl-, kezdődátum-, olvasási és előértesítés-sorban ezek üresek. Az `ANNUL` nem kap sort. A számlafeldolgozás meglévő `Error` sorai megmaradnak.

## Capabilities

### New Capabilities

- `notification-content`: fizetési, bizonylat- és hibasorok összeállítása, majd tárolása a `notification` táblában.

### Modified Capabilities

- `inbound-invoice-processing`: új `MODIFY` és `STORNO` számlasornál `Document` sort és `STORNO:` előtagot ír. A régi kapcsolatot nem írja át. A `NotificationTimestamp` üres marad. Küldési állapot, újrapróbálás és csatorna nem készül.

## Impact

- Az `OsaPro.Worker` számlafeldolgozása és az `ertesites-osszeallitas` belépése, a `notification` tábla írása, tesztekkel. Az összeállítás az Excelt csak olvassa.

## Érinti-e kritikus komponenst?

Igen. Excel-olvasás, bizonylatkapcsolat a sztornólánc felismeréséhez, határidő és értesítési tartalom. NAV-hívás és Slack-küldés nem készül.

## Assumptions from codebase

- Az `ertesites-osszeallitas` azonosító megvan (`src/OsaPro.Common/FunctionIds.cs`, 7. sor). A `RunNotificationComposition` munkája üres (`src/OsaPro.Worker/FunctionHandlers.cs`, 13–16. sor). A `--invoke` csak Developmentben választ futást (`src/OsaPro.Worker/WorkerEntry.cs`, 47–55. sor). Táblát most csak a számlafeldolgozás nyit (`src/OsaPro.Worker/Program.cs`, 37–44. sor). A `slack-uzenetek-kuldese` azonosító megvan, ez a change nem indítja (`FunctionIds.cs`, 9. sor).
- A tábla neve `notification` (`src/OsaPro.Worker/NotificationLog.cs`, 50. sor). Üres `Processing:NotificationTableUri` megállítja a megnyitást (54–61. sor). Az `Add` 409-nél kihagyja a sort (83–89. sor). A kiírt mezők: `PartitionKey`, `RowKey`, `Type`, `Message`, `Taxnumber`, `Partnername`, `Invoice` (`src/OsaPro.Worker/NotificationRecords.cs`, 76–87. sor). A felületnek csak `Add` művelete van (`NotificationLog.cs`, 7–9. sor).
- A `Kapcsolt bizonylatok` a számlaszámot előtag nélkül fűzi hozzá (`src/OsaPro.Worker/ProcessingRules.cs`, 297–306. sor). A `TRANSFER` címkéje `Átutalás` (89–99. sor).
- Az éves fájl `SZÁMLÁK{év}.xlsx` (`src/OsaPro.Excel/WorkbookRules.cs`, 49–51. sor). Az előértesítés kulcsa `Előértesítési napok`, az új lap értéke `7,5,1` (9–11. sor). A hiányzó évi fájl kimarad a letöltésből (`src/OsaPro.Excel/ExcelWorkbook.cs`, 136–140. sor). A `ReadSnapshot` ilyenkor az előző év előértesítésére eshet vissza (`src/OsaPro.Excel/WorkbookFile.cs`, 343–353. sor). Az összeállítás ezt a visszaesést nem használja.
- A dátumcella Excel-sorszám, a `1899-12-30` epochával (`WorkbookFile.cs`, 598–600. és 708–710. sor). Az összegcella tizedespontos szám (602–604. sor). Az olvasás a nyers cellaértéket adja (217–233. sor). A nem havi lap és a fejlécsor kimarad (459–472. sor).
