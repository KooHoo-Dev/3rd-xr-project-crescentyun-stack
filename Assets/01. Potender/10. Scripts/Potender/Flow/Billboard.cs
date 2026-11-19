using UnityEngine;

namespace Potender
{
    /// <summary>월드 캔버스(말풍선 등)가 항상 플레이어 쪽을 보게 (Y축 회전만).</summary>
    public class Billboard : MonoBehaviour
    {
        Transform cam;

        void LateUpdate()
        {
            if (!cam) { if (Camera.main) cam = Camera.main.transform; else return; }
            Vector3 dir = transform.position - cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
