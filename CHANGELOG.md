# Changelog

## 0.1.3

- The development command file (`DevCommandFile`) is gone from release builds. It let a text
  file in the config folder drive the console, which a client-side mod has no business shipping;
  it now exists only in developer builds. An old `DevCommandFile` line left in your config file
  is simply ignored.

## 0.1.2

- **Mine** visibility now keeps your own hits on creatures another player's game is running.
  Those numbers arrive with no hit behind them, and were being dropped as someone else's; they
  are now recognised by where your hit landed.
- Changing a setting no longer breaks the numbers already on screen: they switch to the new
  outline and shadow instead of drawing with a discarded material. The compatibility check
  also stops writing a log line on every change (every frame while a slider is dragged).
- The Vanilla preset turns off tick merging and the screen-edge flash, which vanilla has
  neither of; Bold and Festive set them to their defaults.
- The distance limit is only applied while DamageSplash is drawing. Disabled, or standing aside
  for another damage-number mod, vanilla's own 30 m is back.
- `splash reload` applies a preset changed in the config file, as choosing it in the dropdown
  would.
- Only the first hit in a sneak attack's frame is tagged SNEAK; a second hit landing in the
  same frame no longer is.
- The screen-edge flash measures the health a hit actually takes, after the world's
  damage-taken setting, and no longer fires in god mode.
- Bounce bounces once, as described, instead of hopping every time it drops back down.
- Sneak and crit hits never merge into other numbers, even with their tag word blanked out.
- A merged number keeps the transparency of its configured colour.
- The screen-edge flash no longer leaks a texture on each world change.
- The development command file no longer bypasses the game's check for cheat commands, and a
  file it cannot read is retried rather than raising an error every half second.
- `splash demo 2.5` and the other console numbers parse the same on every system locale.

## 0.1.1

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
