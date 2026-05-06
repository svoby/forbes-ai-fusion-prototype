using Fusion;

public partial class NetworkCombatController {
  void SetCombatFeedback(CombatFeedbackReason reason) {
    if (reason == CombatFeedbackReason.None) {
      return;
    }
    LastCombatFeedbackReason = (byte)reason;
    LastCombatFeedbackTick   = Runner.Tick;
  }

  /// <summary>
  /// Sends a cosmetic impact RPC to all clients immediately after authoritative
  /// damage is dispatched. Called only on State Authority; never mutates gameplay
  /// state on any client.
  /// </summary>
  void DispatchImpactVisual(byte spellId, NetworkId targetId) {
    RpcOnSpellImpact(spellId, targetId);
  }

  /// <summary>
  /// Received on every client (including host) after a spell successfully damages
  /// its target. Delegates to <see cref="SpellImpactView"/> for the local visual;
  /// if the component is absent the call is a no-op.
  /// </summary>
  [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
  void RpcOnSpellImpact(byte spellId, NetworkId targetId) {
    _impactView?.OnSpellImpact(spellId, targetId);
  }
}
