using System;
using UnityEngine;
using Mirror;
using TMPro;

/// <summary>
/// 엔진 모듈 - 함선의 기어 입력을 전달하는 모듈입니다.
/// [박상민 - 2025.06.27]
/// </summary>
[RequireComponent(typeof(ModuleController))]
public class Storage : Module
{
    private GameObject _storageUI;
    private ShipStorageManager _shipStorageManager;

    protected override void Start()
    {
        base.Start();

        _shipStorageManager = ShipSpawner.Instance.CurrentShip.GetComponent<ShipStorageManager>();
        _moduleType = ModuleType.창고;
        _storageUI = _shipStorageManager.StorageUI;
        _storageUI.SetActive(false); // 초기에는 UI를 비활성화

        if (!isServer)
        {
            return;
        }
        InitializeModule();
    }

    private void InitializeModule()
    {
        _moduleController = GetComponent<ModuleController>();
        _moduleController.SetConnectedModule(this);
    }

    public override void PlayerEntered(PlayerModuleController playerController)
    {
        base.PlayerEntered(playerController);
        _shipStorageManager.ToggleStorageUI(_playerModuleController.connectionToClient, true);
    }

    public override void PlayerExited()
    {
        _shipStorageManager.ToggleStorageUI(_playerModuleController.connectionToClient, false);
        base.PlayerExited();
    }
}