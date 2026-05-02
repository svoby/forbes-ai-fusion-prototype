using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative spell casting, GCD, and per-spell cooldowns for one player.
/// Replaces the legacy <c>PlayerCombat</c> component.
/// <para>
/// Authority model (Fusion Shared Mode):
/// The player is their own State Authority. All networked properties below are
/// written only on the player's own machine. Other clients observe them to render
/// cast bars, cooldown timers, etc.
/// </para>
/// <para>
/// Time source: all timers are stored as Fusion tick integers so they survive
/// host migration, late-join, and multi-client editor testing.  Seconds are
/// derived from ticks via <c>Runner.DeltaTime</c> on the render side.
/// </para>
/// </summary>
public class NetworkCombatController : NetworkBehaviour {
  const float GcdSec = 1.0f;

  // --- Networked cast state ---

  /// <summary>Spell ID currently being cast; 0 = idle.</summary>
  [Networked] public byte      CurrentSpellId  { get; set; }

  /// <summary>Target being cast on (for validation at cast completion).</summary>
  [Networked] public NetworkId CastTarget      { get; set; }

  /// <summary>Tick when the current cast started (for progress bar).</summary>
  [Networked] public int       CastStartTick   { get; set; }

  /// <summary>Tick when the current cast resolves; if Tick >= CastEndTick the spell fires.</summary>
  [Networked] public int       CastEndTick     { get; set; }

  /// <summary>Tick when the global cooldown expires.</summary>
  [Networked] public int       GcdEndTick      { get; set; }

  // Individual spell cooldowns (three spells; extend if more are added).
  [Networked] public int Cooldown1EndTick { get; set; }
  [Networked] public int Cooldown2EndTick { get; set; }
  [Networked] public int Cooldown3EndTick { get; set; }

  /// <summary>Last cast failure reason; shown in HUD for a few seconds.</summary>
  [Networked] public byte LastFailReason { get; set; }

  /// <summary>Tick at which <see cref="LastFailReason"/> was set; HUD hides after ~2 s.</summary>
  [Networked] public int LastFailTick { get; set; }

  // ---

  /// <summary>True while a cast-time spell is in flight; <see cref="PlayerMovement"/> uses this to freeze movement.</summary>
  public bool IsCasting => CurrentSpellId != 0 && Runner != null && Runner.Tick < CastEndTick;

  /// <summary>0–1 cast progress; safe to use in render without Runner null check (IsCasting guards it).</summary>
  public float CastProgress {
    get {
      int total = CastEndTick - CastStartTick;
      return total <= 0 ? 0f : Mathf.Clamp01((float)(Runner.Tick - CastStartTick) / total);
    }
  }

  Health         _health;
  NetworkButtons _prevButtons;

  void Awake() {
    _health = GetComponent<Health>();
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority) {
      return;
    }

    if (_health != null && _health.IsDead) {
      ClearCastState();
      return;
    }

    // Resolve a cast-time spell when the timer expires.
    if (CurrentSpellId != 0 && Runner.Tick >= CastEndTick) {
      ResolveCast();
      return;
    }

    if (!GetInput(out GameplayInput input)) {
      return;
    }

    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Spell1)) { TryStartCast(1, input.TargetId); }
    else if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Spell2)) { TryStartCast(2, input.TargetId); }
    else if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Spell3)) { TryStartCast(3, input.TargetId); }

    _prevButtons = input.Buttons;
  }

  // ---- Cast initiation ----

  void TryStartCast(byte spellId, NetworkId targetId) {
    var spell = SpellRegistry.Get(spellId);
    if (!spell.IsValid) {
      return;
    }

    int cooldownEnd = GetCooldownEndTick(spellId);

    if (!CombatValidator.TryValidate(
          Runner, transform, targetId, spell,
          Runner.Tick, GcdEndTick, cooldownEnd,
          isAlreadyCasting: CurrentSpellId != 0,
          out var targetHealth, out var failReason)) {
      SetFailReason(failReason);
      ForbesLog.Net($"Cast rejected: {failReason} spell={spell.Name}", this);
      return;
    }

    // GCD fires immediately on successful cast request.
    if (spell.TriggersGcd) {
      GcdEndTick = Runner.Tick + SecsToTicks(GcdSec);
    }

    int castTicks = SecsToTicks(spell.CastTimeSec);

    if (castTicks == 0) {
      // Instant spell: apply damage directly.
      targetHealth.DealDamageRpc(spell.Damage);
      SetCooldownEndTick(spellId, Runner.Tick + SecsToTicks(spell.CooldownSec));
      ForbesLog.Net($"Instant cast: {spell.Name} -> dmg {spell.Damage}", this);
    } else {
      // Cast-time spell: set networked state; damage fires in ResolveCast.
      CurrentSpellId = spellId;
      CastTarget     = targetId;
      CastStartTick  = Runner.Tick;
      CastEndTick    = Runner.Tick + castTicks;
      // Cooldown begins at cast start (matches WoW: you can't bypass CD by cancelling).
      SetCooldownEndTick(spellId, Runner.Tick + castTicks + SecsToTicks(spell.CooldownSec));
      ForbesLog.Net($"Cast started: {spell.Name} castTicks={castTicks}", this);
    }
  }

  // ---- Cast resolution ----

  void ResolveCast() {
    var spell = SpellRegistry.Get(CurrentSpellId);

    // Re-validate: target might have died or walked out of range during cast.
    if (CombatValidator.TryValidate(
          Runner, transform, CastTarget, spell,
          Runner.Tick, gcdEndTick: 0, cooldownEndTick: 0,
          isAlreadyCasting: false,
          out var targetHealth, out var failReason)) {
      targetHealth.DealDamageRpc(spell.Damage);
      ForbesLog.Net($"Cast resolved: {spell.Name} -> dmg {spell.Damage}", this);
    } else {
      SetFailReason(failReason);
      ForbesLog.Net($"Cast resolved but invalid at completion: {failReason}", this);
    }

    ClearCastState();
  }

  // ---- Helpers ----

  void ClearCastState() {
    CurrentSpellId = 0;
    CastTarget     = default;
    CastStartTick  = 0;
    CastEndTick    = 0;
  }

  void SetFailReason(CombatFailReason reason) {
    LastFailReason = (byte)reason;
    LastFailTick   = Runner.Tick;
  }

  int GetCooldownEndTick(byte spellId) {
    return spellId switch {
      1 => Cooldown1EndTick,
      2 => Cooldown2EndTick,
      3 => Cooldown3EndTick,
      _ => 0,
    };
  }

  void SetCooldownEndTick(byte spellId, int tick) {
    switch (spellId) {
      case 1: Cooldown1EndTick = tick; break;
      case 2: Cooldown2EndTick = tick; break;
      case 3: Cooldown3EndTick = tick; break;
    }
  }

  /// <summary>Converts seconds to tick count using the runner's fixed tick rate.</summary>
  int SecsToTicks(float seconds) {
    return Mathf.CeilToInt(seconds * Runner.TickRate);
  }
}
