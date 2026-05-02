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
| 2 – Scene and player | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/2-scene-and-player) | `NetworkRunner`, bootstrap, spawn hráče | Scéna po menu **Tools → Fusion → Scene → Setup + Floor + Player Spawner**; `Assets/Scripts/PlayerSpawner.cs`; prefab `Assets/PlayerCharacter.prefab` |
| 3 – Movement and camera | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/3-movement-and-camera) | Pohyb v `FixedUpdateNetwork`, kamera | `PlayerMovement.cs`, `FirstPersonCamera.cs` na **Main Camera** ve `Assets/Scenes/SampleScene.unity` |
| 4 – Network properties | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/4-network-properties) | `[Networked]` + `OnChangedRender` | `PlayerColor.cs` na prefabu |
| 5 – RPCs | [tutorial](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/5-remote-procedure-calls) | Poškození přes RPC na autoritu cíle | `Health.cs`, `RaycastAttack.cs` na prefabu |
| 6 – Where to go next | [závěr série](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/6-where-to-go-next) | Už **není kód tutorialu** — jen doporučení číst manuál a samples | V repu **není co „dodělat“ z kapitoly 6**; jde o směr dalšího učení |

**Poznámka k vstupu:** Projekt má v `ProjectSettings/ProjectSettings.asset` **`activeInputHandler: 1`** (nový Input System). Tutorial v části 3–5 původně používá legacy `Input.*`; naše skripty používají **`Keyboard`/`Mouse` z Input Systemu**, aby to v editoru vůbec fungovalo.

---

## Stav „oficiálního“ tutorialu

- **Kapitoly 2–5** máme v kódu reflektované v tomto repu (viz tabulka).
- **Kapitola 6** není implementační úkol — je to **rozcestník** (samples, manuál, AOI, další komponenty).

Tím pádem: **další krok už není „kapitola 7 stejného tutorialu“**, ale buď **rozšíření podle manuálu/samples**, nebo **přechod na vlastní milník** repa (viz `CLAUDE.md` / `AGENTS.md`).

---

## Doporučený plán další implementace (volitelné větve)

### Větev A — „ještě pořád učení Fusionu“ (doporučeno krátce před gameplay milníkem)

1. **NetworkInput** místo čtení kláves přímo v `PlayerMovement` / `RaycastAttack` / `PlayerColor`  
   - Manuál: [Network Input](https://doc.photonengine.com/fusion/current/manual/network-input), Shared: [Shared mode input](https://doc.photonengine.com/fusion/current/manual/input/shared-mode-input).  
   - **Proč:** Soulad s pravidly repa („klient posílá intent“), méně chyb při lag / predikci.

2. **Photon samples ve Shared módu** (část 6 explicitně odkazuje)  
   - Stáhnout / otevřít sample označený jako Shared-compatible, porovnat strukturu runneru, spawnu, inputu.

3. **Area of Interest (AOI)** až budeme mít víc objektů než pár hráčů  
   - Manuál / část 6: optimalizace síťové zátěže.

### Větev B — „Milestone 1 tohoto repa“ (hlavní produktový směr)

Podle `CLAUDE.md` / `AGENTS.md` po zvládnutém základu síťě:

1. **Tab target** + jednoduchý **instant spell** s kontrolou autority a dosahu.  
2. **HP / smrt / respawn** (už máme základ `Health` z tutorialu — zvážit sloučení nebo nový „combat“ model podle autority).  
3. **Minimální HUD** (lokální HP, HP cíle).

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

*Poslední úprava plánu: odrazuje stav repa po dokončení kapitol 2–5 tutorialu a závěru kapitoly 6 jako „žádný další kód z téže série“.*
