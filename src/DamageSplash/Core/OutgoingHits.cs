using UnityEngine;

namespace DamageSplash.Core
{
    /// <summary>
    /// Hits the local player sent to something another client owns. Those come back as a number
    /// with no hit context, since ApplyDamage runs on the owner, so on their own they cannot be
    /// told apart from anyone else's. The owner draws the number at the hit's own point, which
    /// crossed the wire unchanged, so a number arriving where one of ours landed a moment ago is
    /// ours. Only consulted by the "Mine" visibility mode.
    /// </summary>
    public static class OutgoingHits
    {
        private const int Capacity = 16;
        private const float MatchDistanceSq = 0.25f * 0.25f;
        private const float MaxAge = 3f;     // generous: a round trip to the owner and back

        private static readonly Vector3[] _points = new Vector3[Capacity];
        private static readonly float[] _times = new float[Capacity];
        private static int _next;

        public static void Record(Vector3 point)
        {
            _points[_next] = point;
            _times[_next] = Time.time;
            _next = (_next + 1) % Capacity;
        }

        public static bool Matches(Vector3 pos)
        {
            float now = Time.time;
            for (int i = 0; i < Capacity; i++)
            {
                float age = now - _times[i];
                if (_times[i] > 0f && age >= 0f && age <= MaxAge && (pos - _points[i]).sqrMagnitude <= MatchDistanceSq)
                    return true;
            }
            return false;
        }
    }
}
