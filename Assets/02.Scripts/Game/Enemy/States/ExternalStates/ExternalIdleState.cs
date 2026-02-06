using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;

/// <summary>
/// 적 아무것도 동작하지 않는 상태
/// [조우석 - 2025.07.08.]
/// </summary>
public class ExternalIdleState : ExternalBaseState
{
    [SerializeField] private bool _hasExitTime = false;
    [SerializeField] private float _idleTime = 2f;
    private float _idleTimer;

    public override void Enter()
    {
       //Debug.Log($"[{gameObject.name}/{this.GetType().Name}] Now idling and waiting for a target.");
        // Debug.Log("Idle 상태 진입");
        _idleTimer = _idleTime;
    }

    public override void Exit()
    {
        // Debug.Log("Idle 상태 종료");
    }

    public override void StateUpdate()
    {
        // 탈출 시간이 있으면 
        if (_hasExitTime)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer < 0 )
            {
                _idleTimer = _idleTime;
                CheckTransitions();
            }
        }
        else
        {
            CheckTransitoinWithInterval();
        }
    }
}
