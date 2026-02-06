using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 내부 적이 에임 방향을 향해서 돌진하는 상태
/// [조우석 - 2025.08.13.]
/// </summary>
public class InternalChargeAttackState : InternalAttackState, ITimeTrackableState
{
    [SerializeField] private float _chargeMoveSpeed = 10;
    [SerializeField] private InternalBaseState _idleState;
    private float _timer = 0;

    public float Timer => _timer;   // ITimeTrackableState 구현

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        // 기본적으로 서버에서만 충돌을 처리
        if (!_internalController.isServer)
        {
            return;
        }
        // 플레이어와 충돌 시 플레이어 스턴
        if (_fsm.CurrentState == this)
        {
            if (collision.gameObject.TryGetComponent<PhysicsHandler>(out var physicsHandler))
            {
                if (physicsHandler.PhysicsObjectType == PhysicsObjectType.플레이어)
                {
                    physicsHandler.GetComponent<PlayerState>().ApplyStun();
                }
            }
            else if (collision.collider !=  null)
            {
                _fsm.ChangeState(_idleState);
            }
        }
    }

    public override void Enter()
    {
        base.Enter();
        _timer = 0;
    }

    public override void Exit()
    {
        base.Exit();
        _internalController.AnimationController?.SetAttacking(false);
    }

    public override void StateUpdate()
    {
        base.StateUpdate();
        _timer += Time.deltaTime;
    }

    public override void StatePhysicsUpdate()
    {
        base.StatePhysicsUpdate();

        // 일정 속도로 에임을 향해 돌진
        _internalController.Rigidbody.velocity = _internalController.AimDirection * _chargeMoveSpeed;
    }
}
