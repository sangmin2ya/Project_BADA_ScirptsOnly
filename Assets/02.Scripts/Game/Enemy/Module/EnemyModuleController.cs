using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 모듈의 동작을 관리
/// [조우석 - 2025.07.11.]
/// </summary>
public class EnemyModuleController : NetworkBehaviour, IDamageable
{
    [Header("모듈 스텟 설정")]
    [Tooltip("최대 체력")]
    [SerializeField] private float _maxHealth;
    [SyncVar] private float _currentHealth;

    protected EnemyModuleHolder _moduleHolder;
    protected int _index = 0;
    protected ExternalEnemyController _enemyController;
    protected float MaxHealth => _maxHealth;

    protected virtual void Start()
    {
        if (isServer)
        {
            _currentHealth = _maxHealth;
        }
    }

    /// <summary>
    /// 데이터 초기화
    /// </summary>
    /// <param name="holder">모듈 관장 holder</param>
    /// <param name="index">현재 모듈의 index</param>
    public virtual void Initialize(EnemyModuleHolder holder, int index)
    {
        _moduleHolder = holder;
        _index = index;
    }

    /// <summary>
    /// 데이터 초기화
    /// </summary>
    /// <param name="holder">모듈 관장 holder</param>
    /// <param name="index">현재 모듈의 index</param>
    /// <param name="controller">적 함선의 컨트롤러</param>
    public void Initialize(EnemyModuleHolder holder, int index, ExternalEnemyController controller)
    {
        _moduleHolder = holder;
        _index = index;
        _enemyController = controller;
    }

    #region IDamageable 구현

    /// <summary>
    /// 모듈이 파괴될 시 서버에서 동작하는 메서드
    /// </summary>
    [Server]
    public virtual void SetModuleDestroy() { }

    /// <summary>
    /// 받은 데미지에 대해서 동작을 서버에 요청
    /// </summary>
    /// <param name="damage"></param>
    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        _currentHealth -= damage;
        if (_currentHealth <= 0)
        {
            SetModuleDestroy();
        }
    }

    public void DestroySelf()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    #endregion
}
