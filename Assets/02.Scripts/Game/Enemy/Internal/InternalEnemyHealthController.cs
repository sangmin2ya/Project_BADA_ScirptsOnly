using Mirror;
using UnityEngine;

/// <summary>
/// 내부 적의 체력 관리 클래스
/// [작성자 - 2025.11.13]
/// </summary>
public class InternalEnemyHealthController : NetworkBehaviour
{
    [Header("체력 설정")]
    [Tooltip("최대 체력")]
    [SerializeField] private float _maxHealth = 10f;
    [Tooltip("현재 체력")]
    private float _currentHealth;
    [Tooltip("피격 이펙트")]
    [SerializeField] private GameObject _hitEffect;
    private GameObject _currentHitEffect = null;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _currentHealth <= 0;

    private InternalEnemyController _controller;

    private void Awake()
    {
        if (!TryGetComponent<InternalEnemyController>(out _controller))
        {
            Debug.LogError($"내부 적 오브젝트 {gameObject.name}에 InternalEnemyController 컴포넌트를 찾을 수 없습니다.");
        }

        // 체력 초기화
        _currentHealth = _maxHealth;
    }

    /// <summary>
    /// 대미지를 받아 체력을 감소시키는 메서드
    /// </summary>
    /// <param name="damage">받을 대미지</param>
    /// <returns>사망 여부</returns>
    [Server]
    public bool TakeDamage(float damage)
    {
        if (IsDead)
        {
            Debug.Log($"내부 적 {gameObject.name}는 이미 사망한 상태입니다.");
            return true;
        }

        // 체력 감소
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(_currentHealth, 0); // 음수 방지

        Debug.Log($"<color=orange>내부 적 {gameObject.name} 피격 - 남은 체력: {_currentHealth}/{_maxHealth}</color>");

        // 체력이 0 이하면 사망 처리
        if (_currentHealth <= 0)
        {
            OnDeath();
            return true;
        }

        RpcShowDieEffect();

        return false;
    }

    /// <summary>
    /// 사망 처리
    /// </summary>
    [Server]
    private void OnDeath()
    {
        Debug.Log($"<color=red>내부 적 {gameObject.name} 사망</color>");

        // InternalEnemyStunHandler에 사망 알림
        if (TryGetComponent<InternalEnemyStunHandler>(out InternalEnemyStunHandler stunHandler))
        {
            stunHandler.OnEnemyDeath();
        }
    }

    /// <summary>
    /// 체력 회복
    /// </summary>
    /// <param name="amount">회복량</param>
    [Server]
    public void Heal(float amount)
    {
        if (IsDead)
        {
            return;
        }

        _currentHealth += amount;
        _currentHealth = Mathf.Min(_currentHealth, _maxHealth); // 최대 체력 초과 방지

        Debug.Log($"<color=green>내부 적 {gameObject.name} 회복 - 현재 체력: {_currentHealth}/{_maxHealth}</color>");
    }

    /// <summary>
    /// 체력 초기화
    /// </summary>
    [Server]
    public void ResetHealth()
    {
        _currentHealth = _maxHealth;
    }

    /// <summary>
    /// Server -> Client: 모든 클라이언트에서 파괴 효과를 생성하도록 명령 후 상호작용 비활성화
    /// </summary>
    [ClientRpc]
    private void RpcShowDieEffect()
    {
        // 이펙트 프리팹이 할당되어 있을 때만 실행
        if (_hitEffect != null)
        {
            if (transform.parent != null)
            {
                _currentHitEffect = Instantiate(_hitEffect, transform.position, Quaternion.identity, transform.parent);
            }
            else
            {
                _currentHitEffect = Instantiate(_hitEffect, transform.position, Quaternion.identity);
            }
        }
    }
}
