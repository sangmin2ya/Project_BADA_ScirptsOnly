using UnityEngine;
using Mirror;
/// <summary>
/// 모듈을 설치 할 수 있는 들고 옮길 수 있는 박스
/// [박상민 - 2025.06.18]
/// </summary>
public class ModuleBoxObject : LTDObject
{
    [Header("모듈")]
    [Tooltip("모듈 설치 쿨타임")]
    [SerializeField] private float _installCooldown = 1f;
    [Tooltip("박스 안에 들어있는 모듈")]
    [SerializeField] protected GameObject _modulePrefab;
    [Tooltip("박스 안에 있는 모듈을 제어하는 제어판(무늬만)")]
    [SerializeField] protected GameObject _moduleControllerPrefab;
    [Header("설치 상태")]
    [Tooltip("설치된 모듈")]
    [SerializeField] protected Module _installedModule;
    [Tooltip("설치된 모듈을 제어하는 컨트롤러")]
    [SerializeField] protected ModuleController _installedModuleController;

    [Tooltip("모듈이 설치된 지점")]
    [SerializeField] private ModulePoint _installPoint;

    // private 변수
    private bool _ableToInstall = true; // 모듈 설치 가능 여부
    private bool _isJustDetached = false; // 모듈이 방금 분리되었는지 여부
    private float _installCooldownTimer = 0f;
    protected bool isInstalled => _installPoint != null;

    // 프로퍼티
    public Module InstalledModule => _installedModule;
    public ModuleController ModuleController => _installedModuleController;
    public ModulePoint InstallPoint => _installPoint;

    protected override void Awake()
    {
        base.Awake();
        _ltdObjectType = LTDObjectType.모듈박스;
    }

    protected override void Update()
    {
        base.Update();
        if (_isJustDetached)
        {
            _installCooldownTimer += Time.deltaTime;
            if (_installCooldownTimer >= _installCooldown)
            {
                _isJustDetached = false; // 쿨타임이 끝나면 초기화
                _installCooldownTimer = 0f;
                _ableToInstall = true; // 설치 가능 상태로 변경
            }
        }
    }

    public override bool Lifted(PlayerLTDController player)
    {
        if (isInstalled)
        {
            DetachModule();
        }
        return base.Lifted(player);
    }

    /// <summary>
    /// [SERVER] 모듈을 설치
    /// - 이후에 클라이언트가 직접 설치해야되는거면 Command로 바꿔야함
    /// - 기존에 커맨드에서 동작하던 로직을 메서드로 분리
    /// [조우석 - 2025.07.31.]
    /// </summary>
    [Server]
    protected virtual Module InstallModule(ModulePoint modulePoint)
    {
        // 서버에서만 실행되도록 보장
        if (!isServer)
        {
            return null;
        }

        if (isInstalled)
        {
            Debug.LogWarning("이미 설치된 모듈입니다.");
            return null;
        }
        if (modulePoint == null)
        {
            Debug.LogError("모듈 포인트를 찾을 수 없습니다.");
            return null;
        }

        _rigidbody.velocity = Vector2.zero; // 속도 초기화
        _rigidbody.totalTorque = 0;

        _installPoint = modulePoint;

        RpcToggleLtdObject(false);
        GetComponent<Collider2D>().isTrigger = true;
        //gameObject.layer = LayerMask.NameToLayer("Ignore Collision");

        if (_modulePrefab != null)
        {
            // 드릴은 일단 하드하게 스폰위치를 변경해주는 것으로 처리한다 (기본 함선이 아니라 agent 함선 기준으로 스폰시킨다.)
            if (_modulePrefab.GetComponent<Drill>() || _modulePrefab.GetComponent<Cannon>() || _modulePrefab.GetComponent<Crane>())
            {
                // 에이전트 함선의 Transform을 가져옴
                Transform shipTransform = ShipSpawner.Instance.CurrentShip.transform;
                Transform agentShipTransform = ShipSpawner.Instance.AgentShip.transform;

                // 현재 함선 기준 상대적 위치 계산
                Vector3 relativePosition = shipTransform.InverseTransformPoint(_installPoint.ExternalModulePivot.position);

                // 에이전트 함선 기준으로 월드 위치 변환
                Vector3 finalPosition = agentShipTransform.TransformPoint(relativePosition);

                // 상대적 회전값 계산
                Quaternion relativeRotation = Quaternion.Inverse(shipTransform.rotation) * _installPoint.ExternalModulePivot.rotation;
                Quaternion finalRotation = agentShipTransform.rotation * relativeRotation;

                // 모듈 생성 (드릴은 에이전트 함선 기준으로 위치와 회전을 조정)
                Module module = Instantiate(
                    _modulePrefab,
                    finalPosition,
                    finalRotation
                ).GetComponent<Module>();

                //네트워크 스폰
                NetworkServer.Spawn(module.gameObject);
                // 부모 함선으로 설정
                module.GetComponent<NetworkParentSync>().SetNetworkParent(agentShipTransform);
                _installedModule = module;
            }
            else
            {
                // 모듈 생성
                Module module = Instantiate(_modulePrefab, _installPoint.ExternalModulePivot.position, _installPoint.ExternalModulePivot.rotation).GetComponent<Module>();
                //네트워크 스폰
                NetworkServer.Spawn(module.gameObject);
                // 부모 함선으로 설정
                module.GetComponent<NetworkParentSync>().SetNetworkParent(ShipSpawner.Instance.CurrentShip.transform);
                _installedModule = module;
            }
        }
        if (_moduleControllerPrefab != null)
        {
            // 모듈 컨트롤러 생성
            ModuleController moduleController = Instantiate(_moduleControllerPrefab, _installPoint.InternalModulePivot.position, _installPoint.InternalModulePivot.rotation).GetComponent<ModuleController>();
            // 네트워크 스폰
            NetworkServer.Spawn(moduleController.gameObject);
            moduleController.GetComponent<NetworkParentSync>().SetNetworkParent(ShipSpawner.Instance.CurrentShip.transform);
            _installedModuleController = moduleController;
        }
        // 서로 연결해주기
        if (_installedModule != null && _installedModuleController != null)
        {
            ModuleController interactSpot = _installedModuleController;
            Module module = _installedModule;

            if (interactSpot != null && module != null)
            {
                interactSpot.SetConnectedModule(_installedModule);
                module.SetModuleInteractSpot(_installedModuleController); // 모듈에 모듈 컨트롤러 설정
                module.SetModuleBoxObject(this); // 모듈에 모듈 박스 오브젝트 설정
                _installPoint.SetModules(_installedModule, _installedModuleController, this); // 모듈 포인트에 모듈과 모듈 컨트롤러 설정
            }
            else
            {
                Debug.LogError("모듈 컨트롤러에 ModuleInteractSpot 컴포넌트가 없습니다.");
            }
        }
        else
        {
            Debug.LogError("모듈 또는 모듈 컨트롤러가 생성되지 않았습니다.");
        }
        return _installedModule;
    }

    /// <summary>
    /// [SERVER] 모듈을 분리
    /// - 기존에 커맨드에서 동작하던 로직을 메서드로 분리
    /// [조우석 - 2025.07.31.]
    /// </summary>
    [Server]
    protected virtual void DetachModule()
    {
        // 서버에서만 실행되도록 보장
        if (!isServer)
        {
            return;
        }

        Module module = InstalledModule;
        ModuleController moduleController = ModuleController;
        ModulePoint point = InstallPoint;

        RpcToggleLtdObject(true);
        GetComponent<Collider2D>().isTrigger = false;
        gameObject.layer = _originalLayer;

        if (module != null)
        {
            NetworkServer.Destroy(module.gameObject);
        }
        if (moduleController != null)
        {
            NetworkServer.Destroy(moduleController.gameObject);
        }

        // SyncVar 초기화를 명시적으로 처리
        _installedModule = null;
        _installedModuleController = null;
        _installPoint = null;

        point?.SetModules(null, null, null);

        // 즉시 설치가 안되도록 쿨타임 적용
        _ableToInstall = false;
        _isJustDetached = true;
        _installCooldownTimer = 0f;
    }

    public void KnockOutInstalledModule(Vector2 force)
    {
        if (!isInstalled || InstalledModule == null)
        {
            return;
        }
        _installPoint.ToggleModulePoint(false); // 모듈 포인트 비활성화
        DetachModule();
        _rigidbody.velocity = force; // 분리될 때 힘을 가함
    }

    public void JustDetachModule()
    {
        if (!isInstalled || InstalledModule == null)
        {
            return;
        }
        DetachModule();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);

        if (!isServer)
        {
            return;
        }

        if (other.CompareTag("ModulePoint"))
        {
            ModulePoint point = other.GetComponent<ModulePoint>();
            if (point != null && !isInstalled && _ableToInstall)
            {
                // 모듈 포인트에 설치 가능한지 확인
                if (point.IsPointDisabled)
                {
                    Debug.Log("이 지점에는 모듈을 설치할 수 없습니다.");
                    return;
                }
                //만약 설치되어있으면 설치해제하고 설치
                if (point.IsInstalled)
                {
                    point.InstalledModuleBox.JustDetachModule();
                }
                // 해당 지점에 이동시키고 모듈 포인트에 설치

                //플레이어가 들고있다면 내려놓는다
                if (IsBeingLifted)
                {
                    _liftingPlayer.CallLiftDrop();
                }
                transform.position = point.transform.position;

                InstallModule(point);
            }
        }
    }
}
