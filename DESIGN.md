# Doors Expanded Continued Overhaul - Design

This repo is scaffolded from the RimWorld mod templates for DECO, short for Doors Expanded Continued Overhaul.

The authoritative rewrite design currently lives in `DoorsExpanded_Rewrite_DESIGN.md`. That document defines DECO as a greenfield RimWorld 1.6 rewrite inspired by Doors Expanded, with the implementation split into:

- M0: pure XML catalogue on vanilla `Building_Door` and `Building_MultiTileDoor`
- M1: animated doors with a draw-only `Building_MultiTileDoor` subclass
- M2: remote doors and buttons
- M3: polish, CE audit, docs, and save-behavior notes

Identity:

- Display name: Doors Expanded Continued Overhaul
- Codespace / assembly: DECO
- C# namespace: DoorsExpanded (save interchangeability with the original mod; see AGENTS.md)
- Package ID: CheaterEater.DECO
- Harmony ID: com.cheatereater.deco
- Target: RimWorld 1.6, net48

Save interchangeability: DECO uses the original Doors Expanded's defNames (PH_*/Heron*)
and thing class names so saves swap freely in both directions between the two mods.
Those names are frozen API (never rename); the two mods cannot be loaded together.
The implementation is entirely DECO's — only the names are shared.
