using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>Everything a number needs to look the way it should. Resolved once per spawn.</summary>
    public sealed class Style
    {
        public TMP_FontAsset Font;      // null: the prefab's own font
        public Material Material;       // null: the font's own (shared) material
        public bool FauxBold;
        public Color Color;
        public float SizeScale = 1f;
        public float Duration;
        public AnimParams Anim;
        public string Text;             // what is drawn, tag and all
        public string Number;           // the number alone, kept for a later restyle
        public float Value;             // the number as a number, for merging and tiers
        public float MaxHealth;         // of the target, 0 when unknown
        public int TargetId;            // 0 when unknown
        public bool Dot;
        public bool Tagged;             // sneak, crit: never merged into
    }

    /// <summary>How much the hit mattered, which is what decides the size.</summary>
    public enum Tier
    {
        Small,
        Normal,
        Big,
        Huge,
    }

    /// <summary>
    /// Turns (TextType, text, mine) plus the config into a Style. Materials with an outline or
    /// shadow are cloned from the font's material once per font and shared by every number
    /// using that font; nothing is allocated per hit after the first.
    /// </summary>
    public static class Styles
    {
        private static readonly Dictionary<int, Material> _materials = new Dictionary<int, Material>();
        private static string _tooHard;
        private static string _blocked;

        /// <summary>Config changed or a new HUD: forget cached materials and strings.</summary>
        public static void Invalidate()
        {
            foreach (Material m in _materials.Values)
                if (m != null) Object.Destroy(m);
            _materials.Clear();
            _tooHard = null;
            _blocked = null;
        }

        public static Style Resolve(DamageText.TextType type, string text, bool mySelf, Vector3 pos)
        {
            HitInfo hit;
            bool hasHit = HitContext.TryMatch(pos, out hit);

            var s = new Style();
            s.Font = Fonts.Resolve(PluginConfig.FontName.Value);
            s.FauxBold = PluginConfig.FauxBold.Value;
            s.Material = MaterialFor(s.Font ?? SplashPool.PrefabFont);
            s.Color = ColorFor(type, text, mySelf);
            s.Anim = PluginConfig.CurrentAnim();

            // An elemental tint only replaces the plain white of an ordinary hit. Weak,
            // resistant and immune keep their own colour: what those say about the target is
            // worth more than what a tint says about the weapon.
            if (hasHit && hit.Elemental && type == DamageText.TextType.Normal && !mySelf
                && PluginConfig.ElementalTints.Value)
            {
                Color tint;
                if (ElementalColor(hit.Majority, out tint))
                    s.Color = tint;
            }

            s.Value = Amount(hasHit, hit, text);
            s.MaxHealth = hasHit ? hit.MaxHealth : 0f;
            s.TargetId = hasHit ? hit.TargetId : 0;
            s.Dot = hasHit && hit.Dot;

            Tier tier = TierForValue(s.Value, s.MaxHealth);
            string tag = null;
            if (hasHit && PluginConfig.SneakEnabled.Value && hit.Sneak)
            {
                tag = PluginConfig.SneakTag.Value;
                s.Color = PluginConfig.SneakColor.Value;
                tier = Bump(tier);
            }
            else if (hasHit && PluginConfig.CritEnabled.Value && hit.StaggerCrit)
            {
                tag = PluginConfig.CritTag.Value;
                s.Color = PluginConfig.CritColor.Value;
                tier = Bump(tier);
            }

            s.Tagged = !string.IsNullOrEmpty(tag);
            s.Number = Format(type, text);
            s.Text = Decorate(s.Number, tag);
            s.SizeScale = ScaleFor(tier);
            if (Demo.FreezeMotion)
            {
                s.Anim.RiseSpeed = 0f;
                s.Anim.Gravity = 0f;
                s.Anim.Spray = 0f;
                s.Anim.ExitShrink = 1f;
                s.Anim.Jitter = 0f;
            }
            s.Duration = Demo.DurationOverride > 0f ? Demo.DurationOverride : s.Anim.Duration;
            if (type == DamageText.TextType.Bonus)
            {
                // Vanilla: crafting bonuses are half again as big and linger for 3 s.
                s.SizeScale = 1.5f;
                s.Duration = Mathf.Max(3f, s.Duration);
            }
            return s;
        }

        /// <summary>
        /// Turn a number that has already been drawn into a killing blow. The health only drops
        /// after the number is made, so this runs a moment later, while the number is still on
        /// the first frame of its entrance and nobody has seen it yet.
        /// </summary>
        public static void ApplyKill(Splash splash)
        {
            if (splash == null || splash.Text == null)
                return;
            splash.Text.text = Decorate(splash.Number, PluginConfig.KillTag.Value);
            splash.Text.fontSize = splash.BaseFontSize * PluginConfig.ScaleHuge.Value;
            splash.Color = PluginConfig.KillColor.Value;
            splash.Text.color = splash.Color;
            splash.Duration *= PluginConfig.KillDurationScale.Value;
        }

        private static string Decorate(string number, string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return number;
            int percent = Mathf.RoundToInt(PluginConfig.TagScale.Value * 100f);
            return "<size=" + percent + "%>" + tag + "</size> " + number;
        }

        private static Tier Bump(Tier tier)
        {
            return tier < Tier.Huge ? tier + 1 : tier;
        }

        private static float ScaleFor(Tier tier)
        {
            if (!PluginConfig.MagnitudeScaling.Value)
                return 1f;
            switch (tier)
            {
                case Tier.Small: return PluginConfig.ScaleSmall.Value;
                case Tier.Big: return PluginConfig.ScaleBig.Value;
                case Tier.Huge: return PluginConfig.ScaleHuge.Value;
                default: return 1f;
            }
        }

        /// <summary>The number as a number: from the hit where there is one, parsed otherwise.</summary>
        private static float Amount(bool hasHit, HitInfo hit, string text)
        {
            if (hasHit)
                return hit.Damage;
            float amount;
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount) ? amount : 0f;
        }

        /// <summary>
        /// Share of the target's maximum health where that is known, the raw number otherwise.
        /// The share is the point of it: 30 damage is most of a greyling and nothing at all to a
        /// fuling berserker, and no single absolute threshold can say both.
        /// </summary>
        public static Tier TierForValue(float amount, float maxHealth)
        {
            if (!PluginConfig.MagnitudeScaling.Value)
                return Tier.Normal;

            if (maxHealth > 0f)
            {
                float share = amount / maxHealth;
                if (share >= PluginConfig.HugeAbovePercent.Value) return Tier.Huge;
                if (share >= PluginConfig.BigAbovePercent.Value) return Tier.Big;
                if (share < PluginConfig.SmallBelowPercent.Value) return Tier.Small;
                return Tier.Normal;
            }

            if (amount >= PluginConfig.HugeAboveDamage.Value) return Tier.Huge;
            if (amount >= PluginConfig.BigAboveDamage.Value) return Tier.Big;
            if (amount < PluginConfig.SmallBelowDamage.Value) return Tier.Small;
            return Tier.Normal;
        }

        public static float ScaleForValue(float amount, float maxHealth)
        {
            return ScaleFor(TierForValue(amount, maxHealth));
        }

        /// <summary>The number a merged splash should now read, formatted as vanilla formats.</summary>
        public static string NumberText(DamageText.TextType type, float value)
        {
            return Format(type, value.ToString("0.#", CultureInfo.InvariantCulture));
        }

        private static bool ElementalColor(HitData.DamageType type, out Color color)
        {
            switch (type)
            {
                case HitData.DamageType.Fire: color = PluginConfig.ColorFire.Value; return true;
                case HitData.DamageType.Frost: color = PluginConfig.ColorFrost.Value; return true;
                case HitData.DamageType.Lightning: color = PluginConfig.ColorLightning.Value; return true;
                case HitData.DamageType.Poison: color = PluginConfig.ColorPoison.Value; return true;
                case HitData.DamageType.Spirit: color = PluginConfig.ColorSpirit.Value; return true;
                default: color = Color.white; return false;
            }
        }

        /// <summary>Vanilla's colour rules with the configured colours substituted.</summary>
        public static Color ColorFor(DamageText.TextType type, string text, bool mySelf)
        {
            if (mySelf && type <= DamageText.TextType.Immune)
                return text == "0" ? PluginConfig.ColorSelfNoDamage.Value : PluginConfig.ColorSelfDamage.Value;
            switch (type)
            {
                case DamageText.TextType.Normal: return PluginConfig.ColorNormal.Value;
                case DamageText.TextType.Resistant: return PluginConfig.ColorResistant.Value;
                case DamageText.TextType.Weak: return PluginConfig.ColorWeak.Value;
                case DamageText.TextType.Immune: return PluginConfig.ColorImmune.Value;
                case DamageText.TextType.TooHard: return PluginConfig.ColorTooHard.Value;
                case DamageText.TextType.Blocked: return PluginConfig.ColorBlocked.Value;
                case DamageText.TextType.Bonus: return PluginConfig.ColorBonus.Value;
                case DamageText.TextType.Heal: return PluginConfig.ColorHeal.Value;
                default: return Color.white;
            }
        }

        /// <summary>Vanilla's text rewrites, with the localized strings cached.</summary>
        public static string Format(DamageText.TextType type, string text)
        {
            Localization loc = Localization.instance;
            switch (type)
            {
                case DamageText.TextType.TooHard:
                    return _tooHard ?? (_tooHard = loc.Localize("$msg_toohard"));
                case DamageText.TextType.Heal:
                    return "+" + loc.Localize(text);
                case DamageText.TextType.Blocked:
                    return (_blocked ?? (_blocked = loc.Localize("$msg_blocked: "))) + loc.Localize(text);
                default:
                    return loc.Localize(text);
            }
        }

        /// <summary>Outline and shadow live on the material, so one clone per font.</summary>
        private static Material MaterialFor(TMP_FontAsset font)
        {
            float outline = PluginConfig.OutlineWidth.Value;
            float dilate = PluginConfig.FaceDilate.Value;
            bool shadow = PluginConfig.Shadow.Value;
            if (font == null || (outline <= 0f && !shadow && dilate == 0f))
                return null;

            Material m;
            int key = font.GetInstanceID();
            if (_materials.TryGetValue(key, out m) && m != null)
                return m;

            m = new Material(font.material);
            m.name = font.material.name + " (DamageSplash)";
            m.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            if (outline > 0f)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
                m.SetColor(ShaderUtilities.ID_OutlineColor, PluginConfig.OutlineColor.Value);
            }
            else
            {
                m.DisableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            }
            if (shadow)
            {
                float off = PluginConfig.ShadowOffset.Value;
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, off);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -off);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.15f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
            }
            else
            {
                m.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            }
            // The SDF shader scales outline and underlay by ratios derived from the font's
            // padding and the material's own settings; a clone keeps the source's, which would
            // clamp or overflow ours.
            ShaderUtilities.UpdateShaderRatios(m);
            _materials[key] = m;

            if (PluginConfig.Verbose.Value)
                DamageSplashPlugin.Log.LogDebug("material for " + font.name + ": shader " + m.shader.name + ", outline " + outline + ", dilate " + dilate + ", shadow " + shadow
                    + ", ratios " + m.GetFloat(ShaderUtilities.ID_ScaleRatio_A).ToString("0.00") + "/" + m.GetFloat(ShaderUtilities.ID_ScaleRatio_C).ToString("0.00"));
            return m;
        }
    }
}
