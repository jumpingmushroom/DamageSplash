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

            if (text == "0" && (PluginConfig.HideZeros.Value || SplashPool.ActiveCount >= PluginConfig.MaxAlive.Value))
                return false;

            if (!Allowed(type, mySelf, pos))
                return false;

            Style style = Styles.Resolve(type, text, mySelf, pos);

            // Feed a number already floating over this victim rather than adding another.
            int mergeKey = Merger.KeyFor(style, type, mySelf);
            if (Merger.TryMerge(style, type, mergeKey, pos))
                return false;

            Splash splash = SplashPool.Spawn(style, style.Text, pos, distance, type, mergeKey);
            // The kill hook restyles this one if the hit turns out to have been fatal.
            HitContext.SetSpawned(pos, splash);
            return false;
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
