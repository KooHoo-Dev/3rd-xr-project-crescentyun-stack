using UnityEngine;
using UnityEngine.InputSystem;

namespace Potender
{
    /// <summary>
    /// 헤드셋 없이 게임 흐름 테스트 (키보드).
    ///  Enter : 타이틀이면 영업 시작 / 정산이면 재시작
    ///  C     : 첫 번째 대기 손님에게 '맞는' 포션 서빙
    ///  X     : 첫 번째 대기 손님에게 '틀린' 포션 서빙
    ///  T     : 영업 시간 30초 건너뛰기
    ///  R     : (Day 4) 강도 습격 바로 시작
    ///  F     : (Day 4) 잔에 불꽃 포션 채워서 고블린에게 던지기 (실제 투척·폭발 판정 테스트)
    ///  G     : (Day 4) 고블린 즉시 명중 처리
    /// 실제 제조→서빙은 BrewingKeyboardTester(1,2,L,Space,P) + 잔을 소켓에 직접 옮겨서 테스트.
    /// </summary>
    public class FlowKeyboardTester : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            var flow = GameFlow.Instance;
            if (kb == null || !flow) return;

            if (kb.enterKey.wasPressedThisFrame)
            {
                if (flow.Current == GameFlow.State.Title) flow.StartGame();
                else if (flow.Current == GameFlow.State.Result) flow.Restart();
            }
            if (kb.cKey.wasPressedThisFrame) flow.DebugServeFirst(true);
            if (kb.xKey.wasPressedThisFrame) flow.DebugServeFirst(false);
            if (kb.tKey.wasPressedThisFrame) flow.DebugSkipTime(30f);

            var raid = RaidEvent.Instance;
            if (!raid) return;
            if (kb.rKey.wasPressedThisFrame) raid.StartRaid();
            if (kb.gKey.wasPressedThisFrame) raid.DebugHitGoblin();
            if (kb.fKey.wasPressedThisFrame) DebugThrow(raid);
        }

        static void DebugThrow(RaidEvent raid)
        {
            var thrower = FindFirstObjectByType<PotionThrowable>();
            if (!thrower) { Debug.Log("[Potender] 잔에 PotionThrowable 없음"); return; }
            if (!raid.FirePotion) { Debug.Log("[Potender] RaidEvent의 firePotion 비어 있음"); return; }

            Vector3 target;
            if (raid.ActiveGoblin) target = raid.ActiveGoblin.transform.position + Vector3.up * 1.0f;
            else
            {
                var cam = Camera.main ? Camera.main.transform : thrower.transform;
                target = cam.position + cam.forward * 3f;
            }
            thrower.DebugThrowAt(raid.FirePotion, target);
        }
    }
}
