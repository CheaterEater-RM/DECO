# Doors Expanded Continued Overhaul - Milestones

## Completed

### M0 - Scaffold

- Created RimWorld mod directory structure.
- Materialized template metadata, language, docs, solution, and project files.
- Preserved `DoorsExpanded_Rewrite_DESIGN.md` as the authoritative pre-code design.
- Added no implementation C# files.

## In Progress

### M0 - XML catalogue

Implemented (pure XML, no assembly):

- Door catalogue in `Defs/ThingDefs_Buildings/DEx_Doors.xml` on vanilla classes:
  large doors (`DEx_DoorDouble` 2x1, `DEx_DoorTriple` 3x1), curtains
  (`DEx_CurtainTribal` 1x1, `DEx_CurtainDouble` 2x1, `DEx_CurtainTriple` 3x1),
  `DEx_DoorJail`, `DEx_Gate`, and blast doors (`DEx_BlastDoorSingle` 1x1,
  `DEx_BlastDoor` 2x1, `DEx_BlastDoorThick` 3x2).
- Abstract bases in `DEx_DoorBases.xml`: `DExDoorBase` (Building_Door, 1x1) and
  `DExMultiDoorBase` (Building_MultiTileDoor, Blueprint_Build +
  useBlueprintGraphicAsGhost + PlaceWorker_MultiCellDoor).
- Sounds ported to `Defs/SoundDefs/DEx_Sounds.xml`; art + audio copied under
  `Textures/.../DEx/` and `Sounds/DEx/` with MIT attribution.
- Research folded into vanilla: curtains/large doors ungated; jail door + gate =>
  Smithing; blast doors => Machining. (Remote/garage doors are M2 => Microelectronics.)
- 1x1 doors (curtain, jail, blast single) are pure vanilla `Building_Door`.
- Multi-tile doors use `DECO.Building_DoorExpanded : Building_MultiTileDoor` with a
  custom full-width slide `DrawAt` (`Source/Buildings/Building_DoorExpanded.cs`,
  config `Source/Comps/CompProperties_DoorExpanded.cs`). This was needed because the
  reused `_Mover` art is authored at FULL door width; vanilla's half-width two-leaf
  draw squished it into broken "inner pieces". The M1 Standard-slide draw was brought
  forward to fix this. Swing/stretch/frames remain later work; remote doors are M2.

Fixed after first in-game test (2026-06-10):

- Curtains are no longer minifiable (they are doors; minifiable made them haulable
  and triggered a missing-mass config error).
- All 1x1 doors now set `uiIconPath` (vanilla `GhostUtility.GhostGraphicFor` NREs on
  a null icon path for non-support doors).

Fixed after second in-game test (2026-06-10), all by matching the original source:

- Blast doors draw their static frame layers (`doorFrame`/`doorFrameSplit`) and async
  second leaf; the frames are essential to the door reading correctly.
- 1x1 blast door draws TWO mini leaves (the original sets no `singleDoor` on it).
- Curtains are `Stretch` doors (single full-width sheet shrinking to 20% width),
  not Standard sliders; `DrawStretchParams` ported verbatim, stretch defaults
  computed in the comp's `ResolveReferences` like the original.
- Gate uses `DoubleSwing`: swings on east/west facings, parts/slides on north/south
  (faithful to the original, which never swings N/S).
- Ported the original's `DoorRotationAt` + `SpawnSetup` rotation forcing:
  non-rotatable 1x1/stretch doors auto-rotate; `rotatesSouth=false` doors
  (gate, blast doors) force south to north.

Remaining verification (in-game):

- Confirm zero startup errors in `Player.log`.
- Verify multi-tile doors now render full-width and slide correctly in all rotations.
- Verify 3x1 (`DEx_DoorTriple`/`DEx_CurtainTriple`) and especially 3x2
  (`DEx_BlastDoorThick`) draw, path, support-wall ghost, and `StuckOpen` behavior.
- Verify ghost/blueprint visuals and mod-removal degradation.

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
