# Sprint 0 — Foundation & Playable POC

**Goal:** Two players can connect, move, fight, and respawn in a single session.

**Duration:** 1 evening (~4 hours focused)

---

## Tasks

### #1 — Project Setup & Fusion Bootstrap

**Objective:** Unity project opens cleanly; Fusion runner starts; host and client connect to the same room.

**Files:**
- `Assets/Scripts/Networking/NetworkBootstrap.cs`
- `Assets/Settings/NetworkProjectConfig` (Fusion config asset)

**Acceptance Criteria:**
- [ ] `NetworkBootstrap.StartGame(GameMode.AutoHostOrClient)` is called on scene load.
- [ ] First peer becomes host; second peer joins as client within 5 seconds.
- [ ] Console logs "Connected as Host" / "Connected as Client" with player count.
- [ ] No errors in console after connection.

**Manual Test Steps:**
1. Press Play in Editor (peer 1 becomes host).
2. Build and run executable (peer 2 joins as client).
3. Verify both peers show "Connected" in the HUD status label.

---

### #2 — Player Spawn

**Objective:** Each connecting peer gets a networked player object at a fixed spawn point.

**Files:**
- `Assets/Scripts/Player/SpawnManager.cs`
- `Assets/Prefabs/Player.prefab` (capsule + `NetworkObject` + `NetworkTransform`)

**Authority:** Host (`HasStateAuthority`) calls `Runner.Spawn`.

**Acceptance Criteria:**
- [ ] Host spawns at `SpawnPoints[0]`; client spawns at `SpawnPoints[1]`.
- [ ] Both peers see both capsules within 1 tick of the second peer joining.
- [ ] Despawn on disconnect removes the capsule on all peers.

**Manual Test Steps:**
1. Connect two peers.
2. Verify two capsules appear in Scene view on both instances.
3. Disconnect client; verify client capsule disappears on host within 2 seconds.

---

### #3 — Player Movement

**Objective:** WASD/stick moves the local player; movement is host-authoritative.

**Files:**
- `Assets/Scripts/Player/PlayerController.cs`
- `Assets/Scripts/Player/PlayerInputProvider.cs`

**Authority:** Input collected on InputAuthority; position applied by StateAuthority.

**Acceptance Criteria:**
- [ ] Local player moves in 4 directions at `MoveSpeed` units/second.
- [ ] Remote player position updates smoothly (NetworkTransform interpolation).
- [ ] No rubber-banding on localhost (prediction matches authority).

**Manual Test Steps:**
1. Hold W on host; character moves forward.
2. Observe from client window — host character moves the same direction.
3. Hold A on client; client character moves left, visible on host.

---

### #4 — Tab Target

**Objective:** Tab key cycles through enemy players; selected target is highlighted.

**Files:**
- `Assets/Scripts/Combat/TargetSelector.cs`

**Authority:** Target selection predicted on InputAuthority; confirmed by StateAuthority.

**Acceptance Criteria:**
- [ ] Pressing Tab selects the nearest other player.
- [ ] Selected target gets a visible highlight (e.g. colored outline or ring).
- [ ] Target nameplate shows target's display name or "Player 2".
- [ ] Target reference is `[Networked]` — both peers see the same target.

**Manual Test Steps:**
1. Connect two peers.
2. Press Tab on host — verify target highlight appears on client's capsule on both screens.
3. Press Tab again — verify target clears (toggle off) or wraps to next enemy.

---

### #5 — Instant Spell (Q)

**Objective:** Pressing Q deals 20 damage to the current target (host-authoritative).

**Files:**
- `Assets/Scripts/Combat/SpellCaster.cs`

**Authority:** `HasStateAuthority` applies damage via `HealthSystem.TakeDamage`.

**Acceptance Criteria:**
- [ ] Pressing Q with no target does nothing.
- [ ] Pressing Q with a live target reduces target HP by 20 on both peers.
- [ ] Cannot reduce HP below 0 via repeated casts (clamped).

**Manual Test Steps:**
1. Select a target (Tab).
2. Press Q → verify HP bar drops by 20 on both screens.
3. Press Q 5 times on a 100 HP target → HP reaches 0, death triggered.

---

### #6 — HP Sync, Death, & Respawn

**Objective:** HP is networked; reaching 0 triggers a 5-second respawn.

**Files:**
- `Assets/Scripts/Combat/HealthSystem.cs`

**Authority:** All HP mutations on `HasStateAuthority`.

**Acceptance Criteria:**
- [ ] HP starts at 100 for each player.
- [ ] Damage is reflected on both peers within 1 tick.
- [ ] Death disables the player object visually (hide capsule).
- [ ] After 5 seconds, player respawns at original spawn point with full HP.
- [ ] No ghost objects or desync after a full death/respawn cycle.

**Manual Test Steps:**
1. Burn target to 0 HP (5× Q casts).
2. Verify capsule hides on both screens.
3. Wait 5 seconds — verify capsule reappears with HP bar full.
4. Repeat once to confirm no desync.

---

### #7 — Minimal HUD

**Objective:** On-screen display of local HP, target HP/nameplate, and room status.

**Files:**
- `Assets/Scripts/UI/HudController.cs`
- `Assets/Scripts/UI/HealthBar.cs`

**Authority:** Read-only; no state mutation in UI code.

**Acceptance Criteria:**
- [ ] Local HP bar visible at all times.
- [ ] Target HP bar and nameplate visible when a target is selected, hidden otherwise.
- [ ] Room status label shows "Host" or "Client" and current player count.

**Manual Test Steps:**
1. Launch two peers.
2. Verify local HP bar appears on both.
3. Tab-target the opponent; verify target HP bar appears.
4. Deal damage; verify both HP bars update in real time.

---

## Definition of Done (Sprint 0)

All 7 tasks pass their manual test steps in a two-peer session without console errors or visible desyncs.
