// SpellCaster.cs
// Task #5 — Instant Spell (Q)
// AC: Q with no target does nothing; Q on live target reduces HP by 20 (authoritative).
// AC: Cannot reduce HP below 0 (clamped in HealthSystem.TakeDamage).

using Fusion;
using ForbesPrototype.Combat;
using UnityEngine;

namespace ForbesPrototype.Combat
{
    /// <summary>
    /// Fires an instant-damage spell on the current target when the CastSpell input is set.
    /// Damage is applied by StateAuthority only, via HealthSystem.TakeDamage.
    /// </summary>
    [RequireComponent(typeof(TargetSelector))]
    public class SpellCaster : NetworkBehaviour
    {
        [SerializeField] private int spellDamage = 20;

        private TargetSelector _targetSelector;
        private bool _castWasPressed;

        public override void Spawned()
        {
            _targetSelector = GetComponent<TargetSelector>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (!GetInput(out ForbesPrototype.Player.NetworkInputData input)) return;

            // Rising-edge detect — one cast per key press
            bool castDown = input.CastSpell && !_castWasPressed;
            _castWasPressed = input.CastSpell;

            if (!castDown) return;

            NetworkObject target = _targetSelector.Target;
            if (target == null) return;

            if (target.TryGetComponent(out HealthSystem health))
                health.TakeDamage(spellDamage);
        }
    }
}
