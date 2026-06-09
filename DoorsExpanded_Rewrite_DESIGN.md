# Doors Expanded — Ground-Up Rewrite — Design

**Working package ID:** `CheaterEater.DoorsExpanded` ⟨confirm⟩
**Harmony ID:** `com.cheatereater.doorsexpanded`
**Namespace / assembly:** `DoorsExpanded`
**Target:** RimWorld 1.6, `net48`, Harmony 2.4.x
**Status:** Design — pre-code
**Heritage:** Fresh **rewrite** inspired by *Doors Expanded* (jecrell / lbmaian / jebjordan), MIT. **Not a fork, not a cleanup.** Art and two narrow code assets are reused (§0.1); the core is built new. We make a deliberate clean break from a codebase that has carried baggage since RimWorld 1.0.

---

## 0. Framing: rewrite, not cleanup

**Verdict: this is a ground-up rewrite that reuses assets, not a refactor of the existing code.**

The original is MIT, so we *could* fork it freely — but we deliberately won't, for engineering reasons. The thing most worth fixing (global `Thing.DrawAt` prefix + transpiler-based ghost/blueprint rendering, §5) is **structural**: you cannot refactor your way out of it, because removing it requires a different rendering approach by definition. Once the centerpiece of the old architecture is the part you're discarding, "cleanup" is the wrong word for the work.

The cleanup framing also oversells its savings. Subtract everything dead or obsolete from the old 1.6 source — the duplicate `HandleRotationShortcuts` patch, the orphaned `CompProperties_BreakdownableCustom`, `DoorExpandedDef`, the `Building_DoorRegionHandler` tombstone, the empty `Tick`, and (going zero-transpiler) nearly all of `HarmonyPatches.cs` plus the entire `HarmonyExtensions.cs` / `Locals` IL-helper file copied from PawnShields — and what remains worth keeping is small and *portable*. Keeping the scaffolding saves no real work; it only inherits baggage: `jecrell.doorsexpanded`, `ProjectHeron.csproj`, the `DoorsExpanded` / `Heron*` / `PH_` naming, and 1.0–1.6 `LoadFolders` multi-versioning we don't want. Retrofitting that into the `CheaterEater.*` template / `Paths.props` ecosystem is *more* effort than starting clean.

So: **clean break at the core.** The rewrite is nonetheless *small*, because vanilla `Building_MultiTileDoor` (1.5+) now does the hard part — multi-tile pathing, region linking, open/close, power, forbidding. The historic performance disasters (invisible 1×1 sub-doors, region thrash, "invis door has different faction") were pre-1.5 workarounds that **no longer need to exist**. The new codebase is plausibly smaller than the cleaned-up old one would be.

### 0.1 Reuse vs. rewrite — exact boundary

We reuse **exactly three things**. Everything else is new code under our conventions.

| Carry over | How | Why |
|---|---|---|
| **All art & audio assets** | Verbatim, with MIT attribution (§9) | The main reason to involve the old mod at all |
| **Stretch/swing draw math** (`DrawStretchParams`, `DrawDoubleSwingParams`, `DrawStandardParams`, `DrawFrameParams`) | Lifted near-verbatim into one self-contained static helper | Fiddly, correct geometry; reimplementing from scratch is pure risk for zero gain. This is an *asset*, same as the textures — copy it, don't reinvent it. |
| **Remote-link design** (`Notify_Linked` / `Notify_Unlinked`, `Scribe_References` + `LookMode.Reference`, stale-scrub) | Adopt the *pattern*; re-type the code into our naming | Sound and save-safe; small enough to rewrite cleanly |

Explicitly **not** carried over: the rendering patch architecture, all transpilers, `HarmonyExtensions`/`Locals`, `DoorExpandedDef`, `CompProperties_BreakdownableCustom`, `Building_DoorRegionHandler`, the `comp-rewrites-thingClass` trick, build config, naming, and multi-version `LoadFolders`.

> **The one trap:** "it's a rewrite" must not tempt us into reimplementing the draw math for purity. That math is reused, full stop. Rewriting applies to *architecture*, not to settled geometry.

### What stays custom at all

Vanilla doors only slide. After the clean break, the genuinely custom delta is just three things:

1. **Custom open animations** — swing and stretch.
2. **Decorative door frames** — overlay graphics around large doors.
3. **Remote control** — doors slaved to a button/lever, with a locked state.

The rewrite is scoped around that delta, not around "re-implementing a door."

---

## 1. Objectives

- Reproduce the original's door catalogue and visual feel using its MIT assets.
- Do as much as possible in **pure XML on top of vanilla classes**; write C# only for the three delta features above.
- Eliminate every known perf/robustness problem of the original (enumerated in §6).
- Keep CE compatibility a hard constraint.
- Be save-safe by construction (no custom `Zone`, no polymorphic `LookMode.Deep`).

### Non-objectives (v1)

- **No migration from jecrell's mod.** Different package ID; old saved doors won't convert. Documented, not supported.
- **No public modding API.** The config comp is designed cleanly enough to *become* one later, but third-party-modder support is explicitly out of scope until the internal design is stable (§5).
- No new door types beyond the original catalogue.

---

## 2. Architecture — three tiers of cost

The design's spine is minimizing C# surface. Each door type is placed in the cheapest tier that can render it.

### Tier 0 — Pure XML, vanilla classes (no code)

Anything whose only "expansion" is **size, stats, or stuff** and that animates with a normal slide:

- Wider doors (2×1, 3×1) → `Building_MultiTileDoor` via XML, larger `graphicData`.
- 1×1 jail door → `Building_Door`.
- Gates → `Building_MultiTileDoor`.
- Curtains → `Building_Door` / `Building_MultiTileDoor`, fabric stuff, low HP/high flammability.

If vanilla can draw a multi-tile sliding door (it can), these need **zero C#**. This is the bulk of the catalogue and the biggest reliability/perf win — code that doesn't exist can't be slow or buggy.

> ⟨TBD-1⟩ Verify vanilla `Building_MultiTileDoor` accepts arbitrary `size` (e.g. 3×1) cleanly in 1.6, including draw and pathing, with only XML. If a width is rejected, that width moves up to Tier 1. *Check against `Building_MultiTileDoor` source + the vanilla ancient/mech door defs before locking the catalogue.*

### Tier 1 — `Building_DoorExpanded : Building_MultiTileDoor` (draw only)

For doors needing **swing/stretch animation or a decorative frame**: blast doors, ornate gates, anything with a non-slide open.

- Subclass overrides **`DrawAt`** only. No tick logic, no spawn-time footprint hacks beyond what vanilla already does for MultiTileDoor.
- Config carried by `CompProperties_DoorExpanded` (animation type, frame graphics, stretch params, async leaf).
- Draw math (Standard / DoubleSwing / Stretch / StretchVertical) ported from the original — it is correct and allocation-light; keep `MeshPool` reuse, no per-frame `new`.

### Tier 2 — `Building_DoorRemote : Building_DoorExpanded` + `Building_DoorRemoteButton : Building`

Remote/garage doors and their buttons/levers. Adds the link model and locked state (§4).

---

## 3. Config comp — `CompProperties_DoorExpanded`

Single config object on Tier 1/2 defs. Fields (carried over, trimmed):

`doorType` (enum), `singleDoor`, `fixedPerspective`, `rotatesSouth`, `doorFrame` / `doorFrameOffset`, `doorFrameSplit` / `doorFrameSplitOffset`, `doorAsync`, `stretchCloseSize` / `stretchOpenSize` / `stretchOffset`, `tempEqualizeRate`.

### Deliberate departures from the original

- **Do not rewrite `parentDef.thingClass` from inside the comp.** The original's comp reassigns `thingClass` in `ResolveReferences` — slick, but it's "magic" that only earns its keep for third-party modders who forget to set it. Our defs are ours: **set `thingClass` explicitly in XML.** The comp carries data only. (If/when a public API ships, the rewrite trick can return behind that boundary.)
- **`DoorType` enum** defined with explicit integer values; never reordered after first release.
- **Drop `DoorExpandedDef`** (the obsolete `ThingDef` subclass) entirely — comp-only from day one.
- **Drop `CompProperties_BreakdownableCustom`.** In the original it's orphaned — `breakdownMTBUnit` is read by nothing and the half-frequency feature is non-functional. If we want blast/remote doors to break down less often, set `mtbDays` per def in XML on vanilla `CompProperties_Breakdownable`. No code.
- **`CompProperties_PostProcessText`** (the "(3×1)" label suffix + label inheritance): defer to a later phase or cut. It adds a startup pass and a self-removing-comp dance for a cosmetic label. ⟨TBD-2⟩ Decide whether the size-suffix label is worth it; if yes, prefer hand-authored labels in XML over a runtime post-processor.

---

## 4. Remote doors

Keep the original's model — it is sound and **save-safe**:

- `Building_DoorRemoteButton` holds `List<Building_DoorRemote> linkedDoors`, scribed with **`LookMode.Reference`**.
- `Building_DoorRemote` holds a `button` reference (`Scribe_References`) + `securedRemotely` bool.
- Linking via paired `Notify_Linked` / `Notify_Unlinked`; stale links scrubbed on read and before save.
- `SecuredRemotely` → force-closed + forbidden; reactive `ForbidUtility.SetForbidden`. `PawnCanOpen` returns false while force-closed (fixes wild-man walk-out).

### Cleanups

- **Locked-state overlay:** the original reaches `OverlayDrawer.drawBatch` via private `FieldRefAccess` to draw a pulsing lock. ⟨TBD-3⟩ Check for a public overlay API on 1.6 (`OverlayDrawer.DrawOverlay` / `RenderPulsingOverlay`) and use it; fall back to the reflection approach only if none exists. Legibility cue (pulsing lock) is good — keep the *behavior*, drop the reflection if avoidable.
- **Button↔door interaction job:** keep the `WorkGiver_Scanner` + `JobDriver` (goto interaction cell → short wait → toggle, re-validating disabled state). Straightforward; no changes needed beyond a clean rewrite.

### Reusability note

This link model (reference one side, `LookMode.Reference` collection the other, stale-scrub on save) is the removal-safe controller/sensor template relevant to **Lookouts** (watchtower↔sensor) and **Underwatch** (caller↔dampener). Worth keeping consistent across all three.

---

## 5. Ghost & blueprint rendering — the perf-sensitive part

This is where the original gets expensive, and where the rewrite earns its keep.

**The original's problem:** its doors draw as two offset leaves via custom mesh math, so vanilla's ghost (single centered graphic) and blueprint draw don't match. It fixes this with:
- a **transpiler** on `GhostUtility.GhostGraphicFor`,
- a **transpiler** on `GhostDrawer.DrawGhostThing`, and
- a **prefix on `Thing.DrawAt`** — which fires for *every Thing drawn every frame* to catch blueprints.

That last one is a global hot patch. It is the headline performance liability.

### Rewrite approach (in priority order)

1. **Make the closed-state graphic a normal multi-tile graphic.** Ghosts and blueprints are always shown at `openPct = 0` (fully closed). At closed state the two leaves meet in the middle and read as one image. If the art ships a **closed-door graphic authored as a standard `Graphic_Multi`** sized to the footprint, vanilla ghost/blueprint rendering may "just work" with **no patches at all** — the two-leaf split only happens at runtime during animation. *This is the preferred path: it deletes all three patches.*
   > ⟨TBD-4⟩ Confirm with the assets that a single closed-door sprite per direction is available or can be composed. The original's textures are per-leaf; we may need to author/flatten a closed composite. Low art effort, high code savings.

2. **If the split must be visible even when closed** (e.g. a center seam that looks wrong as one sprite): replace the global `Thing.DrawAt` prefix with a **custom `Blueprint` subclass** assigned via an early patch on `ThingDefGenerator_Buildings.NewBlueprintDef_Thing` (run before def generation). This is the clean, scoped version of the fix the original author left as a `// TODO`. Cost is one early patch + one subclass, paid once, instead of a per-frame global prefix.

3. **Avoid the ghost transpilers regardless.** If (1) holds, they're unnecessary. If (2) is needed, the same custom graphic path handles ghosts without IL rewriting.

**`rotatesSouth = false`** (doors that shouldn't face south): the original transpiles `Designator_Place.HandleRotationShortcuts`. ⟨TBD-5⟩ Assess whether any door in our catalogue actually needs this. If only a couple do, prefer a small postfix or a `PlaceWorker` rotation correction over a transpiler; if none do, drop the feature.

**Net target:** zero transpilers, zero per-frame global patches. At most one early run-once patch (only if §5.2 is needed).

---

## 6. Known problems of the original → resolution

| Original problem | Root cause | Rewrite resolution |
|---|---|---|
| Region thrash / pathing bugs / "invis door has different faction" | Invisible 1×1 sub-door composites | Gone — vanilla `Building_MultiTileDoor` |
| `Thing.DrawAt` prefix on all things | Couldn't subclass generated Blueprint | §5: closed-composite graphic (no patch) or early `ThingDefGenerator` patch + Blueprint subclass |
| Brittle ghost transpilers (version-fragile) | Custom two-leaf draw vs vanilla ghost | §5: avoided via standard closed graphic |
| Orphaned `CompProperties_BreakdownableCustom` | Transpiler removed, field left behind | Dropped; use XML `mtbDays` |
| Dead duplicate `HandleRotationShortcuts` attribute patch | Leftover alt implementation, never applied | Not ported |
| Empty `Tick` override, `[Obsolete]` tombstone classes | Migration cruft | Not ported (greenfield, no old saves) |
| `DoorExpandedDef` legacy carrier | Pre-comp config | Not ported; comp-only |
| Per-draw reflection (`Comps_PostDraw` FastInvoke) | Side effect of blueprint draw hack | Eliminated with the hack (§5) |

---

## 7. Save compatibility

- Greenfield package ID; **no conversion** from the original mod. Document: "remove the old mod, rebuild doors."
- No custom `Zone`. No polymorphic `LookMode.Deep` collections. Remote links are `LookMode.Reference` + `Scribe_References` (the canonical save-safe pattern).
- `DoorType` enum values fixed at first release; additions go at the end only.
- On removing *this* mod mid-save: Tier-0 doors are vanilla classes (degrade gracefully). Tier-1/2 doors vanish with their custom `thingClass`; remote-button references null out and are scrubbed. Document the remove-from-save behavior per the standard checklist.

---

## 8. CE compatibility

- We extend `Building_MultiTileDoor`; we do not patch door internals. Vanilla-compatible ≈ CE-compatible for doors.
- ⟨TBD-6⟩ Audit CE for patches on `Building_Door` / `Building_MultiTileDoor` (cover/fillPercent, embrasure handling, fire-from-explosion behavior — the original had a CE explosion-slowdown fix in its history). Confirm our `fillPercent = 1` impassable doors and any partial-cover doors behave under CE. Verify before release, not as an afterthought.

---

## 9. Assets & attribution

- **Textures, sounds, Photoshop sources:** reuse from the MIT repo. Preserve the original `LICENSE` (MIT, © 2020 jecrell) in our repo and credit jecrell / lbmaian / jebjordan + "art commissioned by CMDR Toss Antilles" in `About.xml` and `README`.
- ⟨TBD-7⟩ **Asset license confirmation.** The MIT text covers "the Software and associated documentation files"; the art is noted as *commissioned*. Committed-to-an-MIT-repo is the defensible reading, but a one-line confirmation from the maintainer (or generous attribution + readiness to pull on request) closes the small residual risk. Low severity; resolve before publishing, not before coding.
- New `defName` prefix to avoid collision if both mods are ever loaded. Note `CE_*` is taken by Combat Extended — pick something like `CEx_*` or `DEx_*`. ⟨confirm prefix⟩

---

## 10. Milestones

- **M0 — XML catalogue (Tier 0).** All slide-animated doors/gates/curtains/jail door as pure XML on vanilla classes. Assets wired. Ships and is fully playable with **no assembly**. Validates the "how much needs code at all" question early.
- **M1 — Animated doors (Tier 1).** `Building_DoorExpanded` draw subclass + config comp. Swing/stretch/frames. Resolve ⟨TBD-4⟩ (ghost/blueprint) here — prove the no-patch path before adding any patch.
- **M2 — Remote doors (Tier 2).** Remote door + button/lever, link model, locked overlay, interaction job.
- **M3 — Polish.** Optional label suffix (⟨TBD-2⟩), CE audit sign-off (⟨TBD-6⟩), docs, save-behavior matrix.

Each milestone is independently shippable and testable in isolation.

---

## 11. Open decisions (consolidated)

| # | Decision | Recommendation |
|---|---|---|
| TBD-1 | Does vanilla MultiTileDoor accept all needed widths via XML? | Verify against source before locking catalogue |
| TBD-2 | Keep the "(3×1)" size-suffix label? | Defer to M3; prefer hand-authored labels |
| TBD-3 | Public overlay API for locked-state pulse? | Use if exists; reflection fallback only |
| TBD-4 | Closed-state composite graphic to avoid ghost/blueprint patches? | **Pursue — biggest perf/robustness win** |
| TBD-5 | Is `rotatesSouth = false` needed by any door? | Drop if unused; postfix/PlaceWorker if few |
| TBD-6 | CE patches on door classes? | Audit before release |
| TBD-7 | Commissioned-art license under MIT? | Confirm + attribute before publish |

> Reference only for the original's behavior — read its source via the Filesystem MCP (`Doors Expanded Continued/Source/`) and cross-check vanilla (`Building_MultiTileDoor`, `Building_Door`, `GhostUtility`, `ThingDefGenerator_Buildings`, `OverlayDrawer`) with the rimworld-source MCP. Do not copy code.
