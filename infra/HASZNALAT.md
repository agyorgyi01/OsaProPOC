# Infrastruktúra használata

A parancsok a `infra/scripts` könyvtárból futnak. Kell hozzájuk a PowerShell 7, az Azure CLI és a Bicep. A szkript a bemenet `tenantId` és `subscriptionId` értékével jelentkezik be, mielőtt más Azure-hívás indul. A bemenet az `infra/input.example.json` másolata: a `tenantId`, a `subscriptionId`, a régió és a `prefix`. A szkript csak ebben a bérlőben és előfizetésben dolgozik, és ezeket nem olvassa ki a bejelentkezett fiókból. Az Azure-hívás a megadott előfizetést kapja. A tokenkérés csak a megadott bérlőt kapja. A régió az Azure-erőforrások helye. SharePoint-mappát a szkript nem hoz létre. A `dev` vagy `prod` a parancs `-Environment` paramétere. Más értéket a paraméter nem fogad el. Az előtag a JSON `prefix` mezője: kisbetűvel kezdődik, csak kisbetű és szám, legfeljebb 13 karakter. A nevek ebből és a környezetből képződnek. A táblázat a `prefix` érték `osp` mellett érvényes.

| Szerep | `-Environment dev` | `-Environment prod` |
|---|---|---|
| OsaPro erőforráscsoport | `osp-dev-rg` | `osp-prod-rg` |
| Megosztott erőforráscsoport | `osp-dev-shared-rg` | `osp-prod-shared-rg` |
| OsaPro Function App | `osp-dev-func` | `osp-prod-func` |
| Slack Function App | `osp-dev-slack` | `osp-prod-slack` |
| Application Insights | `osp-dev-ai` | `osp-prod-ai` |
| Log Analytics workspace | `osp-dev-log` | `osp-prod-log` |
| Excel-alkalmazásregisztráció | `osp-dev-excel` | `osp-prod-excel` |
| OsaPro hoszt storage | `ospdevst` | `ospprodst` |
| Slack hoszt storage | `ospdevslackst` | `ospprodslackst` |
| Értesítési Table Storage | `ospdevtbl` | `ospprodtbl` |

Meglévő leltárnál a környezetnek és az előtagnak egyeznie kell a leltáréval.

A leltár útvonala a gépen marad. Nem kerül verziókezelésbe. Az Excel client azonosító és a client secret értéke benne van. A napló nem írja ki. Token, storage-kulcs, SAS és a kapcsolatkarakterlánc nincs benne.

## Telepítő joga

A bejelentkezett személy hozza létre az erőforráscsoportokat, a szerepköröket és az Excel-alkalmazásregisztrációt, és ő adja meg az admin consentet. Ez a jog nem kerül a futó alkalmazásra. A futó alkalmazás Graph-engedélye `Sites.Read.All` és `Files.ReadWrite.All`. A `Files.ReadWrite.All` a tenáns fájljaira szól. A kód csak a konfigurált mappát használja. A szkript SharePoint-webhelyet és mappát nem hoz létre, és nem töröl.

A szkript NAV-titkot és Slack-tokent nem gyárt, és a Nav- vagy Excel-beállításokat nem írja a Function Appra. Key Vault nincs.

## Létrehozás

```powershell
./Invoke-OsaProInfrastructure.ps1 -Action Create -Environment dev -InputPath input.json -InventoryPath inventory.json
```

A második futás a leltár azonosítóit használja. A megadott előfizetés várt erőforráscsoportjában már meglévő, képzett nevű erőforrást nem hozza létre újra. Nem készít második regisztrációt, engedélyt vagy Excel-titkot. A telepítés után a leltárban lévő Excel client azonosítót és client secretet visszaírja a Function Appra. Máshol foglalt névnél megáll. Olyan storage-névnél, amely foglalt, de a megadott előfizetésben élő fiókként nincs meg, a telepítés el sem indul. Részleges futás után a hiányzó saját lépés pótolható. A teljes siker csak akkor jelenik meg, ha minden kötelező lépés sikerült.

## Ellenőrzés

```powershell
./Invoke-OsaProInfrastructure.ps1 -Action Verify -Environment dev -InputPath input.json -InventoryPath inventory.json
```

Az ellenőrzés nem hoz létre és nem töröl erőforrást. Eltérő azonosítónál sikertelen.

## Lebontás

Előnézet, törlés nélkül:

```powershell
./Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target OsaPro -Environment dev -InputPath input.json -InventoryPath inventory.json
```

## Adatvesztés

Az OsaPro megerősítése törli a saját erőforráscsoportot, a hosztot, a hoszt storage-ot, az értesítési Table Storage-fiókot, a dedikált alkalmazásregisztrációt és a service principalt. A küldési állapot elvész. Külön megőrző kapcsoló nincs.

```powershell
./Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target OsaPro -Environment dev -Confirm -InputPath input.json -InventoryPath inventory.json
```

A megosztott megerősítés törli a Slack-hosztot, a saját storage-át, az Application Insightst, a workspace-t és a megosztott erőforráscsoportot. A telemetria elvész.

```powershell
./Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target Shared -Environment dev -Confirm -InputPath input.json -InventoryPath inventory.json
```

A SharePoint-hely, a dokumentumtár, a mappa, a dokumentumok és az Excel-fájlok egyik parancsban sem törlődnek. Más alkalmazás engedélye nem vonódik vissza. Eltérő azonosítónál törlés nem indul.
