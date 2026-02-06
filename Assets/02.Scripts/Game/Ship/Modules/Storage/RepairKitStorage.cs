using System;
using UnityEngine;
using Mirror;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 포탄 보관함 모듈 클래스
/// [조우석 - 2025.07.31.]
/// </summary>
[RequireComponent(typeof(ModuleController))]
public class RepairKitStorage : Module
{
    [Header("RepairKitStorage")]
    [Tooltip("현재 남은 수리키트 수")]
    private uint _currentRepairKitCount = 0;
    [Tooltip("미리 생성해줄 수리키트 오브젝트 수")]
    private int _preloadCount = 1;

    private Dictionary<uint, RepairKitObject> _repairKitObjects = new Dictionary<uint, RepairKitObject>();  // 보관함 속 수리키트 목록

    protected override void Start()
    {
        base.Start();

        _moduleType = ModuleType.수리키트보관함;
        // _currentRepairKitCount = BaseCampManager.Instance.RepairKitAmount;

        if (!isServer)
        {
            return;
        }
        InitializeModule();
        InitializeRepairKit();
    }

    /// <summary>
    /// 모듈 정보 초기화
    /// </summary>
    private void InitializeModule()
    {
        _moduleController = GetComponent<ModuleController>();
        _moduleController.SetConnectedModule(this);
    }

    /// <summary>
    /// 초기 수리키트 생성
    /// </summary>
    private void InitializeRepairKit()
    {
        for (int i = 0; i < _preloadCount; i++)
        {
            SpawnRepairKitObject();
        }
    }

    #region 모듈
    public override void PlayerEntered(PlayerModuleController playerController)
    {
        return;
    }

    public override void PlayerExited()
    {
        return;
    }
    #endregion

    public void AddRepairKits()
    {
        _currentRepairKitCount++;
        SpawnRepairKitObject();
    }

    /// <summary>
    /// 수리키트 오브젝트 생성
    /// </summary>
    private void SpawnRepairKitObject()
    {   
        if (_currentRepairKitCount == 0 || _repairKitObjects.Count >= _currentRepairKitCount)
        {
            return;
        }
        GameObject repairKitPrefab = ItemManager.Instance.GetItemPrefab(new ItemData { itemType = LTDObjectType.수리도구 });
        if (repairKitPrefab == null)
        {
            Debug.LogError("수리키트 프리팹을 ItemManager에서 가져오지 못했습니다!");
            return;
        }
        uint repairKitNetId = NetworkObjectSpawner.Instance.RequestSpawnObjectAtPositionAndGetId(repairKitPrefab, transform.position, Quaternion.identity);
        Debug.Log($"<color=red>repairKit: {transform.position}</color>");
        RepairKitObject repairKitObject = GameNetworkManager.GetNetworkComponent<RepairKitObject>(repairKitNetId);
        if (!_repairKitObjects.TryAdd(repairKitNetId, repairKitObject))
        {
            Debug.LogWarning($"잘못된 수리키트 딕셔너리 추가 시도 감지. netID: {repairKitNetId}");
            return;
        }

        repairKitObject.GetComponent<Collider2D>().isTrigger = true;    // 충돌 방지를 위해서 미리 트리거를 false로 설정
        repairKitObject.RpcToggleLtdObject(false);                         // 생성 후 오브젝트를 투명화
        repairKitObject.OnDirtyObject += RemoveFromRepairKitStorage;        // 오브젝트 상호작용 시 창고에서 빼내도록 하는 이벤트 구독
        _currentRepairKitCount--;
    }

    /// <summary>
    /// 수리키트 오브젝트를 보관함에서 빼내고 새 예비 오브젝트를 생성
    /// </summary>
    /// <param name="repairKitNetId"></param>
    private void RemoveFromRepairKitStorage(uint repairKitNetId)
    {
        if (!_repairKitObjects.TryGetValue(repairKitNetId, out var repairKitObject))
        {
            Debug.LogWarning($"수리키트 창고에 존재하지 않는 수리키트를 딕셔너리에서 제거 시도 감지. netID: {repairKitNetId}");
            return;
        }
        repairKitObject.OnDirtyObject -= RemoveFromRepairKitStorage;
        repairKitObject.RpcToggleLtdObject(true);
        //repairKitObject.GetComponent<Collider2D>().isTrigger = false;

        _repairKitObjects.Remove(repairKitNetId);
        SpawnRepairKitObject();                 // 창고 리스트에 새 수리키트 추가
    }
}