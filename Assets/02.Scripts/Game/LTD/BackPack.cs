using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.UI;

public class BackPack : LTDObject, ISaleable
{
    [SyncVar(hook = nameof(OnContainedItem1Changed))]
    [SerializeField] private ItemData _containedItem1;
    [SyncVar(hook = nameof(OnContainedItem2Changed))]
    [SerializeField] private ItemData _containedItem2;
    [SyncVar(hook = nameof(OnContainedItem3Changed))]
    [SerializeField] private ItemData _containedItem3;
    [Header("백팩 UI")]
    [SerializeField] private GameObject _backPackUI = null;
    [SerializeField] private List<Button> _itemTakeButtons = new List<Button>();

    [Header("가격 설정")]
    [SerializeField] private uint _price = 150;

    public uint Price => _price;

    protected override void Awake()
    {
        base.Awake();
        _ltdObjectType = LTDObjectType.가방;
        _backPackUI.SetActive(false);
        _containedItem1 = new ItemData() { itemType = LTDObjectType.기타 };
        _containedItem2 = new ItemData() { itemType = LTDObjectType.기타 };
        _containedItem3 = new ItemData() { itemType = LTDObjectType.기타 };
    }

    protected override void Update()
    {
        base.Update();
        if (_backPackUI != null && _backPackUI.activeSelf)
        {
            // 항상 카메라 위쪽을 향하게 함
            _backPackUI.transform.rotation = Quaternion.Euler(0, 0, 0);
            _backPackUI.transform.position = transform.position + Vector3.up * 1.0f;
        }
    }

    public bool CallPutItem(LTDObject lTDObject)
    {
        if (!isServer)
        {
            return false;
        }
        if(lTDObject == this)
        {
            return false;
        }

        ItemData itemData = new ItemData
        {
            itemType = lTDObject.LtdobjectType,
            scrapType = lTDObject is ScrapObject scrapObject ? scrapObject.ScrapType : ScrapType.일반고철1
        };
        if (_containedItem1.itemType == LTDObjectType.기타)
        {
            _containedItem1 = itemData;
            NetworkServer.Destroy(lTDObject.gameObject);
            return true;
        }
        else if (_containedItem2.itemType == LTDObjectType.기타)
        {
            _containedItem2 = itemData;
            NetworkServer.Destroy(lTDObject.gameObject);
            return true;
        }
        else if (_containedItem3.itemType == LTDObjectType.기타)
        {
            _containedItem3 = itemData;
            NetworkServer.Destroy(lTDObject.gameObject);
            return true;
        }
        return false;
    }

    public void CallToggleBackPack(NetworkConnectionToClient conn)
    {
        RpcToggleBackPackUI(conn);
    }

    public void CallCloseBackPack(NetworkConnectionToClient conn)
    {
        RpcCloseBackPackUI(conn);
    }

    public bool TakeItem(int slotIndex, PlayerLTDController caller)
    {
        if (!isServer)
        {
            return false;
        }

        if(caller == null)
        {
            return false;
        }

        if(caller.LiftedLTDObject != null)
        {
            Debug.LogWarning("아이템을 꺼내기 전에 들고있는 아이템을 내려놓으세요.");
            return false;
        }

        ItemData itemData;
        switch (slotIndex)
        {
            case 0:
                itemData = _containedItem1;
                _containedItem1 = new ItemData() { itemType = LTDObjectType.기타 };
                break;
            case 1:
                itemData = _containedItem2;
                _containedItem2 = new ItemData() { itemType = LTDObjectType.기타 };
                break;
            case 2:
                itemData = _containedItem3;
                _containedItem3 = new ItemData() { itemType = LTDObjectType.기타 };
                break;
            default:
                return false;
        }

        if (itemData != null)
        {
            GameObject itemObject = Instantiate(ItemManager.Instance.GetItemPrefab(itemData), transform.position + Vector3.up * 1f, Quaternion.identity);
            NetworkServer.Spawn(itemObject);
            // 꺼내고 난 후 플레이어에게 아이템 들기처리
            LTDObject ltdObject = itemObject.GetComponent<LTDObject>();
            caller.CallTakeItem(ltdObject);
            return true;
        }
        return false;
    }

    #region IInteractable 구현

    public override InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
            return new InteractionInfo();
        }

        string text = "넣기/열기";
        string action = "Player/Interact";
        bool interactable = true;
        Vector3 position = transform.position + Vector3.up * 0.5f;

        return new InteractionInfo(netId, text, action, interactable, position);
    }

    #endregion

    #region 클라이언트 동기화

    [TargetRpc]
    private void RpcToggleBackPackUI(NetworkConnectionToClient conn)
    {
        if (_backPackUI != null)
        {
            _backPackUI.SetActive(!_backPackUI.activeSelf);
        }
    }

    [TargetRpc]
    private void RpcCloseBackPackUI(NetworkConnectionToClient conn)
    {
        if (_backPackUI != null)
        {
            _backPackUI.SetActive(false);
        }
    }

    private void OnContainedItem1Changed(ItemData oldItem, ItemData newItem)
    {
        UpdateBackPackUI();
    }

    private void OnContainedItem2Changed(ItemData oldItem, ItemData newItem)
    {
        UpdateBackPackUI();
    }

    private void OnContainedItem3Changed(ItemData oldItem, ItemData newItem)
    {
        UpdateBackPackUI();
    }

    // UpdateBackPackUI에서 버튼 클릭 시 ServerInputHandler를 통해 호출하도록 변경
    private void UpdateBackPackUI()
    {
        if (_backPackUI == null || _itemTakeButtons.Count < 3)
        {
            return;
        }

        // 슬롯 0
        _itemTakeButtons[0].onClick.RemoveAllListeners();
        _itemTakeButtons[0].image.sprite = null;
        if (_containedItem1 != null && _containedItem1.itemType != LTDObjectType.기타)
        {
            _itemTakeButtons[0].onClick.AddListener(() => RequestTakeItem(0));
            //버튼에 아이콘 적용
            _itemTakeButtons[0].image.sprite = ItemManager.Instance.GetItemIcon(_containedItem1);
        }

        // 슬롯 1
        _itemTakeButtons[1].onClick.RemoveAllListeners();
        _itemTakeButtons[1].image.sprite = null;
        if (_containedItem2 != null && _containedItem2.itemType != LTDObjectType.기타)
        {
            _itemTakeButtons[1].onClick.AddListener(() => RequestTakeItem(1));
            //버튼에 아이콘 적용
            _itemTakeButtons[1].image.sprite = ItemManager.Instance.GetItemIcon(_containedItem2);
        }

        // 슬롯 2
        _itemTakeButtons[2].onClick.RemoveAllListeners();
        _itemTakeButtons[2].image.sprite = null;
        if (_containedItem3 != null && _containedItem3.itemType != LTDObjectType.기타)
        {
            _itemTakeButtons[2].onClick.AddListener(() => RequestTakeItem(2));
            //버튼에 아이콘 적용
            _itemTakeButtons[2].image.sprite = ItemManager.Instance.GetItemIcon(_containedItem3);
        }
    }

    /// <summary>
    /// 클라이언트에서 아이템 꺼내기 요청을 ServerInputHandler로 전달합니다.
    /// </summary>
    private void RequestTakeItem(int slotIndex)
    {
        // 로컬 플레이어의 netId를 가져와서 ServerInputHandler로 요청
        uint localPlayerId = NetworkClient.connection.identity.netId;
        ServerInputHandler.Instance.CmdRequestTakeItemFromBackPack(localPlayerId, netId, slotIndex);
    }

    #endregion
}