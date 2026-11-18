using UnityEngine;

namespace Potender
{
    /// <summary>따르는 물줄기(LineRenderer 2점). 머티리얼은 버텍스 컬러를 쓰는 URP/Particles/Unlit 권장.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PourStream : MonoBehaviour
    {
        LineRenderer lr;

        void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.enabled = false;
        }

        public void Show(Vector3 from, Vector3 to, Color color)
        {
            lr.enabled = true;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startColor = lr.endColor = color;
        }

        public void Hide() { if (lr) lr.enabled = false; }
    }
}
