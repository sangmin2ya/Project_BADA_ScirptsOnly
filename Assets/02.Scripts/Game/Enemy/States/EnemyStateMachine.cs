using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 상태 초기화 및 변경을 관리하는 메서드
/// [조우석 - 2025.07.08]
/// </summary>
public class EnemyStateMachine : MonoBehaviour
{
    public EnemyBaseState CurrentState { get; private set; }

    public void Initialize(EnemyBaseState startingState)
    {
        Debug.Log($"<color=yellow>[StateMachine]</color> Initializing with state: {startingState.GetType().Name}");
        CurrentState = startingState;
        CurrentState.Enter(); // 초기 상태 진입
    }

    /// <summary>
    /// 상태 변경
    /// - 기존 상태의 Exit, 바뀔 상태의 Enter를 수행
    /// </summary>
    /// <param name="newState"></param>
    public void ChangeState(EnemyBaseState newState)
    {
        if (newState == null)
        {
            Debug.LogError("[StateMachine] Cannot change to a null state!");
            return;
        }

        string fromState = CurrentState != null ? CurrentState.GetType().Name : "NULL";
        //Debug.Log($"<color=yellow>[StateMachine]</color> Changing state from <b>{fromState}</b> to <b>{newState.GetType().Name}</b>");

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }
}
