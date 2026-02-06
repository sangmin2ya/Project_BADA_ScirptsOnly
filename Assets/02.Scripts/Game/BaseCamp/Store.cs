using UnityEngine;
using Mirror;
using TMPro;

/// <summary>
/// 배에 실을 물건을 사고파는 상점 컴포넌트
/// [박상민 - 2025.10.30]
/// </summary>
public class Store : NetworkBehaviour, IInteractable
{
    [Tooltip("판매시 골드 환산 비율")]
    [SerializeField] private float _purchaseRatio = 0.5f; // 판매시 골드 환산 비율 (구매가의 몇 %로 판매되는지)
    [SerializeField] private GameObject _storeUI;

    #region  IInteractable 구현
    public InteractionInfo GetInteractionInfo()
    {
        // 서버에서만 정확한 정보 계산 가능
        if (!isServer)
        {
            Debug.LogWarning("GetInteractionInfo는 서버에서만 호출되어야 합니다!");
            return new InteractionInfo();
        }

        string text = "판매하기";
        string action = "Player/Interact";
        bool interactable = true;
        Vector3 position = transform.position + Vector3.up * 1f;

        return new InteractionInfo(netId, text, action, interactable, position);
    }
    #endregion

    void Awake()
    {
        _storeUI.SetActive(false);
    }

    public void SellItem(ISaleable item)
    {
        if (!isServer)
        {
            return;
        }

        uint price = (uint)Mathf.Ceil(item.Price * _purchaseRatio);
        BaseCampManager.Instance.AddGold(price);
        switch (item)
        {
            case RepairKitObject:
                //BaseCampManager.Instance.SubstractRepairKits(1);
                break;
            case ShellObject:
                //BaseCampManager.Instance.SubstractCannonShells(1);
                break;
        }
    }

    [TargetRpc]
    public void RpcToggleStoreUI(NetworkConnectionToClient conn, bool isActive, uint price)
    {
        if (_storeUI != null)
        {
            _storeUI.SetActive(isActive);
            if (isActive)
            {
                TMP_Text priceText = _storeUI.GetComponentInChildren<TMP_Text>();
                if (priceText != null)
                {
                    priceText.text = (price * _purchaseRatio).ToString() + " G";
                }
            }
        }
    }
}