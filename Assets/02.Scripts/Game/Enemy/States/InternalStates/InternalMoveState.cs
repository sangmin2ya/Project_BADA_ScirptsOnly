using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내부 적의 이동 상태
/// - 내부 적은 해당 클래스를 상속받아야 함
/// [조우석 - 2025.08.08.]
/// </summary>
public class InternalMoveState : InternalBaseState
{
    [Tooltip("이동 속도")]
    [SerializeField] protected float _moveSpeed = 1;
    protected Vector3 _targetPosition;

    public override void Enter()
    {
        base.Enter();
        _internalController.AnimationController?.SetWalk(true);
        _internalController.AnimationController?.SetAttacking(false);
    }

    public override void Exit()
    {
        base.Exit();
        _internalController.AnimationController?.SetWalk(false);

        // 상태를 나갈 때 반드시 속도를 0으로 만들어 관성으로 미끄러지는 현상을 방지합니다.
        if (_controller.Rigidbody != null)
        {
            _controller.Rigidbody.velocity = Vector2.zero;
        }
    }
}
