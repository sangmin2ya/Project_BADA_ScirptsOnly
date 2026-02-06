using UnityEngine;
using Mirror;
using TMPro;

/// <summary>
/// 상점에 진열되어있는 아이템
/// [박상민 - 2025.10.30]
/// </summary>
public class ShowWindowItem : NetworkBehaviour, IInteractable
{
    [SerializeField] private ItemData _itemData;
    [SerializeField] private GameObject _priceUI;

    #region  IInteractable 구현
    public InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
            return new InteractionInfo();
        }

        string text = "구매하기";
        string action = "Player/LiftDrop";
        bool interactable = true;
        Vector3 position = transform.position + Vector3.up * 1f;

        return new InteractionInfo(netId, text, action, interactable, position);
    }

    #endregion

    public uint Price => ItemManager.Instance.GetItemPrice(_itemData);

    void Start()
    {
        if (_priceUI != null)
        {
            _priceUI.GetComponentInChildren<TMP_Text>().text = Price.ToString() + " G";
            _priceUI.SetActive(false);
        }
    }

    public LTDObject BuyItem()
    {
        if (!isServer)
        {
            return null;
        }
        // 구매 성공하면 아이템 스폰 후, 네트워크에 등록
        GameObject itemObject = Instantiate(ItemManager.Instance.GetItemPrefab(_itemData));
        NetworkServer.Spawn(itemObject);
        LTDObject ltdObject = itemObject.GetComponent<LTDObject>();
        switch (_itemData.itemType)
        {
            case LTDObjectType.수리도구:
                //BaseCampManager.Instance.AddRepairKits(1);
                break;
            case LTDObjectType.포탄:
                //BaseCampManager.Instance.AddCannonShells(1);
                break;
            default:
                break;
        }
        return ltdObject;
    }

    [TargetRpc]
    public void RpcTogglePriceUI(NetworkConnectionToClient conn, bool isActive)
    {
        if (_priceUI != null)
            _priceUI.SetActive(isActive);
    }
}
