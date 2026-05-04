# Test harness (EditMode + PlayMode)

This project uses the **Unity Test Framework** (`com.unity.test-framework` in `Packages/manifest.json`).

- Edit Mode assembly: **Forbes.Tests.EditMode** (`Assets/Tests/EditMode/`).
- Play Mode assembly: **Forbes.Tests.PlayMode** (`Assets/Tests/PlayMode/`).

## Run EditMode tests in the Editor

1. Open the project in Unity.
2. **Window → General → Test Runner**.
3. Open the **Edit Mode** tab, select assembly **Forbes.Tests.EditMode**, then **Run All**.

No batchmode is required; this works while the Editor is open.

After **Run All** (from either tab), the Editor also writes a **plain-text** summary of that run to **`TestResults/last-editor-test-run.log`** (repo root). Failed leaf tests include **message + stack trace**. Check the Console for `[Forbes] Test run log written: ...` if the path is useful to copy.

## Run EditMode tests from the command line (Windows)

1. **Close the Unity Editor** for this project (batchmode cannot use a locked project).
2. From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-editmode-tests.ps1
```

Optional: pass `-UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"` if Hub discovery is wrong.

Outputs:

- **JUnit-style XML:** `TestResults/editmode.xml`
- **Unity log:** `TestResults/unity-editmode.log`

A non-zero exit code means one or more tests failed (or Unity could not run).

The script `tools/Run-EditModeTests.ps1` is a thin wrapper that calls `run-editmode-tests.ps1` for backwards compatibility.

## PlayMode tests (Fusion smoke)

PlayMode fixtures live in **Forbes.Tests.PlayMode** (`Assets/Tests/PlayMode/`). They start a `NetworkRunner` in **GameMode.Single** for Fusion smoke coverage.

### Run PlayMode tests in the Editor

1. **Window → General → Test Runner** → **Play Mode** tab → assembly **Forbes.Tests.PlayMode** → **Run All**.

The same **`TestResults/last-editor-test-run.log`** file is updated whenever you finish a run from the Test Runner window (Edit Mode or Play Mode).

### Run PlayMode tests from the command line (Windows)

1. **Close the Unity Editor** for this project (batchmode cannot use a locked project).
2. From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-playmode-tests.ps1
```

Outputs:

- **JUnit-style XML:** `TestResults/playmode.xml`
- **Unity log:** `TestResults/unity-playmode.log`

### Fusion PlayMode notes

- **`NetworkMobBrainMeleeSmokeTests`:** authority mob melee uses tick cadence; victim is spawned out of range first so HP can settle, then moved in range. Baseline HP is taken **before** closing distance, because the mob attacks immediately in range.
- **`FusionHealthSmokeTests`:** do not `Destroy` networked behaviours on the dummy; use **`FusionPlayModeTestHelpers.PinMobBrainNoCombat`** so wander/melee are off while Fusion still ticks sibling **`Health`** (respawn assertions need simulation).
- **`Health.AuthorityApplyStartingHealthIfUnset`:** used by PlayMode smoke when Editor spawn order leaves **`NetworkedHealth`** at zero; real deaths (scheduled **`RespawnAtTick`**) are not overwritten.

## More detail

See `AGENTS.md` for the definition of done and test obligations per task. See `docs/PROJECTILE_POLICY.md` for the testing policy specific to the missile/projectile layer.
