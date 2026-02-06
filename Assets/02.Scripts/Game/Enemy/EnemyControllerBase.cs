using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 적의 기본적인 동작을 수행하는 중심 컨트롤러
/// [조우석 - 2025.08.12.]
/// </summary>
public class EnemyControllerBase : NetworkBehaviour, IEnemyController
{
    [Header("타겟 감지 (Base)")]
    [Tooltip("타겟 Transform")]
    [SerializeField] protected Transform _currentTarget;
    [SyncVar(hook = nameof(OnTargetNetIdChanged))] private uint _targetNetId = 0;
    protected Rigidbody2D _targetRigidbody;
    [Space]
    [Tooltip("타겟을 감지할 레이어")]
    [SerializeField] private LayerMask _targetLayer;
    [Tooltip("타겟 추적을 놓치는 거리")]
    [SerializeField] private float _loseTargetRadius = 50f;
    [Tooltip("타겟을 감지하는 간격")]
    [SerializeField] private float _scanInterval = 1.0f;
    [Tooltip("타겟이 유효한지 감지하는 간격")]
    [SerializeField] private float _validationInterval = 0.2f;
    [Tooltip("새 타겟을 감지하기 시작하는 거리")]
    [SerializeField] private float _detectionRadius;
    [Space]
    [Tooltip("벽이 있으면 타겟을 감지하지 못하는가")]
    [SerializeField] private bool _blockDetectionByWall = false;
    [Tooltip("타겟 감지를 가로막을 벽 레이어. _blockDetectionByWall가 false라면 설정 불필요")]
    [SerializeField] private LayerMask _wallLayer;
    [Space]
    [Tooltip("탐지하고 있던 타겟을 잃어버리면 스스로 파괴한다")]
    [SerializeField] private bool _shouldDestroyOnTargetLoss = false;
    [Tooltip("타겟을 잃고 파괴되기까지 유예시간. 해당 시간 내 다시 타겟을 감지하면 파괴되지 않음")]
    [SerializeField] private float _targetLossDestructionDelay = 1;
    private float _targetLossTimer = 0;
    private bool _willDestroy => _targetLossTimer > 0 && _shouldDestroyOnTargetLoss;

    [Header("상태 머신 (Base)")]
    [Tooltip("시작 기본 상태")]
    [SerializeField] protected EnemyBaseState _startState;
    private EnemyBaseState[] _availableStates;

    [Tooltip("적 오브젝트 동작 플래그(true 시 동작)")]
    [SyncVar] private bool _isLogicEnabled = true;
    private int _wallLayerIndex = 0;

    [Header("컴포넌트 참조 (Base)")]
    private Rigidbody2D _rigidbody;
    public event Action<Transform> OnTargetChanged;

    #region IEnemyController & ITargetDetector
    public Transform CurrentTarget => _currentTarget;
    public bool HasTarget => _targetNetId != 0;
    public EnemyStateMachine StateMachine { get; private set; }
    public Rigidbody2D Rigidbody => _rigidbody;
    public Rigidbody2D TargetRigidbody => _targetRigidbody;
    public bool HasCollidedWithWallThisFrame { get; private set; }
    #endregion

    [SyncVar] protected float _currentSpeed;
    public float CurrentSpeed => _currentSpeed;
    private float _scanTimer;
    private float _validationTimer;
    private readonly Collider2D[] _targetCandidates = new Collider2D[10];

    [Header("Debug")]
    [SerializeField] private bool _willDrawGizmos = false;

    protected virtual void Awake()
    {
        Debug.Log($"<color=cyan>[{gameObject.name}/Controller]</color> Initializing Base...");
        StateMachine = gameObject.AddComponent<EnemyStateMachine>();
        _rigidbody = GetComponent<Rigidbody2D>();

        _wallLayerIndex = LayerMask.NameToLayer("Wall");

        _availableStates = GetComponents<EnemyBaseState>();
        Debug.Log($"<color=cyan>[{gameObject.name}/Controller]</color> Found {_availableStates.Length} state components.");

        foreach (var state in _availableStates)
        {
            state.Setup(this, StateMachine);
        }

        if (_startState == null && _availableStates.Length > 0)
        {
            _startState = _availableStates.First();
        }
    }

    protected virtual void Start()
    {
        if (_availableStates.Length > 0)
        {
            StateMachine.Initialize(_startState);
            _scanTimer = _scanInterval;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}/Controller] No state components found! AI will be inactive.");
            this.enabled = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 서버에서만 충돌 로직을 처리
        if (!isServer)
        {
            return;
        }

        // 벽과 충돌했는지 확인
        if (collision.gameObject.layer == _wallLayerIndex)
        {
            HasCollidedWithWallThisFrame = true;
            //Debug.Log("벽 충돌 감지! 플래그를 true로 설정.");
        }
    }

    protected virtual void Update()
    {
        if (!isServer)
        {
            return;
        }
        // 동작 플래그
        if (!_isLogicEnabled)
        {
            return;
        }
        StateMachine.CurrentState?.StateUpdate();
        DetectTarget();

        if (_willDestroy)
        {
            CheckDestroyTimer();
        }

        if (HasTarget)
        {
            _currentSpeed = _rigidbody.velocity.magnitude;
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!isServer)
        {
            return;
        }
        // 동작 플래그
        if (!_isLogicEnabled)
        {
            return;
        }
        StateMachine.CurrentState?.StatePhysicsUpdate();
    }

    protected virtual void LateUpdate()
    {
        // 매 프레임이 끝날 때 플래그를 초기화합니다.
        HasCollidedWithWallThisFrame = false;
    }

    /// <summary>
    /// 적 오브젝트의 동작 플래그 설정
    /// [2025.09.25 - 박상민]
    /// </summary>
    protected void SetLogicEnabled(bool isEnabled)
    {
        if (!isServer)
        {
            return;
        }
        _isLogicEnabled = isEnabled;
        if (!isEnabled)
        {
            StateMachine.ChangeState(null); // 상태 초기화
        }
    }

    /// <summary>
    /// 타겟을 감지하는 메서드
    /// - 일정 시간마다 타겟의 유효성을 검사하고, 현재 타겟이 없으면 새 타겟을 찾음
    /// </summary>
    public void DetectTarget()
    {
        if (HasTarget)
        {
            _validationTimer -= Time.deltaTime;
            if (_validationTimer <= 0f)
            {
                _validationTimer = _validationInterval;
                ValidateCurrentTarget();
            }
        }
        else
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                ScanForNewTarget();
            }

            if (_shouldDestroyOnTargetLoss && !_willDestroy && !HasTarget)
            {
                // 타겟이 없고, 파괴 카운트 중이 아니고, 탐지 후 여전히 타겟이 없으면 파괴 명령
                CheckTargetLossDestroy();
            }
        }
    }

    /// <summary>
    /// 새 타겟을 찾음
    /// </summary>
    private void ScanForNewTarget()
    {
        int numFound = Physics2D.OverlapCircleNonAlloc(transform.position, _detectionRadius, _targetCandidates, _targetLayer);
        if (numFound > 0)
        {
            var newTarget = _targetCandidates[0];
            if (_currentTarget != newTarget.transform)
            {
                if (_blockDetectionByWall && !CastToTarget(newTarget.attachedRigidbody))
                {
                    // 타겟이 벽에 의해 가려지면 return
                    return;
                }

                if (_willDestroy)
                {
                    // 파괴 카운트다운 중일 때 새 타겟을 찾으면 카운트다운 취소
                    _targetLossTimer = 0;
                }

                _currentTarget = newTarget.transform;
                _targetNetId = _currentTarget.GetComponent<NetworkIdentity>()?.netId ?? 0;
                _currentTarget.TryGetComponent(out _targetRigidbody);
                OnTargetChanged?.Invoke(_currentTarget);
                Debug.Log($"<color=green>[{gameObject.name}/Controller]</color> New target acquired: {CurrentTarget.name}");
            }
        }
    }

    /// <summary>
    /// 현재 타겟에 대해서 유효성 검사를 함
    /// </summary>
    private void ValidateCurrentTarget()
    {
        if (_currentTarget == null)
        {
            return;
        }
        float distance = Vector2.Distance(transform.position, _currentTarget.position);
        if (distance > _loseTargetRadius)
        {
            Debug.Log($"<color=orange>[{gameObject.name}/Controller]</color> Target {CurrentTarget.name} lost (out of range).");
            _currentTarget = null;
            _targetNetId = 0;
            _targetRigidbody = null;

            OnTargetChanged?.Invoke(null);
        }
    }

    /// <summary>
    /// 타겟을 향해 Cast후 결과를 반환
    /// </summary>
    /// <returns>true: 타겟까지 장애물이 없음 / false: 타겟에 문제가 있거나 장애물이 있음</returns>
    private bool CastToTarget(Rigidbody2D targetRb)
    {
        if (targetRb == null)
        {
            return false;
        }
        Vector2 directionToTarget = targetRb.position - (Vector2)transform.position;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget.normalized, directionToTarget.magnitude, _wallLayer);
        if (hit.collider != null)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// 파괴 예정 시 카운트다운 체크
    /// </summary>
    private void CheckDestroyTimer()
    {
        _targetLossTimer -= Time.deltaTime;
        if (_targetLossTimer <= 0)
        {
            DoTargetLossDestroy();
        }
    }

    /// <summary>
    /// 타겟을 상실했을 때 언제 파괴시킬 것인지 체크
    /// - _shouldDestroyOnTargetLoss가 false라면 동작하지 않음
    /// </summary>
    private void CheckTargetLossDestroy()
    {
        if (_targetLossDestructionDelay <= 0)
        {
            // 즉시 파괴
            DoTargetLossDestroy();
        }
        else
        {
            // 타이머 시작
            _targetLossTimer = _targetLossDestructionDelay;
        }
    }

    /// <summary>
    /// 타겟 상실에 따른 파괴 동작
    /// </summary>
    protected virtual void DoTargetLossDestroy()
    {
        if (!isServer)
        {
            return;
        }
        NetworkServer.Destroy(gameObject);
    }

    private void OnTargetNetIdChanged(uint oldNetId, uint newNetId)
    {
        if (newNetId == 0)
        {
            _currentTarget = null;
            _targetRigidbody = null;
            OnTargetChanged?.Invoke(null);
            Debug.Log($"<color=orange>[{gameObject.name}/Controller]</color> Target cleared via SyncVar.");
        }
        else if (newNetId != oldNetId)
        {
            NetworkIdentity targetIdentity = NetworkServer.spawned.ContainsKey(newNetId) ? NetworkServer.spawned[newNetId] : null;
            if (targetIdentity != null)
            {
                _currentTarget = targetIdentity.transform;
                _currentTarget.TryGetComponent(out _targetRigidbody);
                OnTargetChanged?.Invoke(_currentTarget);
                Debug.Log($"<color=green>[{gameObject.name}/Controller]</color> Target updated via SyncVar: {_currentTarget.name}");
            }
            else
            {
                Debug.LogWarning($"<color=red>[{gameObject.name}/Controller]</color> Target with NetId {newNetId} not found on server.");
                _currentTarget = null;
                _targetRigidbody = null;
                OnTargetChanged?.Invoke(null);
            }
        }
    }

    public void OnDrawGizmos()
    {
        if (_willDrawGizmos)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);
            Gizmos.DrawWireSphere(transform.position, _loseTargetRadius);
        }
    }
}
