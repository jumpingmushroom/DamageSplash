using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using DamageSplash.Core;
using UnityEngine;

namespace DamageSplash
{
    /// <summary>
    /// Read by ConfigurationManager, so the fields carry an Order attribute and sit in a few
    /// sections. Choosing a preset writes that preset's values into every other entry, and
    /// touching any other entry flips the preset to Custom, so the dropdown always tells the
    /// truth about what is on screen.
    /// </summary>
    public static class PluginConfig
    {
        public const string PresetCustom = "Custom";
        public const string VisibilityAll = "All";
        public const string VisibilityMine = "Mine";
        public const string VisibilityNone = "None";
        public static readonly string[] PresetNames = { "Vanilla", "Bold", "Festive", PresetCustom };
        public static readonly string[] AnimPresetNames = { "Vanilla", "Pop", "Arc", "Fountain", "Bounce", PresetCustom };

        public static event Action Changed;

        // General
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> Preset;
        public static ConfigEntry<float> MaxDistance;
        public static ConfigEntry<bool> HideZeros;
        public static ConfigEntry<int> MaxAlive;
        public static ConfigEntry<bool> Verbose;
#if DEBUG
        public static ConfigEntry<bool> DevCommandFile;   // Debug builds only; see Plugin.RunCommandFile
#endif

        // Font
        /// <summary>Both Valheim-Norse faces draw the digit zero as a rune; see Fonts.PreferredDisplay.</summary>
        public static ConfigEntry<string> FontName;
        public static ConfigEntry<bool> FauxBold;
        public static ConfigEntry<float> NearSize;
        public static ConfigEntry<float> FarSize;
        public static ConfigEntry<float> NearDistance;
        public static ConfigEntry<float> FarDistance;
        public static ConfigEntry<float> FaceDilate;
        public static ConfigEntry<float> OutlineWidth;
        public static ConfigEntry<Color> OutlineColor;
        public static ConfigEntry<bool> Shadow;
        public static ConfigEntry<float> ShadowOffset;

        // Colours
        public static ConfigEntry<Color> ColorNormal;
        public static ConfigEntry<Color> ColorResistant;
        public static ConfigEntry<Color> ColorWeak;
        public static ConfigEntry<Color> ColorImmune;
        public static ConfigEntry<Color> ColorHeal;
        public static ConfigEntry<Color> ColorTooHard;
        public static ConfigEntry<Color> ColorBlocked;
        public static ConfigEntry<Color> ColorBonus;
        public static ConfigEntry<Color> ColorSelfDamage;
        public static ConfigEntry<Color> ColorSelfNoDamage;

        // Hits
        public static ConfigEntry<bool> UseHitContext;
        public static ConfigEntry<bool> ElementalTints;
        public static ConfigEntry<Color> ColorFire;
        public static ConfigEntry<Color> ColorFrost;
        public static ConfigEntry<Color> ColorLightning;
        public static ConfigEntry<Color> ColorPoison;
        public static ConfigEntry<Color> ColorSpirit;
        public static ConfigEntry<bool> SneakEnabled;
        public static ConfigEntry<string> SneakTag;
        public static ConfigEntry<Color> SneakColor;
        public static ConfigEntry<bool> CritEnabled;
        public static ConfigEntry<string> CritTag;
        public static ConfigEntry<Color> CritColor;
        public static ConfigEntry<bool> KillEnabled;
        public static ConfigEntry<string> KillTag;
        public static ConfigEntry<Color> KillColor;
        public static ConfigEntry<float> KillDurationScale;
        public static ConfigEntry<float> TagScale;

        // Merging
        public static ConfigEntry<float> DotMergeWindow;
        public static ConfigEntry<float> HitMergeWindow;
        public static ConfigEntry<float> MergeMaxLife;

        // Self
        public static ConfigEntry<string> Visibility;
        public static ConfigEntry<bool> EdgeFlash;
        public static ConfigEntry<float> EdgeFlashPercent;
        public static ConfigEntry<Color> EdgeFlashColor;
        public static ConfigEntry<float> EdgeFlashFade;
        public static ConfigEntry<float> EdgeFlashThickness;

        // Compatibility
        public static ConfigEntry<bool> YieldToOtherMods;
        public static ConfigEntry<bool> DebugSimulateConflict;

        // Magnitude
        public static ConfigEntry<bool> MagnitudeScaling;
        public static ConfigEntry<float> SmallBelowPercent;
        public static ConfigEntry<float> BigAbovePercent;
        public static ConfigEntry<float> HugeAbovePercent;
        public static ConfigEntry<float> SmallBelowDamage;
        public static ConfigEntry<float> BigAboveDamage;
        public static ConfigEntry<float> HugeAboveDamage;
        public static ConfigEntry<float> ScaleSmall;
        public static ConfigEntry<float> ScaleBig;
        public static ConfigEntry<float> ScaleHuge;

        // Animation
        public static ConfigEntry<string> AnimPreset;
        public static ConfigEntry<float> Duration;
        public static ConfigEntry<float> PopFrom;
        public static ConfigEntry<float> PopDuration;
        public static ConfigEntry<float> Overshoot;
        public static ConfigEntry<float> FadeIn;
        public static ConfigEntry<float> Tilt;
        public static ConfigEntry<float> RiseSpeed;
        public static ConfigEntry<float> Gravity;
        public static ConfigEntry<float> Spray;
        public static ConfigEntry<float> ExitShrink;
        public static ConfigEntry<bool> Bounce;
        public static ConfigEntry<float> Jitter;

        private static ConfigFile _cfg;
        private static bool _applying;
        private const string FontDescription = "Font for the numbers. \"Vanilla\" keeps the game's own damage-text font. The list is filled with every TextMeshPro font the game has loaded once you are in a world; use `splash fonts` to refresh it.";

        public static void Bind(ConfigFile cfg)
        {
            _cfg = cfg;
            int o = 0;

            Enabled = cfg.Bind("1 General", "Enabled", true, D("Turn the mod off and the vanilla renderer takes over on the next hit.", o++));
            Preset = cfg.Bind("1 General", "Preset", "Festive", D("Vanilla: the stock look. Bold: same motion, bigger outlined font. Festive: bold plus a pop and an arc. Choosing one overwrites the entries below; changing any of them flips this to Custom.", o++, PresetNames));
            MaxDistance = cfg.Bind("1 General", "MaxDistance", 30f, D("Numbers further than this from the camera are not shown. Vanilla: 30.", o++, new AcceptableValueRange<float>(0f, 100f)));
            HideZeros = cfg.Bind("1 General", "HideZeros", false, D("Do not show hits for 0 damage at all. Vanilla shows a grey 0.", o++));
            MaxAlive = cfg.Bind("1 General", "MaxAlive", 64, D("How many numbers can be on screen at once; the oldest is dropped beyond that. Vanilla has no cap (200 for zeros).", o++, new AcceptableValueRange<int>(8, 400)));
            Verbose = cfg.Bind("1 General", "Verbose", false, D("Extra lines in the BepInEx log.", o++, null, true));
#if DEBUG
            DevCommandFile = cfg.Bind("1 General", "DevCommandFile", false, D("Development aid: run console commands written to BepInEx/config/DamageSplash.cmd, one per line, then delete the file.", o++, null, true));
#endif

            // Bound without an acceptable-value list on purpose: with one, BepInEx clamps a saved
            // name that is not in it back to the default before the fonts are known. The list
            // is attached by RebindFontChoices once a world is loaded.
            FontName = cfg.Bind("2 Font", "Font", Fonts.VanillaName, D(FontDescription, o++));
            FauxBold = cfg.Bind("2 Font", "FauxBold", false, D("Apply TextMeshPro's bold style on top of the font. Useful when only a regular weight is loaded.", o++));
            NearSize = cfg.Bind("2 Font", "NearSize", 30f, D("Font size for hits at or closer than NearDistance. Vanilla: 16.", o++, new AcceptableValueRange<float>(4f, 96f)));
            FarSize = cfg.Bind("2 Font", "FarSize", 14f, D("Font size for hits at or beyond FarDistance. Vanilla: 8.", o++, new AcceptableValueRange<float>(2f, 96f)));
            NearDistance = cfg.Bind("2 Font", "NearDistance", 8f, D("Metres. Size shrinks smoothly from NearSize at this distance to FarSize at FarDistance. Set both equal for vanilla's hard step at 10 m.", o++, new AcceptableValueRange<float>(0f, 100f)));
            FarDistance = cfg.Bind("2 Font", "FarDistance", 30f, D("Metres. See NearDistance.", o++, new AcceptableValueRange<float>(0f, 100f)));
            FaceDilate = cfg.Bind("2 Font", "Boldness", 0.28f, D("Fattens (positive) or thins (negative) the strokes. TextMeshPro's outline grows half inward, so pair an outline with some boldness or thin fonts go black. Vanilla: 0.", o++, new AcceptableValueRange<float>(-0.5f, 0.6f)));
            OutlineWidth = cfg.Bind("2 Font", "OutlineWidth", 0.18f, D("Outline thickness, 0 to 1 in TextMeshPro units. 0 disables the outline. Keep it modest; see Boldness. Vanilla: 0.", o++, new AcceptableValueRange<float>(0f, 1f)));
            OutlineColor = cfg.Bind("2 Font", "OutlineColor", new Color(0f, 0f, 0f, 1f), D("Outline colour.", o++));
            Shadow = cfg.Bind("2 Font", "Shadow", true, D("Soft drop shadow behind the number. Vanilla: off.", o++));
            ShadowOffset = cfg.Bind("2 Font", "ShadowOffset", 0.3f, D("Shadow offset, 0 to 1.", o++, new AcceptableValueRange<float>(0f, 1f)));

            ColorNormal = cfg.Bind("3 Colours", "Normal", Color.white, D("Plain hits on enemies and things.", o++));
            ColorResistant = cfg.Bind("3 Colours", "Resistant", new Color(0.6f, 0.6f, 0.6f, 1f), D("Target resists the damage type.", o++));
            ColorWeak = cfg.Bind("3 Colours", "Weak", new Color(1f, 1f, 0f, 1f), D("Target is weak to the damage type.", o++));
            ColorImmune = cfg.Bind("3 Colours", "Immune", new Color(0.6f, 0.6f, 0.6f, 1f), D("Target is immune.", o++));
            ColorHeal = cfg.Bind("3 Colours", "Heal", new Color(0.5f, 1f, 0.5f, 0.7f), D("Healing, shown as +N.", o++));
            ColorTooHard = cfg.Bind("3 Colours", "TooHard", new Color(0.8f, 0.7f, 0.7f, 1f), D("The \"too hard\" message on rocks and trees your tool cannot damage.", o++));
            ColorBlocked = cfg.Bind("3 Colours", "Blocked", Color.white, D("Damage you blocked with a shield or weapon.", o++));
            ColorBonus = cfg.Bind("3 Colours", "Bonus", new Color(1f, 0.63f, 0.24f, 1f), D("Crafting and gathering bonuses (+N).", o++));
            ColorSelfDamage = cfg.Bind("3 Colours", "SelfDamage", new Color(1f, 0f, 0f, 1f), D("Damage taken by you or your tames.", o++));
            ColorSelfNoDamage = cfg.Bind("3 Colours", "SelfNoDamage", new Color(0.5f, 0.5f, 0.5f, 1f), D("A 0 on you or your tames.", o++));

            UseHitContext = cfg.Bind("5 Hits", "UseHitContext", true, D("Read the hit behind each number, which is what sneak, crit, kill, elemental tints and magnitude all rest on. Off means every number is styled by its kind alone, as vanilla does. Only hits resolved on your own machine carry this: yours on things you own, and everything that hits you.", o++));
            MagnitudeScaling = cfg.Bind("5 Hits", "MagnitudeScaling", true, D("Size the number by how much the hit mattered. With hit context that is a share of the target's maximum health, so 30 on a greyling reads big and 30 on a fuling berserker does not; without it, by the raw number.", o++));
            ElementalTints = cfg.Bind("5 Hits", "ElementalTints", true, D("Colour plain hits by their damage type. Weak, resistant and immune hits keep their own colour, because what they say is worth more than a tint.", o++));
            SneakEnabled = cfg.Bind("5 Hits", "SneakEnabled", true, D("Mark sneak attacks, the multiplier Valheim gives for hitting an unaware target.", o++));
            CritEnabled = cfg.Bind("5 Hits", "CritEnabled", true, D("Mark hits on a staggered creature, which Valheim doubles. This is the closest thing the game has to a critical hit.", o++));
            KillEnabled = cfg.Bind("5 Hits", "KillEnabled", true, D("Mark the blow that kills.", o++));
            SneakTag = cfg.Bind("5 Hits", "SneakTag", "SNEAK", D("Word shown with a sneak attack. Empty for none.", o++));
            CritTag = cfg.Bind("5 Hits", "CritTag", "CRIT", D("Word shown with a hit on a staggered creature. Empty for none.", o++));
            KillTag = cfg.Bind("5 Hits", "KillTag", "KILL", D("Word shown with a killing blow. Empty for none. Try SKAL or VALHALLA.", o++));
            SneakColor = cfg.Bind("5 Hits", "SneakColor", new Color(1f, 0.85f, 0.3f, 1f), D("Colour of a sneak attack.", o++));
            CritColor = cfg.Bind("5 Hits", "CritColor", new Color(1f, 0.55f, 0.15f, 1f), D("Colour of a hit on a staggered creature.", o++));
            KillColor = cfg.Bind("5 Hits", "KillColor", new Color(1f, 0.3f, 0.25f, 1f), D("Colour of a killing blow.", o++));
            KillDurationScale = cfg.Bind("5 Hits", "KillDurationScale", 1.6f, D("How much longer a killing blow stays on screen.", o++, new AcceptableValueRange<float>(1f, 4f)));
            TagScale = cfg.Bind("5 Hits", "TagScale", 0.55f, D("Size of the tag word relative to the number.", o++, new AcceptableValueRange<float>(0.2f, 1.5f)));
            ColorFire = cfg.Bind("5 Hits", "FireColor", new Color(1f, 0.5f, 0.15f, 1f), D("Tint for fire damage.", o++));
            ColorFrost = cfg.Bind("5 Hits", "FrostColor", new Color(0.55f, 0.85f, 1f, 1f), D("Tint for frost damage.", o++));
            ColorLightning = cfg.Bind("5 Hits", "LightningColor", new Color(1f, 0.95f, 0.5f, 1f), D("Tint for lightning damage.", o++));
            ColorPoison = cfg.Bind("5 Hits", "PoisonColor", new Color(0.55f, 0.9f, 0.3f, 1f), D("Tint for poison damage.", o++));
            ColorSpirit = cfg.Bind("5 Hits", "SpiritColor", new Color(0.85f, 0.95f, 1f, 1f), D("Tint for spirit damage.", o++));

            DotMergeWindow = cfg.Bind("7 Merging", "DotMergeWindow", 1.2f, D("Seconds. Burning, poison and the other ticks on one victim add into a single number that climbs, instead of stacking up one per tick. 0 turns it off.", o++, new AcceptableValueRange<float>(0f, 5f)));
            HitMergeWindow = cfg.Bind("7 Merging", "HitMergeWindow", 0f, D("The same for ordinary hits on one target, for volleys and multi-hit swings. 0 (the default) keeps every hit its own number, which is usually what you want to read.", o++, new AcceptableValueRange<float>(0f, 5f)));
            MergeMaxLife = cfg.Bind("7 Merging", "MergeMaxLife", 6f, D("Seconds. A climbing number stops accepting more after this long and a fresh one starts, so something burning forever does not leave one number on screen forever.", o++, new AcceptableValueRange<float>(1f, 30f)));

            Visibility = cfg.Bind("8 Self", "Visibility", VisibilityAll, D("All: every number, as vanilla does. Mine: only hits you dealt and damage done to you. None: no damage numbers at all, though blocks, heals and bonuses still show. Only hits resolved on your own machine can be told apart, so in multiplayer Mine also hides other people's hits on creatures they own.", o++, new[] { VisibilityAll, VisibilityMine, VisibilityNone }));
            EdgeFlash = cfg.Bind("8 Self", "EdgeFlash", true, D("Flash the edges of the screen when you take a heavy hit.", o++));
            EdgeFlashPercent = cfg.Bind("8 Self", "EdgeFlashPercent", 0.12f, D("Share of your maximum health a hit must take to flash the screen.", o++, new AcceptableValueRange<float>(0.01f, 1f)));
            EdgeFlashColor = cfg.Bind("8 Self", "EdgeFlashColor", new Color(0.7f, 0f, 0f, 0.55f), D("Colour of the flash. The alpha is how strong it gets at its peak.", o++));
            EdgeFlashFade = cfg.Bind("8 Self", "EdgeFlashFade", 0.6f, D("Seconds the flash takes to fade out.", o++, new AcceptableValueRange<float>(0.1f, 3f)));
            EdgeFlashThickness = cfg.Bind("8 Self", "EdgeFlashThickness", 0.35f, D("How far in from the edge the flash reaches, as a share of half the screen.", o++, new AcceptableValueRange<float>(0.05f, 1f)));

            YieldToOtherMods = cfg.Bind("9 Compatibility", "YieldToOtherMods", true, D("Another mod that redraws the damage numbers (ColorfulDamage, ZenCombat) claims the same method, and whichever runs first silently wins. With this on, DamageSplash stands aside and lets the other one draw.", o++));
            DebugSimulateConflict = cfg.Bind("9 Compatibility", "DebugSimulateConflict", false, D("Pretend a conflicting mod is installed, to check what this mod does about it.", o++, null, true));

            SmallBelowPercent = cfg.Bind("6 Magnitude", "SmallBelowPercent", 0.05f, D("A hit worth less than this share of the target's maximum health is drawn small.", o++, new AcceptableValueRange<float>(0f, 1f)));
            BigAbovePercent = cfg.Bind("6 Magnitude", "BigAbovePercent", 0.2f, D("At or above this share of maximum health a hit is drawn big.", o++, new AcceptableValueRange<float>(0f, 1f)));
            HugeAbovePercent = cfg.Bind("6 Magnitude", "HugeAbovePercent", 0.5f, D("At or above this share of maximum health a hit is drawn huge.", o++, new AcceptableValueRange<float>(0f, 1f)));
            SmallBelowDamage = cfg.Bind("6 Magnitude", "SmallBelowDamage", 10f, D("Fallback for numbers with no hit context: below this they are drawn small.", o++, new AcceptableValueRange<float>(0f, 10000f)));
            BigAboveDamage = cfg.Bind("6 Magnitude", "BigAboveDamage", 40f, D("Fallback for numbers with no hit context: at or above this they are drawn big.", o++, new AcceptableValueRange<float>(0f, 10000f)));
            HugeAboveDamage = cfg.Bind("6 Magnitude", "HugeAboveDamage", 120f, D("Fallback for numbers with no hit context: at or above this they are drawn huge.", o++, new AcceptableValueRange<float>(0f, 10000f)));
            ScaleSmall = cfg.Bind("6 Magnitude", "ScaleSmall", 0.8f, D("Size multiplier for a small hit. Normal hits are 1.", o++, new AcceptableValueRange<float>(0.2f, 3f)));
            ScaleBig = cfg.Bind("6 Magnitude", "ScaleBig", 1.35f, D("Size multiplier for a big hit.", o++, new AcceptableValueRange<float>(0.2f, 3f)));
            ScaleHuge = cfg.Bind("6 Magnitude", "ScaleHuge", 1.8f, D("Size multiplier for a huge hit, and for a killing blow.", o++, new AcceptableValueRange<float>(0.2f, 3f)));

            AnimPreset = cfg.Bind("4 Animation", "AnimPreset", "Arc", D("Vanilla: straight drift up. Pop: vanilla drift with a scale-in. Arc: pop plus gravity and a little spray. Fountain: high and wide. Bounce: lands and bounces once. Choosing one overwrites the entries below.", o++, AnimPresetNames));
            Duration = cfg.Bind("4 Animation", "Duration", 1.8f, D("Seconds on screen. Vanilla: 1.5.", o++, new AcceptableValueRange<float>(0.2f, 10f)));
            PopFrom = cfg.Bind("4 Animation", "PopFrom", 1.8f, D("Scale the number appears at before settling to 1. 1 disables the pop.", o++, new AcceptableValueRange<float>(0.2f, 4f)));
            PopDuration = cfg.Bind("4 Animation", "PopDuration", 0.18f, D("Seconds the pop takes to settle.", o++, new AcceptableValueRange<float>(0f, 1f)));
            Overshoot = cfg.Bind("4 Animation", "Overshoot", 0.5f, D("How far the pop settles past its resting size before coming back, 0 to 1. 0 is a plain ease-out.", o++, new AcceptableValueRange<float>(0f, 1f)));
            FadeIn = cfg.Bind("4 Animation", "FadeIn", 0.05f, D("Seconds the number takes to fade in. 0 makes it appear at once, as vanilla does.", o++, new AcceptableValueRange<float>(0f, 0.5f)));
            Tilt = cfg.Bind("4 Animation", "Tilt", 6f, D("Degrees. Each number is rotated by a random angle up to this, so a run of hits does not read as a printed column.", o++, new AcceptableValueRange<float>(0f, 30f)));
            RiseSpeed = cfg.Bind("4 Animation", "RiseSpeed", 2.2f, D("Initial upward speed in metres per second. Vanilla: 1.", o++, new AcceptableValueRange<float>(0f, 10f)));
            Gravity = cfg.Bind("4 Animation", "Gravity", 4f, D("Downward acceleration in metres per second squared. 0 is vanilla's straight drift.", o++, new AcceptableValueRange<float>(0f, 30f)));
            Spray = cfg.Bind("4 Animation", "Spray", 35f, D("Degrees. The launch direction is tilted off vertical by a random angle up to this, in a random direction.", o++, new AcceptableValueRange<float>(0f, 90f)));
            ExitShrink = cfg.Bind("4 Animation", "ExitShrink", 0.7f, D("Scale the number shrinks to over its last third while fading. 1 disables the shrink.", o++, new AcceptableValueRange<float>(0.1f, 1.5f)));
            Bounce = cfg.Bind("4 Animation", "Bounce", false, D("Numbers that fall below their spawn height bounce once.", o++));
            Jitter = cfg.Bind("4 Animation", "Jitter", 0.3f, D("Random offset of the spawn point in metres, so overlapping hits separate. Vanilla: 0.5.", o++, new AcceptableValueRange<float>(0f, 2f)));

            cfg.SettingChanged -= OnSettingChanged;
            cfg.SettingChanged += OnSettingChanged;

            // Presets apply on load only when the file was written by a fresh install: the
            // stored entries are then the defaults, which already equal Festive.
        }

        private static ConfigDescription D(string desc, int order, AcceptableValueBase acceptable = null, bool advanced = false)
        {
            return new ConfigDescription(desc, acceptable, new ConfigurationManagerAttributes { Order = 1000 - order, IsAdvanced = advanced });
        }

        private static ConfigDescription D(string desc, int order, string[] choices)
        {
            return D(desc, order, new AcceptableValueList<string>(choices));
        }

        private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (_applying)
                return;

            ConfigEntryBase e = args.ChangedSetting;
            if (e == Preset)
            {
                ApplyPreset(Preset.Value);
                return;
            }
            if (e == AnimPreset)
            {
                ApplyAnimPreset(AnimPreset.Value);
                return;
            }

            // A hand edit: the presets no longer describe what is on screen.
            _applying = true;
            try
            {
                if (IsVisual(e) && Preset.Value != PresetCustom)
                    Preset.Value = PresetCustom;
                if (IsAnim(e) && AnimPreset.Value != PresetCustom)
                    AnimPreset.Value = PresetCustom;
            }
            finally { _applying = false; }

            Raise();
        }

        private static bool IsAnim(ConfigEntryBase e)
        {
            return e == Duration || e == PopFrom || e == PopDuration || e == Overshoot || e == FadeIn
                || e == Tilt || e == RiseSpeed || e == Gravity || e == Spray || e == ExitShrink
                || e == Bounce || e == Jitter;
        }

        private static bool IsVisual(ConfigEntryBase e)
        {
#if DEBUG
            if (e == DevCommandFile)
                return false;
#endif
            return e != Enabled && e != Verbose && e != MaxDistance && e != HideZeros
                && e != MaxAlive && e != Visibility && e != YieldToOtherMods && e != DebugSimulateConflict;
        }

        public static void ApplyPreset(string name)
        {
            _applying = true;
            try
            {
                switch (name)
                {
                    case "Vanilla":
                        FontName.Value = Fonts.VanillaName; FauxBold.Value = false;
                        NearSize.Value = 16f; FarSize.Value = 8f; NearDistance.Value = 10f; FarDistance.Value = 10f;
                        FaceDilate.Value = 0f; OutlineWidth.Value = 0f; Shadow.Value = false;
                        ResetColors();
                        SetHitStyling(magnitude: false, tints: false, flags: false);
                        SetExtras(false);
                        AnimPreset.Value = "Vanilla"; SetAnim(AnimPresetValues("Vanilla"));
                        break;
                    case "Bold":
                        FontName.Value = Fonts.PreferredDisplay(); FauxBold.Value = false;
                        NearSize.Value = 24f; FarSize.Value = 12f; NearDistance.Value = 8f; FarDistance.Value = 30f;
                        FaceDilate.Value = 0.22f; OutlineWidth.Value = 0.15f; OutlineColor.Value = Color.black; Shadow.Value = true; ShadowOffset.Value = 0.3f;
                        ResetColors();
                        SetHitStyling(magnitude: true, tints: false, flags: true);
                        SetExtras(true);
                        AnimPreset.Value = "Pop"; SetAnim(AnimPresetValues("Pop"));
                        break;
                    case "Festive":
                        FontName.Value = Fonts.PreferredDisplay(); FauxBold.Value = false;
                        NearSize.Value = 30f; FarSize.Value = 14f; NearDistance.Value = 8f; FarDistance.Value = 30f;
                        FaceDilate.Value = 0.28f; OutlineWidth.Value = 0.18f; OutlineColor.Value = Color.black; Shadow.Value = true; ShadowOffset.Value = 0.3f;
                        ResetColors();
                        SetHitStyling(magnitude: true, tints: true, flags: true);
                        SetExtras(true);
                        AnimPreset.Value = "Arc"; SetAnim(AnimPresetValues("Arc"));
                        break;
                    default:
                        return;
                }
                if (Preset.Value != name)
                    Preset.Value = name;
            }
            finally { _applying = false; }
            Raise();
        }

        public static void ApplyAnimPreset(string name)
        {
            _applying = true;
            try
            {
                switch (name)
                {
                    case "Vanilla":
                    case "Pop":
                    case "Arc":
                    case "Fountain":
                    case "Bounce":
                        SetAnim(AnimPresetValues(name));
                        break;
                    default: return;
                }
                if (AnimPreset.Value != name)
                    AnimPreset.Value = name;
                if (Preset.Value != PresetCustom)
                    Preset.Value = PresetCustom;
            }
            finally { _applying = false; }
            Raise();
        }

        /// <summary>The motion each animation preset stands for. One place, so the preset
        /// dropdown and the three look presets cannot drift apart.</summary>
        public static AnimParams AnimPresetValues(string name)
        {
            switch (name)
            {
                case "Vanilla":
                    return AnimParams.Vanilla;
                case "Pop":
                    return new AnimParams { Duration = 1.5f, PopFrom = 1.5f, PopDuration = 0.15f, Overshoot = 0.3f, FadeIn = 0.05f, Tilt = 0f, RiseSpeed = 1f, Gravity = 0f, Spray = 0f, ExitShrink = 0.85f, Bounce = false, Jitter = 0.3f };
                case "Arc":
                    return new AnimParams { Duration = 1.8f, PopFrom = 1.8f, PopDuration = 0.18f, Overshoot = 0.5f, FadeIn = 0.05f, Tilt = 6f, RiseSpeed = 2.2f, Gravity = 4f, Spray = 35f, ExitShrink = 0.7f, Bounce = false, Jitter = 0.3f };
                case "Fountain":
                    return new AnimParams { Duration = 2f, PopFrom = 1.6f, PopDuration = 0.15f, Overshoot = 0.5f, FadeIn = 0.05f, Tilt = 10f, RiseSpeed = 3.5f, Gravity = 7f, Spray = 70f, ExitShrink = 0.6f, Bounce = false, Jitter = 0.3f };
                case "Bounce":
                    return new AnimParams { Duration = 2f, PopFrom = 1.6f, PopDuration = 0.15f, Overshoot = 0.6f, FadeIn = 0.05f, Tilt = 8f, RiseSpeed = 3f, Gravity = 9f, Spray = 30f, ExitShrink = 0.8f, Bounce = true, Jitter = 0.3f };
                default:
                    return CurrentAnim();
            }
        }

        private static void SetAnim(AnimParams a)
        {
            Duration.Value = a.Duration; PopFrom.Value = a.PopFrom; PopDuration.Value = a.PopDuration;
            Overshoot.Value = a.Overshoot; FadeIn.Value = a.FadeIn; Tilt.Value = a.Tilt;
            RiseSpeed.Value = a.RiseSpeed; Gravity.Value = a.Gravity; Spray.Value = a.Spray;
            ExitShrink.Value = a.ExitShrink; Bounce.Value = a.Bounce; Jitter.Value = a.Jitter;
        }

        /// <summary>Vanilla means vanilla: no tags, no tints, no magnitude.</summary>
        private static void SetHitStyling(bool magnitude, bool tints, bool flags)
        {
            MagnitudeScaling.Value = magnitude;
            ElementalTints.Value = tints;
            SneakEnabled.Value = flags;
            CritEnabled.Value = flags;
            KillEnabled.Value = flags;
        }

        /// <summary>Merging and the edge flash: vanilla has neither, the other presets both, at
        /// the defaults. Set here so a preset describes all of what is on screen.</summary>
        private static void SetExtras(bool on)
        {
            DotMergeWindow.Value = on ? 1.2f : 0f;
            HitMergeWindow.Value = 0f;
            EdgeFlash.Value = on;
        }

        private static void ResetColors()
        {
            ColorNormal.Value = Color.white;
            ColorResistant.Value = new Color(0.6f, 0.6f, 0.6f, 1f);
            ColorWeak.Value = new Color(1f, 1f, 0f, 1f);
            ColorImmune.Value = new Color(0.6f, 0.6f, 0.6f, 1f);
            ColorHeal.Value = new Color(0.5f, 1f, 0.5f, 0.7f);
            ColorTooHard.Value = new Color(0.8f, 0.7f, 0.7f, 1f);
            ColorBlocked.Value = Color.white;
            ColorBonus.Value = new Color(1f, 0.63f, 0.24f, 1f);
            ColorSelfDamage.Value = new Color(1f, 0f, 0f, 1f);
            ColorSelfNoDamage.Value = new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        /// <summary>
        /// The font list is empty until a world is loaded, so the entry is bound with a
        /// placeholder list and re-bound here with the real names so ConfigurationManager offers a
        /// dropdown. The current choice is kept, even when it is not in the list.
        /// </summary>
        public static void RebindFontChoices(IList<string> names)
        {
            if (_cfg == null || FontName == null || names == null || names.Count == 0)
                return;

            string current = FontName.Value;
            var list = new List<string>(names);
            if (!list.Contains(current))
                list.Add(current);

            _applying = true;
            try
            {
                ConfigDescription old = FontName.Description;
                _cfg.Remove(FontName.Definition);
                FontName = _cfg.Bind("2 Font", "Font", Fonts.VanillaName,
                    new ConfigDescription(FontDescription, new AcceptableValueList<string>(list.ToArray()), old.Tags));
                if (FontName.Value != current)
                    FontName.Value = current;
            }
            finally { _applying = false; }
        }

        /// <summary>
        /// Set any setting by its key, for live tuning from the console. The value is parsed by
        /// BepInEx's own converter, so a colour takes the RRGGBBAA it writes in the file and a
        /// number is clamped to the range the entry declares.
        /// </summary>
        public static string SetByName(string key, string value)
        {
            ConfigEntryBase entry = Find(key);
            if (entry == null)
                return "no setting called \"" + key + "\"";
            try
            {
                entry.BoxedValue = TomlTypeConverter.ConvertToValue(value, entry.SettingType);
                return entry.Definition.Key + " = " + entry.GetSerializedValue();
            }
            catch (Exception ex)
            {
                return "could not set " + entry.Definition.Key + ": " + ex.Message;
            }
        }

        /// <summary>Current values as "Section.Key = value" lines, optionally filtered.</summary>
        public static List<string> Describe(string filter)
        {
            var lines = new List<string>();
            if (_cfg == null)
                return lines;
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in (IDictionary<ConfigDefinition, ConfigEntryBase>)_cfg)
            {
                if (!string.IsNullOrEmpty(filter)
                    && kv.Key.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && kv.Key.Section.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                lines.Add(kv.Key.Key + " = " + kv.Value.GetSerializedValue());
            }
            lines.Sort(StringComparer.OrdinalIgnoreCase);
            return lines;
        }

        private static ConfigEntryBase Find(string key)
        {
            if (_cfg == null || string.IsNullOrEmpty(key))
                return null;
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> kv in (IDictionary<ConfigDefinition, ConfigEntryBase>)_cfg)
            {
                if (string.Equals(kv.Key.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }
            return null;
        }

        public static void Reload()
        {
            if (_cfg == null)
                return;
            string preset = Preset.Value, anim = AnimPreset.Value;
            _applying = true;
            try { _cfg.Reload(); }
            finally { _applying = false; }

            // A preset edited in the file is a request to apply it, as picking it in the
            // dropdown would be. Other edits made alongside it are kept only when the preset
            // itself did not change.
            if (Preset.Value != preset && Preset.Value != PresetCustom)
                ApplyPreset(Preset.Value);
            else if (AnimPreset.Value != anim && AnimPreset.Value != PresetCustom)
                ApplyAnimPreset(AnimPreset.Value);
            else
                Raise();
        }

        public static AnimParams CurrentAnim()
        {
            return new AnimParams
            {
                Duration = Duration.Value,
                PopFrom = PopFrom.Value,
                PopDuration = PopDuration.Value,
                Overshoot = Overshoot.Value,
                FadeIn = FadeIn.Value,
                Tilt = Tilt.Value,
                RiseSpeed = RiseSpeed.Value,
                Gravity = Gravity.Value,
                Spray = Spray.Value,
                ExitShrink = ExitShrink.Value,
                Bounce = Bounce.Value,
                Jitter = Jitter.Value,
            };
        }

        private static void Raise()
        {
            Action a = Changed;
            if (a != null)
                a();
        }
    }

    /// <summary>The subset of ConfigurationManager's attribute class we use. It is read by
    /// field name through reflection, so the type need not be shared.</summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public int? Order;
        public bool? IsAdvanced;
    }
}
