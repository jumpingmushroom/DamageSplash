using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>One number in flight.</summary>
    public sealed class Splash
    {
        public GameObject Go;
        public Transform Tr;
        public TMP_Text Text;
        public Vector3 Origin;
        public Vector3 Offset;
        public Vector3 Velocity;
        public float Timer;
        public float Duration;
        public float Tilt;
        public float BaseFontSize;   // before the magnitude tier, so a kill can be resized
        public string Number;        // the number without its tag, for the same reason
        public float Value;          // the number as a number, so ticks can add into it
        public float MaxHealth;
        public int MergeKey;         // 0: this number never merges
        public float FirstSpawn;
        public AnimParams Anim;
        public Color Color;
        public DamageText.TextType Type;
        public bool Visible;
    }

    /// <summary>
    /// Replaces vanilla's instantiate-and-destroy per hit with a pool of the same prefab under
    /// the same parent. The objects are children of the DamageText object, so they die with the
    /// HUD; Init is called from DamageText.Awake and starts over.
    /// </summary>
    public static class SplashPool
    {
        private static readonly List<Splash> _active = new List<Splash>(64);
        private static readonly Stack<Splash> _free = new Stack<Splash>(64);
        private static Transform _parent;
        private static GameObject _prefab;
        private static bool _ready;

        public static TMP_FontAsset PrefabFont { get; private set; }
        public static Material PrefabMaterial { get; private set; }
        public static int ActiveCount => _active.Count;
        public static int FreeCount => _free.Count;
        public static bool Ready => _ready;

        public static void Init(DamageText dt)
        {
            Clear();
            _parent = dt.transform;
            _prefab = dt.m_worldTextBase;
            TMP_Text t = _prefab != null ? _prefab.GetComponent<TMP_Text>() : null;
            PrefabFont = t != null ? t.font : null;
            PrefabMaterial = t != null ? t.fontSharedMaterial : null;
            _ready = _prefab != null && t != null;
            if (!_ready)
                DamageSplashPlugin.Log.LogWarning("DamageText prefab has no TMP_Text; leaving vanilla in charge");
            else if (PluginConfig.Verbose.Value)
                DamageSplashPlugin.Log.LogDebug("pool ready: prefab font " + (PrefabFont != null ? PrefabFont.name : "none")
                    + ", material " + (PrefabMaterial != null ? PrefabMaterial.name + " / " + PrefabMaterial.shader.name : "none"));
        }

        public static void Clear()
        {
            foreach (Splash s in _active)
                if (s.Go != null) Object.Destroy(s.Go);
            foreach (Splash s in _free)
                if (s.Go != null) Object.Destroy(s.Go);
            _active.Clear();
            _free.Clear();
            _ready = false;
        }

        /// <summary>A live number this one can be added into, or null.</summary>
        public static Splash FindMergeable(int key, float now, float window, float maxLife)
        {
            if (key == 0)
                return null;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Splash s = _active[i];
                if (s.MergeKey != key || s.Go == null)
                    continue;
                // Time since this number was last fed. Deliberately not "still in its first
                // half of life": burning ticks once a second and half of the default 1.8 s life
                // is 0.9 s, so that rule would have meant nothing ever merged. A number caught
                // while fading is fine, because feeding it resets its clock and it brightens.
                if (s.Timer > window)
                    continue;
                if (now - s.FirstSpawn > maxLife)
                    continue;
                return s;
            }
            return null;
        }

        public static Splash Spawn(Style style, string text, Vector3 worldPos, float distance, DamageText.TextType type, int mergeKey = 0)
        {
            if (!_ready || _parent == null)
                return null;

            int cap = PluginConfig.MaxAlive.Value;
            while (_active.Count >= cap && _active.Count > 0)
                Recycle(0);

            Splash s = _free.Count > 0 ? _free.Pop() : null;
            if (s == null || s.Go == null)
            {
                s = new Splash();
                s.Go = Object.Instantiate(_prefab, _parent);
                s.Tr = s.Go.transform;
                s.Text = s.Go.GetComponent<TMP_Text>();
            }

            TMP_Text t = s.Text;
            TMP_FontAsset font = style.Font ?? PrefabFont;
            if (t.font != font)
                t.font = font;                                  // also resets the shared material
            Material mat = style.Material ?? (font == PrefabFont ? PrefabMaterial : font.material);
            if (t.fontSharedMaterial != mat)
                t.fontSharedMaterial = mat;
            t.fontStyle = style.FauxBold ? FontStyles.Bold : FontStyles.Normal;
            t.richText = true;                                  // tags are drawn with <size>
            float baseSize = SizeAt(distance);
            t.fontSize = baseSize * style.SizeScale;
            t.text = text;
            t.color = style.Color;

            s.BaseFontSize = baseSize;
            s.Number = style.Number ?? text;
            s.Value = style.Value;
            s.MaxHealth = style.MaxHealth;
            s.MergeKey = mergeKey;
            s.FirstSpawn = Time.time;

            s.Type = type;
            s.Color = style.Color;
            s.Anim = style.Anim;
            s.Duration = style.Duration;
            s.Timer = 0f;
            s.Origin = worldPos + Random.insideUnitSphere * style.Anim.Jitter;
            s.Offset = Vector3.zero;
            s.Velocity = Animation.InitialVelocity(ref s.Anim);
            s.Visible = false;
            s.Tilt = Animation.Tilt(ref s.Anim);
            s.Tr.localRotation = s.Tilt != 0f ? Quaternion.Euler(0f, 0f, s.Tilt) : Quaternion.identity;
            s.Tr.localScale = Vector3.one * (style.Anim.PopDuration > 0f ? style.Anim.PopFrom : 1f);
            s.Go.SetActive(false);                              // positioned on the next update
            _active.Add(s);
            return s;
        }

        /// <summary>Vanilla's near/far sizes, but blended between two distances instead of
        /// stepped. Equal distances give the vanilla step.</summary>
        public static float SizeAt(float distance)
        {
            float near = PluginConfig.NearDistance.Value, far = PluginConfig.FarDistance.Value;
            if (far <= near)
                return distance > near ? PluginConfig.FarSize.Value : PluginConfig.NearSize.Value;
            float k = Mathf.Clamp01((distance - near) / (far - near));
            return Mathf.Lerp(PluginConfig.NearSize.Value, PluginConfig.FarSize.Value, k);
        }

        public static void Update(float dt)
        {
            if (_active.Count == 0)
                return;
            Camera cam = Utils.GetMainCamera();
            if (cam == null)
                return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Splash s = _active[i];
                if (s.Go == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                s.Timer += dt;
                if (s.Timer > s.Duration)
                {
                    Recycle(i);
                    continue;
                }

                Animation.Step(ref s.Anim, ref s.Offset, ref s.Velocity, dt);
                float t01 = Mathf.Clamp01(s.Timer / s.Duration);

                Vector3 screen = cam.WorldToScreenPointScaled(s.Origin + s.Offset);
                bool visible = screen.z >= 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
                if (visible != s.Visible)
                {
                    s.Visible = visible;
                    s.Go.SetActive(visible);
                }
                if (!visible)
                    continue;

                s.Tr.position = screen;
                s.Tr.localScale = Vector3.one * Animation.Scale(ref s.Anim, s.Timer, t01);
                Color c = s.Color;
                c.a *= Animation.Alpha(ref s.Anim, s.Timer, t01);
                s.Text.color = c;
            }
        }

        private static void Recycle(int index)
        {
            Splash s = _active[index];
            _active.RemoveAt(index);
            if (s.Go == null)
                return;
            s.Go.SetActive(false);
            s.Visible = false;
            _free.Push(s);
        }
    }
}
