using System.Globalization;
using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>
    /// Burning, poison and the rest tick once a second for as long as they last, and a volley of
    /// arrows lands several hits at once. Left alone each becomes its own number and the victim
    /// disappears behind a column of small ones. Merging feeds the new amount into the number
    /// already floating over that victim, so it climbs instead of stacking.
    /// </summary>
    public static class Merger
    {
        /// <summary>
        /// What makes two numbers the same running total: the same victim, the same kind of
        /// text, the same side. Zero means this number stands alone. Tagged numbers never merge,
        /// because a sneak attack is an event worth reading on its own.
        /// </summary>
        public static int KeyFor(Style style, DamageText.TextType type, bool mySelf)
        {
            if (style.TargetId == 0 || style.Tagged || WindowFor(style) <= 0f)
                return 0;
            unchecked
            {
                int k = style.TargetId;
                k = k * 31 + (int)type;
                k = k * 31 + (mySelf ? 1 : 0);
                k = k * 31 + (style.Dot ? 1 : 0);
                return k == 0 ? 1 : k;   // 0 is reserved for "never merges"
            }
        }

        public static float WindowFor(Style style)
        {
            return style.Dot ? PluginConfig.DotMergeWindow.Value : PluginConfig.HitMergeWindow.Value;
        }

        /// <summary>Feed this amount into a number already on screen, if there is a matching one.</summary>
        public static bool TryMerge(Style style, DamageText.TextType type, int key, Vector3 worldPos)
        {
            if (key == 0)
                return false;

            Splash s = SplashPool.FindMergeable(key, Time.time, WindowFor(style), PluginConfig.MergeMaxLife.Value);
            if (s == null)
                return false;

            s.Value += style.Value;
            s.Number = Styles.NumberText(type, s.Value);
            s.Text.text = s.Number;
            s.Text.fontSize = s.BaseFontSize * Styles.ScaleForValue(s.Value, s.MaxHealth);

            // Follow the victim. Every burning tick reports the creature's centre as it is now,
            // so a number left at the first tick's position falls behind anything that walks,
            // and is soon behind the camera entirely.
            s.Origin = worldPos;
            s.Offset = Vector3.zero;
            s.Velocity = Animation.InitialVelocity(ref s.Anim);

            // Start the life over: the number brightens, pops again and stays a while longer,
            // which is what makes a run of ticks read as one thing that is still happening.
            s.Timer = 0f;
            s.Color.a = 1f;
            s.Tr.localScale = Vector3.one * (s.Anim.PopDuration > 0f ? s.Anim.PopFrom : 1f);
            return true;
        }
    }
}
