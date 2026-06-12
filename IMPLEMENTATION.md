# DECO Implementation Notes

Working source snapshot: RimWorld `1.6.4850` decompiled source.

This document is the implementation-side companion to `DoorsExpanded_Rewrite_DESIGN.md`. It records the vanilla code references DECO should lean on before changing door behavior. The repo identity is DECO: package `CheaterEater.DECO`, Harmony ID `com.cheatereater.deco`, assembly `DECO`, and save-compatible C# namespace `DoorsExpanded`.

## Implementation Shape

Vanilla-first catalogue:

- 1x1 doors, jail doors, and simple curtains use `Building_Door`.
- 2x1 and wider doors build on `Building_MultiTileDoor` behavior through `Building_DoorExpanded`.
- Vanilla still owns wall support checks, power, forbidding, pathing, reachability, temperature exchange, hold-open gizmos, and basic ghost/blueprint rendering wherever possible.

Scoped C#:

- `Building_DoorExpanded : Building_MultiTileDoor`, draw-only where possible.
- Config comp carries DECO draw data, but defs set `thingClass` explicitly.
- Custom blueprint or ghost code should be scoped to DECO classes/defs, not global `Thing.DrawAt`.
- Asymmetric single-panel doors opt in with config fields rather than by inference.
  Curtains and jail doors use this to mirror their moving panel toward the adjacent
  wall and, when enabled in settings, sync matching paired doors.
- Wide curtains also opt into one-sided wall support. They still require wall
  support, but do not require vanilla multi-tile door walls on both sides.

Remote control:

- Remote state should override normal open permission, not replace vanilla door pathing.
- Link state is reference-scribed and scrubbed, following the pattern from the design doc.

## Vanilla Door Model

`RimWorld/Building_Door.cs`

- `Building_Door : Building` is the core door state machine, lines 9-621.
- Open state, hold-open state, close timers, friendly-touch tracking, temperature exchange, reachability cache clearing, fog notification, and sounds all live here.
- `OpenPct` is protected virtual at line 268. Custom draw subclasses can use it without reimplementing timing.
- `SpawnSetup` caches `CompPowerTrader`, clears reachability, and opens if blocked, lines 277-285.
- `Tick` handles open/close progress and reachability invalidation, lines 322-392.
- `Notify_PawnApproaching` opens non-slow powered doors early, lines 401-412.
- `CanPhysicallyPass` and `PawnCanOpen` are the pathing permission surface, lines 415-459.
- `DoorOpen`, `DoorTryClose`, manual open/close, and sound/event notifications are lines 468-517.
- `DrawAt` calls `DoorPreDraw`, draws moving leaves when `CanDrawMovers` is true, then `Comps_PostDraw`, lines 519-528.
- `DrawMovers` is the vanilla two-leaf sliding draw helper, lines 530-554.
- `DoorPreDraw` only auto-rotates 1x1 doors, lines 600-606. Multi-tile doors depend on their placed rotation.
- `StuckOpen` requires non-1x1 doors to be enclosed by walls, lines 164-184.

`RimWorld/Building_SupportedDoor.cs`

- `Building_SupportedDoor : Building_Door`, lines 7-95.
- Adds optional `doorSupportGraphic` and `doorTopGraphic` drawing before the base door draw, lines 43-55.
- Plays `soundDoorCloseEnd` when a door finishes closing, lines 57-67.
- Caches support/top graphics and clears them on color changes, lines 13-40 and 75-80.

`RimWorld/Building_MultiTileDoor.cs`

- `Building_MultiTileDoor : Building_SupportedDoor`, lines 6-41.
- Overrides `CanDrawMovers => false` so the base `Building_Door` mover draw is not used, line 17.
- Caches `StuckOpen` in `Tick`, lines 19-23.
- `DrawAt` draws lower movers with scale `(0.5, 1, 1)` and offset `0.25 + 0.35 * OpenPct`, lines 25-32.
- If `def.building.upperMoverGraphic` exists, it draws a second mover layer with a faster clamped open percentage, lines 33-38.
- Calls `base.DrawAt` afterward, so support/top graphics and comps still draw, line 39.

Implementation implication: DECO should subclass `Building_MultiTileDoor` for custom large-door animations so it inherits door behavior and support-door visuals. Override draw behavior only when vanilla sliding is not enough.

## Vanilla Multi-Tile Door Defs

RimWorld 1.6.4850 has two buildable vanilla-style `Building_MultiTileDoor` defs:

- Core `OrnateDoor`, `ThingDefs_Buildings/Buildings_Structure.xml`
- Anomaly `SecurityDoor`, `ThingDefs_Buildings/Buildings_Misc.xml`

The Anomaly security door is the strongest vanilla reference. It is a 2x1 multi-tile sliding door:

```xml
<defName>SecurityDoor</defName>
<thingClass>Building_MultiTileDoor</thingClass>
<size>(2, 1)</size>
<rotatable>true</rotatable>
<useBlueprintGraphicAsGhost>true</useBlueprintGraphicAsGhost>
<building>
  <blueprintClass>Blueprint_Build</blueprintClass>
  <blueprintGraphicData>
    <texPath>Things/Building/SecurityDoor/SecurityDoor_MenuIcon</texPath>
    <graphicClass>Graphic_Multi</graphicClass>
    <shaderType>EdgeDetect</shaderType>
    <drawSize>(2.6, 3.1)</drawSize>
  </blueprintGraphicData>
  <isSupportDoor>true</isSupportDoor>
  <doorTopGraphic>...</doorTopGraphic>
  <doorSupportGraphic>...</doorSupportGraphic>
  <upperMoverGraphic>...</upperMoverGraphic>
</building>
<placeWorkers>
  <li>PlaceWorker_MultiCellDoor</li>
</placeWorkers>
```

Other notable `SecurityDoor` fields:

- `terrainAffordanceNeeded` is `Heavy`.
- It is powered with `CompPowerTrader`, base power consumption `50`.
- `poweredDoorOpenSpeedFactor` is `4`; unpowered open/close factors are `2`.
- It sets `isAirtight=true`.
- It uses `SecurityDoor_Open`, `SecurityDoor_BeginClosing`, and `SecurityDoor_EndClosing`.
- It clears stuff inheritance with `<stuffCategories Inherit="False" />`.

`OrnateDoor` is also `Building_MultiTileDoor`, `size=(2, 1)`, `rotatable=true`, `isSupportDoor=true`, with `doorTopGraphic` and `doorSupportGraphic`, but no `upperMoverGraphic`.

XML guidance:

- Use `<size>(N, 1)</size>` for wide doors, then rely on rotation for the other axis.
- Add `rotatable>true</rotatable>` for all multi-tile doors.
- Add `building.isSupportDoor=true` when using support/top/blueprint support-door behavior.
- Add `useBlueprintGraphicAsGhost=true` plus `building.blueprintGraphicData` for multi-tile support doors.
- Add `PlaceWorker_MultiCellDoor` so placement ghosts draw required wall cells and post-place warnings.
- Test 3x1 explicitly. The source logic is generic over non-1x1 sizes, but vanilla shipped examples here are 2x1.

## Wall Support And Placement

`RimWorld/DoorUtility.cs`

- `WallRequirementCells` skips 1x1 doors and computes required side-wall cells for larger doors, lines 8-23.
- It uses `def.defaultPlacingRot`, `def.size`, and the actual placed rotation to find the support cells.
- `EncapsulatingWallAt` accepts full-fill non-door edifices, and optionally matching blueprints/frames, lines 25-51.
- `DoorRotationAt` is only for auto-orienting 1x1 doors, lines 53-61.

`RimWorld/PlaceWorker_MultiCellDoor.cs`

- `DrawGhost` draws grey wall ghosts for the required support cells, lines 8-14.
- `PostPlace` warns if support walls are missing, including unbuilt wall blueprints/frames in the check, lines 16-30.
- It warns; it does not block placement. Runtime `StuckOpen` is what makes unsupported multi-tile doors remain stuck.

DECO implication: `PlaceWorker_DoorExpanded` preserves the vanilla worker for
ordinary multi-tile doors, but curtain defs with `oneSidedWallSupport=true`
block placement unless at least one full support side has a wall, wall blueprint,
or frame. A scoped `Building_Door.StuckOpen` postfix applies the same one-side
rule at runtime for those opted-in curtain defs only.

`RimWorld/Blueprint_Door.cs`

- This is for 1x1 doors. It forces `Graphic => DefaultGraphic` and auto-rotates from adjacent walls only when the build def size is `IntVec2.One`, lines 6-18.
- Multi-tile vanilla defs use `Blueprint_Build`, not `Blueprint_Door`.

Implementation implication: do not use `Blueprint_Door` for DECO multi-tile doors. Use `Blueprint_Build` for vanilla-like multi-tile slide doors, or a DECO blueprint subclass only for custom animated blueprint drawing.

## Ghost And Blueprint Rendering

`Verse/GhostUtility.cs`

- `GhostGraphicFor` returns the base graphic unchanged when `thingDef.useSameGraphicForGhost` is true, lines 11-14.
- Linked graphics or ordinary non-support doors use the UI icon path as a `Graphic_Single`, lines 24-27.
- Otherwise, `useBlueprintGraphicAsGhost` switches the base graphic to `thingDef.blueprintDef.graphic`, lines 31-34.
- It then creates an edge-detect ghost from the selected base graphic, lines 35-46.

`Verse/GhostDrawer.cs`

- `DrawGhostThing` gets the ghost graphic, computes true center using `thingDef.Size`, draws it, then lets comps and place workers draw overlays, lines 8-28.

`RimWorld/ThingDefGenerator_Buildings.cs`

- `NewBlueprintDef_Thing` copies buildable fields from the source def to the generated blueprint, lines 93-178.
- If `building.blueprintGraphicData` exists, it copies that data and defaults missing `graphicClass` to `Graphic_Single` and missing shader to `Transparent`, lines 124-151.
- Otherwise it copies `def.graphicData`, applies `EdgeDetect`, and clears shadow data, lines 152-159.
- It assigns `thingDef.thingClass = def.building.blueprintClass`, line 165.
- It only special-cases exact `def.thingClass == typeof(Building_Door)` for blueprint drawer type, lines 171-178. Subclasses and `Building_MultiTileDoor` use `def.drawerType`.

`RimWorld/Blueprint.cs`

- `Blueprint.DrawAt` just calls `base.DrawAt` unless gravship cutscene rendering suppresses it, lines 94-101.

`Verse/Thing.cs`

- `Thing.DrawAt` draws `Graphic.Draw(...)` for realtime or unspawned things, then silhouette, lines 1324-1331.

Implementation implication:

- Prefer the vanilla `SecurityDoor` pattern: `useBlueprintGraphicAsGhost=true` and a closed-state `building.blueprintGraphicData`.
- For custom animated doors, first try a normal closed composite blueprint/ghost graphic. Ghosts and blueprints do not need to show open animation.
- If custom blueprint drawing is needed for DECO-only defs, set `<building><blueprintClass>DECO.Blueprint_DoorExpanded</blueprintClass></building>` on those defs. Vanilla blueprint generation already honors this; no global `Thing.DrawAt` patch is needed.
- Avoid `GhostUtility` and `GhostDrawer` transpilers unless a later source check proves a scoped def/class solution cannot work.

## Pathing And Regions

`Verse/GridsUtility.cs`

- `GetDoor(this IntVec3, Map)` returns the edifice if it is a `Building_Door`, lines 424-431. Because `Building_MultiTileDoor` inherits `Building_Door`, it participates in this path.

`Verse.AI/Pawn_PathFollower.cs`

- When the pawn consumes a new path node, it calls `nextCell.GetDoor(pawn.Map)?.Notify_PawnApproaching(pawn, num)`, line 729.

`Building_Door` pathing hooks:

- `FreePassage`, `WillCloseSoon`, `CanPhysicallyPass`, `PawnCanOpen`, and `BlocksPawn` are all inherited by `Building_MultiTileDoor`.
- Reachability is cleared on spawn, despawn, faction change, and open/free-passage changes.

Implementation implication: as long as DECO custom doors inherit from `Building_Door` through `Building_MultiTileDoor`, vanilla pathing and region logic should see them as doors. Do not rebuild pathing with invisible helper doors.

## Remote Door Hooks

`RimWorld/Building_HackableDoor.cs` is a useful vanilla permission reference:

- `Building_HackableDoor : Building_SupportedDoor, IHackable`, lines 5-35.
- It overrides `CheckFaction => false`, line 13.
- It overrides `PawnCanOpen`; locked doors return false, unlocked doors defer to base logic, lines 15-23.
- `OnHacked` sets faction to the hacker's faction, lines 30-34.

Implementation implication: DECO remote-secured doors should use the same narrow surface. Override `PawnCanOpen` to return false while secured, then defer to base. Do not patch pawn pathing.

## Overlays

`RimWorld/OverlayDrawer.cs`

- Public API: `DrawOverlay(Thing, OverlayTypes)`, `Enable`, and `Disable`, lines 72-96.
- `OverlayTypes` is a fixed enum, `RimWorld/OverlayTypes.cs`, lines 6-19.
- Built-in pulsing render helpers and the `DrawBatch` are private, lines 242-276.

Implementation implication:

- There is a public overlay API for built-in overlay types only.
- There is no public "draw this custom lock material as a pulsing overlay" API in 1.6.4850.
- Prefer either a DECO `DrawAt`/post-draw lock icon or an acceptable built-in overlay type. Avoid reflection into `OverlayDrawer.drawBatch` unless a later visual requirement makes it worth the fragility.

## Rotation And Placement Controls

`RimWorld/Designator_Place.cs`

- `HandleRotationShortcuts` only collects middle-click/rotate-left/rotate-right input and calls `HandleRotation`, lines 192-219.
- `HandleRotation` rotates attachments to valid wall sides, otherwise plays a sound and rotates `placingRot`, lines 221-239.

Implementation implication: if DECO later needs "cannot rotate south" behavior, prefer def/catalogue choices first, then a scoped `PlaceWorker` or designator postfix. A transpiler is not justified by this source shape.

## Asymmetric Door Helper

`Building_DoorExpanded` supports explicit asymmetric-door opt-in through
`CompProperties_DoorExpanded.asymmetric` and
`syncAdjacentAsymmetricPair`.

- Orientation correction is always visual-only and always on for opted-in defs.
  The helper checks the two local slide/stretch-axis sides after `DoorRotationAt`;
  if exactly one outside side is a wall-like impassable non-door edifice, the
  single panel draws through the existing `flipped` branch. This reverses the
  movement offset and mirrors directional art such as curtain folds and jail-door
  handles.
- Pair sync is gated by `DecoSettings.syncPairedAsymmetricDoors` and only accepts
  unambiguous pairs: same `ThingDef`, same rotation/axis, aligned same-size
  occupied rects, directly adjacent, and wall-bracketed on the outside. Mixed defs,
  chains, missing walls, and ambiguous both-wall layouts stay independent.
- One-sided curtain support checks the real placed footprint, then canonicalizes
  parallel rotations (`South` to `North`, `West` to `East`) for draw and pair
  checks, so player rotation cannot create mirrored-twice N/S or E/W pairs.
- Open events sync through `DoorOpen(int)` with a recursion guard. `Tick`
  reconciliation is deliberately narrow: hold-open or blocked-open keeps/reopens
  the partner, but ordinary close timing is allowed to settle closed.
- Forbid mirroring uses a narrow Harmony postfix on `CompForbiddable.Forbidden`
  setter, routed back through the same pair helper and recursion guard. This covers
  vanilla gizmos and mass forbid/unforbid designators without replacing the comp UI.

Current opt-in defs: `HeronCurtainTribal`, `HeronCurtainTribalDouble`,
`HeronCurtainTribalTriple`, and `PH_DoorJail`.
