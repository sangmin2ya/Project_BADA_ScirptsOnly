using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 게임에서 쓰이는 아이템들을 관리하는 매니저
/// 이후에 적 등 다른 오브젝트들도 관리할 수 있도록 확장 가능
/// [박상민 - 2025.10.03]
/// </summary>
public class ItemManager : Singleton<ItemManager>
{
    [Header("아이템 사전 등록")]
    [SerializeField] private ItemDataWithPrefab[] _itemDatasWithPrefab; // 아이템 데이터 배열

    // 아이템 타입과 프리팹을 매핑하는 딕셔너리들 (이후에 아이템 코드로 관리하면 하나로 통일 가능)
    private Dictionary<LTDObjectType, GameObject> _itemDictionary = new Dictionary<LTDObjectType, GameObject>(); // 모듈 외 아이템들 
    private Dictionary<ModuleType, GameObject> _moduleDictionary = new Dictionary<ModuleType, GameObject>(); // 모듈 아이템들
    private Dictionary<ScrapType, GameObject> _scrapDictionary = new Dictionary<ScrapType, GameObject>(); // 고철 아이템들

    protected override void Awake()
    {
        base.Awake();

        // 아이템 데이터 딕셔너리 초기화
        foreach (var data in _itemDatasWithPrefab)
        {

            // 모듈박스인 경우 모듈 타입으로 등록
            if (data.itemData.itemType == LTDObjectType.모듈박스)
            {
                if (!_moduleDictionary.ContainsKey(data.itemData.moduleType))
                {
                    _moduleDictionary.Add(data.itemData.moduleType, data.itemPrefab);
                }
                else
                {
                    Debug.LogWarning($"중복된 모듈 타입: {data.itemData.moduleType}");
                }
                continue;
            }
            // 고철인 경우 고철 타입으로 등록
            if (data.itemData.itemType == LTDObjectType.고철)
            {
                if (!_scrapDictionary.ContainsKey(data.itemData.scrapType))
                {
                    _scrapDictionary.Add(data.itemData.scrapType, data.itemPrefab);
                }
                else
                {
                    Debug.LogWarning($"중복된 고철 타입: {data.itemData.scrapType}");
                }
                continue;
            }
            if (!_itemDictionary.ContainsKey(data.itemData.itemType))
            {
                // 그 외 아이템들은 아이템 타입으로 등록
                _itemDictionary.Add(data.itemData.itemType, data.itemPrefab);
                continue;
            }
            else
            {
                Debug.LogWarning($"중복된 아이템 타입: {data.itemData.itemType}");
            }
        }
    }

    /// <summary>
    /// 아이템 프리팹을 가져오는 메서드
    /// </summary>
    /// <param name="itemType">아이템 타입</param>
    /// <returns>아이템 프리팹, 없으면 null 반환</returns>
    public GameObject GetItemPrefab(ItemData itemData)
    {
        // 모듈박스인 경우 모듈 타입으로 프리팹 반환
        if (itemData.itemType == LTDObjectType.모듈박스)
        {
            if (_moduleDictionary.TryGetValue(itemData.moduleType, out GameObject modulePrefab))
            {
                return modulePrefab;
            }
            Debug.LogWarning($"모듈 타입 {itemData.moduleType}에 해당하는 프리팹이 없음");
            return null;
        }
        // 고철인 경우 고철 타입으로 프리팹 반환
        if (itemData.itemType == LTDObjectType.고철)
        {
            if (_scrapDictionary.TryGetValue(itemData.scrapType, out GameObject scrapPrefab))
            {
                return scrapPrefab;
            }
            Debug.LogWarning($"고철 타입 {itemData.scrapType}에 해당하는 프리팹이 없음");
            return null;
        }
        // 그 외 아이템들은 아이템 타입으로 프리팹 반환
        if (_itemDictionary.TryGetValue(itemData.itemType, out GameObject prefab))
        {
            return prefab;
        }
        Debug.LogWarning($"아이템 타입 {itemData.itemType}에 해당하는 프리팹이 없음");
        return null;
    }

    /// <summary>
    /// 아이템 가격을 가져오는 메서드
    /// </summary>
    public uint GetItemPrice(ItemData itemData)
    {
        foreach (var data in _itemDatasWithPrefab)
        {
            if (data.itemData.itemType == itemData.itemType)
            {
                // 모듈박스인 경우 모듈 타입도 비교
                if (itemData.itemType == LTDObjectType.모듈박스)
                {
                    if (data.itemData.moduleType == itemData.moduleType)
                    {
                        return data.itemPrice;
                    }
                }
                // 고철인 경우 고철 타입도 비교
                else if (itemData.itemType == LTDObjectType.고철)
                {
                    if (data.itemData.scrapType == itemData.scrapType)
                    {
                        return data.itemPrice;
                    }
                }
                else
                {
                    return data.itemPrice;
                }
            }
        }
        Debug.LogWarning($"아이템 {itemData.itemType}에 해당하는 가격이 없음");
        return 0;
    }
    /// <summary>
    /// 아이템 아이콘을 가져오는 메서드
    /// </summary>
    /// <param name="itemData"></param>
    /// <returns></returns>
    public Sprite GetItemIcon(ItemData itemData)
    {
        foreach (var data in _itemDatasWithPrefab)
        {
            if (data.itemData.itemType == itemData.itemType)
            {
                // 모듈박스인 경우 모듈 타입도 비교
                if (itemData.itemType == LTDObjectType.모듈박스)
                {
                    if (data.itemData.moduleType == itemData.moduleType)
                    {
                        return data.itemIcon;
                    }
                }
                // 고철인 경우 고철 타입도 비교
                else if (itemData.itemType == LTDObjectType.고철)
                {
                    if (data.itemData.scrapType == itemData.scrapType)
                    {
                        return data.itemIcon;
                    }
                }
                else
                {
                    return data.itemIcon;
                }
            }
        }
        Debug.LogWarning($"아이템 {itemData.itemType}에 해당하는 아이콘이 없음");
        return null;
    }
}
/// <summary>
/// 아이템 데이터 클래스 - 만약 모듈이면 모듈 타입도 포함
/// </summary>
[Serializable]
public class ItemData
{
    public LTDObjectType itemType;
    [ConditionalEnum("itemType", 0)] public ModuleType moduleType;
    [ConditionalEnum("itemType", 1)] public ScrapType scrapType;
    // 향후에 다른 아이템 타입이 추가되면 여기에 필드 추가
}

/// <summary>
/// 아이템 타입과 프리팹을 매핑하는 클래스
/// </summary>
[Serializable]
public struct ItemDataWithPrefab
{
    public ItemData itemData;
    public Sprite itemIcon;
    public uint itemPrice;
    public GameObject itemPrefab;
}