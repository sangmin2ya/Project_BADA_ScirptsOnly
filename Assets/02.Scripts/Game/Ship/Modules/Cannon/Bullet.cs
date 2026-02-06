using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 네트워크 동기화되는 투사체 클래스
/// [박상민 - 2025.06.27]
/// </summary>
public class Bullet : NetworkBehaviour
{
    #region 필드

    [Header("총알 설정")][SerializeField] private float _lifeTime = 60f; // 총알 생존 시간
    [SerializeField] private float _damage = 10f; // 총알 데미지
    [SerializeField] protected string _enemyTag; // 피격 대상 태그

    [SerializeField] private float _initialIgnoreTime = 0.1f; // 발사 직후 충돌 무시 시간

    //[SerializeField] private float _collisionThreshold = 3f;  // 충돌 판정 활성화 높이 임계값
    [SerializeField] private float _customGravity = 9.81f; // 탑다운용 중력값 (양수)
    [Header("패링 가능여부")]
    [SerializeField] protected bool _isAbleToParry = true; // 튕겨나감 가능 여부

    [Header("타격 파티클")][SerializeField] protected GameObject _hitParticlePrefab; // 타격 이펙트 프리팹
    [SerializeField] private GameObject _explosionColliderPrefab; // 폭발 콜라이더

    [Header("포탄 스프라이트")][SerializeField] protected SpriteRenderer _bulletSprite;
    [SerializeField] protected float _offset = 0.5f;

    [Header("타겟 인디케이터 설정")]
    [SerializeField] private bool _showTargetIndicator = true; // 인디케이터 표시 여부
    [SerializeField] private GameObject _targetIndicatorPrefab; // 타겟 인디케이터 프리팹
    [SerializeField] private Color _enemyIndicatorColor = new Color(1f, 0f, 0f, 0.5f); // 적 인디케이터 색상
    [SerializeField] private Color _allyIndicatorColor = new Color(0f, 0.5f, 1f, 0.5f); // 아군 인디케이터 색상
    [SerializeField] private float _indicatorSize = 2f; // 인디케이터 크기
    [SerializeField] private float _indicatorFadeDistance = 2f; // 페이드 시작 거리
    [SerializeField] private bool _showPathLine = true; // 경로선 표시 여부
    [SerializeField] private float _pathLineWidth = 1f; // 경로선 두께

    [Header("동적 충돌 감지")]
    [SerializeField] private float _trajectoryCheckInterval = 0.1f;  // 궤적 체크 간격
    [SerializeField] private LayerMask _shipLayerMask;              // 배 레이어 마스크
    [SerializeField] private float _shipHeightCheckRadius = 5f;     // 배 높이 체크 반경

    protected Vector2 _velocityX; // 수평 속도 (배의 관성 포함)
    protected float _velocityY; // 수직 속도
    [SyncVar] protected float _currentHeight; // 현재 높이 (포물선 궤도상의 높이)
    protected float _startTime;
    protected Camera _mainCamera;

    private GameObject _targetIndicator;
    private LineRenderer _pathLine;
    private SpriteRenderer _indicatorRenderer; // 인디케이터 스프라이트 렌더러
    private Vector2 _targetPosition; // 목표 지점
    private Vector2 _actualCollisionPoint; // 실제 충돌 예상 지점
    private float _initialDistance; // 초기 거리
    private Color _currentIndicatorColor; // 현재 인디케이터 색상
    private bool _willCollideWithShip = false;  // 배와 충돌 예정 여부
    private GameObject _predictedCollisionShip;  // 충돌 예정 배

    //private bool CanCollide => _currentHeight <= _collisionThreshold; // 특정 높이 이하면 충돌 가능

    public string EnemyTag => _enemyTag;
    public Vector2 VelocityX => _velocityX;
    public float VelocityY => _velocityY;
    public float CurrentHeight => _currentHeight;
    public bool IsAbleToParry => _isAbleToParry;
    public GameObject TargetIndicator => _targetIndicator;
    public LineRenderer PathLine => _pathLine;


    #endregion

    #region Unity 생명주기

    public override void OnStartClient()
    {
        base.OnStartClient();
        _mainCamera = GameObject.Find("AgentCamera")?.GetComponent<Camera>();
        // 생존 시간 후 자동 파괴
        Invoke(nameof(DestroyBulletAfterLifeTime), _lifeTime);
    }

    protected virtual void Update()
    {
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.Find("AgentCamera")?.GetComponent<Camera>();
        }

        if (_mainCamera == null)
        {
            return;
        }

        if (_bulletSprite == null)
        {
            return;
        }
        _bulletSprite.transform.position = transform.position + _mainCamera.transform.up * (_currentHeight * _offset);
        _bulletSprite.sortingOrder = Mathf.CeilToInt(_currentHeight * _offset / 0.01f + 1);

        if (!isServer)
        {
            return;
        }
        UpdateIndicators();
    }

    private void FixedUpdate()
    {
        if (!isServer)
        {
            return;
        }

        UpdateParabolicMovement();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 실제 총알만 처리
        if (!isServer || Time.time - _startTime <= _initialIgnoreTime) return;
        var obj = other.GetComponentInParent<DisplayObject>(); // 자기자신 우선 체크
        if (obj != null && obj.TotalHeight > _currentHeight)
        {
            // DisplayObject의 높이가 현재 총알 높이보다 낮으면 충돌 처리
            HandleHit(obj.gameObject);
        }
    }

    #endregion

    #region 총알 설정

    /// <summary>
    /// 네트워크 스폰된 투사체에 물리력을 적용하는 RPC
    /// </summary>
    public void ApplyForceToProjectile(Vector2 velocity)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.velocity = velocity;
        }
        else
        {
            Debug.LogWarning($"발사체에 Rigidbody2D가 없습니다: {gameObject.name}");
        }
    }

    /// <summary>
    /// 총알의 EnemyTag를 변경
    /// [김도형 - 2025.07.28]
    /// </summary>
    public void SetEnemyTag(string enemyTag)
    {
        _enemyTag = enemyTag;
    }

    /// <summary>
    /// 발사 데이터를 적용하는
    /// </summary>
    public virtual void ApplyFireData(FireData fireData)
    {
        transform.position = fireData.position;
        transform.rotation = fireData.rotation;
        _enemyTag = fireData.enemyTag;
        _velocityX = fireData.velocityX;
        _velocityY = fireData.velocityY;
        _currentHeight = fireData.height;
        _startTime = fireData.timestamp;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            //NaN 오류 예외처리 (우석아 막아라 이거)
            if (float.IsNaN(fireData.velocityX.x) || float.IsNaN(fireData.velocityX.y))
            {
                Debug.LogError("Bullet ApplyFireData: fireData.velocityX is NaN");
                return;
            }
            rb.velocity = fireData.velocityX;
        }

        // ★ 타겟 인디케이터 초기화 추가
        if (_showTargetIndicator)
        {
            CalculateActualCollisionPoint();
            RpcCreateTargetIndicator(_targetPosition, _currentIndicatorColor, _indicatorSize);
        }

        if (_showPathLine)
        {
            RpcCreatePathLine(_targetPosition, _currentIndicatorColor);
        }
    }

    #endregion

    #region 피격 처리

    /// <summary>
    /// 피격 처리를 담당하는 함수
    /// </summary>
    [Server]
    protected virtual void HandleHit(GameObject target)
    {
        // 서버에서만 처리
        if (!isServer)
        {
            return;
        }

        if (target.transform.parent != null &&
            target.transform.parent.TryGetComponent<FixedShield>(out FixedShield shield))
        {
            return;
        }

        Debug.Log($"<color=red>총알이 {target.name}에 맞았습니다.</color>");
        // 데미지 처리 (예시)
        if (target.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            damageable.TakeDamage(_damage, target.transform.InverseTransformPoint(transform.position)); // 타겟기준 로컬좌표를 넘겨줌
            if (_explosionColliderPrefab != null)
            {
                GameObject explosionCollider = Instantiate(_explosionColliderPrefab,
                    (Vector2)target.transform.InverseTransformPoint(transform.position) + ShipSpawner.Instance.OriginOffset, Quaternion.identity);
                NetworkServer.Spawn(explosionCollider);
            }
        }

        // 네트워크 파괴 (모든 클라이언트에서 동기화됨)
        NetworkServer.Destroy(gameObject);
    }

    /// <summary>
    /// 피격 이펙트를 표시하는 RPC
    /// </summary>
    [ClientRpc]
    private void RpcShowHitEffect(Vector3 hitPosition)
    {
        // 피격 이펙트 생성 (예시)
        // EffectManager.Instance?.PlayHitEffect(hitPosition);
        Debug.Log($"피격 이펙트 재생: {hitPosition}");
    }

    /// <summary>
    /// 생존 시간 후 총알을 파괴하는 함수
    /// </summary>
    private void DestroyBulletAfterLifeTime()
    {
        // 서버에서만 파괴 처리
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_targetIndicator != null)
        {
            Destroy(_targetIndicator);
        }

        if (_pathLine != null)
        {
            Destroy(_pathLine.gameObject);
        }

        if (_currentHeight < 0)
        {
            GameObject hitParticle = Instantiate(_hitParticlePrefab, transform.position, Quaternion.identity);
            ParticleSystem.ColorOverLifetimeModule colorModule =
                hitParticle.GetComponent<ParticleSystem>().colorOverLifetime;
            colorModule.color = new ParticleSystem.MinMaxGradient(Color.cyan, Color.blue);
            //랜더링 순서 _bulletSprite.sortingOrder +1 하여 설정
            hitParticle.GetComponent<ParticleSystemRenderer>().sortingOrder = _bulletSprite.sortingOrder + 1;
        }
        else
        {
            GameObject hitParticle = Instantiate(_hitParticlePrefab, transform.position, Quaternion.identity);
            //랜더링 순서 _bulletSprite.sortingOrder +1 하여 설정
            hitParticle.GetComponent<ParticleSystemRenderer>().sortingOrder = _bulletSprite.sortingOrder + 1;
        }
    }

    #endregion

    #region 포물선 시스템

    private void UpdateParabolicMovement()
    {
        if (_currentHeight < 0f)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }

        _currentHeight += _velocityY * Time.deltaTime - 0.5f * _customGravity * Time.deltaTime * Time.deltaTime;
        _velocityY -= _customGravity * Time.deltaTime;
    }

    #endregion

    #region 인디케이터 시스템

    /// <summary>
    /// 실제 충돌 지점 계산 (배 높이 고려)
    /// </summary>
    private void CalculateActualCollisionPoint()
    {
        // 기본 착지점 계산
        CalculateTargetPosition();

        // 포물선 궤적상의 충돌 체크
        CheckTrajectoryCollision();

        // 실제 충돌 지점 설정
        if (_willCollideWithShip && _actualCollisionPoint != Vector2.zero)
        {
            _targetPosition = _actualCollisionPoint;
        }

        // 충돌 예정에 따른 색상 변경
        if (_willCollideWithShip)
        {
            // 배와 충돌 예정이면 더 밝은 색으로 표시
            _currentIndicatorColor = (_enemyTag == "Ship") ?
                new Color(1f, 0.5f, 0f, 0.7f) :  // 주황색 (적이 배를 맞출 예정)
                new Color(0f, 1f, 1f, 0.7f);     // 청록색 (아군이 배를 맞출 예정)
        }
        else
        {
            // 일반 착지점 색상
            _currentIndicatorColor = (_enemyTag == "Ship") ? _enemyIndicatorColor : _allyIndicatorColor;
        }
    }

    /// <summary>
    /// 포물선 궤적의 목표 지점을 계산
    /// </summary>
    private void CalculateTargetPosition()
    {
        // 포물선 운동의 착지점 계산
        // t = 2 * velocityY / gravity (총 비행시간)
        float totalFlightTime = (_velocityY + Mathf.Sqrt(_velocityY * _velocityY + 2 * _customGravity * _currentHeight)) / _customGravity;

        // 수평 이동 거리 계산
        Vector2 horizontalDistance = _velocityX * totalFlightTime;

        // 목표 지점 설정
        _targetPosition = new Vector2(
            transform.position.x + horizontalDistance.x,
            transform.position.y + horizontalDistance.y
        );

        _initialDistance = Vector2.Distance(transform.position, _targetPosition);
    }

    /// <summary>
    /// 포물선 궤적상의 충돌 체크
    /// </summary>
    private void CheckTrajectoryCollision()
    {
        _willCollideWithShip = false;
        _predictedCollisionShip = null;
        _actualCollisionPoint = Vector2.zero;

        float maxTime = (_velocityY + Mathf.Sqrt(_velocityY * _velocityY + 2 * _customGravity * _currentHeight)) / _customGravity;

        Vector2 currentPos = transform.position;
        float maxRange = _velocityX.magnitude * maxTime;

        RaycastHit2D[] hits = Physics2D.RaycastAll(currentPos, _velocityX, maxRange, _shipLayerMask);

        float earliestCollisionTime = maxTime;
        GameObject earliestCollisionShip = null;
        Vector2 earliestCollisionPoint = Vector2.zero;

        foreach (RaycastHit2D hit in hits)
        {
            // 태그 체크
            bool isValidTarget = false;
            if (_enemyTag == "Ship" && hit.collider.CompareTag("Ship"))
                isValidTarget = true;
            else if (_enemyTag != "Ship" && hit.collider.CompareTag("Enemy"))
                isValidTarget = true;

            if (!isValidTarget) continue;

            // 배의 높이 가져오기
            DisplayObject displayObj = hit.collider.GetComponentInParent<DisplayObject>();
            if (displayObj == null) continue;

            float shipHeight = displayObj.TotalHeight;

            // 2. 이 배와 충돌하는 시간 t 계산
            float collisionTime = CalculateCollisionTime(shipHeight);

            if (collisionTime < 0)
            {
                float expectedCollisionTime = (hit.point - currentPos).magnitude / VelocityX.magnitude;
                if (_currentHeight + _velocityY * expectedCollisionTime - 0.5f * _customGravity * expectedCollisionTime * expectedCollisionTime < shipHeight)
                    collisionTime = expectedCollisionTime;
                else continue;
            }

            // 3. 시간 t에서의 위치 계산
            Vector2 collisionPosition = GetPositionAtTime(collisionTime);

            // 4. 해당 위치가 실제로 배 범위 내에 있는지 확인
            if (IsPositionInsideShip(collisionPosition, hit.collider))
            {
                // 5. 가장 빨리 충돌하는 배 선택
                if (collisionTime < earliestCollisionTime)
                {
                    earliestCollisionTime = collisionTime;
                    earliestCollisionShip = hit.collider.gameObject;
                    earliestCollisionPoint = collisionPosition;
                }
            }
        }

        // 6. 결과 저장
        if (earliestCollisionShip != null)
        {
            _willCollideWithShip = true;
            _predictedCollisionShip = earliestCollisionShip;
            _actualCollisionPoint = new Vector2(earliestCollisionPoint.x, earliestCollisionPoint.y);

            //Debug.Log($"<color=green>정확한 충돌 예측: 시간={earliestCollisionTime:F2}초, 위치={_actualCollisionPoint}</color>");
        }

        // 충돌 예측이 없으면 기본 착지점 사용
        if (!_willCollideWithShip)
        {
            // 바닥 착지 시간 계산 (높이 = 0)
            float groundTime = CalculateCollisionTime(0);
            if (groundTime > 0)
            {
                Vector2 groundPosition = GetPositionAtTime(groundTime);
                _targetPosition = new Vector2(groundPosition.x, groundPosition.y);
            }
        }
        else
        {
            _targetPosition = _actualCollisionPoint;
        }
    }

    /// <summary>
    /// 특정 높이와 충돌하는 시간 계산
    /// </summary>
    private float CalculateCollisionTime(float targetHeight)
    {
        // 포물선 방정식: h(t) = h0 + v0*t - 0.5*g*t^2
        // targetHeight = _currentHeight + _velocityY*t - 0.5*_customGravity*t^2
        // 0 = -0.5*_customGravity*t^2 + _velocityY*t + (_currentHeight - targetHeight)

        float a = -0.5f * _customGravity;
        float b = _velocityY;
        float c = _currentHeight - targetHeight;

        // 판별식
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            // 해가 없음 - 이 높이에 도달하지 않음
            return -1f;
        }

        // 근의 공식
        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float t1 = (-b + sqrtDiscriminant) / (2 * a);
        float t2 = (-b - sqrtDiscriminant) / (2 * a);

        // 올바른 시간 선택
        // 총알이 상승 중일 때와 하강 중일 때 모두 충돌 가능

        // 둘 다 양수면 더 빠른 시간 (상승 중 충돌)
        if (t1 > 0 && t2 > 0)
        {
            return Mathf.Min(t1, t2);
        }

        // 하나만 양수면 그것을 선택
        if (t1 > 0) return t1;
        if (t2 > 0) return t2;

        // 둘 다 음수면 충돌하지 않음
        return -1f;
    }

    /// <summary>
    /// 위치가 배 범위 내에 있는지 확인
    /// </summary>
    private bool IsPositionInsideShip(Vector2 position, Collider2D shipCollider)
    {
        // 방법 1: Collider2D.OverlapPoint 사용
        if (shipCollider.OverlapPoint(position))
        {
            return true;
        }

        // 방법 2: 거리 기반 체크 (Collider bounds 사용)
        Bounds bounds = shipCollider.bounds;

        return bounds.Contains(position);
    }

    /// <summary>
    /// 특정 시간 t에서의 위치 계산
    /// </summary>
    private Vector2 GetPositionAtTime(float t)
    {
        Vector2 startPos = transform.position;
        return startPos + _velocityX * t;
    }

    /// <summary>
    /// 특정 시간 t에서의 높이 계산
    /// </summary>
    private float GetHeightAtTime(float t)
    {
        return _currentHeight + _velocityY * t - 0.5f * _customGravity * t * t;
    }

    /// <summary>
    /// 특정 위치에서 배와의 충돌 체크
    /// </summary>
    private bool CheckShipCollisionAtPosition(Vector2 position, float bulletHeight, out GameObject ship)
    {
        ship = null;

        // 해당 위치 주변의 배 검색
        Collider2D[] colliders = Physics2D.OverlapCircleAll(position, _shipHeightCheckRadius, _shipLayerMask);

        // 디버그 로그
        if (colliders.Length > 0)
        {
            Debug.Log($"<color=cyan>충돌 체크 - 위치: {position}, 높이: {bulletHeight}, 발견된 콜라이더 수: {colliders.Length}</color>");
        }

        foreach (Collider2D col in colliders)
        {
            // 적 총알이면 우리 배(Ship)와 충돌 체크
            // 아군 총알이면 적 배(Enemy)와 충돌 체크
            if (_enemyTag == "Ship")  // 적 총알
            {
                if (!col.CompareTag("Ship"))  // 우리 배가 아니면 스킵
                {
                    Debug.Log($"<color=gray>적 총알 - Ship 태그 아님: {col.tag}</color>");
                    continue;
                }
            }
            else  // 아군 총알
            {
                if (!col.CompareTag("Enemy"))  // 적 배가 아니면 스킵
                {
                    Debug.Log($"<color=gray>아군 총알 - Enemy 태그 아님: {col.tag}</color>");
                    continue;
                }
            }

            // 배의 높이 컴포넌트 체크 (IEnterable 또는 다른 높이 시스템)
            float shipHeight = GetShipHeight(col.gameObject);

            // 총알 높이가 배 높이보다 낮으면 충돌
            if (bulletHeight <= shipHeight)
            {
                // 정확한 충돌 지점 계산
                Vector2 closestPoint = col.ClosestPoint(position);

                // 충돌 범위 내에 있는지 확인
                if (Vector2.Distance(position, closestPoint) <= 1f) // 임계값
                {
                    ship = col.gameObject;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 배의 높이 가져오기
    /// </summary>
    private float GetShipHeight(GameObject ship)
    {
        DisplayObject obj = ship.GetComponent<DisplayObject>();
        if (obj != null)
        {
            return obj.TotalHeight;
        }

        return 3f;  // 기본 높이
    }

    /// <summary>
    /// 인디케이터 업데이트
    /// </summary>
    private void UpdateIndicators()
    {
        // 주기적으로 충돌 지점 재계산
        CheckTrajectoryCollision();

        // 충돌 예상 지점이 변경되었으면 인디케이터 위치 업데이트
        if (_willCollideWithShip && _actualCollisionPoint != Vector2.zero)
        {
            CheckTrajectoryCollision();

            // 충돌 예상 지점이 변경되었으면 인디케이터 위치 업데이트
            if (_willCollideWithShip && _actualCollisionPoint != Vector2.zero)
            {
                _targetIndicator.transform.position = _actualCollisionPoint;

                // 색상도 업데이트
                if (_indicatorRenderer != null)
                {
                    // 색상도 업데이트
                    if (_indicatorRenderer != null)
                    {
                        Color newColor = (_enemyTag == "Ship") ?
                            new Color(1f, 0.5f, 0f, 0.7f) :
                            new Color(0f, 1f, 1f, 0.7f);
                        _indicatorRenderer.color = newColor;
                    }
                }

                _targetPosition = _actualCollisionPoint;
            }
            else
            {
                // 기본 착지점으로 복원
                CalculateTargetPosition();
                _targetPosition = new Vector2(_targetPosition.x, _targetPosition.y);

                // 색상도 기본으로 복원
                if (_indicatorRenderer != null)
                {
                    Color defaultColor = (_enemyTag == "Ship") ? _enemyIndicatorColor : _allyIndicatorColor;
                    _indicatorRenderer.color = defaultColor;
                }
            }
        }

        // 현재 거리 계산
        float currentDistance = Vector2.Distance(transform.position, _targetPosition);

        // 경로선 업데이트
        if (_pathLine != null)
        {
            // 거리에 따른 투명도 조절
            Color lineColor = _pathLine.material.color;
            lineColor.a = (_currentIndicatorColor.a * 0.3f) * (currentDistance / _initialDistance);
            _pathLine.material.color = lineColor;
        }


        // 인디케이터 페이드 처리
        if (currentDistance < _indicatorFadeDistance && _indicatorRenderer != null)
        {
            float alpha = currentDistance / _indicatorFadeDistance;
            Color color = _indicatorRenderer.color;
            color.a = _currentIndicatorColor.a * alpha;
            _indicatorRenderer.color = color;
        }

        // 인디케이터 애니메이션 (선택적)
        if (_targetIndicator != null)
        {
            float pulse = 1 / _indicatorSize + Mathf.Min(1f, (_targetIndicator.transform.position - transform.position).magnitude * 0.05f);
            float sizeMultiplier = _willCollideWithShip ? 1.5f : 1f;
            _targetIndicator.transform.localScale = Vector3.one * (_indicatorSize * pulse * sizeMultiplier);
        }

        // 인디케이터 업데이트
        if (_targetIndicator != null && _indicatorRenderer != null)
        {
            RpcUpdateIndicatorPosition(_targetPosition, _indicatorRenderer.color, _indicatorRenderer.color.a, _targetIndicator.transform.localScale);
        }
        else
        {
            RpcUpdateIndicatorPosition(_targetPosition, _currentIndicatorColor, _currentIndicatorColor.a, Vector3.one * _indicatorSize);
        }
    }

    #endregion

    #region 클라이언트 시각 동기화
    /// <summary>
    /// 타겟 인디케이터 생성
    /// </summary>
    [ClientRpc]
    private void RpcCreateTargetIndicator(Vector3 targetPosition, Color indicatorColor, float size)
    {
        // 기존 인디케이터 제거
        if (_targetIndicator != null)
        {
            Destroy(_targetIndicator);
        }

        if (_targetIndicatorPrefab != null)
        {
            _targetIndicator = Instantiate(_targetIndicatorPrefab, targetPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("타겟 인디케이터 프리팹이 할당되지 않았습니다.");
            return;
        }

        _indicatorRenderer = _targetIndicator.GetComponent<SpriteRenderer>();

        if (_indicatorRenderer == null)
        {
            _indicatorRenderer = _targetIndicator.GetComponentInChildren<SpriteRenderer>();
        }

        if (_indicatorRenderer != null)
        {
            _indicatorRenderer.color = indicatorColor;
            _indicatorRenderer.sortingOrder = 100; // 배 위에 표시
        }

        _targetIndicator.transform.localScale = Vector3.one * size;
    }

    /// <summary>
    /// 경로선 생성
    /// </summary>
    [ClientRpc]
    private void RpcCreatePathLine(Vector3 targetPosition, Color currentIndicatorColor)
    {
        // 기존 경로선 제거
        if (_pathLine != null)
        {
            Destroy(_pathLine.gameObject);
        }

        GameObject lineObj = new GameObject("BulletPath");
        _pathLine = lineObj.AddComponent<LineRenderer>();

        Material lineMat = new Material(Shader.Find("Sprites/Default"));
        Color lineColor = currentIndicatorColor;
        lineColor.a *= 0.3f;
        lineMat.color = lineColor;
        _pathLine.material = lineMat;

        _pathLine.startWidth = _pathLineWidth;
        _pathLine.endWidth = _pathLineWidth * 0.5f;
        _pathLine.positionCount = 2;
        _pathLine.SetPosition(0, transform.position);
        _pathLine.SetPosition(1, targetPosition);

        _pathLine.sortingOrder = 99;
    }

    [ClientRpc]
    private void RpcUpdateIndicatorPosition(Vector2 newPosition, Color indicatorColor, float alpha, Vector3 indicatorSize)
    {
        if (_targetIndicator != null)
        {
            _targetIndicator.transform.position = newPosition;
            _targetIndicator.transform.localScale = indicatorSize;
            _indicatorRenderer.color = indicatorColor;
        }
        if (_pathLine != null)
        {
            _pathLine.SetPosition(0, transform.position);
            _pathLine.SetPosition(1, newPosition);
        }
    }

    #endregion
}