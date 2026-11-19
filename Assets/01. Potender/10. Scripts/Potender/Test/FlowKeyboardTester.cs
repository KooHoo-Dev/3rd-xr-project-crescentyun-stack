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
        }
    }
}
