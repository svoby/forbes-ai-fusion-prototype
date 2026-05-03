using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Ensures Fusion runner shutdown runs even when StartGame / assertions fail mid-test.
  /// Shared polling/teleport coroutine helpers avoid copy-pasted <c>WaitUntil</c>/<c>WaitFrames</c> in PlayMode fixtures.
  /// </summary>
  internal static class FusionPlayModeTestHelpers {
    internal static IEnumerator RunWithFusionSession(FusionSinglePlayerTestSession session, Func<IEnumerator> body) {
      AssertionException assertionFail = null;
      Exception otherFail = null;

      IEnumerator startEnum = session.Start();
      bool startFinishedCleanly = false;
      while (!startFinishedCleanly) {
        bool hasNext;
        try {
          hasNext = startEnum.MoveNext();
        } catch (AssertionException ex) {
          assertionFail = ex;
          break;
        } catch (Exception ex) {
          otherFail = ex;
          break;
        }

        if (!hasNext) {
          startFinishedCleanly = true;
          break;
        }

        yield return startEnum.Current;
      }

      if (assertionFail == null && otherFail == null) {
        bool prevSuppress = TargetingController.SuppressLocalSelectionInputInTests;
        TargetingController.SuppressLocalSelectionInputInTests = true;
        try {
          IEnumerator inner = body();
          bool innerDone = false;
          while (!innerDone) {
            bool hasNext;
            try {
              hasNext = inner.MoveNext();
            } catch (AssertionException ex) {
              assertionFail = ex;
              break;
            } catch (Exception ex) {
              otherFail = ex;
              break;
            }

            if (!hasNext) {
              innerDone = true;
              break;
            }

            yield return inner.Current;
          }
        } finally {
          TargetingController.SuppressLocalSelectionInputInTests = prevSuppress;
        }
      }

      IEnumerator shutdown = session.ShutdownAndDestroy();
      while (shutdown.MoveNext()) {
        yield return shutdown.Current;
      }

      if (assertionFail != null) {
        throw assertionFail;
      }

      if (otherFail != null) {
        throw otherFail;
      }
    }

    /// <summary>
    /// Polls until <paramref name="predicate"/> is true or <paramref name="maxFrames"/> steps elapse (fails the test).
    /// </summary>
    /// <param name="physicsThenRenderEachStep">
    /// When true (default): <see cref="WaitForFixedUpdate"/> then <c>yield return null</c> per step (typical Fusion smoke).
    /// When false: only <c>yield return null</c> (slower polling; used by movement smoke where timing matched this pattern).
    /// </param>
    internal static IEnumerator WaitUntil(
      Func<bool> predicate,
      int maxFrames,
      bool physicsThenRenderEachStep = true,
      string messageOnFail = null) {
      int i = 0;
      while (i < maxFrames && !predicate()) {
        i++;
        if (physicsThenRenderEachStep) {
          yield return new WaitForFixedUpdate();
        }

        yield return null;
      }

      if (predicate()) {
        yield break;
      }

      string suffix = !string.IsNullOrEmpty(messageOnFail) ? " " + messageOnFail : "";
      Assert.Fail($"Predicate not satisfied within {maxFrames} frames.{suffix}");
    }

    /// <summary>Same as <see cref="WaitUntil"/> but builds the failure suffix lazily only on timeout.</summary>
    internal static IEnumerator WaitUntilLazy(
      Func<bool> predicate,
      int maxFrames,
      Func<string> buildFailureDetails,
      bool physicsThenRenderEachStep = true) {
      int i = 0;
      while (i < maxFrames && !predicate()) {
        i++;
        if (physicsThenRenderEachStep) {
          yield return new WaitForFixedUpdate();
        }

        yield return null;
      }

      if (predicate()) {
        yield break;
      }

      string extra = buildFailureDetails != null ? " " + buildFailureDetails() : "";
      Assert.Fail($"Predicate not satisfied within {maxFrames} frames.{extra}");
    }

    /// <seealso cref="WaitUntil(Func{bool},int,bool,string)"/>
    internal static IEnumerator WaitFrames(int count, bool physicsThenRenderEachStep = true) {
      for (var i = 0; i < count; i++) {
        if (physicsThenRenderEachStep) {
          yield return new WaitForFixedUpdate();
        }

        yield return null;
      }
    }

    /// <summary>
    /// PlayMode-only positioning: expects this runner to hold <b>state authority</b> on <paramref name="obj"/>
    /// (e.g. <see cref="NetworkSpawnFlags.SharedModeStateAuthLocalPlayer"/> in Single smoke). Uses
    /// <see cref="NetworkTransform.Teleport"/> when present; otherwise assigns <see cref="Transform"/> (caller must ensure that is valid).
    /// </summary>
    internal static void TeleportNetworkObjectForPlayModeSmokeTest(NetworkObject obj, Vector3 worldPos) {
      Quaternion rot = Quaternion.identity;
      if (obj.TryGetComponent<CharacterController>(out var cc)) {
        cc.enabled = false;
      }

      if (obj.TryGetComponent<NetworkTransform>(out var nt) && nt.HasStateAuthority) {
        nt.Teleport(worldPos, rot);
      } else {
        obj.transform.SetPositionAndRotation(worldPos, rot);
      }

      if (obj.TryGetComponent<CharacterController>(out var cc2)) {
        cc2.enabled = true;
      }
    }

    /// <summary>
    /// With <c>EnqueueIncompleteSynchronousSpawns</c>, <see cref="NetworkRunner.Spawn"/> can return null while loads queue.
    /// Poll <see cref="NetworkRunner.TrySpawn"/> until <see cref="NetworkSpawnStatus.Spawned"/> or fail fast on terminal errors.
    /// Also covers the case where prefab acquisition keeps retrying (e.g. scene manager stuck busy in PlayMode).
    /// </summary>
    internal static IEnumerator SpawnPrefabBlocking(
      NetworkRunner runner,
      UnityEngine.GameObject prefab,
      UnityEngine.Vector3 position,
      UnityEngine.Quaternion rotation,
      PlayerRef inputAuthority,
      NetworkSpawnFlags flags,
      Action<NetworkObject> assign,
      int maxFrames = 900) {
      int frames = 0;
      while (frames < maxFrames) {
        NetworkObject obj;
        var status = runner.TrySpawn(prefab, out obj, position, rotation, inputAuthority, null, flags);
        if (status == NetworkSpawnStatus.Spawned) {
          Assert.IsNotNull(obj, "TrySpawn reported Spawned but NetworkObject is null.");
          assign(obj);
          yield break;
        }

        if (status == NetworkSpawnStatus.Queued) {
          frames++;
          yield return null;
          continue;
        }

        Assert.Fail($"TrySpawn failed with status {status} after {frames} frames.");
        yield break;
      }

      Assert.Fail($"TrySpawn remained Queued longer than {maxFrames} frames.");
    }

    /// <summary>
    /// Same as <see cref="SpawnPrefabBlocking"/> but retries with <see cref="PlayerRef.None"/> if local player is not ready yet.
    /// </summary>
    internal static IEnumerator SpawnPlayerPrefabBlocking(
      NetworkRunner runner,
      UnityEngine.GameObject prefab,
      UnityEngine.Vector3 position,
      UnityEngine.Quaternion rotation,
      NetworkSpawnFlags flags,
      Action<NetworkObject> assign,
      int maxFrames = 900) {
      PlayerRef auth = runner.LocalPlayer;
      int frames = 0;
      while (frames < maxFrames) {
        NetworkObject obj;
        var status = runner.TrySpawn(prefab, out obj, position, rotation, auth, null, flags);
        if (status == NetworkSpawnStatus.Spawned) {
          Assert.IsNotNull(obj, "TrySpawn reported Spawned but NetworkObject is null.");
          assign(obj);
          yield break;
        }

        if (status == NetworkSpawnStatus.FailedLocalPlayerNotYetSet && auth != PlayerRef.None) {
          auth = PlayerRef.None;
          frames = 0;
          continue;
        }

        if (status == NetworkSpawnStatus.Queued) {
          frames++;
          yield return null;
          continue;
        }

        Assert.Fail($"TrySpawn failed with status {status} after {frames} frames.");
        yield break;
      }

      Assert.Fail($"TrySpawn remained Queued longer than {maxFrames} frames.");
    }

    /// <summary>
    /// PlayMode/smoke helper: disables wander and combat via serialized tuning only.
    /// Keeps <see cref="NetworkMobBrain"/> enabled so Fusion still ticks sibling <see cref="Health"/> (respawn/melee sibling tests rely on this).
    /// </summary>
    internal static void PinMobBrainNoCombat(NetworkMobBrain brain) {
      if (brain == null) {
        return;
      }

      brain.WanderRadius = 0f;
      brain.MoveSpeed = 0f;
      brain.MinLegDistance = 0f;
      brain.IdleTicksMin = 1;
      brain.IdleTicksMax = 1;
      brain.AttackRange = 0f;
      brain.AggroRadius = 0f;
      brain.AttackDamage = 0f;
      brain.AttackIntervalSeconds = 999f;
    }
  }
}
