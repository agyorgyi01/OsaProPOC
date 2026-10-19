# AI-támogatott fejlesztési folyamat (OpenSpec + grill-me)

**Cél:** Ez a dokumentum leírja, hogyan használjuk az OpenSpec-et és a grill-me skill-t a fejlesztési folyamatban úgy, hogy minden kritikus döntési ponton biztosított legyen az emberi kontroll (HITL — Human-in-the-Loop). Új csapattag ez alapján önállóan be tud kapcsolódni a folyamatba, akár új (greenfield), akár meglévő (brownfield) projektről van szó.

---

## Előfeltételek (egyszeri projekt-setup)

- `npm install -g @fission-ai/openspec` telepítve
- `openspec init` lefuttatva a repóban (létrehozza az `openspec/` mappát és az agent-specifikus instrukciókat)
- `grill-me` skill telepítve az AI coding agentbe: `npx skills add https://github.com/mattpocock/skills --skill grill-me`
- Git hook a `openspec validate --all --strict` automatikus futtatására commit előtt
- CI pipeline lépés, ami elutasítja a merge-et, ha a `tasks.md`-ben nincs minden task `[x]`-elve, vagy ha a change-hez nincs csatolva verify riport

**Hatókör:** a dokumentum alább részletezett 1–9. lépése egy **konkrét feature/change** végigviteléről szól — feltételezi, hogy a projekt architektúrája, tech stackje és egy priorizált feature-lista már létezik. Ha ez nincs meg, előbb a **Fázis 0**-t kell lefuttatni (lásd lent) — ennek két változata van attól függően, hogy új vagy meglévő projektről van szó.

---

## Fázis 0 — Projekt indítás

Ez a fázis egyszeri, és megelőzi az 1–9. lépéses per-feature ciklust. Célja, hogy létrehozza azt a keretet (architektúra, tech stack, feature-lista, konvenciók), amire minden későbbi egyedi change támaszkodhat. Amíg ez nincs meg legalább vázlatos formában, az 1. lépés ("Igény megfogalmazása") nem értelmezhető, mert nincs mihez viszonyítani egy-egy feature-igényt.

### Greenfield változat (új projekt)

| | |
|---|---|
| **Mikor** | Projekt kickoff, az első OpenSpec change előtt — egyszeri, nem ismétlődő lépés (kivéve jelentős architektúra-váltásnál) |
| **Ki** | Architect/tech lead az ügyféllel/product ownerrel közösen; AI itt facilitáló eszköz, nem automatizált flow-vezető |
| **Mit csinál** | Felmérés és rögzítés: <br>1. Üzleti cél és sikerkritériumok (mit old meg a rendszer, milyen KPI-k mentén mérjük) <br>2. Magas szintű architektúra (rendszerhatárok, komponensek, adatáramlás, integrációs pontok) <br>3. Tech stack kiválasztása és indoklása (nyelv, keretrendszer, cloud, adatbázis stb.) <br>4. Kezdeti feature-lista / backlog összeállítása és durva priorizálása — ez lesz a későbbi 1. lépések forrása <br>5. Non-functional requirementek (biztonság, compliance, skálázhatóság, banki/pénzügyi szabályozási elvárások, ha relevánsak) <br>6. Kritikus komponensek listájának első verziója — ez közvetlenül megalapozza a 7. lépés (Verify) blokkoló szabályát |
| **AI szerepe** | AI-asszisztált brainstorming, architektúra-alternatívák felvázolása, tech stack összehasonlítás, kockázatok feltárása — ez nem az `openspec` automatizált agent-flow-ja, hanem emberi vezetésű workshop, amit AI eszközként támogat |
| **HITL-kapu** | A fázis addig nem tekinthető lezártnak, amíg a stakeholder/ügyfél és az architect explicit jóvá nem hagyja az architektúrát, a tech stacket és a feature-listát — enélkül a per-feature ciklus nem indulhat el |
| **Eredmény** | Jóváhagyott projekt-keret: architektúra, tech stack, kezdeti backlog, konvenciók |
| **Fájl** | `openspec/project.md` vagy `openspec/architecture.md` (architektúra + tech stack leírása); `openspec/project-conventions.md` (kritikus komponensek listája és egyéb csapat-konvenciók); kezdeti backlog `openspec/roadmap.md`-ben vagy külső rendszerben (pl. Jira epic-ek) |

### Brownfield változat (meglévő projekt)

Nem projekt kickoffkor fut le automatikusan, hanem az alábbiak valamelyike váltja ki:
- Egy meglévő projektbe új csapattag/architect érkezik, és nincs megbízható, naprakész architektúra-dokumentáció
- Az OpenSpec/grill-me folyamatot most vezetik be egy már futó, korábban dokumentálatlan vagy elavultan dokumentált projektben
- Egy nagyobb, több komponenst érintő change előtt kiderül, hogy a meglévő dokumentáció nem tükrözi megbízhatóan a tényleges kódbázist (pl. a grill-me rendszeresen olyan feltételezéseket "olvas ki a kódból", amik utólag tévesnek bizonyulnak — ez önmagában jelzés, hogy Fázis 0-t brownfield módban le kellene futtatni)

Célja nem új architektúra kitalálása, hanem a tényleges, élő állapot feltárása és dokumentálása, hogy a grill-me és a Verify megbízható alapra tudjon támaszkodni.

| | |
|---|---|
| **Mikor** | A fenti triggerek bármelyikénél, mielőtt az érintett terület(ek)en bármelyik change belépne az 1. lépésbe |
| **Ki** | Architect/tech lead vezeti; AI agent kódbázis-feltáró szerepben aktívan közreműködik (nem csak facilitál) — mivel itt konkrét, meglévő kódot kell elemezni, nem csak koncepciókat egyeztetni |
| **Mit csinál** | 1. Kódbázis-feltárás (AI-asszisztált, reverse engineering): tényleges architektúra, modulhatárok, adatáramlás, függőségek feltérképezése a kódból — nem a régi dokumentációból, ha az megbízhatatlan <br>2. Gap-elemzés: eltérés a dokumentált (vagy vélt) és a tényleges architektúra/tech stack között — ez explicit listázandó, nem csendben felülírandó <br>3. Tech stack és technikai adósság leltár: elavult könyvtárak, deprecated minták, tesztlefedettségi hiányok, ismert "no-go" zónák a kódban <br>4. Kritikus komponensek azonosítása a meglévő rendszerben — itt gyakran nagyobb súlyú, mint greenfieldnél, mert éles, üzemelő komponensekről van szó <br>5. Feature-lista/backlog frissítése vagy első felállítása a felmért állapot fényében <br>6. Migrációs/kompatibilitási korlátok rögzítése — mit nem szabad megváltoztatni AI-asszisztált change-ekkel átgondolás nélkül |
| **AI szerepe** | Aktív kódbázis-elemzés (statikus elemzés, dependency-gráf, meglévő tesztek lefedettségének feltérképezése), majd a talált eltérések és kockázatok strukturált összefoglalása emberi review-ra — az AI itt javaslattevő, a tényleges "mi számít megbízhatónak" döntés emberi kézben marad |
| **HITL-kapu** | Az architect/tech lead explicit jóváhagyja vagy korrigálja az AI által feltárt architektúra-képet, gap-listát és kritikus komponens listát, mielőtt az az élő `project.md`/`project-conventions.md` tartalma lenne. Ez különösen fontos, mert itt az AI feltételezései valós, üzemelő rendszerről szólnak — egy téves feltételezés itt közvetlenül éles kockázatot jelent |
| **Eredmény** | Naprakész, a tényleges kódbázist tükröző architektúra-dokumentáció, gap-lista, frissített kritikus komponens lista, migrációs korlátok listája |
| **Fájl** | `openspec/project.md`/`architecture.md` frissítve (vagy első alkalommal létrehozva); `openspec/project-conventions.md` frissítve; `openspec/brownfield-gap-analysis.md` — a feltárt eltérések és technikai adósság listája, dátumozva, ami idővel csökken, ahogy a change-ek feldolgozzák az egyes pontokat, de nem törlődik, hanem archiválódik |

### A két változat összehasonlítása

| Szempont | Greenfield | Brownfield |
|---|---|---|
| Kiindulópont | Üres lap, tervezés | Meglévő, éles kód — feltárás, nem tervezés |
| AI fő szerepe | Facilitáló (brainstorming, alternatívák) | Aktív elemző (kódbázis reverse engineering) |
| Fő kockázat | Rosszul megtervezett architektúra | Téves feltételezés egy már működő rendszerről |
| Kritikus komponens lista eredete | Tervezett, még nem üzemelő logika alapján | Éles, üzemelő logika alapján — jellemzően bővebb és sürgetőbb |
| Extra output | — | Gap-analysis dokumentum, migrációs korlátok listája |

**Kapcsolódás a per-feature ciklushoz:** a Fázis 0-ban (akár greenfield, akár brownfield úton) rögzített feature-lista minden egyes eleme egy külön "igényként" lép be az 1. lépésbe. A Fázis 0 output-jai (architektúra, konvenciók) nem íródnak újra minden change-nél — azokra hivatkozik, azokhoz igazodik minden egyes proposal/spec, amit a grill-me és a Verify is ellenőriz. Brownfield esetben emellett a `brownfield-gap-analysis.md` is bemenete lehet egy-egy change proposal-nak.

**Élő dokumentum, nem egyszer-és-kész:** ha egy konkrét feature fejlesztése közben architektúra-szintű döntés vagy eltérés merül fel, azt vissza kell vezetni a `project.md`/`architecture.md`-be is — ellenkező esetben a projekt-szintű dokumentáció elavul, és a jövőbeli feature-ök már hibás alapra épülnek. Ez a visszavezetés a 9. lépés (Archive) részeként, checklist-elemként rögzítendő. Brownfield projektnél ugyanez vonatkozik a gap-listára is: ha egy change lezár egy gap-listán szereplő tételt, azt a change archiválásakor a gap-listából is ki kell pipálni (archiválva, nem törölve).

---

## A folyamat áttekintése (per-feature ciklus)

```
Ötlet → Explore → Proposal → GRILL-ME → Jóváhagyás → Apply → Verify → Code Review → Archive
                                  ↑           ↑                  |         |            |
                                  |           └── spec-hiány ────┘         |            |
                                  └────────────── spec-hiány (bármelyik pontból) ────────┘
```

Négy HITL-kapu biztosítja, hogy az AI sose menjen tovább emberi jóváhagyás nélkül: **(1) Grill-me válaszadás + feltételezés-jóváhagyás, (2) Spec/Design jóváhagyás, (3a) Code review, (3b) Archive-sync jóváhagyás.**

A diagram és az alábbi táblázat rögzíti, hogy Verify, Code Review és Archive bármelyike triggerelhet visszalépést a Grill-me-hez, ha spec-hiányt tár fel:

| Honnan | Mi történik, ha spec-hiányt találnak |
|---|---|
| Apply közben | Rövid grill-me/human döntés → delta spec + `tasks.md` frissítés |
| Verify közben | Ugyanaz, mint Apply-nál, de a verify riportban explicit rögzíteni kell a talált hiányt, ne csak szóban |
| Code Review közben | Reviewer megjelöli a PR-ban → szerző dönt: kis fix helyben, vagy vissza a spechez, ha strukturális |
| Archive közben | Ha a sync közben derül ki eltérés a kód és a spec között, az archiválás felfüggesztésre kerül, amíg nincs vagy spec-frissítés, vagy kód-fix |

---

## Lépésről lépésre

### 1. lépés — Igény megfogalmazása

| | |
|---|---|
| **Mikor** | Amikor új feature, módosítás vagy bugfix igénye felmerül |
| **Ki** | Termékfelelős / ügyfél / fejlesztő |
| **Mit csinál** | Természetes nyelven leírja, mit szeretne (pl. Jira ticket, Teams üzenet, szóbeli egyeztetés) |
| **Eredmény** | Informális, még nem strukturált igényleírás |
| **Fájl** | Nincs OpenSpec fájl még — külső rendszerben (Jira, ticket) rögzítve |

---

### 2. lépés — Explore *(opcionális, ha a scope bizonytalan)*

| | |
|---|---|
| **Mikor** | Ha a követelmény nem egyértelmű, vagy a kódbázisra gyakorolt hatás nem világos |
| **Ki** | Fejlesztő indítja, AI agent végzi |
| **Mit csinál** | `/opsx:explore` — az agent felméri a kódbázist, kérdéseket tesz fel a fejlesztőnek a bizonytalan pontokról |
| **Eredmény** | Tisztázott scope, a fejlesztő dönt, hogy megéri-e folytatni |
| **Fájl** | Nincs kötelező artifact; opcionálisan jegyzet a change leendő `design.md`-jébe kerül majd |

---

### 3. lépés — Proposal (javaslat) létrehozása

| | |
|---|---|
| **Mikor** | Miután a scope tisztázott |
| **Ki** | Fejlesztő kéri, AI agent generálja |
| **Mit csinál** | `/opsx:new <change-név>` majd `/opsx:ff` (vagy lépésenként `/opsx:continue`) — az agent legenerálja a proposal-t, spec deltát, design-t, tasks listát |
| **Eredmény** | Első draft a tervről — még nem jóváhagyott állapot |
| **Fájl** | `openspec/changes/<change-név>/proposal.md`, `openspec/changes/<change-név>/specs/<capability>/spec.md`, `design.md`, `tasks.md` |

---

### 4. lépés — Grill-me: Terv stressz-tesztelése (HITL-kapu #1)

Ez a lépés a folyamat gerince — ez biztosítja, hogy a terv ne felszínes AI-generálás maradjon, hanem tényleges emberi átgondoláson menjen át.

| | |
|---|---|
| **Mikor** | Közvetlenül a proposal/spec draft elkészülte után, implementáció előtt |
| **Ki hívja** | Fejlesztő/architect explicit kéri: *"grill me a proposal.md-n"* |
| **Mit csinál az AI** | A grill-me skill szisztematikusan végigkérdezi a tervet, ágról ágra a döntési fán; minden kérdésnél ajánlott választ is ad; amit a kódból meg tud válaszolni, azt nem kérdezi meg, hanem megnézi maga |
| **Ki válaszol (ez a valódi HITL)** | A fejlesztő/architect — minden kérdésre reagálnia kell: elfogadja az ajánlott választ, módosítja, vagy elutasítja |
| **Eredmény** | A nyitott kérdések, edge case-ek, nem tisztázott feltételezések bekerülnek a specbe, mielőtt kódba fagynának |
| **Fájl** | Frissül: `openspec/changes/<change-név>/proposal.md` és `specs/<capability>/spec.md` |

Miért nem hagyható ki: enélkül az AI a saját feltételezéseivel tölti ki a hiányokat a spec-ben — ez az a pont, ahol a legtöbb downstream implementációs hiba keletkezik.

**Feltételezések naplózása:** a grill-me-nek kötelezően listáznia kell egy külön szekcióban (`proposal.md` végén, "Assumptions from codebase" cím alatt) minden olyan pontot, amit *nem* kérdezett meg, mert "a kódból egyértelmű volt" — pontosan megjelölve, mely fájl/sor alapján jutott erre a következtetésre. Ez a lista is a HITL-kapu része: a fejlesztőnek ezt is jóvá kell hagynia (vagy felül kell bírálnia), nem csak a feltett kérdésekre adott válaszokat.

---

### 5. lépés — Spec + Design jóváhagyása (HITL-kapu #2)

| | |
|---|---|
| **Mikor** | A grill-me kör lezárása után |
| **Ki** | Fejlesztő/architect (csapatban: minden nem-triviális change esetén kötelező peer reviewer) |
| **Mit csinál** | `openspec show <change-név>` — átnézi a teljes proposal/spec/design/tasks csomagot; `openspec validate <change-név> --strict` — formai ellenőrzés |
| **Explicit emberi jóváhagyás** | Csak akkor mondja ki: *"a specek jók, mehet az implementáció"* — ez a tényleges kapu, amíg ez nem hangzik el, nincs `apply` |
| **Eredmény** | Jóváhagyott, validált terv |
| **Fájl** | Nincs új fájl — státuszváltás: a change "implementálásra kész" |

**A kapu technikai kikényszerítése:** ahol lehetséges, a jóváhagyást ne csak megállapodás, hanem eszközszintű zár is biztosítsa: a change addig `draft` státuszban marad, amíg valaki explicit nem futtatja az `openspec approve <change-név>` (vagy ezzel ekvivalens) parancsot, és a `/opsx:apply` elutasítja a futást, ha ez a státusz hiányzik. Ha az eszköz ezt natívan nem támogatja, minimum egy git hook/CI check ellenőrizze, hogy az `apply` commit előtt létezik-e jóváhagyó commit/PR-comment.

**Egyszemélyes (consultant) forgatókönyv:** ha nincs elérhető peer reviewer, a jóváhagyás nem eshet ki, hanem helyettesítő technikát kell alkalmazni:
- **Időbeli távolság:** a jóváhagyást legalább egy alvás/pihenő ciklus után kell megadni, nem közvetlenül a grill-me kör után
- **Külső LLM second opinion:** a proposal/spec csomagot egy másik modellel (más szolgáltató vagy más modellcsalád) is át kell futtatni kritikai review céljából, mielőtt a jóváhagyás megtörténik — ez explicit rögzítendő lépés
- Ezt a helyettesítést a `proposal.md`-ben dokumentálni kell (ki/mi végezte a második véleményt)

---

### 6. lépés — Implementáció (`apply`)

| | |
|---|---|
| **Mikor** | Az 5. lépés jóváhagyása után |
| **Ki** | AI agent hajtja végre, fejlesztő felügyeli |
| **Mit csinál** | `/opsx:apply` — a `tasks.md`-ben lévő taskokat sorban implementálja |
| **HITL menet közben** | Fejlesztő code review-zol taskonként/commitonként; ha menet közben spec-hiányt talál, vissza a 4. lépéshez (legalább egy rövid grill-me/human döntés, majd a delta spec + `tasks.md` frissítése — új task explicit hozzáadása, mert az `apply` csak azt hajtja végre, ami a listában szerepel) |
| **Eredmény** | Implementált kód |
| **Fájl** | Forráskód + `openspec/changes/<change-név>/tasks.md` (checkboxok pipálva: csak `[x]`/`[X]` számít késznek) |

---

### 7. lépés — Verify

| | |
|---|---|
| **Mikor** | Az összes task lezárása után |
| **Ki** | AI agent végzi az ellenőrzést, ember értékeli az eredményt |
| **Mit csinál** | `/opsx:verify` — ellenőrzi: completeness (minden requirement lefedve), correctness (implementáció megfelel a spec szándékának), coherence (design döntések tükröződnek a kódban) |
| **Eredmény** | Verify riport |
| **Fájl** | Verify riport csatolva a change-hez; ajánlott: jegyzet a PR leírásába, ha van eltérés |

**A Verify blokkolóvá tétele:**
- **Kritikus komponenseknél** (fizetés, adatbiztonság, külső integráció, banki/pénzügyi logika — a csapatnak projektspecifikusan explicit listába kell foglalnia, mi számít kritikusnak; ez a lista a Fázis 0-ban kerül első rögzítésre, és `openspec/project-conventions.md`-ben él) a verify riportban talált probléma blokkolja a Code Review-ra lépést, amíg nincs vagy fix, vagy explicit, írásos kockázatvállalási döntés
- **Nem kritikus komponenseknél** maradhat a "tudatosan dokumentált ismert hiányosság" opció, de a döntést és indoklását kötelező rögzíteni a PR leírásában, nem csak a fejlesztő fejében
- **Automatizált teszt kötelező elem:** minden `tasks.md`-nek tartalmaznia kell legalább egy "automated test coverage" taskot a lezárt feature(k)re; a verify ezt is ellenőrzi

A `proposal.md`-nek legyen egy kötelező mezője: *"Érinti-e kritikus komponenst? Igen/Nem + melyiket"* — ez a mező a `project-conventions.md` listájára hivatkozik. A `/opsx:verify` ezt a mezőt olvassa ki, hogy eldöntse: blokkoló-e a talált probléma vagy sem.

A kritikus komponens lista karbantartása: a csapat időszakos review-ja (pl. negyedévente) frissíti, illetve amikor új integrációs terület/domain kerül a rendszerbe. Maga a lista módosítása is HITL-döntés, de könnyű súlyú — elég egy rövid csapat-jóváhagyás (pl. PR a `project-conventions.md`-re), nem igényel teljes grill-me kört.

---

### 8. lépés — Code review / PR jóváhagyás (HITL-kapu #3a)

| | |
|---|---|
| **Mikor** | Verify után, mielőtt a kód a fő ágba kerülne |
| **Ki** | Emberi reviewer (kolléga, vagy ügyfélnél maga a felelős fejlesztő) |
| **Mit csinál** | Szabványos PR/code review — ez a "hagyományos" fejlesztési HITL kapu, ami itt sem marad ki |
| **Eredmény** | Jóváhagyott, mergelt PR |
| **Fájl** | Kód a fő ágban |

Ha itt derül ki spec-hiány: a reviewer megjelöli a PR-ban, a szerző dönt helyben javításról vagy visszalépésről a 4. lépéshez.

---

### 9. lépés — Archive (spec szinkronizálás) (HITL-kapu #3b)

| | |
|---|---|
| **Mikor** | Merge után, a change lezárásakor |
| **Ki** | Fejlesztő futtatja, explicit megerősíti a sync kérdést |
| **Mit csinál** | `/opsx:archive` — ellenőrzi a taskok teljességét, majd megkérdezi: *"Delta specs: Not yet synced to main → szinkronizáljam?"* |
| **HITL** | A fejlesztőnek aktívan igent kell mondania a szinkronizálásra — ez a pont, ahol a delta spec a kanonikus specifikációba kerül |
| **Eredmény** | A "hivatalos" specifikáció frissül, a change archiválásra kerül |
| **Fájl** | `openspec/specs/<capability>/spec.md` (frissítve) + a change mappa átkerül ide: `openspec/changes/archive/<dátum>-<change-név>/` |

Ha itt derül ki eltérés a kód és a spec között: az archiválás felfüggesztésre kerül, amíg nincs vagy spec-frissítés, vagy kód-fix.

Kiegészítő checklist-elem: ha a change érintette a projekt-szintű architektúrát vagy tech stacket, az archiválás előtt explicit ellenőrizni kell, hogy a `project.md`/`architecture.md` frissült-e ennek megfelelően. Ha nem, az archiválás ugyanúgy felfüggesztésre kerül, amíg ez meg nem történik. Brownfield projektnél ugyanez vonatkozik a `brownfield-gap-analysis.md`-re: ha a change lezár egy gap-listán szereplő tételt, azt a gap-listából is ki kell pipálni (archiválva, nem törölve).

---

## Összefoglaló táblázat: a HITL-kapuk

| # | Kapu | Mi történik, ha kihagyják |
|---|---|---|
| 1 | Grill-me válaszadás + feltételezés-jóváhagyás (4. lépés) | Az AI a saját feltételezéseivel tölti ki a hiányos specet, ellenőrizetlenül → rejtett hibák az implementációban |
| 2 | Spec/Design jóváhagyás (5. lépés) | Implementáció indul át nem gondolt terven |
| 3a | Code review (8. lépés) | Kód bekerül review nélkül |
| 3b | Archive-sync (9. lépés) | A spec "csendben" szétcsúszik a kódtól (silent spec drop) |

---

## Gyors parancs-referencia

| Fázis | Parancs |
|---|---|
| Projekt-indítás (greenfield) | Architektúra/tech stack/backlog workshop, rögzítve `openspec/project.md`-ben |
| Projekt-indítás (brownfield) | AI-asszisztált kódbázis-feltárás, rögzítve `openspec/brownfield-gap-analysis.md`-ben |
| Setup | `openspec init` |
| Feltárás | `/opsx:explore` |
| Terv generálás | `/opsx:new <név>` → `/opsx:ff` (vagy `/opsx:continue` lépésenként) |
| Stressz-teszt | grill-me kérés a proposal/spec fájlon + feltételezés-lista jóváhagyása |
| Jóváhagyás | `openspec approve <név>` (vagy ekvivalens állapotváltás) |
| Validálás | `openspec validate <név> --strict` |
| Implementáció | `/opsx:apply` (elutasítja a futást jóváhagyás nélkül) |
| Ellenőrzés | `/opsx:verify` (blokkoló kritikus komponenseknél) |
| Lezárás | `/opsx:archive` (felfüggeszthető eltérés esetén) |

---

## Ismert korlátok

- Az `openspec approve` / állapot-alapú kikényszerítés feltételezi, hogy az `openspec` CLI ezt natívan támogatja vagy kiterjeszthető rá — ezt a tényleges eszköz dokumentációjával célszerű validálni, mielőtt csapatszinten bevezetik; ha nem támogatott natívan, a git hook/CI-alapú alternatíva bonyolultabb lehet, mint elsőre tűnik
- A "kritikus komponens" definíciója projektspecifikus — ezt a csapatnak explicit listába kell foglalnia, különben a Verify-blokkolás szabálya értelmezésfüggő marad
- A Fázis 0 brownfield változata feltételezi, hogy az AI-asszisztált kódbázis-feltárás (statikus elemzés, dependency-gráf) megbízhatóan lefuttatható a konkrét kódbázison — nagyon nagy vagy erősen fragmentált (több repó, több nyelv) rendszereknél ez maga is jelentős, több körös munka lehet; ezt érdemes a Fázis 0 tervezésekor explicit időbecsléssel kezelni

---

*Ha ez a folyamat változik, ezt a dokumentumot is frissíteni kell — ez maga is egy "spec" a csapat munkamódjáról.*

---

## OsaPro projektkiegészítés

Ebben a projektben a távoli CI- és PR-ellenőrzéseket egyelőre helyben végezzük, push nélkül. Az ellenőrzött változás helyben a fő ágba kerül. Az egyszemélyes jóváhagyásnál a grill-me és az emberi jóváhagyások megmaradnak; kötelező pihenőidőt és külső LLM-másodvéleményt nem alkalmazunk. A Git-munkarend az `openspec/project-conventions.md` fájlban van. Jóváhagyott átmeneti eltérés: egyelőre nincs commit előtti hook. Az `openspec validate --all --strict` parancsot commit előtt kézzel, kötelezően futtatjuk. A többi ellenőrzés és emberi jóváhagyási kapu megmarad.
