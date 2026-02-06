using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using static Mirror.BouncyCastle.Math.EC.ECCurve;

/// <summary>
/// 적 함선 도망 시 동작하는 상태
/// [조우석 - 2025.07.10.]
/// </summary>
public class ExternalFleeState : ExternalMoveState
{
    [Header("Physics Ship Controls")]
    [Tooltip("전진(측면) 추력")]
    [SerializeField] private float _forwardThrust = 50f;
    [Tooltip("선회 토크(회전력)")]
    [SerializeField] private float _turnTorque = 100f;
    [Tooltip("선회 시 발생하는 진동을 줄여주는 감쇠 계수")]
    [SerializeField] private float _turnDamping = 10f;

    [Header("Flee Behavior")]
    [Tooltip("도주 시 적과 이 정도 거리 이상 벌어지면 도주 종료")]
    public float _fleeDistance = 20f;

    public override void StateUpdate()
    {
        base.StateUpdate();
    }

    public override void StatePhysicsUpdate()
    {
        if (_controller.CurrentTarget == null || _rigidbody == null)
        {
            return;
        }

        // 1. 목표: 도주 방향 결정 (타겟에서 멀어지는 방향)
        Vector2 enemyPos = transform.position;
        Vector2 playerPos = _controller.CurrentTarget.position;
        Vector2 desiredMoveDirection = (enemyPos - playerPos).normalized;

        if (desiredMoveDirection == Vector2.zero)
        {
            desiredMoveDirection = transform.up; // 위치가 완전히 겹친 경우의 예외 처리
        }

        // 2. 조타: 배를 도주 방향과 수직으로 회전시켜 측면으로 도주
        Vector2 desiredNoseDirection = new Vector2(-desiredMoveDirection.y, desiredMoveDirection.x);
        float targetAngle = Mathf.Atan2(desiredNoseDirection.y, desiredNoseDirection.x) * Mathf.Rad2Deg - 90f;

        float angleError = Mathf.DeltaAngle(_rigidbody.rotation, targetAngle);
        float correctiveTorque = (angleError * CalculateAdjustedSpeed(_turnTorque)) - (_rigidbody.angularVelocity * _turnDamping);
        (float finalTorque, float finalThrust) = GetDodgeTorqueAndThrust(correctiveTorque, _forwardThrust);
        _rigidbody.AddTorque(finalTorque);

        // 3. 추진: 함선의 측면(transform.right)을 따라 일정한 추력 가함
        // 도망 중에는 단순히 멀어지기만 하면 되므로 복잡한 회전/후진 로직은 불필요
        _rigidbody.AddForce((Vector2)transform.right * CalculateAdjustedSpeed(finalThrust));
    }
}
