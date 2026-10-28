# OsaPro üzleti szabályok

A jelölések jelentése: `project.md`. A hatókör és a kizárások ott vannak. A rendszerek és a tárolók: `architecture.md`.

## Nyilvántartás

**KNOWN** – A számlákat éves Excel-fájlokban, havi munkalapokon tartják nyilván.

**KNOWN** – A számlák kiegyenlítését a felhasználó jelöli az Excelben.

**DECISION** – A megfelelő Excel-fájlt és havi munkalapot a számla kelte határozza meg.

**DECISION** – Az új számla a megfelelő havi munkalap utolsó kitöltött sora után kerül.

**DECISION** – A felhasználó szabadon rendezheti, szűrheti a munkalapot és szúrhat be sorokat. A feldolgozás nem támaszkodhat rögzített sorszámokra.

**DECISION** – A program az éves Excel-fájl `Paraméterek` munkalapjának `InsDate` mezőjében tárolja, meddig jutott a feldolgozásban. Ez nem a NAV-bizonylat `insDate` mezője.

**DECISION** – Az új éves Excel-fájl létrehozásakor a `Paraméterek` munkalap értékeit az előző éves fájlból vesszük át, az `InsDate` értékével együtt. Az évváltás miatt nem maradhat ki számla.

**DECISION** – A havi munkalap oszlopai ebben a sorrendben:

1. `Partner neve`: a számlát kiállító eladó vagy szolgáltató neve.
2. `Adószám`: a partner adószáma. A számlasorszámmal együtt azonosítja a bizonylatot.
3. `Bankszámlaszám`: a partner bankszámlaszáma, amelyre az átutalás teljesíthető.
4. `Számlasorszám`: a bizonylat kibocsátó által megadott azonosítója.
5. `Eredeti számla`: módosító vagy sztornóbizonylat esetén az eredeti számla hivatkozása.
6. `Kapcsolt bizonylatok`: az eredeti számlához tartozó módosító és sztornóbizonylatok hivatkozásai, ismétlés nélkül.
7. `Számla kelte`: a bizonylat kiállításának dátuma. Ez határozza meg az éves fájlt és a havi munkalapot.
8. `Fiz. határidő`: a fizetés esedékessége. Hiányában a számla keltét használjuk.
9. `Fiz. összeg`: a bizonylat fizetendő összege.
10. `Áfa tartalom`: áfakulcs vagy adózási jelölés, például `27%`, `0%`, `FAD`.
11. `Áfa összege`: a bizonylaton szereplő áfa pénzösszege.
12. `Fizetési mód`: például `ÁTUTALÁS`, `KÉSZPÉNZ`, `BANKKÁRTYA`, `UTÁNVÉT`. Az `ELŐLEGEZVE` előzetes fizetésre utaló jelölés.
13. `Számla megvan?`: felhasználói jelölés. Üres: a rendelkezésre állás még nincs igazolva. `IGEN`: a bizonylat rendelkezésre áll.
14. `Fizetve?`: felhasználói jelölés. A szélső whitespace nélkül, nagybetűvel `FIZETVE` esetén kiegyenlített; üresen kiegyenlítetlen. Más érték hibás.
15. `Autó rendelés`: felhasználó által kezelt belső hivatkozás, például rendszám, személynév vagy rendelési megjelölés.
16. `Megjegyzés`: felhasználó által kezelt szabad szöveges kiegészítés.

A példák nem zárt értékkészletek. Kivétel a `Számla megvan?` és a `Fizetve?` mező, amelyek szabálya fent és a későbbi szakaszokban rögzített. Az `Autó rendelés` és a `Megjegyzés` mezőből az OsaPro nem vezet le automatikus üzleti működést.

## Lekérdezés és folytatás

**DECISION** – Az OsaPro a NAV által befogadott számlákat a befogadás időpontja (`insDate`) alapján kérdezi le.

**DECISION** – Az OsaPro konfigurációs paraméterben megadott CRON-ütemezés szerint lekérdezi az újonnan befogadott számlákat, és frissíti az Excel-nyilvántartást.

**DECISION** – A feldolgozás kezdődátuma az első éles futás napja. A korábban befogadott számlákat nem tölti vissza.

**DECISION** – A program kizárólag az aktuális és az előző naptári évben kiállított számlákat kezeli. A fizetési figyelésre is ez vonatkozik.

**DECISION** – A `queryInvoiceDigest` lekérdezés az eredeti, tárolt `InsDate` UTC naptári napjának `00:00:00Z` időpontjától a kapott `InsDate` UTC naptári napját követő nap `00:00:00Z` időpontjáig tart. A kapott `InsDate` a lekérdezés aktuális UTC időpontja. A kezdődátum `00:00:00Z` időpontja előtt a lekérdezés nem indul. A 35 napnál hosszabb időszak legfeljebb 35 napos szeletekben kérdezendő.

**DECISION** – Egy szelet sikeres feldolgozása után a program az `InsDate` értékét a szelet végére állítja, akkor is, ha nem érkezett számla. Hibás vagy részleges feldolgozás esetén nem léphet túl a feldolgozatlan időszakon.

**DECISION** – A bizonylatokhoz nem tárolunk külön feldolgozási állapotot.

## Azonosítás és meglévő sorok

**DECISION** – A számlát az eladó adószáma és a számlaszám együtt azonosítja. Ha ez a számla már szerepel a nyilvántartásban, nem jön létre új sor, és a számlaadatok nem módosulnak.

**DECISION** – A meglévő számlasorok adatait az OsaPro nem írja felül, kivéve az eredeti számla `Kapcsolt bizonylatok` mezőjének bővítését. A számlaadatokat a felhasználó a kézhez kapott bizonylat alapján ellenőrzi és javítja.

**DECISION** – A rögzített bizonylatokat nem töröljük és nem helyezzük át.

**DECISION** – A NAV-ban opcionális adatok az OsaPro számára is opcionálisak. Ha a fizetési határidő hiányzik, a számla keltét használjuk határidőként.

## Bizonylattípusok és kapcsolatok

**DECISION** – A `CREATE`, `MODIFY` és `STORNO` bizonylatok önálló Excel-sorba kerülnek, a saját kiállítási dátumuk szerint.

**DECISION** – A módosító és sztornóbizonylat kapcsolódik az eredeti számlához; a kapcsolatot az `Eredeti számla` mező jelzi.

**DECISION** – Az eredeti számla `Kapcsolt bizonylatok` mezője az összes hozzá tartozó módosító és sztornóbizonylatot tartalmazza, ismétlés nélkül.

**DECISION** – A `MODIFY` saját fizetési határidővel és saját `Fizetve?` mezővel rendelkezik.

**DECISION** – A sztornóval érintett számlaláncokat a bizonylatok kapcsolata alapján határozzuk meg, külön státuszmező nélkül.

**DECISION** – Ha egy számlalánchoz sztornóbizonylat tartozik, a lánc egyik bizonylatáról sem küldünk fizetési figyelmeztetést vagy előértesítést. A sztornóbizonylatról szóló egyszeri értesítést elküldjük.

Az `ANNUL` művelet a hatókörön kívül van: `project.md`.

## A számla rendelkezésre állása

**DECISION** – A nyilvántartásban szerepel egy felhasználó által kezelt `Számla megvan?` oszlop:

- Üres: a számlabizonylat rendelkezésre állását a felhasználó még nem igazolta.
- `IGEN`: a számla papíron vagy elektronikusan a felhasználó rendelkezésére áll.

**DECISION** – A `Számla megvan?` mező nem befolyásolja a fizetési figyelmeztetéseket. A számla ellenőrzése és a fizetésről szóló döntés a felhasználó felelőssége.

## Kiegyenlítettség

**DECISION** – A `Fizetve?` mezőt kizárólag a felhasználó módosíthatja.

**DECISION** – A számla csak akkor kiegyenlített, ha a mező szélső whitespace nélkül, nagybetűvel `FIZETVE`.

**DECISION** – Az üres mező kiegyenlítetlen számlát jelent. Más érték hibás, és a számlát továbbra is kiegyenlítetlenként kezeljük; a hibát az értesítőben jelezzük.

A kiegyenlítés automatikus megállapítása és a részkiegyenlítések kezelése a hatókörön kívül van: `project.md`.

## Fizetési határidők

**DECISION** – A számlafeldolgozás és az értesítés-összeállítás minden naptári napon fut, ebben a sorrendben. Szombaton és vasárnap fizetési sor nem készül. A Slack-összesítő hétfőtől péntekig fut, a napi írás után. Ünnepnapnaptárat nem használunk.

**DECISION** – Az előértesítési napokat a felhasználó állítja be, például `7,3,1`. Ezek naptári napokat jelentenek.

**DECISION** – A hétvégére eső figyelmeztetés a következő hétfőn kerül kiküldésre. A számla tényleges fizetési határidejét nem módosítjuk.

**DECISION** – A határidő napjától a kiegyenlítetlen számla minden hétfőtől péntekig küldött összesítőben szerepel, amíg `FIZETVE` nem lesz, kivéve a sztornóval érintett számlaláncokat.

**DECISION** – Egy számlának egy magyar napon egy fizetési sora van. A még kiküldetlen korábbi fizetési sorok is bekerülnek az összesítőbe.

## Slack-értesítések

**DECISION** – Az OsaPro hétköznaponként egy összesítőt ad át a közös Slack-küldőnek. Az összesítő akkor is elmegy, ha nincs `Payment` sor.

**DECISION** – Az összesítő az üres `NotificationTimestamp` értékű sorokból áll, három részben. Az első a `Payment` sorok. Ha ilyen sor nincs, a szöveg: `Nincs fizetni való, hátradőlhetsz!` A második a `Document` sorok. A harmadik az `Error` sorok.

**DECISION** – Pozitív küldési válasz után az OsaPro az elküldött sorok `NotificationTimestamp` mezőjébe az aktuális UTC időt írja. A közös Slack-küldő a táblát nem írja. Üres mezőnél a sor a következő hétköznapi összesítőben újra szerepel.

**DECISION** – A Slack-értesítésben megjeleníthető személyes adatok köre: `architecture.md`.

## Hibák és rendkívüli esetek

**DECISION** – A kezelt éveken kívüli számlák, az ismeretlen eredeti számlához tartozó `MODIFY`/`STORNO` és a tartós NAV- vagy Excel-elérési hibák Slack-jelzést kapnak. A kezelt éveken kívüli számlák felvétele a hatókörön kívül van: `project.md`.

**DECISION** – A programhibákat naplózzuk és jelezzük a Slack-összesítőben.

**DECISION** – Nem vezetünk Excel-alapú hibanyilvántartást vagy `Hibák` oszlopot.

## Indulási átmenet

**DECISION** – A kezdődátum előtti eredeti számlához később érkező `MODIFY` vagy `STORNO` önálló sorba kerül, ha a bizonylat kelte a kezelt időszakba esik. A hiányzó eredeti számláról a felhasználó Slack-jelzést kap.

## Adatfelelősség

**DECISION** – Az adatok módosításának felelőssége:

| Adat | Ki kezeli? |
|---|---|
| Új számlasor és a kezdeti NAV-adatok | OsaPro |
| Eredeti számla `Kapcsolt bizonylatok` mezője | OsaPro |
| Meglévő számlasor adatainak javítása | Felhasználó, a tényleges számlabizonylat alapján |
| `Fizetve?` | Felhasználó |
| `Számla megvan?` | Felhasználó |
| Előértesítési paraméterek | Felhasználó |
| `notification` sor és `NotificationTimestamp` | OsaPro |

## Nyitott kérdések

Ezek nem döntések.

**OPEN QUESTION** – Küldjön-e az OsaPro figyelmeztetést, ha a `Számla megvan?` mező üres?

**OPEN QUESTION** – A Slack-üzenet célcsatornája és a három rész elrendezése később pontosítandó. A részek sorrendje és a fizetés nélküli mondat döntött. A megjeleníthető személyes adatok: `architecture.md`.

**OPEN QUESTION** – A Slack elérési hibájáról milyen módon történjen értesítés?

**OPEN QUESTION** – Az Excel-sablon oszloplistán túli részei később pontosítandók. Az oszlopnevek és a sorrendjük döntöttek.
