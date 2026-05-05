---
name: manual-audit-docs-before-coding
description: >-
  Before implementing a feature, audit documentation against the actual codebase.
  Finds stale script/file listings, removed or renamed files still referenced in docs,
  outdated status claims ("not yet implemented" that are now shipped), and wrong paths.
  Use when about to implement a feature after being told to read docs first, when the
  user says "audit current state and implement", or when docs and code are known to
  drift. Keeps doc fixes in a separate stage from the implementation diff.
disable-model-invocation: true
---

# Audit Docs Before Coding

Correct documentation before the first line of implementation code so the diff stays clean.

## Workflow

### 1. Locate structural docs

Find files that make claims about actual code:
- `README*`, `AGENTS.md`, `CLAUDE.md` — script layouts, feature lists
- `docs/*.md` — architecture, pipeline diagrams, terminology
- Any doc with a file tree, class list, or "not yet implemented" marker

Use `Glob` (`docs/*.md`, `README*`) and project-specific context clues to find them quickly.

### 2. Map every claim to reality

For each structural claim, verify with `Glob` or `Shell (dir/ls)`:

| Claim type | Check |
|---|---|
| File listed in docs | Does the file exist at that path? |
| File not listed | Does it exist and belong in docs? |
| "Not yet implemented" | Is it actually implemented? |
| Class / method name | Does the symbol match the actual source? |
| Folder structure | Does it match `Glob` output? |

### 3. Classify every discrepancy

| Category | Action |
|---|---|
| **Stale: removed** — doc lists a file that no longer exists | Delete the reference |
| **Stale: renamed** — doc uses old name | Update to current name |
| **Stale: status** — "not yet implemented" but shipped | Remove the qualifier |
| **Stale: description** — doc describes wrong behaviour | Correct it |
| **Missing** — exists in code, absent from docs | Add it |
| **Correct** — matches reality | No action |

### 4. Fix docs first, then implement

Apply all doc corrections before writing any implementation code. This keeps documentation fixes and feature changes in separate, reviewable stages.

### 5. Note findings that affect the implementation

If a doc said "future work" for something that already exists, or if a listed dependency is gone, that changes what needs building. Record it before proceeding.

## Summary before implementation

After the audit, write a short note:

```
Docs audit: N stale entries fixed, M entries added.
Stale fixed: [brief list]
Missing added: [brief list]
Proceeding with implementation.
```

## Scope limits

- Fix only what is demonstrably wrong — do not restructure docs opportunistically.
- Do not implement product behaviour during the audit pass.
- This covers the *pre-coding* audit only; post-implementation diff audit is handled by the `post-feature-diff-audit` rule.
