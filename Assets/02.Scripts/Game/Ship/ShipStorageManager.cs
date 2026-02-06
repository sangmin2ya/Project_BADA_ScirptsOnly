using UnityEngine;
using Mirror;
using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// 함선의 저장소를 관리하는 클래스
/// [박상민 - 2025.07.24]
/// </summary>
public class ShipStorageManager : NetworkBehaviour
{
    #region 필드

    [Header("함선 저장소")]
    [Tooltip("저장소 최대 아이템 수")]
    private static int _maxStorageItems = 15;
    [Tooltip("저장소 아이템 목록")]
    [SerializeField] private StorageItem[] _storageItems = new StorageItem[_maxStorageItems];
    [SerializeField] private int _currentItemCount = 0;

    [Header("UI 설정")]
    [SerializeField] private GameObject _storageUI;
    public GameObject StorageUI => _storageUI;
    [SerializeField] private TMP_Text _storageCountText;
    [SerializeField] private TMP_Text _storageTotalValueText;
    private Transform _storageGrid;

    #endregion

    #region Unity 생명주기
    private void Start()
    {
        // SyncList 콜백 등록
        _storageGrid = _storageUI.transform.Find("StorageGrid");

        if (isServer)
        {
            InitializeStorage();
        }

        InitializeButtons();
        _storageUI.SetActive(false); // 초기에는 UI를 비활성화

    }

    #endregion

    #region 저장소 초기화

    /// <summary>
    /// 저장소 초기화
    /// </summary>
    [Server]
    private void InitializeStorage()
    {
        for (int i = 0; i < _maxStorageItems; i++)
        {
            _storageItems[i] = new StorageItem { itemIndex = 0, LTDObjectNetId = 0 };
        }
        Debug.Log("저장소가 초기화되었습니다.");
    }

    /// <summary>
    /// 버튼에 TakeItem 기능을 연결
    /// </summary>
    private void InitializeButtons()
    {
        for (int i = 0; i < _storageGrid.childCount; i++)
        {
            Button button = _storageGrid.GetChild(i).GetComponent<Button>();
            if (button != null)
            {
                int slotIndex = i; // 지역 변수로 복사하여 클로저 문제 해결
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"슬롯 {slotIndex} 클릭됨");

                    // 슬롯 범위 체크
                    if (slotIndex >= 0 && slotIndex < _maxStorageItems)
                    {
                        StorageItem item = _storageItems[slotIndex];
                        if (item.itemIndex > 0)
                        {
                            LTDObject ltdObject = CallTakeItem(item.LTDObjectNetId);
                            if (ltdObject == null)
                            {
                                return;
                            }
                            NetworkClient.localPlayer.GetComponent<PlayerLTDController>().CallTakeItem(ltdObject);
                        }
                        else
                        {
                            Debug.Log($"슬롯 {slotIndex}은 비어있습니다.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"잘못된 슬롯 인덱스: {slotIndex}");
                    }
                });
            }
        }
    }

    #endregion

    #region UI 업데이트

    /// <summary>
    /// 아이템 변경시 UI를 업데이트합니다.
    /// </summary>
    private void UpdateStorageUI(StorageItem[] currentItems)
    {
        int itemCount = 0;
        int totalValue = 0;

        for (int i = 0; i < currentItems.Length && i < _storageGrid.childCount; i++)
        {
            StorageItem item = currentItems[i];
            Transform itemTransform = _storageGrid.GetChild(i);

            if (item.itemIndex > 0) // 아이템이 존재하는 경우
            {
                itemTransform.gameObject.SetActive(true);

                // 아이템의 스프라이트를 설정
                LTDObject ltdObject = GameNetworkManager.GetNetworkComponent<LTDObject>(item.LTDObjectNetId);
                if (ltdObject != null)
                {
                    SpriteRenderer spriteRenderer = ltdObject.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        itemTransform.GetComponentInChildren<Image>().sprite = spriteRenderer.sprite;
                    }
                }

                itemCount++;

                ISaleable saleable = GameNetworkManager.GetNetworkComponent<LTDObject>(item.LTDObjectNetId) as ISaleable;
                if (saleable != null)
                {
                    totalValue += (int)saleable.Price; // 아이템의 가치를 더함
                }
            }
            else // 빈칸인 경우
            {
                itemTransform.gameObject.SetActive(false);
                Image itemImage = itemTransform.GetComponentInChildren<Image>();
                if (itemImage != null)
                {
                    itemImage.sprite = null;
                }
            }
        }

        // UI 텍스트 업데이트
        if (_storageCountText != null)
        {
            _storageCountText.text = $"아이템: {itemCount} / {_maxStorageItems}";
        }
        if (_storageTotalValueText != null)
        {
            _storageTotalValueText.text = $"총 가치: {totalValue}";
        }
    }

    #endregion

    #region 저장소 관리

    /// <summary>
    /// 저장소가 가득 찼는지 확인합니다.
    /// </summary>
    /// <returns>가득찼으면 True 아님 False</returns>
    public bool IsStorageFull()
    {
        for (int i = 0; i < _maxStorageItems; i++)
        {
            if (_storageItems[i].itemIndex == 0) // 빈 슬롯이 있다면
            {
                return false;
            }
        }
        return true; // 빈 슬롯이 없다면 저장소가 가득 찼음
    }

    /// <summary>
    /// 클라이언트가 호출하는 아이템 가져가기
    /// 이때 숨겨져있던 LTDObject를 활성화 시키고 Lift시킴
    /// </summary>
    public LTDObject CallTakeItem(uint itemNetId)
    {
        LTDObject item = GameNetworkManager.GetNetworkComponent<LTDObject>(itemNetId);
        if (item == null)
        {
            Debug.LogError($"아이템 {itemNetId}을 찾을 수 없습니다!");
            return null;
        }
        RemoveItem(itemNetId);
        ClientInputHandler.Instance.OnForceExit(); // 인벤토리 창 닫기 (모듈에서 나오기)
        return item;
    }

    /// <summary>
    /// 클라이언트가 호출하는 아이템 추가
    /// 이때 들고있는 LTDObject를 내려놓고 아이템을 비활성화 함 (비활성화는 서버에서)
    /// </summary>
    public void CallAddItem(LTDObject item)
    {
        if (item.TryGetComponent<NetworkIdentity>(out NetworkIdentity itemIdentity))
        {
            uint itemNetId = item.netId;
            AddItem(itemNetId);
        }
        else
        {
            Debug.LogError("아이템에 NetworkIdentity가 없습니다!");
        }
    }

    #endregion

    #region 네트워크 명령

    /// <summary>
    /// 저장소에서 아이템을 제거하는 명령을 서버로 전달
    /// </summary>
    private void RemoveItem(uint itemNetId)
    {
        if (!isServer)
        {
            return;
        }

        // 아이템 제거 로직
        for (int i = 0; i < _maxStorageItems; i++)
        {
            if (_storageItems[i].LTDObjectNetId == itemNetId)
            {
                _storageItems[i] = new StorageItem { itemIndex = 0, LTDObjectNetId = 0 };
                Debug.Log($"아이템 {itemNetId}이 저장소 슬롯 {i}에서 제거되었습니다.");
                _currentItemCount = Math.Max(0, _currentItemCount - 1);
                break;
            }
        }
    }

    /// <summary>
    /// 저장소에 아이템을 추가
    /// </summary>
    private void AddItem(uint itemNetId)
    {
        if (!isServer)
        {
            return;
        }

        if (IsStorageFull())
        {
            Debug.LogWarning("저장소가 가득 차서 아이템을 추가할 수 없습니다!");
            return;
        }
        Debug.Log($"아이템 추가 요청: {itemNetId}");

        // 아이템 추가 로직
        for (int i = 0; i < _maxStorageItems; i++)
        {
            if (_storageItems[i].itemIndex == 0) // 빈 슬롯 찾기
            {
                _storageItems[i] = new StorageItem
                {
                    itemIndex = i + 1, // 1부터 시작하는 인덱스
                    LTDObjectNetId = itemNetId
                };
                _currentItemCount++;
                Debug.Log($"아이템 {itemNetId}이 저장소 슬롯 {i}에 추가되었습니다.");
                break;
            }
        }
    }

    #endregion

    #region UI 관리

    [Server]
    public void ToggleStorageUI(NetworkConnection target, bool isOpen)
    {
        RpcToggleStorageUI(target, isOpen, _storageItems);
    }

    /// <summary>
    /// 저장소 UI on/off
    /// </summary>
    [TargetRpc]
    public void RpcToggleStorageUI(NetworkConnection target, bool isOpen, StorageItem[] currentItems)
    {
        if (_storageUI != null)
        {
            _storageUI.SetActive(isOpen);
            if (isOpen)
            {
                UpdateStorageUI(currentItems);
            }
        }
        else
        {
            Debug.LogError("StorageUI가 할당되지 않았습니다!");
        }
    }
    #endregion
}

[Serializable]
public struct StorageItem
{
    public int itemIndex; // 아이템이 저장되어있는 순서 (0은 빈 슬롯)
    public uint LTDObjectNetId; // LTDObject의 네트워크 ID
}