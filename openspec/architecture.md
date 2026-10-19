# OsaPro architektúra

A jelölések jelentése: `project.md`. Az üzleti szabályok: `business-rules.md`. A kritikus területek: `project-conventions.md`.

## Tech stack

**DECISION** – Az OsaPro és a közös Slack-küldő C# / .NET 10 alapú, Azure Functions isolated worker környezetben fut.

**DECISION** – A hosting Azure Functions Flex Consumption, igény szerinti futtatással, folyamatosan fenntartott példány nélkül. A választás célja az alacsony üzemeltetési költség.

**DECISION** – Az adattároláshoz SharePointon tárolt Excel-fájlokat és Azure Table Storage-ot használunk.

| Tároló | Tárolt adatok |
|---|---|
| Az éves Excel-fájlok havi munkalapjai | Számlaadatok és a felhasználó által kezelt mezők. |
| Az éves Excel-fájlok `Paraméterek` munkalapja | Felhasználói beállítások és a lekérdezés folytatási pontja (`InsDate`). |
| Azure Table Storage | Az egyszeri értesítések küldési állapota. |

A folytatási pont frissítésének szabálya: `business-rules.md`.

## Komponensek

**DECISION** – A rendszer három Azure Functionből áll, két Function Appban:

| Function App | Azure Function | Feladat |
|---|---|---|
| OsaPro | Számlafeldolgozás | NAV-számlák lekérdezése, Excelbe rögzítése és a bizonylatkapcsolatok kezelése. |
| OsaPro | Értesítések összeállítása | Fizetési teendők, bizonylatértesítések és hibajelzések összesítése, majd átadása a Slack-küldőnek. |
| Közös Slack-küldő | Slack-üzenetek küldése | A kapott üzenet elküldése a megadott célra és a küldés eredményének visszajelzése. |

**DECISION** – Az OsaPro két Functionje külön ütemezés szerint fut.

**DECISION** – A Slack-küldő külön Azure Function, saját Function Appban. Az OsaPro mellett más alkalmazások is használhatják, több Slack-workspace és csatorna elérésére.

**DECISION** – Az értesítés tartalmát a hívó alkalmazás határozza meg. A közös küldő kezeli a Slack-hozzáférést, és ellenőrzi, hogy a hívó alkalmazás küldhet-e a megadott célra.

## Rendszerhatárok

**DECISION** – Az OsaPro az alábbi rendszereket használja:

| Rendszer | Szerepe |
|---|---|
| NAV Online Számla (OSA) | A beérkező számlák, módosító és sztornóbizonylatok adatforrása. |
| SharePointon tárolt Excel | Éves fájlok: számlanyilvántartás a havi munkalapokon, felhasználói beállítások és `InsDate` a `Paraméterek` munkalapon. |
| Közös Slack-küldő | Az OsaPro által összeállított üzenetek elküldése Slackra. Más alkalmazások is használhatják. |
| Slack | A felhasználó értesítése egy összesített üzenetben. |
| Azure Table Storage | Az egyszeri értesítések küldési állapotának tárolása. |

Az adatfelelősség: `business-rules.md`. A tárolt adatok bontása a Tech stack táblában van.

## Adatáramlás

**DECISION** – A számlaadatok útja: NAV Online Számla → OsaPro → SharePoint Excel.

**DECISION** – Az értesítések útja: SharePoint Excel és az egyszeri értesítések tárolója → OsaPro → közös Slack-küldő Function → Slack.

## Integrációk

**DECISION** – A rendszer az alábbi kapcsolatokat használja:

| Kapcsolat | Megoldás |
|---|---|
| OsaPro → NAV Online Számla | Online Számla API v3.0 |
| OsaPro → SharePoint Excel | Microsoft Graph API |
| OsaPro → Azure Table Storage | Azure Storage SDK |
| OsaPro → közös Slack-küldő Function | Hitelesített szolgáltatáshívás |
| Közös Slack-küldő Function → Slack | Slack API |

**DECISION** – A Slack-küldő kezeli a workspace-ekhez tartozó hozzáférési adatokat és az alkalmazások számára engedélyezett célokat. A hívó alkalmazásoknak nem kell Slack-hitelesítő adatot átadniuk.

**DECISION** – Az Excel olvasásának, írásának és az egyidejű szerkesztés kezelésének módját a részletes technikai tervezésben határozzuk meg.

**DECISION** – A szolgáltatáshívás, az újrapróbálás és a küldési állapotok kezelésének részleteit a technikai tervezésben határozzuk meg.

## Konfiguráció és titkok

**DECISION** – Az érzékeny konfigurációs értékek nem kerülhetnek a forráskódba.

**DECISION** – A titkos konfigurációs értékeket helyben .NET User Secrets, Azure-ban Function App Settings vagy környezeti változók tárolják. Titok nem kerülhet verziókezelt fájlba.

## Nem funkcionális követelmények

### Megbízható feldolgozás

**DECISION** – Az átmeneti kommunikációs hibák után a rendszer újrapróbálkozik.

**DECISION** – Egy hibás tétel nem akadályozhatja meg a többi feldolgozását.

**DECISION** – Megszakítás után a feldolgozás folytatható legyen adatvesztés és duplikált számlasorok nélkül.

**DECISION** – Az Excel egyidejű felhasználói használata megengedett; az esetleges írási ütközést a rendszer későbbi futásban kezeli.

**DECISION** – Nincs külön, szerződésben vállalt átfutási idő a NAV-ból az Excelbe kerülésre.

### Naplózás

**DECISION** – A rendszer naplózza a futásokat és a hibákat.

A programhibák Slack-jelzésének szabálya: `business-rules.md`.

### Adatvédelem

**KNOWN** – Az OsaPro személyes adatokat is kezelhet, különösen egyéni vállalkozók számlaadatainak feldolgozásakor (GDPR 4. cikk 1. pont).

Az adatkezelési jogalap, az ügyfél tájékoztatási és nyilvántartási kötelezettsége, valamint a Nexande szerepe: [Adatkezelési keretek és felelősségek](project.md#adatkezelési-keretek-és-felelősségek).

**DECISION** – Csak a működéshez szükséges személyes adatokat kezeljük, korlátozott hozzáféréssel (GDPR 5. cikk (1) c), 32. cikk).

**DECISION** – A Slack-értesítés tartalmazhatja az eladó nevét, adószámát, a számlaszámot, az összeget és a fizetési határidőt.

## Feltételezés

**ASSUMPTION** – A feldolgozási futások időtartama belefér a választott hostingcsomag korlátaiba. Ezt a fejlesztés során ellenőrizzük. Ez nem igazolt tény.
