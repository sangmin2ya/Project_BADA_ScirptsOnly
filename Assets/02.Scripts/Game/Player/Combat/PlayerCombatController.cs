using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 플레이어 근접 공격 컨트롤러
/// [작성자 - 2025.11.13]
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombatController : NetworkBehaviour
{
    #region 공격 상태 Enum

    /// <summary>
    /// 공격 상태
    /// </summary>
    private enum AttackState
    {
        대기,      // 공격하지 않은 상태
        선딜레이,   // 공격 시작 후 0~0.05초
        판정,      // 공격 시작 후 0.05~0.30초
        후딜레이    // 공격 시작 후 0.30~0.45초
    }

    #endregion

    #region SerializeField

    [Header("공격 기본 설정")]
    [SerializeField] private float _baseDamage = 4f;
    [SerializeField] private float _baseAttackRange = 1.2f;  // 캐릭터 크기 기준 배율
    [SerializeField] private float _attackArcAngle = 120f;   // 공격 부채꼴 각도

    [Header("공격 타이밍")]
    [SerializeField] private float _preDelayTime = 0.05f;    // 선딜레이
    [SerializeField] private float _activeTime = 0.25f;      // 판정 프레임
    [SerializeField] private float _postDelayTime = 0.15f;   // 후딜레이

    [Header("공격 이동")]
    [SerializeField] private float _attackLungeDistance = 0.6f;  // 공격 시 전진 거리 (캐릭터 크기 배율)

    [Header("적 피격 반응")]
    [SerializeField] private float _enemyKnockbackDistance = 0.6f;  // 넉백 거리
    [SerializeField] private float _enemyStunDuration = 0.3f;        // 기절 시간

    [Header("레이어 설정")]
    [SerializeField] private LayerMask _enemyLayerMask;     // 적 레이어
    [SerializeField] private LayerMask _playerLayerMask;    // 플레이어 레이어 (팀원)

    [Header("디버그 설정")]
    [SerializeField] private bool _showDebugGizmos = true;
    [SerializeField] private Color _gizmoColor = Color.red;

    [Header("애니메이터 설정")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _attackStateParameterName = "AttackState";

    [Header("이펙트 설정")]
    [SerializeField] private MeleeAttackVisualEffect _visualEffect;

    #endregion

    #region Private Fields

    private PlayerController _playerController;
    private PlayerState _playerState;
    private PlayerLTDController _ltdController;
    private Rigidbody2D _rigidbody;

    private AttackState _currentState = AttackState.대기;
    private float _attackTimer = 0f;
    private int _currentCombo = 0;  // 1 = 첫 타, 2 = 두 타
    private float _comboResetTimer = 0f;
    private const float ComboResetTime = 0.9f;
    private const float TotalAttackTime = 0.45f;

    // 선입력 시스템
    private bool _isAttackBuffered = false;
    private bool _isHoldingAttack = false;

    // 공격 방향
    private Vector2 _lastAttackDirection = Vector2.right;

    // 타격한 적 추적 (중복 타격 방지)
    private HashSet<uint> _hitEnemiesThisAttack = new HashSet<uint>();
    private HashSet<uint> _hitPlayersThisAttack = new HashSet<uint>();

    // 공격 범위 판정 시스템 (확장 가능)
    private IAttackRangeDetector _rangeDetector;

    // 전진 코루틴 참조 (중복 실행 방지)
    private Coroutine _lungeCoroutine;

    #endregion

    #region Properties

    /// <summary>
    /// 현재 공격 가능한 상태인지 확인
    /// </summary>
    public bool CanAttack
    {
        get
        {
            // 플레이어가 조작 가능한 상태여야 함
            if (_playerState != null && !_playerState.IsControllable)
            {
                return false;
            }

            // 모듈에 탑승 중이면 공격 불가
            if (GetComponent<PlayerModuleController>()?.ActiveModule != null)
            {
                return false;
            }

            // 빈손이거나 근접무기를 들고 있어야 함
            if (!_ltdController.IsLifting)
            {
                return true;  // 빈손
            }

            LTDObjectType heldType = _ltdController.LiftedLTDObject.LtdobjectType;
            return heldType == LTDObjectType.근접무기;
        }
    }

    /// <summary>
    /// 현재 공격 중인지 확인
    /// </summary>
    public bool IsAttacking => _currentState != AttackState.대기;

    #endregion

    #region MonoBehaviour

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _playerState = GetComponent<PlayerState>();
        _ltdController = GetComponent<PlayerLTDController>();
        _rigidbody = GetComponent<Rigidbody2D>();

        // 애니메이터가 할당되지 않았다면 자동으로 찾기
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        // 공격 범위 판정 시스템 초기화 (기본: 부채꼴)
        _rangeDetector = new ArcRangeDetector(_attackArcAngle);
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        UpdateAttackState();
        UpdateComboReset();
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showDebugGizmos || _rangeDetector == null)
        {
            return;
        }

        // 공격 범위 시각화
        Vector2 origin = (Vector2)transform.position;
        float range = CalculateAttackRange();
        _rangeDetector.DrawGizmos(origin, _lastAttackDirection, range, _gizmoColor);
    }

    #endregion

    #region 공격 입력 처리

    /// <summary>
    /// 공격 입력 처리 (서버에서 호출)
    /// </summary>
    /// <param name="attackDirection">공격 방향</param>
    /// <param name="isHolding">입력 유지 여부</param>
    public void OnAttackInput(Vector2 attackDirection, bool isHolding)
    {
        if (!isServer)
        {
            return;
        }

        // 공격 가능 상태 체크
        if (!CanAttack)
        {
            return;
        }

        _lastAttackDirection = attackDirection.normalized;
        _isHoldingAttack = isHolding;

        // 현재 상태에 따라 처리
        switch (_currentState)
        {
            case AttackState.대기:
                // 즉시 공격 시작
                StartAttack();
                break;

            case AttackState.선딜레이:
            case AttackState.판정:
                // 너무 이른 입력은 무시
                break;

            case AttackState.후딜레이:
                // 선입력 버퍼링
                _isAttackBuffered = true;
                break;
        }
    }

    /// <summary>
    /// 공격 버튼을 뗐을 때 호출
    /// </summary>
    public void OnAttackRelease()
    {
        if (!isServer)
        {
            return;
        }

        _isHoldingAttack = false;
    }

    #endregion

    #region 공격 상태 관리

    /// <summary>
    /// 공격 상태 업데이트
    /// </summary>
    private void UpdateAttackState()
    {
        if (_currentState == AttackState.대기)
        {
            return;
        }

        _attackTimer += Time.deltaTime;

        // 상태 전환
        if (_attackTimer < _preDelayTime)
        {
            _currentState = AttackState.선딜레이;
        }
        else if (_attackTimer < _preDelayTime + _activeTime)
        {
            if (_currentState == AttackState.선딜레이)
            {
                // 선딜레이 → 판정중 전환 시 공격 판정 시작
                OnAttackActive();
            }
            _currentState = AttackState.판정;
        }
        else if (_attackTimer < TotalAttackTime)
        {
            _currentState = AttackState.후딜레이;
        }
        else
        {
            // 공격 종료
            OnAttackEnd();
        }
    }

    /// <summary>
    /// 새로운 공격 시작
    /// </summary>
    private void StartAttack()
    {
        _currentState = AttackState.선딜레이;
        _attackTimer = 0f;
        _isAttackBuffered = false;
        _hitEnemiesThisAttack.Clear();
        _hitPlayersThisAttack.Clear();

        // 콤보 카운트 증가
        _currentCombo = (_currentCombo % 2) + 1;  // 1 ↔ 2 토글
        _comboResetTimer = 0f;

        // 애니메이션 재생
        if (_animator != null)
        {
            _animator.SetInteger(_attackStateParameterName, _currentCombo);
        }

        // 공격 시 플레이어 전진
        PerformAttackLunge();

        // 모든 클라이언트에 공격 이펙트 재생 (네트워크 동기화)
        RpcPlayAttackEffects(transform.position, _lastAttackDirection, _currentCombo);

        Debug.Log($"<color=cyan>근접 공격 시작 - 콤보: {_currentCombo}타</color>");
    }

    /// <summary>
    /// 모든 클라이언트에서 공격 이펙트 재생 (ClientRpc)
    /// </summary>
    [ClientRpc]
    private void RpcPlayAttackEffects(Vector2 position, Vector2 direction, int comboIndex)
    {
        // 슬래시 마크 이펙트 재생
        if (_visualEffect != null)
        {
            _visualEffect.PlaySlashEffect(position, direction, comboIndex);
        }
    }

    /// <summary>
    /// 공격 판정 활성화 (선딜레이 종료 시)
    /// </summary>
    private void OnAttackActive()
    {
        Vector2 attackOrigin = (Vector2)transform.position;
        float attackRange = CalculateAttackRange();

        // 공격 범위 내 적 감지
        List<Collider2D> hitTargets = _rangeDetector.DetectTargets(
            attackOrigin,
            _lastAttackDirection,
            attackRange,
            _enemyLayerMask
        );

        // 적 타격 처리
        foreach (Collider2D col in hitTargets)
        {
            if (col.TryGetComponent<NetworkIdentity>(out NetworkIdentity netIdentity))
            {
                // 이미 타격한 적은 스킵
                if (!_hitEnemiesThisAttack.Add(netIdentity.netId))
                {
                    continue;
                }

                // 적 피격 처리
                ApplyDamageToEnemy(col.gameObject, netIdentity.netId);
            }
        }

        // 팀원 타격 처리 (대미지 0, 넉백/기절만)
        List<Collider2D> hitPlayers = _rangeDetector.DetectTargets(
            attackOrigin,
            _lastAttackDirection,
            attackRange,
            _playerLayerMask
        );

        foreach (Collider2D col in hitPlayers)
        {
            // 자기 자신은 제외
            if (col.gameObject == gameObject)
            {
                continue;
            }

            if (col.TryGetComponent<NetworkIdentity>(out NetworkIdentity netIdentity))
            {
                // 이미 타격한 플레이어는 스킵
                if (!_hitPlayersThisAttack.Add(netIdentity.netId))
                {
                    continue;
                }

                // 팀원 피격 처리 (대미지 0)
                ApplyKnockbackToPlayer(col.gameObject, netIdentity.netId);
            }
        }
    }

    /// <summary>
    /// 공격 종료 처리
    /// </summary>
    private void OnAttackEnd()
    {
        // 선입력이나 홀드 입력이 있으면 다음 공격 시작
        if (_isAttackBuffered || _isHoldingAttack)
        {
            StartAttack();
        }
        else
        {
            _currentState = AttackState.대기;
            _attackTimer = 0f;

            // 애니메이션을 Idle로 전환
            if (_animator != null)
            {
                _animator.SetInteger(_attackStateParameterName, 0);
            }
        }
    }

    /// <summary>
    /// 콤보 리셋 타이머 업데이트
    /// </summary>
    private void UpdateComboReset()
    {
        if (_currentState == AttackState.대기 && _currentCombo > 0)
        {
            _comboResetTimer += Time.deltaTime;

            if (_comboResetTimer >= ComboResetTime)
            {
                _currentCombo = 0;
                _comboResetTimer = 0f;
                Debug.Log("<color=yellow>콤보 리셋</color>");
            }
        }
    }

    #endregion

    #region 공격 액션

    /// <summary>
    /// 공격 시 플레이어 전진
    /// </summary>
    private void PerformAttackLunge()
    {
        // 기존 전진 코루틴이 있으면 중지
        if (_lungeCoroutine != null)
        {
            StopCoroutine(_lungeCoroutine);
        }

        _lungeCoroutine = StartCoroutine(LungeCoroutine());
    }

    /// <summary>
    /// 전진 코루틴 - 일정 시간 동안 힘을 가함
    /// </summary>
    private System.Collections.IEnumerator LungeCoroutine()
    {
        float lungeDistance = _attackLungeDistance;
        float lungeDuration = 0.1f;  // 가속 지속 시간 (0.1초)

        // 힘 계산 (질량 고려)
        // Force = Mass * Acceleration
        // 목표 거리에 도달하기 위한 가속도 계산
        float mass = _rigidbody.mass;
        float acceleration = (lungeDistance * 2f) / (lungeDuration * lungeDuration);
        Vector2 forceToApply = _lastAttackDirection * (mass * acceleration);

        // AddForce는 매 FixedUpdate마다 적용되므로 FixedDeltaTime으로 조정
        Vector2 forcePerFrame = forceToApply * Time.fixedDeltaTime;

        float elapsed = 0f;

        // 일정 시간 동안만 가속
        while (elapsed < lungeDuration)
        {
            // 매 프레임 벽 충돌 체크
            RaycastHit2D checkHit = Physics2D.Raycast(
                transform.position,
                _lastAttackDirection,
                0.1f,
                LayerMask.GetMask("Default", "Wall")
            );

            if (checkHit.collider != null)
            {
                // 벽에 닿으면 즉시 중지 (momentum 유지)
                yield break;
            }

            // AddForce로 힘 적용
            _rigidbody.AddForce(forcePerFrame, ForceMode2D.Force);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 가속 종료 - momentum 유지
    }

    /// <summary>
    /// 공격 범위 계산
    /// </summary>
    private float CalculateAttackRange()
    {
        return _baseAttackRange;
    }

    /// <summary>
    /// 공격 상태 초기화
    /// </summary>
    public void ResetAttackState()
    {
        _currentState = AttackState.대기;
        _attackTimer = 0f;
        _currentCombo = 0;
        _comboResetTimer = 0f;
        _isAttackBuffered = false;
        _isHoldingAttack = false;
        _hitEnemiesThisAttack.Clear();
        _hitPlayersThisAttack.Clear();

        // 애니메이터를 Idle로 전환
        if (_animator != null)
        {
            _animator.SetInteger(_attackStateParameterName, 0);
        }
    }

    #endregion

    #region 대미지 및 피격 처리

    /// <summary>
    /// 적에게 대미지 적용
    /// </summary>
    private void ApplyDamageToEnemy(GameObject enemy, uint enemyNetId)
    {
        // 내부 적 (InternalEnemy)인 경우 - Health와 Stun을 분리하여 처리
        if (enemy.TryGetComponent<InternalEnemyHealthController>(out InternalEnemyHealthController healthController))
        {
            // 1. 체력 감소
            bool isDead = healthController.TakeDamage(_baseDamage);

            /*
            // 2. 스턴 적용 (사망하지 않았을 때만)
            if (!isDead && enemy.TryGetComponent<InternalEnemyStunHandler>(out InternalEnemyStunHandler stunHandler))
            {
                stunHandler.ApplyStun(_enemyStunDuration); // 기획서: 0.3초
            }

            // 3. 넉백 적용
            Vector2 knockbackDirection = _lastAttackDirection;
            float knockbackDistance = _enemyKnockbackDistance;

            ApplyKnockback(enemy, knockbackDirection, knockbackDistance);
            */

            // 4. 모든 클라이언트에 타격 이펙트 재생 (네트워크 동기화)
            RpcPlayHitEffect(enemy.transform.position);

            Debug.Log($"<color=red>내부 적 타격 - NetID: {enemyNetId}, 대미지: {_baseDamage}, 남은 체력: {healthController.CurrentHealth}/{healthController.MaxHealth}</color>");
        }
        // 외부 적 (ExternalEnemy) 등 다른 타입의 적 처리
        else if (enemy.TryGetComponent<IEnemyController>(out IEnemyController enemyController))
        {
            // 외부 적의 경우 리플렉션으로 TakeDamage(float, Vector2) 호출
            Vector2 hitPoint = enemy.transform.position;
            var takeDamageMethod = enemyController.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(Vector2) });
            if (takeDamageMethod != null)
            {
                takeDamageMethod.Invoke(enemyController, new object[] { _baseDamage, hitPoint });
            }

            // 넉백 적용
            Vector2 knockbackDirection = _lastAttackDirection;
            float knockbackDistance = _enemyKnockbackDistance;

            ApplyKnockback(enemy, knockbackDirection, knockbackDistance);

            // 기절 적용
            ApplyStun(enemy, _enemyStunDuration);

            // 모든 클라이언트에 타격 이펙트 재생 (네트워크 동기화)
            RpcPlayHitEffect(enemy.transform.position);

            Debug.Log($"<color=red>외부 적 타격 - NetID: {enemyNetId}, 대미지: {_baseDamage}</color>");
        }
    }

    /// <summary>
    /// 모든 클라이언트에서 타격 이펙트 재생 (ClientRpc)
    /// </summary>
    [ClientRpc]
    private void RpcPlayHitEffect(Vector2 hitPosition)
    {
        // 타격 이펙트 재생
        if (_visualEffect != null)
        {
            _visualEffect.PlayHitEffect(hitPosition);
        }
    }

    /// <summary>
    /// 팀원에게 넉백 적용 (대미지 0)
    /// </summary>
    private void ApplyKnockbackToPlayer(GameObject player, uint playerNetId)
    {
        // 넉백만 적용 (대미지 없음)
        Vector2 knockbackDirection = _lastAttackDirection;
        float knockbackDistance = _enemyKnockbackDistance;

        ApplyKnockback(player, knockbackDirection, knockbackDistance);

        // 기절 적용
        ApplyStun(player, _enemyStunDuration);

        Debug.Log($"<color=yellow>팀원 타격 - NetID: {playerNetId}, 대미지: 0 (넉백만)</color>");
    }

    /// <summary>
    /// 넉백 적용
    /// </summary>
    private void ApplyKnockback(GameObject target, Vector2 direction, float distance)
    {
        if (target.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            Vector2 targetPosition = (Vector2)target.transform.position + direction * distance;

            // 벽 충돌 체크
            RaycastHit2D hit = Physics2D.Raycast(
                target.transform.position,
                direction,
                distance,
                LayerMask.GetMask("Default", "Wall")
            );

            if (hit.collider != null)
            {
                targetPosition = hit.point;
            }

            rb.MovePosition(targetPosition);
        }
    }

    /// <summary>
    /// 기절 적용
    /// </summary>
    private void ApplyStun(GameObject target, float duration)
    {
        // PlayerState의 ApplyStun 메서드 호출 (플레이어인 경우)
        if (target.TryGetComponent<PlayerState>(out PlayerState playerState))
        {
            // 기절 시간을 파라미터로 받을 수 있도록 PlayerState 수정 필요
            playerState.ApplyStun();
        }

        // 적 기절 처리 (IEnemyController 인터페이스에 Stun 메서드 있다고 가정)
        if (target.TryGetComponent<IEnemyController>(out IEnemyController enemy))
        {
            // enemy.ApplyStun(duration);  // 적 스턴 메서드 호출
        }
    }

    #endregion

    #region 공격 범위 판정 시스템 교체 (확장성)

    /// <summary>
    /// 공격 범위 판정 방식을 변경
    /// </summary>
    /// <param name="detector">새로운 판정 시스템</param>
    public void SetRangeDetector(IAttackRangeDetector detector)
    {
        _rangeDetector = detector;
        Debug.Log($"<color=green>공격 범위 판정 방식 변경: {detector.GetType().Name}</color>");
    }

    /// <summary>
    /// 기본 부채꼴 판정으로 복원
    /// </summary>
    public void ResetToDefaultDetector()
    {
        _rangeDetector = new ArcRangeDetector(_attackArcAngle);
    }

    #endregion

    #region 공개 유틸리티

    /// <summary>
    /// 공격 범위 각도 변경 (런타임)
    /// </summary>
    public void SetAttackArcAngle(float newAngle)
    {
        _attackArcAngle = newAngle;
        if (_rangeDetector is ArcRangeDetector)
        {
            _rangeDetector = new ArcRangeDetector(_attackArcAngle);
        }
    }

    /// <summary>
    /// 공격 범위 거리 변경 (런타임)
    /// </summary>
    public void SetAttackRange(float newRange)
    {
        _baseAttackRange = newRange;
    }

    #endregion
}