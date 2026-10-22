# Infrastruktúra használata

A parancsok a `infra/scripts` könyvtárból futnak. Kell hozzájuk a PowerShell 7, az Azure CLI és a Bicep. Előbb `az login`. A bemenet az `infra/input.example.json` másolata. A SharePoint-hely és a NAV-beállítások a Worker `appsettings.json` nem titkos értékei. Kitöltendő a `tenantId` és a `subscriptionId`. A `dev` vagy `prod` a parancs `-Environment` paramétere, nincs az `appsettings.json` fájlban. Más értéket a paraméter nem fogad el. Az előtag `osp`. A nevek a paraméterből képződnek.

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

A SharePoint-mappa neve a meglévő `FolderPath`, most `OsaPro.Test`. Nem a környezetből képződik. Meglévő leltárnál a paraméternek egyeznie kell a leltár környezetével.

A leltár útvonala a gépen marad. Nem kerül verziókezelésbe. Titokérték nincs benne, csak a tárolás helye, például `Secrets__ExcelClientId`.

## Telepítő joga

A bejelentkezett személy hozza létre az erőforráscsoportokat, a szerepköröket, az Excel-alkalmazásregisztrációt és a konfigurált mappát, és ő adja meg az admin consentet. Ez a jog nem kerül a futó alkalmazásra. A futó alkalmazás Graph-engedélye `Sites.Read.All` és `Files.ReadWrite.All`. A `Files.ReadWrite.All` a tenáns fájljaira szól. A kód csak a konfigurált mappát használja.

A NAV-titkot a szkript nem gyártja. A bemenet az operátor környezetváltozójának nevét tartalmazza. Ha a változó megvan, az értéke a Function App környezetbe kerül. Ha nincs meg, a létrehozás ettől még teljes lehet. Slack-token nem készül. Key Vault nincs.

## Létrehozás

```powershell
./Invoke-OsaProInfrastructure.ps1 -Action Create -Environment dev -InputPath input.json -InventoryPath inventory.json
```

A második futás a leltár azonosítóit használja. Nem készít második regisztrációt, engedélyt vagy Excel-titkot. Foglalt idegen névnél megáll. Részleges futás után a hiányzó saját lépés pótolható. A teljes siker csak akkor jelenik meg, ha minden kötelező lépés sikerült.

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

A SharePoint-hely, a dokumentumtár, a telepítő által létrehozott mappa, a dokumentumok és az Excel-fájlok egyik parancsban sem törlődnek. Más alkalmazás engedélye nem vonódik vissza. Eltérő azonosítónál törlés nem indul.
