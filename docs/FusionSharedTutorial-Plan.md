# Plán: Fusion Shared Mode — kde jsme a kam dál

Tento dokument je **orientační plán pro další práci**. Slouží tomu, kdo bude v repu pokračovat (člověk nebo agent), aby **věděl kontext** oficiálního tutorialu Photonu a **kde v projektu** leží naše implementace.

---

## Co vlastně děláme (smysl celé série)

**Fusion Shared Mode Basics** ([oficiální série](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/1-getting-started)) je **úzký vertikální řez**: z jedné Unity scény a Photon cloudu udělat **víc klientů ve stejné „místnosti“**, kde každý má **svého hráče** (objekt s `NetworkObject`), **synchronizovaný stav** (pozice přes `NetworkTransform`, další data přes `[Networked]`) a **autoritu** — kdo smí měnit co, a jak poslat akci na cizí objekt (**RPC na State Authority**).

Není to celá hra; je to **základní slovníček Fusionu** (runner, spawn, tick, networked properties, RPC) v režimu **Shared**, který je vhodný pro P2P/cloud room styl hry.

---

## Mapování: část tutorialu → co v repu

| Část Photonu | Odkaz | Co to řeší | Kde to máme v projektu |
|--------------|--------|------------|-------------------------|
| 1 – Getting started | [dokumentace](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/1-getting-started) | Účet, App Id, import Fusion | `Assets/Photon/`, `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` |
| 2 – Scene and player | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/2-scene-and-player) | `NetworkRunner`, bootstrap, spawn hráče | Scéna po menu **Tools → Fusion → Scene → Setup + Floor + Player Spawner**; `Assets/Scripts/Networking/PlayerSpawner.cs`; prefab `Assets/PlayerCharacter.prefab` |
| 3 – Movement and camera | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/3-movement-and-camera) | Pohyb v `FixedUpdateNetwork`, kamera | `Assets/Scripts/Player/PlayerMovement.cs`, `Assets/Scripts/Player/FirstPersonCamera.cs` (kamera na **Main Camera** ve `Assets/Scenes/SampleScene.unity`) |
| 4 – Network properties | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/4-network-properties) | `[Networked]` + `OnChangedRender` | `Assets/Scripts/Player/PlayerColor.cs` na prefabu |
| 5 – RPCs | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/5-remote-procedure-calls) | Poškození přes RPC na autoritu cíle | `Assets/Scripts/Player/Health.cs` (`DealDamageRpc`), `Assets/Scripts/Player/PlayerCombat.cs` (Tab / kouzlo volá RPC na cíli); pomocná logika cílů v `Assets/Scripts/Player/CombatTargetSelector.cs` |
| 6 – Where to go next | [závěr série](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/6-where-to-go-next) | Už **není kód tutorialu** — jen doporučení číst manuál a samples | **Hotovo z pohledu projektu** — žádný další kód z téže série; viz níže |

**Vstup (New Input System):** V `ProjectSettings/ProjectSettings.asset` je **`activeInputHandler: 1`**. Veškerý gameplay vstup jde přes balíček **Input System** (`Keyboard` / `Mouse` v `PlayerSpawner` / `FirstPersonCamera`) a tick data přes Fusion **`GameplayInput`** v `OnInput` — bez legacy `UnityEngine.Input.*`, v souladu s oficiálním návodem na [Shared mode input](https://doc.photonengine.com/fusion/current/manual/input/shared-mode-input).

**Struktura skriptů:** `Assets/Scripts/` je rozdělené podle vrstev (viz `docs/UnityCSharp-CodeOrganization.md`): `Core/` (např. `GameplayInput`), `Networking/` (spawner), `Player/`, `UI/` (`CombatHud`), `Training/` (`TrainingDummy`).

---

## Stav „oficiálního“ tutorialu

- **Kapitoly 2–5** — implementace v repu odpovídá záměru série (spawn, pohyb/kamera, `[Networked]` barva, RPC poškození + výběr cíle / kouzlo).
- **Kapitola 6** — žádný další kód z Photona; v repu je to **uzavřené** (čti manuál / samples podle vlastního směru).

**Série Shared Mode Basics je z pohledu tohoto repozitáře dokončená:** zbývá jen ruční testování (dva klienti, checklist níže) a případně větve A/B z následující sekce — ne další kapitola stejného tutorialu.

---

## Doporučený plán další implementace (volitelné větve)

### Větev A — „ještě pořád učení Fusionu“ (doporučeno krátce před gameplay milníkem)

1. **NetworkInput** — základ je `PlayerSpawner.OnInput` + `GameplayInput` (`Assets/Scripts/Core/`); dál zpevnit / rozšířit podle manuálu místo jakéhokoli přímého čtení vstupu v `PlayerMovement` / `PlayerCombat` / `PlayerColor`  
   - Manuál: [Network Input](https://doc.photonengine.com/fusion/current/manual/network-input), Shared: [Shared mode input](https://doc.photonengine.com/fusion/current/manual/input/shared-mode-input).  
   - **Proč:** Soulad s pravidly repa („klient posílá intent“), méně chyb při lag / predikci.

2. **Photon samples ve Shared módu** (část 6 explicitně odkazuje)  
   - Stáhnout / otevřít sample označený jako Shared-compatible, porovnat strukturu runneru, spawnu, inputu.

3. **Area of Interest (AOI)** až budeme mít víc objektů než pár hráčů  
   - Manuál / část 6: optimalizace síťové zátěže.

### Větev B — „Milestone 1 tohoto repa“ (hlavní produktový směr)

Podle `CLAUDE.md` / `AGENTS.md` po zvládnutém základu síťě:

1. **Tab target** + jednoduchý **instant spell** s kontrolou autority a dosahu.  
2. **HP / smrt / respawn** (základ `Assets/Scripts/Player/Health.cs` z tutorialu — zvážit sloučení nebo nový „combat“ model podle autority).  
3. **Minimální HUD** (`Assets/Scripts/UI/CombatHud.cs` — lokální HP, HP cíle).

**Závislost:** Větev B předpokládá stabilní **pohyb + kamera + 2 klienty**; pokud něco z tutorialu 3–5 v buildu nefunguje, nejdřív opravit / zpevnit (viz test checklist v `AGENTS.md`).

---

## Kontrolní checklist před další větší změnou

- [ ] Dva klienti (editor + build nebo dva buildy), stejná room, oba vidí pohyb druhého.  
- [ ] Žádné červené chyby ve Console po recompile (Fusion weaver, `Networked` / `Rpc`).  
- [ ] `PlayerCharacter` má `FusionPrefab` label (pro `NetworkProjectConfig` / spawn).  
- [ ] Scéna v **Build Settings** odpovídá tomu, co testuješ (`SampleScene`).

---

## Rychlé odkazy

- Série: [Shared Mode Basics](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/1-getting-started)  
- Další krok dle Photona po sérii: [6 – Where to go next](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/6-where-to-go-next)  
- Editor zkratka v projektu: `Assets/Editor/ForbesFusionSharedSceneSetup.cs` (menu pod **Tools → Fusion** a **GameObject → Fusion**)

---

*Poslední úprava plánu: série 2–6 je z pohledu kódu uzavřená; vstup je výhradně New Input System + `GameplayInput`; tabulka cest odpovídá `Assets/Scripts/` (Core / Networking / Player / UI / Training).*
