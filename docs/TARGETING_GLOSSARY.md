# Targeting glossary

Short definitions for terms used in issues, PRs, and reviews. Wording matches the current prototype scripts unless noted.

## `Targetable`

`MonoBehaviour` on an entity the local player can select. `TargetingController` filters candidates (e.g. skips the local player for Tab, skips dead targets when `Health` reports dead).

## `CurrentTarget`

The `Targetable` instance the local `TargetingController` has selected right now (`TargetingController.CurrentTarget`). Cleared on Escape, on death of the target (when `Health.IsDead`), and updated by Tab / LMB flows.

## `CurrentTargetId`

`NetworkId` derived from the current target’s `NetworkObject.Id`, or `default` when there is no target (`TargetingController.CurrentTargetId`). `FusionInputProvider` copies this into `GameplayInput.TargetId` each tick so state authority can validate combat against the same id.

## Tab targeting

Press **Tab** to cycle the current target through alive `Targetable` objects in the scene (deterministic order by `NetworkId.Raw`), skipping the local player. Implemented in `TargetingController.CycleTarget` with `TargetingAcquisitionLogic.IsTabTargetingCandidate`.

**Review note:** the `TargetingController` class XML summary still mentions “closest-first” Tab cycling, but `CycleTarget` currently sorts candidates by `NetworkId.Raw` (not by distance). This glossary describes the **implementation**; align or update the XML separately if you change behavior.

## LMB targeting

On **left mouse button release**, if the orbit camera did **not** treat the interaction as an orbit drag, the controller raycasts from the screen to try to hit a `Targetable` and selects it. A miss does not clear the current target (per controller behavior).

## orbit-drag gate

`ProceedWithOrbitAwareLmbSelection` calls `EnsureCamera()` first, then checks `ThirdPersonOrbitCamera.IsLmbDragging`. If the user was dragging to orbit, LMB release does **not** run world targeting (avoids accidental retarget while rotating the camera).

## `ClickTargetingProxy`

Child `GameObject` created by `TrainingDummy.Awake` when missing: holds the **trigger** `CapsuleCollider` used for click rays while keeping the main dummy’s `CharacterController` layout stable (“prefab-first” comment in code).

## Fusion physics scene vs Unity default physics scene

`TargetingAcquisitionLogic.TryPickSelectableAlongRay` raycasts **`NetworkRunner.GetPhysicsScene()`** while the runner exists and is running; if that miss (or no runner), it falls back to **`Physics.Raycast`** in Unity’s default physics scene. Trigger colliders are included (`QueryTriggerInteraction.Collide`) so trigger proxy volumes still resolve.

## player-facing targeting boundary

Local-only selection and highlight (`TargetingController`, `TargetHighlight`, `SelectedTargetHealthBar`) vs authoritative combat validation on the host using `GameplayInput.TargetId` / range checks. Clients express intent; they do not authoritatively apply damage through targeting alone.
