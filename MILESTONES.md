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

### Jail door identity — prisoner locks + shoot-through bars (2026-06-11)

Gave jail doors (and blast doors) a real role beyond "slower, tougher door".
Implemented (C# + XML + settings, pending in-game validation):

- **Mod settings infrastructure** (new, DECO had none): `DecoMod : Mod` +
  `DecoSettings : ModSettings` (`Source/Settings/`), drawn with vanilla
  `Listing_Standard`, auto-registered (no `modClass` in About.xml). Toggles:
  `jailBlocksPrisoners` (default on), `blastBlocksPrisoners` (default on),
  `escapingPawnsOpenOwnDoor` (off). Keyed UI labels added to `Keys.xml`.
- **Prisoner locks** (`Source/Patches/PawnCanOpen_PrisonerLock_Patch.cs`): postfix on
  `Building_Door.PawnCanOpen` (covers jail via `Building_DoorExpanded` and blast via
  `Building_DoorRemote`, whose override calls base). Only narrows the result for the
  four DECO jail/blast defs; never touches other doors. Prisoners can't open unless
  actively prison-breaking, and then only a door that cardinally borders their current
  prison cell if the escape toggle is on. This is intentionally stricter than
  "Prisoners Don't Have Keys", whose OwnDoor check is room-only
  (`OwnDoor && p.GetRoom().IsPrisonCell`): DECO matches its own "own cell door"
  setting text and avoids optimistic pathing through later DECO doors. Prisoners only
  — no slave or entity handling. Logic adapted from "Prisoners Don't Have Keys" by
  Mlie (MIT, attributed in the file header and `LICENSE`). Added the four door
  `ThingDef` DefOfs to `HeronDefOf`.
- **Shoot-through bars — vanilla** (`PH_DoorJail` in `DEx_Doors.xml`): `fillPercent
  0.25` makes the closed door Partial-fillage, so vanilla `GenGrid.CanBeSeenOver`
  lets shots through and `CoverUtility.BaseBlockChance` gives exactly 25% directional
  cover (off-axis and point-blank shots already get reduced cover by vanilla geometry).
  Explicit `holdsRoof=true` keeps roof support despite the low fill. Removed
  `isStuffableAirtight` (a ConfigError with non-Full fillage, and bars aren't airtight).
- **Air through bars**: jail door `doorTempEqualizeIntervalClosed=34` +
  `doorTempEqualizeRate=1.0` so a *closed* cell equalizes temperature like an open
  passage — the door is bars, it doesn't insulate. Blast doors unchanged (still seal).
- **Shoot-through bars — Combat Extended** (`Source/Patches/CombatExtendedCompat.cs`):
  CE treats building `fillPercent` as cover *height* (shoot-over), which is wrong for
  bars; its any-height random interception is the Plant path, hardcoded to `Plant`.
  So under CE we register a `BlockerRegistry.RegisterCheckForCollisionCallback` (all
  reflection — CE is a soft dependency, never referenced at build, no-ops cleanly when
  absent) that replicates CE's plant formula on closed jail-door cells: trajectory-
  gated, point-blank "gun-through-the-bars" exemption, and `0.25 * distance/40 *
  accuracy` intercept chance via `Rand.ChanceSeeded`. Bars block more from range,
  barely block point-blank — directionally consistent with vanilla cover and CE bushes.

Scope: blast doors stay fully solid/airtight in both combat systems; only the prisoner
lock applies to them. Save-safe: `PH_DoorJail` defName unchanged (frozen API per the
Save Interchangeability Contract); this is a def + additive-settings change, no Scribe
field rename. Build is clean (0 warnings/0 errors).

Remaining verification (in-game): zero startup errors incl. no "is airtight but Fillage
is not Full" ConfigError; settings UI toggles persist across restart; prisoner can't
path out of a jail/blast cell, prison-break + escape toggle opens own cell door only;
vanilla shoot-through + ~25% cover (and blast doors still block); closed jail cell
tracks adjacent room temperature; under CE, across-room shots intercept a fraction of
the time at varied heights while point-blank passes freely, blast doors still block;
pre-change save with existing jail doors loads and gains new behavior with no errors.

### Asymmetric door orientation + paired state sync (2026-06-11)

Implemented (C# + XML + settings, validated by user screenshot/in-game pass):

- Added additive `CompProperties_DoorExpanded` opt-in fields: `asymmetric` and
  `syncAdjacentAsymmetricPair`. Defaults are false, preserving the original
  Doors Expanded XML surface unless a DECO def opts in.
- Opted in tribal curtains (`HeronCurtainTribal*`) and the jail door
  (`PH_DoorJail`). Other single-panel doors, including remote/garage doors, remain
  unchanged unless explicitly tagged later.
- `Building_DoorExpanded` now corrects asymmetric single-panel draw direction
  toward the adjacent wall. It uses the existing `flipped` draw branch, so both the
  motion offset and directional art mirror together (curtain folds, jail handle).
- Added `syncPairedAsymmetricDoors` mod setting (default on). Matching, same-def,
  wall-bracketed adjacent asymmetric pairs sync open events, blocked-open retries,
  hold-open state, and forbid/unforbid state.
- Added a narrow `CompForbiddable.Forbidden` setter postfix for pair forbid
  mirroring. It routes through the same pair detector and recursion guard; unrelated
  doors and ambiguous layouts are ignored.

Scope and safety: pair detection requires same `ThingDef`, same axis/rotation,
aligned occupied rects, direct adjacency, and outside wall brackets. Mixed widths,
chains, missing walls, and both-wall ambiguous layouts stay independent. No
save-visible names, scribe fields, or enum values changed. Build is clean
(0 warnings/0 errors).

Fixed after wide-curtain placement review (2026-06-11):

- Wide tribal curtains now opt into `oneSidedWallSupport`. Placement rejects 2x1
  and 3x1 curtains with no adjacent wall support, with user-facing descriptions
  and a specific placement rejection message. The 1x1 curtain has no wall
  constraint and continues to infer orientation from nearby walls.
- 2x1 and 3x1 tribal curtains no longer require both vanilla multi-tile support
  sides to close. A full wall-supported side is enough; unsupported loaded or
  dev-spawned curtains still stay stuck open.
- One-sided support checks the real placed footprint but canonicalizes parallel
  rotations (`South` => `North`, `West` => `East`) for draw/pair checks, so
  rotated N/S or E/W placements do not double-mirror asymmetric curtain art.

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
- linked tribal curtains so that they move in sync when opposite one another (completed 2026-06-11 as shared asymmetric-door orientation + pair sync; curtains and jail doors now opt in)
- jail cell doors are not passable by prisoners, and they allow firing through (implemented 2026-06-11; see milestone above. Prisoner/blast locks + 25% shoot-through cover + air-through + CE compat.)
- Stop forbidding doors with secure remotely button (is this necessary?) (complete, no forbidding with DECO)
- Ensure that if we use a "build remote" from a door, it gets linked automatically 
- Allow labelling remote switches (completed)
- Add clearer lines for remote switches to doors (completed)
- Check containment with Anomaly (audited 2026-06-10, no code change needed — see below)
- Ensure that creatures and anything else can't get through (resolved by inheritance — see below)
- Allow Rimworld security door to use buttons
- Balance and tuning: costs, HP, opening/closing speeds

### Anomaly Containment Audit (2026-06-10)

Audited how the Anomaly DLC's entity containment interacts with DECO doors, especially the
3x2 blast door `PH_DoorThickBlastDoor`. **Conclusion: no bug; no code change.** Cleared by
inheritance + an invariant in AGENTS.md (hard rule 13).

Why it works:

- Every DECO door is a `Building_Door` subclass, so vanilla
  `StatWorker_ContainmentStrength.CalculateDoorStats` counts them. It collects doors into a
  `HashSet<Building_Door>`, deduping by object reference — so the 3x2 door (or any multi-tile
  door) is counted **once** at its full HitPoints, never per-cell.
- `Building_Door.ContainmentBreached` (`openInt && ticksOpen >= 600`) and `BlocksPawn` /
  `CanPhysicallyPass` are inherited untouched. `Building_DoorRemote` only overrides
  `PawnCanOpen` to return false *more* often (when remotely secured) — it blocks more, never
  less, so "creatures can't get through" holds.
- The remote 30-tick `DoorOpen(120)` refresh extends the close timer but does **not** reset
  `ticksOpen`, so a remotely-held-open blast door still reads as breached after 10 s (correct).

Caveats (not bugs, no shipped def affected):

- A door authored as `FreePassage` (DoorType => `AlwaysOpen`) would read as permanently
  breached. No DECO def uses FreePassage (all are Standard/Stretch/DoubleSwing). Do not put a
  FreePassage door on a containment room.
- Curtains (`isStuffableAirtight=false`, ~50-150 HP) give low door contribution (HP/5) — low
  containment strength, not a breach. Working as intended.

In-game verification still pending: build a holding room with a 3x2 blast door + platform +
entity, confirm the ContainmentStrength breakdown shows the door HP line with doorCount=1,
hold it open 10 s to confirm "Door forced open" zeroes the contribution (manually and via the
remote button), and confirm a secured door blocks an escaping entity.
