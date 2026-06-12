# Doors Expanded Continued Overhaul - Design

This repo contains DECO, short for Doors Expanded Continued Overhaul: a RimWorld 1.6 clean-room rewrite inspired by Doors Expanded.

Current implementation note: DECO now has a shared asymmetric-door helper for
single-panel doors that opt in through `CompProperties_DoorExpanded`. Curtains and
the jail door mirror their moving panel toward the adjacent wall, and matching
wall-bracketed pairs can sync open/close, hold-open, and forbid state behind the
`syncPairedAsymmetricDoors` mod setting.

`DoorsExpanded_Rewrite_DESIGN.md` is kept as the architecture record for the rewrite.
`IMPLEMENTATION.md` records the vanilla source checks and implementation notes that
matter when changing door behavior.

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
