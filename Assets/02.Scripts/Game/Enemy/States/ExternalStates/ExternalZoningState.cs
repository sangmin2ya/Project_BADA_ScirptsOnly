using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 항상 타겟으로부터 일정 거리를 유지하는 MoveState
/// [조우석 - 2025.08.01.]
/// </summary>
public class ExternalZoningState : ExternalMoveState
{
    [Header("Engagement Behavior")]
    [Tooltip("타겟과 유지하려는 최적 거리")]
    [SerializeField] private float _zoningDistance = 10f;
    [Tooltip("최적 거리에서 허용되는 오차 범위. 이 범위 내에서는 추력을 사용하지 않음")]
    [SerializeField] private float _distanceDeadZone = 1f;
    [Tooltip("이 거리 오차 이상부터 최대 추력을 사용. 값이 작을수록 빠르게 최대 속도에 도달")]
    [SerializeField] private float _maxThrustErrorDistance = 20f;

    [Header("Physics Submarine Controls")]
    [Tooltip("전/후진 시 가해지는 추력의 크기")]
    [SerializeField] private float _thrustForce = 20f;
    [Tooltip("조향을 위해 가해지는 회전력의 크기")]
    [SerializeField] private float _turnTorque = 1f;
    [Tooltip("회전 시 목표 각도에 부드럽게 도달하기 위한 감쇠 계수")]
    [SerializeField] private float _turnDamping = 1f;

    private float _targetAngle;
    private float _angleErr;

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

        // 1. 방향 및 거리 계산
        Vector2 myPosition = transform.position;
        Vector2 targetPosition = _controller.CurrentTarget.position;
        Vector2 directionToTarget = (targetPosition - myPosition).normalized;
        float distanceToTarget = Vector2.Distance(myPosition, targetPosition);

        // 2. 조향(Torque), 추력(Thrust) 계산
        float correctiveTorque = GetSteeringTorque(directionToTarget);
        float correctiveThrust = GetThrust(distanceToTarget);

        // 3. 회피 확인 후 조향, 추력 적용
        (float finalTorque, float finalThrust) = GetDodgeTorqueAndThrust(correctiveTorque, correctiveThrust);
        _rigidbody.AddTorque(finalTorque);
        _rigidbody.AddForce(transform.right * finalThrust);
    }

    /// <summary>
    /// 타겟 방향으로 잠수함의 정면(transform.right)을 돌리기 위해 회전력을 적용
    /// </summary>
    /// <param name="desiredDirection">목표로 하는 방향 벡터</param>
    private float GetSteeringTorque(Vector2 desiredDirection)
    {
        // 목표 방향 벡터를 각도(degree)로 변환합니다.
        // Atan2는 x, y 컴포넌트로 정확한 각도를 계산해줍니다.
        float targetAngle = Mathf.Atan2(desiredDirection.y, desiredDirection.x) * Mathf.Rad2Deg;
        _targetAngle = targetAngle;

        // 현재 각도와 목표 각도 사이의 가장 짧은 각도 차이를 계산합니다. (-180 ~ 180)
        float angleError = Mathf.DeltaAngle(_rigidbody.rotation, targetAngle);
        _angleErr = angleError;

        // PD 컨트롤러(비례-미분 제어)를 사용하여 부드러운 회전을 구현합니다.
        // 1. (angleError * _turnTorque): 목표 각도와의 차이가 클수록 강한 회전력을 가합니다. (비례)
        // 2. (_rigidbody.angularVelocity * _turnDamping): 현재 회전 속도에 반대되는 힘을 가해 목표에 가까워질수록 감속시킵니다. (미분/감쇠)
        float correctiveTorque = (angleError * _turnTorque) - (_rigidbody.angularVelocity * _turnDamping);

        return correctiveTorque;
    }

    /// <summary>
    /// 타겟과의 거리에 비례하여 전진 또는 후진 추력을 부드럽게 적용
    /// </summary>
    /// <param name="distance">타겟까지의 현재 거리</param>
    private float GetThrust(float distance)
    {
        float distanceError = distance - _zoningDistance;  // 목표 거리와 차이

        // 오차가 매우 작으면 힘을 가하지 않고 정지
        if (Mathf.Abs(distanceError) < _distanceDeadZone)
        {
            return 0;
        }

        // 데드존을 벗어난 오차를 기준으로 0~1 사이의 값으로 정규화합니다.
        // 오차가 데드존 경계에 있으면 0, _maxThrustErrorDistance 이상이면 1
        float errorMagnitude = Mathf.Abs(distanceError) - _distanceDeadZone;
        float maxError = _maxThrustErrorDistance - _distanceDeadZone;

        // maxError가 0보다 작거나 같은 경우 방지
        float thrustRatio = (maxError <= 0) ? 1f : Mathf.Clamp01(errorMagnitude / maxError);

        // 전/후진 결정
        // distanceError가 양수이면 너무 먼 것이므로 전진, 음수이면 너무 가까운 것이므로 후진
        float direction = Mathf.Sign(distanceError);

        // 추력 계산
        float thrust = _thrustForce * thrustRatio * direction;
        return thrust;
    }
}
