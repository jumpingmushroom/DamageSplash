using System;
using System.Collections.Generic;
using System.Reflection;
using DamageSplash.Core;
using HarmonyLib;

namespace DamageSplash.Patches
{
    /// <summary>
    /// Every IDestructible's Damage(HitData) is where an attack hands a hit over, on the
    /// attacker's own client, before it travels to whoever owns the target. Read-only.
    /// </summary>
    [HarmonyPatch]
    internal static class OutgoingHitPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // A throw here would abort PatchAll and leave the whole mod unpatched, so a type that
            // cannot be loaded (another mod's missing dependency) is skipped, not fatal.
            Type[] types;
            try { types = typeof(Character).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }

            foreach (Type t in types)
            {
                if (t == null || t.IsAbstract || t.IsInterface || !typeof(IDestructible).IsAssignableFrom(t))
                    continue;
                MethodInfo m = t.GetMethod("Damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(HitData) }, null);
                if (m != null && !m.IsAbstract)
                    yield return m;
            }
        }

        private static void Prefix(HitData hit)
        {
            try
            {
                if (hit == null || PluginConfig.Visibility.Value != PluginConfig.VisibilityMine)
                    return;
                Player me = Player.m_localPlayer;
                if (me != null && hit.m_attacker == me.GetZDOID())
                    OutgoingHits.Record(hit.m_point);
            }
            catch (Exception ex)
            {
                Guard.Report("outgoing hit", ex);
            }
        }
    }
}
