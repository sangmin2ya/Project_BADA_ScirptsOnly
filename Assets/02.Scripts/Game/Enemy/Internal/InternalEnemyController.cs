using Mirror;
using UnityEngine;
using System.Collections;

/// <summary>
/// 내부 적의 기본적인 동작을 수행하는 중심 컨트롤러
/// [조우석 - 2025.08.12.]
/// </summary>
public class InternalEnemyController : EnemyControllerBase
{
    [Header("State (Internal)")]
    [Tooltip("스턴 시 상태")]
    [SerializeField] private EnemyBaseState _stunState;
    [Tooltip("사망 시 상태")]
    [SerializeField] private EnemyBaseState _dieState;

    [Header("컴포넌트 참조 (Internal)")]
    protected InternalEnemyStunHandler _stunHandler;
    private InternalEnemyHealthController _healthController;
    private InternalEnemyAnimationController _animationController;
    private InternalEnemyPhysicsHandler _physicsHandler;
    [SerializeField] private Transform _spriteTransform;
    private ShipController _playerShipController;
    private Transform _agentShipTransform;

    public Vector2 MoveDirection;
    public Vector2 AimDirection;
    public Vector2 TargetPos;

    [Header("이펙트")]
    [SerializeField] private GameObject _dieEffect;
    private GameObject _currentDieEffect = null;

    // 프로퍼티는 그대로 유지
    public bool IsStun => _stunHandler.IsStun;
    public bool IsDead => _stunHandler.IsDead;
    public InternalEnemyAnimationController AnimationController => _animationController;
    public InternalEnemyPhysicsHandler PhysicsHandler => _physicsHandler;
    public InternalEnemyHealthController HealthController => _healthController;
    public ShipController PlayerShipController => _playerShipController;

    protected override void Awake()
    {
        base.Awake(); // 부모의 Awake()를 반드시 호출!

        if (!TryGetComponent(out _stunHandler))
            Debug.LogError($"'{gameObject.name}'에 InternalEnemyStunHandler가 없습니다.");
        if (!TryGetComponent(out _healthController))
            Debug.LogError($"'{gameObject.name}'에 InternalEnemyHealthController가 없습니다.");
        if (!TryGetComponent(out _animationController))
            Debug.LogError($"'{gameObject.name}'에 InternalEnemyAnimationController가 없습니다.");
        if (!TryGetComponent(out _physicsHandler))
            Debug.LogError($"'{gameObject.name}'에 InternalEnemyPhysicsHandler가 없습니다.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _agentShipTransform = ShipSpawner.Instance.AgentShip.transform;
        _playerShipController = ShipSpawner.Instance.CurrentShip.GetComponent<ShipController>();
    }

    protected override void Update()
    {
        base.Update();

        if (!isServer)
        {
            return;
        }
    }

    #region 네트워크 (서버 전용 로직)
    /// <summary>
    /// 스턴을 적용
    /// </summary>
    [Server]
    public void ApplyStun()
    {
        _animationController.SetStun(true);
        StateMachine.ChangeState(_stunState);
    }

    [Server]
    public virtual void ApplyDead()
    {
        _animationController.SetStun(false);
        _animationController.TriggerDead();
        StateMachine.ChangeState(_dieState);
    }

    [Server]
    public void ApplyRecoverStun()
    {
        _animationController.SetStun(false);
        StateMachine.ChangeState(_startState);
    }

    #endregion

    #region 파괴 명령
    /// <summary>
    /// Server: 파괴 이펙트 생성과 함께 일정 딜레이 후 서버에서 파괴되도록 요청
    /// </summary>
    /// <param name="delay"></param>
    [Server]
    public void RequestDieAndCleanUp(float delay)
    {
        RpcShowDieEffect();

        StartCoroutine(IE_DestroyAfterDelay(delay));
    }

    /// <summary>
    /// Server -> Client: 모든 클라이언트에서 파괴 효과를 생성하도록 명령 후 상호작용 비활성화
    /// </summary>
    [ClientRpc]
    private void RpcShowDieEffect()
    {
        // 이펙트 프리팹이 할당되어 있을 때만 실행
        if (_dieEffect != null)
        {
            if (transform.parent != null)
            {
                _currentDieEffect = Instantiate(_dieEffect, transform.position, Quaternion.identity, transform.parent);
            }
            else
            {
                _currentDieEffect = Instantiate(_dieEffect, transform.position, Quaternion.identity);
            }
        }

        // 서버에서 삭제 되기 전 다른 오브젝트와 상호작용하지 않도록 설정
        GetComponent<Collider2D>().enabled = false;
    }

    /// <summary>
    /// 일정 시간이 지난 후 소멸되는 코루틴
    /// </summary>
    [Server]
    private IEnumerator IE_DestroyAfterDelay(float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }
        else
        {
            yield return null;
        }

            Debug.Log("fdsa");
        NetworkServer.Destroy(gameObject);
    }
    #endregion
}