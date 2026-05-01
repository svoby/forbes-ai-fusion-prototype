// HealthBar.cs
// Utility component — drives a UI Slider from a HealthSystem reference.
// Attach directly to a world-space or screen-space Slider for per-player HP display.

using ForbesPrototype.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ForbesPrototype.UI
{
    /// <summary>
    /// Mirrors HP from a HealthSystem onto a UI Slider every frame.
    /// Read-only — does not mutate any networked state.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        private HealthSystem _source;

        public void SetSource(HealthSystem source)
        {
            _source = source;
        }

        private void Update()
        {
            if (_source == null || slider == null) return;
            slider.value = (float)_source.HP / HealthSystem.MaxHP;
        }
    }
}
