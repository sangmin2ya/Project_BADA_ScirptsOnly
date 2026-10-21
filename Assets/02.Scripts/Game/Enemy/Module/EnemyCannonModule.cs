using Mirror;
using Mirror.Examples.Tanks;
using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 적 함포 모듈의 동작을 관리
/// [김도형 - 2025.07.11.]
/// </summary>
public class EnemyCannonModule : EnemyModuleController
{
    [Header("포격 설정")]
    [Tooltip("함포가 스스로 판단을 하여 포격을 하는가? true 시 범위 내의 적이 있으면 쿨타임이 될 때마다 공격을 한다")]
    [SerializeField] private bool _autoFire = true;
    [SerializeField] private float _cooldown = 3f;                  // 쿨다운 시간
    [SerializeField] private float _randomCooldownOffset = 0.2f;    // 랜덤 쿨다운 오프셋
    [SerializeField] private float _fireRange = 50f;                // 사격 범위
    [SerializeField] private float _projectileSpeed = 20f;          // 포탄 속도
    [SerializeField] private float _aimingAngle = 60f;              // 좌우 각도 (60도씩 총 120도)
    [SerializeField] private float _randomTargetRadius = 20f;       // 랜덤 타겟 반경
    [SerializeField] private float _angleToleranceThreshold = 6f;   // 발사 허용 오차 각도
    [SerializeField] private float _fireHeight = 3f;              // 발사 높이 (포탄이 발사되는 높이)

    [Header("전조 설정")]
    [SerializeField] private GameObject _chargeParticleEffect;      // 충전 파티클 이펙트
    [SerializeField] private GameObject _fireParticleEffect;        // 발사 파티클 이펙트
    [SerializeField] private LineRenderer _aimLineRenderer;         // 타격 예상선 LineRenderer
    [SerializeField] private float _aimLineWidth = 0.1f;            // 조준선 두께
    [SerializeField] private Color _aimLineColor = Color.red;    // 조준선 색상
    [SerializeField] private float _offset = 0.5f;                  // 발사 높이에 따른 파티클 위치 조절 오프셋

    [Header("참조")]
    private Transform _shipTransform;                       // 자신의 함선 Transform
    private Transform _targetShipTransform = null;          // 타겟 함선
    [SerializeField] private GameObject _projectilePrefab;  // 포탄 프리팹
    [SerializeField] private Transform _cannonHead;        // 함포 헤드 위치
    [SerializeField] private Transform _firePoint;          // 발사 지점

    [Header("디버그")]
    [SerializeField] private bool _showDebugLines = true;   // 디버그 라인 표시

    private Rigidbody2D _shipRigidbody;           // 자신의 함선 Rigidbody
    private Rigidbody2D _targetRigidbody = null;  // 타겟 함선 Rigidbody
    private float _currentCooldown;               // 현재 쿨다운
    private Vector2 _randomTargetOffset;          // 랜덤 타겟 오프셋
    private bool _isOnCooldown = false;
    private bool _targetLocked = false;
    private GameObject _currentChargeEffect = null;
    private GameObject _currentFireEffect = null;
    private Camera _mainCamera;

    private void Awake()
    {
        _shipTransform = _enemyController.transform;

        // LineRenderer 초기 설정
        if (_aimLineRenderer == null)
        {
            _aimLineRenderer = GetComponent<LineRenderer>();
            if (_aimLineRenderer == null)
            {
                // LineRenderer가 없으면 생성
                _aimLineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        SetupAimLineRenderer();
    }

    protected override void Start()
    {
        base.Start();

        _shipRigidbody = _enemyController.Rigidbody;

        // 적 함선 타겟 연결 시, 함포에도 타겟 연결
        _enemyController.OnTargetChanged += SetTarget;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        StartCooldown();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        _mainCamera = GameObject.Find("AgentCamera")?.GetComponent<Camera>();
        if (_mainCamera == null)
        {
            Debug.LogError("클라이언트에서 AgentCamera를 찾을 수 없습니다!");
        }
    }

    private void Update()
    {
        if (_targetShipTransform == null)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = GameObject.Find("AgentCamera")?.GetComponent<Camera>();
        }
        if (_mainCamera == null)
        {
            return;
        }

        // 각 플레이어의 카메라를 기준으로 계산을 하기에 모든 클라이언트에서 동작
        if (_currentChargeEffect != null)
        {
            _currentChargeEffect.transform.position =
                _firePoint.position + _mainCamera.transform.up * (_fireHeight * _offset);
        }

        if (_currentFireEffect != null)
        {
            _currentFireEffect.transform.position =
                _firePoint.position + _mainCamera.transform.up * (_fireHeight * _offset);
        }

        // 일단 각 플레이어가 알아서 함포의 대포 회전 계산
        RotateBarrel();

        if (!isServer)
        {
            return;
        }

        // 자동으로 공격 타이밍을 계산
        if (_autoFire)
        {
            if (_targetLocked)
            {
                // 쿨다운 처리
                if (_isOnCooldown)
                {
                    _currentCooldown -= Time.deltaTime;
                    if (_currentCooldown <= 0)
                    {
                        _isOnCooldown = false;

                        Fire();
                    }
                    else
                    {
                        Vector2 finalTargetPosition = (CalculatePredictedRelativePosition() == Vector2.zero ? _targetShipTransform.position - transform.position : CalculatePredictedRelativePosition()) + _randomTargetOffset;
                        UpdateAimLine(finalTargetPosition);
                    }
                }
                else
                {
                    // 초기 발사 시도
                    Fire();
                }
            }
            else
            {
                CheckAndLockTarget();
            }
        }
        // 외부에서 수동으로 쿨타임을 계산
        else
        {
            // 타겟이 고정되었고 쿨타임 체크 중이라면,
            // 쿨타임 적용 및 발포
            if (_targetLocked && _isOnCooldown)
            {
                _currentCooldown -= Time.deltaTime;
                if (_currentCooldown <= 0)
                {
                    Fire();
                }
                else
                {
                    Vector2 finalTargetPosition = (CalculatePredictedRelativePosition() == Vector2.zero ? _targetShipTransform.position - transform.position : CalculatePredictedRelativePosition()) + _randomTargetOffset;
                    RpcUpdateAimLine(finalTargetPosition);
                }
            }
        }
    }

    /// <summary>
    /// 공격 가능한 타겟 탐지 후 고정
    /// - Update 내에서 동작하던 동작 메서드로 분리
    /// [조우석 - 2025.08.05.]
    /// </summary>
    public void CheckAndLockTarget()
    {
        if (_targetShipTransform == null)
        {
            return;
        }
        // 거리 체크
        float distanceToTarget = Vector2.Distance(transform.position, _targetShipTransform.position);
        if (distanceToTarget < _fireRange)
        {
            // 예측 사격 계산
            Vector2 predictedRelativePosition = CalculatePredictedRelativePosition();

            if (predictedRelativePosition == Vector2.zero)
            {
                FireAtAngle(Random.Range(-_aimingAngle, _aimingAngle), (_targetShipTransform.position - transform.position + new Vector3(_randomTargetOffset.x, _randomTargetOffset.y, 0)).magnitude);
            }

            // 랜덤 오프셋 적용
            Vector2 finalTargetPosition = predictedRelativePosition + _randomTargetOffset;

            // 각도 계산
            Vector2 directionToTarget = finalTargetPosition.normalized;
            float angleToTarget = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;

            // 함선의 현재 각도 기준으로 상대 각도 계산
            float shipAngle = _shipTransform.localEulerAngles.z;
            float moduleAngle = transform.localEulerAngles.z;
            float relativeAngle = Mathf.DeltaAngle(shipAngle + moduleAngle, angleToTarget);

            // 사격 가능한 각도 범위 내인지 체크
            bool canFireAtTarget = Mathf.Abs(relativeAngle) <= _aimingAngle;

            if (canFireAtTarget)
            {
                RpcToggleAimLine(true);
                _targetLocked = true;

                // 자동으로 공격 타이밍을 계산할 때에만 쿨타임 자동 적용
                if (_autoFire)
                {
                    StartCooldown();
                }
            }
        }
    }

    /// <summary>
    /// 
    /// [김도형]
    /// </summary>
    public void Fire()
    {
        if (!isServer)
        {
            return; // 서버에서만 실행
        }

        // 예측 사격 계산
        Vector2 predictedRelativePosition = CalculatePredictedRelativePosition();

        if (predictedRelativePosition == Vector2.zero)
        {
            FireAtAngle(Random.Range(-_aimingAngle, _aimingAngle), (_targetShipTransform.position - transform.position + (Vector3)_randomTargetOffset).magnitude);
        }

        // 랜덤 오프셋 적용
        Vector2 finalTargetPosition = predictedRelativePosition + _randomTargetOffset;

        // 각도 계산
        Vector2 directionToTarget = finalTargetPosition.normalized;
        float angleToTarget = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;

        // 함선의 현재 각도 기준으로 상대 각도 계산
        float shipAngle = _shipTransform.localEulerAngles.z;
        float moduleAngle = transform.localEulerAngles.z;
        float relativeAngle = Mathf.DeltaAngle(shipAngle + moduleAngle, angleToTarget);

        // 사격 가능한 각도 범위 내인지 체크
        bool canFireAtTarget = Mathf.Abs(relativeAngle) <= _aimingAngle;

        if (canFireAtTarget)
        {
            // 직접 사격 가능
            FireAtTarget(finalTargetPosition);
        }
        else
        {
            // 각도 범위를 벗어남 - 경계 각도에서 사격 시도
            float boundaryAngle = relativeAngle > 0 ? _aimingAngle : -_aimingAngle;
            float actualFireAngle = shipAngle + moduleAngle + boundaryAngle;

            FireAtAngle(actualFireAngle, finalTargetPosition.magnitude);
        }

        if (_fireParticleEffect != null)
        {
            RpcShowFireEffect();
        }

        // 네트워크 동기화
        RpcToggleAimLine(false);
        _targetLocked = false;
    }

    private Vector2 CalculatePredictedRelativePosition()
    {
        Vector2 targetPosition = _targetShipTransform.position;
        Vector2 targetVelocity = _targetRigidbody.velocity;
        Vector2 myPosition = transform.position;
        Vector2 myVelocity = _shipRigidbody != null ? _shipRigidbody.velocity : Vector2.zero;

        // 상대 속도 계산
        Vector2 relativeVelocity = targetVelocity - myVelocity;
        Vector2 relativePosition = targetPosition - myPosition;

        // 2차 방정식을 사용한 예측 계산
        float a = Vector2.Dot(relativeVelocity, relativeVelocity) - (_projectileSpeed * _projectileSpeed);
        float b = 2 * Vector2.Dot(relativePosition, relativeVelocity);
        float c = Vector2.Dot(relativePosition, relativePosition);

        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            // 해가 없는 경우 - 단순 예측 사용
            return Vector2.zero;
        }

        float t1 = (-b + Mathf.Sqrt(discriminant)) / (2 * a);
        float t2 = (-b - Mathf.Sqrt(discriminant)) / (2 * a);

        float timeToIntercept = (t1 > 0 && t2 > 0) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);

        if (timeToIntercept < 0)
        {
            // 과거 시점 - 단순 예측 사용
            return Vector2.zero;
        }

        return relativePosition + relativeVelocity * timeToIntercept;
    }

    private void FireAtTarget(Vector2 targetPosition)
    {
        Vector2 direction = targetPosition.normalized;
        float distance = Vector2.Distance(Vector2.zero, targetPosition);

        // 포탄 발사
        FireProjectile(direction, distance);
    }

    private void FireAtAngle(float angle, float distance)
    {
        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        // 포탄 발사
        FireProjectile(direction, distance);
    }

    private void FireProjectile(Vector2 direction, float distance)
    {
        if (_projectilePrefab == null || _firePoint == null)
        {
            return;
        }

        float localAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        GameObject bullet = BulletPool.Get(_projectilePrefab, _firePoint.position, Quaternion.Euler(0f, 0f, localAngle));
        Bullet bulletComponent = bullet.GetComponent<Bullet>();

        if (bulletComponent != null)
        {
            NetworkServer.Spawn(bullet);
        }
        else
        {
            Destroy(bullet);
            return;
        }

        // 총알이 함선을 향하도록 힘 적용
        Vector2 shipVelocity = _shipRigidbody != null ? _shipRigidbody.velocity : Vector2.zero;
        if (shipVelocity == null)
        {
            shipVelocity = Vector2.zero;
        }
        //Asin 입력값 범위 체크
        float asinInput = distance * 9.81f / (_projectileSpeed * _projectileSpeed);

        // 값이 유효 범위를 벗어나면 최대 각도(45도) 사용
        float angle;
        if (asinInput > 1f)
        {
            // 포탄 속도로는 해당 거리에 도달 불가능
            // 최대 각도(45도 = π/4)로 발사
            angle = 45f * Mathf.Deg2Rad;
            Debug.LogWarning($"Target too far for projectile speed. Distance: {distance}, Max reachable: {_projectileSpeed * _projectileSpeed / 9.81f}");
        }
        else if (asinInput < -1f)
        {
            angle = 0f;
        }
        else
        {
            angle = 0.5f * Mathf.Asin(asinInput);
        }
        bulletComponent.ApplyFireData(new FireData
        {
            position = bullet.transform.position,
            rotation = bullet.transform.rotation,
            velocityX = direction * (_projectileSpeed * Mathf.Cos(angle)) + shipVelocity,
            velocityY = _projectileSpeed * Mathf.Sin(angle),
            height = _fireHeight,
            enemyTag = "Ship",
            timestamp = Time.time
        });
    }

    /// <summary>
    /// 함포의 포신을 회전하는 메서드
    /// [조우석 - 2025.07.14.]
    /// </summary>
    private void RotateBarrel()
    {
        // 지금은 각도를 대충 계산하지만 나중에 폴리싱하면서 자연스럽게 다듬기
        // 현재는 마냥 타겟을 향하도록 계산, 범위 밖에 있으면 계산하지 않음
        Vector2 directionToTarget = _targetShipTransform.position - _cannonHead.position;

        if (directionToTarget.magnitude >= _fireRange)
        {
            return;
        }

        Vector2 localDirection = _cannonHead.parent.InverseTransformDirection(directionToTarget);
        float localAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;

        // 조준 각도 제한
        float clampedLocalAngle = Mathf.Clamp(localAngle, -_aimingAngle, _aimingAngle);

        _cannonHead.localRotation = Quaternion.Euler(0f, 0f, clampedLocalAngle); // -90f는 포신이 기본적으로 오른쪽을 향하도록 하기 위함
    }
    /// <summary>
    /// 발사 직전까지 쿨다운을 시작
    /// </summary>
    [Server]
    private void StartCooldown()
    {
        _isOnCooldown = true;

        if (_autoFire)
        {
            SetRandomCooldown();
        }

        SetRandomTargetOffset();
        if (_chargeParticleEffect != null)
        {
            RpcShowChargeEffect();
        }
    }

    [Server]
    public void StartCooldown(float cooldown)
    {
        _isOnCooldown = true;

        SetCooldown(cooldown);

        SetRandomTargetOffset();
        if (_chargeParticleEffect != null)
        {
            RpcShowChargeEffect();
        }
    }

    private void SetRandomCooldown()
    {
        _currentCooldown = _cooldown + Random.Range(-_randomCooldownOffset, _randomCooldownOffset);
    }

    private void SetCooldown(float cooldown)
    {
        _currentCooldown = cooldown;
    }

    private void SetRandomTargetOffset()
    {
        // 랜덤 원형 오프셋 생성
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(0f, _randomTargetRadius);
        _randomTargetOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    /// <summary>
    /// 함포의 타겟을 설정하는 메서드
    /// [조우석 - 2025.07.14.]
    /// </summary>
    /// <param name="target"></param>
    private void SetTarget(Transform target)
    {
        _targetShipTransform = target;
        if (_targetShipTransform != null && _targetShipTransform.TryGetComponent<Rigidbody2D>(out var targetRigidbody))
        {
            _targetRigidbody = targetRigidbody;
        }
        else
        {
            _targetRigidbody = null;
        }
    }

    /// <summary>
    /// LineRenderer 초기 설정
    /// </summary>
    private void SetupAimLineRenderer()
    {
        if (_aimLineRenderer == null) return;

        _aimLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _aimLineRenderer.startColor = _aimLineColor;
        _aimLineRenderer.endColor = new Color(_aimLineColor.r, _aimLineColor.g, _aimLineColor.b, 0.0f); // 투명도 조절
        _aimLineRenderer.startWidth = _aimLineWidth;
        _aimLineRenderer.endWidth = _aimLineWidth;
        _aimLineRenderer.positionCount = 2;
        _aimLineRenderer.useWorldSpace = true;
        _aimLineRenderer.enabled = false;
    }

    /// <summary>
    /// 조준선 표시
    /// </summary>
    private void ShowAimLine()
    {
        if (_aimLineRenderer != null)
        {
            _aimLineRenderer.enabled = true;
        }
    }

    /// <summary>
    /// 조준선 숨김
    /// </summary>
    private void HideAimLine()
    {
        if (_aimLineRenderer != null)
        {
            _aimLineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 조준선 위치 업데이트
    /// </summary>
    private void UpdateAimLine(Vector3 predictedFirePosition)
    {
        if (_aimLineRenderer == null || !_aimLineRenderer.enabled) return;

        Vector3 firePointPos = _firePoint.position + _mainCamera.transform.up * (_fireHeight * _offset);
        Vector3 targetPos = _firePoint.position + predictedFirePosition + (Vector3)_shipRigidbody.velocity * predictedFirePosition.magnitude / _projectileSpeed;

        _aimLineRenderer.SetPosition(0, firePointPos);
        _aimLineRenderer.SetPosition(1, targetPos);
    }

    void OnDrawGizmosSelected()
    {
        if (!_showDebugLines)
        {
            return;
        }

        // 사격 범위 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _fireRange);

        // 사격 각도 범위 표시
        Gizmos.color = Color.red;
        float shipAngle = _shipTransform.localEulerAngles.z;
        float moduleAngle = transform.localEulerAngles.z;
        Vector3 leftBoundary = transform.position + Quaternion.AngleAxis(shipAngle + moduleAngle - _aimingAngle, Vector3.forward) * Vector3.right * _fireRange;
        Vector3 rightBoundary = transform.position + Quaternion.AngleAxis(shipAngle + moduleAngle + _aimingAngle, Vector3.forward) * Vector3.right * _fireRange;

        Gizmos.DrawLine(transform.position, leftBoundary);
        Gizmos.DrawLine(transform.position, rightBoundary);

        // 타겟이 있을 때 예측 위치 표시
        if (_targetShipTransform != null)
        {
            Vector2 predictedRelativePos = CalculatePredictedRelativePosition();
            if (predictedRelativePos != Vector2.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, predictedRelativePos * _fireRange);
            }
        }
    }

    #region 네트워크 동기화용

    [Server]
    public override void SetModuleDestroy()
    {
        _moduleHolder.OnCannonDestroyed(_index);
        _enemyController.TakeDamage(MaxHealth * 2, Vector2.zero);
    }

    [ClientRpc]
    private void RpcShowChargeEffect()
    {
        if (_chargeParticleEffect != null && _mainCamera != null)
        {
            _currentChargeEffect = Instantiate(_chargeParticleEffect, _firePoint.position + _mainCamera.transform.up * (_fireHeight * _offset), Quaternion.identity);
            _currentChargeEffect.transform.SetParent(_firePoint);
        }
        else if (_mainCamera = null)
        {
            Debug.LogWarning($"{gameObject.name}: 참조된 MainCamera가 없어 충전 이펙트를 생성하지 않습니다.");
        }
    }

    [ClientRpc]
    private void RpcShowFireEffect()
    {
        if (_fireParticleEffect != null && _mainCamera != null)
        {
            _currentFireEffect = Instantiate(_fireParticleEffect, _firePoint.position + _mainCamera.transform.up * (_fireHeight * _offset), Quaternion.identity);
            _currentFireEffect.transform.SetParent(_firePoint);
        }
        else if (_mainCamera = null)
        {
            Debug.LogWarning($"{gameObject.name}: 참조된 MainCamera가 없어 충전 이펙트를 생성하지 않습니다.");
        }
    }

    [ClientRpc]
    private void RpcUpdateAimLine(Vector3 predictedFirePosition)
    {
        UpdateAimLine(predictedFirePosition);
    }

    [ClientRpc]
    private void RpcToggleAimLine(bool isEnabled)
    {
        if (isEnabled)
        {
            ShowAimLine();
        }
        else
        {
            HideAimLine();
        }
    }

    #endregion
}