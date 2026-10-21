using System;
using Mirror;
using TMPro;
using UnityEngine;

/// <summary>
/// 함포 모듈 - 함선에 장착되어 적을 공격하는 모듈 (네트워크 동기화 버전)
/// [박상민 - 2025.06.27]
/// </summary>
public class Cannon : Module
{
    #region 필드

    [Header("함포 설정")]
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private float _rotationDampingAngle = 10;
    [SerializeField] private float _fireForce = 20f;            // 발사 힘
    [SerializeField] private float _fireCooldown = 0.1f;        // 발사 쿨다운
    [SerializeField] private float _rotationLimit = 60f;        // 회전 각도 제한
    [SerializeField] private int _maxShellCount = 5;            // 최대 장전 가능 포탄 수
    [SerializeField] private float _cannonHeight = 3f;
    [SerializeField] private float _barrelLength = 3f;
    [SerializeField] private float _maxAngleDegree = 15f;
    [SerializeField] private float _angleSpeed = 10f;

    [Header("컴포넌트 참조")]
    [SerializeField] private Transform _cannonHead;         // 함포의 회전 중심
    [SerializeField] private Transform _firePoint;          // 발사 지점
    [SerializeField] private GameObject _aimTarget;         // 조준점
    [SerializeField] private GameObject _projectilePrefab;  // 발사체 프리팹 (NetworkObject 포함)
    [SerializeField] private GameObject _actualAimTarget;   // 실제 발사 조준점
    [SerializeField] private GameObject _razerSight;        // 레이저 사이트 오브젝트

    [SyncVar(hook = nameof(OnLoadedShellCountChanged))] private int _loadedShellCount;                // 장전된 포탄 수

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _loadedShellCountText; // 현재 장전 수를 보여줄 UI

    private float _lastFireTime = -10f;
    private bool _isCharging = false;
    private const float InitialAngle = 0f; // -5f * Mathf.Deg2Rad;
    private float _currentAngle = InitialAngle;
    private float _maxAngle => _maxAngleDegree * Mathf.Deg2Rad;
    private float _angleSpeedRad => _angleSpeed * Mathf.Deg2Rad;

    // 람다식 & 프로퍼티
    public bool IsShellFull => _loadedShellCount >= _maxShellCount;
    public bool IsShellEmpty => _loadedShellCount <= 0;
    public int LoadedShellCount { get { return _loadedShellCount; } }

    #endregion

    #region Unity 생명주기

    protected override void Awake()
    {
        base.Awake();

        if (_cannonHead == null)
        {
            _cannonHead = transform.Find("CannonHead");
            if (_cannonHead == null)
            {
                Debug.LogError($"CannonHead를 찾을 수 없습니다. - {gameObject.name}");
            }
        }

        if (_firePoint == null && _cannonHead != null)
        {
            _firePoint = _cannonHead.transform.Find("FirePoint");
            if (_firePoint == null)
            {
                Debug.LogError($"FirePoint를 찾을 수 없습니다. - {gameObject.name}");
            }
        }

        // if (_aimTarget == null)
        // {
        //     Transform aimTargetTransform = transform.Find("AimTarget");
        //     if (aimTargetTransform != null)
        //     {
        //         _aimTarget = aimTargetTransform.gameObject;
        //         _aimTarget.SetActive(false);
        //     }
        //     else
        //     {
        //         Debug.LogError($"AimTarget을 찾을 수 없습니다. - {gameObject.name}");
        //     }
        // }

        if (_actualAimTarget == null)
        {
            Transform actualAimTargetTransform = transform.Find("ActualAimTarget");
            if (actualAimTargetTransform != null)
            {
                _actualAimTarget = actualAimTargetTransform.gameObject;
                _actualAimTarget.SetActive(false);
            }
            else
            {
                Debug.LogError($"ActualAimTarget을 찾을 수 없습니다. - {gameObject.name}");
            }
        }

        // 투사체 프리팹 검증
        if (_projectilePrefab != null && _projectilePrefab.GetComponent<NetworkIdentity>() == null)
        {
            Debug.LogError($"투사체 프리팹에 NetworkIdentity 컴포넌트가 없습니다. - {_projectilePrefab.name}");
        }
    }

    protected override void Start()
    {
        base.Start();

        _moduleType = ModuleType.함포; // 모듈 타입 설정
        _rotationInput = Vector2.zero; // 초기 회전 입력값 설정
        _aimInput = Vector2.zero; // 초기 조준 입력값 설정
        ReloadShell();

        InitializeCannonUI();
        _razerSight.SetActive(false);
    }

    private void Update()
    {
        if (!isServer || !IsOccupied)
        {
            return;
        }
        RotateCannonHead();
        LimitRotation();
        ChargeCannon();
    }

    #endregion

    #region Module

    public override void PlayerEntered(PlayerModuleController playerController)
    {
        if (IsOccupied || !isServer)
        {
            return;
        }
        RpcToggleRazerSight(true);
        base.PlayerEntered(playerController);
    }


    override public void PlayerExited()
    {
        if (!isServer)
        {
            return;
        }
        //내리면 현재 충전중인거 취소
        _isAbility1Activated = false;
        _isCharging = false;
        RpcToggleAimVisibility(_playerModuleController.connectionToClient, false); // 에임점 토글
        RpcUpdateAimPosition(_playerModuleController.connectionToClient, Vector2.zero);
        RpcToggleRazerSight(false);

        base.PlayerExited();
    }

    #endregion

    #region 함포 회전

    /// <summary>
    /// 함포 헤드를 회전시킵니다
    /// </summary>
    private void RotateCannonHead()
    {
        if (_cannonHead == null)
        {
            return;
        }

        // 마우스를 이용하여 조준하고 있는 경우
        if (_aimInput != Vector2.zero)
        {
            // 함포 헤드에서 목표 지점으로의 직접적인 방향 계산
            Vector2 localAimDirection = _aimInput - ShipSpawner.Instance.OriginOffset;
            Vector2 agentAimPos = _agentShip.transform.TransformPoint((Vector3)localAimDirection);
            Vector2 aimDirection = agentAimPos - (Vector2)_cannonHead.position;

            if (aimDirection.sqrMagnitude > 0.05f)
            {
                float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                float parentRotation = _cannonHead.parent.eulerAngles.z;
                float localTargetAngle = targetAngle - parentRotation - 90f;

                ApplyRotationWithEasing(localTargetAngle);
            }
        }
        // 패드를 사용하여 조준하고 있는 경우
        else if (_rotationInput != Vector2.zero)
        {

            float targetAngle = Mathf.Atan2(_rotationInput.y, _rotationInput.x) * Mathf.Rad2Deg;
            float parentRotation = _cannonHead.parent.eulerAngles.z;
            float localTargetAngle = targetAngle - parentRotation - 90f;

            ApplyRotationWithEasing(localTargetAngle);
        }
    }

    /// <summary>
    /// 이징을 적용한 회전 처리
    /// </summary>
    private void ApplyRotationWithEasing(float localTargetAngle)
    {
        float currentAngle = _cannonHead.localEulerAngles.z;
        float angleDifference = Mathf.DeltaAngle(currentAngle, localTargetAngle);

        // OutQuad 이징 적용
        float normalizedDistance = Mathf.Abs(angleDifference) / 180f; // 0~1로 정규화
        float easingFactor = OutQuadEasing(normalizedDistance);

        float angleToRotate = Mathf.Sign(angleDifference) * easingFactor * _rotationDampingAngle;
        _cannonHead.localEulerAngles = new Vector3(0, 0, currentAngle + angleToRotate * _rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// OutQuad 이징 함수 (목표에 멀수록 빠르고, 가까워질수록 천천히)
    /// </summary>
    private float OutQuadEasing(float t)
    {
        // 거리가 멀 때(0.3 이상)는 최대 속도로 움직임
        if (t > 0.3f)
        {
            return 1f;
        }

        // 가까워졌을 때만 감속 적용 (0~0.3 구간을 0~1로 재매핑)
        float remappedT = t / 0.3f;
        return 1f - (1f - remappedT) * (1f - remappedT);
    }

    /// <summary>
    /// 회전 각도를 제한하는 함수
    /// </summary>
    private void LimitRotation()
    {
        if (_cannonHead == null) return;

        float currentAngle = _cannonHead.localEulerAngles.z;
        if (currentAngle > 180f)
            currentAngle -= 360f;

        if (currentAngle > _rotationLimit)
            _cannonHead.localEulerAngles = new Vector3(0, 0, _rotationLimit);
        else if (currentAngle < -_rotationLimit)
            _cannonHead.localEulerAngles = new Vector3(0, 0, -_rotationLimit);
    }

    #endregion

    #region 발사 시스템

    /// <summary>
    /// 함포 충전 및 발사 처리
    /// </summary>
    private void ChargeCannon()
    {
        if (Time.time - _lastFireTime < _fireCooldown)
        {
            return;
        }

        if (_isAbility1Activated)
        {
            RequestFireCannon();
        }
    }

    /// <summary>
    /// 에임 타겟 위치 업데이트
    /// </summary>
    private void UpdateAimTargetPosition()
    {
        // 발사 속도 계산
        float velocityX = Mathf.Cos(_currentAngle) * _fireForce;
        float velocityY = Mathf.Sin(_currentAngle) * _fireForce;

        // firePoint의 up 방향 (발사 방향)
        Vector2 fireDirection = _firePoint.up;

        // 포물선 운동 계산 (간단한 물리학 공식 사용)
        float gravity = 9.81f;
        float timeOfFlight = (velocityY + Mathf.Sqrt(velocityY * velocityY + 2 * gravity * (_cannonHeight + _barrelLength * Mathf.Sin(_currentAngle)))) / gravity;

        // 수평 거리 계산 (함선 속도 포함)
        Vector2 shipVelocity = _agentShip.GetComponent<Rigidbody2D>().velocity;
        Vector2 horizontalDistance = (fireDirection * velocityX + shipVelocity) * timeOfFlight;

        // 에임 타겟 위치 설정
        RpcUpdateAimPosition(_playerModuleController.connectionToClient, (Vector2)_firePoint.position + horizontalDistance);
    }

    /// <summary>
    /// 서버에 발사를 요청하는 함수
    /// </summary>
    private void RequestFireCannon()
    {
        if (_firePoint == null || _projectilePrefab == null)
        {
            Debug.LogError("FirePoint 또는 ProjectilePrefab이 없습니다.");
            return;
        }

        _lastFireTime = Time.time;

        // 발사 데이터 구조체 생성
        FireData fireData = new FireData
        {
            position = _firePoint.position,
            rotation = _firePoint.rotation,
            enemyTag = "Enemy",
            velocityX = _firePoint.up * (_fireForce * Mathf.Cos(_currentAngle)) + (Vector3)_agentShip.GetComponent<Rigidbody2D>().velocity,
            velocityY = _fireForce * Mathf.Sin(_currentAngle),
            height = _cannonHeight + _barrelLength * Mathf.Sin(_currentAngle),
            timestamp = Time.time
        };

        // 서버에 발사 요청
        FireCannon(fireData);
    }

    /// <summary>
    /// 서버에서 투사체를 생성하고 스폰하는 명령
    /// </summary>
    private void FireCannon(FireData fireData)
    {
        if (IsShellEmpty)
        {
            Debug.Log($"{this.name} 함포에 포탄이 없습니다.");
            return;
        }

        try
        {
            // 서버에서 투사체 생성
            GameObject projectile = BulletPool.Get(_projectilePrefab, fireData.position, fireData.rotation);

            // Bullet 컴포넌트 설정
            if (projectile.TryGetComponent<Bullet>(out Bullet bulletComponent))
            {
                // 네트워크 스폰 확인
                NetworkIdentity networkIdentity = projectile.GetComponent<NetworkIdentity>();
                if (networkIdentity != null)
                {
                    // 총알 소모
                    ServerConsumeShell();

                    // 네트워크 스폰
                    NetworkServer.Spawn(projectile);

                    // 발사체 방향 설정
                    bulletComponent.ApplyFireData(fireData);

                    Debug.Log($"함포 발사 완료: {projectile.name}");
                }
                else
                {
                    Debug.LogError($"투사체에 NetworkIdentity가 없습니다: {projectile.name}");
                    Destroy(projectile);
                }
            }
            else
            {
                Debug.LogError($"발사체에 Bullet 컴포넌트가 없습니다: {projectile.name}");
                Destroy(projectile);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"함포 발사 중 오류 발생: {ex.Message}");
        }
    }
    #endregion

    #region 재장전 시스템

    /// <summary>
    /// 함포 장전 수 UI 값 업데이트
    /// [조우석 - 2025.07.31.]
    /// </summary>
    private void UpdateCannonUI(int count)
    {
        _loadedShellCountText.text = count.ToString() + "<color=white>/" + _maxShellCount.ToString() + "</color>";
    }

    /// <summary>
    /// 함포 UI 초기화
    /// [조우석 - 2025.07.31.]
    /// </summary>
    private void InitializeCannonUI()
    {
        _loadedShellCountText.text = _loadedShellCount.ToString() + "<color=white>/" + _maxShellCount.ToString() + "</color>";
    }

    /// <summary>
    /// 장전 포탄 수 지정하는 외부 참조용 메서드
    /// [조우석 - 2025.07.31.]
    /// </summary>
    /// <param name="count"></param>
    [Server]
    public void SetLoadedShellCount(int count)
    {
        _loadedShellCount = count;
    }

    /// <summary>
    /// (SERVER) 함포의 포탄을 충전하는 메서드
    /// [조우석 - 2025.07.30]
    /// </summary>
    [Server]
    public void ReloadShell()
    {
        // 원래는 해당 메서드가 호출되기 전에 포탄의 수를 체크해야하나, 안정성을 위해 비정상 동작 확인
        if (IsShellFull)
        {
            Debug.LogWarning($"<color=yellow>{this.name} 함포에 포탄이 가득찬 상황에서 포탄 충전이 호출되었습니다.</color>");
            return;
        }

        _loadedShellCount = _maxShellCount;

        Debug.Log($"{this.name} 현재 포탄 개수 : {_loadedShellCount}");
    }

    /// <summary>
    /// (SERVER) 함포의 포탄을 소모하는 메서드
    /// [조우석 - 2025.07.30]
    /// </summary>
    [Server]
    public void ServerConsumeShell()
    {
        // 원래는 해당 메서드가 호출되기 전에 포탄의 수를 체크해야하나, 안정성을 위해 비정상 동작 확인
        if (IsShellEmpty)
        {
            Debug.LogWarning($"<color=yellow>{this.name} 함포에 포탄이 비어있는 상황에서 포탄 소모가 호출되었습니다.</color>");
            return;
        }

        _loadedShellCount--;

        Debug.Log($"{this.name} 현재 포탄 개수 : {_loadedShellCount + 1} -> {_loadedShellCount}");
    }

    #endregion

    #region 클라이언트 시각 동기화

    [TargetRpc]
    public void RpcToggleAimVisibility(NetworkConnection target, bool isVisible)
    {
        if (_actualAimTarget != null)
        {
            _actualAimTarget.SetActive(isVisible);
        }
    }

    [TargetRpc]
    public void RpcUpdateAimPosition(NetworkConnection target, Vector2 position)
    {
        if (_actualAimTarget != null)
        {
            _actualAimTarget.transform.position = position;
        }
    }

    private void OnLoadedShellCountChanged(int oldCount, int newCount)
    {
        UpdateCannonUI(newCount);
    }

    [ClientRpc]
    private void RpcToggleRazerSight(bool isActive)
    {
        if (_razerSight != null)
        {
            _razerSight.SetActive(isActive);
        }
    }

    #endregion
}

/// <summary>
/// 발사 데이터를 담는 구조체
/// [박상민 - 2025.06.27]
/// </summary>
[Serializable]
public struct FireData
{
    public Vector2 position;
    public Quaternion rotation;
    public Vector2 velocityX;
    public float velocityY;
    public float height;
    public string enemyTag;
    public float timestamp;
}