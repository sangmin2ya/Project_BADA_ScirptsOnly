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
public class ShellStorage : Module
{
    [Header("ShellStorage")]
    [Tooltip("현재 남은 포탄 수")]
    private uint _currentShellCount = 0;
    [Tooltip("미리 생성해줄 포탄 오브젝트 수")]
    private int _preloadCount = 1;

    private Dictionary<uint, ShellObject> _shellObjects = new Dictionary<uint, ShellObject>();  // 보관함 속 포탄 목록

    protected override void Start()
    {
        base.Start();

        _moduleType = ModuleType.포탄보관함;
        // _currentShellCount = BaseCampManager.Instance.CannonShellAmount;

        if (!isServer)
        {
            return;
        }
        InitializeModule();
        InitializeShell();
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
    /// 초기 포탄 생성
    /// </summary>
    private void InitializeShell()
    {
        for (int i = 0; i < _preloadCount; i++)
        {
            SpawnShellObject();
        }
    }

    /// <summary>
    /// 창고에다가 넣었을때
    /// </summary>
    public void AddShells()
    {
        _currentShellCount++;
        SpawnShellObject();
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

    /// <summary>
    /// 포탄 오브젝트 생성
    /// </summary>
    private void SpawnShellObject()
    {
        if (_currentShellCount == 0 || _shellObjects.Count >= _currentShellCount)
        {
            return;
        }
        GameObject shellPrefab = ItemManager.Instance.GetItemPrefab(new ItemData { itemType = LTDObjectType.포탄 });
        if (shellPrefab == null)
        {
            Debug.LogError("포탄 프리팹을 ItemManager에서 가져오지 못했습니다!");
            return;
        }
        uint shellNetId = NetworkObjectSpawner.Instance.RequestSpawnObjectAtPositionAndGetId(shellPrefab, transform.position, Quaternion.identity);
        Debug.Log($"<color=red>shell: {transform.position}</color>");
        ShellObject shellObject = GameNetworkManager.GetNetworkComponent<ShellObject>(shellNetId);
        if (!_shellObjects.TryAdd(shellNetId, shellObject))
        {
            Debug.LogWarning($"잘못된 포탄 딕셔너리 추가 시도 감지. netID: {shellNetId}");
            return;
        }

        shellObject.GetComponent<Collider2D>().isTrigger = true;    // 충돌 방지를 위해서 미리 트리거를 false로 설정
        shellObject.RpcToggleLtdObject(false);                         // 생성 후 오브젝트를 투명화
        shellObject.OnDirtyObject += RemoveFromShellStorage;        // 오브젝트 상호작용 시 창고에서 빼내도록 하는 이벤트 구독
        _currentShellCount--;
    }

    /// <summary>
    /// 포탄 오브젝트를 보관함에서 빼내고 새 예비 오브젝트를 생성
    /// </summary>
    /// <param name="shellNetId"></param>
    private void RemoveFromShellStorage(uint shellNetId)
    {
        if (!_shellObjects.TryGetValue(shellNetId, out var shellObject))
        {
            Debug.LogWarning($"포탄 창고에 존재하지 않는 포탄을 딕셔너리에서 제거 시도 감지. netID: {shellNetId}");
            return;
        }
        shellObject.OnDirtyObject -= RemoveFromShellStorage;
        shellObject.RpcToggleLtdObject(true);
        //shellObject.GetComponent<Collider2D>().isTrigger = false;

        _shellObjects.Remove(shellNetId);
        SpawnShellObject();                 // 창고 리스트에 새 포탄 추가
    }
}