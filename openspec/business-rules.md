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

## Lekérdezés és folytatás

**DECISION** – Az OsaPro a NAV által befogadott számlákat a befogadás időpontja (`insDate`) alapján kérdezi le.

**DECISION** – Az OsaPro konfigurációs paraméterben megadott CRON-ütemezés szerint lekérdezi az újonnan befogadott számlákat, és frissíti az Excel-nyilvántartást.

**DECISION** – A feldolgozás kezdődátuma az első éles futás napja. A korábban befogadott számlákat nem tölti vissza.

**DECISION** – A program kizárólag az aktuális és az előző naptári évben kiállított számlákat kezeli. A fizetési figyelésre is ez vonatkozik.

**DECISION** – A lekérdezés a tárolt `InsDate` előtti nap kezdetétől indul, de nem mehet az indulási kezdődátum elé. Az aktuális lekérdezési időpontig tart, legfeljebb 35 napos szeletekben.

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

**DECISION** – A számla csak akkor kiegyenlített, ha a mező értéke pontosan `FIZETVE`.

**DECISION** – Az üres mező kiegyenlítetlen számlát jelent. Más érték hibás, és a számlát továbbra is kiegyenlítetlenként kezeljük; a hibát az értesítőben jelezzük.

A kiegyenlítés automatikus megállapítása és a részkiegyenlítések kezelése a hatókörön kívül van: `project.md`.

## Fizetési határidők

**DECISION** – A határidő-ellenőrzés és a Slack-összesítő hétfőtől péntekig, magyarországi idő szerint fut. Ünnepnapnaptárat nem használunk.

**DECISION** – Az előértesítési napokat a felhasználó állítja be, például `7,3,1`. Ezek naptári napokat jelentenek.

**DECISION** – A hétvégére eső figyelmeztetés a következő hétfőn kerül kiküldésre. A számla tényleges fizetési határidejét nem módosítjuk.

**DECISION** – A határidő napjától a kiegyenlítetlen számla minden hétfőtől péntekig küldött összesítőben szerepel, amíg `FIZETVE` nem lesz, kivéve a sztornóval érintett számlaláncokat.

**DECISION** – Egy számla egy összesítőben csak egyszer szerepelhet.

## Slack-értesítések

**DECISION** – Az OsaPro értesítési futásonként egy Slack-összesítőt küld az ügyfél kijelölt csatornájára, akkor is, ha nincs aktuális teendő.

**DECISION** – Az összesítő tartalma:

- Esedékes és lejárt fizetési tételek, valamint előértesítések.
- Új `MODIFY` és `STORNO` bizonylatok egyszeri jelzése.
- Külön figyelmet igénylő esetek és tartós technikai hibák.

**DECISION** – Az egyszeri értesítések küldési állapotát Azure Table Storage tárolja. Sikertelen küldés után az értesítés később újrapróbálható. A fizetési figyelmeztetések minden futáskor az Excel alapján készülnek.

**DECISION** – A Slack-értesítésben megjeleníthető személyes adatok köre: `architecture.md`.

## Hibák és rendkívüli esetek

**DECISION** – A kezelt éveken kívüli számlák, az ismeretlen eredeti számlához tartozó `MODIFY`/`STORNO` és a tartós NAV- vagy Excel-elérési hibák Slack-jelzést kapnak. A kezelt éveken kívüli számlák felvétele a hatókörön kívül van: `project.md`.

**DECISION** – A programhibákat naplózzuk és jelezzük a Slack-összesítőben.

**DECISION** – A sikertelenül kiküldött egyszeri értesítések megmaradnak újrapróbálásra.

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
| Egyszeri értesítések küldési állapota | OsaPro |

## Nyitott kérdések

Ezek nem döntések.

**OPEN QUESTION** – Küldjön-e az OsaPro figyelmeztetést, ha a `Számla megvan?` mező üres?

**OPEN QUESTION** – A Slack-üzenet végleges mezői, szövegezése és célcsatornája később pontosítandó. A megjeleníthető személyes adatok köre ettől függetlenül döntött: `architecture.md`.

**OPEN QUESTION** – A Slack elérési hibájáról milyen módon történjen értesítés?

**OPEN QUESTION** – A pontos Excel-oszlop listát és a sablont a részletes tervezésben rögzítjük.
