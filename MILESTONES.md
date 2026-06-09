# Doors Expanded Continued Overhaul - Milestones

## Completed

### M0 - Scaffold

- Created RimWorld mod directory structure.
- Materialized template metadata, language, docs, solution, and project files.
- Preserved `DoorsExpanded_Rewrite_DESIGN.md` as the authoritative pre-code design.
- Added no implementation C# files.

## In Progress

### M0 - XML catalogue

- Define slide-animated doors, gates, curtains, and jail door as XML on vanilla classes.
- Wire reused art and audio assets with MIT attribution.
- Verify vanilla `Building_MultiTileDoor` handles required widths.

Dependencies: scaffold complete.

## Planned

### M1 - Animated doors

- Add draw-only animated door type.
- Add config comp for swing, stretch, frame, and related drawing data.
- Prove the no-transpiler ghost and blueprint path before adding any patch.

Dependencies: M0 XML catalogue validated.

### M2 - Remote doors

- Add remote door, button or lever, link model, locked state, overlay cue, and interaction job.

Dependencies: M1 animated doors validated.

### M3 - Polish

- Decide label suffix behavior.
- Complete CE audit.
- Document save behavior, old-mod non-migration, attribution, and publishing checklist.

Dependencies: M2 remote doors validated.
