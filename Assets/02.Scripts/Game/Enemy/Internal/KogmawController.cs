using DG.Tweening;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코그모 전용 InternalEnemyController
/// [조우석 - 2025.10.23]
/// </summary>
public class KogmawController : InternalEnemyController
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("Explosion")]
    [Tooltip("폭파 대기 시간")]
    [SerializeField] private float _explodeTime = 10f;
    [SerializeField] private float _explodeDamage = 25;
    [SerializeField] private GameObject _explosionPrefab;

    private float _timer = 0;
    private bool _isCountingDown = false;

    private Color _originalColor;
    private Color _targetColor = Color.red;
    private Tween _colorTween;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
            _colorTween = _spriteRenderer.DOColor(_targetColor, _explodeTime);
        }
    }

    private void OnDisable()
    {
        _colorTween?.Kill();
    }


    protected override void Update()
    {
        base.Update();

        if (!isServer || !_isCountingDown)
        {
            return;
        }

        // 타이머 종료 시 폭파하며 배를 공격
        _timer -= Time.deltaTime;
        if (_timer < 0)
        {
            Explode();
        }
    }

    #region 네트워크
    /// <summary>
    /// SERVER : 자폭 카운트다운 시작
    /// </summary>
    [Server]
    public void StartExplosionCountdown()
    {
        if (_isCountingDown)
        {
            return;
        }

        _isCountingDown = true;
        _timer = _explodeTime;

        RpcStartColorTween();
    }

    /// <summary>
    /// SERVER : 자폭 카운트다운 정지
    /// </summary>
    [Server]
    public void CancelExplosionCountdown()
    {
        if (!_isCountingDown)
        {
            return;
        }

        _isCountingDown = false;
        RpcCancelColorTween();
    }

    /// <summary>
    /// SERVER -> CLIENT : 카운트다운 시각 효과 재생
    /// </summary>
    [ClientRpc]
    private void RpcStartColorTween()
    {
        if (_spriteRenderer != null)
        {
            // 기존 트윈이 있다면 제거 후 새로 시작
            _colorTween?.Kill();
            _spriteRenderer.color = _originalColor;
            _colorTween = _spriteRenderer.DOColor(_targetColor, _explodeTime).SetEase(Ease.InQuad);
        }
    }

    /// <summary>
    /// SERVER -> CLIENT : 카운트다운 시각 효과 정지
    /// </summary>
    [ClientRpc]
    private void RpcCancelColorTween()
    {
        _colorTween?.Kill();
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
    }
    #endregion

    [Server]
    public override void ApplyDead()
    {
        // 사망하며 폭파 타이머를 정지
        CancelExplosionCountdown();
        base.ApplyDead();
    }

    /// <summary>
    /// 호출 시 배에 피해를 입히며 플레이어 캐릭터에 피해를 입히는 폭발 이펙트 스폰
    /// </summary>
    private void Explode()
    {
        _isCountingDown = false;

        PlayerShipController?.TakeDamage(_explodeDamage, transform.position - PlayerShipController.transform.position);

        // 폭발 이펙트 스폰
        if (_explosionPrefab != null)
        {
            NetworkObjectSpawner.Instance.RequestSpawnObjectAtPosition(_explosionPrefab, transform.position, transform.rotation);
        }

        ApplyDead();
    }
}
