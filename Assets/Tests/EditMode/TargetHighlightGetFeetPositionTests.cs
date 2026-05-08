using NUnit.Framework;
using UnityEngine;

namespace Forbes.Tests.EditMode {
  /// <summary>
  /// Covers <see cref="TargetHighlight.GetFeetPosition"/> without PlayMode timing.
  /// </summary>
  public class TargetHighlightGetFeetPositionTests {
    [Test]
    public void GetFeetPosition_NoCollider_ReturnsTransformPosition() {
      var go = new GameObject();
      go.transform.position = new Vector3(1f, 5f, -2f);
      try {
        Vector3 feet = TargetHighlight.GetFeetPosition(go.transform);
        Assert.AreEqual(go.transform.position, feet);
      } finally {
        Object.DestroyImmediate(go);
      }
    }

    [Test]
    public void GetFeetPosition_CharacterController_ReturnsCapsuleBottomWorldSpace() {
      var go = new GameObject();
      go.transform.position = new Vector3(0f, 10f, 0f);
      var cc = go.AddComponent<CharacterController>();
      cc.center       = Vector3.zero;
      cc.height       = 2f;
      cc.radius       = 0.5f;
      cc.skinWidth    = 0.08f;
      try {
        Vector3 feet = TargetHighlight.GetFeetPosition(go.transform);
        float expectedY =
          go.transform.position.y + cc.center.y - (cc.height * 0.5f - cc.skinWidth);
        Assert.AreEqual(expectedY, feet.y, 1e-4f,
          "Feet Y must match CharacterController bottom (centre minus half-extents minus skin).");
        Assert.AreEqual(go.transform.position.x, feet.x, 1e-6f);
        Assert.AreEqual(go.transform.position.z, feet.z, 1e-6f);
      } finally {
        Object.DestroyImmediate(go);
      }
    }

    [Test]
    public void GetFeetPosition_CapsuleColliderChild_ReturnsBottomAlongWorldUp() {
      var root = new GameObject();
      root.transform.position = Vector3.zero;
      var child = new GameObject("CapsuleHost");
      child.transform.SetParent(root.transform, worldPositionStays: false);
      child.transform.localPosition = new Vector3(0f, 1f, 0f);
      child.transform.localRotation = Quaternion.identity;
      child.transform.localScale    = Vector3.one;
      var cap = child.AddComponent<CapsuleCollider>();
      cap.center = Vector3.zero;
      cap.radius = 0.4f;
      cap.height = 2f;
      try {
        Vector3 feet = TargetHighlight.GetFeetPosition(root.transform);
        float halfHeight = cap.height * 0.5f * child.transform.lossyScale.y;
        float expectedY  = child.transform.position.y - halfHeight;
        Assert.AreEqual(expectedY, feet.y, 1e-4f);
      } finally {
        Object.DestroyImmediate(root);
      }
    }
  }
}
