using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 내부 적의 Idle 상태
/// [조우석 - 2025.08.12.]
/// </summary>
public class InternalIdleState : InternalBaseState
{
    [SerializeField] private bool _hasExitTime = false;
    [SerializeField] private float _idleTime = 2f;
    private float _idleTimer;

    public override void Enter()
    {
        base.Enter();
        Debug.Log($"[{gameObject.name}/{this.GetType().Name}] Now idling and waiting for a target.");
        // Debug.Log("Idle 상태 진입");
        _idleTimer = _idleTime;

        _internalController.AnimationController?.SetWalk(false);
        _internalController.AnimationController?.SetAttacking(false);
    }

    public override void Exit()
    {
        base.Exit();
        // Debug.Log("Idle 상태 종료");
    }

    public override void StateUpdate()
    {
        // 탈출 시간이 있으면 시간 체크 후 다음 상태 체크
        if (_hasExitTime)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer < 0)
            {
                _idleTimer = _idleTime;
                CheckTransitions();
            }
        }
        // 탈출 시간이 없으면 일정 간격마다 다음 상태를 확인
        else
        {
            CheckTransitoinWithInterval();
        }
    }
}
