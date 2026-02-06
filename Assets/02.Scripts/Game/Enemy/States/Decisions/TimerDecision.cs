using UnityEngine;

/// <summary>
/// 일정 시간이 지나면 true가 되는 Decision
/// [조우석 - 2025.08.14.]
/// </summary>
[CreateAssetMenu(menuName = "ScriptableObjects/Enemy/Decisions/Timer")]
public class StateTimerDecision : Decision
{
    [Tooltip("몇 초 후에 true가 될 것인가")]
    [SerializeField] private float _checkTime = 5;

    public override bool Decide(IEnemyController controller)
    {
        var currentState = controller.StateMachine.CurrentState;

        if (currentState is ITimeTrackableState timeTrackableState)
        {
            // 현재 상태가 ITimeTrackableState 인지 확인 후
            // 맞으면 체크 중인 시간이 checkTime을 넘겼는지 확인
            return timeTrackableState.Timer > _checkTime;
        }

        // 현재 상태가 시간을 추적하는 상태가 아니라면 항상 false를 반환
        return false;
    }
}