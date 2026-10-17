# OsaPro – Kickoff

A dokumentumban használt jelölések:

* **KNOWN** – Külső, igazolt tény vagy jelenlegi működés.
* **DECISION** – Elfogadott döntés, amely tudatos módosítással változtatható.
* **ASSUMPTION** – Még nem igazolt feltételezés.
* **OPEN QUESTION** – Eldöntendő kérdés.

## 1. Projekt célja

### 1.1. Cél

**KNOWN** – Az OsaPro célja a beérkező számlák manuális rögzítésének csökkentése és a fizetési határidők automatikus figyelése.

**KNOWN** – A meglévő, SharePointon tárolt Excel-nyilvántartás megmarad.

**DECISION** – Az OsaPro a NAV Online Számla (OSA) adatai alapján felveszi az új számlákat, és Slack-értesítéseket küld.

### 1.2. Kiinduló helyzet

**KNOWN** – A számlákat éves Excel-fájlokban, havi munkalapokon tartják nyilván.

**KNOWN** – A számlák kiegyenlítését a felhasználó jelöli az Excelben.

**KNOWN** – A számlaadatok rögzítése és a fizetési határidők követése jelenleg manuális munkát igényel.

### 1.3. Fő döntések

**DECISION** – Az OsaPro a NAV által befogadott számlákat a befogadás időpontja (`insDate`) alapján kérdezi le.

**DECISION** – A feldolgozás kezdődátuma az első éles futás napja. A korábban befogadott számlákat nem tölti vissza.

**DECISION** – A program kizárólag az aktuális és az előző naptári évben kiállított számlákat kezeli. A fizetési figyelésre is ez vonatkozik.

**DECISION** – A megfelelő Excel-fájlt és havi munkalapot **a számla kelte** határozza meg.

**DECISION** – A meglévő számlasorok adatait az OsaPro nem írja felül, kivéve az eredeti számla `Kapcsolt bizonylatok` mezőjének bővítését. A számlaadatokat a felhasználó a kézhez kapott bizonylat alapján ellenőrzi és javítja.

### 1.4. Hatókör és kizárások

**DECISION** – A feladat része:

* Új beérkező számlák, módosító és sztornóbizonylatok felvétele az Excelbe.
* Az éves Excel-fájlok és havi munkalapok létrehozása sablon alapján.
* A fizetési határidők követése és a Slack-összesítők küldése.
* A feldolgozási hibák és rendkívüli esetek jelzése.

**DECISION** – Nem része a feladatnak:

* Az `ANNUL` művelet felismerése és kezelése.
* A számlabizonylatok beszerzése, tárolása és automatikus egyeztetése.
* A kiegyenlítés automatikus megállapítása és a részkiegyenlítések kezelése.
* A feldolgozás kezdődátuma előtti számlák visszatöltése.

**DECISION** – A kezelt éveken kívüli keltezésű számlákat az OsaPro nem veszi fel az Excelbe; ezekről a felhasználó Slack-értesítést kap.

### 1.5. Sikerkritériumok

**DECISION** – A projekt sikerkritériumai:

* Kevesebb időt igényel a számlák kézi rögzítése.
* Csökken a nyilvántartásból kimaradó új számlák kockázata.
* A fizetési határidőkről a felhasználó rendszeres értesítést kap.
* Az ismételt feldolgozás nem hoz létre duplikált számlasort, és nem írja felül a felhasználó módosításait.

**DECISION** – A kiinduló kézi munkaidőt és a kimaradt számlák becsült számát az ügyféllel rögzítjük, ha rendelkezésre állnak ezek az adatok.

---

## 2. Üzleti folyamat

### 2.1. NAV-számlák feldolgozása

#### 2.1.1. Nyilvántartásba vétel

**DECISION** – Az OsaPro konfigurációs paraméterben megadott CRON-ütemezés szerint lekérdezi az újonnan befogadott számlákat, és frissíti az Excel-nyilvántartást.

**DECISION** – A program az éves Excel-fájl `Paraméterek` munkalapjának `InsDate` mezőjében tárolja, meddig jutott a feldolgozásban. Ez nem a NAV-bizonylat `insDate` mezője.

**DECISION** – A lekérdezés a tárolt `InsDate` előtti nap kezdetétől indul, de nem mehet az indulási kezdődátum elé. Az aktuális lekérdezési időpontig tart, legfeljebb 35 napos szeletekben.

**DECISION** – Egy szelet sikeres feldolgozása után a program az `InsDate` értékét a szelet végére állítja, akkor is, ha nem érkezett számla. Hibás vagy részleges feldolgozás esetén nem léphet túl a feldolgozatlan időszakon.

**DECISION** – Az új éves Excel-fájl létrehozásakor a `Paraméterek` munkalap értékeit az előző éves fájlból vesszük át, az `InsDate` értékével együtt. Az évváltás miatt nem maradhat ki számla.

**DECISION** – Az új számla a megfelelő havi munkalap utolsó kitöltött sora után kerül.

**DECISION** – A számlát az eladó adószáma és a számlaszám együtt azonosítja. Ha ez a számla már szerepel a nyilvántartásban, nem jön létre új sor, és a számlaadatok nem módosulnak.

**DECISION** – A bizonylatokhoz nem tárolunk külön feldolgozási állapotot.

**DECISION** – A felhasználó szabadon rendezheti, szűrheti a munkalapot és szúrhat be sorokat. A feldolgozás nem támaszkodhat rögzített sorszámokra.

**DECISION** – A NAV-ban opcionális adatok az OsaPro számára is opcionálisak. Ha a fizetési határidő hiányzik, a számla keltét használjuk határidőként.

#### 2.1.2. Bizonylattípusok és kapcsolatok

**DECISION** – A `CREATE`, `MODIFY` és `STORNO` bizonylatok önálló Excel-sorba kerülnek, a saját kiállítási dátumuk szerint.

**DECISION** – A módosító és sztornóbizonylat kapcsolódik az eredeti számlához; a kapcsolatot az `Eredeti számla` mező jelzi.

**DECISION** – Az eredeti számla `Kapcsolt bizonylatok` mezője az összes hozzá tartozó módosító és sztornóbizonylatot tartalmazza, ismétlés nélkül.

**DECISION** – A rögzített bizonylatokat nem töröljük és nem helyezzük át.

**DECISION** – A MODIFY saját fizetési határidővel és saját `Fizetve?` mezővel rendelkezik.

#### 2.1.3. A számla rendelkezésre állása

**DECISION** – A nyilvántartásban szerepel egy felhasználó által kezelt `Számla megvan?` oszlop:

* Üres: a számlabizonylat rendelkezésre állását a felhasználó még nem igazolta.
* `IGEN`: a számla papíron vagy elektronikusan a felhasználó rendelkezésére áll.

**DECISION** – A `Számla megvan?` mező nem befolyásolja a fizetési figyelmeztetéseket. A számla ellenőrzése és a fizetésről szóló döntés a felhasználó felelőssége.

**OPEN QUESTION** – Küldjön-e az OsaPro figyelmeztetést, ha a `Számla megvan?` mező üres?

### 2.2. Kiegyenlítettség

**DECISION** – A `Fizetve?` mezőt kizárólag a felhasználó módosíthatja.

**DECISION** – A számla csak akkor kiegyenlített, ha a mező értéke pontosan `FIZETVE`.

**DECISION** – Az üres mező kiegyenlítetlen számlát jelent. Más érték hibás, és a számlát továbbra is kiegyenlítetlenként kezeljük; a hibát az értesítőben jelezzük.

### 2.3. Fizetési határidők követése

**DECISION** – A határidő-ellenőrzés és a Slack-összesítő hétfőtől péntekig, magyarországi idő szerint fut. Ünnepnapnaptárat nem használunk.

**DECISION** – Az előértesítési napokat a felhasználó állítja be, például `7,3,1`. Ezek naptári napokat jelentenek.

**DECISION** – A hétvégére eső figyelmeztetés a következő hétfőn kerül kiküldésre. A számla tényleges fizetési határidejét nem módosítjuk.

**DECISION** – A határidő napjától a kiegyenlítetlen számla minden hétfőtől péntekig küldött összesítőben szerepel, amíg `FIZETVE` nem lesz, kivéve a sztornóval érintett számlaláncokat.

**DECISION** – Egy számla egy összesítőben csak egyszer szerepelhet.

**DECISION** – A sztornóval érintett számlaláncokat a bizonylatok kapcsolata alapján határozzuk meg, külön státuszmező nélkül.

**DECISION** – Ha egy számlalánchoz sztornóbizonylat tartozik, a lánc egyik bizonylatáról sem küldünk fizetési figyelmeztetést vagy előértesítést. A sztornóbizonylatról szóló egyszeri értesítést elküldjük.

### 2.4. Slack-értesítések

**DECISION** – Az OsaPro értesítési futásonként egy Slack-összesítőt küld az ügyfél kijelölt csatornájára, akkor is, ha nincs aktuális teendő.

**DECISION** – Az összesítő tartalma:

* Esedékes és lejárt fizetési tételek, valamint előértesítések.
* Új MODIFY és STORNO bizonylatok egyszeri jelzése.
* Külön figyelmet igénylő esetek és tartós technikai hibák.

**DECISION** – Az egyszeri értesítések küldési állapotát Azure Table Storage tárolja. Sikertelen küldés után az értesítés később újrapróbálható. A fizetési figyelmeztetések minden futáskor az Excel alapján készülnek.

**OPEN QUESTION** – A Slack-üzenet végleges mezői, szövegezése és célcsatornája később pontosítandó.

### 2.5. Hibák és rendkívüli esetek

**DECISION** – A kezelt éveken kívüli számlák, az ismeretlen eredeti számlához tartozó MODIFY/STORNO és a tartós NAV- vagy Excel-elérési hibák Slack-jelzést kapnak.

**DECISION** – A programhibákat naplózzuk és jelezzük a Slack-összesítőben.

**DECISION** – A sikertelenül kiküldött egyszeri értesítések megmaradnak újrapróbálásra.

**OPEN QUESTION** – A Slack elérési hibájáról milyen módon történjen értesítés?

**DECISION** – Nem vezetünk Excel-alapú hibanyilvántartást vagy `Hibák` oszlopot.

### 2.6. Indulási átmeneti helyzet

**DECISION** – A kezdődátum előtti eredeti számlához később érkező MODIFY vagy STORNO önálló sorba kerül, ha a bizonylat kelte a kezelt időszakba esik. A hiányzó eredeti számláról a felhasználó Slack-jelzést kap.

---

## 3. A rendszer képességei

**DECISION** – Az OsaPro az alábbi fő képességeket biztosítja:

| Képesség | Eredmény |
|---|---|
| Automatikus számlafeldolgozás | Az új NAV-bizonylatok felhasználói beavatkozás nélkül bekerülnek a megfelelő Excel-nyilvántartásba. |
| Bizonylatkapcsolatok kezelése | Az eredeti, módosító és sztornóbizonylatok kapcsolata követhető az Excelben. |
| Meglévő nyilvántartás használata | A felhasználó továbbra is a megszokott Excelben dolgozhat, módosításait a rendszer megőrzi. |
| Fizetési teendők követése | A felhasználó rendszeres, összesített tájékoztatást kap az aktuális fizetési kötelezettségekről. |
| Rendkívüli esetek jelzése | A felhasználó értesül a külön figyelmet igénylő bizonylatokról és feldolgozási problémákról. |
| Automatikus működés és helyreállás | A rendszer ütemezetten működik, az átmeneti hibákat kezeli, és a megszakadt feldolgozást folytatni tudja. |

---

## 4. Rendszerhatárok és felelősségek

### 4.1. Rendszerek

**DECISION** – Az OsaPro az alábbi rendszereket használja:

|Rendszer|Szerepe|
|-|-|
|NAV Online Számla (OSA)|A beérkező számlák, módosító és sztornóbizonylatok adatforrása.|
|SharePointon tárolt Excel|Éves fájlok: számlanyilvántartás a havi munkalapokon, felhasználói beállítások és `InsDate` a `Paraméterek` munkalapon.|
|Közös Slack-küldő|Az OsaPro által összeállított üzenetek elküldése Slackra. Más alkalmazások is használhatják.|
|Slack|A felhasználó értesítése egy összesített üzenetben.|
|Azure Table Storage|Az egyszeri értesítések küldési állapotának tárolása.|

### 4.2. Adatok módosítása

**DECISION** – Az adatok módosításának felelőssége:

|Adat|Ki kezeli?|
|-|-|
|Új számlasor és a kezdeti NAV-adatok|OsaPro|
|Eredeti számla `Kapcsolt bizonylatok` mezője|OsaPro|
|Meglévő számlasor adatainak javítása|Felhasználó, a tényleges számlabizonylat alapján|
|`Fizetve?`|Felhasználó|
|`Számla megvan?`|Felhasználó|
|Előértesítési paraméterek|Felhasználó|
|Egyszeri értesítések küldési állapota|OsaPro|

**OPEN QUESTION** – A pontos Excel-oszloplistát és a sablont a részletes tervezésben rögzítjük.

---

## 5. Nem funkcionális követelmények

### 5.1. Megbízható feldolgozás

**DECISION** – Az átmeneti kommunikációs hibák után a rendszer újrapróbálkozik.

**DECISION** – Egy hibás tétel nem akadályozhatja meg a többi feldolgozását.

**DECISION** – Megszakítás után a feldolgozás folytatható legyen adatvesztés és duplikált számlasorok nélkül.

**DECISION** – Az Excel egyidejű felhasználói használata megengedett; az esetleges írási ütközést a rendszer későbbi futásban kezeli.

### 5.2. Naplózás és konfiguráció

**DECISION** – A rendszer naplózza a futásokat és a hibákat.

**DECISION** – Nincs külön, szerződésben vállalt átfutási idő a NAV-ból az Excelbe kerülésre.

**DECISION** – Az érzékeny konfigurációs értékek nem kerülhetnek a forráskódba. Tárolásuk módját a 7.4. fejezet rögzíti.

### 5.3. Adatvédelem / GDPR

**KNOWN** – Az OsaPro személyes adatokat is kezelhet, különösen egyéni vállalkozók számlaadatainak feldolgozásakor (GDPR 4. cikk 1. pont).

**KNOWN** – A számlák számviteli célú kezelésének jogalapja jogi kötelezettség. A fizetési határidők követése és az értesítések jogos érdeken alapulhatnak, megfelelő érdekmérlegelés mellett. Ezekhez nem szükséges külön érintetti hozzájárulás (GDPR 6. cikk (1) c), f); 2000. évi C. törvény 169. §).

**KNOWN** – Az ügyfélnek biztosítania kell az érintettek adatkezelési tájékoztatását, és a rendszeres adatkezelésről egyszerű nyilvántartást kell vezetnie (GDPR 13–14., 30. cikk).

**KNOWN** – A Nexande nem kezel személyes adatokat az ügyfél nevében, ezért az OsaPro kapcsán nem szükséges külön adatfeldolgozói megállapodás vele (GDPR 4. cikk 8. pont, 28. cikk).

**DECISION** – Csak a működéshez szükséges személyes adatokat kezeljük, korlátozott hozzáféréssel (GDPR 5. cikk (1) c), 32. cikk).

**DECISION** – A Slack-értesítés tartalmazhatja az eladó nevét, adószámát, a számlaszámot, az összeget és a fizetési határidőt.

---

## 6. Kritikus területek

### 6.1. NAV-integráció

**DECISION** – A számlák hiánytalan feldolgozása, duplikációk és adatvesztés nélkül, megszakítás vagy hiba esetén is.

### 6.2. Excel-nyilvántartás

**DECISION** – A felhasználói módosítások megőrzése, a helyes adatrögzítés és az egyidejű szerkesztés kezelése.

### 6.3. Bizonylatkapcsolatok

**DECISION** – A MODIFY és STORNO bizonylatok helyes összekapcsolása az eredeti számlával, a fizetési figyelmeztetésekre gyakorolt hatás figyelembevételével.

### 6.4. Határidők és értesítések

**DECISION** – Pontos fizetési figyelmeztetések és megbízható értesítésküldés, az átmeneti hibák kezelése mellett.

---

## 7. Architekturális döntések

### 7.1. Tech stack

**DECISION** – Az OsaPro és a közös Slack-küldő C# / .NET 10 alapú, Azure Functions isolated worker környezetben fut.

**DECISION** – A hosting Azure Functions Flex Consumption, igény szerinti futtatással, folyamatosan fenntartott példány nélkül. A választás célja az alacsony üzemeltetési költség.

**DECISION** – Az adattároláshoz SharePointon tárolt Excel-fájlokat és Azure Table Storage-ot használunk:

| Tároló | Tárolt adatok |
|---|---|
| Az éves Excel-fájlok havi munkalapjai | Számlaadatok és a felhasználó által kezelt mezők. |
| Az éves Excel-fájlok `Paraméterek` munkalapja | Felhasználói beállítások és a lekérdezés folytatási pontja (`InsDate`). |
| Azure Table Storage | Az egyszeri értesítések küldési állapota. |

**ASSUMPTION** – A feldolgozási futások időtartama belefér a választott hostingcsomag korlátaiba. Ezt a fejlesztés során ellenőrizzük.

### 7.2. Architektúra / komponensek

**DECISION** – A rendszer három Azure Functionből áll, két Function Appban:

| Function App | Azure Function | Feladat |
|---|---|---|
| OsaPro | Számlafeldolgozás | NAV-számlák lekérdezése, Excelbe rögzítése és a bizonylatkapcsolatok kezelése. |
| OsaPro | Értesítések összeállítása | Fizetési teendők, bizonylatértesítések és hibajelzések összesítése, majd átadása a Slack-küldőnek. |
| Közös Slack-küldő | Slack-üzenetek küldése | A kapott üzenet elküldése a megadott célra és a küldés eredményének visszajelzése. |

**DECISION** – Az OsaPro két Functionje külön ütemezés szerint fut.

**DECISION** – A Slack-küldő külön Azure Function, saját Function Appban. Az OsaPro mellett más alkalmazások is használhatják, több Slack-workspace és csatorna elérésére.

**DECISION** – Az értesítés tartalmát a hívó alkalmazás határozza meg. A közös küldő kezeli a Slack-hozzáférést, és ellenőrzi, hogy a hívó alkalmazás küldhet-e a megadott célra.

### 7.3. Adatáramlás

**DECISION** – A számlaadatok útja: NAV Online Számla → OsaPro → SharePoint Excel.

**DECISION** – Az értesítések útja: SharePoint Excel és az egyszeri értesítések tárolója → OsaPro → közös Slack-küldő Function → Slack.

### 7.4. Integrációk

**DECISION** – A rendszer az alábbi kapcsolatokat használja:

| Kapcsolat | Megoldás |
|---|---|
| OsaPro → NAV Online Számla | Online Számla API v3.0 |
| OsaPro → SharePoint Excel | Microsoft Graph API |
| OsaPro → Azure Table Storage | Azure Storage SDK |
| OsaPro → közös Slack-küldő Function | Hitelesített szolgáltatáshívás |
| Közös Slack-küldő Function → Slack | Slack API |

**DECISION** – Az Excel olvasásának, írásának és az egyidejű szerkesztés kezelésének módját a részletes technikai tervezésben határozzuk meg.

**DECISION** – A Slack-küldő kezeli a workspace-ekhez tartozó hozzáférési adatokat és az alkalmazások számára engedélyezett célokat. A hívó alkalmazásoknak nem kell Slack-hitelesítő adatot átadniuk.

**DECISION** – A szolgáltatáshívás, az újrapróbálás és a küldési állapotok kezelésének részleteit a technikai tervezésben határozzuk meg.

**DECISION** – A titkos konfigurációs értékeket helyben .NET User Secrets, Azure-ban Function App Settings vagy környezeti változók tárolják. Titok nem kerülhet verziókezelt fájlba.

### 7.5. Projekt-konvenciók

**DECISION** – A fejlesztés az `ai-fejlesztesi-folyamat.md` szerinti OpenSpec + grill-me módszertant követi, a projektben rögzített kiegészítésekkel és emberi jóváhagyásokkal.

**DECISION** – Az üzleti logikát elkülönítjük a külső rendszerek elérésétől, hogy az üzleti szabályok külső kapcsolatok nélkül is tesztelhetők legyenek.

**DECISION** – A fejlesztett működéshez automatizált tesztek készülnek. A 6. fejezet kritikus területein talált Verify-probléma csak javítás vagy írásos emberi kockázatvállalás után fogadható el.

**DECISION** – A projektdokumentáció és az OpenSpec-dokumentumok magyarul készülnek.

**DECISION** – Az ellenőrzéseket és a változások jóváhagyását egyelőre helyben végezzük, távoli push nélkül. A részletes Git-munkarendet az `openspec/project-conventions.md` tartalmazza.

---

## 8. OpenSpec change-ek

**DECISION** – A fejlesztés az alábbi OpenSpec change-ekben történik:

| # | Change | Rövid cél | Sorrend / függőség |
|---|---|---|---|
| 1 | `setup-application-foundation` | Alkalmazásalapok, konfiguráció, naplózás és közös hibakezelési alapelvek kialakítása. | Első |
| 2 | `integrate-nav-invoices` | NAV-bizonylatok lekérdezése az indulási kezdődátum és az `InsDate` szabályai szerint. | 1 után |
| 3 | `manage-excel-workbooks` | SharePoint Excel-fájlok és munkalapok kezelése, sablonalapú létrehozás, a paraméterek átvétele évváltáskor. | 1 után |
| 4 | `process-inbound-invoices` | Új bizonylatok rögzítése, duplikációk elkerülése, bizonylatkapcsolatok és az `InsDate` frissítése. | 2, 3 után |
| 5 | `compose-notification-content` | Fizetési teendők, bizonylatértesítések és hibajelzések összesítése; a küldésre váró egyszeri értesítések tárolása. | 4 után |
| 6 | `build-shared-slack-sender` | Külön Slack-küldő Function készítése, több hívó alkalmazás, workspace és csatorna támogatásával. | 1 után |
| 7 | `integrate-slack-sender` | Az OsaPro csatlakoztatása a közös küldőhöz, a küldési eredmény és az egyszeri értesítések küldési állapotának kezelése. | 5, 6 után |
| 8 | `schedule-and-orchestrate` | A számlafeldolgozó és az értesítés-összeállító Function külön ütemezése, a teljes folyamat összekötése. | 4, 7 után |

**DECISION** – A 2., 3. és 6. change az alkalmazásalapok elkészülte után egymástól függetlenül is fejleszthető.

**DECISION** – A change-ek részletes követelményeit, technikai megoldásait és feladatait az egyes OpenSpec ciklusokban dolgozzuk ki.

**DECISION** – A change-lista és a függőségek a részletes tervezés során, emberi jóváhagyással módosíthatók.

---

## 9. Review

A kickoff végén az ügyfél és a fejlesztésért felelős személy átnézi a dokumentumot, és jóváhagyja az alábbiakat:

* a hatókör;
* az üzleti működés és szabályok;
* a rendszer képességei és határai;
* a fő technikai döntések;
* az OpenSpec change-ek és azok sorrendje.
