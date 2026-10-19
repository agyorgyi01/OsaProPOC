# OsaPro projektkonvenciók

A jelölések jelentése: `project.md`.

## Munkamód

**DECISION** – A fejlesztés a `docs/ai-fejlesztesi-folyamat.md` szerinti OpenSpec + grill-me módszertant követi, a dokumentum végén lévő OsaPro-kiegészítéssel és emberi jóváhagyásokkal.

**DECISION** – A projektdokumentáció és az OpenSpec-dokumentumok magyarul készülnek.

**DECISION** – Az üzleti logikát elkülönítjük a külső rendszerek elérésétől, hogy az üzleti szabályok külső kapcsolatok nélkül is tesztelhetők legyenek.

**DECISION** – A fejlesztett működéshez automatizált tesztek készülnek. A lenti kritikus területeken talált Verify-probléma csak javítás vagy írásos emberi kockázatvállalás után fogadható el.

A 0. fázis ember által vezetett projektindítás. Nem feature change. A projektkeret nem helyettesíti az `openspec/specs/` alatti specifikációt, sem egy change proposal, delta spec, design és tasks dokumentumait.

A `roadmap.md` tervezett change-lista, nem OpenSpec change-regiszter.

A módszer szerint egy change proposaljában külön meg kell mondani, hogy érinti-e valamelyik kritikus területet, és melyiket.

## Kritikus területek

**DECISION** – Az alábbi területek kritikusak:

| Terület | Miért kritikus |
|---|---|
| NAV-integráció | A számlák hiánytalan feldolgozása, duplikációk és adatvesztés nélkül, megszakítás vagy hiba esetén is. |
| Excel-nyilvántartás | A felhasználói módosítások megőrzése, a helyes adatrögzítés és az egyidejű szerkesztés kezelése. |
| Bizonylatkapcsolatok | A `MODIFY` és `STORNO` bizonylatok helyes összekapcsolása az eredeti számlával, a fizetési figyelmeztetésekre gyakorolt hatás figyelembevételével. |
| Határidők és értesítések | Pontos fizetési figyelmeztetések és megbízható értesítésküldés, az átmeneti hibák kezelése mellett. |

A lista módosítása emberi döntés.

## Jóváhagyás

**DECISION** – Egyszemélyes munkánál a grill-me és az emberi jóváhagyások megmaradnak. Kötelező pihenőidőt és külső LLM-másodvéleményt nem alkalmazunk.

A 0. fázis addig nem zárul le, amíg a stakeholder/ügyfél és az architect explicit jóvá nem hagyja az architektúrát, a tech stacket és a feature-listát. A kickoff jóváhagyása ezt a projektkeretet nem hagyja jóvá.

## Helyi Git-munkarend

**DECISION** – Az ellenőrzéseket és a változások jóváhagyását egyelőre helyben végezzük, távoli push nélkül. Az ellenőrzött változás helyben a fő ágba kerül.

**DECISION** – A távoli CI- és PR-ellenőrzéseket egyelőre helyben végezzük.

**DECISION** – Minden roadmap-change három branchen megy végig. Ez a rend a 0. fázis dokumentációjára nem vonatkozik.

| Branch | Feladata |
|---|---|
| `plan/<change-név>` | Tervezés, grill-me és tervjóváhagyás. |
| `impl/<change-név>` | Implementáció és tesztelés. |
| `verify/<change-név>` | Verify, szükséges javítások és emberi code review. |

**DECISION** – Az `impl/<change-név>` branch a lezárt `plan/<change-név>` branch utolsó commitjából indul. A `verify/<change-név>` branch a lezárt `impl/<change-név>` branch utolsó commitjából indul. A lezárt állapot a fázis befejezése, nem az OpenSpec change archiválása.

**DECISION** – Minden fázis végén commit készül:

- Tervezés: a grill-me lezárása és az explicit emberi tervjóváhagyás után, az implementációs branch létrehozása előtt.
- Implementáció: a feladatok elkészülte és a szükséges tesztek sikeres futása után, az ellenőrzési branch létrehozása előtt.
- Ellenőrzés: a Verify megállapításainak rendezése és az emberi code review jóváhagyása után, a fő ágba emelés előtt.

**DECISION** – Commit előtt áttekintjük a változásokat, és lefuttatjuk az adott fázishoz szükséges ellenőrzéseket. Egyelőre nincs commit előtti hook. Az `openspec validate --all --strict` parancsot commit előtt kézzel futtatjuk; a validálás kötelező. A többi szükséges ellenőrzés és emberi jóváhagyás megmarad.

**DECISION** – A commit vagy a branchváltás önmagában nem emberi jóváhagyás.

**DECISION** – Az OpenSpec-archiválás a fő ágba emelés után, a módszertan szerint történik. A delta-spec szinkronhoz külön explicit emberi jóváhagyás kell.

**DECISION** – Ha a Verify spec-hiányt tár fel, a módszertan szerinti visszalépés és emberi döntés szükséges. A javítás lehetősége nem jogosít fel a terv önálló átírására.
