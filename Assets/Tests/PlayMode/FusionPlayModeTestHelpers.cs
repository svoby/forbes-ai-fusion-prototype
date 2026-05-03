using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Ensures Fusion runner shutdown runs even when StartGame / assertions fail mid-test.
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
    /// Wander off, no melee: keeps <see cref="NetworkMobBrain"/> enabled so Fusion still ticks sibling <see cref="Health"/>.
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
