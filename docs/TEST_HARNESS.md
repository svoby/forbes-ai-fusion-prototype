# Test harness (EditMode + PlayMode)

This project uses the **Unity Test Framework** (`com.unity.test-framework` in `Packages/manifest.json`). Automated coverage includes **EditMode** tests in assembly **Forbes.Tests.EditMode** (`Assets/Tests/EditMode/`) and **PlayMode** component tests in **Forbes.Tests.PlayMode** (`Assets/Tests/PlayMode/`).

## Run EditMode tests in the Editor

1. Open the project in Unity.
2. **Window → General → Test Runner**.
3. Open the **Edit Mode** tab, select assembly **Forbes.Tests.EditMode**, then **Run All**.

No batchmode is required; this works while the Editor is open.

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

## Run PlayMode tests in the Editor

1. Open the project in Unity.
2. **Window → General → Test Runner**.
3. Open the **Play Mode** tab, select assembly **Forbes.Tests.PlayMode**, then **Run All**.

PlayMode tests can run while the Editor is open (same as EditMode).

## Run PlayMode tests from the command line (Windows)

1. **Close the Unity Editor** for this project (batchmode cannot open a locked project).
2. From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-playmode-tests.ps1
```

Optional: pass `-UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"` if Hub discovery is wrong.

**Do not** add `-nographics` for PlayMode: the Unity Test Framework requires a graphics device for Play Mode tests. The script omits it on purpose.

Outputs:

- **JUnit-style XML:** `TestResults/playmode.xml`
- **Unity log:** `TestResults/unity-playmode.log`

A non-zero exit code means one or more tests failed (or Unity could not run).

## Not implemented yet

**Fusion** network smoke tests and multi-client automation are **intentionally out of scope** for this harness. They are listed in `docs/TEST_COVERAGE_PLAN.md` for future work.

## More detail

See `docs/TEST_COVERAGE_PLAN.md` for planned coverage and naming of test fixtures.
