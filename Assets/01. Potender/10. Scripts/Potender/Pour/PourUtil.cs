
using UnityEngine;

namespace Potender
{
    public class PourUtil
    {
        static readonly RaycastHit[] Buffer = new RaycastHit[16];
        
        public static bool IsTilted(Transform t, float angle) => Vector3.Angle(t.up, Vector3.up) >= angle;

        public static bool CastDown(Transform self, Vector3 origin, float maxDist, LayerMask mask, out RaycastHit hit)
        {
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, Buffer, maxDist, mask, QueryTriggerInteraction.Collide);
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (Buffer[i].transform.IsChildOf(self)) continue;
                if (Buffer[i].distance < best) { best = Buffer[i].distance; hit = Buffer[i]; found = true; }
            }
            return found;
        }
    }
}

    