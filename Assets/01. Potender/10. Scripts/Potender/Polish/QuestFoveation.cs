using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Potender
{
    /// <summary>
    /// Day 5 성능: Quest 고정 포비티드 렌더링(FFR) 켜기 — 시야 가장자리 해상도를 낮춰 GPU 부하 감소.
    /// Project Settings ▸ XR Plug-in Management ▸ OpenXR (Android) ▸ Foveated Rendering 기능이 켜져 있어야 적용됨.
    /// 에디터(PC)에선 아무 효과 없음.
    /// </summary>
    public class QuestFoveation : MonoBehaviour
    {
        [Tooltip("0 = 끔, 1 = 최대. 가장자리가 흐려 보이면 0.5")]
        [Range(0f, 1f)] [SerializeField] float level = 1f;

        IEnumerator Start()
        {
            var displays = new List<XRDisplaySubsystem>();
            for (int i = 0; i < 120; i++)
            {
                SubsystemManager.GetSubsystems(displays);
                if (displays.Count > 0 && displays[0].running) break;
                yield return null;
            }
            foreach (var d in displays) d.foveatedRenderingLevel = level;
            if (displays.Count > 0) Debug.Log($"[Potender FPS] 포비티드 렌더링 level {level}");
        }
    }
}
