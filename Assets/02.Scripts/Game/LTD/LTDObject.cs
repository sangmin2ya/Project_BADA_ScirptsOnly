using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// LTD 가능한 오브젝트의 부모 클래스
/// [박상민 - 2025.06.11]
/// </summary>
[RequireComponent(typeof(NetworkIdentity), typeof(NetworkTransformUnreliable))]
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D), typeof(PhysicsHandler))]
public class LTDObject : NetworkBehaviour, IInteractable
{
    [Header("LTD오브젝트 설정")]
    [Tooltip("LTD 오브젝트의 타입")]
    [SerializeField] protected LTDObjectType _ltdObjectType;
    [Tooltip("LTD 오브젝트의 크기")]
    [SerializeField] protected LTDObjectSize _ltdObjectSize;

    [Header("높이 시스템")]
    [Tooltip("오브젝트의 현재 높이")]
    [SerializeField] private float _currentHeight = 0.2f;
    [Tooltip("중력값")]
    [SerializeField] private float _customGravity = 9.81f;
    [Tooltip("수평 속도")]
    [SerializeField] private Vector2 _horizontalVelocity = Vector2.zero;
    [Tooltip("수직 속도")]
    [SerializeField] private float _verticalVelocity = 0f;
    [Tooltip("높이가 0이 되었을 때 멈추는지 여부")]
    [SerializeField] private bool _stopWhenHeightZero = true;

    [Header("<color=yellow>LTD오브젝트 상태")]
    [Tooltip("해당 LTD 오브젝트를 들고 있는 플레이어")]
    protected PlayerLTDController _liftingPlayer;
    [Tooltip("해당 LTD 오브젝트를 드랍할 부모")]
    protected Transform _dropParent = null;
    [Tooltip("던진 플레이어")]
    protected uint _throwerNetId = 0;
    [Tooltip("몇 초 동안 최근에 던진 오브젝트라고 판단할 것인가. 던지자마자 던진 플레이어가 충돌하는 것을 방지")]
    [SerializeField] private float _checkRecentlyThrownTime = 0.1f;
    private float _recentlyThrownTimer = 0f;

    [Header("LTD오브젝트 참조")]
    [Tooltip("포커싱 효과")]
    [SerializeField] private GameObject _focusOutline;

    [Header("레이어 관련")]
    [Tooltip("기존 레이어")]
    protected int _originalLayer = 0;

    // 참조
    protected Rigidbody2D _rigidbody;
    protected PhysicsHandler _physicsHandler;
    protected SpriteRenderer _spriteRenderer;
    protected NetworkTransformUnreliable _networkTransform;

    // 프로퍼티
    public LTDObjectType LtdobjectType => _ltdObjectType;
    public LTDObjectSize LtdObjectSize => _ltdObjectSize;
    public bool IsBeingLifted => _liftingPlayer != null;
    public bool IsStunnable => _physicsHandler.CanStunBySpeed/* && !_wasRecentlyThrown*/;
    public PlayerLTDController LiftingPlayer => _liftingPlayer;
    public uint ThrowerNetId => _throwerNetId;
    public float CurrentHeight => _currentHeight;
    public bool IsAbleToLift => !IsBeingLifted && (_rigidbody.velocity.magnitude < 0.1f || _currentHeight <= 0.2f); // 임시로 속도가 0.5 이하일때만 들수있게

    #region  IInteractable 구현
    public virtual InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
            return new InteractionInfo();
        }

        string text = "들기";
        string action = "Player/LiftDrop";
        bool interactable = IsAbleToLift;
        Vector3 position = transform.position + Vector3.up * (_currentHeight + 0.5f); // 높이 반영

        return new InteractionInfo(netId, text, action, interactable, position);
    }
    #endregion

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _physicsHandler = GetComponent<PhysicsHandler>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _networkTransform = GetComponent<NetworkTransformUnreliable>();
    }

    // /// <summary>
    // /// 함선 내부로 들어가면 함선부모설정, 나가면 null 설정
    // /// </summary>
    // protected virtual void OnTriggerEnter2D(Collider2D other)
    // {
    //     if (other.CompareTag("Ship"))
    //     {
    //         Debug.Log($"LTDObject {gameObject.name} entered ship {other.name}");
    //         // 함선에 들어가면 부모를 함선으로 설정
    //         _dropParent = other.transform;
    //         transform.SetParent(_dropParent, true); // 부모 설정 후 위치 초기화 - 나중에 네트워크 동기화 필요
    //     }
    // }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // 초기 상태 설정
        _dropParent = null;
        _liftingPlayer = null;
        _originalLayer = gameObject.layer;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // 클라이언트에서의 초기화 작업
        //GetComponent<Collider2D>().enabled = false; // 클라이언트에서는 충돌체 비활성화
    }

    protected virtual void FixedUpdate()
    {
        if (!isServer)
        {
            return;
        }


        // 들어올려진 상태에서의 업데이트 로직
        if (IsBeingLifted)
        {
            transform.position = _liftingPlayer.LiftPivot.position;
        }
        else //들리지 않았으면 중력적용
        {
            // 높이 시스템 업데이트
            UpdateHeightSystem();
        }

        if (_recentlyThrownTimer > 0)
        {
            _recentlyThrownTimer -= Time.fixedDeltaTime;
            if (_recentlyThrownTimer <= 0)
            {
                ClearThrowerId();
                _recentlyThrownTimer = 0;
            }
        }
    }

    protected virtual void Update()
    {
        if (!isServer)
        {
            return;
        }
    }

    public override void OnStopServer()
    {
        // 모든 클라이언트에서 상호작용 프롬프트 숨기기
        RpcHidePromptOnDestroy();
        base.OnStopServer();
    }

    [ClientRpc]
    protected void RpcHidePromptOnDestroy()
    {
        Debug.Log("LTDObject가 파괴되어 상호작용 프롬프트를 숨깁니다.");
        // 모든 클라이언트에서 상호작용 프롬프트 숨기기
        InteractionPromptManager.Instance.HidePrompt(netId);
    }

    #region 높이 시스템

    /// <summary>
    /// 높이 시스템 업데이트 (포물선 운동)
    /// [김도형 - 2025.09.15]
    /// </summary>
    private void UpdateHeightSystem()
    {
        if (_stopWhenHeightZero && _currentHeight <= 0.2f)
        {
            // 높이가 0 이하가 되면 멈춤
            HandleLanding();
            return;
        }
        _rigidbody.drag = 0f;

        // 수직 속도 업데이트 (중력 적용)
        _verticalVelocity -= _customGravity * Time.fixedDeltaTime;

        // 높이 업데이트
        _currentHeight += _verticalVelocity * Time.fixedDeltaTime;

        // 높이가 0 이하가 되면 착지 처리
        if (_currentHeight <= 0.2f)
        {
            HandleLanding();
        }

        // DisplayObject가 있다면 높이 반영
        if (TryGetComponent<DisplayObject>(out DisplayObject displayObj))
        {
            RpcSetObjectDepth(_currentHeight);
        }
    }

    /// <summary>
    /// 착지 처리
    /// [김도형 - 2025.09.15]
    /// </summary>
    private void HandleLanding()
    {
        _currentHeight = 0.2f;
        _verticalVelocity = 0f;
        _rigidbody.drag = 10f;
    }

    /// <summary>
    /// 던지기 시 높이와 속도 설정
    /// [김도형 - 2025.09.15]
    /// </summary>
    public void SetThrowVelocity(Vector2 horizontalVel, float verticalVel, float initialHeight)
    {
        // 던지기 힘 적용
        _rigidbody.velocity = horizontalVel;

        _horizontalVelocity = horizontalVel;
        _verticalVelocity = verticalVel;
        _currentHeight = initialHeight;
    }

    #endregion

    #region LTD 관련 메소드

    [TargetRpc]
    public virtual void Focused(NetworkConnection conn, bool isFocused)
    {
        // 포커싱 효과 활성화/비활성화
        if (_focusOutline != null)
        {
            _focusOutline.SetActive(isFocused);
        }
    }

    /// <summary>
    /// LTD 오브젝트가 들어올려졌을때 (들어올린 플레이어가 실행함`)
    /// </summary>
    /// <param name="player">들어올린 플레이어</param>
    /// <returns>성공적으로 들어올렸다면 true, 실패했다면 false</returns>
    [Server]
    public virtual bool Lifted(PlayerLTDController player)
    {
        if (IsBeingLifted)
        {
            return false; // 이미 들어올려진 상태입니다.
        }
        _liftingPlayer = player;
        _focusOutline.SetActive(false);
        // 물리 상태 초기화
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.totalTorque = 0;

        // 높이 시스템 초기화
        _currentHeight = player.LiftHeight;
        _verticalVelocity = 0f;
        _horizontalVelocity = Vector2.zero;

        GetComponent<DisplayObject>()?.ChangeDepthTo(player.LiftHeight); // 로컬에서 우선 처리
        //CmdSetObjectDepth(player.LiftHeight); // 오브젝트 깊이를 플레이어 키높이로 설정
        //gameObject.layer = LayerMask.NameToLayer("Ignore Collision");
        GetComponent<Collider2D>().isTrigger = true; // 충돌체 비활성화

        if (TryGetComponent<DisplayObject>(out DisplayObject displayObj))
        {
            RpcSetObjectDepth(player.LiftHeight);
        }
        return true;
    }

    /// <summary>
    /// LTD 오브젝트가 드랍되었을때
    /// </summary>
    [Server]
    public virtual void Dropped()
    {
        if (!IsBeingLifted)
        {
            return;
        }
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.totalTorque = 0;
        //gameObject.layer = _originalLayer; // 드랍 후에는 기본 레이어로 변경
        GetComponent<Collider2D>().isTrigger = false; // 충돌체 활성화

        // 높이 시스템 초기화
        _currentHeight = 0.2f;
        _verticalVelocity = 0f;
        _horizontalVelocity = Vector2.zero;

        if (TryGetComponent<DisplayObject>(out DisplayObject displayObj))
        {
            RpcSetObjectDepth(0.1f);
        }
        _liftingPlayer = null;
    }

    /// <summary>
    /// LTD 오브젝트가 리셋되었을때(LTD인 플레이어가 리스폰되거나 부활됐을때)
    /// </summary>
    public void Reset()
    {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.totalTorque = 0;
        //gameObject.layer = _originalLayer; // 리셋 후에는 기본 레이어로 변경
        GetComponent<Collider2D>().isTrigger = false; // 충돌체 활성화

        // 높이 시스템 초기화
        _currentHeight = 0.2f;
        _verticalVelocity = 0f;
        _horizontalVelocity = Vector2.zero;

        if (TryGetComponent<DisplayObject>(out DisplayObject displayObj))
        {
            RpcSetObjectDepth(0.1f);
        }
        _liftingPlayer = null;
    }

    /// <summary>
    /// [SERVER-side] 서버단에서 오브젝트 던지기 적용
    /// [박상민 - 2025.09.08]
    /// </summary>
    [Server]
    public virtual void Thrown(ThrowData throwData, uint throwerId)
    {
        //if (!IsBeingLifted)
        //{
        //    return;
        //}
        //_liftingPlayer = null;
        //GetComponent<Collider2D>().isTrigger = false; // 충돌체 활성화

        //_throwerNetId = throwerId;
        //_rigidbody.velocity = throwForce; // 던지기 힘 적용
        //_recentlyThrownTimer = _checkRecentlyThrownTime;

        if (!IsBeingLifted)
        {
            return;
        }
        _liftingPlayer = null;
        GetComponent<Collider2D>().isTrigger = false; // 충돌체 활성화

        _throwerNetId = throwerId;

        // 크기에 따른 힘 조정
        Vector2 adjustedHorizontalVel = throwData.horizontalVelocity;
        float adjustedVerticalVel = throwData.verticalVelocity;

        // 높이 시스템 던지기 적용
        SetThrowVelocity(adjustedHorizontalVel, adjustedVerticalVel, throwData.initialHeight);
        _recentlyThrownTimer = _checkRecentlyThrownTime;

        Debug.Log($"<color=blue>높이 시스템 던지기 실행 - 수평속도: {adjustedHorizontalVel}, 수직속도: {adjustedVerticalVel}</color>");
    }

    /// <summary>
    /// [SERVER-side] 높이 시스템이 포함된 오브젝트 던지기 적용
    /// [김도형 - 2025.09.15]
    /// </summary>
    /// <param name="throwData">던지기 데이터</param>
    /// <param name="throwerId">던진 플레이어 ID</param>
    [Server]
    public virtual void ThrownWithHeight(ThrowData throwData, uint throwerId)
    {
        if (!IsBeingLifted)
        {
            return;
        }
        _liftingPlayer = null;
        GetComponent<Collider2D>().isTrigger = false; // 충돌체 활성화

        _throwerNetId = throwerId;

        // 크기에 따른 힘 조정
        Vector2 adjustedHorizontalVel = throwData.horizontalVelocity;
        float adjustedVerticalVel = throwData.verticalVelocity;

        // 높이 시스템 던지기 적용
        SetThrowVelocity(adjustedHorizontalVel, adjustedVerticalVel, throwData.initialHeight);
        _recentlyThrownTimer = _checkRecentlyThrownTime;

        Debug.Log($"<color=blue>높이 시스템 던지기 실행 - 수평속도: {adjustedHorizontalVel}, 수직속도: {adjustedVerticalVel}</color>");
    }

    /// <summary>
    /// [SERVER-side] 적 AI가 LTD 오브젝트를 던질 떄 호출. 높이 적용됨
    /// [조우석 - 2025.10.26]
    /// </summary>
    /// <param name="throwData"></param>
    /// <param name="throwerId"></param>
    [Server]
    public virtual void ThrownWithHeightByEnemy(ThrowData throwData, uint throwerId)
    {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.totalTorque = 0;
        GetComponent<Collider2D>().isTrigger = false; // 충돌체 활성화

        // 높이 시스템 초기화
        _currentHeight = 0.2f;
        _verticalVelocity = 0f;
        _horizontalVelocity = Vector2.zero;

        _throwerNetId = throwerId;

        // 크기에 따른 힘 조정
        Vector2 adjustedHorizontalVel = throwData.horizontalVelocity;
        float adjustedVerticalVel = throwData.verticalVelocity;

        // 높이 시스템 던지기 적용
        SetThrowVelocity(adjustedHorizontalVel, adjustedVerticalVel, throwData.initialHeight);
        _recentlyThrownTimer = _checkRecentlyThrownTime;

        Debug.Log($"<color=cyan>높이 시스템 던지기 실행 [Enemy] - 수평속도: {adjustedHorizontalVel}, 수직속도: {adjustedVerticalVel}</color>");
    }


    [ClientRpc]
    private void RpcSetObjectDepth(float depth)
    {
        GetComponent<DisplayObject>().ChangeDepthTo(depth);
    }

    #endregion

    /// <summary>
    /// LTD 오브젝트의 비활성화/활성화 토글
    /// </summary>
    [ClientRpc]
    public void RpcToggleLtdObject(bool enable)
    {
        GameObject parts = GetComponent<DisplayObject>().Parts;
        if (parts != null)
        {
            parts.SetActive(enable);
        }
    }

    /// <summary>
    /// 던진 플레이어 설정 초기화 (던진 플레이어도 충돌 판정하도록)
    /// </summary>
    public void ClearThrowerId()
    {
        _throwerNetId = 0;
    }

    /// <summary>
    /// 플레이어가 진입 가능한 오브젝트 내부로 들어갔을 때 호출
    /// </summary>
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        // if (!isServer) return;
        // if (collision.tag == "Enterable")
        // {
        //     transform.SetParent(collision.transform, true);
        //     GetComponent<NetworkParentSync>()?.SetNetworkParent(collision.transform);
        // }
    }
    /// <summary>
    /// 플레이어가 진입 가능한 오브젝트 내부에서 나갔을 때 호출
    /// </summary>
    protected virtual void OnTriggerExit2D(Collider2D collision)
    {
        // if (!isServer) return;
        // if (collision.tag == "Enterable") // 트리거 감지상태일때만 
        // {
        //     transform.SetParent(null, true);
        //     GetComponent<NetworkParentSync>()?.SetNetworkParent(null);
        // }
    }
}