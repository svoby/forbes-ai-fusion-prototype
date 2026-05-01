// HudController.cs
// Task #7 — Minimal HUD
// AC: Local HP bar, target HP/nameplate, room status label visible at runtime.
// AC: No state mutation — reads [Networked] properties only.

using Fusion;
using ForbesPrototype.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForbesPrototype.UI
{
    /// <summary>
    /// Reads networked state each frame and updates UI elements.
    /// Must never write to any [Networked] property.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("Local Player")]
        [SerializeField] private Slider localHpBar;

        [Header("Target")]
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private Slider targetHpBar;
        [SerializeField] private TMP_Text targetNameplate;

        [Header("Room Status")]
        [SerializeField] private TMP_Text roomStatusLabel;

        private HealthSystem _localHealth;
        private TargetSelector _localTargetSelector;
        private NetworkRunner _runner;

        public void Init(NetworkRunner runner, HealthSystem localHealth, TargetSelector targetSelector)
        {
            _runner = runner;
            _localHealth = localHealth;
            _localTargetSelector = targetSelector;
        }

        private void Update()
        {
            UpdateLocalHP();
            UpdateTarget();
            UpdateRoomStatus();
        }

        private void UpdateLocalHP()
        {
            if (_localHealth == null) return;
            localHpBar.value = (float)_localHealth.HP / HealthSystem.MaxHP;
        }

        private void UpdateTarget()
        {
            if (_localTargetSelector == null || _localTargetSelector.Target == null)
            {
                targetPanel.SetActive(false);
                return;
            }

            targetPanel.SetActive(true);

            if (_localTargetSelector.Target.TryGetComponent(out HealthSystem targetHealth))
            {
                targetHpBar.value = (float)targetHealth.HP / HealthSystem.MaxHP;
                targetNameplate.text = $"Player {_localTargetSelector.Target.InputAuthority.PlayerId}";
            }
        }

        private void UpdateRoomStatus()
        {
            if (_runner == null) return;
            string role = _runner.IsServer ? "Host" : "Client";
            roomStatusLabel.text = $"{role} | Players: {_runner.ActivePlayers.GetPlayerCount()}";
        }
    }
}
