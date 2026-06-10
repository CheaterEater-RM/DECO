# DECO Implementation Notes

Working source snapshot: RimWorld `1.6.4850` decompiled source.

This document is the implementation-side companion to `DoorsExpanded_Rewrite_DESIGN.md`. It records the vanilla code references DECO should lean on before adding custom code. The repo identity is DECO: package `CheaterEater.DECO`, Harmony ID `com.cheatereater.deco`, namespace and assembly `DECO`. If older planning text still says `CheaterEater.DoorsExpanded` or `DoorsExpanded`, treat that as provisional design-era text, not implementation identity.

## Milestone Shape

M0 should stay pure XML:

- 1x1 doors, jail doors, and simple curtains use `Building_Door`.
- 2x1 and wider slide-animated doors, gates, and large curtains should try `Building_MultiTileDoor` first.
- No C# is needed for vanilla sliding behavior, wall support checks, power, forbidding, pathing, reachability, temperature exchange, hold-open gizmos, or basic ghost/blueprint rendering.

M1 adds C# only for non-vanilla draw behavior:

- `Building_DoorExpanded : Building_MultiTileDoor`, draw-only where possible.
- Config comp carries DECO draw data, but defs set `thingClass` explicitly.
- Custom blueprint or ghost code should be scoped to DECO classes/defs, not global `Thing.DrawAt`.

M2 adds remote control:

- Remote state should override normal open permission, not replace vanilla door pathing.
- Link state should be reference-scribed and scrubbed, following the pattern from the design doc.

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

The Anomaly security door is the strongest M0 reference. It is a 2x1 multi-tile sliding door:

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

M0 XML guidance:

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

- For M0, prefer the vanilla `SecurityDoor` pattern: `useBlueprintGraphicAsGhost=true` and a closed-state `building.blueprintGraphicData`.
- For M1 custom animated doors, first try a normal closed composite blueprint/ghost graphic. Ghosts and blueprints do not need to show open animation.
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
- For M2, prefer either a DECO `DrawAt`/post-draw lock icon or an acceptable built-in overlay type. Avoid reflection into `OverlayDrawer.drawBatch` unless a later visual requirement makes it worth the fragility.

## Rotation And Placement Controls

`RimWorld/Designator_Place.cs`

- `HandleRotationShortcuts` only collects middle-click/rotate-left/rotate-right input and calls `HandleRotation`, lines 192-219.
- `HandleRotation` rotates attachments to valid wall sides, otherwise plays a sound and rotates `placingRot`, lines 221-239.

Implementation implication: if DECO later needs "cannot rotate south" behavior, prefer def/catalogue choices first, then a scoped `PlaceWorker` or designator postfix. A transpiler is not justified by this source shape.

## Current Open Checks

- Validate 3x1 `Building_MultiTileDoor` in-game. `DoorUtility` and `Building_MultiTileDoor.DrawMovers` look generic over `size.x`, but vanilla 1.6.4850 examples are 2x1.
- Verify closed composite art for M1 custom doors before writing blueprint/ghost code.
- Decide whether remote locked doors use a custom icon drawn by the door, or a built-in overlay type.
- Update `DoorsExpanded_Rewrite_DESIGN.md` identity header when doing a docs pass, so it matches DECO instead of the old provisional names.
