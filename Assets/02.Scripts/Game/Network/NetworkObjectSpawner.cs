using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 네트워크 오브젝트를 스폰을 요청받고 실제로 스폰하는 매니저 클래스
/// [박상민 - 2025.06.11]
/// </summary>
public class NetworkObjectSpawner : NetworkSingleton<NetworkObjectSpawner>
{
    #region 필드

    [Header("<color=green>내부 LTD 오브젝트")]
    [Tooltip("스폰할 오브젝트 프리팹")]
    [SerializeField] private List<ItemData> _spawnObjectType;

    [Header("<color=red>적 오브젝트")]
    [Tooltip("적 오브젝트 프리팹")]
    [SerializeField] private GameObject[] _enemyPrefabs;

    [Header("<color=red>적 캐릭터 오브젝트")]
    [Tooltip("적 캐릭터 오브젝트 프리팹")]
    [SerializeField] private GameObject[] _enemyCharacterPrefabs;

    [Header("<color=blue>건물 오브젝트와 그 내부 맵")]
    [Tooltip("건물 오브젝트 프리팹")]
    [SerializeField] private GameObject[] _buildingPrefabs;

    [Header("<color=blue>비밀기지 오브젝트")]
    [Tooltip("비밀기지 오브젝트 프리팹")]
    [SerializeField] private GameObject _secretBasePrefab;

    #endregion

    #region 네트워크 오브젝트 스폰

    /// <summary>
    /// 네트워크 오브젝트를 스폰하는 메서드
    /// </summary>
    [Server]
    private GameObject SpawnNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("스폰할 프리팹이 null입니다!");
            return null;
        }

        GameObject obj = Instantiate(prefab, position, rotation);
        NetworkServer.Spawn(obj);
        //Debug.Log($"네트워크 오브젝트 스폰: {prefab.name} at {position}");

        // 부모 설정 (옵션) - TempShipSpawner가 존재하고 CurrentShip이 있는 경우에만
        //Debug.Log($"{TempShipSpawner.Instance.CurrentShip != null}    {TempShipSpawner.Instance.CurrentShip}");
        if (ShipSpawner.Instance != null && ShipSpawner.Instance.CurrentShip != null)
        {
            NetworkParentSync parentSync = obj.GetComponent<NetworkParentSync>();
            if (parentSync != null)
            {
                parentSync.SetNetworkParent(ShipSpawner.Instance.CurrentShip.transform);
            }
        }

        return obj;
    }

    /// <summary>
    /// 네트워크 오브젝트를 스폰해달라는 요청을 서버로 전송 (단일 오브젝트)
    /// </summary>
    private void SpawnNetworkObjectPosition(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        SpawnNetworkObject(prefab, position, rotation);
    }

    private uint SpawnNetworkObjectPositionAndGetId(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject spawnedObject = SpawnNetworkObject(prefab, position, rotation);
        return spawnedObject.GetComponent<NetworkBehaviour>().netId;
    }

    /// <summary>
    /// 네트워크 오브젝트를 스폰해달라는 요청을 서버로 전송 (기본 위치)
    /// </summary>
    private void SpawnNetworkObjectDefault(GameObject prefab)
    {
        Vector3 spawnPosition = Vector3.zero; // 기본 위치 (0, 0, 0)
        Quaternion spawnRotation = Quaternion.identity; // 기본 회전 (회전 없음)

        SpawnNetworkObject(prefab, spawnPosition, spawnRotation);
    }

    #endregion

    #region 공개 메서드

    /// <summary>
    /// 테스트용 오브젝트들을 스폰 요청
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdRequestSpawnLtdObject()
    {
        if (_spawnObjectType != null && _spawnObjectType.Count > 0)
        {
            foreach (var itemData in _spawnObjectType)
            {
                GameObject prefab = ItemManager.Instance.GetItemPrefab(itemData);
                if (prefab != null)
                {
                    SpawnNetworkObjectPosition(prefab, ShipSpawner.Instance.CurrentShip.transform.position, Quaternion.identity);
                }
                else
                {
                    Debug.LogWarning("테스트용 오브젝트 프리팹 리스트에 null 항목이 있습니다!");
                }
            }
        }
    }

    /// <summary>
    /// 적 오브젝트를 스폰 요청
    /// </summary>
    /// <param name="index">0 - 전투선, 1 - 도망선, 2 - 자코선, 3 - 잠수선, 4 - 미니선</param>
    [Command(requiresAuthority = false)]
    public void CmdRequestSpawnEnemyObject(EnemyShipType enemyShipType, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (!NetworkClient.isConnected)
        {
            Debug.LogError("클라이언트가 네트워크에 연결되어 있지 않습니다!");
            return;
        }

        if (_enemyPrefabs[(int)enemyShipType] == null)
        {
            Debug.LogError("적 오브젝트 프리팹이 설정되지 않았습니다!");
            return;
        }
        SpawnNetworkObjectPosition(_enemyPrefabs[(int)enemyShipType], spawnPosition, spawnRotation);
    }

    public void RequestSpawnBuildingobject(int index, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (!isServer)
        {
            Debug.LogError("서버에서만 건물 오브젝트를 스폰할 수 있습니다!");
            return;
        }
        if (!NetworkClient.isConnected)
        {
            Debug.LogError("클라이언트가 네트워크에 연결되어 있지 않습니다!");
            return;
        }

        if (_buildingPrefabs[index] == null)
        {
            Debug.LogError("건물 오브젝트 프리팹이 설정되지 않았습니다!");
            return;
        }
        SpawnNetworkObjectPosition(_buildingPrefabs[index], spawnPosition, spawnRotation);
    }
    /// <summary>
    /// 비밀기지 오브젝트 스폰 요청
    /// </summary>
    public void RequestSecretBaseObject(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (!isServer)
        {
            Debug.LogError("서버에서만 비밀기지 오브젝트를 스폰할 수 있습니다!");
            return;
        }
        if (!NetworkClient.isConnected)
        {
            Debug.LogError("클라이언트가 네트워크에 연결되어 있지 않습니다!");
            return;
        }

        if (_secretBasePrefab == null)
        {
            Debug.LogError("비밀기지 오브젝트 프리팹이 설정되지 않았습니다!");
            return;
        }
        SpawnNetworkObjectPosition(_secretBasePrefab, spawnPosition, spawnRotation);
    }

    /// <summary>
    /// 특정 위치에 네트워크 오브젝트를 스폰 요청
    /// </summary>
    public void RequestSpawnObjectAtPosition(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!NetworkClient.isConnected)
        {
            Debug.LogError("클라이언트가 네트워크에 연결되어 있지 않습니다!");
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("스폰할 프리팹이 null입니다!");
            return;
        }

        SpawnNetworkObjectPosition(prefab, position, rotation);
    }

    public uint RequestSpawnObjectAtPositionAndGetId(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!NetworkClient.isConnected)
        {
            Debug.LogError("클라이언트가 네트워크에 연결되어 있지 않습니다!");
            return 0;
        }

        if (prefab == null)
        {
            Debug.LogError("스폰할 프리팹이 null입니다!");
            return 0;
        }

        return SpawnNetworkObjectPositionAndGetId(prefab, position, rotation);
    }

    /// <summary>
    /// 호스트에서만 적 오브젝트 스폰 요청 
    /// 커맨드가 아닌 이유는 모든 클라에서 실행되기 때문
    /// </summary>
    public bool RequestSpawnEnemy(EnemyShipType enemyShipType, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (!isServer)
        {
            return false;
        }

        if (_enemyPrefabs[(int)enemyShipType] == null)
        {
            Debug.LogError("적 오브젝트 프리팹이 설정되지 않았습니다!");
            return false;
        }

        SpawnNetworkObjectPosition(_enemyPrefabs[(int)enemyShipType], spawnPosition, spawnRotation);
        return true;
    }

    public GameObject RequestSpawnEnemyCharacter(EnemyCharacterType enemyCharacterType, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (!isServer)
        {
            return null;
        }

        if (_enemyCharacterPrefabs[(int)enemyCharacterType] == null)
        {
            Debug.LogError("적 캐릭터 오브젝트 프리팹이 설정되지 않았습니다!");
            return null;
        }

        return SpawnNetworkObject(_enemyCharacterPrefabs[(int)enemyCharacterType], spawnPosition, spawnRotation);
    }

    #endregion
}