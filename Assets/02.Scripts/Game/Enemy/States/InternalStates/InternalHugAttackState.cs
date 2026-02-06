using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 내부 적이 에임 방향을 향해서 돌진하는 상태
/// [조우석 - 2025.11.20.]
/// </summary>
public class InternalHugAttackState : InternalChargeAttackState
{
    protected override void OnCollisionEnter2D(Collision2D collision)
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
                    // 플레이어에게 스턴을 걸고 시야를 줄이고 이동속도를 줄인다
                    //physicsHandler.GetComponent<PlayerState>().ApplyStun();
                    if (_controller is FaceHuggerController faceHugger)
                    {
                        faceHugger.AttachToFace(physicsHandler.GetComponent<PlayerController>());
                    }
                }
            }
        }
    }

    public override void Enter()
    {
        base.Enter();
        
    }

    public override void Exit()
    {
        base.Exit();

    }

    public override void StateUpdate()
    {
        base.StateUpdate();

    }

    public override void StatePhysicsUpdate()
    {
        base.StatePhysicsUpdate();

    }
}
