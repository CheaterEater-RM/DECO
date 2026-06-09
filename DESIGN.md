# Doors Expanded Continued Overhaul - Design

This repo is scaffolded from the RimWorld mod templates for DECO, short for Doors Expanded Continued Overhaul.

The authoritative rewrite design currently lives in `DoorsExpanded_Rewrite_DESIGN.md`. That document defines DECO as a greenfield RimWorld 1.6 rewrite inspired by Doors Expanded, with the implementation split into:

- M0: pure XML catalogue on vanilla `Building_Door` and `Building_MultiTileDoor`
- M1: animated doors with a draw-only `Building_MultiTileDoor` subclass
- M2: remote doors and buttons
- M3: polish, CE audit, docs, and save-behavior notes

Scaffold identity:

- Display name: Doors Expanded Continued Overhaul
- Codespace / assembly / namespace: DECO
- Package ID: CheaterEater.DECO
- Harmony ID: com.cheatereater.deco
- Target: RimWorld 1.6, net48

No implementation code has been added in this scaffold.
