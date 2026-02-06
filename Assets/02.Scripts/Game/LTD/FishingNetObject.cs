using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// 크레인으로 건져올린 LTD 오브젝트를 담는 그물
/// [박상민 - 2025.10.03]
/// </summary>
public class FishingNetObject : LTDObject
{
    [Header("<color=green>그물 설정")]
    [Tooltip("언패킹 시간")]
    [SerializeField] private float _unpackingTime = 3f;
    [Tooltip("언패킹 진행률 UI")]
    [SerializeField] private Image _unPackingProgressBar;
    [Tooltip("그물에 담긴 오브젝트 타입들")]
    [SerializeField] private List<ItemData> _caughtObjectTypes = new List<ItemData>();

    // private 필드
    private bool _isUnpacking = false; // 언패킹 중인지 여부
    private float _unpackingTimer = 0f; // 언패킹 타이머

    // 접근용 프로퍼티
    public bool IsUnpacking => _isUnpacking;

    #region IInteractable 구현

    private string InteractionText
    {
        get
        {
            if (IsBeingLifted)
            {
                return "풀기(홀드)";
            }
            else
            {
                return "들기";
            }
        }
    }
    private string ActionName
    {
        get
        {
            if (IsBeingLifted)
            {
                return "Player/Use";
            }
            else
            {
                return "Player/LiftDrop";
            }
        }
    }
    private bool IsInteractable
    {
        get
        {
            if (IsBeingLifted)
            {
                return !_isUnpacking; // 들려있을 때는 언패킹 중이 아닐 때만 상호작용 가능
            }
            else
            {
                return IsAbleToLift; // 들려있지 않을 때는 들 수 있을 때만 상호작용 가능
            }
        }
    }

    private Vector3 GetInteractionUIPosition()
    {
        return transform.position + Vector3.up * CurrentHeight;
    }

    public override InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
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
        _ltdObjectType = LTDObjectType.그물망; // LTD 오브젝트 타입을 그물로 설정
        //_ltdObjectSize = LTDObjectSize.중; // 크기는 임시로
    }
    protected override void Update()
    {
        base.Update();

        if (!isServer) return;

        if (_isUnpacking)
        {
            _unpackingTimer += Time.deltaTime;
            TargetUpdateUnpackingProgress(LiftingPlayer.connectionToClient, _unpackingTimer / _unpackingTime);
            if (_unpackingTimer >= _unpackingTime)
            {
                // 언패킹 완료, 그물 안의 오브젝트타입으로 프리팹 받아와서 스폰
                foreach (var itemData in _caughtObjectTypes)
                {
                    var obj = ItemManager.Instance.GetItemPrefab(itemData);
                    GameObject ltdObj = Instantiate(obj, transform.position, Quaternion.identity);
                    NetworkServer.Spawn(ltdObj);
                }
                _liftingPlayer.RpcHideInteractionPrompt(_liftingPlayer.connectionToClient, netId);
                _liftingPlayer.DropAction();
                NetworkServer.Destroy(gameObject); // 그물 오브젝트 파괴
            }
            return;
        }
        _unPackingProgressBar.fillAmount = 0f;
    }
    /// <summary>
    /// 그물에 담긴 오브젝트 타입 추가
    /// </summary>
    public void AddCaughtObject(ItemData itemData)
    {
        _caughtObjectTypes.Add(itemData);
    }

    /// <summary>
    /// 그물에 담긴 오브젝트 타입 초기화
    /// </summary>
    public void ClearCaughtObjects()
    {
        _caughtObjectTypes.Clear();
    }

    /// <summary>
    /// 언패킹 시작
    /// </summary>
    public void StartUnpacking()
    {
        if (!_isUnpacking)
        {
            _isUnpacking = true;
            _unpackingTimer = 0f;
        }
    }
    /// <summary>
    /// 언패킹 중지
    /// </summary>
    public void StopUnpacking()
    {
        _isUnpacking = false; // 언패킹 중지
        _unpackingTimer = 0f; // 언패킹 타이머 초기화
        TargetUpdateUnpackingProgress(LiftingPlayer.connectionToClient, 0f);
    }

    public override void Dropped()
    {
        StopUnpacking(); // 떨어뜨리면 언패킹 중지
        base.Dropped();
    }

    public override void Thrown(ThrowData throwData, uint throwerNetId)
    {
        StopUnpacking(); // 던지면 언패킹 중지
        base.Thrown(throwData, throwerNetId);
    }

    #region 클라이언트 동기화

    /// <summary>
    /// 언패킹 UI 진행률 업데이트
    /// </summary>
    [TargetRpc]
    private void TargetUpdateUnpackingProgress(NetworkConnection target, float progress)
    {
        _unPackingProgressBar.fillAmount = progress;
    }

    #endregion
}