using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using static Mirror.BouncyCastle.Math.EC.ECCurve;

/// <summary>
/// 적 함선의 적극적으로 교전 시 동작하는 상태
/// [조우석 - 2025.07.10.]
/// </summary>
public class ExternalAttackingZoningState : ExternalMoveState
{
    [Header("Zoning Behavior")]
    [Tooltip("타겟을 선회할 때 유지하는 거리")]
    [SerializeField] private float _zoningRadius = 80f;
    [Tooltip("Zoning Radius의 랜덤한 Offset")]
    [SerializeField] private float _zoningRadiusRandomOffset = 0;
    [Tooltip("플레이어 움직임을 얼마나 앞서 예측할지 (초 단위). 1.0이면 1초 후의 위치를 예측")]
    [SerializeField] private float _predictionTimeFactor = 0.2f;

    [Header("Physics Ship Controls")]
    [Tooltip("전방 추진력")]
    public float _forwardThrust = 50f;
    [Tooltip("조향 회전력")]
    public float _turnTorque = 100f;
    [Tooltip("회전력 예측하여 감소시키는 계수. 값이 높을 수록 미리 예측을 하긴 하지만 적절한 값 조절 필요")]
    public float _turnDamping = 10f;

    [Header("Behavior Tuning")]
    [Tooltip("일정 각도가 벌어지면 선회할지 멈출지 결정하는 임계 각도. 목표 방향과 진행장향이 이루는 각도가 이 값보다 크다면 회전 시도")]
    [Range(60, 180)]
    public float _pivotAngleThreshold = 130f;

    public override void Enter()
    {
        base.Enter();

        Debug.Log($"[{gameObject.name}/{this.GetType().Name}] Now Attacking target.");
    }

    public override void StatePhysicsUpdate()
    {
        if (_controller.CurrentTarget == null || _rigidbody == null)
        {
            return;
        }

        Vector2 enemyPos = transform.position;
        Vector2 targetPos = _controller.CurrentTarget.position;

        // (_predictionTimeFactor)초 후 플레이어의 위치 예측
        Vector2 targetVelocity = _externalController.TargetRigidbody != null ? _externalController.TargetRigidbody.velocity : Vector2.zero;
        float timeToPredict = (Vector2.Distance(enemyPos, targetPos) / (_rigidbody.velocity.magnitude + 5f)) * _predictionTimeFactor; // Added 5f to prevent division by zero and stabilize at low speeds
        Vector2 predictedPlayerPos = targetPos + (targetVelocity * timeToPredict);
        Vector2 toOrbitCenter = predictedPlayerPos - enemyPos;
        Vector2 closestPointOnOrbit = predictedPlayerPos - (toOrbitCenter.normalized * (_zoningRadius + Random.Range(-_zoningRadiusRandomOffset, _zoningRadiusRandomOffset)));

        // 공전 방향 결정 (현재 속도 기준)
        Vector2 orbitTangent = new Vector2(-toOrbitCenter.y, toOrbitCenter.x).normalized;
        int orbitDirection = (Vector2.Dot(_rigidbody.velocity, orbitTangent) > 0) ? 1 : -1;

        // 일정 거리 앞의 '리드 포인트' 계산 (속도 기반)
        float lookAheadDistance = _rigidbody.velocity.magnitude * 0.5f;
        Vector2 leadOrbitPoint = closestPointOnOrbit + (orbitTangent * lookAheadDistance * orbitDirection);

        // 리드 포인트 방향 계산
        Vector2 desiredMoveDirection = (leadOrbitPoint - enemyPos).normalized;

        // 선회 - 회전력 적용
        Vector2 desiredNoseDirection = new Vector2(-desiredMoveDirection.y, desiredMoveDirection.x);

        // Calculate the angle our nose needs to be at.
        float targetAngle = Mathf.Atan2(desiredNoseDirection.y, desiredNoseDirection.x) * Mathf.Rad2Deg - 90f;

        // Apply torque to turn our nose towards that perpendicular direction.
        float angleError = Mathf.DeltaAngle(_rigidbody.rotation, targetAngle);
        float correctiveTorque = (angleError * CalculateAdjustedSpeed(_turnTorque)) - (_rigidbody.angularVelocity * _turnDamping);
        var (finalTorque, finalThrust) = GetDodgeTorqueAndThrust(correctiveTorque, _forwardThrust);
        _rigidbody.AddTorque(finalTorque);

        // _pivotAngleThreshold를 통해서 전/후진, 정지를 결정
        // - 일정 각 이하일 경우: 정지한 채로 회전만 함
        float angleToTarget = Vector2.SignedAngle(transform.right, desiredMoveDirection);
        if (Mathf.Abs(angleToTarget) < _pivotAngleThreshold)
        {
            // Apply forward thrust along the side axis (transform.right).
            _rigidbody.AddForce((Vector2)transform.right * CalculateAdjustedSpeed(finalThrust));
        }
        // 회전 중일 때는 아무런 추진을 하지 않고 회전력만 적용
    }
}
