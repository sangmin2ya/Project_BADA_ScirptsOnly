using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적이 목표 대상으로부터 설정된 거리까지 도망가는 상태
/// [조우석 2025.08.09.]
/// </summary>
public class InternalFleeState : InternalMoveState
{
    public override void Enter()
    {
        base.Enter();
        Debug.Log($"[{_controller.gameObject.name}] Fleeing from target: {_controller.CurrentTarget?.name}");
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

        // 목표로부터 멀어지는 방향 벡터 계산
        _internalController.MoveDirection = (_controller.transform.position - _controller.CurrentTarget.position).normalized;

        _controller.Rigidbody.velocity = _internalController.MoveDirection * _moveSpeed;
    }
}
