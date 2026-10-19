# OsaPro projektkeret

A jelölések:

- **KNOWN** – külső, igazolt tény vagy jelenlegi működés.
- **DECISION** – elfogadott döntés, amely tudatos módosítással változtatható.
- **ASSUMPTION** – még nem igazolt feltételezés.
- **OPEN QUESTION** – eldöntendő kérdés.

A további change-ek ebből a projektkeretből indulnak. A `docs/OsaPro_Kickoff_v3.md` nem bemenet. Ez a keret nem kanonikus specifikáció: az az `openspec/specs/` alatt, az egyes change-ekből épül.

**Állapot:** a 0. fázis 2026. október 19-én lezárult. A projektkeret jóváhagyott: az architektúra és a tech stack, a feature-lista a change-ek sorrendjével és függőségeivel, az üzleti szabályok és a nem funkcionális követelmények, a projektkonvenciók, a kritikus területek és a hárombranch-es Git-munkarend. Ez a jóváhagyás későbbi change-tervre, implementációra vagy archive-syncre nem terjed ki.

## Cél

**KNOWN** – Az OsaPro célja a beérkező számlák manuális rögzítésének csökkentése és a fizetési határidők automatikus figyelése.

**KNOWN** – A meglévő, SharePointon tárolt Excel-nyilvántartás megmarad.

**DECISION** – Az OsaPro a NAV Online Számla (OSA) adatai alapján felveszi az új számlákat, és Slack-értesítéseket küld.

## Kiinduló helyzet

**KNOWN** – A számlákat éves Excel-fájlokban, havi munkalapokon tartják nyilván.

**KNOWN** – A számlák kiegyenlítését a felhasználó jelöli az Excelben.

**KNOWN** – A számlaadatok rögzítése és a fizetési határidők követése jelenleg manuális munkát igényel.

## Hatókör

**DECISION** – A feladat része:

- Új beérkező számlák, módosító és sztornóbizonylatok felvétele az Excelbe.
- Az éves Excel-fájlok és havi munkalapok létrehozása sablon alapján.
- A fizetési határidők követése és a Slack-összesítők küldése.
- A feldolgozási hibák és rendkívüli esetek jelzése.

A működés szabályai: `business-rules.md`. A technikai keret: `architecture.md`.

## Kizárások

**DECISION** – Nem része a feladatnak:

- Az `ANNUL` művelet felismerése és kezelése.
- A számlabizonylatok beszerzése, tárolása és automatikus egyeztetése.
- A kiegyenlítés automatikus megállapítása és a részkiegyenlítések kezelése.
- A feldolgozás kezdődátuma előtti számlák visszatöltése. A kezdődátum szabálya: `business-rules.md`.
- A kezelt éveken kívüli keltezésű számlák felvétele az Excelbe. Ezekről a felhasználó Slack-értesítést kap; a jelzés a hibaszabályok között is szerepel: `business-rules.md`.

## Sikerkritériumok

**DECISION** – A projekt sikerkritériumai:

- Kevesebb időt igényel a számlák kézi rögzítése.
- Csökken a nyilvántartásból kimaradó új számlák kockázata.
- A fizetési határidőkről a felhasználó rendszeres értesítést kap.
- Az ismételt feldolgozás nem hoz létre duplikált számlasort, és nem írja felül a felhasználó módosításait.

**DECISION** – A kiinduló kézi munkaidőt és a kimaradt számlák becsült számát az ügyféllel rögzítjük, ha rendelkezésre állnak ezek az adatok.

Ezek a számértékek nincsenek megadva.

## Képességek

**DECISION** – Az OsaPro az alábbi fő képességeket biztosítja:

| Képesség | Eredmény |
|---|---|
| Automatikus számlafeldolgozás | Az új NAV-bizonylatok felhasználói beavatkozás nélkül bekerülnek a megfelelő Excel-nyilvántartásba. |
| Bizonylatkapcsolatok kezelése | Az eredeti, módosító és sztornóbizonylatok kapcsolata követhető az Excelben. |
| Meglévő nyilvántartás használata | A felhasználó továbbra is a megszokott Excelben dolgozhat, módosításait a rendszer megőrzi. |
| Fizetési teendők követése | A felhasználó rendszeres, összesített tájékoztatást kap az aktuális fizetési kötelezettségekről. |
| Rendkívüli esetek jelzése | A felhasználó értesül a külön figyelmet igénylő bizonylatokról és feldolgozási problémákról. |
| Automatikus működés és helyreállás | A rendszer ütemezetten működik, az átmeneti hibákat kezeli, és a megszakadt feldolgozást folytatni tudja. |

A képességek change-ekre bontása: `roadmap.md`.

## Adatkezelési keretek és felelősségek

**KNOWN** – A számlák számviteli célú kezelésének jogalapja jogi kötelezettség. A fizetési határidők követése és az értesítések jogos érdeken alapulhatnak, megfelelő érdekmérlegelés mellett. Ezekhez nem szükséges külön érintetti hozzájárulás (GDPR 6. cikk (1) c), f); 2000. évi C. törvény 169. §).

**KNOWN** – Az ügyfélnek biztosítania kell az érintettek adatkezelési tájékoztatását, és a rendszeres adatkezelésről egyszerű nyilvántartást kell vezetnie (GDPR 13–14., 30. cikk).

**KNOWN** – A Nexande nem kezel személyes adatokat az ügyfél nevében, ezért az OsaPro kapcsán nem szükséges külön adatfeldolgozói megállapodás vele (GDPR 4. cikk 8. pont, 28. cikk).

## Dokumentumtérkép

| Dokumentum | Szerepe |
|---|---|
| `openspec/project.md` | Cél, hatókör, kizárások, sikerkritériumok, képességek, adatkezelési keretek. |
| `openspec/business-rules.md` | Üzleti működés és adatfelelősség. |
| `openspec/architecture.md` | Technikai keret, rendszerhatárok, adatáramlás, integrációk, nem funkcionális követelmények. |
| `openspec/project-conventions.md` | Munkamód, kritikus területek, ellenőrzés, jóváhagyás, helyi Git-munkarend. |
| `openspec/roadmap.md` | Tervezett change-célok, sorrend és függőségek. |
| `docs/ai-fejlesztesi-folyamat.md` | A fejlesztési módszer, az OsaPro-kiegészítéssel. |
| `openspec/specs/` | Kanonikus specifikáció. Change-ekből kerül ide. |
| `openspec/changes/` | Az egyes change-ek tervező dokumentumai. |
