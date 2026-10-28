# OsaPro roadmap

A jelölések jelentése: `project.md`.

**DECISION** – Ez tervezett change-lista, nem OpenSpec change-regiszter. A change-ek részletes követelményeit, technikai megoldásait és feladatait az egyes OpenSpec ciklusokban dolgozzuk ki. A lista és a függőségek a részletes tervezés során, emberi jóváhagyással módosíthatók.

**DECISION** – A fejlesztés az alábbi change-ekben történik:

| # | Change | Rövid cél | Sorrend / függőség |
|---|---|---|---|
| 1 | `setup-application-foundation` | Alkalmazásalapok, konfiguráció, naplózás és közös hibakezelési alapelvek kialakítása. | Első |
| 2 | `integrate-nav-invoices` | NAV-bizonylatok lekérdezése az indulási kezdődátum és az `InsDate` szabályai szerint. | 1 után |
| 3 | `manage-excel-workbooks` | SharePoint Excel-fájlok és munkalapok kezelése, sablonalapú létrehozás, a paraméterek átvétele évváltáskor. | 1 után |
| 4 | `provision-application-infrastructure` | Az OsaPro és a közös Slack-küldő Azure-erőforrásainak létrehozása, ellenőrzése és lebontása. | 2, 3 után |
| 5 | `process-inbound-invoices` | Új bizonylatok rögzítése, duplikációk elkerülése, bizonylatkapcsolatok és az `InsDate` frissítése. | 4 után |
| 6 | `compose-notification-content` | `Payment`, `Document` és `Error` sorok írása a `notification` táblába. Slack-üzenet nincs. | 5 után |
| 7 | `build-shared-slack-sender` | Külön Slack-küldő Function készítése, több hívó alkalmazás, workspace és csatorna támogatásával. | 1 után |
| 8 | `integrate-slack-sender` | Hétköznapi összesítő az üres `NotificationTimestamp` sorokból, átadás a közös küldőnek, sikeres válasz után az időpont kitöltése. | 6, 7 után |
| 9 | `schedule-and-orchestrate` | Napi sorrend: számlafeldolgozás, utána értesítés-összeállítás. A Slack-összesítő hétköznap, a napi írás után. | 5, 8 után |

**DECISION** – A 2. és a 3. change az alkalmazásalapok elkészülte után egymástól függetlenül is fejleszthető. A 7. change, a közös Slack-küldő, szintén az alkalmazásalapok után indulhat. A hosztját a 4. change hozza létre.

Ebben a fázisban change nem jött létre.
