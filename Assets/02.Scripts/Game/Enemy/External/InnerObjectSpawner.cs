using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// 함선과 건물 내부 오브젝트 스포너
/// [박상민 - 2025.07.18]
/// </summary>
public class InnerObjectSpawner : NetworkBehaviour
{
    [Header("스폰 설정")]
    [Tooltip("스폰할 오브젝트 데이터")]
    [SerializeField] private List<SpawnInnerObjectInfo> _innerObjectInfos;
    [Tooltip("넣어줄 부모 오브젝트")]
    [SerializeField] private Transform _parentTransform;

    [Server]
    public void SpawnInnerObject()
    {
        foreach (SpawnInnerObjectInfo info in _innerObjectInfos)
        {
            if(info.InnerObjectPrefab == null || info.SpawnPoints == null || info.SpawnPoints.Count == 0)
            {
                Debug.LogWarning("스폰 정보가 올바르지 않습니다. 스폰을 건너뜁니다.");
                continue;
            }
            foreach (Transform spawnPoint in info.SpawnPoints)
            {
                if(spawnPoint == null)
                {
                    Debug.LogWarning("스폰 포인트가 null입니다. 해당 포인트 스폰을 건너뜁니다.");
                    continue;
                }
                GameObject innerObject = Instantiate(info.InnerObjectPrefab, spawnPoint.position, info.IsEnemy ? Quaternion.identity : spawnPoint.rotation);
                NetworkServer.Spawn(innerObject);
                // 부모설정
                if (innerObject.TryGetComponent<NetworkParentSync>(out NetworkParentSync parentSync))
                {
                    parentSync.SetNetworkParent(transform);
                }
                //NetworkObjectSpawner.Instance.RequestSpawnObjectAtPosition(info.InnerObjectPrefab, spawnPoint.position, spawnPoint.rotation);
            }
        }
    }
    [Serializable]
    public struct SpawnInnerObjectInfo
    {
        public bool IsEnemy;
        public GameObject InnerObjectPrefab;
        public List<Transform> SpawnPoints;
    }
}