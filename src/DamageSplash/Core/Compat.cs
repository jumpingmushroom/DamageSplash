using System;
using BepInEx.Bootstrap;

namespace DamageSplash.Core
{
    /// <summary>
    /// ColorfulDamage and ZenCombat replace the same method this mod replaces, and Harmony stops
    /// at the first prefix that returns false. Whichever happens to run first wins and the other
    /// does nothing at all, with no error and no clue as to why. Say so plainly, and by default
    /// stand aside rather than race.
    /// </summary>
    public static class Compat
    {
        private static readonly string[] Known = { "colorfuldamage", "zencombat" };

        /// <summary>The conflicting mod, or null when there is none.</summary>
        public static string Conflict { get; private set; }

        public static bool ShouldYield => Conflict != null && PluginConfig.YieldToOtherMods.Value;

        private static bool _logged;
        private static string _loggedConflict;
        private static bool _loggedYield;

        /// <summary>
        /// Runs on every setting change, which is every frame while a slider is dragged, so
        /// the verdict is logged only when it differs from the last one logged.
        /// </summary>
        public static void Detect()
        {
            Conflict = null;

            if (PluginConfig.DebugSimulateConflict.Value)
            {
                Conflict = "a pretend mod (DebugSimulateConflict is on)";
            }
            else
            {
                foreach (var entry in Chainloader.PluginInfos)
                {
                    string guid = entry.Key ?? string.Empty;
                    string name = entry.Value != null && entry.Value.Metadata != null ? entry.Value.Metadata.Name : string.Empty;
                    if (IsKnown(guid) || IsKnown(name))
                    {
                        Conflict = (string.IsNullOrEmpty(name) ? guid : name) + " (" + guid + ")";
                        break;
                    }
                }
            }

            if (_logged && _loggedConflict == Conflict && _loggedYield == ShouldYield)
                return;
            _logged = true;
            _loggedConflict = Conflict;
            _loggedYield = ShouldYield;

            if (Conflict == null)
                DamageSplashPlugin.Log.LogInfo("no other damage-number mod found.");
            else if (ShouldYield)
                DamageSplashPlugin.Log.LogWarning(Conflict + " also redraws the damage numbers. "
                    + "DamageSplash is standing aside and letting it draw; set YieldToOtherMods to false to take over instead, "
                    + "but do not run both.");
            else
                DamageSplashPlugin.Log.LogWarning(Conflict + " also redraws the damage numbers and YieldToOtherMods is off. "
                    + "Both will fight over the same method and whichever loads first wins; remove one of them.");
        }

        private static bool IsKnown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            foreach (string k in Known)
            {
                if (text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
