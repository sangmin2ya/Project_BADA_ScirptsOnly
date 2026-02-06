using UnityEngine;
using Mirror;
using System;

/// <summary>
/// 베이스캠프에 오브젝트와 아이템을 스폰해주는 스크립트
/// </summary>
public class BaseCampSpawner : NetworkBehaviour
{
    [Header("베이스캠프에 스폰해줄 오브젝트들")]
    [SerializeField] private SpawnedObjectData[] _spawnedObjects;
    [Header("베이스캠프에 스폰해줄 아이템들")]
    [SerializeField] private SpawnedItemData[] _spawnedItems;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!isServer) return;

        // 오브젝트 스폰
        foreach (var objData in _spawnedObjects)
        {
            GameObject obj = Instantiate(objData.objectPrefab, objData.spawnPoint.position, objData.spawnPoint.rotation);
            NetworkServer.Spawn(obj);
        }

        // 아이템 스폰
        foreach (var itemData in _spawnedItems)
        {
            GameObject itemObj = Instantiate(ItemManager.Instance.GetItemPrefab(itemData.itemData), itemData.spawnPoint.position, itemData.spawnPoint.rotation);
            NetworkServer.Spawn(itemObj);
        }
    }

    [Serializable]
    private struct SpawnedItemData
    {
        public ItemData itemData;
        public Transform spawnPoint;
    }
    [Serializable]
    private struct SpawnedObjectData
    {
        public GameObject objectPrefab;
        public Transform spawnPoint;
    }
}