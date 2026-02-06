
using UnityEngine;
using Mirror;
/// <summary>
/// 실제로 작동하는 모듈
/// [박상민 - 2025.06.18]
/// </summary>
public class Module : NetworkBehaviour, IDamageable, IInteractable
{
    [Header("<color=yellow>모듈 타입")]
    [SerializeField] protected ModuleType _moduleType;
    public ModuleType ModuleType { get { return _moduleType; } }
    [Header("<color=green>모듈 상태")]
    [Tooltip("모듈을 사용중인 플레이어")]
    protected PlayerModuleController _playerModuleController = null;
    public bool IsOccupied => _playerModuleController != null;
    [Tooltip("모듈 제어판 오브젝트")]
    [SerializeField] protected ModuleController _moduleController = null;
    [SerializeField] protected ModuleBoxObject _moduleBoxObject = null;

    [Header("배 참조")]
    [Tooltip("모듈이 장착된 배의 참조")]
    protected ShipController _shipController;
    [Tooltip("원격 배의 참조")]
    protected GameObject _agentShip;

    [Header("모듈 파괴 설정")]
    [Tooltip("모듈이 파괴될 때 튕겨나가는 힘")]
    private float _knockbackForce = 25f;
    // 유저 입력 관련 변수
    protected bool _isAbility1Activated = false;
    protected bool _isAbility2Activated = false;
    protected Vector2 _rotationInput = Vector2.zero;
    protected Vector2 _aimInput = Vector2.zero;

    //프로퍼티
    public PlayerModuleController PlayerModuleController => _playerModuleController;
    public ModuleController ModuleController => _moduleController;

    #region  IInteractable 구현
    public string InteractionText => "앉기";
    public string ActionName => "Player/Interact";
    public Vector3 GetInteractionUIPosition()
    {
        return transform.position + Vector3.up * 1f;
    }
    public bool IsInteractable => !IsOccupied;

    public virtual InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
            return new InteractionInfo();
        }

        string text = "앉기";
        string action = "Player/Interact";
        bool interactable = !IsOccupied;
        Vector3 position = transform.position + Vector3.up * 1f;

        // 모듈 컨트롤러가 있으면 그 위치 사용
        if (_moduleController != null)
        {
            position = _moduleController.InteractionPosition.position + Vector3.up * 0.5f;
        }
        return new InteractionInfo(netId, text, action, interactable, position);
    }

    #endregion

    protected virtual void Awake()
    {
        //_shipController = TempShipSpawner.Instance.CurrentShip.GetComponent<ShipController>();
        //_agentShip = TempShipSpawner.Instance.AgentShip;
    }

    protected virtual void Start()
    {
        if (!isServer)
        {
            return;
        }

        _shipController = ShipSpawner.Instance.CurrentShip.GetComponent<ShipController>();
        _agentShip = ShipSpawner.Instance.AgentShip;
    }

    /// <summary>
    /// 모듈이 사용되었을 때 호출되는 함수 - 서버에서 사용중인 플레이어를 설정해줌
    /// </summary>
    public virtual void PlayerEntered(PlayerModuleController playerModuleController)
    {
        if (!isServer)
        {
            return;
        }
        if (IsOccupied)
        {
            Debug.LogWarning("모듈이 이미 사용 중입니다.");
            return;
        }
        _playerModuleController = playerModuleController;

        // 상호작용 위치 off
        if (_moduleController != null)
        {
            _moduleController.ToggleInteraction(false);
        }
    }

    /// <summary>
    /// 모듈이 사용 중인 플레이어가 모듈을 놓았을 때 호출되는 함수 - 서버에서 사용중인 플레이어를 해제해줌
    /// </summary>
    public virtual void PlayerExited()
    {
        if (!isServer)
        {
            return;
        }
        if (!IsOccupied)
        {
            Debug.LogWarning("모듈이 사용 중이지 않습니다.");
            return;
        }
        _playerModuleController = null;

        // 상호작용 위치 on
        if (_moduleController != null)
        {
            _moduleController.ToggleInteraction(true);
        }
    }

    /// <summary>
    /// 모듈이 파괴되었을 때 호출되는 함수
    /// [박상민 - 2025.09.19]
    /// </summary>
    /// <param name="vector2">데미지가 들어온 방향 혹은 모듈의 아래방향</param>
    public virtual void ModuleDamage(Vector2 vector2)
    {
        if (!isServer)
        {
            return;
        }
        // 튕겨나가는 방향을 vector2 값 +- 30도로 랜덤하게 조정 (모듈박스용, 플레이어용 2번 선정)
        Vector2 playerRandomDirection = Quaternion.Euler(0, 0, Random.Range(-30f, 30f)) * vector2;
        Vector2 randomDirection = Quaternion.Euler(0, 0, Random.Range(-30f, 30f)) * vector2;

        // 플레이어가 있다면 모듈에서 내리게하고 기절시킨후 튕겨냄
        if (IsOccupied)
        {
            _playerModuleController.CallForceExitModule(playerRandomDirection * _knockbackForce);
        }
        // 모듈을 설치해제하고 박스채로 튕겨나가게 함
        _moduleBoxObject?.KnockOutInstalledModule(randomDirection * _knockbackForce); // 모듈 박스 오브젝트에 분리 요청
    }

    /// <summary>
    /// 모듈 제어판을 오브젝트를 설정
    /// </summary>
    [Server]
    public void SetModuleInteractSpot(ModuleController moduleController)
    {
        if (_moduleController != null)
        {
            Debug.LogWarning("이미 모듈 제어판이 설정되어 있습니다.");
            return;
        }
        _moduleController = moduleController;
    }

    /// <summary>
    /// 모듈 박스 오브젝트를 설정
    /// </summary>
    [Server]
    public void SetModuleBoxObject(ModuleBoxObject moduleBoxObject)
    {
        if (_moduleBoxObject != null)
        {
            Debug.LogWarning("이미 모듈 박스 오브젝트가 설정되어 있습니다.");
            return;
        }
        _moduleBoxObject = moduleBoxObject;
    }

    //IDamageable 인터페이스 구현
    public virtual void TakeDamage(float damage, Vector2 hitPoint)
    {
        if (!isServer)
        {
            return;
        }
        ModuleDamage(-transform.up); // 데미지가 들어온 방향이 없으므로 모듈의 아래방향으로 설정
    }

    public void DestroySelf()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    [ContextMenu("Test Module Damage")]
    public void TestModuleDamage()
    {
        if (!isServer)
        {
            return;
        }
        ModuleDamage(-transform.up); // 데미지가 들어온 방향이 없으므로 모듈의 아래방향으로 설정
    }

    #region 플레이어 인풋 반영
    public virtual void Rotate(Vector2 direction)
    {
        _rotationInput = direction;
    }

    public virtual void Aim(Vector2 direction)
    {
        _aimInput = direction;
    }

    public virtual void ActivateAbility1()
    {
        _isAbility1Activated = true;
    }

    public virtual void DeactivateAbility1()
    {
        _isAbility1Activated = false;
    }

    public virtual void ActivateAbility2()
    {
        _isAbility2Activated = true;
    }

    public virtual void DeactivateAbility2()
    {
        _isAbility2Activated = false;
    }
    #endregion
}