using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 내부 적이 랜덤한 방향으로 배회하는 상태
/// - 랜덤한 일정 시간동안 고정된 방향으로 이동
/// [조우석 - 2025.10.17.]
/// </summary>
public class InternalWanderState : InternalMoveState
{
    [Header("Wander")]
    [Tooltip("랜덤 이동 시간 최소값")]
    [SerializeField] private float _minMoveTime = 1;
    [Tooltip("랜덤 이동 시간 최대값")]
    [SerializeField] private float _maxMoveTime = 3;
    [Tooltip("기존 이동 속도에 (0, _moveSpeedOffset)의 랜덤 값 합연산")]
    [SerializeField] private float _moveSpeedOffset = 1;

    [Tooltip("벽 감지용 LayerMask")]
    [SerializeField] private LayerMask _wallLayer;

    private float _timer = 0;
    private float _speedRandomOffset = 0;

    public override void Enter()
    {
        base.Enter();

        // 상태 진입 시 랜덤 방향 지정
        RandomizeValue();

        Debug.Log($"[{_controller.gameObject.name}] Wandering");
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void StateUpdate()
    {
        base.StateUpdate();

        // 일정 시간마다 Randomize
        _timer -= Time.deltaTime;
        if (_timer < 0)
        {
            RandomizeValue();
        }
    }

    public override void StatePhysicsUpdate()
    {
        if (!_controller.HasTarget)
        {
            return;
        }

        _controller.Rigidbody.velocity = _internalController.MoveDirection * (_moveSpeed + _speedRandomOffset);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        int otherLayer = collision.gameObject.layer;

        if (_wallLayer == (_wallLayer | (1 << otherLayer)))
        {
            // 벽과 충돌 시 다른 방향으로 이동
            RandomizeValue();
        }
    }

    /// <summary>
    /// 적이 이동할 랜덤한 방향을 지정
    /// </summary>
    private void SetRandomMoveDirection()
    {
        Vector2 randomDirection = Vector2.right;
        RaycastHit2D hit;

        // 벽이 없는 방향으로 이동
        for (int i = 0; i < 50; i++)
        {
            randomDirection = Random.insideUnitCircle.normalized;
            hit = Physics2D.Raycast(transform.position, randomDirection, 2, _wallLayer);
            if (hit.collider == null)
            {
                break;
            }
        }

        _internalController.MoveDirection = randomDirection;
    }

    /// <summary>
    /// 랜덤한 방향, 이동 속도 offset, 이동 시간을 지정
    /// </summary>
    private void RandomizeValue()
    {
        _timer = Random.Range(_minMoveTime, _maxMoveTime);
        _speedRandomOffset = Random.Range(0, _moveSpeedOffset);
        SetRandomMoveDirection();
    }
}
