using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// 실제로 함선을 제어하는 클래스
/// [박상민 - 2025.06.27]
/// </summary>
public class ShipController : NetworkBehaviour, IDamageable, IEnterable
{
    [Header("<color=yellow>함선 기본 모듈")]
    [Tooltip("함선의 창고 위치")]
    [SerializeField] private Transform _storagePosition = null;
    [Tooltip("함선의 창고 모듈")]
    [SerializeField] private GameObject _storagePrefab = null;
    [Tooltip("함선의 엔진/조향 위치")]
    [SerializeField] private Transform _cockPitPosition = null;
    [Tooltip("함선의 엔진/조향 모듈")]
    [SerializeField] private GameObject _cockPitPrefab = null;
    [Tooltip("함선의 포탄 창고 위치")]
    [SerializeField] private Transform _shellStoragePosition = null;
    [Tooltip("함선의 포탄 창고 모듈")]
    [SerializeField] private GameObject _shellStoragePrefab = null;
    [Tooltip("함선의 수리키트 창고 위치")]
    [SerializeField] private Transform _repairKitStoragePosition = null;
    [Tooltip("함선의 수리키트 창고 모듈")]
    [SerializeField] private GameObject _repairKitStoragePrefab = null;

    [Header("<color=yellow>함선 함포")]
    [Tooltip("함선의 함포 위치")]
    [SerializeField] private List<Transform> _cannonPosition = null;
    [Tooltip("함선의 함포 계기판 모듈")]
    [SerializeField] private GameObject _cannonControllerPrefab = null;
    [Tooltip("함선의 함포 모듈")]
    [SerializeField] private GameObject _cannonPrefab = null;

    [Header("<color=yellow>함선 드릴")]
    [Tooltip("함선의 드릴 계기판 위치")]
    [SerializeField] private List<Transform> _drillControllerPosition = null;
    [Tooltip("함선의 드릴 계기판 모듈")]
    [SerializeField] private GameObject _drillControllerPrefab = null;
    [Tooltip("함선의 드릴 모듈")]
    [SerializeField] private GameObject _drillPrefab = null;

    [Header("<color=yellow>함선 크레인")]
    [Tooltip("함선의 크레인 위치")]
    [SerializeField] private Transform _cranePosition = null;
    [Tooltip("함선의 크레인 계기판 모듈")]
    [SerializeField] private GameObject _craneControllerPrefab = null;
    [Tooltip("함선의 크레인 모듈")]
    [SerializeField] private GameObject _cranePrefab = null;

    [Header("<color=yellow>함선 모듈 설치지점")]
    [Tooltip("함선의 모듈 포인트 위치")]
    [SerializeField] private List<Transform> _modulePointPositions = null;
    [Tooltip("함선의 모듈 포인트")]
    [SerializeField] private GameObject _modulePointPrefab = null;

    [Header("<color=yellow>함선 해치(도킹포트용)")]
    [Tooltip("함선의 해치 (1부터 순서대로)")]
    [SerializeField] private List<GameObject> _hatchs = null;

    [Header("<color=yellow>구멍(수리지점)")]
    [Tooltip("큰 구멍이 뚫리는 데미지 임계값")]
    [SerializeField] private float _largeHoleDamageThreshold = 20f;
    [Tooltip("함선의 일반 수리지점 오브젝트")]
    [SerializeField] private GameObject _normalHolePrefab = null;
    [Tooltip("함선의 대형 수리지점 오브젝트")]
    [SerializeField] private GameObject _largeHolePrefab = null;
    [Tooltip("함선의 수리지점 오브젝트를 생성할 부모 오브젝트")]
    [SerializeField] private Transform _holeParent = null;
    [Tooltip("함선내부에 스폰될 적 오브젝트 최댓값")]
    [SerializeField] private uint _maxEnemySpawnCount = 3;

    [Header("함선 설정")]
    [Tooltip("함선의 전체 체력")]
    [SerializeField] private float _maxHealth = 100f;
    [Tooltip("체력바 UI")]
    [SerializeField] private Image _healthBar = null;
    [Tooltip("함선의 무게")]
    [SerializeField] private uint _mass = 100;
    [Tooltip("함선의 기어 당 전진속도")]
    [SerializeField] private float _forwardSpeedPerGear = 1f;
    [Tooltip("함선의 기어 당 후진속도")]
    [SerializeField] private float _backwardSpeedPerGear = 0.5f;
    [Tooltip("가속도")]
    [SerializeField] private float _acceleration = 2f;
    [Tooltip("힘 적용 배율")]
    [SerializeField] private float _forceMultiplier = 10f;
    [Tooltip("회전 토크")]
    [SerializeField] private float _rotationTorque = 100f;
    [Tooltip("속도당 회전 가산치")]
    [SerializeField] private float _angularSpeedMultiplierPerSpeed = 2f;
    [Tooltip("최대 회전토크")]
    [SerializeField] private float _maxAngularTorque = 10f;
    [Tooltip("부스터 사용시 기어")]
    [SerializeField] private int _boostGear = 5;
    [Tooltip("부스터 사용시 지속시간")]
    [SerializeField] private float _boostDuration = 3f;
    [Tooltip("부스터 활성화/비활성화 시 속도 변화 시간")]
    [SerializeField] private float _boostTransitionTime = 1f;

    [Header("함선 상태")]
    [Tooltip("함선의 현재 체력")]
    [SyncVar(hook = nameof(OnHealthChanged))]
    private float _currentHealth;
    [SyncVar] private bool _isDocked = false; // 함선이 도킹 상태인지 여부

    #region Private 필드
    private AgentController _agentController;
    private Rigidbody2D _agentRigidbody2D;
    private int _currentEngineGear = 0;
    private float _currentAngularInput = 0f;
    private float _targetSpeed = 0f;
    [SyncVar] private float _currentSpeed = 0f;
    [SyncVar] private float _currentAngularSpeed = 0f;
    private float _boostTimer = 0f; // 부스터 타이머
    private float _boostTransitionTimer = 0f; // 부스터 전환 타이머
    private bool _isBoostTransitioning = false; // 부스터 전환 중인지 여부
    private float _preBoostSpeed = 0f; // 부스터 사용 전 속도
    private float _boostStartSpeed = 0f; // 부스터 시작 시점의 실제 속도
    [SyncVar] private bool _isBoosting = false; // 부스터 활성화 여부
    private bool _hasInitialized = false;
    private List<Transform> _holePoints = new List<Transform>();
    private uint _currentEnemySpawnCount => GetCurrentSpawnedEnemyCount();
    private List<RepairPoint> _activeRepairPoints = new List<RepairPoint>();
    private List<LTDObject> _storedItems = new List<LTDObject>();
    #endregion

    #region Public 프로퍼티
    public uint Mass => _mass;
    public bool IsDocked => _isDocked;
    public float ForwardSpeedPerGear => _forwardSpeedPerGear;
    public float BackwardSpeedPerGear => _backwardSpeedPerGear;
    public float CurrentSpeed => _currentSpeed;
    public bool IsAbleToSpawnEnemy => _currentEnemySpawnCount < _maxEnemySpawnCount;
    #endregion

    private Vector2 _spawnOffset => ShipSpawner.Instance != null ? ShipSpawner.Instance.OriginOffset : Vector2.zero;

    #region Unity 생명주기 메소드
    private void Awake()
    {
        _currentHealth = _maxHealth;
        _healthBar = GameObject.Find("HealthBar")?.GetComponent<Image>();
        if (_healthBar != null)
        {
            _healthBar.fillAmount = 1f; // 초기 체력바 설정
        }
        GetComponent<SpriteRenderer>().enabled = false; // 시각적으로 함선 오브젝트 숨기기
    }

    //public override void OnStartServer()
    //{
    //    base.OnStartServer();

    //    _agentController = TempShipSpawner.Instance?.AgentShip.GetComponent<AgentController>();
    //    _agentRigidbody2D = TempShipSpawner.Instance?.AgentShip.GetComponent<Rigidbody2D>();

    //    if (!isServer)
    //    {
    //        return;
    //    }

    //    CreateShipModules();
    //    CreateModulePoints();
    //}

    private void Update()
    {
        if (!_hasInitialized)
        {
            return;
        }
        //// ShipAgent와 위치 동기화
        //transform.rotation = _agentController.transform.rotation;
        //transform.position = (Vector2)_agentController.transform.position + _spawnOffset;
    }

    private void FixedUpdate()
    {
        if (!_hasInitialized)
        {
            return;
        }

        // // ShipAgent와 위치 동기화
        // transform.rotation = _agentController.transform.rotation;
        // transform.position = (Vector2)_agentController.transform.position + _spawnOffset;

        if (!isServer || _isDocked)
        {
            return;
        }

        if (_isBoosting || _isBoostTransitioning)
        {
            ApplyEngineBoost();
        }
        else
        {
            ApplyEngineForce();
        }

        ApplyRotationTorque();
    }
    #endregion

    #region 초기화 메서드

    public override void OnStartClient()
    {
        base.OnStartClient();

        Debug.Log($"OnStartClient: 클라이언트에서 함선 초기화를 시작합니다.");

        _agentController = ShipSpawner.Instance?.AgentShip.GetComponent<AgentController>();
        _agentRigidbody2D = ShipSpawner.Instance?.AgentShip.GetComponent<Rigidbody2D>();

        if (_agentController == null)
        {
            Debug.LogWarning("OnStartClient에서 AgentController를 찾지 못했습니다. TempShipSpawner나 AgentShip이 클라이언트에 준비되지 않았을 수 있습니다.");
        }

        _hasInitialized = true;

        // 구멍 생성지점 초기화
        _holePoints = new List<Transform>();
        foreach (Transform child in _holeParent)
        {
            _holePoints.Add(child);
        }
    }

    void Start()
    {
        if (!isServer)
        {
            return;
        }
        // 시작할때 배 내부에 아이템들 스폰
        var storedItemsData = BaseCampManager.Instance.GetStoredItemsAndReset();
        foreach (var itemData in storedItemsData)
        {
            //배 기준 로컬좌표를 월드 좌표로 변환
            Vector2 spawnPos = transform.TransformPoint(itemData.SpawnLocalPosition);
            float rotationZ = transform.eulerAngles.z + itemData.SpawnLocalRotation;
            GameObject ltdObject = Instantiate(ItemManager.Instance.GetItemPrefab(itemData.Item), spawnPos, Quaternion.Euler(0f, 0f, rotationZ));
            NetworkServer.Spawn(ltdObject);
        }
    }

    /// <summary>
    /// SERVER: 배를 생성 이후 시점에 배 내부 데이터를 초기화
    /// [조우석 - 2025.08.16.]
    /// </summary>
    [Server]
    public void InitializeShipOnServer()
    {
        if (!isServer)
        {
            return;
        }

        CreateShipModules();
        CreateModulePoints();

        Debug.Log("SERVER: 모듈 생성 완료");
    }

    /// <summary>
    /// 함선의 기본 모듈 (엔진, 조향) 생성
    /// </summary>
    [Server]
    private void CreateShipModules()
    {
        if (_storagePrefab != null && _storagePosition != null)
        {
            // 엔진 모듈 생성
            //GameObject asdf = Instantiate(_storagePrefab, _storagePosition.position, _storagePosition.rotation);
            //NetworkServer.Spawn(asdf);
            //asdf.GetComponent<NetworkParentSync>().SetNetworkParent(TempShipSpawner.Instance.CurrentShip.transform);
            NetworkObjectSpawner.Instance.RequestSpawnObjectAtPosition(_storagePrefab, _storagePosition.position, _storagePosition.rotation);
        }
        if (_cockPitPrefab != null && _cockPitPosition != null)
        {
            // 조향 모듈 생성
            NetworkObjectSpawner.Instance.RequestSpawnObjectAtPosition(_cockPitPrefab, _cockPitPosition.position, _cockPitPosition.rotation);
        }
        if (_shellStoragePrefab != null && _shellStoragePosition != null)
        {
            // 포탄 창고 모듈 생성
            NetworkObjectSpawner.Instance.RequestSpawnObjectAtPosition(_shellStoragePrefab, _shellStoragePosition.position, _shellStoragePosition.rotation);
        }
        if (_repairKitStoragePrefab != null && _repairKitStoragePosition != null)
        {
            // 수리키트 창고 모듈 생성
            NetworkObjectSpawner.Instance.RequestSpawnObjectAtPosition(_repairKitStoragePrefab, _repairKitStoragePosition.position, _repairKitStoragePosition.rotation);
        }
        if (_cannonPosition != null && _cannonControllerPrefab != null && _cannonPrefab != null)
        {
            foreach (Transform cannonPos in _cannonPosition)
            {
                // 드릴 모듈 생성
                // 에이전트 함선의 Transform을 가져옴
                Transform shipTransform = ShipSpawner.Instance.CurrentShip.transform;
                Transform agentShipTransform = ShipSpawner.Instance.AgentShip.transform;
                Transform externalModulePivot = cannonPos.GetChild(0);

                // 현재 함선 기준 상대적 위치 계산
                Vector3 relativePosition = shipTransform.InverseTransformPoint(externalModulePivot.position);

                // 에이전트 함선 기준으로 월드 위치 변환
                Vector3 finalPosition = agentShipTransform.TransformPoint(relativePosition);

                // 상대적 회전값 계산
                Quaternion relativeRotation = Quaternion.Inverse(shipTransform.rotation) * externalModulePivot.rotation;
                Quaternion finalRotation = agentShipTransform.rotation * relativeRotation;

                // 모듈 생성
                Module module = Instantiate(_cannonPrefab, finalPosition, finalRotation).GetComponent<Module>();
                //네트워크 스폰
                NetworkServer.Spawn(module.gameObject);
                // 부모 함선으로 설정
                module.GetComponent<NetworkParentSync>().SetNetworkParent(agentShipTransform);

                // 모듈 컨트롤러 생성
                ModuleController moduleController = Instantiate(_cannonControllerPrefab, cannonPos.position, cannonPos.rotation).GetComponent<ModuleController>();
                // 네트워크 스폰
                NetworkServer.Spawn(moduleController.gameObject);
                moduleController.GetComponent<NetworkParentSync>().SetNetworkParent(ShipSpawner.Instance.CurrentShip.transform);

                // 서로 연결해주기
                if (module != null && moduleController != null)
                {
                    if (moduleController != null && module != null)
                    {
                        moduleController.SetConnectedModule(module);
                        module.SetModuleInteractSpot(moduleController);
                    }
                    else
                    {
                        Debug.LogError("모듈 컨트롤러에 ModuleInteractSpot 컴포넌트가 없습니다.");
                    }
                }
            }
        }
        if (_drillPrefab != null && _drillControllerPrefab != null && _drillControllerPosition != null)
        {
            foreach (Transform drillPos in _drillControllerPosition)
            {
                // 에이전트 함선의 Transform을 가져옴
                Transform shipTransform = ShipSpawner.Instance.CurrentShip.transform;
                Transform agentShipTransform = ShipSpawner.Instance.AgentShip.transform;
                Transform externalModulePivot = drillPos.GetChild(0);

                // 현재 함선 기준 상대적 위치 계산
                Vector3 relativePosition = shipTransform.InverseTransformPoint(externalModulePivot.position);

                // 에이전트 함선 기준으로 월드 위치 변환
                Vector3 finalPosition = agentShipTransform.TransformPoint(relativePosition);

                // 상대적 회전값 계산
                Quaternion relativeRotation = Quaternion.Inverse(shipTransform.rotation) * externalModulePivot.rotation;
                Quaternion finalRotation = agentShipTransform.rotation * relativeRotation;

                // 모듈 생성
                Module module = Instantiate(_drillPrefab, finalPosition, finalRotation).GetComponent<Module>();

                //네트워크 스폰
                NetworkServer.Spawn(module.gameObject);
                // 부모 함선으로 설정
                module.GetComponent<NetworkParentSync>().SetNetworkParent(agentShipTransform);

                // 모듈 컨트롤러 생성
                ModuleController moduleController = Instantiate(_drillControllerPrefab, drillPos.position, drillPos.rotation).GetComponent<ModuleController>();
                // 네트워크 스폰
                NetworkServer.Spawn(moduleController.gameObject);
                moduleController.GetComponent<NetworkParentSync>().SetNetworkParent(ShipSpawner.Instance.CurrentShip.transform);

                // 서로 연결해주기
                if (module != null && moduleController != null)
                {
                    if (moduleController != null && module != null)
                    {
                        moduleController.SetConnectedModule(module);
                        module.SetModuleInteractSpot(moduleController); // 모듈에 모듈 컨트롤러 설정
                    }
                    else
                    {
                        Debug.LogError("모듈 컨트롤러에 ModuleInteractSpot 컴포넌트가 없습니다.");
                    }
                }
            }
        }
        if (_cranePrefab != null && _cranePosition != null && _craneControllerPrefab != null)
        {
            // 에이전트 함선의 Transform을 가져옴
            Transform shipTransform = ShipSpawner.Instance.CurrentShip.transform;
            Transform agentShipTransform = ShipSpawner.Instance.AgentShip.transform;
            Transform externalModulePivot = _cranePosition.GetChild(0);

            // 현재 함선 기준 상대적 위치 계산
            Vector3 relativePosition = shipTransform.InverseTransformPoint(externalModulePivot.position);

            // 에이전트 함선 기준으로 월드 위치 변환
            Vector3 finalPosition = agentShipTransform.TransformPoint(relativePosition);

            // 상대적 회전값 계산
            Quaternion relativeRotation = Quaternion.Inverse(shipTransform.rotation) * externalModulePivot.rotation;
            Quaternion finalRotation = agentShipTransform.rotation * relativeRotation;
            // 크레인 모듈 생성
            Module module = Instantiate(_cranePrefab, finalPosition, finalRotation).GetComponent<Module>();
            //네트워크 스폰
            NetworkServer.Spawn(module.gameObject);
            // 부모 함선으로 설정
            module.GetComponent<NetworkParentSync>().SetNetworkParent(agentShipTransform);

            // 모듈 컨트롤러 생성
            ModuleController moduleController = Instantiate(_craneControllerPrefab, _cranePosition.position, _cranePosition.rotation).GetComponent<ModuleController>();
            // 네트워크 스폰
            NetworkServer.Spawn(moduleController.gameObject);
            moduleController.GetComponent<NetworkParentSync>().SetNetworkParent(ShipSpawner.Instance.CurrentShip.transform);

            // 서로 연결해주기
            if (module != null && moduleController != null)
            {
                if (moduleController != null && module != null)
                {
                    moduleController.SetConnectedModule(module);
                    module.SetModuleInteractSpot(moduleController);
                }
                else
                {
                    Debug.LogError("모듈 컨트롤러에 ModuleInteractSpot 컴포넌트가 없습니다.");
                }
            }
        }
    }

    /// <summary>
    /// 모듈 포인트를 네트워크상에 생성
    /// </summary>
    [Server]
    private void CreateModulePoints()
    {
        foreach (Transform pointPosition in _modulePointPositions)
        {
            uint modulePointId = NetworkObjectSpawner.Instance.RequestSpawnObjectAtPositionAndGetId(_modulePointPrefab, pointPosition.position, pointPosition.rotation);

            ModulePoint modulePointComponent = GameNetworkManager.GetNetworkComponent<ModulePoint>(modulePointId);
            if (modulePointComponent != null)
            {
                modulePointComponent.SetModulePointIndex(_modulePointPositions.IndexOf(pointPosition));
                modulePointComponent.SetModulePointLocalPosition(pointPosition.position);
            }
        }
    }
    #endregion

    #region 물리 처리 메소드
    /// <summary>
    /// 엔진 추진력을 적용합니다.
    /// </summary>
    [Server]
    private void ApplyEngineForce()
    {
        // 부스터 사용 중이거나 전환 중이면 ApplyEngineBoost에서 처리
        if (_isBoosting || _isBoostTransitioning)
        {
            return;
        }

        UpdateTargetSpeed();

        // 현재 속도를 목표 속도로 Lerp
        _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, _acceleration * Time.fixedDeltaTime);

        // 전진 방향으로 힘 계산
        Vector2 forceDirection = _agentRigidbody2D.transform.up;

        // 현재 속도와 목표 속도의 차이를 계산
        Vector2 currentVelocity = _agentRigidbody2D.velocity;
        Vector2 targetVelocity = forceDirection * _currentSpeed;
        Vector2 velocityDifference = targetVelocity - currentVelocity;

        // 속도 차이에 비례하여 힘 적용
        Vector2 force = velocityDifference * _forceMultiplier;

        _agentRigidbody2D.AddForce(force, ForceMode2D.Force);
    }

    /// <summary>
    /// 회전 토크를 적용합니다.
    /// </summary>
    [Server]
    private void ApplyRotationTorque()
    {
        if (Mathf.Abs(_currentAngularInput) > 0.1f)
        {
            // 회전 방향을 반대로 수정 (음수로 변경) // 현재 배가 전진중이면 정방향, 후진중이면 역방향
            _currentAngularSpeed = -_currentAngularInput * _rotationTorque * Mathf.Sign(_currentSpeed) * Mathf.Sqrt(Mathf.Abs(_currentSpeed)) * _angularSpeedMultiplierPerSpeed;
            _agentRigidbody2D.AddTorque(_currentAngularSpeed);
        }
    }
    /// <summary>
    /// 부스터를 적용합니다. 부스터가 활성화된 상태에서 속도를 증가시킵니다.
    /// </summary>
    [Server]
    private void ApplyEngineBoost()
    {
        _boostTimer += Time.fixedDeltaTime;

        // 부스터 전환 중일 때의 처리
        if (_isBoostTransitioning)
        {
            _boostTransitionTimer += Time.fixedDeltaTime;
            float transitionProgress = _boostTransitionTimer / _boostTransitionTime;

            if (_isBoosting)
            {
                // 부스터 활성화 중 - 점진적으로 부스터 속도로 증가
                _targetSpeed = Mathf.Lerp(_boostStartSpeed, _boostGear * _forwardSpeedPerGear, transitionProgress);

                if (transitionProgress >= 1f)
                {
                    _isBoostTransitioning = false;
                    _targetSpeed = _boostGear * _forwardSpeedPerGear;
                }
            }
            else
            {
                // 부스터 비활성화 중 - 점진적으로 원래 속도로 감소
                float originalTargetSpeed = _currentEngineGear * _forwardSpeedPerGear;
                _targetSpeed = Mathf.Lerp(_preBoostSpeed, originalTargetSpeed, transitionProgress);

                if (transitionProgress >= 1f)
                {
                    _isBoostTransitioning = false;
                    _targetSpeed = originalTargetSpeed;
                    _agentController.SetBoosting(false);
                    _agentController.SetCenterOfMass(_currentEngineGear);
                }
            }
        }
        else if (_isBoosting)
        {
            // 부스터가 완전히 활성화된 상태 - 부스터 속도 유지
            _targetSpeed = _boostGear * _forwardSpeedPerGear;
        }

        // 부스터 지속시간 체크
        if (_isBoosting && _boostTimer >= _boostDuration)
        {
            StartBoostDeactivation();
        }

        // 현재 속도를 목표 속도로 부드럽게 변경
        _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, _acceleration * Time.fixedDeltaTime);

        // 물리 힘 적용
        Vector2 currentVelocity = _agentRigidbody2D.velocity;
        Vector2 targetVelocity = _agentRigidbody2D.transform.up * _currentSpeed;
        Vector2 velocityDifference = targetVelocity - currentVelocity;
        Vector2 force = velocityDifference * _forceMultiplier;

        _agentRigidbody2D.AddForce(force, ForceMode2D.Force);
    }

    /// <summary>
    /// 부스터 비활성화를 시작합니다.
    /// </summary>
    [Server]
    private void StartBoostDeactivation()
    {
        _isBoosting = false;
        _isBoostTransitioning = true;
        _boostTransitionTimer = 0f;
        _preBoostSpeed = _currentSpeed; // 현재 부스터 속도를 저장
        _boostTimer = 0f;
    }

    /// <summary>
    /// 현재 기어에 맞는 목표 속도를 업데이트합니다.
    /// </summary>
    private void UpdateTargetSpeed()
    {
        if (_currentEngineGear > 0)
        {
            _targetSpeed = _currentEngineGear * _forwardSpeedPerGear;
        }
        else if (_currentEngineGear < 0)
        {
            _targetSpeed = _currentEngineGear * _backwardSpeedPerGear;
        }
        else
        {
            _targetSpeed = 0f;
        }
    }
    #endregion

    #region 입력 처리 메소드
    /// <summary>
    /// 엔진 모듈에서 기어 설정
    /// </summary>
    public void SetEngineGear(int gear)
    {
        _currentEngineGear = gear;
        _agentController.SetCenterOfMass(_currentEngineGear);
    }

    /// <summary>
    /// 엔진모듈에서 부스터 실행
    /// </summary>
    public void ActivateBoost()
    {
        if (_isBoosting || _isBoostTransitioning)
        {
            return; // 이미 부스터가 활성화되었거나 전환 중이면 무시
        }

        _isBoosting = true;
        _isBoostTransitioning = true;
        _boostTimer = 0f;
        _boostTransitionTimer = 0f;
        _boostStartSpeed = _currentSpeed; // 부스터 시작 시점의 현재 속도 저장
        _agentController.SetBoosting(true);
    }

    /// <summary>
    /// 조향 모듈에서 회전 입력 설정
    /// </summary>
    public void SetAngularInput(float angularInput)
    {
        _currentAngularInput = angularInput;
    }
    #endregion

    #region 데미지 처리 메소드

    /// <summary>
    /// 함선이 일반 데미지를 받았을때 호출, 구멍 오브젝트 생성
    /// </summary>
    public void CreateNormalHole(float damage, Vector2 hitLocalPoint)
    {
        Transform closestHole = GetClosestHolePos(transform.position + (Vector3)hitLocalPoint);
        GameObject hole = Instantiate(_normalHolePrefab, closestHole.position, Quaternion.identity);
        // 1. 먼저 NetworkServer.Spawn 호출
        NetworkServer.Spawn(hole);

        // 2. 그 다음 NetworkParentSync로 부모 설정 (위치 유지)
        NetworkParentSync parentSync = hole.GetComponent<NetworkParentSync>();
        if (parentSync != null)
        {
            parentSync.SetNetworkParent(this.transform);
        }
        else
        {
            Debug.LogWarning("NormalHole에 NetworkParentSync 컴포넌트가 없습니다!");
            hole.transform.SetParent(this.transform);
        }

        // 3. RepairPoint 초기화
        RepairPoint repairPoint = hole.GetComponent<RepairPoint>();
        if (repairPoint != null)
        {
            repairPoint.Init(this, damage, closestHole);
            _activeRepairPoints.Add(repairPoint);
        }
    }

    /// <summary>
    /// 함선이 대형 데미지를 받았을때 호출, 구멍 오브젝트 생성
    /// </summary>
    public void CreateLargeHole(float damage, Vector2 hitLocalPoint)
    {
        Transform closestHole = GetClosestHolePos(transform.position + (Vector3)hitLocalPoint);
        GameObject hole = Instantiate(_largeHolePrefab, closestHole.position, Quaternion.identity);

        // 1. 먼저 NetworkServer.Spawn 호출
        NetworkServer.Spawn(hole);

        // 2. 그 다음 NetworkParentSync로 부모 설정 (위치 유지)
        NetworkParentSync parentSync = hole.GetComponent<NetworkParentSync>();
        if (parentSync != null)
        {
            parentSync.SetNetworkParent(this.transform);
        }
        else
        {
            Debug.LogWarning("LargeHole에 NetworkParentSync 컴포넌트가 없습니다!");
            hole.transform.SetParent(this.transform);
        }

        // 3. RepairPoint 초기화
        RepairPoint repairPoint = hole.GetComponent<RepairPoint>();
        if (repairPoint != null)
        {
            repairPoint.Init(this, damage, closestHole);
            _activeRepairPoints.Add(repairPoint);

            // RpcSetPosition 제거 - NetworkParentSync가 이미 위치를 동기화함
            // repairPoint.RpcSetPosition(closestHole.position);
        }
    }

    /// <summary>
    /// 너무 이곳저곳 구멍이 생성되지 않도록 가장 가까운 구멍 위치 반환
    /// </summary>
    private Transform GetClosestHolePos(Vector2 hitPoint)
    {
        Transform closestHole = null;
        float closestDistanceSqr = Mathf.Infinity;

        foreach (Transform holePoint in _holePoints)
        {
            float distance = Vector2.Distance(holePoint.position, hitPoint);
            if (distance < closestDistanceSqr)
            {
                closestDistanceSqr = distance;
                closestHole = holePoint;
            }
        }
        // 선택된 구멍 위치는 리스트에서 제거하여 중복 사용 방지
        if (closestHole != null)
        {
            _holePoints.Remove(closestHole);
        }
        return closestHole;
    }

    /// <summary>
    /// 가장 가까운 구멍 위치 반환
    /// </summary>
    public Transform GetClosestHolePoint(Vector2 hitPoint)
    {
        Vector2 hitPosition = transform.position + (Vector3)hitPoint;
        Transform closestHole = null;
        float closestDistanceSqr = Mathf.Infinity;

        foreach (Transform holePoint in _holeParent)
        {
            float distance = Vector2.Distance(holePoint.position, hitPosition);
            if (distance < closestDistanceSqr)
            {
                closestDistanceSqr = distance;
                closestHole = holePoint;
            }
        }
        return closestHole;
    }

    /// <summary>
    /// 함선 데미지처리
    /// </summary>
    [Server]
    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        if (_currentHealth <= 0)
        {
            // 이미 체력이 0 이하인 경우, 타이머감소
            _agentController.GetComponent<ShipSinkController>()?.DecreaseSinkingPhaseTimerOnHit();
            return;
        }

        _currentHealth -= damage;
        if (_currentHealth < 0)
        {
            _currentHealth = 0;
        }

        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            // 함선 침수 단계 시작
            _agentController.GetComponent<ShipSinkController>()?.StartSinkingPhase();
        }

        // 구멍 생성
        if (damage > _largeHoleDamageThreshold)
        {
            CreateLargeHole(damage, hitPoint);
        }
        else if (damage > 0)
        {
            CreateNormalHole(damage, hitPoint);
        }
    }

    public void DestroySelf()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    /// <summary>
    /// 수리 지점의 오브젝트를 전달받아 해당 수리지점의 index를 서버에 전달
    /// </summary>
    public void Repair(float repairAmount, Transform holePoint, RepairPoint repairPoint)
    {
        _currentHealth = Mathf.Min(_currentHealth + repairAmount, _maxHealth); // 수리 후 체력 회복
        if (_currentHealth > 0)
        {
            // 함선 침수 단계 중지
            _agentController.GetComponent<ShipSinkController>()?.StopSinkingPhase();
        }
        _holePoints.Add(holePoint); // 수리된 구멍 위치를 다시 사용 가능하도록 리스트에 추가
        _activeRepairPoints.Remove(repairPoint);
    }

    /// <summary>
    /// 생성된 구멍을 탐색하여 현재 스폰된 적의 수를 반환
    /// </summary>
    private uint GetCurrentSpawnedEnemyCount()
    {
        uint count = 0;
        foreach (RepairPoint rp in _activeRepairPoints)
        {
            if (rp.IsEnemySpawned)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 체력 변경 시 UI 업데이트
    /// </summary>
    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        if (_healthBar != null)
        {
            _healthBar.fillAmount = _currentHealth / _maxHealth; // 체력바 UI 업데이트
        }
    }

    #endregion

    #region 도킹 처리 메소드
    /// <summary>
    /// 함선의 도킹 상태를 설정
    /// </summary>
    public void SetDockedState(bool isDocked)
    {
        _isDocked = isDocked;
        if (_isDocked)
        {
            // 도킹 상태일 때 속도와 회전 속도를 0으로 설정
            _agentRigidbody2D.velocity = Vector2.zero;
            _agentRigidbody2D.angularVelocity = 0f;
        }
    }
    /// <summary>
    /// 도킹 상태일 때, 도킹된 물체의 질량을 추가
    /// </summary>
    public void SetDockedRigidBodyMass(float mass)
    {
        _agentRigidbody2D.mass += mass;
    }
    /// <summary>
    /// 도킹 해제 시, 원래 질량으로 복구 @@@@@@이건 rigidbody에 mass에 맞춰서 밸런싱이 된 후에 적용해야함
    /// </summary>
    public void ResetRigidbodyMass()
    {
        _agentRigidbody2D.mass = _mass;
    }

    #endregion

    #region 유틸리티 메소드
    /// <summary>
    /// 해치 인덱스에 해당하는 해치를 반환
    /// </summary>
    public GameObject GetHatchByIndex(int index)
    {
        if (index < 0 || index >= _hatchs.Count)
        {
            Debug.LogError($"Invalid hatch index: {index}");
            return null;
        }
        return _hatchs[index];
    }

    /// <summary>
    /// 외부 모듈 설치 피벗을 인덱스로 가져옴
    /// </summary>
    public Transform GetExternalModulePivotByIndex(int index)
    {
        if (index < 0 || index >= _modulePointPositions.Count)
        {
            Debug.LogError($"Invalid module point index: {index}");
            return null;
        }
        return _modulePointPositions[index].transform.Find("ExternalModulePos");
    }

    /// <summary>
    /// 씬이 전환될때 함선에 저장된 아이템들을 저장
    /// </summary>
    public void SaveStoredItems()
    {
        BaseCampManager.Instance.GetStoredItemsAndReset(); //혹시 모르니까 초기화
        foreach (LTDObject item in _storedItems)
        {
            if (item == null)
            {
                continue;
            }
            ItemData itemData = new ItemData
            {
                itemType = item.LtdobjectType,
                scrapType = item is ScrapObject scrap ? scrap.ScrapType : default
            };
            Vector2 spawnPos = transform.InverseTransformPoint(item.transform.position);
            float rotationZ = item.transform.eulerAngles.z - transform.eulerAngles.z;
            BaseCampManager.Instance.StoreItemInShip(itemData, spawnPos, rotationZ);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
        {
            return;
        }
        // 배안에 들어오면 저장된 아이템 리스트에 추가
        if (collision.TryGetComponent<LTDObject>(out LTDObject ltdObject))
        {
            // 플레이어나 적일 경우 리턴
            if (ltdObject.LtdobjectType == LTDObjectType.플레이어 || ltdObject.LtdobjectType == LTDObjectType.몬스터)
            {
                return;
            }
            if (!_storedItems.Contains(ltdObject))
            {
                _storedItems.Add(ltdObject);
            }
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (!isServer)
        {
            return;
        }
        // 배밖으로 나가면 저장된 아이템 리스트에서 제거
        if (collision.TryGetComponent<LTDObject>(out LTDObject ltdObject))
        {
            if (_storedItems.Contains(ltdObject))
            {
                _storedItems.Remove(ltdObject);
            }
        }
    }
    #endregion
}