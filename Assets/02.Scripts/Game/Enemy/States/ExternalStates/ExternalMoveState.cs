using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.XR;

/// <summary>
/// 적 이동 상태
/// [조우석 - 2025.07.08.]
/// </summary>
public class ExternalMoveState : ExternalBaseState
{
    [Header("MoveState")]
    [Tooltip("엔진 파괴 시 감속 계수")]
    [SerializeField] [Range(0, 1)] protected float _engineLossSlowdownFactor = 0.2f;
    [Tooltip("각 엔진 파괴마다 감속을 적용할 것인가")]
    [SerializeField] protected bool _slowdownOnEachEngineDestroy = false;

    [Header("Dodge")]
    [Tooltip("장애물을 회피하는가?")]
    [SerializeField] protected bool _isDodgeObstacle = true;
    [Space]
    [Tooltip("가깝다고 인식할 반경")]
    [SerializeField] protected float _closeDodgeRange = 10;
    [Tooltip("전방으로 인식할 시야각. _dodgeAngle보다 많이 작음.")]
    [SerializeField] protected float _forwardAngle = 15;
    [Tooltip("후진 시 기존 Thrust 대비 Thrust Ratio")]
    [SerializeField, Range(0, 1)] protected float _backwardThrustRatio = 0.5f;
    [Space]
    [Tooltip("장애물을 인식할 거리(CircleCast의 Distance)")]
    [SerializeField] protected float _dodgeCircleCastDistance = 40;
    [Tooltip("최소 Thrust. 충돌 지점과의 거리가 가까울수록 해당 Ratio에 가까워짐")]
    [SerializeField, Range(0, 1)] protected float _dodgeMinThrustRatio = 0.5f;
    [Tooltip("장애물 인식 범위 반경")]
    [SerializeField] protected float _dodgeCircleCastRadius = 7;
    [Tooltip("장애물 인식 각도. 해당 각도 이상에 장애물이 감지되면 회피하지 않음")]
    [SerializeField] protected float _dodgeAngle = 60;
    [Tooltip("예측 시간. 각속도 기반 조향 방향 예측")]
    [SerializeField] protected float _predictTime = .5f;
    [Tooltip("장애물 인식 시 Torque 세기 계수")]
    [SerializeField] protected float _dodgeTorque = 10;
    [Tooltip("장애물로 인식할 오브젝트 레이어마스크(일반적으로는 'Obstacle' 레이어)")]
    [SerializeField] protected LayerMask _obstacleLayerMask;
    [Tooltip("현재 어느 방향으로 회피 중인가(-1 : 반시계 방향, 0 : 회피하지 않음 , 1 : 시계 방향)")]
    private int _dodgeDirection = 0;

    // 성능을 위해 Enter 시점에 캐싱
    protected Rigidbody2D _rigidbody;

    public override void Enter()
    {
        base.Enter();

        _rigidbody = _externalController.Rigidbody;
    }

    /// <summary>
    /// 현재 엔진의 상태에 따라서 조정된 힘을 반환하는 메서드
    /// </summary>
    /// <param name="originSpeed"></param>
    /// <returns></returns>
    protected float CalculateAdjustedSpeed(float originSpeed)
    {
        // 각 엔진이 파괴될 때마다 감속하는 경우
        if (_slowdownOnEachEngineDestroy)
        {
            return Mathf.Lerp(originSpeed, originSpeed * _engineLossSlowdownFactor, _externalController.EngineDestructionRatio);
        }
        // 모든 엔진이 파괴되어야 감속되는 경우
        else
        {
            return _externalController.EngineAllDestroyed ? originSpeed * _engineLossSlowdownFactor : originSpeed;
        }
    }

    protected virtual (float torque, float thrust) GetDodgeTorqueAndThrust(float inputTorque, float inputThrust)
    {
        // 장애물을 피하지 않는다면 기존 토크 그대로 반환
        if (!_isDodgeObstacle)
        {
            return (inputTorque, inputThrust);
        }

        // 장애물이 가까운 전방에 있는지 감지 후, 있으면 후진
        RaycastHit2D[] closeHits = Physics2D.CircleCastAll(transform.position, _closeDodgeRange, Vector2.zero, 0, _obstacleLayerMask);
        foreach(RaycastHit2D hit in closeHits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.transform.IsChildOf(this.transform))
            {
                // 나의 일부이므로 무시하고 다음 충돌체로 넘어감
                continue;
            }

            Vector2 detectedDir = (hit.point - (Vector2)transform.position).normalized;
            if (Vector2.Angle(transform.right, detectedDir) > _forwardAngle)
            {
                // 장애물 인식 위치가 전방이 아니라고 판단할 만큼 일정 시야각 밖이라면 회피기동 하지 않음
                continue;
            }

            // 회피 중이 아니었다면 회피 방향을 지정
            if (_dodgeDirection == 0)
            {
                // 장애물의 위치가 castDirection으로부터 어느 방향에 있는지 확인
                float detectedDirCrossZ = Vector3.Cross(transform.right, detectedDir).z;
                _dodgeDirection = (int)-Mathf.Sign(detectedDirCrossZ);
            }

            float dodgeTorque = _dodgeDirection * _dodgeTorque * 0.5f;  // @@@@@@@임시로 후진할 때에는 최대 토크의 절반만 
            float dodgeThrust = inputThrust * -_backwardThrustRatio;

            return (dodgeTorque, dodgeThrust);
        }

        // 배의 각속도를 기준으로 장애물 인식
        Vector2 castDirection = Quaternion.Euler(0, 0, _rigidbody.angularVelocity * _predictTime) * transform.right;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, _dodgeCircleCastRadius, castDirection, _dodgeCircleCastDistance, _obstacleLayerMask);
        
        // 현재 확인해보니 한 Cast 내에 많아도 6개의 콜라이더만 감지되기 때문에 CircleCastAll을 사용해도 괜찮다고 판단
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.transform.IsChildOf(this.transform))
            {
                // 나의 일부이므로 무시하고 다음 충돌체로 넘어감
                continue; 
            }

            Vector2 detectedDir = (hit.point - (Vector2)transform.position).normalized;
            if (Vector2.Angle(castDirection, detectedDir) > _dodgeAngle)
            {
                // 장애물 인식 위치가 일정 시야각 밖이라면 회피기동 하지 않음
                continue;
            }

            // 회피 중이 아니었다면 회피 방향을 지정
            if (_dodgeDirection == 0)
            {
                // 장애물의 위치가 castDirection으로부터 어느 방향에 있는지 확인
                float detectedDirCrossZ = Vector3.Cross(castDirection, detectedDir).z;
                _dodgeDirection = (int)-Mathf.Sign(detectedDirCrossZ);
            }
            
            float dodgeTorque = _dodgeDirection * _dodgeTorque * Mathf.Clamp01((_dodgeCircleCastDistance - hit.distance) / (_dodgeCircleCastDistance + 1));
            float distanceRatio = Mathf.Clamp01(hit.distance / (_dodgeCircleCastDistance + 1));     // 최대 감지 거리 기준 Ratio
            float dodgeThrust = inputThrust * Mathf.Lerp(_dodgeMinThrustRatio, 1, distanceRatio);   // Ratio 기준 Thrust 세기에 대해서 선형 보간 처리

            return (dodgeTorque, dodgeThrust);
        }
        _dodgeDirection = 0;
        return (inputTorque, inputThrust);
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position + Quaternion.Euler(0, 0, _rigidbody != null ? _rigidbody.angularVelocity * _predictTime : 0) * transform.right * _dodgeCircleCastDistance, _dodgeCircleCastRadius);
        Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, 0, _rigidbody != null ? _rigidbody.angularVelocity * _predictTime : 0) * transform.right * _dodgeCircleCastDistance);
    }
}
