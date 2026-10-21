using UnityEngine;
using System;
using System.Collections;
using Mirror;

/// <summary>
/// 고정 방어막 모듈
/// 플레이어가 마우스 클릭 위치에 방어막을 생성하고, 패링
/// [박상민 - 2025.08.04]
/// </summary>
public class FixedShield : Module
{
    #region 필드

    [Header("방어막 설정")]
    [SerializeField] private float _shieldRadius = 5f;              // 방어막 범위
    [SerializeField] private float _parryDuration = 0.5f;           // 패링 단계 지속 시간
    [SerializeField] private float _reflectSpeedMultiplier = 3f;  // 반사 속도 배율
    [SerializeField] private float _rotationSpeed = 5f;            // 회전 속도
    [SerializeField] private float _rotationDampingAngle = 10f;    // 회전 감쇠 각도
    [SerializeField] private float _maxRayCastLength = 10f;        // 최대 레이캐스트 길이
    [SerializeField] private LayerMask _shieldLayerMask;

    [Header("에너지 시스템")]
    [SerializeField] private float _maxEnergy = 3f;                 // 최대 에너지
    [SerializeField] private float _activationCost = 1f;            // 활성화 비용
    [SerializeField] private float _defenseDrainRate = 2f;          // 방어 단계 소모율 (/초)
    [SerializeField] private float _rechargeRate = 1f;              // 충전율 (/초)
    [SerializeField] private float _cooldownDuration = 1f;          // 쿨다운 시간

    [Header("시각적 효과")]
    [SerializeField] private GameObject _rayCastRoot;              // 레이캐스트 루트 오브젝트
    [SerializeField] private GameObject _shieldVisualPrefab;        // 방어막 시각 효과 프리팹
    [SerializeField] private Color _previewColor = new Color(1f, 1f, 1f, 0.3f); // 방어막 미리보기 색상
    [SerializeField] private Color _parryColor = new Color(0f, 0f, 1f, 0.5f);     // 패링 단계 색상
    [SerializeField] private Color _defenseColor = new Color(0f, 1f, 0f, 0.5f);  // 방어 단계 색상

    // 상태 관리 변수
    [SyncVar] private float _currentEnergy;
    [SyncVar] private ShieldState _shieldState = ShieldState.Inactive;
    [SyncVar] private bool _isOnCooldown = false;

    private GameObject _shieldVisual;                               // 방어막 시각 효과 인스턴스
    private Coroutine _shieldCoroutine;                             // 방어막 동작 코루틴
    private Coroutine _energyCoroutine;                             // 에너지 관리 코루틴
    private Vector2 _currentShieldLocalPosition;

    #endregion

    #region Unity 생명주기

    protected override void Awake()
    {
        base.Awake();
        _moduleType = ModuleType.방어막;
        _currentEnergy = _maxEnergy;
    }

    protected override void Start()
    {
        base.Start();

        if(!isServer)
        {
            return;
        }

        // 방어막 시각 효과 초기화
        InitializeShieldVisual();

        // 에너지 관리 시작
        if (isServer)
        {
            _energyCoroutine = StartCoroutine(ManageEnergy());
            InitializeModule();
        }
    }

    private void Update()
    {
        if (!isServer || !IsOccupied) return;

        if (_shieldState == ShieldState.Inactive)
        {
            DetectShieldPos();
        }

        // 방어막 활성화/비활성화 처리
        if (_isAbility1Activated && _shieldState == ShieldState.Inactive && CanActivateShield())
        {
            ActivateShield(_currentShieldLocalPosition);
        }
        else if (!_isAbility1Activated && _shieldState != ShieldState.Inactive)
        {
            DeactivateShield();
        }
    }

    private void OnDestroy()
    {
        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
        }
        if (_energyCoroutine != null)
        {
            StopCoroutine(_energyCoroutine);
        }
    }

    #endregion

    #region 초기화

    /// <summary>
    /// 방어막 시각 효과 초기화
    /// </summary>
    private void InitializeShieldVisual()
    {
        if (_shieldVisualPrefab != null)
        {
            _shieldVisual = Instantiate(_shieldVisualPrefab, _agentShip.transform);
            NetworkServer.Spawn(_shieldVisual);
            _shieldVisual.transform.localPosition = new Vector3(0f, 2f, 0f);
            _shieldVisual.transform.localScale = Vector3.one * _shieldRadius * 20f;
            _shieldVisual.GetComponent<NetworkParentSync>().SetNetworkParent(_agentShip.transform);
            _shieldVisual.GetComponent<FixedShieldParryPoint>().Initialize(this, _shieldRadius);
            _shieldVisual.GetComponent<FixedShieldParryPoint>().RpcToggleShieldVisual(false, _previewColor);
        }
    }

    /// <summary>
    /// 모듈 초기화
    /// </summary>
    private void InitializeModule()
    {
        _moduleController = GetComponent<ModuleController>();
        _moduleController.SetConnectedModule(this);
    }

    #endregion

    #region 방어막 활성화/비활성화
    public override void PlayerEntered(PlayerModuleController playerModuleController)
    {
        base.PlayerEntered(playerModuleController);
        if (_shieldState == ShieldState.Inactive)
        {
            _shieldVisual.GetComponent<FixedShieldParryPoint>().RpcToggleShieldVisual(true, _previewColor);
        }
    }

    public override void PlayerExited()
    {
        base.PlayerExited();
        if (_shieldState == ShieldState.Inactive)
        {
            _shieldVisual.GetComponent<FixedShieldParryPoint>().RpcToggleShieldVisual(false, _previewColor);
        }
    }
    /// <summary>
    /// 방어막 활성화 가능 여부 확인
    /// </summary>
    private bool CanActivateShield()
    {
        return _currentEnergy >= _activationCost && !_isOnCooldown;
    }

    /// <summary>
    /// 서버에서 방어막을 활성화하는 명령
    /// </summary>
    private void ActivateShield(Vector2 position)
    {
        if (!CanActivateShield())
        {
            return;
        }
        // 에너지 소모
        _currentEnergy -= _activationCost;

        // 방어막 동작 시작
        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
        }
        _shieldCoroutine = StartCoroutine(ShieldSequence(position));
    }

    #endregion

    #region 방어막 동작 시퀀스
    /// <summary>
    /// 방어막 실행 위치를 탐색하기 위한 Raycast 기준점 회전
    /// </summary>
    private void RotateDetectRaycast()
    {
        if (_aimInput != Vector2.zero)
        {
            Vector2 aimDirection = _aimInput - (Vector2)transform.position;

            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            float parentRotation = _rayCastRoot.transform.parent.eulerAngles.z;
            float localTargetAngle = targetAngle - parentRotation - 90f;
            ApplyRotationWithEasing(localTargetAngle);
        }
        // 패드를 사용하여 조준하고 있는경우
        else if (_rotationInput != Vector2.zero)
        {
            // 목표 각도 계산
            float targetAngle = Mathf.Atan2(_rotationInput.y, _rotationInput.x) * Mathf.Rad2Deg;
            float parentRotation = _rayCastRoot.transform.parent.eulerAngles.z;
            float localTargetAngle = targetAngle - parentRotation - 90f;
            ApplyRotationWithEasing(localTargetAngle);
        }
    }

    /// <summary>
    /// 이징을 적용한 회전 처리
    /// </summary>
    private void ApplyRotationWithEasing(float localEulerAngles)
    {
        float currentAngle = _rayCastRoot.transform.localEulerAngles.z;
        float angleDifference = Mathf.DeltaAngle(currentAngle, localEulerAngles);

        // OutQuad 이징 적용
        float normalizedDistance = Mathf.Abs(angleDifference) / 180f; // 0~1로 정규화
        float easingFactor = OutQuadEasing(normalizedDistance);

        float angleToRotate = Mathf.Sign(angleDifference) * easingFactor * _rotationDampingAngle;
        _rayCastRoot.transform.localEulerAngles = new Vector3(0, 0, currentAngle + angleToRotate * _rotationSpeed * Time.deltaTime);
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
    /// 플레이어 마우스 위치에 따라 방어막 위치 감지
    /// </summary>
    private void DetectShieldPos()
    {
        if (_rayCastRoot == null)
        {
            return;
        }

        // Raycast를 사용하여 방어막 위치 감지
        RotateDetectRaycast();

        Vector2 rayOrigin = _rayCastRoot.transform.position;
        Vector2 rayDirection = _rayCastRoot.transform.up;

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDirection, _maxRayCastLength, _shieldLayerMask);
        if (hit.collider != null)
        {
            _currentShieldLocalPosition = Quaternion.Inverse(_rayCastRoot.transform.parent.rotation) * (hit.point - (Vector2)transform.position); // Raycast가 감지한 위치
        }
        else
        {
            return; // Raycast가 아무것도 감지하지 못하면 위치를 업데이트하지 않음
        }
        _shieldVisual.transform.localPosition = _currentShieldLocalPosition;
    }

    /// <summary>
    /// 방어막 동작 시퀀스 (패링 → 방어)
    /// </summary>
    private IEnumerator ShieldSequence(Vector2 position)
    {
        // 패링 단계 시작
        _shieldState = ShieldState.Parry;
        _shieldVisual.GetComponent<FixedShieldParryPoint>().EnableCollider(true);
        RpcUpdateShieldVisual(ShieldState.Parry, position);

        // 패링 단계 지속
        yield return new WaitForSeconds(_parryDuration);

        // 방어 단계로 전환 (에너지가 있는 동안)
        _shieldState = ShieldState.Defense;
        RpcUpdateShieldVisual(ShieldState.Defense, position);

        // 방어 단계에서 에너지 소모
        while (_shieldState == ShieldState.Defense && _currentEnergy > 0)
        {
            yield return new WaitForSeconds(1f / _defenseDrainRate);
            _currentEnergy -= _defenseDrainRate / _defenseDrainRate; // 초당 소모량
        }

        // 방어막 비활성화
        DeactivateShield();
    }

    /// <summary>
    /// 방어막 비활성화
    /// </summary>
    [Server]
    private void DeactivateShield()
    {
        _shieldState = ShieldState.Inactive;
        _shieldVisual.GetComponent<FixedShieldParryPoint>().EnableCollider(false);
        RpcUpdateShieldVisual(ShieldState.Inactive, Vector2.zero);

        // 쿨다운 시작
        if (!_isOnCooldown)
        {
            StartCoroutine(CooldownSequence());
        }

        // 코루틴 정리
        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
            _shieldCoroutine = null;
        }
    }

    #endregion

    #region 에너지 관리

    /// <summary>
    /// 에너지 관리 코루틴
    /// </summary>
    private IEnumerator ManageEnergy()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.02f);
            // 쿨다운 중이 아니고 방어막이 비활성 상태일 때만 충전
            if (!_isOnCooldown && _shieldState == ShieldState.Inactive)
            {
                _currentEnergy = Mathf.Min(_currentEnergy + _rechargeRate * 0.02f, _maxEnergy);
            }
        }
    }

    /// <summary>
    /// 쿨다운 시퀀스
    /// </summary>
    private IEnumerator CooldownSequence()
    {
        _isOnCooldown = true;
        yield return new WaitForSeconds(_cooldownDuration);
        _isOnCooldown = false;
    }

    #endregion

    #region 충돌 처리

    /// <summary>
    /// 총알과의 충돌 처리
    /// </summary>
    public void OnShieldTriggerEnter2D(Collider2D other)
    {
        if (!isServer)
        {
            return;
        }
        Debug.Log($"<color=red>난 모든것을 \"패리\"한다 {other.name}</color>");

        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet == null)
        {
            return;
        }

        if (bullet.EnemyTag != "Ship")
        {
            return;
        }

        if (!bullet.IsAbleToParry)
        {
            return;
        }

        switch (_shieldState)
        {
            case ShieldState.Parry:
                HandleParry(bullet);
                break;
            case ShieldState.Defense:
                HandleDefense(bullet);
                break;
        }
    }
    /// <summary>
    /// 패링 처리
    /// </summary>
    [Server]
    private void HandleParry(Bullet bullet)
    {
        bullet.SetEnemyTag("Enemy");

        // 총알의 현재 속도 방향을 반대로 바꿔서 더 빠르게 반사
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            Vector2 reflectedVelocity = -bulletRb.velocity * _reflectSpeedMultiplier;
            // 총알 방향도 바꿔줌
            float angle = Mathf.Atan2(reflectedVelocity.y, reflectedVelocity.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            // 반사된 속도를 총알에 적용, 이때 모든 클라이언트에서 동기화 시켜야 함.
            bullet.ApplyFireData(new FireData
            {
                position = bullet.transform.position,
                rotation = bullet.transform.rotation,
                velocityX = -bullet.VelocityX,
                velocityY = -bullet.VelocityY,
                height = bullet.CurrentHeight,
                enemyTag = bullet.EnemyTag,
                timestamp = Time.time
            });
        }

        // 패링 효과 표시
        RpcShowParryEffect(bullet.transform.position);
    }

    /// <summary>
    /// 방어 처리
    /// </summary>
    [Server]
    private void HandleDefense(Bullet bullet)
    {
        // 총알 반납
        BulletPool.Despawn(bullet.gameObject);

        // 방어 효과 표시
        RpcShowDefenseEffect(bullet.transform.position);
    }

    #endregion

    #region 시각적 효과

    /// <summary>
    /// 방어막 시각 효과 업데이트
    /// </summary>
    [ClientRpc]
    private void RpcUpdateShieldVisual(ShieldState state, Vector2 position)
    {
        if (_shieldVisual == null)
        {
            return;
        }

        switch (state)
        {
            case ShieldState.Inactive:
                _shieldVisual.GetComponent<FixedShieldParryPoint>().RpcToggleShieldVisual(true, _previewColor);
                break;
            case ShieldState.Parry:
                _shieldVisual.GetComponent<FixedShieldParryPoint>().RpcToggleShieldVisual(true, _parryColor);
                break;
            case ShieldState.Defense:
                _shieldVisual.GetComponent<FixedShieldParryPoint>().RpcToggleShieldVisual(true, _defenseColor);
                break;
        }
    }

    /// <summary>
    /// 패링 효과 표시
    /// </summary>
    [ClientRpc]
    private void RpcShowParryEffect(Vector3 position)
    {
        // 패링 효과 생성 (예시)
        Debug.Log($"패링 효과 재생: {position}");
        // EffectManager.Instance?.PlayParryEffect(position);
    }

    /// <summary>
    /// 방어 효과 표시
    /// </summary>
    [ClientRpc]
    private void RpcShowDefenseEffect(Vector3 position)
    {
        // 방어 효과 생성 (예시)
        Debug.Log($"방어 효과 재생: {position}");
        // EffectManager.Instance?.PlayDefenseEffect(position);
    }

    #endregion

    #region 디버그 및 기즈모

    /// <summary>
    /// 방어막 범위 시각화 (Scene 뷰에서만)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _shieldState switch
        {
            ShieldState.Parry => _parryColor,
            ShieldState.Defense => _defenseColor,
            _ => Color.white
        };

        Gizmos.DrawWireSphere(transform.position, _shieldRadius);
    }

    #endregion

    #region 공개 프로퍼티

    /// <summary>
    /// 현재 에너지량
    /// </summary>
    public float CurrentEnergy => _currentEnergy;

    /// <summary>
    /// 최대 에너지량
    /// </summary>
    public float MaxEnergy => _maxEnergy;

    /// <summary>
    /// 현재 방어막 상태
    /// </summary>
    public ShieldState CurrentState => _shieldState;

    /// <summary>
    /// 쿨다운 중 여부
    /// </summary>
    public bool IsOnCooldown => _isOnCooldown;

    #endregion
}