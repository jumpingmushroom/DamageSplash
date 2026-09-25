using System;
using DamageSplash.Core;
using HarmonyLib;
using UnityEngine;

namespace DamageSplash.Patches
{
    /// <summary>
    /// The render layer. AddInworldText is replaced so every number goes through the pool, and
    /// UpdateWorldTexts gets our update in front of it. Vanilla's own list is left alone, so
    /// disabling the mod mid-session hands back cleanly: vanilla numbers made after that are
    /// drawn by vanilla, ours finish their flight and go back to the pool.
    /// </summary>
    [HarmonyPatch(typeof(DamageText))]
    internal static class DamageTextPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(DamageText.Awake))]
        private static void AwakePostfix(DamageText __instance)
        {
            Fonts.Refresh();
            PluginConfig.RebindFontChoices(Fonts.Names);
            Styles.Invalidate();
            Compat.Detect();
            SplashPool.Init(__instance);
            __instance.m_maxTextDistance = PluginConfig.MaxDistance.Value;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(DamageText.AddInworldText))]
        private static bool AddInworldTextPrefix(DamageText __instance, DamageText.TextType type, Vector3 pos, float distance, string text, bool mySelf)
        {
            // Another damage-number mod is installed and we agreed to let it draw.
            if (!PluginConfig.Enabled.Value || !SplashPool.Ready || Compat.ShouldYield)
                return true;

            // On the client that owns the target this runs inside ApplyDamage, before the health
            // is taken, with nothing catching in between: an exception escaping here would cancel
            // the hit. If drawing fails, vanilla draws instead.
            try
            {
                Show(type, pos, distance, text, mySelf);
                return false;
            }
            catch (Exception ex)
            {
                Guard.Report("AddInworldText", ex);
                return true;
            }
        }

        private static void Show(DamageText.TextType type, Vector3 pos, float distance, string text, bool mySelf)
        {
            // "Too hard" arrives with a payload of "0" too, but it is a message, not a hit for
            // nothing, so HideZeros leaves it alone. Vanilla's busy rule still applies to it.
            if (text == "0" && ((PluginConfig.HideZeros.Value && type != DamageText.TextType.TooHard)
                || SplashPool.ActiveCount >= PluginConfig.MaxAlive.Value))
                return;

            if (!Allowed(type, mySelf, pos))
                return;

            Style style = Styles.Resolve(type, text, mySelf, pos);

            // Feed a number already floating over this victim rather than adding another. The
            // kill hook restyles whichever number this hit ended up in, merged or new, if the
            // hit turns out to have been fatal.
            int mergeKey = Merger.KeyFor(style, type, mySelf);
            Splash splash = Merger.TryMerge(style, type, mergeKey, pos)
                ?? SplashPool.Spawn(style, style.Text, pos, distance, type, mergeKey);
            HitContext.SetSpawned(pos, splash);
        }

        /// <summary>
        /// Whose numbers to show. Only hits resolved on this machine can be told apart at all,
        /// so "Mine" keeps yours and anything aimed at you, and drops what arrives over the wire
        /// from someone else's fight. Blocks, heals, bonuses and "too hard" are not damage
        /// numbers and always show.
        /// </summary>
        private static bool Allowed(DamageText.TextType type, bool mySelf, Vector3 pos)
        {
            string mode = PluginConfig.Visibility.Value;
            if (mode == PluginConfig.VisibilityAll)
                return true;
            if (type == DamageText.TextType.Blocked || type == DamageText.TextType.Heal
                || type == DamageText.TextType.Bonus || type == DamageText.TextType.TooHard)
                return true;
            if (mode == PluginConfig.VisibilityNone)
                return false;
            if (mySelf)
                return true;

            HitInfo hit;
            return HitContext.TryMatch(pos, out hit) && (hit.AttackerIsLocal || hit.TargetIsLocalPlayer);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(DamageText.UpdateWorldTexts))]
        private static void UpdateWorldTextsPrefix(float dt)
        {
            SplashPool.Update(dt);
        }
    }
}
