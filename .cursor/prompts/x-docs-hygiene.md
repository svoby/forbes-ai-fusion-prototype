---
description: Clean up repository docs/ so AI agents do not treat historical plans, audits, or next-step notes as source-of-truth context.
---

Model: Claude Sonnet 4.6, medium reasoning.
Mode: Agent / Composer.
Task type: documentation hygiene only.

Goal:
Clean up repository documentation so AI agents do not accidentally treat historical plans, audits, checkpoints, or next-step notes as source-of-truth context.

Read first:
- README.md
- AGENTS.md
- docs/architecture.md
- docs/TEST_HARNESS.md

Rules:
- Do not modify production code.
- Do not modify tests.
- Do not implement any gameplay feature.
- Do not create new plans or next-step documents.
- Do not preserve obsolete planning docs unless they contain durable architectural decisions not documented elsewhere.
- If a historical doc contains one still-useful durable rule, move that rule into the appropriate stable document, then delete or archive the historical doc.

Classify every file under docs/ as one of:
1. Stable source-of-truth documentation
2. Durable technical reference
3. Historical audit/checkpoint/plan
4. Obsolete or misleading document

Desired final state:
- docs/ contains only stable current documentation.
- No file in docs/ should contain task plans, next steps, audit conclusions, temporary checklists, or historical implementation notes.
- README.md and AGENTS.md remain the primary entry points for agents.
- docs/architecture.md describes current architecture only, not future task planning.
- docs/TEST_HARNESS.md describes how tests work, not a backlog.
- If archiving is preferred over deletion, move historical files to archive/notes/ and mark clearly:
  "Historical note. Not source of truth. Do not use as task context."
  But prefer deletion if the content is not needed.

Inspect especially:
- Any file under docs/ that contains Next Steps, Plan, Audit, TODO, Checkpoint, or future work sections.
- docs/AGENT_CONTEXT.md — verify source-of-truth table is accurate and points only to existing, current docs.
- docs/architecture.md — remove any milestone checklists or next-step planning sections; keep script map current.

Deliverable:
- Minimal documentation cleanup committed on a chore/docs-hygiene branch.
- Summary with: deleted files, archived files (if any), stable docs kept, any durable rule moved into stable docs.
