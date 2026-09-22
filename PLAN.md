# DamageSplash — Technical Plan

**Goal:** make Valheim's floating damage numbers prominent and festive: bold outlined type, a
"pop" entrance, an arc instead of a straight drift, numbers that grow with how much the hit
mattered, and celebrations for sneak attacks, stagger crits and killing blows. The reference is
the WoW addon NiceDamage (bold Pepsi-style font, scale, gravity, ramp duration) plus the parts of
WoW's own combat text that people attribute to it (bigger crits that pop in).

**Target build:** Valheim **1.0.15**. Analysis source: `assembly_valheim.dll` from the rig
(2026-09-17 build), decompiled with ILSpy 9.1 on 2026-09-22. Every member cited below exists in
that assembly. The Recount sibling holds the same lib set and a PLAN.md whose §1 covers the
damage pipeline in more depth.

**Principles**

- Client-side only. The mod never changes what goes over the wire; unmodded players are
  unaffected and no server install is needed.
- Everything visible is configurable, with a handful of presets so nobody has to touch twenty
  sliders. "Vanilla" preset must reproduce the stock look exactly.
- Never slower than vanilla. Vanilla instantiates and destroys a GameObject per number; we pool.
- Degrade gracefully: a number with no hit context (remote owner, non-character targets) still
  gets font, outline and animation, just not the context-aware extras.

---

## 1. What the game actually does

### 1.1 One class, one RPC

`DamageText` (a MonoBehaviour, `DamageText.instance`) owns everything:

- `ShowText(TextType|DamageModifier, Vector3 pos, float dmg | string text, bool player)` packs
  `(int type, Vector3 pos, string text, bool player)` into a ZPackage and calls
  `ZRoutedRpc.instance.InvokeRoutedRPC(0L, "RPC_DamageText", pkg)`. Target `0L` is everybody.
- `RPC_DamageText(long sender, ZPackage)` bails if there is no main camera or the HUD is hidden
  (`Hud.IsUserHidden()`), drops texts beyond `m_maxTextDistance` (30 m), sets
  `mySelf = player && sender == ZNet.GetUID()`, and calls `AddInworldText`.
- `AddInworldText(TextType, pos, distance, string text, bool mySelf)` instantiates
  `m_worldTextBase` under the DamageText transform, grabs its `TMP_Text`, picks a colour, picks
  `m_largeFontSize` (16) or `m_smallFontSize` (8) by whether `distance > m_smallFontDistance`
  (10 m), rewrites the text for TooHard / Heal / Blocked / Bonus, and pushes a
  `WorldTextInstance {m_worldPos, m_gui, m_timer, m_textField, m_duration}` onto `m_worldTexts`.
  Position gets `Random.insideUnitSphere * 0.5f` of jitter. Zero-damage texts are skipped once
  200 are alive.
- `UpdateWorldTexts(dt)` from `LateUpdate`: `m_worldPos.y += dt` (1 m/s straight up), alpha
  `1 - t^3`, `m_gui.transform.position = camera.WorldToScreenPointScaled(worldPos)` (GUI-scale
  aware), hides when off screen, destroys **one** expired text per frame.

Colours: own damage taken is red (grey if "0"); Normal white, Resistant/Immune grey 0.6, Weak
yellow, TooHard pinkish, Bonus orange, Heal pale green at 0.7 alpha. `Bonus` is the orange
crafting/pickable "+N" popup and gets 1.5× size and 3 s; it is not a crit.

### 1.2 Who calls it

| Caller | Type | Notes |
|---|---|---|
| `Character.ApplyDamage` | modifier-derived (Normal/Resistant/Weak/Immune) | `player` flag = `IsPlayer() \|\| IsTamed()`. Number is `totalDamage` **before** the final difficulty multipliers. Skipped when result ≤ 0.1. |
| `Humanoid.BlockAttack` | Blocked | Amount blocked, at `m_point + up*0.5`. |
| `Character.RPC_Heal` | Heal | `player` = IsPlayer. |
| `MineRock`, `MineRock5`, `TreeBase`, `WearNTear`, `Destructible` | modifier-derived or TooHard | Non-characters. |
| `InventoryGui`, `Pickable`, `CookingStation` | Bonus | Crafting/gathering bonuses. |

### 1.3 The RPC is delivered locally, synchronously, before it is sent

`ZRoutedRpc.InvokeRoutedRPC(targetPeerID, …)`: if `targetPeerID == m_id || targetPeerID == 0L`
it calls `HandleRoutedRPC` **immediately**, then `RouteRPC` to peers. So on the client that owns
the target, the call chain is

    Character.RPC_Damage → ApplyDamage → DamageText.ShowText → RPC_DamageText → AddInworldText

all on one stack, before `ApplyDamage` subtracts health. This is the foundation of §2.2: a
Harmony prefix on `ApplyDamage` can stash the `HitData`, `AddInworldText` can read it in the same
frame, and a postfix can see whether the hit killed and restyle the number it just made.

### 1.4 What decides "crit" in Valheim

Valheim has no random crits. Two deterministic multipliers happen in `Character.RPC_Damage`
(owner only), before `ApplyDamage`:

- **Sneak attack**: if the target's AI is not alerted and `hit.m_backstabBonus > 1` and it has
  been 300 s since the last one, `hit.ApplyModifier(m_backstabBonus)` and
  `m_backstabTime = Time.time`. Detect: `target.m_backstabTime == Time.time` (Recount uses this).
- **Stagger crit**: if `IsStaggering() && !IsPlayer()`, `hit.ApplyModifier(2f)` and
  `m_critHitEffects` plays. Detect: `target.IsStaggering() && !target.IsPlayer()` in the prefix.

`HitData` also carries `m_damage` (per-type amounts; `GetMajorityDamageType()`), `m_ranged`,
`m_hitType` (EnemyHit, PlayerHit, Fall, Burning, Freezing, Poisoned, …), `m_attacker` (ZDOID;
`GetAttacker()`), `m_skill`, `m_itemLevel`. Burning/poison ticks call `ApplyDamage` directly with
`m_attacker = ZDOID.None` and `m_hitType` Burning/Poisoned.

### 1.5 Prefab and rendering

`m_worldTextBase` is a serialized prefab with a `TMP_Text` (UGUI) on it, parented to the
DamageText object, positioned in screen pixels. Because it is a UI element we can freely set
`localScale`, `rotation`, `fontSize`, `font`, `fontMaterial` and TMP outline/underlay properties
per instance. Game fonts available as `TMP_FontAsset`: Valheim-Norse, Valheim-Norse Bold,
Valheim-AveriaSansLibre and variants (Recount's `Core/Fonts.cs` already enumerates and resolves
them by name). The stock text has no outline configured beyond whatever the prefab material sets;
that is why vanilla numbers vanish against snow and sand.

---

## 2. Design

### 2.1 Two layers, two hook sets

```
   context layer (owner client only)          render layer (every client)
   ───────────────────────────────            ────────────────────────────
   Character.ApplyDamage  prefix ─┐           DamageText.AddInworldText   prefix (replace)
   Humanoid.BlockAttack   prefix ─┼─► HitContext.Current ─►  Splash.Spawn(type, text, ctx)
   Character.RPC_Heal     prefix ─┘                          DamageText.UpdateWorldTexts prefix (replace)
   Character.ApplyDamage  postfix ──► kill? restyle last     DamageText.RPC_DamageText  transpiler (distance)
```

- **Render layer** replaces `AddInworldText` and `UpdateWorldTexts` with our own (returning
  `false`), the same shape ColorfulDamage uses, and patches the distance check in
  `RPC_DamageText`. We keep vanilla's `m_worldTexts` list untouched and run our own pool so a
  mid-session disable falls back to vanilla cleanly.
- **Context layer** sets a static `HitContext.Current` in the prefixes (target character, the
  `HitData`, sneak flag, stagger flag, target max health, whether the attacker is the local
  player, whether the target is the local player) and clears it in a `finally`-style postfix.
  `AddInworldText` consumes it if present and matching (same frame, distance to `hit.m_point`
  under 1 m — cheap sanity check against a stray text from another caller).
- **Kill detection**: the ApplyDamage postfix checks `__instance.GetHealth() <= 0` (or
  `IsDead()`), and if the context recorded a spawned splash for this hit, upgrades it to the
  Kill style in place. The number is still on frame 0 of its animation, so this is invisible.
- Remote hits (target owned by another client) arrive with no context. They get the base style
  for their `TextType`, sized by absolute magnitude tiers. Your own hits on things you own (most
  of what you see) get full context. Hits **on you** always have context because you own your
  own player.

### 2.2 HitContext

```
struct HitContext {
  Character target; HitData hit;
  bool sneak, staggerCrit, ranged, dot;      // dot = hitType Burning/Freezing/Poisoned
  HitData.DamageType majority;               // for elemental colouring
  float targetMaxHealth;                     // for % magnitude
  bool attackerIsLocal, targetIsLocal, targetIsTame;
  SplashInstance spawned;                    // set by render layer, read by kill postfix
}
```

### 2.3 Style resolution

A `Style` is: font, base size, colour (or gradient top/bottom), outline width and colour, an
optional tag string ("SNEAK", "CRIT", "KILL"), an animation preset, and a duration. Resolution
order, later steps override earlier:

1. **Base by TextType** — the eight vanilla types, each with configurable colour (ColorfulDamage
   parity). Own damage taken keeps its red override.
2. **Elemental tint** (context only) — if the majority damage type is fire, frost, lightning,
   poison or spirit, use that colour: fire orange, frost ice-blue, lightning pale yellow, poison
   green, spirit off-white. Physical stays the TextType colour.
   **Changed in M3:** the tint applies only to `Normal` hits, not to weak, resistant or immune
   ones as this plan first had it. Those colours say something about the target that is worth
   more than what a tint says about the weapon, and a yellow "weak" hit turning ice-blue loses
   the one reading Valheim players actually act on. Note also that `RPC_Damage` strips fire,
   spirit and poison out of a hit before `ApplyDamage`, so a direct hit can only ever be tinted
   frost or lightning; fire, poison and spirit reach the numbers as their own ticks, which is
   where those tints show up.
3. **Magnitude tier** — sizes the number. With context: percent of `targetMaxHealth`, tiers
   default `<5% small, 5–20% normal, 20–50% big, ≥50% huge` with scale 0.8 / 1.0 / 1.35 / 1.8.
   Without context: absolute thresholds, defaults 10 / 40 / 120. Percent-of-target is the
   important design choice: a 30 hit is a big deal on a greyling and nothing on a fuling
   berserker; absolute thresholds cannot express that across biomes.
4. **Flags** — sneak → tag "SNEAK", one tier up, gold colour; stagger crit → tag "CRIT", one
   tier up, brief extra pop; kill → tag "KILL" (configurable; Norse flavour options "SKÅL",
   "VALHALLA"), max tier, longer duration, celebration animation.
5. **Distance** — vanilla's near/far split becomes a continuous size falloff between
   `nearDistance` and `maxDistance`, clamped, so far numbers shrink smoothly instead of snapping.
6. **Self** — damage to the local player gets its own style block (red, bigger outline, optional
   screen-edge flash for hits ≥ N% of max health).

### 2.4 Animation

Each splash runs a small keyframed timeline over `duration` in screen space, anchored to a world
point (so it stays glued to the hit location when the camera moves, as vanilla does):

- **Entrance** (0–0.18 s): scale from `popFrom` (default 1.8) down to 1.0 on an **ease-out-back**
  curve, so the number settles a little past its resting size instead of stopping dead; alpha
  from 0 to 1 over `fadeIn` (default 0.05 s). The `overshoot` setting is the back constant's
  multiplier, and at 0 the curve reduces exactly to the ease-out cubic (verified numerically),
  which is what keeps the Vanilla preset faithful.
- **Motion**: initial velocity `v0` with a random horizontal spread of ±`spray` degrees, gravity
  `g` pulling down, expressed in metres of world space so it reads the same at any distance.
  `g = 0`, `v0 = (0,1,0)` reproduces vanilla exactly.
- **Hold**: nothing.
- **Exit** (last 30%): alpha fades with the vanilla `1 - t^3` curve; optional shrink to 0.8.
- **Tilt**: each number is rotated by a random angle up to `tilt` degrees, so a run of hits
  reads as scattered rather than as a printed column. 0 in the Vanilla preset.
- **Presets** (name → the parameters above): `Vanilla`, `Pop` (entrance only), `Arc` (pop +
  gravity), `Fountain` (arc + wide spray), `Bounce` (arc with one floor bounce). Kill style adds
  a second scale pulse at 0.3 s.

Distance falloff and GUI scale are handled at position time: `WorldToScreenPointScaled` as
vanilla, so the mod respects the in-game GUI scale slider.

### 2.5 Merging rapid ticks

Burning does a tick per second, poison likewise, and a 5-arrow volley or a multi-hit swing
produces overlapping numbers. WoW SCT solves this by merging. Ours: if a number with the same
(target, TextType, self flag, DoT flag) was last fed within `mergeWindow`, add to its value,
re-render, and replay the entrance pop, which also restarts its life so it brightens. DoT ticks
get a 1.2 s window so a burning target shows one climbing number rather than a stack; direct
hits default to 0, off, because reading each hit is usually what you want.

**A merged number must follow its victim.** `SE_Burning` sets `hit.m_point` to
`m_character.GetCenterPoint()` on every tick, so consecutive ticks on anything that walks report
different places. A number that stays where the first tick landed falls behind the creature and
is soon behind the camera entirely, which is exactly what the first M4 test showed: the pool held
one number absorbing nine ticks, correctly, and none of them were on screen. So a merge moves the
number to the new tick's position and restarts its arc.

**Changed in M4:** this plan first said "is still in its first half of life". That rule would
have made the feature do nothing: burning ticks once a second and half of the default 1.8 s life
is 0.9 s, so no tick would ever have qualified. The window alone decides, with `mergeMaxLife`
(6 s) capping how long one number may keep growing so something burning forever does not leave a
number on screen forever. Tagged numbers (sneak, crit) never merge: those are events worth
reading on their own. Numbers with no hit context never merge either, having no victim to key on.

### 2.6 Fonts and outline

- **Game fonts only** (decided 2026-09-22). Sources are the `TMP_FontAsset`s already loaded by
  the game: Valheim-Norse, Valheim-Norse Bold, Valheim-AveriaSansLibre and their variants,
  enumerated via `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()` and resolved by name the way
  Recount's `Core/Fonts.cs` does. Norse Bold is the default for the Festive preset; the vanilla
  prefab font for the Vanilla preset. The config entry is an `AcceptableValueList` of what was
  found, so ConfigurationManager shows a dropdown. No OS fonts, no AssetBundle; if a bundled
  display font is ever wanted it is a separate milestone with its own Unity build step.
- Outline and underlay (drop shadow) are TMP SDF material properties (`_OutlineWidth`,
  `_OutlineColor`, `_UnderlayColor`, `_UnderlayOffsetX/Y`). We create one material instance per
  (font, style) and share it across splashes; never `material` per instance. Size is the main
  prominence lever, outline is the legibility lever (snow, sand, sky).
- **Outline needs face dilation** (verified on the rig, 2026-09-22). TMP grows an outline half
  *inward*, so an outline alone eats a thin face: at width 0.3 on Valheim-Norse the glyphs read
  as black blobs with a white hairline. The fix is `_FaceDilate` (the **Boldness** setting) to
  fatten the strokes first, then a modest outline: 0.28 / 0.18 for Festive, 0.22 / 0.15 for Bold.
  A cloned material must also be passed to `ShaderUtilities.UpdateShaderRatios`, or it keeps the
  source material's scale ratios and the new outline and underlay values are clamped.
- **Both Valheim-Norse faces draw the digit zero as a rune-like diamond**, so a blocked or immune
  hit reads as a symbol rather than "0". The presets therefore use **Valheim-AveriaSansLibre**,
  the game's own damage-text face, fattened by Boldness rather than swapped for a heavier face.
  Norse and Norsebold stay in the dropdown for anyone who wants the flavour.

### 2.7 Pooling and limits

Pool of `maxAlive` (default 64, configurable) splash objects created from `m_worldTextBase` on
first use; spawn beyond the cap evicts the oldest. Update loop iterates the active list in
reverse and swap-removes. No per-frame allocations: text formatting reuses a StringBuilder and
the numeric formatting matches vanilla `"0.#"` invariant.

### 2.8 Configuration

BepInEx `ConfigEntry`s, grouped so ConfigurationManager renders them sensibly, all hot-reloading
(re-resolve styles on `SettingChanged`):

- `General`: enabled, preset (Vanilla / Bold / Festive / Custom), visibility (All / Self / None
  for others' numbers, like ZenCombat), hide zeros, max distance.
- `Font`: font name, base size near, min size far, near/far distances, outline width/colour,
  shadow on/off.
- `Colours`: one per TextType, plus the five elemental tints, plus self-damage.
- `Magnitude`: percent tiers (3 thresholds), absolute tiers (3), tier scales (4).
- `Flags`: sneak/crit/kill enabled, their tags and colours, kill flavour text.
- `Animation`: preset, duration, pop scale, v0, gravity, spray, exit shrink.
- `Merge`: direct window, DoT window.
- `Self`: style block, edge-flash threshold.

Choosing `preset` other than Custom writes the preset's values into the other entries, so users
can start from Festive and tweak.

### 2.9 Console commands

`splash demo [seconds]` spawns a scripted sequence around the player (a normal hit, a resist, a
weak, a heal, a block, a "too hard", a bonus, self hits, two far ones) so styling can be judged
without finding a fight; with a hold it freezes them in place, since a held number left to fly
would fall out of the world. `splash rain [seconds]` trickles numbers so sliders can be dragged
against something on screen. `splash burst <n>` stress-tests pooling. `splash set <key> <value>`
and `splash get [filter]` tune any setting live, parsed and clamped by BepInEx's own converter.
`splash fonts`, `splash preset <name>`, `splash anim <name>`, `splash reload`, `splash stats`.
Mirrors Recount's `ConsoleCommands.cs`.

### 2.10 Compatibility

- ColorfulDamage and ZenCombat both prefix `AddInworldText` and return false. Harmony runs
  prefixes in priority order and stops at the first `false`, so whichever wins, the other is
  silently dead. On startup, detect `redseiko.valheim.colorfuldamage` and ZenCombat's GUID via
  `BepInEx.Bootstrap.Chainloader.PluginInfos`, log a clear warning, and (configurable) yield.
- Recount hooks `ApplyDamage` with prefix+postfix too; both are read-only and order-independent.
- No Jotunn dependency needed. Keep it BepInEx + Harmony only; that is one fewer thing to
  version-chase. Reference `Unity.TextMeshPro` and `UnityEngine.UI`.

---

## 3. Brainstorm — festive ideas, ranked

**Must (v1)**
- Bold outlined font, pop entrance, arc motion, smooth distance falloff.
- Magnitude tiers by % of target health.
- Sneak, stagger-crit and kill styling with tags.
- Elemental tints.
- Vanilla preset that is pixel-faithful.

**Should (v1.x)**
- DoT merge into a climbing number.
- Self-damage block with screen-edge flash.
- Personal-best hit → "NEW RECORD" splash (read Recount's record store if present, else keep a
  session max per creature name in memory).
- Combo counter: N hits within 2 s shows a small "×N" ticking up next to the latest number.
- Kill celebration variants: the number bursts into a ring of small runes / sparks (a few
  pooled TMP glyphs launched radially; no particle system needed).

**Could**
- Per-weapon-skill colour (swords vs axes vs bows) for people who want to read their build.
- Boss-hit flair: hits on bosses get the gold treatment regardless of tier.
- Numbers that "land" on the ground and rest for a beat before fading (Bounce preset).
- Damage taken by tames shown with a paw tag.
- Seasonal palettes (midsummer, yule) as presets, since "festive" was the brief.

**Later, not now** (decided 2026-09-22)
- Sound: a soft chime on sneak/crit, a horn on kill. Client-side SFX via a pooled AudioSource;
  Recount's `Fanfare.cs` shows how to find usable prefab sounds. Revisit once the look is settled.
- Bundled display font via AssetBundle.

**Won't**
- Anything that changes RPC payloads or requires the server to have the mod.
- Random crits or any gameplay change. Presentation only.
- Screen shake. Valheim already has hit-stop and camera kick; stacking more makes people ill.

---

## 4. Milestones

| # | Deliverable | Done when |
|---|---|---|
| M0 | Project skeleton: copy Recount's `Directory.Build.props`, csproj (renamed, publicizer on), `build/*.sh` with paths swapped, `lib/` refreshed from the rig, manifest, README stub. | `./build/deploy.sh` produces `DamageSplash.dll` and it loads on the rig with a log line. |
| M1 ✅ | Render layer: replace `AddInworldText`/`UpdateWorldTexts`; pool; font, outline, near/far falloff; Vanilla and Pop presets; `splash demo`. | **Done 2026-09-22.** Verified on the rig: `docs/images/crop-vanilla.png` (stock look reproduced) against `crop-averia.png` (Festive), and `crop-motion.png` for a 60-number burst. |
| M2 ✅ | Animation: arc/gravity/spray, exit shrink, presets Arc/Fountain/Bounce; full config surface with hot reload. | **Done 2026-09-22.** Ease-out-back entrance, fade-in, tilt, the five presets behind one `AnimPresetValues` table, `splash set`/`get` and `splash rain`. Verified on the rig: `docs/images/crop-m2-arc.png`, `crop-m2-fountain.png`, `crop-m2-bounce.png`; preset switches and individual settings take effect on the next number with no restart. The entrance curve is a sub-0.2 s transient a still cannot show, so it was checked numerically instead (overshoot 0 reproduces the old ease-out cubic to 2e-16). |
| M3 | Context layer: ApplyDamage prefix + kill postfix, HitContext pairing, magnitude tiers by %, sneak/crit/kill tags, elemental tints. | `splash demo` and a real Black Forest fight show all six styles; a stray context never mis-tags another caller's text (distance check logged under Verbose). **Done 2026-09-22.** Verified on the rig with `splash demo`: `docs/images/crop-m3-styles.png` (daylight) and `m3-daylight.png` (full frame). Colours were checked by sampling the rendered pixels rather than by eye, on a night capture where eleven of the twelve appear at their exact configured RGB; heal is the twelfth only because it is drawn at 0.7 alpha and so blends with the scene. Magnitude, the three tags and the kill restyle all behave. **One criterion was waived by the user:** the live Black Forest fight was not run, so the styling is proven but the context hooks have not been seen firing on real damage. Nothing in the log suggests otherwise (no exceptions from our patches), but it is untested. Worth doing before release. Scope call: only `ApplyDamage` carries context. Blocked, heal, bonus and "too hard" numbers fall back to absolute magnitude with no flags, which is all those callers can support anyway, and it keeps one hook instead of three. |
| M4 | Merge windows, self block with edge flash, visibility modes, compatibility detection. | Burning troll shows one climbing number; ColorfulDamage installed alongside logs the warning. **Done 2026-09-22.** All four verified on the rig. Merging: one number absorbed a 45 s burn, climbing 6 → 26 (`docs/images/crop-m4-merge.png`), after the fix above. Edge flash: red at all four edges, centre untouched (`m4-flash.png`). Visibility: a 20-number demo drew 20 / 6 / 4 under All / Mine / None, the 6 being block, heal, too-hard, bonus and the two self numbers, and the 4 those minus self. Conflict: with a pretend conflict the warning logged and the pool went to 0 active / 20 free, which is DamageSplash drawing nothing and vanilla taking the lot. Untested: the edge-flash threshold on real damage taken, and the warning against a genuinely installed ColorfulDamage. |
| M5 | Polish and ship. | **Package ready, not published (2026-09-22).** `dist/DamageSplash-0.1.0.zip`, 102 KB, validated by `build/package.sh`; `build/publish.sh --dry-run` generates the config cleanly. Versions agree across `Plugin.cs`, the csproj and `manifest.json`. Icon is the game's own letterforms lifted from a screenshot (see `build/make_icon.py`), README written for the store page. **Publishing is deliberately left to the user**: a Thunderstore version can never be replaced or deleted, and it needs their team name and service-account token. **Published 2026-09-22**: thunderstore.io/c/valheim/p/Jumpingmushroom/DamageSplash 0.1.0, alongside the repo at github.com/jumpingmushroom/DamageSplash with description and topics. Note the Thunderstore namespace is `Jumpingmushroom`, capital J, which is not the GitHub account spelling. A first submission appeared to be rejected and a retry then failed as a duplicate version, but the package went live regardless; the public API 404s until a package is visible, so an anonymous check is the only honest way to confirm one is live. Still outstanding: the live-combat check. Screenshots were trimmed from 82 MB to 14 MB before the first commit, the crops stored at native size and the icon generator repointed at a 120x84 crop instead of a full frame. |
| M6 (optional) | Kill burst; record splash; combo counter. | — |

---

## 5. Risks and open questions

- **Font enumeration timing.** `TMP_FontAsset`s are not all loaded at plugin `Awake`;
  ColorfulDamage defers the font config bind to `FejdStartup.Awake` and Recount resolves lazily.
  Do the same, and re-resolve on `SettingChanged`.
- ~~**Prefab material.**~~ Resolved in M1: `m_worldTextBase` uses `TextMeshPro/Distance Field`
  with `Valheim-AveriaSansLibre`, so outline and underlay properties work. See §2.6 for the two
  things that must go with them (face dilation and a shader-ratio update).
- **Context leak.** `ShowText` inside `ApplyDamage` is the only DamageText call on that stack,
  but `ApplyDamage` can recurse via status effects on death. Clear `HitContext.Current` in the
  postfix and pair by proximity to `hit.m_point`, never by "whatever is current".
- **Percent tiers on non-characters.** Trees, rocks and structures have health but our context
  hook is Character-only. v1 uses absolute tiers for them; a later `IDestructible` hook could
  add percent for `WearNTear`/`TreeBase` if it feels wrong in practice.
- **Numbers before difficulty scaling.** Vanilla shows `totalDamage` before the per-player and
  world-level multipliers; the health bar loses a different amount. Match vanilla (show the same
  number); note it in the README FAQ because Recount users will notice the mismatch.
- **200-text vanilla cap vs our pool.** Our cap replaces theirs; keep the "skip zeros when
  busy" rule so a shield-wall of "0"s never crowds real numbers.

---

## 6. Repository layout (mirrors Recount)

```
DamageSplash/
  Directory.Build.props   build/{deploy,logs,shot,package,publish}.sh   lib/ (gitignored)
  thunderstore/manifest.json  README.md  CHANGELOG.md  PLAN.md  CLAUDE.md
  src/DamageSplash/
    Plugin.cs            BepInPlugin, Harmony.CreateAndPatchAll, ConsoleCommands.Register
    PluginConfig.cs      all ConfigEntrys + presets
    Core/Fonts.cs        game TMP font resolution (lift from Recount)
    Core/Styles.cs       Style, StyleResolver (§2.3)
    Core/HitContext.cs   §2.2
    Core/SplashPool.cs   pooled instances, spawn/evict
    Core/Animator.cs     timelines and presets (§2.4)
    Core/Merger.cs       §2.5
    Core/ConsoleCommands.cs
    Patches/DamageTextPatches.cs   render layer
    Patches/ContextPatches.cs      Character/Humanoid prefixes and postfix
```
