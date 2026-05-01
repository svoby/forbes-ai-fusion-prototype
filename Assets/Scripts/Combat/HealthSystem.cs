// HealthSystem.cs
// Task #6 — HP Sync, Death, & Respawn
// AC: HP starts at 100; damage reflected on both peers within 1 tick.
// AC: Death hides capsule; respawn after 5 seconds at original spawn point.

using Fusion;
using UnityEngine;

namespace ForbesPrototype.Combat
{
    /// <summary>
    /// Manages networked HP, death detection, and timed respawn.
    /// All mutations are StateAuthority-only.
    /// </summary>
    public class HealthSystem : NetworkBehaviour
    {
        public const int MaxHP = 100;
        private const float RespawnDelay = 5f;

        [Networked] public int HP { get; private set; }
        [Networked] private TickTimer RespawnTimer { get; set; }

        [SerializeField] private GameObject visualRoot; // capsule mesh root
        private Vector3 _spawnPosition;

        public bool IsDead => HP <= 0;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
                HP = MaxHP;

            _spawnPosition = transform.position;
            UpdateVisuals();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (IsDead && RespawnTimer.Expired(Runner))
                Respawn();

            UpdateVisuals();
        }

        /// <summary>
        /// Apply damage. Must be called only from StateAuthority context.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (!Object.HasStateAuthority) return;
            if (IsDead) return;

            HP = Mathf.Max(0, HP - amount);

            if (HP == 0)
                Die();
        }

        private void Die()
        {
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, RespawnDelay);
        }

        private void Respawn()
        {
            HP = MaxHP;
            transform.position = _spawnPosition;
            RespawnTimer = default;
        }

        private void UpdateVisuals()
        {
            if (visualRoot != null)
                visualRoot.SetActive(!IsDead);
        }
    }
}
