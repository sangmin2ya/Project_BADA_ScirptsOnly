using UnityEngine;
using Mirror;

/// <summary>
/// 플레이어가 LTD 오브젝트를 다루는 컨트롤러
/// [박상민 - 2025.06.12]
/// </summary>
public class PlayerLTDController : NetworkBehaviour
{
    [Header("<color=green>LTD 설정")]
    [Tooltip("LTD 오브젝트를 감지하는 반경")]
    [SerializeField] private float _detectRadius = 1;
    [Tooltip("LTD 오브젝트 태그")]
    [SerializeField] private string _ltdObjectTag = "LTDObject";
    [Tooltip("LTD 오브젝트을 들어올릴 높이")]
    [SerializeField] private float _liftHeight = 2f;

    [Header("<color=cyan>던지기 시스템")]
    [Tooltip("던지기 힘")]
    [SerializeField] private float _throwForce = 20f;
    [Tooltip("던지기 각도 (도)")]
    [SerializeField] private float _throwAngleDegree = 0f;
    [Tooltip("중력값")]
    [SerializeField] private float _customGravity = 9.81f;
    [Tooltip("착지점 미리보기 UI")]
    [SerializeField] private GameObject _aimTarget;
    [Tooltip("던지기위한 차징 필요 시간")]
    [SerializeField] private float _maxChargeTime = 1f;

    [Header("<color=yellow>LTD 상태")]
    [Tooltip("감지한 주변 LTD 오브젝트")]
    [SerializeField] private LTDObject _detectedLTDObject;
    [Tooltip("들어올린 LTD 오브젝트")]
    [SerializeField] private LTDObject _liftedLTDObject;
    [Tooltip("특수 행동 중인지")]
    [SerializeField] private bool _isOnSpecialAction = false;
    [Tooltip("던지기 차징 중인지")]
    [SerializeField] private bool _isChargingThrow = false;

    [Header("LTD 참조")]
    [Tooltip("들어올린 오브젝트 부모 피벗")]
    [SerializeField] private Transform _liftPivot;
    public Transform LiftPivot => _liftPivot;
    [SerializeField] private Transform _handPivot;
    public Transform HandPivot => _handPivot;

    private PlayerController _playerController;
    private PlayerState _playerState => _playerController.PlayerState;
    private PlayerTradeController _playerTradeController;
    private float _throwChargeTimer = 0f;
    private Vector2 _lastFaceVector => _playerController.LastFaceVector;
    private bool _wasLiftedObjectInteractable = false; // 이전 프레임의 상호작용 가능 여부

    public bool IsLifting => _liftedLTDObject != null;
    public LTDObject DetectedLTDObject => _detectedLTDObject;
    public LTDObject LiftedLTDObject => _liftedLTDObject;
    public float LiftHeight => _liftHeight;

    private float ThrowAngle => _throwAngleDegree * Mathf.Deg2Rad;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _playerTradeController = GetComponent<PlayerTradeController>();
    }

    void Start()
    {
        _handPivot.gameObject.SetActive(isLocalPlayer); // 로컬 플레이어만 손 피벗 활성화

        // UI 초기화
        if (_aimTarget != null)
        {
            _aimTarget.SetActive(false);
        }
    }

    void Update()
    {
        // 로컬 플레이어만 감지 및 위치 업데이트 처리, 기절이나 사망상태라 컨트롤 불가하다면 return
        if (!isServer || !_playerState.IsControllable)
        {
            return;
        }

        // 플레이어가 LTD 오브젝트를 감지
        DetectLTDObject();

        // 들고 있는 오브젝트가 있으면 위치 업데이트
        UpdateLiftedObjectPosition();

        // 들고 있는 오브젝트의 상호작용 가능 여부 업데이트
        UpdateLiftedObjectInteraction();
        //UpdateMassByLiftedObject();

        // // 던지기 차징 처리 ltd오브젝트 없는거는 이미 인풋에서 걸러짐
        // if (_isChargingThrow)
        // {
        //     _throwChargeTimer += Time.deltaTime;
        //     if (_throwChargeTimer >= _maxChargeTime)
        //     {
        //         _throwChargeTimer = _maxChargeTime;
        //     }
        // }
        // else
        // {
        //     // 아예 충전이 안된상태라면 return
        //     if (_throwChargeTimer <= 0f)
        //     {
        //         return;
        //     }
        //     if (_throwChargeTimer == _maxChargeTime)
        //     {
        //         ThrowAction();
        //     }
        //     else
        //     {
        //         DropAction();
        //     }
        //     _throwChargeTimer = 0f;
        // }
    }

    //private void UpdateMassByLiftedObject()
    //{
    //    if (_liftedLTDObject != null)
    //    {
    //        switch (_liftedLTDObject.LtdObjectSize)
    //        {
    //            case LTDObjectSize.소:
    //                _playerController.UpdateMass(1.25f);
    //                break;
    //            case LTDObjectSize.중:
    //                _playerController.UpdateMass(1.5f);
    //                break;
    //            case LTDObjectSize.대:
    //                _playerController.UpdateMass(2.0f);
    //                break;
    //        }
    //    }
    //    else
    //    {
    //        _playerController.UpdateMass(1f);
    //    }
    //}

    private float GetMassBySize(LTDObjectSize size)
    {
        float mass = 0;

        switch (size)
        {
            case LTDObjectSize.소:
                mass = .25f;
                break;
            case LTDObjectSize.중:
                mass = .5f;
                break;
            case LTDObjectSize.대:
                mass = 1.0f;
                break;
        }

        return mass;
    }

    #region 던지기 시스템

    /// <summary>
    /// 던지기 실행
    /// [김도형 - 2025.09.15]
    /// </summary>
    public void ThrowAction()
    {
        if (!_playerState.IsControllable || _liftedLTDObject == null)
        {
            return;
        }
        LTDObject objectToThrow = _liftedLTDObject;
        float speedByLtdSize = 1f;
        switch (objectToThrow.LtdObjectSize)
        {
            case LTDObjectSize.소:
                speedByLtdSize = 1f;
                break;
            case LTDObjectSize.중:
                speedByLtdSize = 0.6f;
                break;
            case LTDObjectSize.대:
                speedByLtdSize = 0.2f;
                break;
        }

        // 던지기 속도 계산
        float velocityX = Mathf.Cos(ThrowAngle) * _throwForce * speedByLtdSize;
        float velocityY = Mathf.Sin(ThrowAngle) * _throwForce * speedByLtdSize;

        Vector2 throwDirection = _lastFaceVector.normalized;
        Vector2 horizontalVelocity = throwDirection * velocityX;

        // 던지기 데이터 구조체 생성
        ThrowData throwData = new ThrowData
        {
            horizontalVelocity = horizontalVelocity,
            verticalVelocity = velocityY,
            initialHeight = _liftHeight
        };
        _liftedLTDObject.transform.position = _handPivot.position; // 던질 때 손 피벗 위치로 이동
        PerformThrowWithHeight(objectToThrow.netId, throwData);
        RpcHideInteractionPrompt(connectionToClient, _liftedLTDObject.netId);

        // 플레이어 질량 감소
        _playerController.AddMass(-GetMassBySize(_liftedLTDObject.LtdObjectSize));

        // 상태 초기화
        _liftedLTDObject = null;

        // // UI 비활성화
        // if (_aimTarget != null)
        // {
        //     _aimTarget.SetActive(false);
        // }
    }

    #endregion

    /// <summary>
    /// 들고 있는 오브젝트의 위치를 업데이트
    /// </summary>
    private void UpdateLiftedObjectPosition()
    {
        if (_liftedLTDObject != null)
        {
            //Debug.Log("들고 있는 LTD 오브젝트 위치 업데이트");
            // 들고 있는 오브젝트를 LiftPivot 위치로 이동
            // NetworkTransform이 자동으로 다른 클라이언트들에게 동기화
            _liftedLTDObject.transform.position = _liftPivot.position;
            _liftedLTDObject.transform.rotation = _liftPivot.rotation;
        }
    }

    /// <summary>
    /// 플레이어가 주기적으로 주변의 LTD오브젝트를 감지하는 함수
    /// - 플레이어가 오브젝트를 감지하기 위해서는 해당 오브젝트에 태그 'LTDObject'가 붙어 있어야 한다.
    /// </summary>
    private void DetectLTDObject()
    {
        // 들고있으면 감지안함
        if (_liftedLTDObject != null)
        {
            // 들고 있을 때 이전에 감지된 오브젝트가 있다면 포커스 해제 + UI 숨김
            if (_detectedLTDObject != null)
            {
                _detectedLTDObject.Focused(connectionToClient, false);

                // 클라이언트에서만 UI 처리
                if (_detectedLTDObject is IInteractable interactable)
                {
                    RpcHideInteractionPrompt(connectionToClient, _detectedLTDObject.netId);
                }

                _detectedLTDObject = null;
            }
            return;
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(_handPivot.position, _detectRadius);

        float closestDistanceSqr = Mathf.Infinity;
        LTDObject closestObject = null;

        foreach (var col in colliders)
        {
            if (col.CompareTag(_ltdObjectTag))
            {
                LTDObject ltdObj = col.GetComponent<LTDObject>();
                if (ltdObj != null && ltdObj != _liftedLTDObject && !ltdObj.IsBeingLifted)
                {
                    // 감지된 LTD 오브젝트가 나 자신이거나 들어올릴 수 없는 상태라면 건너뛰기
                    if (ltdObj.gameObject == gameObject || !ltdObj.IsAbleToLift)
                    {
                        continue;
                    }

                    float distSqr = (col.transform.position - _handPivot.position).sqrMagnitude;
                    if (distSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distSqr;
                        closestObject = ltdObj;
                    }
                }
            }
        }

        // 이전에 감지된 오브젝트와 현재 감지된 오브젝트가 다르면 포커싱 변경
        if (_detectedLTDObject != closestObject)
        {
            // 이전 오브젝트 포커스 해제 + UI 숨김
            if (_detectedLTDObject != null)
            {
                _detectedLTDObject.Focused(connectionToClient, false);

                // 클라이언트에서만 UI 처리
                if (_detectedLTDObject is IInteractable)
                {
                    RpcHideInteractionPrompt(connectionToClient, _detectedLTDObject.netId);
                }
            }

            // 새로운 오브젝트 포커스 설정 + UI 표시
            _detectedLTDObject = closestObject;

            if (_detectedLTDObject != null)
            {
                _detectedLTDObject.Focused(connectionToClient, true);
            }
        }

        if (_detectedLTDObject != null)
        {
            RpcShowInteractionPrompt(connectionToClient, (_detectedLTDObject as IInteractable).GetInteractionInfo());
        }
    }

    /// <summary>
    /// 특수 행동 중인지 여부를 설정하는 메서드 (예: 수리, 부활 등)
    /// </summary>
    public void CallSpecialAction(bool isOnSpecialAction)
    {
        _isOnSpecialAction = isOnSpecialAction;
        if (_isOnSpecialAction)
        {
            //CallRespawn(true); // 리스폰
            CallRevive(true); // 부활
            CallRepair(true); // 수리 시작
            CallUnpackNet(true); // 그물망 풀기 시작
        }
        else
        {
            //CallRespawn(false); // 리스폰 중지
            CallRevive(false); // 부활 중지
            CallRepair(false); // 수리 중지
            CallUnpackNet(false); // 그물망 풀기 중지
        }
    }

    #region LTD 오브젝트 조작 액션

    /// <summary>
    /// LTD 오브젝트를 들어올리거나 내려놓는 메서드
    /// - PlayerInputHandler에서 호출
    /// </summary>
    public bool CallLiftDrop()
    {
        // 플레이어가 스턴 상태에서 LTD 상호작용이 불가능하다면 return
        if (!_playerState.IsControllable)
        {
            return false;
        }

        // 사용중이라면 return;
        if (_detectedLTDObject != null && _detectedLTDObject.TryGetComponent<ModuleBoxObject>(out ModuleBoxObject moduleBox))
        {
            if (moduleBox.InstalledModule != null && moduleBox.InstalledModule.IsOccupied)
            {
                return false;
            }
        }

        if (_liftedLTDObject == null) // 들고 있는 오브젝트가 없다면 들어올리기
        {
            return LiftAction();
        }
        else // 들고 있는 오브젝트가 있다면 내려놓기
        {
            return DropAction();
        }
    }

    /// <summary>
    /// LTD 오브젝트를 들어올리는 메서드
    /// </summary>
    private bool LiftAction()
    {
        if (_detectedLTDObject != null)
        {
            _detectedLTDObject.Lifted(this);
            _liftedLTDObject = _detectedLTDObject;

            // 바닥에 있을 때의 UI 숨김
            RpcHideInteractionPrompt(connectionToClient, _detectedLTDObject.netId);
            _liftedLTDObject.Focused(connectionToClient, false);

            // 들고 있는 상태에서 상호작용 가능하면 UI 표시
            if (_liftedLTDObject is IInteractable interactable && interactable.GetInteractionInfo().isInteractable)
            {
                var info = interactable.GetInteractionInfo();
                _wasLiftedObjectInteractable = true;
                RpcShowInteractionPrompt(connectionToClient, info);
            }
            else
            {
                _wasLiftedObjectInteractable = false;
            }

            _detectedLTDObject = null;

            // 플레이어 질량 증가
            _playerController.AddMass(GetMassBySize(_liftedLTDObject.LtdObjectSize));

            return true;
        }

        Debug.Log("감지된 LTD 오브젝트가 없습니다.");
        return false;
    }

    /// <summary>
    /// LTD 오브젝트를 내려놓는 메서드
    /// </summary>
    public bool DropAction()
    {
        if (_liftedLTDObject != null)
        {
            _liftedLTDObject.transform.position = _handPivot.position; // 내려놓을 때 손 피벗 위치로 이동
            _liftedLTDObject.Dropped();
            RpcHideInteractionPrompt(connectionToClient, _liftedLTDObject.netId);
            // 플레이어 질량 감소
            _playerController.AddMass(-GetMassBySize(_liftedLTDObject.LtdObjectSize));

            _liftedLTDObject = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Throw 조건 판별 후 호출 메서드
    /// - PlayerInputHandler에서 호출
    /// </summary>
    public void CallThrow()
    {
        if (!_playerState.IsControllable || _liftedLTDObject == null)
        {
            return;
        }
        ThrowAction();
    }

    /// <summary>
    /// LTD 오브젝트(플레이어일 경우)를 부활시키는 메서드
    /// </summary>
    public void CallRevive(bool isRevive)
    {
        if (_liftedLTDObject == null)
        {
            return;
        }
        if (_liftedLTDObject.TryGetComponent<PlayerState>(out PlayerState playerState))
        {
            if (isRevive)
            {
                if (!playerState.IsRevivable)
                {
                    Debug.Log("플레이어가 사망상태가 아닙니다.");
                    return;
                }
                playerState.StartRevive();
            }
            else
            {
                if (!playerState.IsReviving)
                {
                    Debug.Log("플레이어가 부활 중이 아닙니다.");
                    return;
                }
                playerState.StopRevive();
            }
        }
        else
        {
            Debug.Log("PlayerLTDController : LTD 오브젝트가 플레이어 상태를 가지고 있지 않습니다.");
        }
    }

    /// <summary>
    /// 자기 자신(플레이어일 경우)을 리스폰시키는 메서드
    /// </summary>
    public void CallRespawn(bool isRespawn)
    {
        if (TryGetComponent<PlayerState>(out PlayerState playerState))
        {
            if (isRespawn)
            {
                if (!playerState.IsRespawnable)
                {
                    Debug.Log("플레이어가 리스폰 가능한 상태가 아닙니다.");
                    return;
                }
                playerState.CallRespawn();
                GetComponent<LTDPlayer>().Reset(); // LTD 오브젝트 초기화
            }
        }
        else
        {
            Debug.Log("PlayerLTDController : LTD 오브젝트가 플레이어 상태를 가지고 있지 않습니다.");
        }
    }

    /// <summary>
    /// 들고 있는 LTD 오브젝트로 수리하는 메서드
    /// </summary>
    public void CallRepair(bool isRepair)
    {
        if (_liftedLTDObject == null)
        {
            return;
        }
        if (_liftedLTDObject.TryGetComponent<RepairKitObject>(out RepairKitObject repairKit))
        {
            if (isRepair)
            {
                repairKit.StartRepair(); // 수리 시작
            }
            else
            {
                repairKit.StopRepair(); // 수리 중지
            }
        }
        else
        {
            Debug.Log("PlayerLTDController : LTD 오브젝트가 수리 도구 상태를 가지고 있지 않습니다.");
        }
    }

    /// <summary>
    /// 들고있는 오브젝트를 창고에 넣는 메서드(이때 들고있는거 파괴)
    /// </summary>
    public void CallPutItem()
    {
        //들고있는 오브젝트 내려놓기
        if (_liftedLTDObject != null)
        {
            _liftedLTDObject.Dropped();
            NetworkServer.Destroy(_liftedLTDObject.gameObject); // 서버에서 오브젝트 파괴

            // 플레이어 질량 감소
            _playerController.AddMass(-GetMassBySize(_liftedLTDObject.LtdObjectSize));
            // UI있으면 끄기
            RpcHideInteractionPrompt(connectionToClient, _liftedLTDObject.netId);

            _liftedLTDObject = null;
        }
        else
        {
            Debug.Log("들고 있는 LTD 오브젝트가 없습니다.");
        }
    }

    /// <summary>
    /// 저장소에서 아이템을 꺼내면 활성화하고 Lift처리
    /// </summary>
    public void CallTakeItem(LTDObject item)
    {
        // 아이템을 활성화하고 들어올리기
        item.Lifted(this);
        _liftedLTDObject = item;

        // 플레이어 질량 증가
        _playerController.AddMass(GetMassBySize(_liftedLTDObject.LtdObjectSize));

        RpcToggleActiveLTDObject(_liftedLTDObject.netId, true);
    }

    /// <summary>
    /// 아이템을 구매하여 들고오는 메서드
    /// </summary>
    public void CallBuyItem(LTDObject item)
    {
        // 아이템을 들어올리기
        item.Lifted(this);
        _liftedLTDObject = item;

        // 플레이어 질량 증가
        _playerController.AddMass(GetMassBySize(_liftedLTDObject.LtdObjectSize));
    }

    public void CallResetLTDObject()
    {
        if (_liftedLTDObject != null)
        {
            _liftedLTDObject.Dropped();

            // 플레이어 질량 감소
            _playerController.AddMass(-GetMassBySize(_liftedLTDObject.LtdObjectSize));

            _liftedLTDObject = null;
        }
    }

    public void CallForceDropItemAndKnocked(Vector2 force, float knockbackForce)
    {
        // 튕겨나가는 방향을 vector2 값 +- 45도로 랜덤하게 조정 (플레이어용, 들고있는 박스용 2번 선정)
        Vector2 playerRandomDirection = Quaternion.Euler(0, 0, Random.Range(-45f, 45f)) * force;
        Vector2 ltdObjectRandomDirection = Quaternion.Euler(0, 0, Random.Range(-45f, 45f)) * force;

        //혹시 모듈에 앉아있으면 넉백
        if (GetComponent<PlayerModuleController>().ActiveModule != null)
        {
            GetComponent<PlayerModuleController>().CallForceExitModule(playerRandomDirection * knockbackForce);
            return;
        }
        //들고있는 오브젝트 내려놓기
        if (_liftedLTDObject != null)
        {
            _liftedLTDObject.Dropped();
            _liftedLTDObject.GetComponent<Rigidbody2D>().velocity = ltdObjectRandomDirection * knockbackForce;

            // 플레이어 질량 감소
            _playerController.AddMass(-GetMassBySize(_liftedLTDObject.LtdObjectSize));

            _liftedLTDObject = null;
        }
        // 플레이어를 잠시 기절시킴
        _playerState.ApplyStun();
        // 플레이어를 밀쳐냄
        GetComponent<Rigidbody2D>().velocity = playerRandomDirection * knockbackForce;
    }

    /// <summary>
    /// 그물망을 풀기 시작하거나 중지하는 메서드
    /// </summary>
    public void CallUnpackNet(bool isUnpack)
    {
        if (_liftedLTDObject == null)
        {
            return;
        }
        if (_liftedLTDObject.TryGetComponent<FishingNetObject>(out FishingNetObject fishingNet))
        {
            if (isUnpack)
            {
                fishingNet.StartUnpacking(); // 그물망 풀기 시작
            }
            else
            {
                fishingNet.StopUnpacking(); // 그물망 풀기 중지
            }
        }
        else
        {
            Debug.Log("PlayerLTDController : LTD 오브젝트가 그물망 상태를 가지고 있지 않습니다.");
        }
    }

    /// <summary>
    /// 들고 있는 오브젝트의 상호작용 가능 여부를 체크하고 UI를 업데이트합니다
    /// </summary>
    private void UpdateLiftedObjectInteraction()
    {
        if (_liftedLTDObject == null)
        {
            _wasLiftedObjectInteractable = false;
            return;
        }

        // 들고 있는 오브젝트가 IInteractable을 구현하고 있는지 체크
        if (_liftedLTDObject is IInteractable interactable)
        {
            var info = interactable.GetInteractionInfo();
            _wasLiftedObjectInteractable = info.isInteractable;

            if (_wasLiftedObjectInteractable)
            {
                RpcShowInteractionPrompt(connectionToClient, info);
            }
            else
            {
                RpcHideInteractionPrompt(connectionToClient, _liftedLTDObject.netId);
            }
        }
    }

    #endregion

    #region 상호작용 프롬프트 RPC
    /// <summary>
    /// 클라이언트에 상호작용 프롬프트 표시를 요청합니다 (서버에서 계산된 정보 전달)
    /// </summary>
    /// <param name="target">타겟 클라이언트</param>
    /// <param name="info">서버에서 계산된 상호작용 정보</param>
    [TargetRpc]
    public void RpcShowInteractionPrompt(NetworkConnection target, InteractionInfo info)
    {
        if (InteractionPromptManager.Instance == null)
        {
            return;
        }

        InteractionPromptManager.Instance.ShowPromptFromServer(info);
    }

    /// <summary>
    /// 클라이언트에 상호작용 프롬프트 숨김을 요청합니다
    /// </summary>
    /// <param name="target">타겟 클라이언트</param>
    /// <param name="targetNetId">타겟 오브젝트의 netId</param>
    [TargetRpc]
    public void RpcHideInteractionPrompt(NetworkConnection target, uint targetNetId)
    {
        if (InteractionPromptManager.Instance == null)
        {
            return;
        }

        InteractionPromptManager.Instance.HidePrompt(targetNetId);
    }

    [TargetRpc]
    private void RpcForceHideAllPrompts(NetworkConnection target)
    {
        InteractionPromptManager.Instance?.HideAllPrompts();
    }

    #endregion

    #region Network Commands
    /// <summary>
    /// 높이 시스템이 포함된 새로운 던지기 커맨드
    /// [김도형 - 2025.09.15]
    /// </summary>
    private void PerformThrowWithHeight(uint objectNetId, ThrowData throwData)
    {
        if (NetworkServer.spawned.TryGetValue(objectNetId, out NetworkIdentity identity))
        {
            if (identity.TryGetComponent<LTDObject>(out LTDObject ltdObject))
            {
                ltdObject.Thrown(throwData, this.netId);
            }
        }
    }

    [ClientRpc]
    private void RpcToggleActiveLTDObject(uint objectId, bool isActive)
    {
        LTDObject item = GameNetworkManager.GetNetworkComponent<LTDObject>(objectId);
        if (item != null)
        {
            item.gameObject.SetActive(isActive);
        }
        else
        {
            Debug.LogError($"아이템 {objectId}을 찾을 수 없습니다!");
        }
    }
    #endregion
}

/// <summary>
/// 던지기 데이터를 담는 구조체
/// [김도형 - 2025.09.15]
/// </summary>
[System.Serializable]
public struct ThrowData
{
    public Vector2 horizontalVelocity;
    public float verticalVelocity;
    public float initialHeight;
}