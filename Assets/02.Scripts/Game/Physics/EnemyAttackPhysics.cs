using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackPhysics : PhysicsHandler
{
    [Header("적 공격 오브젝트 물리")]
    [Tooltip("타겟을 향해서 날아갈 것인가")]
    private bool _isTargetting = true;
    [Tooltip("공격 타겟")]
    protected Vector2 _targetPosition;
    [Tooltip("감속 저항값")]
    [SerializeField] protected float _decelerationDrag = 10;
    [Tooltip("정지 강제 저항 값")]
    [SerializeField] protected float _nearStopDrag = 50;
    [Tooltip("감속 적용 감지 거리")]
    [SerializeField] protected float _decelerationDistance = 2;
    [Tooltip("정지 강제 감지 거리")]
    [SerializeField] protected float _nearStopDistance = 1;

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        SetTarget(false, Vector2.zero);
        _rigidbody.drag = _decelerationDrag;
    }

    public void SetTarget(bool isTargetting, Vector2 target)
    {
        _targetPosition = target;
        _isTargetting = isTargetting;
    }

    protected override void SetDrag()
    {
        if (!_isTargetting)
        {
            base.SetDrag();
            return;
        }

        float distance = Vector2.Distance(transform.position, _targetPosition);

        // 거리 조건에 따라 직접 drag 값을 할당
        if (distance < _nearStopDistance)
        {
            // 정지 직전 거리: 가장 높은 저항
            _rigidbody.drag = _nearStopDrag;
        }
        else if (distance < _decelerationDistance)
        {
            // 감속 거리: 중간 저항
            _rigidbody.drag = _decelerationDrag;
        }
        else
        {
            // 기본 비행 거리: 최소 저항
            _rigidbody.drag = _minDrag; // PhysicsHandler에 정의된 기본 저항값
        }
    }
}
