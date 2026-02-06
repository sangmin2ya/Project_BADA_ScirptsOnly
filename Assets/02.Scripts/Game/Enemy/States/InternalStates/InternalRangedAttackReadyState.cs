using DG.Tweening;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 중거리에서 원거리 투사체 공격을 수행하는 상태.
/// 거리 유지, 공격 쿨다운 관리, 투사체 발사를 담당.
/// [조우석 - 2025.08.09]
/// </summary>
public class InternalRangedAttackReadyState : InternalAttackState, ITimeTrackableState, IDoColorState
{
    [Header("공격 설정")]
    [SerializeField] private Transform _handRotationPivot;
    [Tooltip("타겟의 몇 초 후 미래 위치를 향해서 공격할 것인가")]
    [SerializeField] private float _predictionTime = .5f;
    [SerializeField] private LayerMask _wallLayer;
    [Tooltip("에임이 타겟을 얼마나 잘 추적할 것인가")]
    [SerializeField] private float _aimLerpFactor = 1;

    [SerializeField] private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private Color _targetColor = Color.red;
    private Tween _colorTween;

    private float _timer = 0;

    public float Timer => _timer;                   // ITimeTrackableState 구현
    public Color OriginalColor => _originalColor;   // IDoColorState 구현
    public Color TargetColor => _targetColor;       // IDoColorState 구현
    public Tween ColorTween => _colorTween;         // IDoColorState 구현
    public float TweenTime => _predictionTime;      // IDoColorState 구현

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }
    }

    public override void Enter()
    {
        base.Enter();
        Debug.Log($"[{_internalController.gameObject.name}] Entering Ranged Attack State.");

        if (_controller.HasTarget)
        {
            _internalController.TargetPos = (Vector2)_controller.CurrentTarget.position + _internalController.TargetRigidbody.velocity * _predictionTime;
            _internalController.AimDirection = (_internalController.TargetPos - (Vector2)transform.position).normalized;
        }
        _timer = 0;
        _controller.Rigidbody.velocity = Vector2.zero;
        _controller.Rigidbody.totalForce = Vector2.zero;

        _internalController.AnimationController?.SetAttacking(true);
        _internalController.AnimationController?.TriggerAttackIntro();
        _internalController.AnimationController?.SetWalk(false);

        RPCStartDoColor();
    }

    public override void Exit()
    {
        base.Exit();

        RPCStopDoColor();
    }

    public override void StateUpdate()
    {
        base.StateUpdate();

        CheckTargetPos();

        // 타겟을 향해 Raycast를 쏴서 공격이 가능한 위치가 아니라면 공격을 시도하지 않음
        if (!CheckValidTarget(transform.position, _internalController.TargetPos, _wallLayer))
        {
            return;
        }

        _timer += Time.deltaTime;
    }

    public override void StatePhysicsUpdate()
    {
        // 공격 중에는 이동을 멈춤
        //_controller.Rigidbody.velocity = Vector2.zero;
    }

    /// <summary>
    /// 타겟의 위치를 추적
    /// </summary>
    private void CheckTargetPos()
    {
        _internalController.TargetPos = (Vector2)_controller.CurrentTarget.position + _internalController.TargetRigidbody.velocity * _predictionTime;
        Vector2 targetDirection = (_internalController.TargetPos - (Vector2)transform.position).normalized;
        _internalController.AimDirection = Vector2.Lerp(_internalController.AimDirection, targetDirection, Time.deltaTime * _aimLerpFactor).normalized;
        _handRotationPivot.right = _internalController.AimDirection;
    }

    /// <summary>
    /// 타겟의 위치로 Raycast를 쏴서 공격을 맞을 수 있는 위치인지 검사.
    /// </summary>
    private bool CheckValidTarget(Vector2 startPoint, Vector2 targetPoint, LayerMask wallLayerMask)
    {
        Vector2 direction = targetPoint - startPoint;
        float distance = direction.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(startPoint, direction.normalized, distance, wallLayerMask);

        if (hit.collider != null)
        {
            // 시각적 디버깅을 위해 Ray를 붉은색으로 그립니다. (벽에 막혔을 때)
            Debug.DrawRay(startPoint, direction.normalized * distance, Color.red);
            return false;
        }

        Debug.DrawRay(startPoint, direction.normalized * distance, Color.green);
        return true;
    }

    public void StartDoColor()
    {
        if (_spriteRenderer != null)
        {
            // 기존 트윈이 있다면 제거 후 새로 시작
            _colorTween?.Kill();
            _spriteRenderer.color = _originalColor;
            _colorTween = _spriteRenderer.DOColor(_targetColor, TweenTime).SetEase(Ease.InQuad);
        }
    }

    public void StopDoColor()
    {
        _colorTween?.Kill();
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
    }

    /// <summary>
    /// SERVER -> CLIENT : 카운트다운 시각 효과 재생
    /// </summary>
    [ClientRpc]
    private void RPCStartDoColor()
    {
        if (!isServer)
        {
            return;
        }

        StartDoColor();
    }

    /// <summary>
    /// SERVER -> CLIENT : 카운트다운 시각 효과 정지
    /// </summary>
    [ClientRpc]
    private void RPCStopDoColor()
    {
        if (!isServer)
        {
            return;
        }

        StopDoColor();
    }
}