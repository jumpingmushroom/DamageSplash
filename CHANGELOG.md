# Changelog

## 0.1.1 (unreleased)

- A failure inside DamageSplash can no longer cancel a hit. The number is drawn inside the
  game's damage call, before the health is taken, so an exception from the mod used to abort
  the hit itself. Every hook now catches its own errors, logs them a few times, and lets the
  game (and vanilla's number) carry on.
- A context left behind by a failed hit can no longer tag a later number: contexts now only
  pair within the frame they were made in.
- A burning or poison tick that kills now gets the kill tag even when it merged into the
  climbing number.
- Only the killing blow is marked as one. A second hit landing before the game registers the
  death (arrows, damage ticks) was also tagged KILL.
- HideZeros no longer hides the "too hard" message on rocks and trees your tool cannot damage.
- Packaging: the archive is flat, with the DLL at its root and no directory entries. 0.1.0
  shipped with the DLL under `plugins/DamageSplash/` and Thunderstore accepted it, so this is
  tidying rather than a fix.

## 0.1.0

- First build: replaces the vanilla damage-text renderer with a pooled one. Font, outline,
  near/far size falloff, pop entrance, arc motion, per-type colours. Vanilla, Bold and Festive
  presets. `splash demo` console command.
- Boldness (face dilation) setting: an outline on its own eats a thin face and the numbers read
  as black blobs, so the presets fatten the strokes first.
- Presets use Valheim-AveriaSansLibre: both Norse faces draw the digit zero as a rune.
- Entrance settles on an ease-out-back curve, with a fade-in and a random tilt per number.
- `splash set`, `splash get` and `splash rain` for tuning the look live.
- Damage-over-time ticks on one victim add into a single number that climbs instead of stacking,
  and it follows the victim rather than staying where the first tick landed.
- A red bloom at the edges of the screen when you take a heavy hit, drawn from a vignette
  generated at runtime; nothing is shipped for it.
- Visibility: show every number, only your own fights, or none at all.
- Detects ColorfulDamage and ZenCombat, which claim the same method, and stands aside by default
  rather than racing them silently.
- Numbers now know the hit behind them: sneak attacks, hits on staggered creatures and killing
  blows get their own colour and a tag, damage-over-time ticks are tinted by element, and every
  number is sized by what share of the target's health it took. Vanilla preset keeps all of it off.
