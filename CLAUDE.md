# Claude Coding Constraints — Forbes AI Fusion Prototype

This file extends [AGENTS.md](AGENTS.md) with Claude-specific guidance.

---

## Primary Reference

Read **AGENTS.md** first. All rules there apply here without exception.

---

## Claude-Specific Workflow

1. **Read the task ticket** (`docs/tasks/`) before writing any code. Confirm you understand the authority model for the feature.
2. **Write the acceptance criteria verification steps** in a code comment block at the top of the primary file you create or modify, e.g.:
   ```csharp
   // Task #3 — Player Spawn
   // AC: Host spawns player at SpawnPoint[0]; Client spawns at SpawnPoint[1].
   // AC: Both peers see both capsules within 1 tick of join.
   ```
3. **Propose the struct/class signature** before writing implementation, especially for `NetworkInputData` changes.
4. **Never exceed 150 lines** in a single file. If a class grows large, split it and ask for confirmation.

## What Claude Must Not Do

- Do not add Unity packages or NuGet references without explicit approval.
- Do not modify `NetworkProjectConfig` or Fusion topology settings without approval.
- Do not write `RPCs` unless state-driven design genuinely cannot solve the problem — and even then, ask first.
- Do not generate placeholder "TODO" methods that leave authority checks unimplemented.

## Preferred Patterns

```csharp
// ✅ Correct — authority-gated state mutation
public override void FixedUpdateNetwork()
{
    if (!Object.HasStateAuthority) return;
    HP -= incomingDamage;
    incomingDamage = 0;
}

// ❌ Wrong — mutating state from any peer
public override void FixedUpdateNetwork()
{
    HP -= incomingDamage; // desync!
}
```

```csharp
// ✅ Correct — input collected in OnInput, applied in FixedUpdateNetwork
public void OnInput(NetworkRunner runner, NetworkInput input)
{
    var data = new NetworkInputData();
    data.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    data.CastSpell = Input.GetKeyDown(KeyCode.Q);
    input.Set(data);
}
```

## Response Format

When generating code for this project, structure your response as:

1. **What I'm doing** — one sentence summary.
2. **Authority model** — who owns state for this feature.
3. **Files changed** — list of file paths.
4. **Code** — full file content (no partial snippets unless clarified).
5. **Manual test steps** — numbered, matching acceptance criteria.
