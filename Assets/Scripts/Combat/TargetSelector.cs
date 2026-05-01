// TargetSelector.cs
// Task #4 — Tab Target
// AC: Tab selects nearest enemy; [Networked] Target seen by all peers.

using Fusion;
using UnityEngine;

namespace ForbesPrototype.Combat
{
    /// <summary>
    /// Selects the nearest enemy on Tab input.
    /// Target is networked so both peers see the same selection.
    /// </summary>
    public class TargetSelector : NetworkBehaviour
    {
        [Networked] public NetworkObject Target { get; private set; }

        private bool _tabWasPressed;

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out ForbesPrototype.Player.NetworkInputData input)) return;

            // Rising-edge detect for Tab (prevent hold spam)
            bool tabDown = input.TabTarget && !_tabWasPressed;
            _tabWasPressed = input.TabTarget;

            if (!tabDown) return;

            if (Object.HasStateAuthority)
            {
                Target = FindNearestEnemy();
            }
        }

        private NetworkObject FindNearestEnemy()
        {
            float best = float.MaxValue;
            NetworkObject nearest = null;

            foreach (var nb in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(nb, out NetworkObject obj)) continue;
                if (obj == Object) continue; // skip self

                float dist = Vector3.Distance(transform.position, obj.transform.position);
                if (dist < best)
                {
                    best = dist;
                    nearest = obj;
                }
            }

            return nearest;
        }
    }
}
