---
name: unity-feature-implementation
description: Implements Unity gameplay features with Photon Fusion authority and tick-safe logic. Use when adding movement, targeting, abilities, HP systems, respawn, or multiplayer gameplay scripts.
disable-model-invocation: true
---

# Unity Feature Implementation

## Purpose
Implement one gameplay feature at a time with minimal risk.

## Required workflow
1. Clarify feature scope (input, state, authority, UI impact).
2. Identify touched scripts and prefabs.
3. Implement smallest working path first.
4. Verify in 2-client test.
5. Report result with quick test steps.

## Networking constraints
- State changes must run on State Authority.
- Client sends intent/input.
- Simulation logic runs in `FixedUpdateNetwork`.

## Output format
When finishing a feature, provide:
- changed files
- authority model used
- manual test checklist
- known limitations
