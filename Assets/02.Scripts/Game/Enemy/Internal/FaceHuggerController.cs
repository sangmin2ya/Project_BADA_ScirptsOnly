using DG.Tweening;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 페이스허거 전용 InternalEnemyController
/// [조우석 - 2025.11.20.]
/// </summary>
public class FaceHuggerController : InternalEnemyController
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Collider2D _collider;

    [Header("Hugging")]
    [Tooltip("붙은 플레이어")]
    private PlayerController _huggedPlayer = null;
    [Tooltip("감소된 플레이어 시야")]
    [SerializeField] private float _huggedFOVRadius = 3;
    [Tooltip("얼굴에 붙은 상태")]
    [SerializeField] private InternalBaseState _hugState;
    

    private Color _originalColor;
    private Color _targetColor = Color.yellow;

    private bool _isHugging => _huggedPlayer != null;

    public Transform PlayerHead => _huggedPlayer.HeadTransform;

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
        }

        if (_collider == null)
        {
            _collider = GetComponent<Collider2D>();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!isServer)
        {
            return;
        }
    }

    /// <summary>
    /// 플레이어에게 붙어 디버프를 줌
    /// </summary>
    public void AttachToFace(PlayerController player)
    {
        StateMachine.ChangeState(_hugState);

        _spriteRenderer.color = _targetColor;
        _collider.enabled = false;

        _huggedPlayer = player;

        _huggedPlayer.UpdateFOV(_huggedFOVRadius);
        _huggedPlayer.AddMass(.5f);
        _huggedPlayer.OnStunned += HandlePlayerStun;
    }

    [Server]
    private void HandlePlayerStun()
    {
        Debug.Log("Player가 스턴된 것을 감지! 스스로 떨어집니다.");
        DetachFromFace();
    }

    /// <summary>
    /// 플레이어에게서 떨어짐
    /// </summary>
    public void DetachFromFace()
    {
        _spriteRenderer.color = _originalColor;
        _collider.enabled = true;

        _huggedPlayer.ResetFOV();
        _huggedPlayer.AddMass(-.5f);
        _huggedPlayer.OnStunned -= HandlePlayerStun;

        _huggedPlayer = null;

        _stunHandler.TakeDamage();
    }
}
