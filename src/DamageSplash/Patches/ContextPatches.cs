using System;
using DamageSplash.Core;
using HarmonyLib;
using UnityEngine;

namespace DamageSplash.Patches
{
    /// <summary>
    /// The context layer. `Character.ApplyDamage` is the one choke point every real hit passes
    /// through on the client that owns the target, direct or damage-over-time, and it shows the
    /// floating number itself before it subtracts the health. So the prefix can leave the hit
    /// where the render layer will find it, and the postfix can see whether that hit killed and
    /// restyle the number it just made, still on frame zero of its animation.
    ///
    /// Nothing here changes the game: both patches are read-only and the mod works without them,
    /// just without tags, tints and magnitude.
    /// </summary>
    [HarmonyPatch(typeof(Character))]
    internal static class ContextPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Character.ApplyDamage))]
        private static void ApplyDamagePrefix(Character __instance, HitData hit, bool showDamageText, out HitInfo __state)
        {
            // Saved and restored rather than cleared, so a hit that triggers another hit while it
            // resolves cannot steal the outer hit's number.
            __state = HitContext.Take();

            // This runs inside the game's damage path, ahead of the health change: anything
            // escaping from here would cancel the hit itself. Styling is not worth that.
            try
            {
                SetContext(__instance, hit, showDamageText);
            }
            catch (Exception ex)
            {
                HitContext.Clear();
                Guard.Report("ApplyDamage prefix", ex);
            }
        }

        private static void SetContext(Character target, HitData hit, bool showDamageText)
        {
            if (hit == null || !showDamageText || !PluginConfig.Enabled.Value || !PluginConfig.UseHitContext.Value)
                return;
            // ApplyDamage returns without a number in these cases; leave no context behind.
            if (target.IsDead() || target.IsDebugFlying() || target.IsTeleporting())
                return;

            bool dot = IsOverTime(hit.m_hitType);
            bool combat = hit.m_hitType == HitData.HitType.EnemyHit || hit.m_hitType == HitData.HitType.PlayerHit;

            bool wantsAttacker = PluginConfig.Visibility.Value != PluginConfig.VisibilityAll;
            var info = new HitInfo
            {
                Pos = hit.m_point,
                TargetId = target.GetInstanceID(),
                TargetIsLocalPlayer = ReferenceEquals(target, Player.m_localPlayer),
                // Resolving the attacker costs a scene lookup, so only when something asks.
                AttackerIsLocal = wantsAttacker && ReferenceEquals(hit.GetAttacker(), Player.m_localPlayer),
                // The number Valheim shows is the damage before the last difficulty and rate
                // multipliers, which this prefix runs ahead of. Read it here so the magnitude
                // tier sizes the number the player actually reads.
                Damage = hit.GetTotalDamage(),
                MaxHealth = target.GetMaxHealth(),
                // IsDead only turns true in the next fixed update (CheckDeath), so a second hit
                // landing before then would pass the check above with the health already gone.
                // Only a hit on something still standing can be the killing blow.
                WasAlive = target.GetHealth() > 0f,
                Dot = dot,
                // RPC_Damage stamps m_backstabTime in this same frame when a sneak attack lands.
                Sneak = combat && target.m_backstabTime == Time.time,
                // RPC_Damage doubles the damage of a hit on a staggered creature. Restricted to
                // real combat hits so a burning tick on a staggering troll is not called a crit.
                StaggerCrit = combat && !target.IsPlayer() && target.IsStaggering(),
            };

            info.Majority = hit.m_damage.GetMajorityDamageType();
            info.Elemental = (info.Majority & HitData.DamageType.Elemental) != 0
                || info.Majority == HitData.DamageType.Poison
                || info.Majority == HitData.DamageType.Spirit;

            HitContext.Set(info);

            if (info.TargetIsLocalPlayer && PluginConfig.EdgeFlash.Value && info.MaxHealth > 0f
                && info.Damage / info.MaxHealth >= PluginConfig.EdgeFlashPercent.Value)
            {
                EdgeFlash.Trigger();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Character.ApplyDamage))]
        private static void ApplyDamagePostfix(Character __instance)
        {
            try
            {
                Splash spawned = HitContext.Spawned;
                if (spawned != null && PluginConfig.KillEnabled.Value && HitContext.WasAlive && __instance.GetHealth() <= 0f)
                    Styles.ApplyKill(spawned);
            }
            catch (Exception ex)
            {
                Guard.Report("ApplyDamage postfix", ex);
            }
        }

        /// <summary>
        /// A finalizer, not the postfix, puts the outer context back: postfixes are skipped when
        /// ApplyDamage throws, and a context left behind would tag whatever came next.
        /// </summary>
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(Character.ApplyDamage))]
        private static void ApplyDamageFinalizer(HitInfo __state)
        {
            HitContext.Restore(__state);
        }

        /// <summary>Burning, poison and the rest arrive as their own calls with no attacker.</summary>
        private static bool IsOverTime(HitData.HitType type)
        {
            switch (type)
            {
                case HitData.HitType.Burning:
                case HitData.HitType.Freezing:
                case HitData.HitType.Poisoned:
                case HitData.HitType.Smoke:
                case HitData.HitType.Drowning:
                case HitData.HitType.Water:
                    return true;
                default:
                    return false;
            }
        }
    }
}
