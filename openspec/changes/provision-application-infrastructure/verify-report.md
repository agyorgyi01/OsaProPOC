# Verify: provision-application-infrastructure

2026-10-23, újragenerált ellenőrzés a visszaírás után. Branch: `verify/provision-application-infrastructure`, a `74ff7b9` impl commitból. A javítások a munkafában vannak, nincsenek commitolva.
Kritikus komponens: igen, Excel-nyilvántartás és az értesítések tárolója. Push, spec-szinkron, archiválás és főágra emelés nem történt.

Using change: `provision-application-infrastructure`. Másik change: `/opsx-verify <név>`.

`openspec validate --all --strict`: 5 sikeres, 0 sikertelen. `powershell.exe -NoProfile -File .\infra\tests\Infrastructure.Tests.ps1`: minden infrastruktúra-teszt sikeres, a Bicep build is. Create, Destroy és a script Verify művelete nem futott. Titokérték nem jelent meg.

## Summary

| Dimension | Status |
|---|---|
| Completeness | 5/5 tasks, 24 ADDED requirements. MODIFIED, REMOVED és RENAMED nincs. |
| Correctness | 24/24 requirements match, 39/39 scenarios covered |
| Coherence | Followed |

## CRITICAL

Nincs.

## WARNING

Nincs. Az ismételt létrehozás a telepítés után visszaírja a leltárban lévő Excel client azonosítót és client secretet a Function Appra (`infra/scripts/Provision.psm1:264`). Új titok nem készül. Ha a leltárban nincs meg az érték, a lépés megáll (`:268`). A `SetAppSettings` a meglévő hosztbeállításokat megtartja, és a két Excel-kulcsot hozzáírja (`infra/scripts/AzureGateway.psm1:440`).

## SUGGESTION

1. A PowerShell 7 belépés és a `-Confirm` kapcsoló nincs a tesztfutásban végrehajtva. A teszt Windows PowerShell 5.1 alatt importálja a modulokat. A belépő szkript 7 előtt megáll (`infra/scripts/Invoke-OsaProInfrastructure.ps1:37`), a `-Confirm` a modul `-Approved` kapcsolójára kerül (`:116`).
2. Az Excel client secret az alkalmazásbeállítás írása előtt keletkezik (`infra/scripts/Provision.psm1:248`). Ha az írás hibázik, a hely és az érték nem kerül a leltárba, és a következő futás új client secretet kér. A feljegyzett titok nem cserélődik.
3. A client secret Entra-felirata `osp-excel` (`infra/scripts/AzureGateway.psm1:428`). Az alkalmazás neve a `prefix` mezőt használja, ez a felirat nem.

## Lefedett működés

A bemenet `tenantId`, `subscriptionId`, `location` és `prefix`. A `dev` vagy `prod` a parancs paramétere. Érvénytelen előtag és hiányzó azonosító Azure-hívás előtt megáll. Az első Azure-lépés `az login` a megadott párral. A többi `az` hívás az előfizetést kapja. A tokenkérés csak a bérlőt kapja, és a token bérlője egyezik a bemenettel.

A sablon két Linux Flex Consumption Function Appot ír, .NET isolated `10.0`, always-ready nélkül, FC1 csomaggal, 2048 MB és legfeljebb 100 példány. Három storage-fiók készül. Az `ertesitesek` tábla az értesítési fiókon van. Blobjog a saját hoszton van mindkét identitynek. Táblajog csak az OsaPro identitynek van. A közös Application Insights és a 30 napos workspace a megosztott csoportban van. A kapcsolat nem kerül a gyökérkimenetre és a leltárba.

Az Excel-regisztráció `Sites.Read.All` és `Files.ReadWrite.All` alkalmazásengedélyt kap, admin consenttel. Az első futás a client azonosítót és a client secretet a Function App környezetébe írja, és mindkét értéket a leltár `secretLocations` mezőjében megőrzi (`infra/scripts/Provision.psm1:248`). Az ismételt futás ugyanezt a két értéket visszaírja, új titok nélkül (`:264`). Az Excel-bérlő azonosítóját a szkript nem írja. A napló az értéket nem írja ki. A `infra/dev_inventory.json` nincs verziókezelésben. NAV-titkot, tokent, SAS-t, storage-kulcsot és kapcsolatkarakterláncot a szkript nem tesz a leltárba. Nav- vagy Excel-beállítást a szkript nem másol.

A várt csoportban meglévő erőforrás nem készül újra. Idegen név megáll. Foglalt, de a megadott előfizetésben élő fiókként nem található storage-névnél a telepítés nem indul. A napló nem állítja, hogy a név az előfizetésen kívül foglalt (`infra/scripts/Provision.psm1:172`). Részleges futás folytatható. A lebontás előnézete megerősítés nélkül nem töröl. Eltérő azonosítónál törlés nincs. Az OsaPro megerősítése a saját csoportot, a hosztot, a két storage-ot, a regisztrációt és a service principalt törli. Az értesítési tábla a küldési állapottal együtt törlődik. A megosztott csoport kimarad. A SharePoint-hely, a mappa, a dokumentumok és az Excel-fájlok nem törlődnek. Más alkalmazás azonosítója nincs a törlési halmazban. A regisztráció törlésének 404-től eltérő hibája megáll (`infra/scripts/AzureGateway.psm1:491`).

## Ebben a futásban nincs új élő olvasás

Azure-lekérdezés nem futott. A korábbi, ugyanaznapi olvasás az `osapro-dev-rg` és az `osapro-dev-shared-rg` csoportot, a két Function Appot, a három storage-fiókot, a két csomagot, a workspace-t és az Application Insightst megtalálta. A Function App indulása, a szerepkör-hozzárendelés és az admin consent ebben a futásban nem volt újra ellenőrizve. A visszaírás a helyi tesztben igazolt, élő második Create nem futott.

## Final Assessment

No critical issues or warnings. 3 suggestions to consider. Ready for archive (with noted improvements).

Emberi code review jóváhagyás nincs. Archiválás, spec-szinkron, push és főágra emelés nem indul.
