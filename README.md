# Doors Expanded Continued Overhaul

Doors Expanded Continued Overhaul, or DECO, is a ground-up RimWorld 1.6 rewrite inspired by Doors Expanded.

DECO restores the Doors Expanded catalogue with wide doors, curtains, gates, jail and blast doors, remote-controlled garage doors, buttons, and levers. The implementation is built around vanilla door behavior first, with scoped C# only where the original visuals or remote-control behavior need it.

## Switching from / to the original Doors Expanded

DECO uses the original mod's internal names (defNames and door classes), so saves are
interchangeable in both directions: swap which mod is enabled and your built doors carry
over automatically — running entirely on DECO's code while DECO is active.

- **Never enable both mods at once.** They claim the same internal names and will
  conflict (DECO declares the incompatibility, so the mod manager warns you).
- Doors, gates, curtains, jail/blast doors, and remote doors, buttons, and levers all
  carry over — DECO reuses the original's defNames and saved field names. The one
  exception is a minified curtain sitting in storage from an old save: DECO's curtains
  aren't minifiable, so any such orphan is removed once on load (its build cost refunded)
  with a one-time warning.

## Attribution

Textures and audio are reused from *Doors Expanded* (© 2020 jecrell, MIT) with attribution to jecrell, lbmaian, and jebjordan. Original art commissioned by CMDR Toss Antilles. The DECO codebase is a clean-room rewrite and does not reuse the original mod's code.

## License

MIT
