using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// LTD수리도구
/// [박상민 - 2025.07.02]
/// </summary>
public class RepairKitObject : LTDObject, ISaleable
{
    [Header("<color=green>수리 도구 설정")]
    [SerializeField]
    private float _repairTime = 5f; // 수리 시간
    [SerializeField]
    private List<GameObject> _currentrepairePoints;
    [SerializeField]
    private bool _isRepairing = false; // 수리 중인지 여부
    [SerializeField]
    private Image _repairProgressBar; // 수리 진행률 UI
    [SerializeField]
    private float _despawnTime = 5f; // 스폰후 상호작용 없을시 디스폰 시간

    // 이벤트
    public event Action<uint> OnDirtyObject;        // 오브젝트 첫 상호작용 후 호출되는 메서드

    // 접근용 프로퍼티
    public float RepairTime => _repairTime;
    public GameObject CurrentRepairPoint => _currentrepairePoints.Count > 0 ? _currentrepairePoints[0] : null;
    private bool _checkDespawnTimer => !_isFreshlySpawned && !IsBeingLifted;    // 오브젝트에 상호작용을 한 적 있고, 손에 들려있지 않으면 디스폰 타이머를 체크
    public uint Price => ItemManager.Instance.GetItemPrice(new ItemData { itemType = _ltdObjectType });

    // private 필드
    private float _despawnTimer = 0f; // 디스폰 타이머
    private bool _isFreshlySpawned = true; // 최초 생성 후 상호작용 여부
    [SyncVar(hook = nameof(OnRepairProgressChanged))] private float _repairProgress = 0f; // 수리 진행률

    #region  IInteractable 구현
    private string InteractionText
    {
        get
        {
            if (IsBeingLifted && CurrentRepairPoint != null && !_isRepairing)
            {
                return "수리(홀드)";
            }
            else if (!IsBeingLifted)
            {
                return "들기";
            }
            return ""; // 기타 상태에서는 빈 문자열 반환
        }
    }
    private string ActionName
    {
        get
        {
            if (IsBeingLifted && CurrentRepairPoint != null && !_isRepairing)
            {
                return "Player/Use";
            }
            else if (!IsBeingLifted)
            {
                return "Player/LiftDrop";
            }
            return ""; // 기타 상태에서는 빈 문자열 반환
        }
    }
    private Vector3 GetInteractionUIPosition()
    {
        if (IsBeingLifted && CurrentRepairPoint != null && !_isRepairing)
        {
            return CurrentRepairPoint.transform.position + Vector3.up * 0.5f;
        }
        else if (!IsBeingLifted)
        {
            return transform.position + Vector3.up * (CurrentHeight + 0.5f);
        }
        return transform.position + Vector3.up * 1f;
    }
    private bool IsInteractable
    {
        get
        {
            if (!IsBeingLifted)
            {
                return IsAbleToLift;
            }
            return CurrentRepairPoint != null && !_isRepairing;
        }
    }
    public override InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            return new InteractionInfo();
        }

        string text = InteractionText;
        string action = ActionName;
        bool interactable = IsInteractable;
        Vector3 position = GetInteractionUIPosition();

        return new InteractionInfo(netId, text, action, interactable, position);
    }

    #endregion

    protected override void Awake()
    {
        base.Awake();
        _ltdObjectType = LTDObjectType.수리도구; // LTD 오브젝트 타입을 플레이어로 설정
        _ltdObjectSize = LTDObjectSize.소; // 크기는 임시로
        _currentrepairePoints = new List<GameObject>();
        _repairProgressBar.fillAmount = 0f; // 수리 진행률 초기화
    }

    protected override void Update()
    {
        base.Update();
        if (!isServer)
        {
            return;
        }

        // 일정 시간 동안 손에 들려있지 않으면 오브젝트 삭제
        // if (_checkDespawnTimer)
        // {
        //     _despawnTimer += Time.deltaTime;
        //     if (_despawnTimer > _despawnTime)
        //     {
        //         NetworkServer.Destroy(gameObject);
        //     }
        // }
        else
        {
            _despawnTimer = 0;
        }

        // 수리 진행
        if (_isRepairing && CurrentRepairPoint != null)
        {
            // 수리 중일 때
            _repairProgress += Time.deltaTime;
            if (_repairProgress >= _repairTime)
            {
                // 수리가 완료되면
                _isRepairing = false;
                _repairProgress = 0f;
                // 수리 가능한 오브젝트에 따라 다르게 처리 (모듈 포인트 or 일반 수리 지점)
                if (CurrentRepairPoint.TryGetComponent<ModulePoint>(out var modulePoint))
                {
                    modulePoint.ToggleModulePoint(true); // 모듈 포인트 활성화
                }
                if (CurrentRepairPoint.TryGetComponent<RepairPoint>(out var repairPoint))
                {
                    bool isFullyRepaired = repairPoint.Repair();
                    if (isFullyRepaired)
                    {
                        // 수리 지점이 모두 수리되었으면 타겟 파괴
                        NetworkServer.Destroy(CurrentRepairPoint); // 수리 지점 오브젝트 제거
                    }
                }
                _liftingPlayer.RpcHideInteractionPrompt(_liftingPlayer.connectionToClient, netId);
                _liftingPlayer.DropAction();
                NetworkServer.Destroy(this.gameObject); // 수리 도구 오브젝트 제거
            }
            return;
        }
        // 수리 중이 아닐 때
        _repairProgress = 0f;
    }

    /// <summary>
    /// 수리를 시작
    /// </summary>
    public void StartRepair()
    {
        if (!_isRepairing && CurrentRepairPoint != null)
        {
            _isRepairing = true; // 수리 시작
            _repairProgress = 0f; // 수리 진행률 초기화
        }
    }
    /// <summary>
    /// 수리를 중지
    /// </summary>
    public void StopRepair()
    {
        _isRepairing = false; // 수리 중지
        _repairProgress = 0f; // 수리 진행률 초기화
    }

    /// <summary>
    /// 수리 가능한 지점과 충돌시 저장
    /// </summary>
    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        base.OnTriggerEnter2D(collision);
        if (!isServer)
        {
            return;
        }
        if (collision.CompareTag("RepairPoint"))
        {
            // 수리 가능한 오브젝트와 충돌 시
            _currentrepairePoints.Add(collision.gameObject);
        }
        if (collision.CompareTag("ModulePoint"))
        {
            // 모듈 포인트와 충돌 시
            if (collision.TryGetComponent<ModulePoint>(out var modulePoint))
            {
                if (!modulePoint.IsPointDisabled)
                {
                    return; // 비활성화 상태가 아니면 종료
                }
                _currentrepairePoints.Add(collision.gameObject);
            }
        }
    }
    protected override void OnTriggerExit2D(Collider2D collision)
    {
        base.OnTriggerExit2D(collision);
        if (!isServer)
        {
            return;
        }
        if (collision.CompareTag("RepairPoint") || collision.CompareTag("ModulePoint"))
        {
            if (_currentrepairePoints.Contains(collision.gameObject))
            {
                if (_isRepairing && CurrentRepairPoint == collision.gameObject)
                {
                    StopRepair(); // 수리 중이던 지점에서 벗어나면 수리 중지
                }
                _currentrepairePoints.Remove(collision.gameObject);
            }
        }
    }

    // protected override void OnTriggerEnter2D(Collider2D other)
    // {
    //     base.OnTriggerEnter2D(other);

    //     if (other.CompareTag("RepairPoint") && isServer)
    //     {
    //         if (_shipController == null)
    //         {
    //             _shipController = TempShipSpawner.Instance.CurrentShip.GetComponent<ShipController>();
    //         }
    //         _shipController.Repair(other.gameObject); // 함선 수리 명령
    //         NetworkServer.Destroy(gameObject); // 오브젝트 제거
    //     }
    //     if (other.CompareTag("ModulePoint") && isServer)
    //     {
    //         ModulePoint point = other.GetComponent<ModulePoint>();
    //         if (!point.IsPointDisabled)
    //         {
    //             return; // 비활성화 상태가 아니면 종료
    //         }
    //         if (point != null)
    //         {
    //             point.ToggleModulePoint(true); // 모듈 포인트 비활성화
    //         }
    //         NetworkServer.Destroy(gameObject); // 오브젝트 제거
    //     }
    // }

    public override bool Lifted(PlayerLTDController player)
    {
        bool result = base.Lifted(player);

        // 첫 상호작용 시 활성화
        if (_isFreshlySpawned)
        {
            SetFreshSpawned(false);
        }

        return result;
    }

    public override void Dropped()
    {
        base.Dropped();

        // 첫 상호작용 시 활성화
        if (_isFreshlySpawned)
        {
            SetFreshSpawned(false);
        }
        StopRepair(); // 수리 중지
    }

    public override void Thrown(ThrowData throwData, uint throwerNetId)
    {
        base.Thrown(throwData, throwerNetId);

        // 첫 상호작용 시 활성화
        if (_isFreshlySpawned)
        {
            _isFreshlySpawned = false;
        }
        StopRepair(); // 수리 중지
    }

    /// <summary>
    /// 최초 생성 후 상호작용 여부에 대해서 적용하는 메서드
    /// </summary>
    private void SetFreshSpawned(bool isFresh)
    {
        _isFreshlySpawned = isFresh;
        if (!isFresh)
        {
            OnDirtyObject?.Invoke(this.netId);
        }
    }

    #region 클라이언트 UI 동기화
    private void OnRepairProgressChanged(float oldProgress, float newProgress)
    {
        float fillAmount = Mathf.Clamp01(newProgress / _repairTime);
        _repairProgressBar.fillAmount = fillAmount;
    }
    #endregion
}