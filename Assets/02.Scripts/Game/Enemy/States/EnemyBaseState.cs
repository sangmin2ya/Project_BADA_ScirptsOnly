using Mirror;
using MoreMountains.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;

public class EnemyBaseState : NetworkBehaviour
{
    // 상속하는 상태 클래스가 쓸 수 있도록 보호된 참조
    protected EnemyStateMachine _fsm;

    protected IEnemyController _controller;

    protected float _transitionCheckInterval = 0.2f;
    protected float _transitionTimer = 0;

    [Header("상태 전환 설정")]
    [SerializeField] protected List<StateTransition> _transitions = new List<StateTransition>();

    /// <summary>
    /// 컨트롤러가 의존성을 주입할 수 있게 하는 공개 설정 메서드
    /// </summary>
    public virtual void Setup(IEnemyController controller, EnemyStateMachine stateMachine) 
    {
        this._controller = controller;
        this._fsm = stateMachine;
        //Debug.Log($"<color=cyan>[{gameObject.name}/{this.GetType().Name}]</color> State setup complete.");
    }
    /// <summary>
    /// 상태 진입 시 호출
    /// </summary>
    public virtual void Enter() { }
    /// <summary>
    /// 상태 탈출 시 호출
    /// </summary>
    public virtual void Exit() { }
    /// <summary>
    /// 해당 상태일 때 Update에서 수행될 메서드
    /// </summary>
    public virtual void StateUpdate() 
    {
        CheckTransitoinWithInterval();
    }
    /// <summary>
    /// 해당 상태일 때 FixedUpdate에서 수행될 메서드
    /// </summary>
    public virtual void StatePhysicsUpdate() { }

    /// <summary>
    /// 주기적으로 다음 상태를 확인하는 메서드
    /// </summary>
    protected void CheckTransitoinWithInterval()
    {
        _transitionTimer -= Time.deltaTime;
        if (_transitionTimer <= 0)
        {
            _transitionTimer = _transitionCheckInterval;
            CheckTransitions();
        }
    }

    /// <summary>
    /// 설정된 모든 전환 조건을 순서대로 확인하고, 조건이 맞으면 상태를 변경합니다.
    /// </summary>
    protected virtual void CheckTransitions() 
    {
        foreach (var transition in _transitions)
        {
            // Decision이 할당되었고, 그 조건이 참이라면
            if (transition.Decision != null && transition.Decision.Decide(_controller))
            {
                // 해당 상태로 전환하고 루프를 빠져나옵니다.
                _fsm.ChangeState(transition.TrueState);
                break;
            }
        }
    }
}

/// <summary>
/// 상태 전환 규칙 명시 클래스
/// [조우석 - 2025.08.11.]
/// </summary>
[Serializable]
public class StateTransition
{
    [Tooltip("이 전환이 발생할 조건(Decision 애셋)")]
    public Decision Decision;
    [Tooltip("조건이 참일 때 전환될 상태 컴포넌트")]
    public EnemyBaseState TrueState;
}
