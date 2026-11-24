using UnityEngine;

namespace AnimalsVsMonsters
{
    /// <summary>Finds the static ground below a point, ignoring characters (anything with a Rigidbody) and triggers.</summary>
    public static class GroundUtil
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static bool TryGetGroundY(Vector3 from, out float groundY, float maxDistance = 30f)
        {
            int n = Physics.RaycastNonAlloc(from + Vector3.up * 0.1f, Vector3.down, Hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            groundY = 0f;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].collider.attachedRigidbody != null) continue;
                if (Hits[i].distance < best)
                {
                    best = Hits[i].distance;
                    groundY = Hits[i].point.y;
                }
            }
            return best < float.MaxValue;
        }
    }
}
