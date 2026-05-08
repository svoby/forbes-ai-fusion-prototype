using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  [TestFixture]
  public class TargetHighlightTests {
    GameObject _go;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [TearDown]
    public void TearDown() {
      if (_go != null) {
        Object.DestroyImmediate(_go);
        _go = null;
      }
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    public IEnumerator AfterAwake_RingRenderer_IsDisabled() {
      _go = new GameObject(nameof(TargetHighlight));
      _go.AddComponent<LineRenderer>();
      _go.AddComponent<TargetHighlight>();
      yield return null;
      Assert.IsFalse(_go.GetComponent<LineRenderer>().enabled);
    }

    [UnityTest]
    public IEnumerator SetTarget_Activates_And_LateUpdate_SnapsToFeetOffset() {
      _go = new GameObject(nameof(TargetHighlight));
      _go.AddComponent<LineRenderer>();
      var highlight = _go.AddComponent<TargetHighlight>();

      var targetGo = new GameObject("Target");
      try {
        targetGo.transform.position = new Vector3(10f, 5f, -3f);
        targetGo.AddComponent<NetworkObject>();
        var targetable = targetGo.AddComponent<Targetable>();

        highlight.SetTarget(targetable);
        Assert.IsTrue(_go.GetComponent<LineRenderer>().enabled);

        yield return null;
        yield return null;
        // IEnumerator resumes after Update but before LateUpdate for a plain yield; TargetHighlight
        // moves the ring in LateUpdate. Wait until the frame finishes so we compare against that pose.
        yield return new WaitForEndOfFrame();

        // Plain target: no CharacterController / CapsuleCollider, so feet == pivot (see EditMode
        // TargetHighlightGetFeetPositionTests for collider cases).
        var expected = targetGo.transform.position + Vector3.up * 0.02f;
        Assert.AreEqual(expected.x, highlight.transform.position.x, 1e-5f);
        Assert.AreEqual(expected.y, highlight.transform.position.y, 1e-5f);
        Assert.AreEqual(expected.z, highlight.transform.position.z, 1e-5f);
      } finally {
        highlight.SetTarget(null);
        Object.DestroyImmediate(targetGo);
      }
    }

    [Test]
    public void SetTarget_Null_DisablesRingImmediately() {
      _go = new GameObject(nameof(TargetHighlight));
      var lr = _go.AddComponent<LineRenderer>();
      var highlight = _go.AddComponent<TargetHighlight>();

      var targetGo = new GameObject("Target");
      targetGo.AddComponent<NetworkObject>();
      var targetable = targetGo.AddComponent<Targetable>();
      try {
        highlight.SetTarget(targetable);
        Assert.IsTrue(lr.enabled);
        highlight.SetTarget(null);
        Assert.IsFalse(lr.enabled);
      } finally {
        Object.DestroyImmediate(targetGo);
      }
    }

    [UnityTest]
    public IEnumerator TwoInstances_SecondAwake_BecomesInstance() {
      GameObject first = null;
      GameObject second = null;
      try {
        first = CreateHighlightGameObject("First");
        var th1 = first.GetComponent<TargetHighlight>();
        Assert.AreSame(th1, TargetHighlight.Instance);

        second = CreateHighlightGameObject("Second");
        var th2 = second.GetComponent<TargetHighlight>();
        Assert.AreSame(th2, TargetHighlight.Instance);
        Assert.AreNotSame(th1, TargetHighlight.Instance);

        Object.DestroyImmediate(second);
        second = null;
        yield return null;
        Assert.IsNull(TargetHighlight.Instance);

        Object.DestroyImmediate(first);
        first = null;
        yield return null;
        Assert.IsNull(TargetHighlight.Instance);
      } finally {
        if (second != null) {
          Object.DestroyImmediate(second);
        }
        if (first != null) {
          Object.DestroyImmediate(first);
        }
      }
    }

    static GameObject CreateHighlightGameObject(string name) {
      var go = new GameObject(name);
      go.AddComponent<LineRenderer>();
      go.AddComponent<TargetHighlight>();
      return go;
    }
  }
}
