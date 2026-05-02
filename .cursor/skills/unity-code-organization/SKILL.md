---
name: unity-code-organization
description: Split Unity C# code into feature folders, thin NetworkBehaviours, and plain C# services. Use when refactoring spaghetti scripts, adding assemblies, or planning Assets/Scripts layout for this Fusion prototype.
disable-model-invocation: true
---

# Unity Code Organization (this repo)

## When to use

- User asks to split a large script, reduce god classes, or reorganize `Assets/Scripts`.
- Adding a feature that would bloat an existing `NetworkBehaviour`.

## Read first

- Project doc: `docs/UnityCSharp-CodeOrganization.md` (folder template + official links).
- Rules: `.cursor/rules/unity-architecture.mdc`, `.cursor/rules/fusion-networking.mdc`, `.cursor/rules/csharp-style.mdc`.

## Rules of thumb

1. **One primary responsibility** per public type; multiple small files beat one mega file.
2. **Fusion**: gameplay mutations on State Authority; simulation in `FixedUpdateNetwork`; clients send intent via input structs only.
3. **Split order**: extract **plain C#** helpers first (no `NetworkBehaviour`), then thin the MonoBehaviour to wiring + calls.
4. **UI** (`UI/`): never spawn network objects or change `Networked` state; read runner / local view state only.
5. **Moves**: prefer **small PR-sized steps** — move one feature at a time, fix compile, play-test host + client.

## Output after structural work

- List new/changed paths under `Assets/Scripts/...`.
- Note any `.asmdef` or namespace changes.
- Short manual test: connect, move, spell, HUD still OK.
