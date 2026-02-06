using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적이 목표 대상에게 설정된 거리까지 접근하는 상태
/// [조우석 - 2025.08.08]
/// </summary>
public class InternalApproachState : InternalMoveState
{
    public override void Enter()
    {
        base.Enter();
        Debug.Log($"[{_controller.gameObject.name}] Approaching target: {_controller.CurrentTarget?.name}");
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void StatePhysicsUpdate()
    {
        if (!_controller.HasTarget)
        {
            return;
        }

        // 목표를 향하는 방향 벡터 계산
        _internalController.MoveDirection = (_controller.CurrentTarget.position - _controller.transform.position).normalized;

        // Rigidbody의 속도를 조절하여 이동
        _controller.Rigidbody.velocity = _internalController.MoveDirection * _moveSpeed;
    }
}
