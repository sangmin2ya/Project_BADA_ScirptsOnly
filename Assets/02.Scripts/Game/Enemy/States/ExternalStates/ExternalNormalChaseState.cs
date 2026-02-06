using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타겟에게 거리를 좁히기만 하는 Move State
/// [조우석 - 2025.07.15.]
/// </summary>
public class ExternalNormalChaseState : ExternalMoveState
{
    [Header("Physics Ship Controls")]
    [Tooltip("전진(측면) 추력")]
    [SerializeField] private float _forwardThrust = 40f;
    [Tooltip("선회 토크(회전력)")]
    [SerializeField] private float _turnTorque = 90f;
    [Tooltip("선회 시 발생하는 진동을 줄여주는 감쇠 계수")]
    [SerializeField] private float _turnDamping = 8f;

    [Header("Chase Behavior")]
    [Tooltip("타겟과의 거리가 해당 값 이하라면 정지")]
    [SerializeField] private float _stopDistance = 15f;
    [Tooltip("타겟과의 거리가 이 값보다 크면 추적. _stopDistance보다 커야 함")]
    [SerializeField] private float _chaseDistance = 22f;

    // 추적, 대기 상태 관리를 위한 서브 상태머신
    private enum ChaseSubState { 추적, 대기 }
    private ChaseSubState _currentSubState;

    public override void Enter()
    {
        base.Enter();

        if (_controller.CurrentTarget == null)
        {
            // 타겟 없이 진입했을 경우의 안전 장치
            _currentSubState = ChaseSubState.대기;
            return;
        }

        // 초기 거리 판단 후 서브 상태 결정
        float distanceToTarget = Vector2.Distance(transform.position, _controller.CurrentTarget.position);
        if (distanceToTarget > _stopDistance)
        {
            _currentSubState = ChaseSubState.추적;
            Debug.Log($"<color=lightblue>[{gameObject.name}/{this.GetType().Name}]</color> 상태 진입. 타겟이 멀리 있음. 초기 모드: 추적.");
        }
        else
        {
            _currentSubState = ChaseSubState.대기;
            Debug.Log($"<color=lightblue>[{gameObject.name}/{this.GetType().Name}]</color> 상태 진입. 타겟이 가까움. 초기 모드: 대기.");
        }
    }

    public override void StateUpdate()
    {
        base.StateUpdate();

        // 내부 서브 상태 판단 루프
        float distanceToTarget = Vector2.Distance(transform.position, _controller.CurrentTarget.position);
        switch (_currentSubState)
        {
            case ChaseSubState.추적:
                // 충분히 가까워졌다면 대기로 전환
                if (distanceToTarget <= _stopDistance)
                {
                    Debug.Log($"<color=green>[{gameObject.name}/{this.GetType().Name}]</color> 타겟이 사정거리 내에 있음. Holding 모드로 전환.");
                    _currentSubState = ChaseSubState.대기;
                }
                break;

            case ChaseSubState.대기:
                // 타겟이 멀어졌다면 다시 접근 시작
                if (distanceToTarget > _chaseDistance)
                {
                    Debug.Log($"<color=orange>[{gameObject.name}/{this.GetType().Name}]</color> 타겟이 사정거리 밖으로 이동함. Approaching 모드로 전환.");
                    _currentSubState = ChaseSubState.추적;
                }
                break;
        }
    }

    public override void StatePhysicsUpdate()
    {
        if (_controller.CurrentTarget == null || _rigidbody == null)
        {
            return;
        }

        // 현재 서브 상태에 따라 물리적 행동 수행
        switch (_currentSubState)
        {
            case ChaseSubState.추적:
                ExecuteApproachMovement();
                break;
            case ChaseSubState.대기:
                ExecuteHoldMovement();
                break;
        }
    }

    private void ExecuteApproachMovement()
    {
        // 1. 목표 방향 계산
        Vector2 desiredMoveDirection = (_controller.CurrentTarget.position - transform.position).normalized;

        // 2. 선회 조절: 브로드사이드 접근을 위해 선체 방향을 회전
        // 선체의 정면(transform.up)은 이동 방향에 대해 수직이 되도록 조정해야 함
        Vector2 desiredNoseDirection = new Vector2(-desiredMoveDirection.y, desiredMoveDirection.x);
        float targetAngle = Mathf.Atan2(desiredNoseDirection.y, desiredNoseDirection.x) * Mathf.Rad2Deg - 90f;

        float angleError = Mathf.DeltaAngle(_rigidbody.rotation, targetAngle);
        float correctiveTorque = (angleError * CalculateAdjustedSpeed(_turnTorque)) - (_rigidbody.angularVelocity * _turnDamping);
        (float finalTorque, float finalThrust) = GetDodgeTorqueAndThrust(correctiveTorque, _forwardThrust);
        _rigidbody.AddTorque(finalTorque);

        // 3. 추진: 선체의 측면(transform.right) 방향으로 힘을 가함
        _rigidbody.AddForce((Vector2)transform.right * CalculateAdjustedSpeed(finalThrust));
    }

    private void ExecuteHoldMovement()
    {
        // 물리 기반 시스템에서는, 선박을 멈추는 가장 좋은 방법은 단순히 힘을 가하지 않는 것임
        // Rigidbody2D의 ‘Linear Drag’가 자연스럽게 선박을 감속시켜 부드럽게 정지시킴
        // 따라서 이 메서드는 의도적으로 아무 작업도 하지 않음
    }
}
