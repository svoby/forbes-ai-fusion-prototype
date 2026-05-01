# Task Template

Copy this file to `docs/tasks/sprint-N-feature-name.md` and fill in each section.

---

## Task #N — Feature Name

**Sprint:** N  
**Estimate:** S / M / L  
**Status:** [ ] Backlog | [ ] In Progress | [ ] Done

---

### Objective

_One sentence: what this task accomplishes and why it matters for the POC._

---

### Authority Model

| Concern | Owner | Notes |
|---|---|---|
| State mutation | StateAuthority / InputAuthority | e.g. HP only written by StateAuthority |
| Input collection | InputAuthority | Struct fields only |
| UI reads | Any peer | No writes |

_Describe who owns what state and why. Reference `AGENTS.md` if needed._

---

### Files Affected

| File | Change Type | Notes |
|---|---|---|
| `Assets/Scripts/Feature/FileName.cs` | Create / Modify | Brief description |

---

### Acceptance Criteria

- [ ] AC1: _Specific, observable, binary outcome._
- [ ] AC2:
- [ ] AC3:

_Each criterion must be verifiable by a human tester in under 30 seconds._

---

### Manual Test Steps

1. _Setup: describe starting state (e.g. "two peers connected, both alive")._
2. _Action: what to do._
3. _Expected result: what you should see on screen or in console._
4. _(Repeat for edge cases.)_

---

### Implementation Notes

_Optional: algorithm sketch, Fusion API to use, known gotchas._

```csharp
// Example: authority guard pattern
public override void FixedUpdateNetwork()
{
    if (!Object.HasStateAuthority) return;
    // mutate state here
}
```

---

### Out of Scope

_What this task explicitly does NOT include, to prevent scope creep._
