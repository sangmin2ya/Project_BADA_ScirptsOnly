using Mirror;
using UnityEngine;

/// <summary>
/// 외부 적의 기본적인 동작을 수행하는 중심 컨트롤러
/// [조우석 - 2025.08.12.]
/// </summary>
public class ExternalEnemyController : EnemyControllerBase, IDamageable, IEnterable
{
    [Header("적 설정 (External)")]
    [SerializeField] private EnemyShipType _enemyShipType;
    [SerializeField] private float _maxHealth;
    [SyncVar] private float _currentHealth;
    [SerializeField] private uint _mass = 50;

    [Tooltip("타겟의 시야 태그")]
    [SerializeField] private string _detectTag = "EnemyDetector";
    [Tooltip("시야 밖에 나갔을때 얼마 후에 디스폰할지")]
    [SerializeField] private float _despawnDelay = 10f;


    [Header("미니맵 아이콘")]
    [SerializeField] private SpriteRenderer _minimapIcon;
    [SerializeField] private Color _minimapDefaultColor = Color.green;
    [SerializeField] private Color _minimapDetectColor = Color.red;

    [Header("컴포넌트 참조 (External)")]
    protected EnemyModuleHolder _moduleHolder;

    //private 필드
    private bool _isOutOfView = false; // 내가 플레이어 미니맵에서 벗어났는지 여부
    private float _despawnTimer = 90f;

    #region 프로퍼티
    public uint Mass => _mass;
    public float EngineDestructionRatio => _moduleHolder.EngineDestructionRatio;
    public bool EngineAllDestroyed => _moduleHolder.EngineDestructionRatio == 1;
    public EnemyShipType EnemyShipType => _enemyShipType;
    #endregion

    protected override void Awake()
    {
        base.Awake(); // 부모의 Awake() 호출
        _moduleHolder = GetComponent<EnemyModuleHolder>();
        _minimapIcon.color = _minimapDefaultColor;
    }

    protected override void Start()
    {
        base.Start(); // 부모의 Start() 호출
        _currentHealth = _maxHealth;
        base.OnTargetChanged += HandleTargetChanged;
    }
    /// <summary>
    /// 디스폰 기능 추가
    /// [박상민] - 2025.08.20
    /// </summary>
    protected override void Update()
    {
        base.Update(); // 부모의 Update() 호출

        if (!isServer)
        {
            return;
        }

        if (_isOutOfView) // 시야밖에서 일정시간 유지되면 삭제
        {
            _despawnTimer -= Time.deltaTime;
            if (_despawnTimer <= 0f)
            {
                NetworkServer.Destroy(gameObject);
            }
        }
        else
        {
            _despawnTimer = _despawnDelay; // 시야에 들어오면 타이머 초기화
        }
    }

    private void OnDestroy()
    {
        base.OnTargetChanged -= HandleTargetChanged;
    }

    /// <summary>
    /// 타겟 변경 시 호출될 메서드
    /// </summary>
    /// <param name="target"></param>
    private void HandleTargetChanged(Transform target)
    {
        // 감지 타겟 변경 시 미니맵 스프라이트 업데이트
        _minimapIcon.color = HasTarget ? _minimapDetectColor : _minimapDefaultColor;
    }

    #region IDamageable & 네트워크
    // IDamageable 구현

    /// <summary>
    /// 데미지를 입었을 때의 처리 메서드
    /// </summary>
    /// <param name="damage"></param>
    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        _currentHealth -= damage;
        if (_currentHealth <= 0)
        {
            SetLogicEnabled(false);
            GetComponent<ShipSinkController>()?.RpcStartSinkingOnDestroy();
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

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(_detectTag))
        {
            // 플레이어 미니맵에 들어갔을 때
            _isOutOfView = false;
            _despawnTimer = _despawnDelay; // 타이머 초기화
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        // if (collision.CompareTag(_detectTag))
        // {
        //     // 플레이어 미니맵에서 벗어났을 때
        //     _isOutOfView = true;
        //     _despawnTimer = _despawnDelay; // 타이머 초기화
        // }
    }

    protected override void DoTargetLossDestroy()
    {
        DestroySelf();
    }
}