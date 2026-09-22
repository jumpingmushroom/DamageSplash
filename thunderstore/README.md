# DamageSplash

Valheim's damage numbers are small, thin and unreadable against snow and sand, and they all look
alike whether you grazed a greyling or landed the hit of your life. DamageSplash makes them
prominent, festive, and worth reading.

![Every kind of number](https://raw.githubusercontent.com/jumpingmushroom/DamageSplash/main/docs/images/crop-m3-styles.png)

Client-side. Nothing to install on the server, and players without it are unaffected.

## The look

A bold outlined face that stays legible on any background, numbers that pop in and settle rather
than appearing fully formed, and an arc instead of a straight drift upward. Three presets to
start from:

- **Vanilla** reproduces the stock look exactly, for when you want only the other features.
- **Bold** keeps vanilla's motion and just makes the numbers readable.
- **Festive** is the full treatment.

Every part of it is a setting: font, size near and far, outline, shadow, colour per damage kind,
and the whole animation, including five motion presets from a gentle pop to a bouncing fountain.

## What the numbers tell you

- **Size means impact.** A number is sized by the share of the target's health it took, so 30
  damage reads big on a greyling and small on a fuling berserker. No fixed threshold can say both.
- **Sneak attacks and hits on staggered enemies** get their own colour and a tag. Those are the
  two damage multipliers Valheim actually has, so they are the honest equivalent of a crit.
- **Killing blows** get a tag, the largest size and a moment longer on screen.
- **Burning, poison and frost** are tinted by element, and repeated ticks on one victim add into
  a single number that climbs and follows the creature rather than stacking into a column.
- **Damage to you** has its own colour, and a heavy hit blooms red around the edges of the screen.

## Multiplayer

Set visibility to **All**, **Mine** or **None**. Only hits resolved on your own machine can be
told apart, so **Mine** keeps your hits and anything aimed at you, and drops other people's
fights. Blocks, heals and bonuses are not damage numbers and always show.

## Console

`splash demo` shows one of every kind of number around you, so you can judge the look without
finding a fight. `splash rain` trickles numbers while you drag a slider. `splash set <name>
<value>` changes any setting live. `splash` on its own lists the rest.

## Compatibility

ColorfulDamage and ZenCombat replace the same thing this mod replaces, and whichever loads first
silently wins. DamageSplash names the conflict in the log and stands aside by default. Pick one.

Requires BepInEx. Source and issues: https://github.com/jumpingmushroom/DamageSplash
