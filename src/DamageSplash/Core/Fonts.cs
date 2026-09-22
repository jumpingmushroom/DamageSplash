using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>
    /// Every TextMeshPro font the game has loaded, by asset name. Nothing is shipped: the choice
    /// is the game's own fonts (Valheim-Norse, Valheim-AveriaSansLibre and their weights) plus
    /// whatever other mods brought along. "Vanilla" means the damage-text prefab's own font.
    /// </summary>
    public static class Fonts
    {
        public const string VanillaName = "Vanilla";

        private static readonly Dictionary<string, TMP_FontAsset> _byName = new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
        private static List<string> _names = new List<string> { VanillaName };
        private static readonly HashSet<string> _warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static IList<string> Names => _names;
        public static bool Scanned { get; private set; }

        /// <summary>Rescan. Fonts are loaded by the time the HUD (and so DamageText) exists.</summary>
        public static void Refresh()
        {
            _byName.Clear();
            try
            {
                foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (f == null || string.IsNullOrEmpty(f.name) || f.atlasTexture == null)
                        continue;
                    if (!_byName.ContainsKey(f.name))
                        _byName[f.name] = f;
                }
            }
            catch (Exception ex)
            {
                DamageSplashPlugin.Log.LogWarning("font scan failed: " + ex.Message);
            }

            var names = new List<string>(_byName.Keys);
            names.Sort((a, b) =>
            {
                int ra = Rank(a), rb = Rank(b);
                return ra != rb ? ra.CompareTo(rb) : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            names.Insert(0, VanillaName);
            _names = names;
            Scanned = true;
        }

        private static int Rank(string name)
        {
            return name.StartsWith("Valheim", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }

        /// <summary>
        /// The asset for a configured name, or null for "Vanilla" and for names that resolve to
        /// nothing (the caller then keeps the prefab's font). An exact match wins; otherwise a
        /// name whose words all occur in a loaded font's name, so presets can say "Norse Bold"
        /// without knowing the exact asset name.
        /// </summary>
        public static TMP_FontAsset Resolve(string name)
        {
            if (string.IsNullOrEmpty(name) || string.Equals(name, VanillaName, StringComparison.OrdinalIgnoreCase))
                return null;

            TMP_FontAsset f;
            if (_byName.TryGetValue(name, out f) && f != null)
                return f;

            string fuzzy = Fuzzy(name);
            if (fuzzy != null && _byName.TryGetValue(fuzzy, out f) && f != null)
                return f;

            if (Scanned && _warned.Add(name))
                DamageSplashPlugin.Log.LogWarning("font \"" + name + "\" is not loaded; using the vanilla damage-text font");
            return null;
        }

        private static string Fuzzy(string name)
        {
            string[] words = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return null;
            foreach (string candidate in _names)
            {
                if (candidate == VanillaName)
                    continue;
                bool all = true;
                foreach (string w in words)
                {
                    if (candidate.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        all = false;
                        break;
                    }
                }
                if (all)
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// The face the presets use. AveriaSansLibre, the game's own damage-text font, fattened
        /// by Boldness rather than swapped for a heavier face: both Norse faces draw the digit
        /// zero as a rune-like diamond, which is wrong on a number that often reads "0"
        /// (blocked and immune hits). Norse stays available for anyone who wants the flavour.
        /// </summary>
        public static string PreferredDisplay()
        {
            string averia = Fuzzy("AveriaSansLibre");
            if (averia != null) return averia;
            string norse = Fuzzy("Norse");
            if (norse != null) return norse;
            return VanillaName;
        }
    }
}
