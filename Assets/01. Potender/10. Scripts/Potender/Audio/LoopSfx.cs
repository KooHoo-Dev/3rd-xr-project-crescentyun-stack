using UnityEngine;

namespace Potender
{
    /// <summary>
    /// 붓기 / 흔들기 루프 사운드. 재료 병·쉐이커에 붙이고 clip만 넣으면 됨 (AudioSource는 자동 생성).
    ///  Pour  : 재료 병(IngredientBottle) 또는 쉐이커(MixingContainer)가 따르는 동안
    ///  Shake : 쉐이커(ShakeDetector)가 흔들리는 동안 — 빠를수록 피치 ↑
    /// 쉐이커는 Pour 1개 + Shake 1개, 두 개 붙이기.
    /// </summary>
    public class LoopSfx : MonoBehaviour
    {
        public enum Mode { Pour, Shake }

        [SerializeField] Mode mode = Mode.Pour;
        [SerializeField] AudioClip clip;
        [Range(0f, 1f)] [SerializeField] float volume = 0.7f;
        [Tooltip("소리 켜지고 꺼지는 속도 (클수록 빠름)")]
        [SerializeField] float fadeSpeed = 8f;
        [Tooltip("흔들기 방향이 바뀔 때 잠깐 끊기는 것 방지")]
        [SerializeField] float holdTime = 0.15f;

        [Header("흔들기 피치")]
        [SerializeField] Vector2 pitchRange = new(0.9f, 1.3f);
        [SerializeField] float speedForMaxPitch = 4f;

        AudioSource src;
        IngredientBottle bottle;
        MixingContainer container;
        ShakeDetector shake;
        float hold;

        void Awake()
        {
            bottle = GetComponentInParent<IngredientBottle>();
            container = GetComponentInParent<MixingContainer>();
            shake = GetComponentInParent<ShakeDetector>();

            src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 1f;      // 3D: 손 위치에서 들림
            src.minDistance = 0.5f;
            src.maxDistance = 8f;
            src.volume = 0f;
        }

        void Update()
        {
            if (!clip) return;
            bool on = mode == Mode.Pour
                ? (bottle ? bottle.IsPouring : container && container.IsPouring)
                : shake && shake.IsShaking;

            hold = on ? holdTime : hold - Time.deltaTime;
            float target = hold > 0f ? volume : 0f;
            src.volume = Mathf.MoveTowards(src.volume, target, fadeSpeed * Time.deltaTime);

            if (mode == Mode.Shake && shake)
                src.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, Mathf.InverseLerp(0.8f, speedForMaxPitch, shake.CurrentSpeed));

            if (src.volume > 0.001f) { if (!src.isPlaying) src.Play(); }
            else if (src.isPlaying) src.Pause();
        }
    }
}
