using UnityEngine;
using Mirror;

/// <summary>
/// 플레이어의 거래 관련 컨트롤러
/// [박상민 - 2025.10.30]
/// </summary>
public class PlayerTradeController : NetworkBehaviour
{
    [SerializeField] private float _detectRadius = 2f;
    private PlayerLTDController _playerLTDController;
    private Store _detectedStore;
    private Store _previousStore;
    private ShowWindowItem _detectedShowWindowItem;
    private ShowWindowItem _previousShowWindowItem;

    void Start()
    {
        _playerLTDController = GetComponent<PlayerLTDController>();
    }

    void Update()
    {
        if (!isServer)
        {
            return;
        }

        DetectStore();
    }

    public bool CallSellItem()
    {
        if (!isServer)
        {
            return false;
        }

        LTDObject item = _playerLTDController.LiftedLTDObject;
        if (item == null || item is not ISaleable || _detectedStore == null)
        {
            return false;
        }

        _detectedStore.SellItem(item as ISaleable);
        _playerLTDController.CallResetLTDObject();
        NetworkServer.Destroy(item.gameObject); // 아이템 오브젝트 삭제

        // 판매 성공 시 UI 숨김
        if (_detectedStore is IInteractable)
        {
            RpcHideInteractionPrompt(connectionToClient, _detectedStore.netId);
        }

        return true;
    }

    public bool CallBuyItem()
    {
        bool result = BuyShowWindowItem();

        if (result && _detectedShowWindowItem != null)
        {
            // 구매 성공 시 UI 숨김
            RpcHideInteractionPrompt(connectionToClient, _detectedShowWindowItem.netId);
        }

        return result;
    }

    public bool BuyShowWindowItem()
    {
        if (!isServer || _detectedShowWindowItem == null)
        {
            return false;
        }
        // 골드가 충분하면 구매 진행
        if (BaseCampManager.Instance.UseGold(_detectedShowWindowItem.Price))
        {
            LTDObject boughtItem = _detectedShowWindowItem.BuyItem();
            if (boughtItem != null)
            {
                _playerLTDController.CallBuyItem(boughtItem);
            }
            return true;
        }
        else
        {
            Debug.LogWarning("골드가 부족합니다.");
            // 골드 부족 처리 (예: UI 알림)
            return false;
        }
    }

    /// <summary>
    /// 플레이어 콜라이더를 사용하여 근처 상점과 ShowWindowItem을 감지합니다.
    /// </summary>
    private void DetectStore()
    {
        _previousShowWindowItem = _detectedShowWindowItem;
        _previousStore = _detectedStore;

        // 초기화
        _detectedStore = null;
        _detectedShowWindowItem = null;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _detectRadius);
        float distance = float.MaxValue;

        // 감지된 모듈 중 가장 가까운 모듈을 찾습니다.
        foreach (Collider2D collider in colliders)
        {
            if (collider.TryGetComponent<Store>(out Store store))
            {
                float currentDistance = Vector2.Distance(transform.position, store.transform.position);
                if (currentDistance < distance)
                {
                    distance = currentDistance;
                    _detectedStore = store; // 가장 가까운 상점으로 업데이트
                }
            }

            if (collider.TryGetComponent<ShowWindowItem>(out ShowWindowItem showWindowItem))
            {
                float currentDistance = Vector2.Distance(transform.position, showWindowItem.transform.position);
                if (currentDistance < distance)
                {
                    distance = currentDistance;
                    _detectedShowWindowItem = showWindowItem; // 가장 가까운 ShowWindowItem으로 업데이트
                }
            }
        }

        // === 상점 UI 처리 ===
        // 상점이 감지된 경우 이전에 감지된 상점과 비교하여 UI 토글
        if (_playerLTDController.LiftedLTDObject != null && _playerLTDController.LiftedLTDObject is ISaleable)
        {
            ISaleable saleableItem = _playerLTDController.LiftedLTDObject as ISaleable;

            if (_detectedStore != null)
            {
                _detectedStore.RpcToggleStoreUI(connectionToClient, true, saleableItem.Price);

                // 상호작용 프롬프트 표시
                if (_detectedStore is IInteractable)
                {
                    var info = ((IInteractable)_detectedStore).GetInteractionInfo();
                    RpcShowInteractionPrompt(connectionToClient, info);
                }
            }
            else
            {
                if (_previousStore != null)
                {
                    _previousStore.RpcToggleStoreUI(connectionToClient, false, 0);

                    // 상호작용 프롬프트 숨김
                    if (_previousStore is IInteractable)
                    {
                        RpcHideInteractionPrompt(connectionToClient, _previousStore.netId);
                    }
                }
            }
        }
        else
        {
            // 상점이 감지되면 0원으로 UI 토글
            if (_detectedStore != null)
            {
                _detectedStore.RpcToggleStoreUI(connectionToClient, true, 0);

                // 프롬프트도 숨김
                if (_detectedStore is IInteractable)
                {
                    RpcHideInteractionPrompt(connectionToClient, _detectedStore.netId);
                }
            }
            else
            {
                if (_previousStore != null)
                {
                    _previousStore.RpcToggleStoreUI(connectionToClient, false, 0);

                    // 상호작용 프롬프트 숨김
                    if (_previousStore is IInteractable)
                    {
                        RpcHideInteractionPrompt(connectionToClient, _previousStore.netId);
                    }
                }
            }
        }

        // === ShowWindowItem UI 처리 ===
        // 감지가 안된경우에도 이전에 감지된 ShowWindowItem의 UI를 비활성화
        if (_detectedShowWindowItem != null)
        {
            if (_previousShowWindowItem != _detectedShowWindowItem)
            {
                if (_previousShowWindowItem != null)
                {
                    _previousShowWindowItem.RpcTogglePriceUI(connectionToClient, false);

                    // 상호작용 프롬프트 숨김
                    if (_previousShowWindowItem is IInteractable)
                    {
                        RpcHideInteractionPrompt(connectionToClient, _previousShowWindowItem.netId);
                    }
                }

                _detectedShowWindowItem.RpcTogglePriceUI(connectionToClient, true);

                // 상호작용 프롬프트 표시
                if (_detectedShowWindowItem is IInteractable)
                {
                    var info = ((IInteractable)_detectedShowWindowItem).GetInteractionInfo();
                    RpcShowInteractionPrompt(connectionToClient, info);
                }
            }
        }
        else
        {
            if (_previousShowWindowItem != null)
            {
                _previousShowWindowItem.RpcTogglePriceUI(connectionToClient, false);

                // 상호작용 프롬프트 숨김
                if (_previousShowWindowItem is IInteractable)
                {
                    RpcHideInteractionPrompt(connectionToClient, _previousShowWindowItem.netId);
                }
            }
        }
    }

    #region 상호작용 프롬프트 RPC

    /// <summary>
    /// 클라이언트에 상호작용 프롬프트 표시를 요청합니다
    /// </summary>
    [TargetRpc]
    private void RpcShowInteractionPrompt(NetworkConnection target, InteractionInfo info)
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
    [TargetRpc]
    private void RpcHideInteractionPrompt(NetworkConnection target, uint targetNetId)
    {
        if (InteractionPromptManager.Instance == null)
        {
            return;
        }

        InteractionPromptManager.Instance.HidePrompt(targetNetId);
    }

    #endregion
}