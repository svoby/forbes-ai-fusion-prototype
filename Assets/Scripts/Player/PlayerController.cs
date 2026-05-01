// PlayerController.cs
// Task #3 — Player Movement
// AC: WASD moves local player at MoveSpeed; remote player interpolated via NetworkTransform.
// AC: No rubber-banding on localhost.

using Fusion;
using UnityEngine;

namespace ForbesPrototype.Player
{
    /// <summary>
    /// Applies movement input in FixedUpdateNetwork (host-authoritative).
    /// Requires NetworkTransform on the same GameObject for position sync.
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;

        private CharacterController _cc;

        public override void Spawned()
        {
            _cc = GetComponent<CharacterController>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out NetworkInputData input)) return;

            Vector3 direction = new Vector3(input.Move.x, 0f, input.Move.y).normalized;
            _cc.Move(direction * moveSpeed * Runner.DeltaTime);
        }
    }
}
