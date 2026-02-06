using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// 적 함선이 파괴될때 아이템을 스폰해주는 클래스
/// [박상민 - 2025.10.20.]
/// </summary>
public class RootingItemSpawner : NetworkBehaviour
{
    [SerializeField] private List<GameObject> _rootedItemPrefab;
    [SerializeField] private float _spawnRate = 0.2f;
    [SerializeField] private int _minSpawnCount = 1;
    [SerializeField] private int _maxSpawnCount = 3;
    [SerializeField] private float _spawnRadius = 2f;

    void OnDestroy()
    {
        if (!isServer) return;
        if (Random.value > _spawnRate) return;
        int spawnCount = Random.Range(_minSpawnCount, _maxSpawnCount + 1);
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject itemPrefab = _rootedItemPrefab[Random.Range(0, _rootedItemPrefab.Count)];
            Vector2 spawnPosition = (Vector2)transform.position + Random.insideUnitCircle * _spawnRadius;
            GameObject spawnedItem = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);
            NetworkServer.Spawn(spawnedItem);
        }
    }
}