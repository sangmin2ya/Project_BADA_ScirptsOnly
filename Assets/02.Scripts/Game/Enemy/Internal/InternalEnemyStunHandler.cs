using Mirror;
using UnityEngine;

/// <summary>
/// 내부 적의 스턴/기절 관리 클래스
/// [조우석 - 2025.08.12.]
/// [수정 - 2025.11.13] 타이머 기반 스턴 시스템으로 변경
/// </summary>
public class InternalEnemyStunHandler : NetworkBehaviour
{
    [Header("스턴 설정")]
    [Tooltip("현재 스턴 상태")]
    private InternalEnemyStunState _stunState = InternalEnemyStunState.기본;
    [SerializeField] private LTDObject _enemyLTDObject;
    private string _enemyTag;

    [Header("스턴 타이머")]
    [Tooltip("현재 스턴 지속 시간")]
    private float _currentStunDuration = 0f;
    [Tooltip("스턴 타이머가 흐르는가")]
    private bool _isStunTimerPaused = false;

    public bool IsStun => _stunState == InternalEnemyStunState.스턴;
    public bool IsDead => _stunState == InternalEnemyStunState.사망;
    public bool IsStunTimerPaused => _isStunTimerPaused;

    private InternalEnemyController _controller;

    private void Awake()
    {
        if (!TryGetComponent<InternalEnemyController>(out _controller))
        {
            Debug.LogError($"내부 적 오브젝트 {gameObject.name}에 InternalEnemyController 컴포넌트를 찾을 수 없습니다.");
        }
        _enemyTag = gameObject.tag;
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        // 스턴 타이머 업데이트
        if (IsStun && !_isStunTimerPaused)
        {
            _currentStunDuration -= Time.deltaTime;

            if (_currentStunDuration <= 0)
            {
                // 스턴 시간 종료 - 기본 상태로 복귀
                RecoverFromStun();
            }
        }
    }

    /// <summary>
    /// 스턴 상태를 변경하는 메서드
    /// </summary>
    /// <param name="newState"></param>
    private void ChangeStunState(InternalEnemyStunState newState)
    {
        switch (newState)
        {
            case InternalEnemyStunState.기본:
                _controller.ApplyRecoverStun();
                ToggleLiftable(false);
                _controller.PhysicsHandler.SetIsLTDObject(false);
                SetTimerPause(true);
                break;
            case InternalEnemyStunState.스턴:
                _controller.ApplyStun();
                ToggleLiftable(true);
                _controller.PhysicsHandler.SetIsLTDObject(true);
                SetTimerPause(false);
                break;
            case InternalEnemyStunState.사망:
                _controller.ApplyDead();
                ToggleLiftable(false);
                _controller.PhysicsHandler.SetIsLTDObject(false);
                SetTimerPause(true);
                break;
        }
        _stunState = newState;
    }

    /// <summary>
    /// 스턴을 적용하는 메서드 (매 공격마다 호출)
    /// - 외부 참조용 메서드
    /// </summary>
    /// <param name="duration">스턴 지속 시간 (초)</param>
    [Server]
    public void ApplyStun(float duration)
    {
        if (IsDead)
        {
            return;
        }

        // 이미 스턴 상태면 타이머만 리셋
        if (IsStun)
        {
            _currentStunDuration = duration;
            Debug.Log($"<color=yellow>내부 적 {gameObject.name} 스턴 타이머 리셋 - {duration}초</color>");
        }
        else
        {
            // 스턴 상태로 전환
            _currentStunDuration = duration;
            ChangeStunState(InternalEnemyStunState.스턴);
            Debug.Log($"<color=yellow>내부 적 {gameObject.name} 스턴 적용 - {duration}초</color>");
        }
    }

    /// <summary>
    /// 사망 처리 (InternalEnemyHealthController에서 호출)
    /// </summary>
    [Server]
    public void OnEnemyDeath()
    {
        ChangeStunState(InternalEnemyStunState.사망);
    }

    /// <summary>
    /// 타격을 받았을 때 스턴 처리 (하위 호환성 - 기존 시스템용)
    /// </summary>
    [Server]
    public void TakeDamage()
    {
        // 기본 스턴 시간 0.3초 적용
        ApplyStun(2f);
    }

    /// <summary>
    /// 스턴으로부터 회복을 명령
    /// - 외부 참조용 메서드
    /// </summary>
    [Server]
    public void RecoverFromStun()
    {
        if (IsStun)
        {
            ChangeStunState(InternalEnemyStunState.기본);
        }
    }

    /// <summary>
    /// 스턴 타이머를 정지
    /// - 외부 참조용 메서드
    /// </summary>
    /// <param name="willPause">true 시 타이머 정지</param>
    public void SetTimerPause(bool willPause)
    {
        _isStunTimerPaused = willPause;
    }

    /// <summary>
    /// 스턴 시 Liftable 여부를 지정
    /// </summary>
    /// <param name="isLiftable"></param>
    [Server]
    public void ToggleLiftable(bool isLiftable)
    {
        _enemyLTDObject.enabled = isLiftable;
        gameObject.tag = isLiftable ? "LTDObject" : _enemyTag;
    }
}
