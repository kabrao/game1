using System.Collections.Generic;
using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Global registry of active enemy transforms. Used for inter-enemy
    /// separation steering (boid-style repulsion) so they don't pile up
    /// in a single stack the player can circle.
    /// </summary>
    public static class EnemyRegistry
    {
        static readonly List<Transform> _all = new List<Transform>(128);

        public static void Register(Transform t)
        {
            if (t != null && !_all.Contains(t)) _all.Add(t);
        }

        public static void Unregister(Transform t)
        {
            _all.Remove(t);
        }

        /// <summary>
        /// Returns a unit-ish vector pointing away from nearby enemies, weighted
        /// by 1/distance so the closest ones push hardest.
        /// </summary>
        public static Vector3 GetSeparation(Vector3 pos, float radius)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            float r2 = radius * radius;
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var t = _all[i];
                if (t == null) { _all.RemoveAt(i); continue; }
                Vector3 diff = pos - t.position;
                diff.y = 0f;
                float distSq = diff.sqrMagnitude;
                if (distSq > 0.0001f && distSq < r2)
                {
                    float dist = Mathf.Sqrt(distSq);
                    // weight = 1 at touching, 0 at edge
                    float weight = 1f - (dist / radius);
                    sum += (diff / dist) * weight;
                    count++;
                }
            }
            return count > 0 ? sum / count : Vector3.zero;
        }
    }
}
