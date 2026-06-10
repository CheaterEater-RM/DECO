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

Save interchangeability pass (2026-06-10, user decision):

- Adopted the original Doors Expanded's save-visible names so saves swap freely both
  ways with zero migration code: namespace `DoorsExpanded`, original class names
  (`Building_DoorExpanded`, stub `Building_DoorRemote` until M2), original defNames
  (`PH_*`/`Heron*`), original comp field names + `DoorType` enum (incl. parse-only
  `remoteDoor`/`tempEqualizeRate` and a working `FreePassage` via `AlwaysOpen`).
- Jail door moved to the expanded class with the original config; blast doors moved to
  the `Building_DoorRemote` stub (what old saves recorded); added `PH_AutodoorDouble`
  and `PH_AutodoorTriple` for catalogue parity.
- `About.xml` declares `incompatibleWith jecrell.doorsexpanded` (names collide).
- These names are frozen API — documented in AGENTS.md's Save Interchangeability
  Contract. Old DECO test saves (DEx_*/DECO.*) lose their built doors once.
- M2 must use the original's remote defNames, `Building_DoorRemoteButton`, and scribe
  field names (`button`, `securedRemotely`, `linkedDoors`, `buttonOn`,
  `needsToBeSwitched`) so old remote state migrates when M2 lands.

Remaining verification (in-game):

- Confirm zero startup errors in `Player.log`.
- Verify multi-tile doors now render full-width and slide correctly in all rotations.
- Verify 3x1 (`DEx_DoorTriple`/`DEx_CurtainTriple`) and especially 3x2
  (`DEx_BlastDoorThick`) draw, path, support-wall ghost, and `StuckOpen` behavior.
- Verify ghost/blueprint visuals and mod-removal degradation.

Dependencies: scaffold complete.

## Planned

### M2 - Remote doors

Implemented (C# + XML, pending in-game validation):

- `Building_DoorRemote` now keeps the original save fields (`button`,
  `securedRemotely`) and links to exactly one remote controller.
- `Building_DoorRemoteButton` now keeps the original save fields (`linkedDoors`,
  `buttonOn`, `needsToBeSwitched`) and can link multiple doors.
- The controller's `buttonOn` state is authoritative. Linked doors continually
  reconcile toward that state: on keeps refreshing `DoorOpen(...)`; off keeps
  retrying `DoorTryClose()` if blocked. Remote state does not write vanilla
  `holdOpenInt`, avoiding stale hold-open behavior when toggling secured mode.
- `securedRemotely` only controls pawn permission: when the linked controller is
  off, `PawnCanOpen` returns false. The button/lever state still owns open/close.
- Added `PH_DoorButton`, `PH_DoorLever`, and `PH_DoorRemoteSingle/Double/Triple`
  defs under Microelectronics, plus keyed UI labels.

Remaining verification (in-game):

- Confirm no startup XML or texture errors for the new button/lever and remote
  garage door defs.
- Verify one door cannot stay linked to two controllers, while one controller can
  drive multiple doors.
- Verify controller on holds linked doors open and controller off retries closing
  after a blocking pawn/item moves.
- Verify disabling/enabling secured remotely never leaves a stale vanilla
  hold-open state behind.
- Verify power loss prevents powered remote actuation, and the door catches up
  to the controller state once power returns.

### M1 - Animated doors

- Add draw-only animated door type.
- Add config comp for swing, stretch, frame, and related drawing data.
- Prove the no-transpiler ghost and blueprint path before adding any patch.

Dependencies: M0 XML catalogue validated.

### M3 - Polish

- Decide label suffix behavior.
- Complete CE audit.
- Document save behavior, old-mod non-migration, attribution, and publishing checklist.

Dependencies: M2 remote doors validated.


### User Todos:
- linked tribal curtains so that they move in sync when opposite one another
- jail cell doors are not passable by prisoners, and they allow firing through (with extra unlock time)
- Stop forbidding doors with secure remotely button (is this necessary?)
- Ensure that if we use a "build remote" from a door, it gets linked automatically
- Allow labelling remote switches
- Add clearer lines for remote switches to doors
- Check containment with Anomaly
- Ensure that creatures and anything else can't get through
- Allow Rimworld security door to use buttons