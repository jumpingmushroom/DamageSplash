using System.Collections.Generic;
using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>
    /// Fake numbers around the player, spawned locally through the same path a real hit takes
    /// (DamageText.AddInworldText, which our prefix intercepts) and never over the network. Each
    /// one can carry a made-up hit context, so sneak, crit, kill, the elemental tints and the
    /// magnitude tiers can all be judged without finding a fight.
    /// </summary>
    public static class Demo
    {
        private struct Item
        {
            public float At;
            public DamageText.TextType Type;
            public string Text;
            public bool MySelf;
            public Vector3 Offset;    // right, up, forward relative to the player
            public bool HasHit;
            public HitInfo Hit;
            public bool Kill;
        }

        private static readonly List<Item> _queue = new List<Item>();
        private static float _hold;
        private static float _rainUntil;
        private static float _nextRain;
        private static float _dotUntil;
        private static float _nextDot;
        private const int DotTargetId = -4242;   // a victim that does not exist

        /// <summary>Non-zero only while a demo number is being spawned: its duration in seconds.</summary>
        public static float DurationOverride { get; private set; }

        /// <summary>
        /// Set only while a held demo number is being spawned. A hold is for looking at the
        /// type, so the number keeps its pop and then stays put: left to fly, gravity would
        /// carry it out of the world in the first few seconds of a long hold.
        /// </summary>
        public static bool FreezeMotion { get; private set; }

        public static bool Running => _queue.Count > 0;

        /// <param name="hold">Seconds each demo number stays, so a screenshot can be taken at
        /// leisure; 0 uses the configured duration.</param>
        public static string Start(float hold = 0f)
        {
            _hold = hold;
            Player p = Player.m_localPlayer;
            if (p == null)
                return "no local player";
            if (DamageText.instance == null)
                return "no DamageText (not in a world?)";

            _queue.Clear();
            float now = Time.time, step = 0.45f;
            int i = 0;

            // A row of ordinary hits, left to right, on a 200 health target.
            Plain(now + step * i++, DamageText.TextType.Normal, "37", new Vector3(-1.6f, 1.4f, 3f), 200f);
            Plain(now + step * i++, DamageText.TextType.Weak, "52", new Vector3(-0.6f, 1.4f, 3f), 200f);
            Plain(now + step * i++, DamageText.TextType.Resistant, "12", new Vector3(0.4f, 1.4f, 3f), 200f);
            Plain(now + step * i++, DamageText.TextType.Immune, "0", new Vector3(1.4f, 1.4f, 3f), 200f);

            // Magnitude: the same 300ish number is huge on a small target.
            Plain(now + step * i++, DamageText.TextType.Normal, "312", new Vector3(-1f, 1.9f, 4.5f), 400f);

            // The three flags.
            Flag(now + step * i++, "210", new Vector3(-2.2f, 1.7f, 4f), 400f, sneak: true);
            Flag(now + step * i++, "180", new Vector3(0.2f, 1.7f, 4f), 400f, crit: true);
            Flag(now + step * i++, "95", new Vector3(2.2f, 1.7f, 4f), 120f, kill: true);

            // Elemental tints, as the damage-over-time ticks that actually carry them.
            Element(now + step * i++, "14", new Vector3(-2.6f, 1.2f, 2.5f), 200f, HitData.DamageType.Fire);
            Element(now + step * i++, "26", new Vector3(-1.3f, 1.2f, 2.5f), 200f, HitData.DamageType.Frost);
            Element(now + step * i++, "33", new Vector3(1.3f, 1.2f, 2.5f), 200f, HitData.DamageType.Lightning);
            Element(now + step * i++, "9", new Vector3(2.6f, 1.2f, 2.5f), 200f, HitData.DamageType.Poison);

            // Everything that reaches the numbers without a hit behind it.
            Add(now + step * i++, DamageText.TextType.Blocked, "24", mySelf: false, new Vector3(-0.2f, 1.1f, 1.2f));
            Add(now + step * i++, DamageText.TextType.Heal, "15", mySelf: true, new Vector3(0f, 1.7f, 0.5f));
            Add(now + step * i++, DamageText.TextType.TooHard, "0", mySelf: false, new Vector3(2.2f, 0.8f, 3f));
            Add(now + step * i++, DamageText.TextType.Bonus, "+3", mySelf: true, new Vector3(-2.2f, 0.8f, 3f));

            // Damage to you, which always has context because you own yourself.
            Self(now + step * i++, "18", new Vector3(1.1f, 1.55f, 0.9f), 100f);
            Add(now + step * i++, DamageText.TextType.Normal, "0", mySelf: true, new Vector3(-1.1f, 1.55f, 0.9f));

            // Two far away, for the distance falloff.
            Plain(now + step * i++, DamageText.TextType.Normal, "44", new Vector3(-2.8f, 2.1f, 12f), 200f);
            Plain(now + step * i++, DamageText.TextType.Normal, "61", new Vector3(5.5f, 2.6f, 25f), 200f);

            return "demo: " + _queue.Count + " numbers over " + (step * i).ToString("0.0") + " s"
                + (hold > 0f ? ", each held " + hold.ToString("0") + " s" : "");
        }

        public static string Burst(int count)
        {
            Player p = Player.m_localPlayer;
            if (p == null)
                return "no local player";
            _queue.Clear();
            _hold = 0f;
            float now = Time.time;
            for (int i = 0; i < count; i++)
            {
                Vector3 off = new Vector3(Random.Range(-3f, 3f), Random.Range(0.8f, 2.2f), Random.Range(2f, 6f));
                var type = (DamageText.TextType)Random.Range(0, 3);
                Add(now + i * 0.02f, type, Random.Range(3, 250).ToString(), mySelf: false, off);
            }
            return "burst: " + count + " numbers";
        }

        /// <summary>
        /// A steady trickle of numbers for a while, so the sliders in ConfigurationManager can be
        /// dragged against something that is actually on screen. Without it, tuning means
        /// re-running a demo after every change.
        /// </summary>
        public static string Rain(float seconds)
        {
            if (Player.m_localPlayer == null)
                return "no local player";
            if (seconds <= 0f)
            {
                _rainUntil = 0f;
                return "rain off";
            }
            _hold = 0f;
            _rainUntil = Time.time + seconds;
            return "raining numbers for " + seconds.ToString("0") + " s (splash rain 0 to stop)";
        }

        /// <summary>
        /// Pretend something is burning: one fire tick a second on one victim, in one place.
        /// With merging on they should add into a single number that climbs, rather than a
        /// column of small ones. This is the same path a real burning creature takes.
        /// </summary>
        public static string Dot(float seconds)
        {
            if (Player.m_localPlayer == null)
                return "no local player";
            if (seconds <= 0f)
            {
                _dotUntil = 0f;
                return "burning off";
            }
            _hold = 0f;
            _dotUntil = Time.time + seconds;
            _nextDot = 0f;
            return "burning for " + seconds.ToString("0") + " s, one tick a second"
                + (PluginConfig.DotMergeWindow.Value > 0f
                    ? "; they should add into one climbing number"
                    : "; merging is off (DotMergeWindow is 0) so each tick gets its own number");
        }

        public static void Stop()
        {
            _queue.Clear();
            _rainUntil = 0f;
            _dotUntil = 0f;
        }

        private static void Add(float at, DamageText.TextType type, string text, bool mySelf, Vector3 offset)
        {
            _queue.Add(new Item { At = at, Type = type, Text = text, MySelf = mySelf, Offset = offset });
        }

        private static void Plain(float at, DamageText.TextType type, string text, Vector3 offset, float maxHealth)
        {
            _queue.Add(new Item
            {
                At = at, Type = type, Text = text, Offset = offset, HasHit = true,
                Hit = new HitInfo { Damage = Parse(text), MaxHealth = maxHealth, Majority = HitData.DamageType.Slash },
            });
        }

        private static void Flag(float at, string text, Vector3 offset, float maxHealth, bool sneak = false, bool crit = false, bool kill = false)
        {
            _queue.Add(new Item
            {
                At = at, Type = DamageText.TextType.Normal, Text = text, Offset = offset, HasHit = true, Kill = kill,
                Hit = new HitInfo
                {
                    Damage = Parse(text), MaxHealth = maxHealth, Sneak = sneak, StaggerCrit = crit,
                    Majority = HitData.DamageType.Slash,
                },
            });
        }

        private static void Element(float at, string text, Vector3 offset, float maxHealth, HitData.DamageType type)
        {
            _queue.Add(new Item
            {
                At = at, Type = DamageText.TextType.Normal, Text = text, Offset = offset, HasHit = true,
                Hit = new HitInfo
                {
                    Damage = Parse(text), MaxHealth = maxHealth, Dot = true, Elemental = true, Majority = type,
                },
            });
        }

        private static void Self(float at, string text, Vector3 offset, float maxHealth)
        {
            _queue.Add(new Item
            {
                At = at, Type = DamageText.TextType.Normal, Text = text, MySelf = true, Offset = offset, HasHit = true,
                Hit = new HitInfo { Damage = Parse(text), MaxHealth = maxHealth, Majority = HitData.DamageType.Blunt },
            });
        }

        private static float Parse(string text)
        {
            float v;
            return float.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        public static void Update(float now)
        {
            if (now < _dotUntil && now >= _nextDot)
            {
                _nextDot = now + 1f;
                float tick = Mathf.Round(Random.Range(4f, 9f));
                _queue.Add(new Item
                {
                    At = now,
                    Type = DamageText.TextType.Normal,
                    Text = tick.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                    Offset = new Vector3(0f, 1.6f, 3f),
                    HasHit = true,
                    Hit = new HitInfo
                    {
                        Damage = tick, MaxHealth = 200f, Dot = true, Elemental = true,
                        Majority = HitData.DamageType.Fire, TargetId = DotTargetId,
                    },
                });
            }

            if (now < _rainUntil && now >= _nextRain)
            {
                _nextRain = now + 0.22f;
                var type = (DamageText.TextType)Random.Range(0, 3);
                Add(now, type, Random.Range(2, 320).ToString(), Random.value < 0.15f,
                    new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0.9f, 2.1f), Random.Range(2f, 7f)));
            }
            if (_queue.Count == 0)
                return;

            Player p = Player.m_localPlayer;
            DamageText dt = DamageText.instance;
            Camera cam = Utils.GetMainCamera();
            if (p == null || dt == null || cam == null)
            {
                _queue.Clear();
                return;
            }

            Transform tr = p.transform;
            for (int i = 0; i < _queue.Count; i++)
            {
                Item it = _queue[i];
                if (it.At > now)
                    continue;
                _queue.RemoveAt(i--);

                Vector3 pos = tr.position + tr.right * it.Offset.x + Vector3.up * it.Offset.y + tr.forward * it.Offset.z;
                float distance = Vector3.Distance(cam.transform.position, pos);

                HitInfo saved = HitContext.Take();
                DurationOverride = _hold;
                FreezeMotion = _hold > 0f;
                try
                {
                    if (it.HasHit)
                    {
                        HitInfo hit = it.Hit;
                        hit.Pos = pos;
                        HitContext.Set(hit);
                    }
                    dt.AddInworldText(it.Type, pos, distance, it.Text, it.MySelf);

                    // The real kill styling is applied by the damage hook once the health has
                    // dropped; a pretend hit has to do it here.
                    if (it.Kill && PluginConfig.KillEnabled.Value)
                        Styles.ApplyKill(HitContext.Spawned);
                }
                finally
                {
                    DurationOverride = 0f;
                    FreezeMotion = false;
                    HitContext.Restore(saved);
                }
            }
        }
    }
}
