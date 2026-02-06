using DG.Tweening;
using Mirror;
using UnityEngine;

/// <summary>
/// 내부 적이 돌진을 준비하는 상태
/// [조우석 - 2025.08.13.]
/// </summary>
public class InternalPreChargeAttackState : InternalAttackState, IDoColorState
{
    [Tooltip("돌진 준비를 끝내는 시간")]
    [SerializeField] private float _preChargeEndTime = 2;
    [Tooltip("돌진 에임 방향을 고정하는 시간")]
    [SerializeField] private float _lockOnTime = 1.5f;
    [Tooltip("타겟의 몇 초 후 미래 위치를 향해서 공격할 것인가")]
    [SerializeField] private float _predictionTime = .5f;
    [Tooltip("에임이 플레이어를 추적하는 값")]
    [SerializeField] private float _aimLerpFactor = 1;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    private bool _hasLockedOn = false;
    private float _timer = 0;

    private Color _originalColor;
    private Color _targetColor = Color.red;
    private Tween _colorTween;

    public Color OriginalColor => _originalColor;   // IDoColorState 구현
    public Color TargetColor => _targetColor;       // IDoColorState 구현
    public Tween ColorTween => _colorTween;         // IDoColorState 구현
    public float TweenTime => _preChargeEndTime;    // IDoColorState 구현

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
        _hasLockedOn = false;
        _internalController.AimDirection = _internalController.MoveDirection;
        _timer = 0;

        _internalController.AnimationController?.TriggerAttackIntro();
        _internalController.AnimationController?.SetAttacking(true);
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
        _timer += Time.deltaTime;
        // 돌진 대기 시간 초과
        if (_timer >= _preChargeEndTime && _hasLockedOn)
        {
            CheckTransitions();
        }
        // 돌진 에임 락온 타이머
        else if (_timer > _lockOnTime && !_hasLockedOn)
        {
            LockOnAim();
        }
        // 에임이 아직 락온되지 않으면 에임 선이 타겟을 노림
        else if (_timer < _lockOnTime && !_hasLockedOn)
        {
            CheckTargetPos();
        }
    }

    /// <summary>
    /// 에임 추적을 정지하는 메서드
    /// </summary>
    private void LockOnAim()
    {
        _hasLockedOn = true;
    }

    /// <summary>
    /// 타겟 위치를 체크하는 메서드
    /// </summary>
    private void CheckTargetPos()
    {
        _internalController.TargetPos = (Vector2)_controller.CurrentTarget.position + _internalController.TargetRigidbody.velocity * _predictionTime;
        Vector2 targetDirection = (_internalController.TargetPos - (Vector2)transform.position).normalized;
        _internalController.AimDirection = Vector2.Lerp(targetDirection, _internalController.AimDirection, Time.deltaTime * _aimLerpFactor);
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
