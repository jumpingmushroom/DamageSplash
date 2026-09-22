using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>How a number moves and scales over its life. All lengths in world metres so the
    /// motion reads the same at any distance; the pop is a screen-space scale.</summary>
    public struct AnimParams
    {
        public float Duration;
        public float PopFrom;       // scale at t=0; 1 disables the pop
        public float PopDuration;   // seconds to settle to 1
        public float Overshoot;     // 0..1, how far the pop settles past resting size
        public float FadeIn;        // seconds to fade in; 0 appears instantly
        public float Tilt;          // degrees, max random rotation
        public float RiseSpeed;     // initial speed, m/s
        public float Gravity;       // m/s^2, 0 = vanilla drift
        public float Spray;         // degrees off vertical, max
        public float ExitShrink;    // scale at the very end; 1 disables
        public bool Bounce;
        public float Jitter;        // spawn-point random offset, metres

        public static AnimParams Vanilla => new AnimParams
        {
            Duration = 1.5f, PopFrom = 1f, PopDuration = 0f, Overshoot = 0f, FadeIn = 0f,
            Tilt = 0f, RiseSpeed = 1f, Gravity = 0f, Spray = 0f, ExitShrink = 1f,
            Bounce = false, Jitter = 0.5f,
        };
    }

    public static class Animation
    {
        private const float ExitPortion = 0.3f;
        private const float BounceRestitution = 0.45f;
        private const float BackEase = 1.70158f;   // the usual ease-out-back constant

        /// <summary>A random tilt for one number, in degrees.</summary>
        public static float Tilt(ref AnimParams p)
        {
            return p.Tilt > 0f ? Random.Range(-p.Tilt, p.Tilt) : 0f;
        }

        public static Vector3 InitialVelocity(ref AnimParams p)
        {
            if (p.Spray <= 0f)
                return Vector3.up * p.RiseSpeed;
            float tilt = Random.Range(0f, p.Spray) * Mathf.Deg2Rad;
            float azimuth = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector3(Mathf.Sin(tilt) * Mathf.Cos(azimuth), Mathf.Cos(tilt), Mathf.Sin(tilt) * Mathf.Sin(azimuth));
            return dir * p.RiseSpeed;
        }

        /// <summary>One step of the flight. Offset is relative to the spawn point.</summary>
        public static void Step(ref AnimParams p, ref Vector3 offset, ref Vector3 velocity, float dt)
        {
            if (p.Gravity > 0f)
                velocity.y -= p.Gravity * dt;
            offset += velocity * dt;
            if (p.Bounce && offset.y < 0f && velocity.y < 0f)
            {
                offset.y = -offset.y * BounceRestitution;
                velocity.y = -velocity.y * BounceRestitution;
                velocity.x *= 0.6f;
                velocity.z *= 0.6f;
            }
        }

        /// <summary>Screen-space scale multiplier at a moment in the life.</summary>
        public static float Scale(ref AnimParams p, float timer, float t01)
        {
            float s = 1f;
            if (p.PopDuration > 0f && timer < p.PopDuration && p.PopFrom != 1f)
            {
                // Ease-out back: the curve passes its target and settles, so a number that pops
                // in large dips just under its resting size before coming to rest. With
                // Overshoot 0 the c1 term vanishes and this is exactly ease-out cubic.
                float u = timer / p.PopDuration - 1f;
                float c1 = BackEase * p.Overshoot;
                float k = 1f + (c1 + 1f) * u * u * u + c1 * u * u;
                s = Mathf.LerpUnclamped(p.PopFrom, 1f, k);
            }
            if (p.ExitShrink != 1f && t01 > 1f - ExitPortion)
            {
                float k = (t01 - (1f - ExitPortion)) / ExitPortion;
                s *= Mathf.Lerp(1f, p.ExitShrink, k);
            }
            return s;
        }

        /// <summary>Vanilla's fade (opaque for most of the life, then a quick cubic drop), with
        /// an optional fade-in over the first moments so a number does not blink into being.</summary>
        public static float Alpha(ref AnimParams p, float timer, float t01)
        {
            float a = 1f - t01 * t01 * t01;
            if (p.FadeIn > 0f && timer < p.FadeIn)
                a *= timer / p.FadeIn;
            return a;
        }
    }
}
