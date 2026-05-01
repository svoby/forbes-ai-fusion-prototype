// NetworkInputData.cs
// Shared input struct — must contain only blittable types.

using Fusion;
using UnityEngine;

namespace ForbesPrototype.Player
{
    /// <summary>
    /// Per-tick input sent from InputAuthority to StateAuthority.
    /// All fields must be blittable (no reference types).
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 Move;
        public NetworkBool CastSpell; // Q — instant spell
        public NetworkBool TabTarget; // Tab — cycle target
    }
}
