# Doors Expanded Continued Overhaul

Doors Expanded Continued Overhaul, or DECO, is a ground-up RimWorld 1.6 rewrite inspired by Doors Expanded.

The first milestone is intentionally small: rebuild the slide-animated door catalogue in XML on top of vanilla door classes before adding any custom C# behavior. Later milestones add custom animations, decorative frames, and remote-control doors.

## Switching from / to the original Doors Expanded

DECO uses the original mod's internal names (defNames and door classes), so saves are
interchangeable in both directions: swap which mod is enabled and your built doors carry
over automatically — running entirely on DECO's code while DECO is active.

- **Never enable both mods at once.** They claim the same internal names and will
  conflict (DECO declares the incompatibility, so the mod manager warns you).
- Until DECO's remote-control milestone ships, the old mod's remote doors, buttons, and
  levers (and any minified curtain in storage) drop from a migrated save with one-time
  warnings. Everything else carries over.

## Attribution

Textures and audio are reused from *Doors Expanded* (© 2020 jecrell, MIT) with attribution to jecrell, lbmaian, and jebjordan. Original art commissioned by CMDR Toss Antilles. The DECO codebase is a clean-room rewrite and does not reuse the original mod's code.

## License

MIT
