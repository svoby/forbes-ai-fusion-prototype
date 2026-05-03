using System.Runtime.CompilerServices;

// EditMode tests live in their own asmdef; expose internals so seam-2
// (NetworkCombatController.SecsToTicks) and any future internal seams stay
// reachable without widening their visibility for production callers.
[assembly: InternalsVisibleTo("Forbes.Tests.EditMode")]
