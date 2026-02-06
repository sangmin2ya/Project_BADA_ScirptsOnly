using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 베이스캠프의 변동사항들을 저장하는 매니저 클래스
/// [박상민 - 2025.10.30]   
/// </summary>
public class BaseCampManager : NetworkSingleton<BaseCampManager>
{
    [Header("베이스캠프 정보")]
    [SyncVar(hook = nameof(OnGoldChanged))]
    [SerializeField] private uint _gold; // 현재 보유 골드
    [SyncVar(hook = nameof(OnCurrentOceanTypeChanged))]
    [SerializeField] private OceanType _currentOceanType; // 현재 선택된 바다 타입
    [Header("플레이어 스폰위치 설정")]
    [SyncVar]
    [SerializeField] private Vector2 _originSpawnPosition = new Vector2(10000, 10000); // 기본 스폰 위치 (배, 플레이어 공통)
    [SyncVar]
    [SerializeField] private Vector2 _baseCampPlayerSpawnPosition; // 베이스캠프 씬 전환시 플레이어 스폰위치
    [Header("배 스폰 위치 설정")]
    [SerializeField] private Vector2 _baseCampShipSpawnPosition;
    [SerializeField] private float _baseCampShipSpawnRotation;
    [SerializeField] private Vector2 _shipSpawnPosition; // 게임 씬 전환시 배 스폰위치
    [SerializeField] private float _shipSpawnRotation; // 게임 씬 전환시 배 스폰 회전값
    [Header("아이템 보유량 설정")]
    [SerializeField] private List<ItemPosAndRotInShip> _storedItemsInShip = new List<ItemPosAndRotInShip>(); // 배에 보관된 아이템들 위치 및 회전값
    // [SerializeField] private uint _repairKitAmount; // 보유 수리키트 개수
    // [SerializeField] private uint _cannonShellAmount; // 보유 대포알 개수
    [Header("바다 프리셋 참조")]
    [SerializeField] private OceanSpawnSetting _oceanSpawnSetting; // 현재 선택된 바다 설정 스크립터블 오브젝트

    // 접근 프로퍼티
    public uint Gold => _gold;
    public OceanType CurrentOceanType => _currentOceanType;
    public Vector2 OriginSpawnPosition => _originSpawnPosition;
    public Vector2 BaseCampSpawnPosition => _baseCampPlayerSpawnPosition;
    public Vector2 SpawnPosition => _shipSpawnPosition;
    public float SpawnRotation => _shipSpawnRotation;
    // public uint RepairKitAmount => _repairKitAmount;
    // public uint CannonShellAmount => _cannonShellAmount;

    protected override void Awake()
    {
        DontDestroyOnLoad(gameObject);  // 씬 전환 시에도 유지
        Debug.Log("BaseCampManager Awake 호출");
        base.Awake();
        Array.Sort(_oceanSpawnSetting.OceanSpawnSettings, (a, b) => a.OceanType.CompareTo(b.OceanType));
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!isServer)
        {
            return;
        }
        // 초기값 설정
        _gold = 100;
        // 씬 변경 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UIManager.Instance.InitializeUI();
    }

    [Server]
    public void AddGold(uint amount)
    {
        _gold += amount;
    }

    [Server]
    public bool UseGold(uint amount)
    {
        if (amount > _gold)
        {
            return false;
        }
        _gold -= amount;
        return true;
    }

    [Server]
    public void SetOceanType(OceanType oceanType)
    {
        _currentOceanType = oceanType;
        SetRandomSpawnPosition();
    }

    [Server]
    public void SetRandomSpawnPosition()
    {
        var setting = _oceanSpawnSetting.OceanSpawnSettings[_currentOceanType.GetHashCode()].SpawnPosAndRots;
        if (setting != null)
        {
            var randomIndex = UnityEngine.Random.Range(0, setting.Length);
            _shipSpawnPosition = setting[randomIndex].SpawnPosition;
            _shipSpawnRotation = setting[randomIndex].SpawnRotation;
        }
        else
        {
            Debug.LogError("현재 바다 타입에 대한 스폰 설정을 찾을 수 없습니다!");
            _shipSpawnPosition = Vector2.zero;
            _shipSpawnRotation = 0f;
        }
    }
    
    [Server]
    public void StoreItemInShip(ItemData itemData, Vector2 spawnLocalPosition, float spawnLocalRotation)
    {
        ItemPosAndRotInShip itemPosAndRot = new ItemPosAndRotInShip
        {
            Item = itemData,
            SpawnLocalPosition = spawnLocalPosition,
            SpawnLocalRotation = spawnLocalRotation
        };
        _storedItemsInShip.Add(itemPosAndRot);
    }

    [Server]
    public List<ItemPosAndRotInShip> GetStoredItemsAndReset()
    {
        var items = new List<ItemPosAndRotInShip>(_storedItemsInShip);
        _storedItemsInShip.Clear();
        return items;
    }

    // [Server]
    // public void AddCannonShells(uint amount)
    // {
    //     _cannonShellAmount += amount;
    // }

    // [Server]
    // public void SubstractCannonShells(uint amount)
    // {
    //     if (amount > _cannonShellAmount)
    //     {
    //         _cannonShellAmount = 0;
    //     }
    //     else
    //     {
    //         _cannonShellAmount -= amount;
    //     }
    // }

    // [Server]
    // public void AddRepairKits(uint amount)
    // {
    //     _repairKitAmount += amount;
    // }

    // [Server]
    // public void SubstractRepairKits(uint amount)
    // {
    //     if (amount > _repairKitAmount)
    //     {
    //         _repairKitAmount = 0;
    //     }
    //     else
    //     {
    //         _repairKitAmount -= amount;
    //     }
    // }

    #region SyncVar Hooks

    private void OnGoldChanged(uint oldValue, uint newValue)
    {
        if (UIManager.Instance == null) return;
        UIManager.Instance.GoldText?.SetText($"골드: {newValue}G");
    }

    private void OnCurrentOceanTypeChanged(OceanType oldValue, OceanType newValue)
    {
        if (UIManager.Instance == null) return;
        UIManager.Instance.OceanTypeText?.SetText($"{newValue}");
    }

    #endregion
    [Serializable]
    public struct ItemPosAndRotInShip
    {
        public ItemData Item;
        public Vector2 SpawnLocalPosition;
        public float SpawnLocalRotation;
    }
}