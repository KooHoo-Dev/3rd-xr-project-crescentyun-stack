using UnityEngine;

namespace Potender
{
    public class PourUtil
    {
        static readonly RaycastHit[] Buffer = new RaycastHit[16];

        public static bool IsTilted(Transform t, float angle) => Vector3.Angle(t.up, Vector3.up) >= angle;
        public static float TiltAngle(Transform t) => Vector3.Angle(t.up, Vector3.up);

        public static bool CastDown(Transform self, Vector3 origin, float maxDist, LayerMask mask, out RaycastHit hit) =>
            CastSegment(self, origin, Vector3.down, maxDist, mask, out hit);

        /// <summary>자기 자신(자식 포함)은 무시하고 가장 가까운 충돌</summary>
        public static bool CastSegment(Transform self, Vector3 origin, Vector3 dir, float dist, LayerMask mask, out RaycastHit hit)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, Buffer, dist, mask, QueryTriggerInteraction.Collide);
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

        /// <summary>
        /// 물줄기 경로. PourFx.arc 켜짐 → 포물선(v0 + 중력), 꺼짐 → 예전처럼 수직 직선 2점.
        /// pts에 경로를 채우고 count 반환. 맞은 게 있으면 true + hit.
        /// </summary>
        public static bool Trace(Transform self, Vector3 origin, Vector3 v0, float maxDrop, LayerMask mask,
                                 Vector3[] pts, out int count, out RaycastHit hit)
        {
            pts[0] = origin;
            if (!PourFx.Arc || pts.Length < 3)
            {
                bool f = CastDown(self, origin, maxDrop, mask, out hit);
                pts[1] = f ? hit.point : origin + Vector3.down * maxDrop;
                count = 2;
                return f;
            }

            float dt = Mathf.Max(0.005f, PourFx.I.arcStep);
            Vector3 g = Physics.gravity, p = origin, v = v0;
            hit = default;
            count = 1;
            for (int i = 1; i < pts.Length; i++)
            {
                Vector3 np = p + v * dt + 0.5f * dt * dt * g;
                v += g * dt;
                Vector3 seg = np - p;
                float len = seg.magnitude;
                if (len > 1e-5f && CastSegment(self, p, seg / len, len, mask, out hit))
                {
                    pts[i] = hit.point;
                    count = i + 1;
                    return true;
                }
                pts[i] = np;
                count = i + 1;
                p = np;
                if (origin.y - p.y >= maxDrop) break;
            }
            return false;
        }
    }
}
