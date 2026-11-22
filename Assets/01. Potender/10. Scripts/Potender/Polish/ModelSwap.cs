using UnityEngine;

namespace Potender
{
    /// <summary>
    /// Day 5 아트 교체: 캡슐/큐브로 된 손님·고블린 몸을 Synty 캐릭터 모델로 바꿔 보여줌.
    ///  - 생성될 때 models 중 하나를 랜덤으로 골라 자식으로 붙이고, 기존 도형 렌더러(hideRenderers)는 숨김
    ///  - 키는 targetHeight(월드 m)에 맞추고, 발바닥은 기존 도형의 바닥 높이에 맞춤
    ///  - 모델의 콜라이더는 꺼 버림 (투척·서빙 판정은 원래 콜라이더가 담당)
    ///  - 휴머노이드 모델이면 애니메이션 파일 없이 코드로 포즈를 만듦 (팔 내리기 + 걸을 때 팔·다리 흔들기 + 숨쉬기)
    /// 말풍선 / 인내심 바 등 UI는 그대로 둠.
    /// </summary>
    public class ModelSwap : MonoBehaviour
    {
        [Tooltip("이 중 하나를 랜덤으로 사용")]
        [SerializeField] GameObject[] models;
        [Tooltip("모델이 붙으면 숨길 기존 도형 렌더러 (몸통·머리·코·귀)")]
        [SerializeField] Renderer[] hideRenderers;
        [Tooltip("모델 키 (월드 m). 0이면 원래 크기")]
        [SerializeField] float targetHeight = 1.7f;
        [Tooltip("모델 그림자 (Quest 성능 → 기본 끔)")]
        [SerializeField] bool castShadows = false;

        [Header("걷기 흔들림")]
        [SerializeField] float bobHeight = 0.04f;
        [SerializeField] float bobSpeed = 10f;
        [Tooltip("좌우 기우뚱 (포즈가 적용되면 무시)")]
        [SerializeField] float bobTilt = 4f;

        [Header("코드 포즈 (휴머노이드 모델 — Synty 캐릭터)")]
        [SerializeField] bool proceduralPose = true;
        [Tooltip("비우면 모델의 Animator에서 찾음 (지금은 참고용)")]
        [SerializeField] Avatar avatar;
        [Tooltip("팔을 내린 뒤 몸에서 벌어지는 각도 (도)")]
        [SerializeField] float armOutDeg = 8f;
        [Tooltip("팔꿈치를 앞으로 굽히는 각도 (0 = 쭉 폄)")]
        [SerializeField] float elbowBendDeg = 12f;
        [SerializeField] float armSwingDeg = 22f;
        [SerializeField] float legSwingDeg = 26f;
        [SerializeField] float kneeBendDeg = 35f;
        [Tooltip("한 걸음 길이 (m) — 속도에 맞춰 걸음 주기 계산")]
        [SerializeField] float strideLength = 0.55f;

        public Transform Model { get; private set; }

        Vector3 baseLocalPos;
        Quaternion baseLocalRot;
        Vector3 lastPos;
        float bobT, bobWeight, phase, speedSmooth;

        // 뼈 직접 회전 (HumanPoseHandler는 스케일 바꾼 모델에서 몸이 날아가는 문제가 있어서 안 씀)
        class Bone { public Transform t; public Quaternion rest; }
        Bone lArm, rArm, lLeg, rLeg, lKnee, rKnee;
        bool posed;

        void Awake()
        {
            if (models == null || models.Length == 0) return;
            var prefab = models[Random.Range(0, models.Length)];
            if (!prefab) return;

            float footY = transform.position.y;
            if (TryGetBounds(hideRenderers, out var oldB)) footY = oldB.min.y;

            Model = Instantiate(prefab, transform).transform;
            Model.name = "Model_" + prefab.name;
            Model.localPosition = Vector3.zero;
            Model.localRotation = Quaternion.identity;

            foreach (var c in Model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in Model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            SetupPose();

            var rs = Model.GetComponentsInChildren<Renderer>(true);
            if (targetHeight > 0f && TryGetBounds(rs, out var b) && b.size.y > 0.01f)
                Model.localScale *= targetHeight / b.size.y;
            if (TryGetBounds(rs, out b))
                Model.position += Vector3.up * (footY - b.min.y);

            if (hideRenderers != null)
                foreach (var r in hideRenderers) if (r) r.enabled = false;

            baseLocalPos = Model.localPosition;
            baseLocalRot = Model.localRotation;
            lastPos = transform.position;
        }

        void SetupPose()
        {
            if (!proceduralPose) return;
            var anim = Model.GetComponentInChildren<Animator>(true);
            Transform B(HumanBodyBones hb, string synty)
            {
                Transform t = null;
                if (anim && anim.isHuman) t = anim.GetBoneTransform(hb);
                return t ? t : FindDeep(Model, synty);
            }
            var lUp = B(HumanBodyBones.LeftUpperArm, "Shoulder_L");
            var rUp = B(HumanBodyBones.RightUpperArm, "Shoulder_R");
            var lLo = B(HumanBodyBones.LeftLowerArm, "Elbow_L");
            var rLo = B(HumanBodyBones.RightLowerArm, "Elbow_R");
            var lHand = B(HumanBodyBones.LeftHand, "Hand_L");
            var rHand = B(HumanBodyBones.RightHand, "Hand_R");
            var lUl = B(HumanBodyBones.LeftUpperLeg, "UpperLeg_L");
            var rUl = B(HumanBodyBones.RightUpperLeg, "UpperLeg_R");
            var lLl = B(HumanBodyBones.LeftLowerLeg, "LowerLeg_L");
            var rLl = B(HumanBodyBones.RightLowerLeg, "LowerLeg_R");
            if (anim) anim.enabled = false; // 컨트롤러 없는 Animator · 루트 모션이 끼어들지 않게
            if (!lUp || !rUp || !lLo || !rLo) return;

            // T자 팔 → 아래로 (월드 방향 기준이라 뼈 축 방향과 무관)
            lArm = ArmDown(lUp, lLo, lHand, -1f);
            rArm = ArmDown(rUp, rLo, rHand, 1f);
            lLeg = Rest(lUl); rLeg = Rest(rUl);
            lKnee = Rest(lLl); rKnee = Rest(rLl);
            posed = true;
        }

        Bone ArmDown(Transform up, Transform lo, Transform hand, float side)
        {
            // 1) 윗팔: T자 → 아래로
            Vector3 dir = (lo.position - up.position).normalized;
            Vector3 down = Quaternion.AngleAxis(side * armOutDeg, Model.forward) * -Model.up;
            up.rotation = Quaternion.FromToRotation(dir, down) * up.rotation;

            // 2) 아래팔: 윗팔과 일직선으로 편 뒤 앞으로 살짝만 굽힘 (모델 기본 포즈의 팔꿈치 꺾임 제거)
            if (hand)
            {
                Vector3 fore = (hand.position - lo.position).normalized;
                Vector3 want = Quaternion.AngleAxis(-elbowBendDeg, Model.right) * down;
                lo.rotation = Quaternion.FromToRotation(fore, want) * lo.rotation;
            }
            return new Bone { t = up, rest = up.localRotation };
        }

        static Bone Rest(Transform t) => t ? new Bone { t = t, rest = t.localRotation } : null;

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>rest 자세에서 모델 오른쪽 축으로 deg 만큼 회전 (+ = 뒤로)</summary>
        void Swing(Bone b, float deg)
        {
            if (b == null || !b.t) return;
            b.t.localRotation = b.rest;
            if (Mathf.Abs(deg) > 0.01f) b.t.rotation = Quaternion.AngleAxis(deg, Model.right) * b.t.rotation;
        }

        void ApplyPose(float walk, float ph)
        {
            if (!posed) return;
            float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
            float idle = Mathf.Sin(Time.time * 1.6f) * 2f * (1f - walk);
            Swing(lArm, -s * armSwingDeg * walk + idle);
            Swing(rArm, s * armSwingDeg * walk + idle);
            Swing(lLeg, s * legSwingDeg * walk);
            Swing(rLeg, -s * legSwingDeg * walk);
            Swing(lKnee, kneeBendDeg * walk * Mathf.Max(0f, c));
            Swing(rKnee, kneeBendDeg * walk * Mathf.Max(0f, -c));
        }

        void LateUpdate()
        {
            if (!Model) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float speed = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;
            speedSmooth = Mathf.Lerp(speedSmooth, speed, 1f - Mathf.Exp(-10f * dt));
            bobWeight = Mathf.MoveTowards(bobWeight, speedSmooth > 0.15f ? 1f : 0f, dt * 6f);

            if (posed)
            {
                // 걸음 주기: 한 걸음(strideLength)마다 반 바퀴
                float stride = Mathf.Max(0.1f, strideLength * transform.lossyScale.y);
                phase += dt * Mathf.PI * Mathf.Max(speedSmooth, 0.6f * bobWeight) / stride;
                ApplyPose(bobWeight, phase);
            }

            if (bobWeight <= 0f) { Model.localPosition = baseLocalPos; Model.localRotation = baseLocalRot; return; }

            bobT = posed ? phase : bobT + dt * bobSpeed;
            float inv = transform.lossyScale.y > 0.001f ? 1f / transform.lossyScale.y : 1f;
            float h = posed ? bobHeight * 0.5f : bobHeight;
            Model.localPosition = baseLocalPos + Vector3.up * (Mathf.Abs(Mathf.Sin(bobT)) * h * inv * bobWeight);
            Model.localRotation = posed ? baseLocalRot : baseLocalRot * Quaternion.Euler(0f, 0f, Mathf.Sin(bobT) * bobTilt * bobWeight);
        }

        static bool TryGetBounds(Renderer[] rs, out Bounds b)
        {
            b = default;
            bool any = false;
            if (rs == null) return false;
            foreach (var r in rs)
            {
                if (!r) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
