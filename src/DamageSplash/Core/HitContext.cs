using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>
    /// What the damage pipeline knows about one hit and the floating number cannot: who was hit,
    /// how hard relative to what they can take, and whether it was a sneak attack, a hit on a
    /// staggered enemy, or the blow that killed them.
    /// </summary>
    public struct HitInfo
    {
        public bool Valid;
        public Vector3 Pos;          // where the number will appear, for pairing
        public float Damage;         // the number that will be shown
        public float MaxHealth;      // 0 when unknown
        public int TargetId;         // identity for merging ticks on the same victim
        public int Frame;            // stamped by Set; a context from another frame never pairs
        public bool WasAlive;        // health above 0 before this hit, so only the killing blow is a kill
        public bool AttackerIsLocal;
        public bool TargetIsLocalPlayer;
        public bool Sneak;
        public bool StaggerCrit;
        public bool Dot;
        public bool Elemental;
        public HitData.DamageType Majority;
        public Splash Spawned;       // filled in by the render layer, read by the kill postfix
    }

    /// <summary>
    /// The bridge between the two layers. A routed RPC to everyone is handled locally and
    /// synchronously before it is sent, so on the client that owns the target the whole chain
    /// (RPC_Damage, ApplyDamage, ShowText, RPC_DamageText, AddInworldText) runs on one stack:
    /// the damage hook can leave the hit here and the text hook picks it up in the same frame.
    ///
    /// Pairing is by position rather than by "whatever is current", so a number from another
    /// caller that happens to land mid-stack cannot be mis-tagged. Nesting (a hit that kills and
    /// triggers another hit) is handled by the patches saving and restoring around themselves.
    /// </summary>
    public static class HitContext
    {
        private const float MatchDistanceSq = 1f;

        private static HitInfo _current;

        public static bool Has => _current.Valid;

        public static Splash Spawned => _current.Spawned;

        public static bool WasAlive => _current.WasAlive;

        public static void Set(HitInfo info)
        {
            info.Valid = true;
            info.Frame = Time.frameCount;
            _current = info;
        }

        public static void Clear()
        {
            _current = default(HitInfo);
        }

        /// <summary>Read the current context and clear it, for a caller that will restore it.</summary>
        public static HitInfo Take()
        {
            HitInfo info = _current;
            _current = default(HitInfo);
            return info;
        }

        public static void Restore(HitInfo info)
        {
            _current = info;
        }

        /// <summary>
        /// The context for a number about to be shown at <paramref name="pos"/>, if there is one
        /// and it belongs to that number. Does not clear it: the kill hook still needs it.
        /// </summary>
        public static bool TryMatch(Vector3 pos, out HitInfo info)
        {
            info = _current;
            if (!IsLive())
            {
                info = default(HitInfo);
                return false;
            }
            if ((pos - _current.Pos).sqrMagnitude > MatchDistanceSq)
            {
                if (PluginConfig.Verbose.Value)
                    DamageSplashPlugin.Log.LogDebug("context ignored: text at " + pos + " is not the hit at " + _current.Pos);
                info = default(HitInfo);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Remember the number this hit produced, so the kill hook can restyle it. Checked
        /// against the hit's own position for the same reason TryMatch is: a number from some
        /// other caller must not be adopted, or a kill would repaint the wrong one.
        /// </summary>
        public static void SetSpawned(Vector3 pos, Splash splash)
        {
            if (!IsLive() || (pos - _current.Pos).sqrMagnitude > MatchDistanceSq)
                return;
            _current.Spawned = splash;
        }

        /// <summary>
        /// The whole chain runs on one stack in one frame, so a context from an earlier frame is
        /// one whose hit never cleaned up after itself, and it must not tag anything.
        /// </summary>
        private static bool IsLive()
        {
            return _current.Valid && _current.Frame == Time.frameCount;
        }
    }
}
