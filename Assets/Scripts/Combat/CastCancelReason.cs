/// <summary>Why an in-progress cast-time spell was interrupted (state authority only; not networked).</summary>
public enum CastCancelReason {
  None           = 0,
  Movement       = 1,
  Jump           = 2,
  NewSpell       = 3,
  Death          = 4,
  InvalidTarget  = 5,
  Manual         = 6,
}
