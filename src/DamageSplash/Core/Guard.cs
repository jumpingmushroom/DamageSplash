using System;
using System.Collections.Generic;

namespace DamageSplash.Core
{
    /// <summary>
    /// The patches run inside the game's damage path: ApplyDamage shows its number before it
    /// takes the health, on the same stack, with nothing catching in between. So every hook
    /// catches what it throws and hands it here, and the hit goes ahead without its styling.
    /// Logged a few times per hook rather than on every hit, so a broken hook does not flood
    /// the log in a long fight.
    /// </summary>
    public static class Guard
    {
        private const int MaxReportsPerSite = 3;

        private static readonly Dictionary<string, int> _reports = new Dictionary<string, int>();

        public static void Report(string site, Exception ex)
        {
            int n;
            _reports.TryGetValue(site, out n);
            _reports[site] = ++n;
            if (n > MaxReportsPerSite)
                return;
            DamageSplashPlugin.Log.LogError(site + " failed; the game carries on without DamageSplash styling for it"
                + (n == MaxReportsPerSite ? " (further errors here are not logged)" : "") + ": " + ex);
        }
    }
}
